using UnityEngine;

namespace ProceduralTerrain.Core
{
    [CreateAssetMenu(menuName = "ProceduralTerrain/World Settings", fileName = "WorldSettings")]
    public class WorldSettings : ScriptableObject
    {
        [Header("World")]
        public int   WorldSeed          = 42;
        public int   ChunkResolution    = 129;   // Must be 2^n + 1 for Unity terrain
        public float ChunkWorldSize     = 200f;  // Meters per chunk side
        public int   ViewDistanceChunks = 4;     // Loaded radius around player

        [Header("Road")]
        public float RoadWidth          = 8f;    // Total road width (meters)
        public float RoadShoulderWidth  = 4f;    // Blend zone beyond road edge
        public float RoadMaxSlopeDeg    = 8f;    // Pathfinding: reject nodes above this slope
        public float RoadSegmentLength  = 20f;   // Arc-length between spline samples

        [Header("Safe Zones")]
        public float SafeZoneIntervalMeters = 1000f;
        public GameObject SafeZonePrefab;

        [Header("Noise — Global")]
        public float MoistureFrequency   = 0.0008f;
        public float TemperatureFrequency = 0.0006f;

        [Header("Biomes")]
        public BiomeDefinition[] BiomeDefinitions;

        [Header("Terrain Layers")]
        public TerrainLayer[] TerrainLayers; // Assigned in Inspector (sand, grass, rock…)
    }
}