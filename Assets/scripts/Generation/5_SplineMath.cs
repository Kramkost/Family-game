// =============================================================================
// FILE 5: SplineMath.cs
// Catmull-Rom spline, arc-length re-parameterization, and A* road pathfinder.
// Pure math — no MonoBehaviour, no Unity Object lifetime concerns.
// =============================================================================

using System;
using System.Collections.Generic;
using ProceduralTerrain.Debugging;
using Unity.Mathematics;
using UnityEngine;

namespace ProceduralTerrain.Road
{
    // =========================================================================
    // Catmull-Rom Spline
    // =========================================================================

    /// <summary>
    /// Centripetal Catmull-Rom spline over a list of Vector3 control points.
    /// Provides position, tangent, and arc-length queries.
    /// Alpha = 0.5 → centripetal (avoids cusps and self-intersections).
    /// </summary>
    public sealed class CatmullRomSpline
    {
        private readonly List<Vector3> _controlPoints;
        private readonly List<float>   _arcLengths;     // Cumulative per segment
        private          float         _totalLength;
        private          int           _lut_samples = 100; // Samples per segment for LUT

        public float TotalLength => _totalLength;
        public IReadOnlyList<Vector3> ControlPoints => _controlPoints.AsReadOnly();

        public CatmullRomSpline()
        {
            _controlPoints = new List<Vector3>();
            _arcLengths    = new List<float>();
        }

        public CatmullRomSpline(IEnumerable<Vector3> points) : this()
        {
            foreach (var p in points) AddPoint(p);
        }

        // ---- Control-point management ----------------------------------------

        public void AddPoint(Vector3 point)
        {
            _controlPoints.Add(point);
            if (_controlPoints.Count >= 4)
                RebuildArcLengthLUT();
        }

        public void InsertPoint(int index, Vector3 point)
        {
            _controlPoints.Insert(index, point);
            RebuildArcLengthLUT();
        }

        public void Clear()
        {
            _controlPoints.Clear();
            _arcLengths.Clear();
            _totalLength = 0f;
        }

        // ---- Core spline math ------------------------------------------------

        /// <summary>
        /// Evaluate spline at global parameter t ∈ [0, 1] (uniform over full spline).
        /// Returns world-space position.
        /// </summary>
        public Vector3 Evaluate(float t)
        {
            if (_controlPoints.Count < 4)
                throw new InvalidOperationException("Spline needs at least 4 control points.");

            int   segments   = _controlPoints.Count - 3;
            float scaledT    = Mathf.Clamp01(t) * segments;
            int   seg        = Mathf.Min((int)scaledT, segments - 1);
            float localT     = scaledT - seg;

            return EvaluateSegment(seg, localT);
        }

        /// <summary>
        /// Evaluate at arc-length distance (meters from start).
        /// Uses the pre-built LUT for O(log n) lookup.
        /// </summary>
        public Vector3 EvaluateAtDistance(float distance)
        {
            if (_arcLengths.Count == 0) return _controlPoints.Count > 0 ? _controlPoints[0] : Vector3.zero;
            float t = ArcLengthToT(distance);
            return Evaluate(t);
        }

        public Vector3 EvaluateTangent(float t)
        {
            if (_controlPoints.Count < 4) return Vector3.forward;
            int   segments = _controlPoints.Count - 3;
            float scaledT  = Mathf.Clamp01(t) * segments;
            int   seg      = Mathf.Min((int)scaledT, segments - 1);
            float localT   = scaledT - seg;
            return EvaluateSegmentTangent(seg, localT).normalized;
        }

        // ---- Nearest point query --------------------------------------------

        /// <summary>
        /// Find the point on the spline nearest to worldPos.
        /// Uses coarse LUT search then refines with Newton's method.
        /// Returns the nearest world-space position; outputs distanceAlongSpline.
        /// </summary>
        public Vector3 NearestPoint(Vector3 worldPos, out float distanceAlongSpline)
        {
            if (_controlPoints.Count < 4)
            {
                distanceAlongSpline = 0f;
                return _controlPoints.Count > 0 ? _controlPoints[0] : Vector3.zero;
            }

            int   totalSamples = (_controlPoints.Count - 3) * _lut_samples;
            float bestT        = 0f;
            float bestDist     = float.MaxValue;

            for (int i = 0; i <= totalSamples; i++)
            {
                float t   = (float)i / totalSamples;
                float d   = Vector3.SqrMagnitude(Evaluate(t) - worldPos);
                if (d < bestDist) { bestDist = d; bestT = t; }
            }

            // Refine with bisection
            float step = 1f / totalSamples;
            for (int iter = 0; iter < 8; iter++)
            {
                step *= 0.5f;
                float tA = bestT - step;
                float tB = bestT + step;
                float dA = Vector3.SqrMagnitude(Evaluate(Mathf.Clamp01(tA)) - worldPos);
                float dB = Vector3.SqrMagnitude(Evaluate(Mathf.Clamp01(tB)) - worldPos);
                if (dA < bestDist) { bestDist = dA; bestT = Mathf.Clamp01(tA); }
                if (dB < bestDist) { bestDist = dB; bestT = Mathf.Clamp01(tB); }
            }

            distanceAlongSpline = bestT * _totalLength;
            return Evaluate(bestT);
        }

        // ---- Private helpers -------------------------------------------------

        private Vector3 EvaluateSegment(int seg, float t)
        {
            Vector3 p0 = _controlPoints[seg];
            Vector3 p1 = _controlPoints[seg + 1];
            Vector3 p2 = _controlPoints[seg + 2];
            Vector3 p3 = _controlPoints[seg + 3];
            return CatmullRom(p0, p1, p2, p3, t);
        }

        private Vector3 EvaluateSegmentTangent(int seg, float t)
        {
            Vector3 p0 = _controlPoints[seg];
            Vector3 p1 = _controlPoints[seg + 1];
            Vector3 p2 = _controlPoints[seg + 2];
            Vector3 p3 = _controlPoints[seg + 3];
            return CatmullRomTangent(p0, p1, p2, p3, t);
        }

        /// <summary>Standard Catmull-Rom formula (alpha = 0.5, centripetal).</summary>
        private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;

            return 0.5f * (
                (2f * p1) +
                (-p0 + p2) * t +
                (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                (-p0 + 3f * p1 - 3f * p2 + p3) * t3
            );
        }

        private static Vector3 CatmullRomTangent(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t;
            return 0.5f * (
                (-p0 + p2) +
                (2f * (2f * p0 - 5f * p1 + 4f * p2 - p3)) * t +
                3f * (-p0 + 3f * p1 - 3f * p2 + p3) * t2
            );
        }

        private void RebuildArcLengthLUT()
        {
            _arcLengths.Clear();
            _totalLength = 0f;

            int segments = _controlPoints.Count - 3;
            for (int seg = 0; seg < segments; seg++)
            {
                Vector3 prev    = EvaluateSegment(seg, 0f);
                float   segLen  = 0f;
                for (int s = 1; s <= _lut_samples; s++)
                {
                    float   localT = (float)s / _lut_samples;
                    Vector3 curr   = EvaluateSegment(seg, localT);
                    segLen        += Vector3.Distance(prev, curr);
                    prev           = curr;
                }
                _arcLengths.Add(segLen);
                _totalLength += segLen;
            }
        }

        private float ArcLengthToT(float distance)
        {
            distance = Mathf.Clamp(distance, 0f, _totalLength);
            float   accum    = 0f;
            int     segments = _arcLengths.Count;
            for (int seg = 0; seg < segments; seg++)
            {
                float segLen = _arcLengths[seg];
                if (accum + segLen >= distance)
                {
                    float localFrac = (distance - accum) / segLen;
                    return ((float)seg + localFrac) / segments;
                }
                accum += segLen;
            }
            return 1f;
        }
    }

    // =========================================================================
    // Road Pathfinder — A* on a height-grid
    // =========================================================================

    /// <summary>
    /// Finds road control points across a heightmap grid using A* search.
    /// Penalizes steep slopes to produce a road that "makes sense" geographically.
    /// Designed for chunk-to-chunk routing; called once per new chunk expansion.
    /// </summary>
    public sealed class RoadPathfinder
    {
        private readonly ITerrainLogger _logger;
        private const    string         LOG_TAG  = "RoadPathfinder";
        private const    int            MAX_ITER = 50_000;

        public RoadPathfinder(ITerrainLogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Finds a path from startWorldXZ to targetWorldXZ using the provided heightmap.
        /// HeightGrid is normalized [0,1]. HeightScale converts to world meters.
        /// Returns a list of world-space Vector3 positions for spline control points.
        /// </summary>
        public List<Vector3> FindPath(
            Vector2 startWorldXZ, Vector2 targetWorldXZ,
            float[] heightGrid, int gridResolution,
            Vector2 gridOriginXZ, float gridWorldSize,
            float heightScale, float maxSlopeDeg)
        {
            _logger.Log(LogLevel.Info, LOG_TAG,
                $"Pathfinding from {startWorldXZ} to {targetWorldXZ}  | grid={gridResolution}x{gridResolution}");

            Vector2Int startCell  = WorldToCell(startWorldXZ,  gridOriginXZ, gridWorldSize, gridResolution);
            Vector2Int targetCell = WorldToCell(targetWorldXZ, gridOriginXZ, gridWorldSize, gridResolution);

            // --- A* ---
            var openSet   = new SortedList<float, Vector2Int>(Comparer<float>.Create((a, b) =>
                a.CompareTo(b) == 0 ? 1 : a.CompareTo(b)));
            var gScore    = new Dictionary<Vector2Int, float>();
            var cameFrom  = new Dictionary<Vector2Int, Vector2Int>();
            var closed    = new HashSet<Vector2Int>();

            gScore[startCell] = 0f;
            openSet.Add(Heuristic(startCell, targetCell), startCell);

            int iterations = 0;
            while (openSet.Count > 0 && iterations < MAX_ITER)
            {
                iterations++;
                var (_, current) = (openSet.Keys[0], openSet.Values[0]);
                openSet.RemoveAt(0);

                if (current == targetCell)
                {
                    _logger.Log(LogLevel.Info, LOG_TAG,
                        $"Path found after {iterations} iterations.");
                    return ReconstructPath(cameFrom, current, gridOriginXZ, gridWorldSize,
                        gridResolution, heightGrid, heightScale);
                }

                closed.Add(current);

                foreach (var neighbor in GetNeighbors(current, gridResolution))
                {
                    if (closed.Contains(neighbor)) continue;

                    float slopePenalty = ComputeSlopePenalty(
                        current, neighbor, heightGrid, gridResolution,
                        gridWorldSize, heightScale, maxSlopeDeg);

                    if (slopePenalty >= float.MaxValue) continue; // Impassable

                    float tentativeG = gScore.GetValueOrDefault(current, float.MaxValue) +
                                       CellDistance(current, neighbor) + slopePenalty;

                    float prevG = gScore.GetValueOrDefault(neighbor, float.MaxValue);
                    if (tentativeG < prevG)
                    {
                        cameFrom[neighbor] = current;
                        gScore[neighbor]   = tentativeG;
                        float f = tentativeG + Heuristic(neighbor, targetCell);
                        if (!openSet.ContainsValue(neighbor))
                            openSet.Add(f, neighbor);
                    }
                }
            }

            _logger.LogWarning(LOG_TAG,
                $"A* exhausted {iterations} iterations without reaching target. " +
                $"Using straight-line fallback.");

            return FallbackStraightLine(startWorldXZ, targetWorldXZ, heightGrid,
                gridResolution, gridOriginXZ, gridWorldSize, heightScale);
        }

        // ---- Private helpers -------------------------------------------------

        private static Vector2Int WorldToCell(Vector2 world, Vector2 origin,
            float worldSize, int resolution)
        {
            float nx = (world.x - origin.x) / worldSize;
            float nz = (world.y - origin.y) / worldSize;
            return new Vector2Int(
                Mathf.Clamp(Mathf.RoundToInt(nx * (resolution - 1)), 0, resolution - 1),
                Mathf.Clamp(Mathf.RoundToInt(nz * (resolution - 1)), 0, resolution - 1)
            );
        }

        private static Vector3 CellToWorld(Vector2Int cell, Vector2 origin,
            float worldSize, int resolution, float[] heightmap, float heightScale)
        {
            float x = origin.x + (float)cell.x / (resolution - 1) * worldSize;
            float z = origin.y + (float)cell.y / (resolution - 1) * worldSize;
            float y = heightmap[cell.y * resolution + cell.x] * heightScale;
            return new Vector3(x, y, z);
        }

        private static float Heuristic(Vector2Int a, Vector2Int b)
            => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y); // Manhattan

        private static float CellDistance(Vector2Int a, Vector2Int b)
            => a == b ? 0f : (Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y) > 1 ? 1.414f : 1f);

        private static IEnumerable<Vector2Int> GetNeighbors(Vector2Int c, int res)
        {
            for (int dx = -1; dx <= 1; dx++)
            for (int dz = -1; dz <= 1; dz++)
            {
                if (dx == 0 && dz == 0) continue;
                int nx = c.x + dx, nz = c.y + dz;
                if (nx >= 0 && nx < res && nz >= 0 && nz < res)
                    yield return new Vector2Int(nx, nz);
            }
        }

        private static float ComputeSlopePenalty(
            Vector2Int from, Vector2Int to,
            float[] heightGrid, int resolution,
            float worldSize, float heightScale, float maxSlopeDeg)
        {
            float hFrom = heightGrid[from.y * resolution + from.x] * heightScale;
            float hTo   = heightGrid[to.y   * resolution + to.x  ] * heightScale;
            float cellWorldSize = worldSize / (resolution - 1);
            float dist  = CellDistance(from, to) * cellWorldSize;
            float rise  = Mathf.Abs(hTo - hFrom);
            float slope = Mathf.Atan2(rise, dist) * Mathf.Rad2Deg;

            if (slope > maxSlopeDeg * 2f) return float.MaxValue; // Hard block
            if (slope > maxSlopeDeg)      return 100f + slope * 10f; // Heavy penalty

            return slope; // Small penalty favors flat routes
        }

        private static List<Vector3> ReconstructPath(
            Dictionary<Vector2Int, Vector2Int> cameFrom,
            Vector2Int current,
            Vector2 gridOrigin, float gridWorldSize, int resolution,
            float[] heightGrid, float heightScale)
        {
            var path = new List<Vector2Int> { current };
            while (cameFrom.TryGetValue(current, out var prev))
            {
                path.Add(prev);
                current = prev;
            }
            path.Reverse();

            // Downsample — keep every N-th cell to reduce control-point density
            const int stride = 8;
            var result = new List<Vector3>();
            for (int i = 0; i < path.Count; i += stride)
                result.Add(CellToWorld(path[i], gridOrigin, gridWorldSize, resolution, heightGrid, heightScale));

            if (result.Count == 0 || result[result.Count - 1] != CellToWorld(path[path.Count - 1],
                gridOrigin, gridWorldSize, resolution, heightGrid, heightScale))
            {
                result.Add(CellToWorld(path[path.Count - 1], gridOrigin, gridWorldSize,
                    resolution, heightGrid, heightScale));
            }
            return result;
        }

        private static List<Vector3> FallbackStraightLine(
            Vector2 start, Vector2 end,
            float[] heightGrid, int resolution,
            Vector2 gridOrigin, float worldSize, float heightScale)
        {
            const int steps = 10;
            var result = new List<Vector3>();
            for (int i = 0; i <= steps; i++)
            {
                float t   = (float)i / steps;
                Vector2 p = Vector2.Lerp(start, end, t);
                var cell  = WorldToCell(p, gridOrigin, worldSize, resolution);
                float y   = heightGrid[cell.y * resolution + cell.x] * heightScale;
                result.Add(new Vector3(p.x, y, p.y));
            }
            return result;
        }
    }
}
