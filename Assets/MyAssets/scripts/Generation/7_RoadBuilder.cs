// =============================================================================
// FILE 7: RoadBuilder.cs
// Concrete IRoadBuilder: spline construction, pathfinding, heightmap carving,
// and pre-sampled SplinePoint cache for jobs.
// =============================================================================

using System;
using System.Collections.Generic;
using ProceduralTerrain.Core;
using ProceduralTerrain.Debugging;
using ProceduralTerrain.Jobs;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace ProceduralTerrain.Road
{
    /// <summary>
    /// Builds and maintains the main road spline across all generated chunks.
    /// Server-authoritative: control points are synced to clients via Mirror.
    /// Clients rebuild the CatmullRomSpline locally from the synced points.
    /// </summary>
    public sealed class RoadBuilder : IRoadBuilder, IDisposable
    {
        private const string LOG_TAG = "RoadBuilder";

        private readonly WorldSettings   _settings;
        private readonly ITerrainLogger  _logger;
        private readonly RoadPathfinder  _pathfinder;
        private readonly CatmullRomSpline _spline;

        // Flat list of sampled SplinePoints (world-space, for jobs and Gizmos)
        private readonly List<SplinePoint> _sampledPoints = new();
        // NativeArray mirror of _sampledPoints.Position — kept in sync for Burst jobs
        private NativeArray<float3>        _nativeSplinePoints;
        private bool                       _nativeDirty = true;

        // Per-chunk control-point lookup (server only for generation; clients use same data)
        private readonly Dictionary<Vector2Int, List<int>> _chunkCPIndices = new();

        public float TotalLength => _spline.TotalLength;
        public IReadOnlyList<Vector3> ControlPoints => _spline.ControlPoints;

        public IReadOnlyList<Vector3> GetControlPoints()
        {
            return _spline.ControlPoints;
        }

        // ---- Constructor -----------------------------------------------------

        public RoadBuilder(WorldSettings settings, ITerrainLogger logger)
        {
            _settings   = settings   ?? throw new ArgumentNullException(nameof(settings));
            _logger     = logger     ?? throw new ArgumentNullException(nameof(logger));
            _pathfinder = new RoadPathfinder(logger);
            _spline     = new CatmullRomSpline();
        }

        // ---- IRoadBuilder ---------------------------------------------------

        public IReadOnlyList<Vector3> GetControlPointsForChunk(Vector2Int chunkCoord)
        {
            if (!_chunkCPIndices.TryGetValue(chunkCoord, out var indices))
                return Array.Empty<Vector3>();

            var points = new List<Vector3>(indices.Count);
            var allCP  = _spline.ControlPoints;
            foreach (int idx in indices)
            {
                if (idx >= 0 && idx < allCP.Count)
                    points.Add(allCP[idx]);
            }
            return points;
        }

        /// <summary>
        /// Routes the road through a new chunk by pathfinding across its heightmap,
        /// then appends the discovered control points to the global spline.
        /// </summary>
        public void ExtendRoadThroughChunk(
            Vector2Int chunkCoord, float[] heightmap,
            int resolution, float worldSize, float heightScale)
        {
            using var scope = _logger.BeginTimed(LOG_TAG,
                $"Road extension through chunk [{chunkCoord.x},{chunkCoord.y}]");

            Vector2 origin = new Vector2(chunkCoord.x * worldSize, chunkCoord.y * worldSize);

            // Entry point: center of the previous chunk's exit edge (or world start)
            Vector2 entryXZ = GetChunkEntryPoint(chunkCoord, worldSize);
            // Exit point: center of the next chunk's entry edge
            Vector2 exitXZ  = new Vector2(origin.x + worldSize * 0.5f, origin.y + worldSize);

            _logger.Log(LogLevel.Verbose, LOG_TAG,
                $"Chunk [{chunkCoord.x},{chunkCoord.y}]: pathfinding from {entryXZ} → {exitXZ}");

            List<Vector3> pathPoints = _pathfinder.FindPath(
                entryXZ, exitXZ,
                heightmap, resolution,
                origin, worldSize,
                heightScale, _settings.RoadMaxSlopeDeg
            );

            if (pathPoints == null || pathPoints.Count < 2)
            {
                _logger.LogWarning(LOG_TAG,
                    $"Road Spline FAILED to connect at Chunk [{chunkCoord.x},{chunkCoord.y}]. " +
                    $"Using straight-line fallback.");
                pathPoints = new List<Vector3>
                {
                    new Vector3(entryXZ.x, SampleHeight(entryXZ, heightmap, resolution, origin, worldSize) * heightScale, entryXZ.y),
                    new Vector3(exitXZ.x,  SampleHeight(exitXZ,  heightmap, resolution, origin, worldSize) * heightScale, exitXZ.y)
                };
            }

            // Smooth the Y values: road should not have sharp dips
            SmoothRoadHeights(pathPoints);

            // Append to spline (first chunk needs a "ghost" point before index 0)
            int startIndex = _spline.ControlPoints.Count;
            if (startIndex == 0)
            {
                // Catmull-Rom needs a leading ghost point
                Vector3 ghost = pathPoints[0] - (pathPoints[1] - pathPoints[0]);
                _spline.AddPoint(ghost);
                startIndex = 1;
            }

            var chunkIndices = new List<int>();
            foreach (var pt in pathPoints)
            {
                chunkIndices.Add(_spline.ControlPoints.Count);
                _spline.AddPoint(pt);
            }

            // Add trailing ghost if this is the final point
            if (_spline.ControlPoints.Count >= 2)
            {
                var last      = _spline.ControlPoints[_spline.ControlPoints.Count - 1];
                var secondLast= _spline.ControlPoints[_spline.ControlPoints.Count - 2];
                _spline.AddPoint(last + (last - secondLast));
            }

            _chunkCPIndices[chunkCoord] = chunkIndices;
            _nativeDirty = true;

            _logger.Log(LogLevel.Info, LOG_TAG,
                $"Chunk [{chunkCoord.x},{chunkCoord.y}]: Road extended +{pathPoints.Count} control points. " +
                $"Total spline length: {_spline.TotalLength:F1} m");

            RebuildSampledPoints();
        }

        public void CarveRoadIntoHeightmap(Vector2Int chunkCoord, int resolution,
                                           ref NativeArray<float> heightmap)
        {
            using var scope = _logger.BeginTimed(LOG_TAG,
                $"Carve road into chunk [{chunkCoord.x},{chunkCoord.y}] heightmap");

            // Ensure native spline points are fresh
            EnsureNativeSplinePoints();

            if (_nativeSplinePoints.Length == 0)
            {
                _logger.LogWarning(LOG_TAG, "No spline points cached — road carving skipped.");
                return;
            }

            float worldSize = _settings.ChunkWorldSize;
            Vector2 origin  = new Vector2(chunkCoord.x * worldSize, chunkCoord.y * worldSize);
            float heightScale = GetHeightScaleFromBiome();

            var job = new RoadCarvingJob
            {
                Resolution        = resolution,
                WorldSize         = worldSize,
                ChunkOriginXZ     = new float2(origin.x, origin.y),
                HeightScale       = heightScale,
                RoadHalfWidth     = _settings.RoadWidth * 0.5f / heightScale, // Normalized
                ShoulderHalfWidth = _settings.RoadShoulderWidth / heightScale,
                SplinePoints      = _nativeSplinePoints,
                Heightmap         = heightmap
            };

            job.Schedule(resolution * resolution, 64).Complete();

            _logger.Log(LogLevel.Info, LOG_TAG,
                $"Chunk [{chunkCoord.x},{chunkCoord.y}]: Road carved into heightmap " +
                $"({_nativeSplinePoints.Length} spline samples used).");
        }

        public void SampleSpline(float t, out Vector3 position, out Vector3 forward)
        {
            if (_spline.ControlPoints.Count < 4)
            {
                position = Vector3.zero;
                forward  = Vector3.forward;
                return;
            }
            position = _spline.Evaluate(t);
            forward  = _spline.EvaluateTangent(t);
        }

        public Vector3 NearestPointOnSpline(Vector3 worldPos, out float distanceAlongSpline)
        {
            if (_spline.ControlPoints.Count < 4)
            {
                distanceAlongSpline = 0f;
                return worldPos;
            }
            return _spline.NearestPoint(worldPos, out distanceAlongSpline);
        }

        // ---- Sampling helpers (for POI/foliage exclusion) -------------------

        /// <summary>
        /// Returns all sampled SplinePoints whose XZ footprint intersects the given chunk rect.
        /// </summary>
        public List<SplinePoint> GetSampledPointsForChunk(Vector2Int chunkCoord)
        {
            float worldSize = _settings.ChunkWorldSize;
            var   rect      = new Rect(chunkCoord.x * worldSize, chunkCoord.y * worldSize,
                                       worldSize, worldSize);
            var   result    = new List<SplinePoint>();
            foreach (var sp in _sampledPoints)
            {
                if (rect.Contains(new Vector2(sp.Position.x, sp.Position.z)))
                    result.Add(sp);
            }
            return result;
        }

        // ---- NativeArray mirror for Burst jobs ------------------------------

        private void EnsureNativeSplinePoints()
        {
            if (!_nativeDirty) return;
            if (_nativeSplinePoints.IsCreated) _nativeSplinePoints.Dispose();

            var points = new NativeArray<float3>(_sampledPoints.Count, Allocator.Persistent);
            for (int i = 0; i < _sampledPoints.Count; i++)
                points[i] = _sampledPoints[i].Position;

            _nativeSplinePoints = points;
            _nativeDirty        = false;

            _logger.Log(LogLevel.Verbose, LOG_TAG,
                $"Native spline point cache rebuilt: {_sampledPoints.Count} samples.");
        }

        // ---- Spline re-sampling ---------------------------------------------

        private void RebuildSampledPoints()
        {
            _sampledPoints.Clear();
            if (_spline.ControlPoints.Count < 4) return;

            float total     = _spline.TotalLength;
            float step      = _settings.RoadSegmentLength;
            int   numSamples= Mathf.Max(2, Mathf.CeilToInt(total / step));

            float dist = 0f;
            for (int i = 0; i <= numSamples; i++)
            {
                float t       = Mathf.Clamp01((float)i / numSamples);
                Vector3 pos   = _spline.Evaluate(t);
                Vector3 fwd   = _spline.EvaluateTangent(t);
                Vector3 right = Vector3.Cross(fwd, Vector3.up).normalized;

                _sampledPoints.Add(new SplinePoint
                {
                    Position           = new float3(pos.x, pos.y, pos.z),
                    Tangent            = new float3(fwd.x, fwd.y, fwd.z),
                    Normal             = new float3(0, 1, 0),
                    DistanceAlongSpline= dist,
                    RoadWidth          = _settings.RoadWidth
                });
                if (i < numSamples) dist += step;
            }

            _nativeDirty = true;
            _logger.Log(LogLevel.Verbose, LOG_TAG, $"Spline re-sampled: {_sampledPoints.Count} points.");
        }

        // ---- Utility --------------------------------------------------------

        private Vector2 GetChunkEntryPoint(Vector2Int coord, float worldSize)
        {
            if (_sampledPoints.Count > 0)
            {
                var lastPt = _sampledPoints[_sampledPoints.Count - 1].Position;
                return new Vector2(lastPt.x, lastPt.z);
            }
            // Start: center bottom of chunk
            return new Vector2(coord.x * worldSize + worldSize * 0.5f, coord.y * worldSize);
        }

        private float SampleHeight(Vector2 worldXZ, float[] heightmap,
            int resolution, Vector2 origin, float worldSize)
        {
            float nx = (worldXZ.x - origin.x) / worldSize * (resolution - 1);
            float nz = (worldXZ.y - origin.y) / worldSize * (resolution - 1);
            int   ix = Mathf.Clamp(Mathf.RoundToInt(nx), 0, resolution - 1);
            int   iz = Mathf.Clamp(Mathf.RoundToInt(nz), 0, resolution - 1);
            return heightmap[iz * resolution + ix];
        }

        private void SmoothRoadHeights(List<Vector3> points)
        {
            // 3-pass Laplacian smoothing on Y
            for (int pass = 0; pass < 3; pass++)
            for (int i = 1; i < points.Count - 1; i++)
            {
                float avg = (points[i - 1].y + points[i].y + points[i + 1].y) / 3f;
                points[i] = new Vector3(points[i].x, avg, points[i].z);
            }
        }

        private float GetHeightScaleFromBiome()
        {
            // Average across registered biomes — good enough for normalization
            if (_settings.BiomeDefinitions == null || _settings.BiomeDefinitions.Length == 0)
                return 50f;
            float total = 0f;
            foreach (var b in _settings.BiomeDefinitions) total += b.HeightScale;
            return total / _settings.BiomeDefinitions.Length;
        }

        // ---- IDisposable ----------------------------------------------------

        private bool _disposed;
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_nativeSplinePoints.IsCreated) _nativeSplinePoints.Dispose();
            _logger.Log(LogLevel.Info, LOG_TAG, "RoadBuilder disposed.");
        }
    }
}
