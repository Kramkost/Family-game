// =============================================================================
// FILE 6: BiomeSystem.cs  (MODIFIED — Requirement 3)
// Changes:
//   [REQ-3] ScheduleBlendedHeightmapJob: passes HeightExponent, RidgeWeight,
//            TerraceCount from BiomeDefinition to HeightmapGenerationJob.
// All other logic unchanged.
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

namespace ProceduralTerrain.Biomes
{
    public sealed class BiomeProvider : IBiomeProvider, IDisposable
    {
        private const string LOG_TAG = "BiomeProvider";

        private readonly WorldSettings        _settings;
        private readonly ITerrainLogger       _logger;
        private readonly List<BiomeDefinition> _biomes;
        private NativeArray<float4>           _biomeRanges;

        public IReadOnlyList<BiomeDefinition> Biomes => _biomes.AsReadOnly();

        public BiomeProvider(WorldSettings settings, ITerrainLogger logger)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _logger   = logger   ?? throw new ArgumentNullException(nameof(logger));
            _biomes   = new List<BiomeDefinition>(settings.BiomeDefinitions);

            if (_biomes.Count == 0)
            {
                _logger.LogError(LOG_TAG, "No BiomeDefinitions registered! Terrain will be flat/default.");
                return;
            }

            _biomeRanges = new NativeArray<float4>(_biomes.Count, Allocator.Persistent);
            for (int i = 0; i < _biomes.Count; i++)
            {
                var b = _biomes[i];
                _biomeRanges[i] = new float4(b.MoistureMin, b.MoistureMax,
                                              b.TemperatureMin, b.TemperatureMax);
            }

            _logger.Log(LogLevel.Info, LOG_TAG,
                $"BiomeProvider initialized with {_biomes.Count} biome(s): " +
                string.Join(", ", _biomes.ConvertAll(b => b.BiomeName)));
        }

        // ---- IBiomeProvider -------------------------------------------------

        public float[] GetBiomeWeights(float worldX, float worldZ)
        {
            float moisture    = SampleMoisture(worldX, worldZ);
            float temperature = SampleTemperature(worldX, worldZ);
            float[] weights   = new float[_biomes.Count];
            float   total     = 0f;

            for (int i = 0; i < _biomes.Count; i++)
            {
                var   b    = _biomes[i];
                float mW   = SmoothFit(b.MoistureMin,    b.MoistureMax,    moisture);
                float tW   = SmoothFit(b.TemperatureMin, b.TemperatureMax, temperature);
                weights[i] = mW * tW;
                total     += weights[i];
            }

            if (total > 1e-6f)
                for (int i = 0; i < weights.Length; i++) weights[i] /= total;
            else
                weights[0] = 1f;

            return weights;
        }

        public int GetDominantBiome(float worldX, float worldZ)
        {
            var weights  = GetBiomeWeights(worldX, worldZ);
            int dominant = 0;
            float max    = float.MinValue;
            for (int i = 0; i < weights.Length; i++)
                if (weights[i] > max) { max = weights[i]; dominant = i; }
            return dominant;
        }

        // ---- Batch Job scheduling -------------------------------------------

        public JobHandle ScheduleBiomeWeightJob(
            Vector2Int chunkCoord, int resolution, float worldSize,
            NativeArray<float> biomeWeightsOutput,
            JobHandle dependency = default)
        {
            Vector2 origin = GetChunkOriginXZ(chunkCoord, worldSize);
            var job = new BiomeWeightJob
            {
                Resolution           = resolution,
                WorldSize            = worldSize,
                ChunkOriginXZ        = new float2(origin.x, origin.y),
                Seed                 = _settings.WorldSeed,
                NumBiomes            = _biomes.Count,
                MoistureFrequency    = _settings.MoistureFrequency,
                TemperatureFrequency = _settings.TemperatureFrequency,
                BiomeRanges          = _biomeRanges,
                BiomeWeights         = biomeWeightsOutput
            };
            return job.Schedule(resolution * resolution, 64, dependency);
        }

        public JobHandle ScheduleBlendedHeightmapJob(
            Vector2Int chunkCoord, int resolution, float worldSize,
            NativeArray<float> biomeWeights,
            NativeArray<float> outputHeightmap,
            out NativeArray<float> tempBiomeHeightmaps,
            JobHandle dependency = default)
        {
            int vertCount = resolution * resolution;
            tempBiomeHeightmaps = new NativeArray<float>(vertCount * _biomes.Count, Allocator.TempJob);

            Vector2 origin  = GetChunkOriginXZ(chunkCoord, worldSize);
            var     handles = new NativeArray<JobHandle>(_biomes.Count, Allocator.Temp);

            for (int b = 0; b < _biomes.Count; b++)
            {
                var biome = _biomes[b];
                var slice = new NativeArray<float>(vertCount, Allocator.TempJob);

                // NEW [REQ-3] — Pass topography shaping fields to the job.
                // Each biome independently controls mountain vs. plain shape
                // via its HeightExponent, RidgeWeight, and TerraceCount.
                var hJob = new HeightmapGenerationJob
                {
                    Resolution    = resolution,
                    WorldSize     = worldSize,
                    ChunkOriginXZ = new float2(origin.x, origin.y),
                    Seed          = _settings.WorldSeed + b * 997,
                    HeightScale   = biome.HeightScale,
                    Frequency     = biome.HeightFrequency,
                    Octaves       = biome.HeightOctaves,
                    Persistence   = biome.HeightPersistence,
                    Lacunarity    = biome.HeightLacunarity,

                    // NEW fields — sourced from BiomeDefinition asset
                    HeightExponent = biome.HeightExponent,  // NEW [REQ-3]
                    RidgeWeight    = biome.RidgeWeight,     // NEW [REQ-3]
                    TerraceCount   = biome.TerraceCount,    // NEW [REQ-3]
                    // END NEW

                    Heightmap = slice
                };
                // END NEW

                handles[b] = hJob.Schedule(vertCount, 64, dependency);
            }

            var combinedDep = JobHandle.CombineDependencies(handles);
            handles.Dispose();

            var blendJob = new BiomeHeightBlendJob
            {
                NumBiomes       = _biomes.Count,
                BiomeWeights    = biomeWeights,
                BiomeHeightmaps = tempBiomeHeightmaps,
                OutputHeightmap = outputHeightmap
            };

            return blendJob.Schedule(vertCount, 64, combinedDep);
        }

        // ---- Private helpers ------------------------------------------------

        private float SampleMoisture(float worldX, float worldZ)
            => BurstNoise.FBM01(new float2(worldX, worldZ), 2,
                _settings.MoistureFrequency, 0.5f, 2f, _settings.WorldSeed + 7);

        private float SampleTemperature(float worldX, float worldZ)
            => BurstNoise.FBM01(new float2(worldX, worldZ), 2,
                _settings.TemperatureFrequency, 0.5f, 2f, _settings.WorldSeed + 13);

        private static float SmoothFit(float min, float max, float value)
        {
            if (value < min || value > max) return 0f;
            float t    = (value - min) / Mathf.Max(max - min, 1e-6f);
            float tent = 1f - Mathf.Abs(t * 2f - 1f);
            return tent * tent * (3f - 2f * tent);
        }

        private static Vector2 GetChunkOriginXZ(Vector2Int coord, float worldSize)
            => new Vector2(coord.x * worldSize, coord.y * worldSize);

        // ---- IDisposable ----------------------------------------------------

        private bool _disposed;
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_biomeRanges.IsCreated) _biomeRanges.Dispose();
            _logger.Log(LogLevel.Info, LOG_TAG, "BiomeProvider disposed.");
        }
    }
}
