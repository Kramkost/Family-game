// =============================================================================
// FILE 9: FoliageAndPOISpawner.cs  (MODIFIED — Requirement 2)
// Changes:
//   [REQ-2] FoliageSpawner.SpawnFoliage: passes ExclusionBuffer + SplineTangents
//            to FoliageCandidateJob (tangents enable corridor check, not just distance)
//   [REQ-2] SpawnGrassDetail: road exclusion per-detail-cell using spline corridor math
// POISpawner unchanged.
// =============================================================================

using System;
using System.Collections.Generic;
using Mirror;
using ProceduralTerrain.Biomes;
using ProceduralTerrain.Core;
using ProceduralTerrain.Debugging;
using ProceduralTerrain.Jobs;
using ProceduralTerrain.Road;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace ProceduralTerrain.Spawning
{
    // =========================================================================
    // Foliage Spawner  (MODIFIED)
    // =========================================================================

    public sealed class FoliageSpawner : IFoliageSpawner
    {
        private const string LOG_TAG = "FoliageSpawner";

        private readonly WorldSettings  _settings;
        private readonly ITerrainLogger _logger;
        private readonly RoadBuilder    _roadBuilder;

        public FoliageSpawner(WorldSettings settings, ITerrainLogger logger, RoadBuilder roadBuilder)
        {
            _settings    = settings    ?? throw new ArgumentNullException(nameof(settings));
            _logger      = logger      ?? throw new ArgumentNullException(nameof(logger));
            _roadBuilder = roadBuilder ?? throw new ArgumentNullException(nameof(roadBuilder));
        }

        public void SpawnFoliage(ChunkData chunk, Terrain terrain,
                                  IReadOnlyList<Rect> exclusionZones)
        {
            using var scope = _logger.BeginTimed(LOG_TAG,
                $"Foliage for chunk [{chunk.Coord.x},{chunk.Coord.y}]");

            if (terrain == null || terrain.terrainData == null)
            {
                _logger.LogWarning(LOG_TAG,
                    $"Chunk [{chunk.Coord.x},{chunk.Coord.y}]: Terrain null — skipping foliage.");
                return;
            }

            var splinePoints = _roadBuilder.GetSampledPointsForChunk(chunk.Coord);

            // NEW [REQ-2] — Build separate NativeArrays for positions AND tangents.
            // Tangents are required by the improved corridor-based exclusion in FoliageCandidateJob.
            var nativePositions = new NativeArray<float3>(splinePoints.Count, Allocator.TempJob);
            var nativeTangents  = new NativeArray<float3>(splinePoints.Count, Allocator.TempJob);
            for (int i = 0; i < splinePoints.Count; i++)
            {
                nativePositions[i] = splinePoints[i].Position;
                nativeTangents[i]  = splinePoints[i].Tangent;
            }

            // Total exclusion radius from road center line:
            //   RoadHalfWidth + ShoulderHalfWidth + FoliageExclusionBuffer
            // Log it so it's visible in console during generation.
            float totalExclusion = _settings.RoadWidth * 0.5f +
                                   _settings.RoadShoulderWidth +
                                   _settings.FoliageExclusionBuffer;
            _logger.Log(LogLevel.Verbose, LOG_TAG,
                $"Chunk [{chunk.Coord.x},{chunk.Coord.y}]: " +
                $"Foliage exclusion radius = {totalExclusion:F1} m from road center.");
            // END NEW

            int totalCandidates = 512;
            var candidateResults = new NativeArray<FoliagePlacement>(totalCandidates, Allocator.TempJob);

            // NEW [REQ-2] — Pass decomposed exclusion fields + tangents to the job
            var candidateJob = new FoliageCandidateJob
            {
                Resolution        = chunk.Resolution,
                WorldSize         = _settings.ChunkWorldSize,
                ChunkOriginXZ     = new float2(chunk.WorldOrigin.x, chunk.WorldOrigin.z),
                Seed              = chunk.Seed,
                HeightScale       = GetAverageHeightScale(),
                RoadHalfWidth     = _settings.RoadWidth * 0.5f,            // NEW [REQ-2]
                ShoulderHalfWidth = _settings.RoadShoulderWidth,            // NEW [REQ-2]
                ExclusionBuffer   = _settings.FoliageExclusionBuffer,       // NEW [REQ-2]
                NumSplinePoints   = nativePositions.Length,
                SplinePoints      = nativePositions,
                SplineTangents    = nativeTangents,                         // NEW [REQ-2]
                Heightmap         = chunk.Heightmap,
                Results           = candidateResults
            };
            // END NEW

            candidateJob.Schedule(totalCandidates, 64).Complete();

            nativePositions.Dispose();
            nativeTangents.Dispose(); // NEW [REQ-2]

            // Build tree instances
            var treeInstances = new List<TreeInstance>();
            var terrainData   = terrain.terrainData;
            SetupTreePrototypes(terrainData);

            for (int i = 0; i < totalCandidates; i++)
            {
                var candidate = candidateResults[i];
                if (!candidate.Valid) continue;

                bool excluded = false;
                var  candXZ   = new Vector2(candidate.WorldPosition.x, candidate.WorldPosition.z);
                foreach (var zone in exclusionZones)
                    if (zone.Contains(candXZ)) { excluded = true; break; }
                if (excluded) continue;

                float normX = (candidate.WorldPosition.x - terrain.transform.position.x) / _settings.ChunkWorldSize;
                float normZ = (candidate.WorldPosition.z - terrain.transform.position.z) / _settings.ChunkWorldSize;
                if (normX < 0f || normX > 1f || normZ < 0f || normZ > 1f) continue;

                treeInstances.Add(new TreeInstance
                {
                    prototypeIndex = candidate.Scale > 1.1f
                        ? 0    // Tall scale → tree prototype
                        : 1 % Mathf.Max(1, terrainData.treePrototypes.Length), // short → bush
                    position       = new Vector3(normX, 0f, normZ),
                    rotation       = candidate.Rotation,
                    widthScale     = candidate.Scale,
                    heightScale    = candidate.Scale,
                    color          = Color.white,
                    lightmapColor  = Color.white
                });
            }

            candidateResults.Dispose();
            terrainData.SetTreeInstances(treeInstances.ToArray(), true);

            _logger.Log(LogLevel.Info, LOG_TAG,
                $"Chunk [{chunk.Coord.x},{chunk.Coord.y}]: " +
                $"Spawned {treeInstances.Count} foliage instances (from {totalCandidates} candidates).");

            // NEW [REQ-2] — Pass spline data to grass spawner for exclusion
            SpawnGrassDetail(chunk, terrain, terrainData, exclusionZones);
            // END NEW
        }

        // ---- Private helpers ------------------------------------------------

        private void SetupTreePrototypes(TerrainData td)
        {
            var protos = new List<TreePrototype>();
            foreach (var biome in _settings.BiomeDefinitions)
            {
                foreach (var treePrefab in biome.TreePrefabs)
                    if (treePrefab != null) protos.Add(new TreePrototype { prefab = treePrefab });
                foreach (var bushPrefab in biome.BushPrefabs)
                    if (bushPrefab != null) protos.Add(new TreePrototype { prefab = bushPrefab });
            }
            if (protos.Count > 0) td.treePrototypes = protos.ToArray();
        }

        // NEW [REQ-2] — Grass exclusion is now road-corridor-aware.
        // We rasterize the road spline into the detail grid and blank out
        // any detail cell whose center falls within the exclusion radius.
        private void SpawnGrassDetail(ChunkData chunk, Terrain terrain,
                                       TerrainData td, IReadOnlyList<Rect> poiExclusionZones)
        {
            if (_settings.BiomeDefinitions.Length == 0) return;

            var biome = _settings.BiomeDefinitions[0];
            int grassLayer = biome.GrassDetailLayer;
            if (grassLayer >= td.detailPrototypes.Length) return;

            int   detailRes = td.detailResolution;
            var   grassMap  = td.GetDetailLayer(0, 0, detailRes, detailRes, grassLayer);
            var   rng       = new System.Random(chunk.Seed);

            // Re-fetch spline points for this chunk (we need them for grass exclusion)
            var splinePoints = _roadBuilder.GetSampledPointsForChunk(chunk.Coord);

            float totalExclusion = _settings.RoadWidth     * 0.5f +
                                   _settings.RoadShoulderWidth     +
                                   _settings.FoliageExclusionBuffer;

            float cellWorldSize  = _settings.ChunkWorldSize / detailRes;
            float chunkOriginX   = chunk.WorldOrigin.x;
            float chunkOriginZ   = chunk.WorldOrigin.z;

            for (int z = 0; z < detailRes; z++)
            for (int x = 0; x < detailRes; x++)
            {
                // World-space center of this detail cell
                float cellWorldX = chunkOriginX + (x + 0.5f) * cellWorldSize;
                float cellWorldZ = chunkOriginZ + (z + 0.5f) * cellWorldSize;
                var   cellXZ     = new Vector2(cellWorldX, cellWorldZ);

                // NEW [REQ-2] — Check each road segment for corridor exclusion
                bool inRoadCorridor = false;
                for (int i = 0; i < splinePoints.Count - 1 && !inRoadCorridor; i++)
                {
                    var   sp0 = splinePoints[i];
                    var   sp1 = splinePoints[i + 1];
                    var   a   = new Vector2(sp0.Position.x, sp0.Position.z);
                    var   b   = new Vector2(sp1.Position.x, sp1.Position.z);

                    float perpDist = PerpendicularDistToSegment(cellXZ, a, b);
                    if (perpDist < totalExclusion)
                        inRoadCorridor = true;
                }
                // END NEW

                // Check POI exclusion zones
                bool inPOIZone = false;
                foreach (var zone in poiExclusionZones)
                    if (zone.Contains(cellXZ)) { inPOIZone = true; break; }

                if (inRoadCorridor || inPOIZone)
                    grassMap[z, x] = 0;
                else
                    grassMap[z, x] = rng.NextDouble() < biome.GrassDensity ? 1 : 0;
            }

            td.SetDetailLayer(0, 0, grassLayer, grassMap);

            _logger.Log(LogLevel.Verbose, LOG_TAG,
                $"Chunk [{chunk.Coord.x},{chunk.Coord.y}]: " +
                $"Grass detail written ({detailRes}x{detailRes}, " +
                $"exclusion radius {totalExclusion:F1}m).");
        }

        // NEW [REQ-2] — Shared perpendicular-distance helper (managed, for grass loop)
        private static float PerpendicularDistToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab    = b - a;
            float   lenSq = Vector2.Dot(ab, ab);
            if (lenSq < 1e-6f) return Vector2.Distance(p, a);
            float   t     = Mathf.Clamp01(Vector2.Dot(p - a, ab) / lenSq);
            return Vector2.Distance(p, a + t * ab);
        }
        // END NEW

        private float GetAverageHeightScale()
        {
            if (_settings.BiomeDefinitions.Length == 0) return 50f;
            float t = 0f;
            foreach (var b in _settings.BiomeDefinitions) t += b.HeightScale;
            return t / _settings.BiomeDefinitions.Length;
        }
    }

    // =========================================================================
    // POI Spawner (unchanged from original)
    // =========================================================================

    public sealed class POISpawner : IPOISpawner
    {
        private const string LOG_TAG = "POISpawner";

        private readonly WorldSettings  _settings;
        private readonly ITerrainLogger _logger;
        private readonly Dictionary<Vector2Int, float> _lastHouseDistancePerChunk = new();

        public POISpawner(WorldSettings settings, ITerrainLogger logger)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _logger   = logger   ?? throw new ArgumentNullException(nameof(logger));
        }

        public void SpawnPOIs(ChunkData chunk, IRoadBuilder roadBuilder,
                               IBiomeProvider biomeProvider, int seed)
        {
            if (!NetworkServer.active)
            {
                _logger.LogWarning(LOG_TAG,
                    $"Chunk [{chunk.Coord.x},{chunk.Coord.y}]: SpawnPOIs on CLIENT. Skipping.");
                return;
            }

            using var scope = _logger.BeginTimed(LOG_TAG,
                $"POI spawning for chunk [{chunk.Coord.x},{chunk.Coord.y}]");

            var   splinePoints     = GetChunkSplinePoints(chunk, roadBuilder);
            float lastDist         = _lastHouseDistancePerChunk.GetValueOrDefault(chunk.Coord, 0f);
            int   spawnedThisChunk = 0;

            foreach (var sp in splinePoints)
            {
                int biomeIdx = biomeProvider.GetDominantBiome(sp.Position.x, sp.Position.z);
                if (biomeIdx >= _settings.BiomeDefinitions.Length) continue;

                var biome = _settings.BiomeDefinitions[biomeIdx];
                if (biome.HousePrefabs == null || biome.HousePrefabs.Length == 0) continue;
                if (sp.DistanceAlongSpline - lastDist < biome.HouseMinSpacing) continue;

                uint  hash = DeterministicHash((uint)seed, (uint)(sp.DistanceAlongSpline * 100f));
                float roll = (hash & 0xFFFF) / 65535f;
                if (roll > biome.HouseSpawnProbability) continue;

                for (int side = -1; side <= 1; side += 2)
                {
                    float3 roadRight     = math.normalize(new float3(sp.Tangent.z, 0, -sp.Tangent.x));
                    float3 spawnWorldPos = sp.Position +
                                          roadRight * (side * (sp.RoadWidth * 0.5f + biome.HouseRoadOffset));
                    spawnWorldPos.y = SampleTerrainHeight(spawnWorldPos.x, spawnWorldPos.z);

                    float yRot = Mathf.Atan2(roadRight.x * side * -1, roadRight.z * side * -1) * Mathf.Rad2Deg;

                    uint prefabHash  = DeterministicHash(hash, (uint)(side + 2));
                    int  prefabIndex = (int)(prefabHash % (uint)biome.HousePrefabs.Length);
                    var  prefab      = biome.HousePrefabs[prefabIndex];

                    if (prefab == null) continue;
                    if (prefab.GetComponent<NetworkIdentity>() == null)
                    {
                        _logger.LogWarning(LOG_TAG,
                            $"House prefab '{prefab.name}' missing NetworkIdentity — skipped.");
                        continue;
                    }

                    Vector3    spawnPos = new Vector3(spawnWorldPos.x, spawnWorldPos.y, spawnWorldPos.z);
                    Quaternion rot      = Quaternion.Euler(0f, yRot, 0f);

                    _logger.Log(LogLevel.Info, LOG_TAG,
                        $"Chunk [{chunk.Coord.x},{chunk.Coord.y}]: " +
                        $"Spawning '{prefab.name}' at {spawnPos} (dist={sp.DistanceAlongSpline:F1}m, side={side})");

                    var instance = UnityEngine.Object.Instantiate(prefab, spawnPos, rot);
                    NetworkServer.Spawn(instance);
                    spawnedThisChunk++;
                }

                lastDist = sp.DistanceAlongSpline;
            }

            _lastHouseDistancePerChunk[chunk.Coord] = lastDist;
            _logger.Log(LogLevel.Info, LOG_TAG,
                $"Chunk [{chunk.Coord.x},{chunk.Coord.y}]: {spawnedThisChunk} POI(s) spawned.");
        }

        private static List<SplinePoint> GetChunkSplinePoints(ChunkData chunk, IRoadBuilder roadBuilder)
        {
            if (roadBuilder is RoadBuilder rb) return rb.GetSampledPointsForChunk(chunk.Coord);
            return new List<SplinePoint>();
        }

        private float SampleTerrainHeight(float worldX, float worldZ)
        {
            if (Physics.Raycast(new Vector3(worldX, 1000f, worldZ), Vector3.down,
                    out var hit, 2000f, LayerMask.GetMask("Terrain")))
                return hit.point.y;
            var terrain = Terrain.activeTerrain;
            return terrain != null ? terrain.SampleHeight(new Vector3(worldX, 0, worldZ)) : 0f;
        }

        private static uint DeterministicHash(uint a, uint b)
        {
            uint x = a ^ (b * 2654435761u);
            x ^= x >> 16;
            x *= 0x45d9f3b;
            x ^= x >> 16;
            return x;
        }
    }
}
