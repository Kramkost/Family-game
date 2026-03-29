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

        [Header("Height Noise — Base")]
        public float  HeightScale      = 50f;
        public float  HeightFrequency  = 0.003f;
        public int    HeightOctaves    = 5;
        public float  HeightPersistence = 0.5f;
        public float  HeightLacunarity = 2.0f;

        // NEW — Topography shaping controls (Requirement 3)
        [Header("Height Shaping (NEW)")]
        [Tooltip("Power curve exponent. >1 = sharp peaks + flat plains (mountains). " +
                 "<1 = rounded, bubbly hills. 1 = flat standard fBm.")]
        [Range(0.1f, 4.0f)]
        public float HeightExponent = 1.0f;

        [Tooltip("Blend weight of ridged multifractal noise. " +
                 "0 = pure standard fBm (rolling hills). " +
                 "1 = pure ridged (sharp mountain ridges).")]
        [Range(0f, 1f)]
        public float RidgeWeight = 0.0f;

        [Tooltip("Number of terrace steps. 0 = disabled. " +
                 "Use 3-6 for canyon/mesa style biomes.")]
        [Range(0, 8)]
        public int TerraceCount = 0;
        // END NEW

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
        public float HouseRoadOffset  = 8f;
        public float HouseMinSpacing  = 40f;
    }
}
