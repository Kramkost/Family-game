using UnityEngine;

namespace ProceduralTerrain.Core
{
    [CreateAssetMenu(menuName = "ProceduralTerrain/Biome Definition", fileName = "BiomeDef_New")]
    public class BiomeDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string    BiomeName   = "Unnamed Biome";
        public BiomeType BiomeType   = BiomeType.GreenPlains;

        [Header("Terrain Textures (Splat layers)")]
        [Tooltip("Terrain splatmap layer index this biome primarily uses.")]
        public int       PrimarySplatLayer  = 0;
        public int       SecondarySplatLayer = 1;

        [Header("Height Noise")]
        public float  HeightScale      = 50f;
        public float  HeightFrequency  = 0.003f;
        public int    HeightOctaves    = 5;
        public float  HeightPersistence = 0.5f;
        public float  HeightLacunarity = 2.0f;

        [Header("Moisture / Temperature Ranges (for blending)")]
        [Range(0f, 1f)] public float MoistureMin = 0f;
        [Range(0f, 1f)] public float MoistureMax = 1f;
        [Range(0f, 1f)] public float TemperatureMin = 0f;
        [Range(0f, 1f)] public float TemperatureMax = 1f;

        [Header("Foliage")]
        public GameObject[] TreePrefabs;
        public GameObject[] BushPrefabs;
        [Range(0f, 1f)] public float TreeDensity   = 0.3f;
        [Range(0f, 1f)] public float BushDensity   = 0.2f;
        [Range(0f, 1f)] public float GrassDensity  = 0.5f;
        public int GrassDetailLayer = 0;

        [Header("POIs")]
        public GameObject[] HousePrefabs;
        [Range(0f, 1f)] public float HouseSpawnProbability = 0.4f;
        public float HouseRoadOffset  = 8f;    // Side offset from road edge (meters)
        public float HouseMinSpacing  = 40f;   // Min distance between houses
    }
}