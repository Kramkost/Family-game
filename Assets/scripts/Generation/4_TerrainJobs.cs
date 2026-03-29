// =============================================================================
// FILE 4: TerrainJobs.cs
// All Unity C# Job System structs (IJobParallelFor).
// ZERO managed heap allocations — only NativeArrays and blittable structs.
// =============================================================================

using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace ProceduralTerrain.Jobs
{
    // =========================================================================
    // Noise helper — static, Burst-safe
    // =========================================================================

    /// <summary>
    /// Deterministic, Burst-compatible noise utilities.
    /// We implement a simple fBm (fractal Brownian Motion) using Unity.Mathematics.
    /// </summary>
    public static class BurstNoise
    {
        /// <summary>
        /// Fractional Brownian Motion layered Perlin noise.
        /// All parameters are explicit so the struct can call this from Burst.
        /// </summary>
        [BurstCompile]
        public static float FBM(float2 p, int octaves, float frequency,
                                float persistence, float lacunarity, int seed)
        {
            float value     = 0f;
            float amplitude = 1f;
            float maxValue  = 0f;
            float2 offset   = new float2(seed * 0.31f, seed * 0.17f);

            for (int o = 0; o < octaves; o++)
            {
                float2 samplePos = (p + offset) * frequency;
                value    += noise.snoise(samplePos) * amplitude;
                maxValue += amplitude;
                amplitude  *= persistence;
                frequency  *= lacunarity;
            }

            return value / maxValue;   // Normalized to ~[-1, 1]
        }

        [BurstCompile]
        public static float FBM01(float2 p, int octaves, float frequency,
                                   float persistence, float lacunarity, int seed)
            => (FBM(p, octaves, frequency, persistence, lacunarity, seed) + 1f) * 0.5f;
    }

    // =========================================================================
    // JOB 1: Heightmap Generation
    // =========================================================================

    /// <summary>
    /// Generates the raw heightmap for a chunk using layered fBm noise.
    /// Each thread handles one height sample independently → IJobParallelFor.
    /// </summary>
    [BurstCompile]
    public struct HeightmapGenerationJob : IJobParallelFor
    {
        // ---- Inputs ---------------------------------------------------------
        [ReadOnly] public int    Resolution;
        [ReadOnly] public float  WorldSize;
        [ReadOnly] public float2 ChunkOriginXZ;   // World-space bottom-left
        [ReadOnly] public int    Seed;

        // Noise params per biome (we store a single "dominant" set here;
        // BiomeBlendJob refines afterwards).
        [ReadOnly] public float  HeightScale;
        [ReadOnly] public float  Frequency;
        [ReadOnly] public int    Octaves;
        [ReadOnly] public float  Persistence;
        [ReadOnly] public float  Lacunarity;

        // ---- Output ---------------------------------------------------------
        [WriteOnly] public NativeArray<float> Heightmap; // Normalized [0,1]

        public void Execute(int index)
        {
            int x = index % Resolution;
            int z = index / Resolution;

            float2 worldXZ = ChunkOriginXZ + new float2(
                (float)x / (Resolution - 1) * WorldSize,
                (float)z / (Resolution - 1) * WorldSize
            );

            float h = BurstNoise.FBM01(worldXZ, Octaves, Frequency, Persistence, Lacunarity, Seed);
            Heightmap[index] = math.saturate(h);
        }
    }

    // =========================================================================
    // JOB 2: Biome-Weighted Heightmap Blending
    // =========================================================================

    /// <summary>
    /// Blends multiple biome heightmaps together based on per-vertex biome weights.
    /// Requires HeightmapGenerationJob to have been run once per biome first.
    /// </summary>
    [BurstCompile]
    public struct BiomeHeightBlendJob : IJobParallelFor
    {
        [ReadOnly] public int NumBiomes;
        [NativeDisableParallelForRestriction]
        public NativeArray<float> BiomeWeights;   // [vertex * numBiomes + biome]
        [ReadOnly] public NativeArray<float> BiomeHeightmaps; // Same layout — pre-generated

        [WriteOnly] public NativeArray<float> OutputHeightmap;

        public void Execute(int vertexIndex)
        {
            float blendedHeight = 0f;
            for (int b = 0; b < NumBiomes; b++)
            {
                float w = BiomeWeights[vertexIndex * NumBiomes + b];
                blendedHeight += BiomeHeightmaps[vertexIndex * NumBiomes + b] * w;
            }
            OutputHeightmap[vertexIndex] = math.saturate(blendedHeight);
        }
    }

    // =========================================================================
    // JOB 3: Biome Weight Calculation
    // =========================================================================

    [BurstCompile]
    public struct BiomeWeightJob : IJobParallelFor
    {
        [ReadOnly] public int    Resolution;
        [ReadOnly] public float  WorldSize;
        [ReadOnly] public float2 ChunkOriginXZ;
        [ReadOnly] public int    Seed;
        [ReadOnly] public int    NumBiomes;
        [ReadOnly] public float  MoistureFrequency;
        [ReadOnly] public float  TemperatureFrequency;

        // Biome thresholds (moisture [0] and temperature [1] ranges, packed)
        [ReadOnly] public NativeArray<float4> BiomeRanges; // (mMin, mMax, tMin, tMax) per biome

        [WriteOnly] public NativeArray<float> BiomeWeights; // [vertex * numBiomes + biome]

        public void Execute(int index)
        {
            int x = index % Resolution;
            int z = index / Resolution;

            float2 worldXZ = ChunkOriginXZ + new float2(
                (float)x / (Resolution - 1) * WorldSize,
                (float)z / (Resolution - 1) * WorldSize
            );

            float moisture    = BurstNoise.FBM01(worldXZ, 2, MoistureFrequency,    0.5f, 2f, Seed + 7);
            float temperature = BurstNoise.FBM01(worldXZ, 2, TemperatureFrequency, 0.5f, 2f, Seed + 13);

            // Compute raw influence per biome then normalize
            float totalInfluence = 0f;
            for (int b = 0; b < NumBiomes; b++)
            {
                float4 r        = BiomeRanges[b];
                float  mWeight  = SmoothStep(r.x, r.y, moisture);
                float  tWeight  = SmoothStep(r.z, r.w, temperature);
                float  inf      = mWeight * tWeight;
                BiomeWeights[index * NumBiomes + b] = inf;
                totalInfluence += inf;
            }

            // Normalize so weights sum to 1
            float invTotal = totalInfluence > 1e-6f ? 1f / totalInfluence : 0f;
            for (int b = 0; b < NumBiomes; b++)
                BiomeWeights[index * NumBiomes + b] *= invTotal;
        }

        private static float SmoothStep(float min, float max, float value)
        {
            float t = math.saturate((value - min) / math.max(max - min, 1e-6f));
            return t * t * (3f - 2f * t);
        }
    }

// =========================================================================
    // JOB 4: Splatmap Generation (Now with Road Painting!)
    // =========================================================================

    [BurstCompile]
    public struct SplatmapGenerationJob : IJobParallelFor
    {
        [ReadOnly] public int   Resolution;
        [ReadOnly] public int   NumLayers;
        [ReadOnly] public int   NumBiomes;
        [ReadOnly] public float HeightScale;      // Max real-world height
        [ReadOnly] public float WorldSize;
        [ReadOnly] public float2 ChunkOriginXZ;

        // Road params (newly added for painting)
        [ReadOnly] public float RoadHalfWidth;
        [ReadOnly] public float ShoulderHalfWidth;
        [ReadOnly] public NativeArray<float3> SplinePoints; // (x, y_norm, z)

        [ReadOnly] public NativeArray<float> Heightmap;
        public NativeArray<float> BiomeWeights; // [vertex * numBiomes + biome]

        // Primary and secondary layer indices per biome
        [ReadOnly] public NativeArray<int2>  BiomeSplatLayers; // (primary, secondary) per biome

         [NativeDisableParallelForRestriction]
         public NativeArray<float> Splatmap; // [vertex * numLayers + layer]

        public void Execute(int index)
        {
            int x = index % Resolution;
            int z = index / Resolution;

            float2 worldXZ = ChunkOriginXZ + new float2(
                (float)x / (Resolution - 1) * WorldSize,
                (float)z / (Resolution - 1) * WorldSize
            );

            float height = Heightmap[index];

            // 1. Zero out all layers for this vertex
            for (int l = 0; l < NumLayers; l++)
                Splatmap[index * NumLayers + l] = 0f;

            // 2. Calculate distance to road spline
            float minDistToRoad = float.MaxValue;
            for (int i = 0; i < SplinePoints.Length; i++)
            {
                float2 spXZ = new float2(SplinePoints[i].x, SplinePoints[i].z);
                float dist = math.distance(worldXZ, spXZ);
                if (dist < minDistToRoad) minDistToRoad = dist;
            }

            // 3. Accumulate natural biome textures
            for (int b = 0; b < NumBiomes; b++)
            {
                float weight  = BiomeWeights[index * NumBiomes + b];
                if (weight < 0.001f) continue;

                int2 layers = BiomeSplatLayers[b];
                int  prim   = layers.x; // Usually 0 (Grass)
                int  sec    = layers.y; // Usually 1 (Dirt/Rock)

                // Blend primary vs secondary based on height (rock at high altitude)
                float rockBlend = math.saturate((height - 0.65f) / 0.15f);
                float secBlend  = rockBlend;
                float primBlend = 1f - secBlend;

                if (prim < NumLayers) Splatmap[index * NumLayers + prim] += weight * primBlend;
                if (sec  < NumLayers) Splatmap[index * NumLayers + sec ] += weight * secBlend;
            }

            // 4. OVERRIDE WITH ROAD TEXTURE
            // We assume Layer 1 (index 1) is the dirt road layer.
            int roadLayerIndex = 1; 
            
            if (minDistToRoad < RoadHalfWidth + ShoulderHalfWidth && roadLayerIndex < NumLayers)
            {
                float roadWeight = 1f;
                
                if (minDistToRoad > RoadHalfWidth)
                {
                    // Fade out on the shoulder
                    float t = (minDistToRoad - RoadHalfWidth) / ShoulderHalfWidth;
                    roadWeight = 1f - math.smoothstep(0f, 1f, t);
                }

                // Apply road weight and reduce other layers proportionally
                float invRoadWeight = 1f - roadWeight;
                for (int l = 0; l < NumLayers; l++)
                {
                    if (l == roadLayerIndex)
                        Splatmap[index * NumLayers + l] = math.max(Splatmap[index * NumLayers + l], roadWeight);
                    else
                        Splatmap[index * NumLayers + l] *= invRoadWeight;
                }
            }

            // 5. Renormalize splatmap so all weights sum to 1.0
            float total = 0f;
            for (int l = 0; l < NumLayers; l++) total += Splatmap[index * NumLayers + l];
            float inv = total > 1e-6f ? 1f / total : 0f;
            for (int l = 0; l < NumLayers; l++) Splatmap[index * NumLayers + l] *= inv;
        }
    }

    // =========================================================================
    // JOB 5: Road Carving (Heightmap Flattening)
    // =========================================================================

    /// <summary>
    /// For each heightmap vertex, checks proximity to the road spline
    /// and flattens the terrain within the road corridor.
    /// Shoulder zone blends smoothly outward.
    /// </summary>
    [BurstCompile]
    public struct RoadCarvingJob : IJobParallelFor
    {
        [ReadOnly] public int    Resolution;
        [ReadOnly] public float  WorldSize;
        [ReadOnly] public float2 ChunkOriginXZ;
        [ReadOnly] public float  HeightScale;
        [ReadOnly] public float  RoadHalfWidth;
        [ReadOnly] public float  ShoulderHalfWidth;

        /// <summary>Pre-sampled road spline points in world XZ (flat array: x0,z0,h0, x1,z1,h1…).</summary>
        [ReadOnly] public NativeArray<float3> SplinePoints; // world pos (x, road_normalized_height, z)

        public NativeArray<float> Heightmap; // Read-Write

        public void Execute(int index)
        {
            int x = index % Resolution;
            int z = index / Resolution;

            float2 worldXZ = ChunkOriginXZ + new float2(
                (float)x / (Resolution - 1) * WorldSize,
                (float)z / (Resolution - 1) * WorldSize
            );

            // Find nearest spline point
            float minDist   = float.MaxValue;
            float roadH     = 0f;

            for (int i = 0; i < SplinePoints.Length; i++)
            {
                float3 sp   = SplinePoints[i];
                float2 spXZ = new float2(sp.x, sp.z);
                float  dist = math.distance(worldXZ, spXZ);
                if (dist < minDist)
                {
                    minDist = dist;
                    roadH   = sp.y; // Normalized height of road at this point
                }
            }

            float totalHalfWidth = RoadHalfWidth + ShoulderHalfWidth;
            if (minDist >= totalHalfWidth) return; // Outside influence zone

            float currentH = Heightmap[index];

            if (minDist <= RoadHalfWidth)
            {
                // Fully inside road — clamp to road height
                Heightmap[index] = roadH;
            }
            else
            {
                // Shoulder blend zone
                float t = (minDist - RoadHalfWidth) / ShoulderHalfWidth;
                t = t * t * (3f - 2f * t); // Smooth-step
                Heightmap[index] = math.lerp(roadH, currentH, t);
            }
        }
    }

    // =========================================================================
    // JOB 6: Foliage Placement Candidate Generation
    // =========================================================================

    /// <summary>
    /// Uses a deterministic pseudo-random scatter to generate candidate positions
    /// for foliage. Candidates failing exclusion tests are flagged invalid.
    /// Final filtering and GPU instancing upload happen on the main thread.
    /// </summary>
    [BurstCompile]
    public struct FoliageCandidateJob : IJobParallelFor
    {
        [ReadOnly] public int    Resolution;
        [ReadOnly] public float  WorldSize;
        [ReadOnly] public float2 ChunkOriginXZ;
        [ReadOnly] public int    Seed;
        [ReadOnly] public float  RoadHalfWidth;
        [ReadOnly] public float  ShoulderHalfWidth;
        [ReadOnly] public int    NumSplinePoints;
        [ReadOnly] public NativeArray<float3> SplinePoints;
        [ReadOnly] public NativeArray<float>  Heightmap;
        [ReadOnly] public float  HeightScale;

        [WriteOnly] public NativeArray<FoliagePlacement> Results; // One per candidate slot

        public void Execute(int index)
        {
            // Generate candidate position from a jittered grid
            uint hash = Hash(((uint)Seed * 2654435761u) ^ (uint)index);

            float jitterX = (float)(hash & 0xFFFF) / 65535f * WorldSize;
            float jitterZ = (float)((hash >> 16) & 0xFFFF) / 65535f * WorldSize;

            float2 localXZ = new float2(jitterX, jitterZ);
            float2 worldXZ = ChunkOriginXZ + localXZ;

            // Sample heightmap at candidate position (bilinear)
            float normX   = localXZ.x / WorldSize * (Resolution - 1);
            float normZ   = localXZ.y / WorldSize * (Resolution - 1);
            int   ix      = (int)normX;
            int   iz      = (int)normZ;
            ix = math.clamp(ix, 0, Resolution - 2);
            iz = math.clamp(iz, 0, Resolution - 2);
            float tx = normX - ix;
            float tz = normZ - iz;
            float h00 = Heightmap[iz * Resolution + ix];
            float h10 = Heightmap[iz * Resolution + ix + 1];
            float h01 = Heightmap[(iz + 1) * Resolution + ix];
            float h11 = Heightmap[(iz + 1) * Resolution + ix + 1];
            float heightNorm = math.lerp(math.lerp(h00, h10, tx), math.lerp(h01, h11, tx), tz);
            float worldY     = heightNorm * HeightScale;

            // Reject if too close to road
            bool valid = true;
            float exclusionRadius = RoadHalfWidth + ShoulderHalfWidth + 8f;
            for (int i = 0; i < NumSplinePoints; i++)
            {
                float3 sp = SplinePoints[i];
                if (math.distance(worldXZ, new float2(sp.x, sp.z)) < exclusionRadius)
                {
                    valid = false;
                    break;
                }
            }

            uint hash2  = Hash(hash ^ 0xDEADBEEF);
            float rot   = (float)(hash2 & 0xFFFF) / 65535f * math.PI * 2f;
            float scale = 0.7f + (float)((hash2 >> 16) & 0xFFFF) / 65535f * 0.6f;

            Results[index] = new FoliagePlacement
            {
                WorldPosition = new float3(worldXZ.x, worldY, worldXZ.y),
                Rotation      = rot,
                Scale         = scale,
                Valid         = valid
            };
        }

        private static uint Hash(uint x)
        {
            x ^= x >> 16;
            x *= 0x45d9f3b;
            x ^= x >> 16;
            return x;
        }
    }

    // Blittable version of FoliagePlacementData for the job
    public struct FoliagePlacement
    {
        public float3 WorldPosition;
        public float  Rotation;
        public float  Scale;
        public bool   Valid;

        public int PrefabIndex;
    }
}
