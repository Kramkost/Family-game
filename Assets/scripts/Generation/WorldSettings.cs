using UnityEngine;

namespace ProceduralTerrain.Core
{
    [CreateAssetMenu(menuName = "ProceduralTerrain/World Settings", fileName = "WorldSettings")]
    public class WorldSettings : ScriptableObject
    {
        [Header("World")]
        public int   WorldSeed          = 42;
        public int   ChunkResolution    = 129;
        public float ChunkWorldSize     = 200f;
        public int   ViewDistanceChunks = 4;

        [Header("Road")]
        public float RoadWidth          = 8f;
        public float RoadShoulderWidth  = 4f;
        public float RoadMaxSlopeDeg    = 8f;
        public float RoadSegmentLength  = 20f;

        [Header("Road Mesh")]
        [Tooltip("Material applied to the procedural road mesh. Use a tiling road texture. UV.x = across width, UV.y = along road.")]
        public Material RoadMaterial;
        [Tooltip("Texture tile distance in world meters. UV.y repeats every N meters.")]
        public float RoadUVTileDistance = 10f;

        [Header("Road Clearance & Embankment")]
        [Tooltip("Высота насыпи дороги над базовым рельефом (в метрах)")]
        public float RoadEmbankmentHeight = 1.5f; 
        [Tooltip("Радиус полной очистки и сглаживания гор вокруг дороги (зона отчуждения)")]
        public float RoadClearanceRadius = 25f;

        [Header("Safe Zones")]
        public float SafeZoneIntervalMeters = 1000f;
        public GameObject SafeZonePrefab;

        [Header("Noise — Global")]
        public float MoistureFrequency   = 0.0008f;
        public float TemperatureFrequency = 0.0006f;

        [Header("Foliage Exclusion")]
        [Tooltip("Extra buffer in meters added on top of RoadWidth/2 + RoadShoulderWidth. All foliage (trees, grass) is blocked within this total radius from road center.")]
        public float FoliageExclusionBuffer = 4f;

        [Header("Biomes")]
        public BiomeDefinition[] BiomeDefinitions;

        [Header("Terrain Layers")]
        public TerrainLayer[] TerrainLayers;
    }
}