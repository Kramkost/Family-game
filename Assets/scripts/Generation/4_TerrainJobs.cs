using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace ProceduralTerrain.Jobs
{
    public static class BurstNoise
    {
        [BurstCompile]
        public static float FBM(float2 p, int octaves, float frequency, float persistence, float lacunarity, int seed)
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
        public static float FBM01(float2 p, int octaves, float frequency, float persistence, float lacunarity, int seed)
            => (FBM(p, octaves, frequency, persistence, lacunarity, seed) + 1f) * 0.5f;

        [BurstCompile]
        public static float FBMRidged01(float2 p, int octaves, float frequency, float persistence, float lacunarity, int seed)
        {
            float value     = 0f;
            float amplitude = 1f;
            float maxValue  = 0f;
            float2 offset   = new float2(seed * 0.31f + 100f, seed * 0.17f + 100f);
            float  weight   = 1f;

            for (int o = 0; o < octaves; o++)
            {
                float2 samplePos = (p + offset) * frequency;
                float  s         = noise.snoise(samplePos);
                float  ridged    = (1f - math.abs(s)) * weight;
                weight           = math.saturate(ridged * 2f);

                value    += ridged * amplitude;
                maxValue += amplitude;
                amplitude  *= persistence;
                frequency  *= lacunarity;
            }
            return math.saturate(value / maxValue);
        }
    }

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
        [ReadOnly] public float  HeightExponent;
        [ReadOnly] public float  RidgeWeight;
        [ReadOnly] public int    TerraceCount;

        [WriteOnly] public NativeArray<float> Heightmap;

        public void Execute(int index)
        {
            int x = index % Resolution;
            int z = index / Resolution;

            float2 worldXZ = ChunkOriginXZ + new float2(
                (float)x / (Resolution - 1) * WorldSize,
                (float)z / (Resolution - 1) * WorldSize
            );

            float standard = BurstNoise.FBM01(worldXZ, Octaves, Frequency, Persistence, Lacunarity, Seed);
            float ridged   = BurstNoise.FBMRidged01(worldXZ, Octaves, Frequency, Persistence, Lacunarity, Seed);
            float h        = math.lerp(standard, ridged, RidgeWeight);

            h = math.pow(h, HeightExponent);

            if (TerraceCount > 0)
            {
                float step  = 1f / TerraceCount;
                float lower = math.floor(h / step) * step;
                float blend = math.smoothstep(0f, 1f, (h - lower) / step);
                h = lower + blend * step;
            }

            Heightmap[index] = math.saturate(h);
        }
    }

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

    [BurstCompile]
    public struct RoadCarvingJob : IJobParallelFor
    {
        [ReadOnly] public int    Resolution;
        [ReadOnly] public float  WorldSize;
        [ReadOnly] public float2 ChunkOriginXZ;
        [ReadOnly] public float  HeightScale;
        
        [ReadOnly] public float  RoadHalfWidth;
        [ReadOnly] public float  ShoulderHalfWidth; // Используется для UV/материалов, но рельеф сглаживаем по Clearance
        [ReadOnly] public float  ClearanceRadius;  
        [ReadOnly] public float  EmbankmentHeight; 

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

            if (minDist >= ClearanceRadius) return;

            float currentH = Heightmap[index];
            float targetRoadHeight = roadH + (EmbankmentHeight / HeightScale);

            if (minDist <= RoadHalfWidth)
            {
                Heightmap[index] = targetRoadHeight;
            }
            else
            {
                float smoothT = math.smoothstep(RoadHalfWidth, ClearanceRadius, minDist);
                Heightmap[index] = math.lerp(targetRoadHeight, currentH, smoothT);
            }
        }
    }

    [BurstCompile]
    public struct FoliageCandidateJob : IJobParallelFor
    {
        [ReadOnly] public int    Resolution;
        [ReadOnly] public float  WorldSize;
        [ReadOnly] public float2 ChunkOriginXZ;
        [ReadOnly] public int    Seed;
        [ReadOnly] public float  HeightScale;
        [ReadOnly] public float  RoadHalfWidth;
        [ReadOnly] public float  ShoulderHalfWidth;
        [ReadOnly] public float  ExclusionBuffer;
        [ReadOnly] public int    NumSplinePoints;
        [ReadOnly] public NativeArray<float3> SplinePoints;
        [ReadOnly] public NativeArray<float3> SplineTangents;
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

            float totalExclusionRadius = RoadHalfWidth + ShoulderHalfWidth + ExclusionBuffer;
            bool  valid                = true;

            float2 candidateXZ = worldXZ;

            for (int i = 0; i < NumSplinePoints && valid; i++)
            {
                float3 sp  = SplinePoints[i];
                float2 spXZ = new float2(sp.x, sp.z);

                float pointDist = math.distance(candidateXZ, spXZ);
                if (pointDist > totalExclusionRadius * 3f) continue;

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
                    if (pointDist < totalExclusionRadius)
                        valid = false;
                }
            }

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

        private static float PerpendicularDistanceToSegment(float2 p, float2 a, float2 b)
        {
            float2 ab = b - a;
            float  lenSq = math.dot(ab, ab);
            if (lenSq < 1e-6f) return math.distance(p, a); 

            float  t   = math.saturate(math.dot(p - a, ab) / lenSq);
            float2 proj = a + t * ab;
            return math.distance(p, proj);
        }

        private static uint Hash(uint x)
        {
            x ^= x >> 16;
            x *= 0x45d9f3b;
            x ^= x >> 16;
            return x;
        }
    }

    public struct FoliagePlacement
    {
        public float3 WorldPosition;
        public float  Rotation;
        public float  Scale;
        public bool   Valid;
    }
}