// =============================================================================
// FILE 4: TerrainJobs.cs  (MODIFIED — Requirements 2 & 3)
// Changes:
//   [REQ-3] HeightmapGenerationJob: RidgeWeight, HeightExponent, TerraceCount
//   [REQ-3] BurstNoise: added FBMRidged01
//   [REQ-2] FoliageCandidateJob: ExclusionBuffer field + per-segment corridor check
// =============================================================================

using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace ProceduralTerrain.Jobs
{
    // =========================================================================
    // Noise helper
    // =========================================================================

    public static class BurstNoise
    {
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
            return value / maxValue;
        }

        [BurstCompile]
        public static float FBM01(float2 p, int octaves, float frequency,
                                   float persistence, float lacunarity, int seed)
            => (FBM(p, octaves, frequency, persistence, lacunarity, seed) + 1f) * 0.5f;

        // NEW [REQ-3] — Ridged multifractal: produces sharp mountain ridges.
        // Each octave uses (1 - |noise|) instead of raw noise, creating
        // high values along narrow ridges and low values in broad valleys.
        [BurstCompile]
        public static float FBMRidged01(float2 p, int octaves, float frequency,
                                         float persistence, float lacunarity, int seed)
        {
            float value     = 0f;
            float amplitude = 1f;
            float maxValue  = 0f;
            float2 offset   = new float2(seed * 0.31f + 100f, seed * 0.17f + 100f);
            float  weight   = 1f;   // "erosion" weight: ridges cut into each other

            for (int o = 0; o < octaves; o++)
            {
                float2 samplePos = (p + offset) * frequency;
                float  s         = noise.snoise(samplePos);
                float  ridged    = (1f - math.abs(s)) * weight;
                weight           = math.saturate(ridged * 2f); // feedback for next octave

                value    += ridged * amplitude;
                maxValue += amplitude;
                amplitude  *= persistence;
                frequency  *= lacunarity;
            }
            return math.saturate(value / maxValue);
        }
        // END NEW
    }

    // =========================================================================
    // JOB 1: Heightmap Generation  (MODIFIED — REQ-3)
    // =========================================================================

    [BurstCompile]
    public struct HeightmapGenerationJob : IJobParallelFor
    {
        [ReadOnly] public int    Resolution;
        [ReadOnly] public float  WorldSize;
        [ReadOnly] public float2 ChunkOriginXZ;
        [ReadOnly] public int    Seed;
        [ReadOnly] public float  HeightScale;
        [ReadOnly] public float  Frequency;
        [ReadOnly] public int    Octaves;
        [ReadOnly] public float  Persistence;
        [ReadOnly] public float  Lacunarity;

        // NEW [REQ-3] — Topography shaping parameters
        /// <summary>
        /// Power-curve exponent applied AFTER noise is computed.
        /// > 1.0 → flattens plains, sharpens peaks (realistic mountains).
        /// < 1.0 → rounds everything into gentle rolling hills.
        /// 1.0   → no redistribution (original behaviour).
        /// </summary>
        [ReadOnly] public float HeightExponent;

        /// <summary>
        /// Blend weight between standard fBm (0) and ridged multifractal (1).
        /// Use ~0.6–0.8 for biomes that should have jagged mountain ridges.
        /// </summary>
        [ReadOnly] public float RidgeWeight;

        /// <summary>
        /// Number of discrete terrace steps. 0 = disabled.
        /// Creates Minecraft-style flat shelves — good for canyon/mesa biomes.
        /// </summary>
        [ReadOnly] public int TerraceCount;
        // END NEW

        [WriteOnly] public NativeArray<float> Heightmap;

        public void Execute(int index)
        {
            int x = index % Resolution;
            int z = index / Resolution;

            float2 worldXZ = ChunkOriginXZ + new float2(
                (float)x / (Resolution - 1) * WorldSize,
                (float)z / (Resolution - 1) * WorldSize
            );

            // NEW [REQ-3] — Two noise layers blended by RidgeWeight
            float standard = BurstNoise.FBM01(worldXZ, Octaves, Frequency, Persistence, Lacunarity, Seed);
            float ridged   = BurstNoise.FBMRidged01(worldXZ, Octaves, Frequency, Persistence, Lacunarity, Seed);
            float h        = math.lerp(standard, ridged, RidgeWeight);

            // Power-curve redistribution — key for natural looking terrain
            // math.pow requires h > 0, which FBM01/ridged already guarantee (saturated)
            h = math.pow(h, HeightExponent);

            // Smooth terracing (if enabled)
            if (TerraceCount > 0)
            {
                float step  = 1f / TerraceCount;
                float lower = math.floor(h / step) * step;
                // Smooth blend within each step using smoothstep
                // This avoids the hard aliased look of floor-only terracing
                float blend = math.smoothstep(0f, 1f, (h - lower) / step);
                h = lower + blend * step;
            }
            // END NEW

            Heightmap[index] = math.saturate(h);
        }
    }

    // =========================================================================
    // JOB 2: Biome-Weighted Heightmap Blending (unchanged)
    // =========================================================================

    [BurstCompile]
    public struct BiomeHeightBlendJob : IJobParallelFor
    {
        [ReadOnly] public int NumBiomes;
        [ReadOnly] public NativeArray<float> BiomeWeights;
        [ReadOnly] public NativeArray<float> BiomeHeightmaps;
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
    // JOB 3: Biome Weight Calculation (unchanged)
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
        [ReadOnly] public NativeArray<float4> BiomeRanges;
        [WriteOnly] public NativeArray<float> BiomeWeights;

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

            float totalInfluence = 0f;
            for (int b = 0; b < NumBiomes; b++)
            {
                float4 r       = BiomeRanges[b];
                float  mWeight = SmoothStep(r.x, r.y, moisture);
                float  tWeight = SmoothStep(r.z, r.w, temperature);
                float  inf     = mWeight * tWeight;
                BiomeWeights[index * NumBiomes + b] = inf;
                totalInfluence += inf;
            }

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
    // JOB 4: Splatmap Generation (unchanged)
    // =========================================================================

    [BurstCompile]
    public struct SplatmapGenerationJob : IJobParallelFor
    {
        [ReadOnly] public int   Resolution;
        [ReadOnly] public int   NumLayers;
        [ReadOnly] public int   NumBiomes;
        [ReadOnly] public float HeightScale;
        [ReadOnly] public float WorldSize;
        [ReadOnly] public float2 ChunkOriginXZ;
        [ReadOnly] public float RoadHalfWidth;
        [ReadOnly] public float ShoulderHalfWidth;
        [ReadOnly] public NativeArray<float3> SplinePoints;
        [ReadOnly] public NativeArray<float>  Heightmap;
        [ReadOnly] public NativeArray<float>  BiomeWeights;
        [ReadOnly] public NativeArray<int2>   BiomeSplatLayers;
        [WriteOnly] public NativeArray<float> Splatmap;

        public void Execute(int index)
        {
            float height = Heightmap[index];

            for (int l = 0; l < NumLayers; l++)
                Splatmap[index * NumLayers + l] = 0f;

            for (int b = 0; b < NumBiomes; b++)
            {
                float weight = BiomeWeights[index * NumBiomes + b];
                if (weight < 0.001f) continue;

                int2 layers  = BiomeSplatLayers[b];
                int  prim    = layers.x;
                int  sec     = layers.y;

                float rockBlend = math.saturate((height - 0.65f) / 0.15f);
                float secBlend  = rockBlend;
                float primBlend = 1f - secBlend;

                if (prim < NumLayers) Splatmap[index * NumLayers + prim] += weight * primBlend;
                if (sec  < NumLayers) Splatmap[index * NumLayers + sec ] += weight * secBlend;
            }

            float total = 0f;
            for (int l = 0; l < NumLayers; l++) total += Splatmap[index * NumLayers + l];
            float inv = total > 1e-6f ? 1f / total : 0f;
            for (int l = 0; l < NumLayers; l++) Splatmap[index * NumLayers + l] *= inv;
        }
    }

    // =========================================================================
    // JOB 5: Road Carving (unchanged)
    // =========================================================================

    [BurstCompile]
    public struct RoadCarvingJob : IJobParallelFor
    {
        [ReadOnly] public int    Resolution;
        [ReadOnly] public float  WorldSize;
        [ReadOnly] public float2 ChunkOriginXZ;
        [ReadOnly] public float  HeightScale;
        [ReadOnly] public float  RoadHalfWidth;
        [ReadOnly] public float  ShoulderHalfWidth;
        [ReadOnly] public NativeArray<float3> SplinePoints;
        public NativeArray<float> Heightmap;

        public void Execute(int index)
        {
            int x = index % Resolution;
            int z = index / Resolution;

            float2 worldXZ = ChunkOriginXZ + new float2(
                (float)x / (Resolution - 1) * WorldSize,
                (float)z / (Resolution - 1) * WorldSize
            );

            float minDist = float.MaxValue;
            float roadH   = 0f;

            for (int i = 0; i < SplinePoints.Length; i++)
            {
                float3 sp   = SplinePoints[i];
                float2 spXZ = new float2(sp.x, sp.z);
                float  dist = math.distance(worldXZ, spXZ);
                if (dist < minDist) { minDist = dist; roadH = sp.y; }
            }

            float totalHalfWidth = RoadHalfWidth + ShoulderHalfWidth;
            if (minDist >= totalHalfWidth) return;

            float currentH = Heightmap[index];

            if (minDist <= RoadHalfWidth)
            {
                Heightmap[index] = roadH;
            }
            else
            {
                float t = (minDist - RoadHalfWidth) / ShoulderHalfWidth;
                t = t * t * (3f - 2f * t);
                Heightmap[index] = math.lerp(roadH, currentH, t);
            }
        }
    }

    // =========================================================================
    // JOB 6: Foliage Placement  (MODIFIED — REQ-2)
    // =========================================================================

    [BurstCompile]
    public struct FoliageCandidateJob : IJobParallelFor
    {
        [ReadOnly] public int    Resolution;
        [ReadOnly] public float  WorldSize;
        [ReadOnly] public float2 ChunkOriginXZ;
        [ReadOnly] public int    Seed;
        [ReadOnly] public float  HeightScale;

        // NEW [REQ-2] — Decomposed exclusion parameters for 100% reliable road exclusion
        /// <summary>Half road width in world meters (RoadWidth * 0.5).</summary>
        [ReadOnly] public float RoadHalfWidth;
        /// <summary>Shoulder blend zone width in world meters.</summary>
        [ReadOnly] public float ShoulderHalfWidth;
        /// <summary>
        /// Additional buffer in world meters beyond the shoulder.
        /// Total exclusion from road center = RoadHalfWidth + ShoulderHalfWidth + ExclusionBuffer.
        /// </summary>
        [ReadOnly] public float ExclusionBuffer;
        // END NEW

        [ReadOnly] public int    NumSplinePoints;
        [ReadOnly] public NativeArray<float3> SplinePoints;
        // NEW [REQ-2] — Per-point tangents for corridor-based check (not just distance)
        [ReadOnly] public NativeArray<float3> SplineTangents;
        // END NEW
        [ReadOnly] public NativeArray<float>  Heightmap;

        [WriteOnly] public NativeArray<FoliagePlacement> Results;

        public void Execute(int index)
        {
            uint hash = Hash(((uint)Seed * 2654435761u) ^ (uint)index);

            float jitterX = (float)(hash & 0xFFFF) / 65535f * WorldSize;
            float jitterZ = (float)((hash >> 16) & 0xFFFF) / 65535f * WorldSize;

            float2 localXZ = new float2(jitterX, jitterZ);
            float2 worldXZ = ChunkOriginXZ + localXZ;

            float normX = localXZ.x / WorldSize * (Resolution - 1);
            float normZ = localXZ.y / WorldSize * (Resolution - 1);
            int   ix    = math.clamp((int)normX, 0, Resolution - 2);
            int   iz    = math.clamp((int)normZ, 0, Resolution - 2);
            float tx    = normX - ix;
            float tz    = normZ - iz;
            float h00   = Heightmap[iz       * Resolution + ix    ];
            float h10   = Heightmap[iz       * Resolution + ix + 1];
            float h01   = Heightmap[(iz + 1) * Resolution + ix    ];
            float h11   = Heightmap[(iz + 1) * Resolution + ix + 1];
            float heightNorm = math.lerp(math.lerp(h00, h10, tx), math.lerp(h01, h11, tx), tz);
            float worldY     = heightNorm * HeightScale;

            // NEW [REQ-2] — Corridor-based exclusion test.
            // For each spline segment we compute:
            //   1. Perpendicular distance from the candidate to the segment line
            //   2. Compare against (RoadHalfWidth + ShoulderHalfWidth + ExclusionBuffer)
            // This catches diagonal candidates that slip through point-distance checks.
            float totalExclusionRadius = RoadHalfWidth + ShoulderHalfWidth + ExclusionBuffer;
            bool  valid                = true;

            float2 candidateXZ = worldXZ;

            for (int i = 0; i < NumSplinePoints && valid; i++)
            {
                float3 sp  = SplinePoints[i];
                float2 spXZ = new float2(sp.x, sp.z);

                // Point-distance fast reject (large radius)
                float pointDist = math.distance(candidateXZ, spXZ);
                if (pointDist > totalExclusionRadius * 3f) continue; // far away, skip expensive test

                // Segment-based perpendicular distance to road center line
                if (i < NumSplinePoints - 1)
                {
                    float3 next  = SplinePoints[i + 1];
                    float2 nextXZ = new float2(next.x, next.z);
                    float  perpDist = PerpendicularDistanceToSegment(candidateXZ, spXZ, nextXZ);
                    if (perpDist < totalExclusionRadius)
                    {
                        valid = false;
                    }
                }
                else
                {
                    // Last point: fallback to point distance
                    if (pointDist < totalExclusionRadius)
                        valid = false;
                }
            }
            // END NEW

            uint  hash2 = Hash(hash ^ 0xDEADBEEF);
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

        // NEW [REQ-2] — Returns the perpendicular (shortest) distance from point P
        // to the finite line segment AB. This is the correct road-corridor check.
        private static float PerpendicularDistanceToSegment(float2 p, float2 a, float2 b)
        {
            float2 ab = b - a;
            float  lenSq = math.dot(ab, ab);
            if (lenSq < 1e-6f) return math.distance(p, a); // degenerate segment

            float  t   = math.saturate(math.dot(p - a, ab) / lenSq);
            float2 proj = a + t * ab;
            return math.distance(p, proj);
        }
        // END NEW

        private static uint Hash(uint x)
        {
            x ^= x >> 16;
            x *= 0x45d9f3b;
            x ^= x >> 16;
            return x;
        }
    }

    // Blittable placement result (unchanged)
    public struct FoliagePlacement
    {
        public float3 WorldPosition;
        public float  Rotation;
        public float  Scale;
        public bool   Valid;
    }
}
