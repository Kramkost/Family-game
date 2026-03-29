// =============================================================================
// FILE 9: FoliageAndPOISpawner.cs
// Concrete IFoliageSpawner and IPOISpawner.
// Foliage uses GPU-instanced TreeInstances via Unity TerrainData.
// POI / House spawning uses NetworkServer.Spawn (Mirror) — server-only.
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
    // Foliage Spawner
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
                    $"Chunk [{chunk.Coord.x},{chunk.Coord.y}]: Terrain is null — skipping foliage.");
                return;
            }

            // Determine dominant biome at chunk center
            float cx = chunk.WorldOrigin.x + _settings.ChunkWorldSize * 0.5f;
            float cz = chunk.WorldOrigin.z + _settings.ChunkWorldSize * 0.5f;

            // Collect spline points for this chunk (road exclusion)
            var splinePoints = _roadBuilder.GetSampledPointsForChunk(chunk.Coord);
            var nativeSpline = new NativeArray<float3>(splinePoints.Count, Allocator.TempJob);
            for (int i = 0; i < splinePoints.Count; i++)
                nativeSpline[i] = splinePoints[i].Position;

            // We'll generate candidates per biome weighted by density
            int totalCandidates = 512; // Candidates per chunk (tunable)
            var candidateResults = new NativeArray<FoliagePlacement>(
                totalCandidates, Allocator.TempJob);

            var candidateJob = new FoliageCandidateJob
            {
                Resolution        = chunk.Resolution,
                WorldSize         = _settings.ChunkWorldSize,
                ChunkOriginXZ     = new float2(chunk.WorldOrigin.x, chunk.WorldOrigin.z),
                Seed              = chunk.Seed,
                RoadHalfWidth     = _settings.RoadWidth * 0.5f,
                ShoulderHalfWidth = _settings.RoadShoulderWidth,
                NumSplinePoints   = nativeSpline.Length,
                SplinePoints      = nativeSpline,
                Heightmap         = chunk.Heightmap,
                HeightScale       = GetAverageHeightScale(),
                Results           = candidateResults
            };

            candidateJob.Schedule(totalCandidates, 64).Complete();
            nativeSpline.Dispose();

            // Convert to Unity TreeInstances
            var treeInstances = new List<TreeInstance>();
            var terrainData   = terrain.terrainData;

            // Setup tree prototypes on terrain (biome-based)
            SetupTreePrototypes(terrainData, chunk, cx, cz);

            for (int i = 0; i < totalCandidates; i++)
            {
                var candidate = candidateResults[i];
                if (!candidate.Valid) continue;

                // Check exclusion zones (POI footprints)
                bool excluded = false;
                var candXZ    = new Vector2(candidate.WorldPosition.x, candidate.WorldPosition.z);
                foreach (var zone in exclusionZones)
                {
                    if (zone.Contains(candXZ)) { excluded = true; break; }
                }
                if (excluded) continue;

                // Convert to terrain-local normalized position
                float normX = (candidate.WorldPosition.x - terrain.transform.position.x) /
                               _settings.ChunkWorldSize;
                float normZ = (candidate.WorldPosition.z - terrain.transform.position.z) /
                               _settings.ChunkWorldSize;

                if (normX < 0f || normX > 1f || normZ < 0f || normZ > 1f) continue;

                var ti = new TreeInstance
                {
                    prototypeIndex  = candidate.PrefabIndex % Mathf.Max(1, terrainData.treePrototypes.Length),
                    position        = new Vector3(normX, 0f, normZ),
                    rotation        = candidate.Rotation,
                    widthScale      = candidate.Scale,
                    heightScale     = candidate.Scale,
                    color           = Color.white,
                    lightmapColor   = Color.white
                };
                treeInstances.Add(ti);
            }

            candidateResults.Dispose();

            terrainData.SetTreeInstances(treeInstances.ToArray(), true);

            _logger.Log(LogLevel.Info, LOG_TAG,
                $"Chunk [{chunk.Coord.x},{chunk.Coord.y}]: " +
                $"Spawned {treeInstances.Count} foliage instances " +
                $"(from {totalCandidates} candidates).");

            // Grass / Detail layers
            SpawnGrassDetail(chunk, terrainData, cx, cz);
        }

        // ---- Private helpers ------------------------------------------------

        private void SetupTreePrototypes(TerrainData td, ChunkData chunk, float cx, float cz)
        {
            // Biome weights are already baked — just use settings.BiomeDefinitions
            var protos = new List<TreePrototype>();
            foreach (var biome in _settings.BiomeDefinitions)
            {
                foreach (var treePrefab in biome.TreePrefabs)
                {
                    if (treePrefab != null)
                        protos.Add(new TreePrototype { prefab = treePrefab });
                }
            }
            if (protos.Count > 0)
                td.treePrototypes = protos.ToArray();
        }

        private void SpawnGrassDetail(ChunkData chunk, TerrainData td, float cx, float cz)
        {
            if (_settings.BiomeDefinitions.Length == 0) return;

            var biome = _settings.BiomeDefinitions[0]; // Simplified: use first biome for now

            int detailRes    = td.detailResolution;
            int grassLayer   = biome.GrassDetailLayer;
            if (grassLayer >= td.detailPrototypes.Length) return;

            var grassMap     = td.GetDetailLayer(0, 0, detailRes, detailRes, grassLayer);
            var rng          = new System.Random(chunk.Seed);

            // --- ДОБАВЛЕНО: Достаем точки дороги для этого чанка ---
            var splinePoints = _roadBuilder.GetSampledPointsForChunk(chunk.Coord);
            // Считаем радиус чистоты: половина дороги + обочина + 2 метра запаса
            float exclusionDist = (_settings.RoadWidth * 0.5f) + _settings.RoadShoulderWidth + 2f; 
            float sqrExclusion = exclusionDist * exclusionDist;

            for (int z = 0; z < detailRes; z++)
            for (int x = 0; x < detailRes; x++)
            {
                // Вычисляем мировые координаты текущего куста травы
                float worldX = chunk.WorldOrigin.x + ((float)x / detailRes) * _settings.ChunkWorldSize;
                float worldZ = chunk.WorldOrigin.z + ((float)z / detailRes) * _settings.ChunkWorldSize;

                bool onRoad = false;
                
                // Проверяем, не слишком ли близко мы к какой-нибудь точке дороги
                foreach (var sp in splinePoints)
                {
                    float dx = sp.Position.x - worldX;
                    float dz = sp.Position.z - worldZ;
                    if (dx * dx + dz * dz < sqrExclusion)
                    {
                        onRoad = true;
                        break;
                    }
                }

                // Если на дороге - пустота (0), иначе - сажаем траву с шансом из биома
                if (onRoad)
                {
                    grassMap[z, x] = 0; 
                }
                else
                {
                    grassMap[z, x] = rng.NextDouble() < biome.GrassDensity ? 1 : 0;
                }
            }

            td.SetDetailLayer(0, 0, grassLayer, grassMap);
        }

        private float GetAverageHeightScale()
        {
            if (_settings.BiomeDefinitions.Length == 0) return 50f;
            float t = 0f;
            foreach (var b in _settings.BiomeDefinitions) t += b.HeightScale;
            return t / _settings.BiomeDefinitions.Length;
        }
    }

    // =========================================================================
    // POI Spawner
    // =========================================================================

    public sealed class POISpawner : IPOISpawner
    {
        private const string LOG_TAG = "POISpawner";

        private readonly WorldSettings  _settings;
        private readonly ITerrainLogger _logger;

        // Track last house position per chunk side for min-spacing enforcement
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
                    $"Chunk [{chunk.Coord.x},{chunk.Coord.y}]: SpawnPOIs called on CLIENT. Skipping.");
                return;
            }

            using var scope = _logger.BeginTimed(LOG_TAG,
                $"POI spawning for chunk [{chunk.Coord.x},{chunk.Coord.y}]");

            var   splinePoints     = GetChunkSplinePoints(chunk, roadBuilder);
            float lastDist         = _lastHouseDistancePerChunk.GetValueOrDefault(chunk.Coord, 0f);
            int   spawnedThisChunk = 0;

            foreach (var sp in splinePoints)
            {
                // Determine biome at this point
                int biomeIdx = biomeProvider.GetDominantBiome(sp.Position.x, sp.Position.z);
                if (biomeIdx >= _settings.BiomeDefinitions.Length) continue;

                var biome = _settings.BiomeDefinitions[biomeIdx];
                if (biome.HousePrefabs == null || biome.HousePrefabs.Length == 0) continue;

                // Spacing check
                if (sp.DistanceAlongSpline - lastDist < biome.HouseMinSpacing) continue;

                // Probability check (deterministic RNG)
                uint hash     = DeterministicHash((uint)seed, (uint)(sp.DistanceAlongSpline * 100f));
                float roll    = (hash & 0xFFFF) / 65535f;
                if (roll > biome.HouseSpawnProbability) continue;

                // Spawn on BOTH sides of road
                for (int side = -1; side <= 1; side += 2)
                {
                    float3 roadRight     = math.normalize(new float3(sp.Tangent.z, 0, -sp.Tangent.x));
                    float3 spawnWorldPos = sp.Position +
                                          roadRight * (side * (sp.RoadWidth * 0.5f + biome.HouseRoadOffset));

                    // Ensure spawn is on terrain surface
                    spawnWorldPos.y = SampleTerrainHeight(spawnWorldPos.x, spawnWorldPos.z);

                    // Y rotation: face the road
                    float yRot = Mathf.Atan2(roadRight.x * side * -1, roadRight.z * side * -1)
                                 * Mathf.Rad2Deg;

                    // Select prefab deterministically
                    uint  prefabHash  = DeterministicHash(hash, (uint)(side + 2));
                    int   prefabIndex = (int)(prefabHash % (uint)biome.HousePrefabs.Length);
                    var   prefab      = biome.HousePrefabs[prefabIndex];

                    if (prefab == null) continue;

                    // Check it has a NetworkIdentity
                    if (prefab.GetComponent<NetworkIdentity>() == null)
                    {
                        _logger.LogWarning(LOG_TAG,
                            $"House prefab '{prefab.name}' is missing NetworkIdentity — cannot NetworkServer.Spawn.");
                        continue;
                    }

                    Vector3 spawnPos = new Vector3(spawnWorldPos.x, spawnWorldPos.y, spawnWorldPos.z);
                    var     spawnRot = Quaternion.Euler(0f, yRot, 0f);

                    _logger.Log(LogLevel.Info, LOG_TAG,
                        $"Chunk [{chunk.Coord.x},{chunk.Coord.y}]: Spawning '{prefab.name}' " +
                        $"at {spawnPos} (biome={biome.BiomeName}, dist={sp.DistanceAlongSpline:F1}m, side={side})");

                    var instance = UnityEngine.Object.Instantiate(prefab, spawnPos, spawnRot);
                    NetworkServer.Spawn(instance);
                    spawnedThisChunk++;
                }

                lastDist = sp.DistanceAlongSpline;
            }

            _lastHouseDistancePerChunk[chunk.Coord] = lastDist;

            _logger.Log(LogLevel.Info, LOG_TAG,
                $"Chunk [{chunk.Coord.x},{chunk.Coord.y}]: {spawnedThisChunk} POI(s) spawned.");
        }

        // ---- Private helpers ------------------------------------------------

        private static List<SplinePoint> GetChunkSplinePoints(ChunkData chunk, IRoadBuilder roadBuilder)
        {
            if (roadBuilder is RoadBuilder rb)
                return rb.GetSampledPointsForChunk(chunk.Coord);
            return new List<SplinePoint>();
        }

        private float SampleTerrainHeight(float worldX, float worldZ)
        {
            // Raycast downward to find actual terrain surface
            if (Physics.Raycast(new Vector3(worldX, 1000f, worldZ), Vector3.down,
                    out var hit, 2000f, LayerMask.GetMask("Terrain")))
                return hit.point.y;

            // Fallback: Unity Terrain API
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
