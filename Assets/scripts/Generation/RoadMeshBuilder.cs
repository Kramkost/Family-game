// =============================================================================
// FILE: RoadMeshBuilder.cs  (NEW — Requirement 1)
//
// Builds a procedural road mesh along a set of SplinePoints.
// Chosen approach: Variant A (procedural mesh) — superior to prefab segments:
//   ✔ No Z-fighting between adjacent prefab tiles
//   ✔ Perfect UV continuity along the entire road length
//   ✔ Single draw call per chunk (GPU instancing friendly)
//   ✔ Mesh snaps exactly to the carved heightmap
//
// Per chunk: one MeshFilter + MeshRenderer GameObject as child of terrain.
// Lifecycle: destroyed when TerrainChunk.Unload() is called.
//
// UV layout:
//   U (0→1): across road width (left edge = 0, right edge = 1)
//   V (0→N): along road, tiled every RoadUVTileDistance meters
//
// =============================================================================

using System;
using System.Collections.Generic;
using ProceduralTerrain.Core;
using ProceduralTerrain.Debugging;
using Unity.Mathematics;
using UnityEngine;

namespace ProceduralTerrain.Road
{
    /// <summary>
    /// Builds and owns the procedural road mesh GameObjects for each chunk.
    /// One mesh per chunk, destroyed on chunk unload.
    /// Thread-safe for the geometry calculation; Unity API calls are main-thread only.
    /// </summary>
    public sealed class RoadMeshBuilder : IDisposable
    {
        private const string LOG_TAG = "RoadMeshBuilder";

        private readonly WorldSettings  _settings;
        private readonly ITerrainLogger _logger;

        // Track all spawned road mesh objects by chunk coord for cleanup
        private readonly Dictionary<Vector2Int, GameObject> _chunkMeshObjects = new();

        // ---- Constructor ----------------------------------------------------

        public RoadMeshBuilder(WorldSettings settings, ITerrainLogger logger)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _logger   = logger   ?? throw new ArgumentNullException(nameof(logger));

            if (_settings.RoadMaterial == null)
                _logger.LogWarning(LOG_TAG,
                    "RoadMaterial is not assigned in WorldSettings! " +
                    "Road mesh will render with magenta error material.");
        }

        // ---- Public API -----------------------------------------------------

        /// <summary>
        /// Builds the road mesh for a chunk and attaches it as a child of
        /// the terrain GameObject. Must be called on the MAIN THREAD after
        /// the heightmap has been carved (road is already flat).
        /// </summary>
        /// <param name="chunkCoord">Grid coordinate of the chunk.</param>
        /// <param name="splinePoints">Sampled points belonging to this chunk.</param>
        /// <param name="parentTerrain">Chunk's terrain GameObject (mesh will be a child).</param>
        public void BuildChunkRoadMesh(
            Vector2Int chunkCoord,
            List<SplinePoint> splinePoints,
            GameObject parentTerrain)
        {
            // Clean up any previous mesh for this chunk
            DestroyChunkMesh(chunkCoord);

            if (splinePoints == null || splinePoints.Count < 2)
            {
                _logger.Log(LogLevel.Verbose, LOG_TAG,
                    $"Chunk [{chunkCoord.x},{chunkCoord.y}]: " +
                    $"< 2 spline points — road mesh skipped.");
                return;
            }

            using var scope = _logger.BeginTimed(LOG_TAG,
                $"Road mesh for chunk [{chunkCoord.x},{chunkCoord.y}]");

            Mesh mesh = GenerateMesh(splinePoints);

            // Create GameObject with MeshFilter + MeshRenderer
            var go = new GameObject($"RoadMesh_{chunkCoord.x}_{chunkCoord.y}");
            go.transform.SetParent(parentTerrain.transform, worldPositionStays: true);

            var filter   = go.AddComponent<MeshFilter>();
            var renderer = go.AddComponent<MeshRenderer>();
            filter.sharedMesh           = mesh;
            renderer.sharedMaterial     = _settings.RoadMaterial;

            // Disable shadow casting on road surface (optional, reduces overdraw)
            renderer.shadowCastingMode  = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows     = true;

            // Keep mesh GC-safe by marking it as not readable after upload
            mesh.UploadMeshData(markNoLongerReadable: false); // Keep readable for physics baking if needed

            _chunkMeshObjects[chunkCoord] = go;

            _logger.Log(LogLevel.Info, LOG_TAG,
                $"Chunk [{chunkCoord.x},{chunkCoord.y}]: " +
                $"Road mesh built — {splinePoints.Count} segments, " +
                $"{mesh.vertexCount} verts, {mesh.triangles.Length / 3} tris.");
        }

        /// <summary>Destroys the road mesh GameObject for the given chunk.</summary>
        public void DestroyChunkMesh(Vector2Int chunkCoord)
        {
            if (_chunkMeshObjects.TryGetValue(chunkCoord, out var go))
            {
                if (go != null) UnityEngine.Object.Destroy(go);
                _chunkMeshObjects.Remove(chunkCoord);
            }
        }

        // ---- Mesh generation ------------------------------------------------

        /// <summary>
        /// Core mesh generation. Pure math — no Unity Object calls except Mesh creation.
        ///
        /// Layout per cross-section (4 verts across):
        ///   0: left outer edge  (shoulder edge)
        ///   1: left road edge
        ///   2: right road edge
        ///   3: right outer edge (shoulder edge)
        ///
        /// We use 4 verts across (not 2) to give shoulder geometry a slight height
        /// drop, making the road feel physically embedded in the terrain.
        /// </summary>
        private Mesh GenerateMesh(List<SplinePoint> points)
        {
            int   segCount   = points.Count - 1;
            int   ringCount  = points.Count;   // One ring of verts per spline sample
            int   vertsPerRing = 4;            // left-outer, left-road, right-road, right-outer
            int   vertCount  = ringCount * vertsPerRing;
            int   triCount   = segCount  * (vertsPerRing - 1) * 2; // Quads → 2 tris each

            var vertices  = new Vector3[vertCount];
            var uvs       = new Vector2[vertCount];
            var normals   = new Vector3[vertCount];
            var triangles = new int[triCount * 3];

            float roadHW     = _settings.RoadWidth    * 0.5f;
            float shoulderW  = _settings.RoadShoulderWidth;
            float uvTile     = _settings.RoadUVTileDistance > 0f
                               ? _settings.RoadUVTileDistance : 10f;
            float shoulderDrop = 0.08f; // Meters the shoulder drops below road surface

            for (int i = 0; i < ringCount; i++)
            {
                var sp = points[i];

                // Compute right vector perpendicular to tangent in XZ plane
                float3 tangXZ = math.normalize(new float3(sp.Tangent.x, 0f, sp.Tangent.z));
                float3 right  = new float3(tangXZ.z, 0f, -tangXZ.x); // Rotate 90° CCW
                float3 up     = new float3(0f, 1f, 0f);

                float3 roadCenter    = sp.Position;
                float  roadY         = roadCenter.y;
                float  shoulderY     = roadY - shoulderDrop;
                float  uvV           = sp.DistanceAlongSpline / uvTile;

                int base_ = i * vertsPerRing;

                // 0 — Left outer (shoulder edge)
                float3 p0 = roadCenter - right * (roadHW + shoulderW);
                vertices[base_]   = new Vector3(p0.x, shoulderY, p0.z);
                uvs[base_]        = new Vector2(-shoulderW / _settings.RoadWidth, uvV);
                normals[base_]    = Vector3.up;

                // 1 — Left road edge
                float3 p1 = roadCenter - right * roadHW;
                vertices[base_+1] = new Vector3(p1.x, roadY, p1.z);
                uvs[base_+1]      = new Vector2(0f, uvV);
                normals[base_+1]  = Vector3.up;

                // 2 — Right road edge
                float3 p2 = roadCenter + right * roadHW;
                vertices[base_+2] = new Vector3(p2.x, roadY, p2.z);
                uvs[base_+2]      = new Vector2(1f, uvV);
                normals[base_+2]  = Vector3.up;

                // 3 — Right outer (shoulder edge)
                float3 p3 = roadCenter + right * (roadHW + shoulderW);
                vertices[base_+3] = new Vector3(p3.x, shoulderY, p3.z);
                uvs[base_+3]      = new Vector2(1f + shoulderW / _settings.RoadWidth, uvV);
                normals[base_+3]  = Vector3.up;
            }

            // Build triangles (quads between rings)
            int triIdx = 0;
            for (int i = 0; i < segCount; i++)
            {
                int ring0 = i       * vertsPerRing;
                int ring1 = (i + 1) * vertsPerRing;

                for (int v = 0; v < vertsPerRing - 1; v++)
                {
                    // Quad: ring0[v], ring0[v+1], ring1[v], ring1[v+1]
                    // Winding: counter-clockwise (Unity standard)
                    int a = ring0 + v;
                    int b = ring0 + v + 1;
                    int c = ring1 + v;
                    int d = ring1 + v + 1;

                    triangles[triIdx++] = a;
                    triangles[triIdx++] = c;
                    triangles[triIdx++] = b;

                    triangles[triIdx++] = b;
                    triangles[triIdx++] = c;
                    triangles[triIdx++] = d;
                }
            }

            var mesh        = new Mesh();
            mesh.name       = "ProceduralRoad";
            mesh.indexFormat = vertCount > 65535
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;

            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            // RecalculateNormals skipped — we assign flat normals (Vector3.up) per vertex
            // which is correct for a road surface and faster than computed normals.

            return mesh;
        }

        // ---- IDisposable ----------------------------------------------------

        private bool _disposed;
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            foreach (var (coord, go) in _chunkMeshObjects)
                if (go != null) UnityEngine.Object.Destroy(go);
            _chunkMeshObjects.Clear();
        }
    }
}
