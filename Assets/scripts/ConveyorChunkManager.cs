using UnityEngine;
using System.Collections.Generic;

public enum BiomeType { Forest, Desert, Town }

/// <summary>
/// Высокооптимизированный менеджер генерации мира.
/// Поддерживает безопасные зоны для дороги, масштаб биомов и органичный скейл пропсов.
/// </summary>
public class ConveyorChunkManager : MonoBehaviour
{
    [System.Serializable]
    public struct BiomeProps
    {
        public BiomeType biomeType;
        public GameObject[] prefabs;
    }

    [Header("Core References")]
    [SerializeField] private Transform worldContainer;
    [SerializeField] private Transform carTransform;

    [Header("Chunk Prefabs")]
    [SerializeField] private GameObject forestChunkPrefab;
    [SerializeField] private GameObject desertChunkPrefab;
    [SerializeField] private GameObject townChunkPrefab;

    [Header("Prop Configuration")]
    [SerializeField] private BiomeProps[] biomePropsConfigs;
    [SerializeField] private Transform propContainer;
    
    [Tooltip("Сколько объектов спавнить на ОДИН чанк")]
    [SerializeField] private int propsPerChunk = 40; 
    
    [Tooltip("Общая зона спавна (Ширина X, Длина Z)")]
    [SerializeField] private Vector2 propSpawnArea = new Vector2(60f, 50f);
    
    [Tooltip("Ширина чистой зоны в центре (Дорога). Сюда деревья не залезут!")]
    [SerializeField] private float roadWidth = 12f;

    [Tooltip("Разброс размера пропсов (min, max) для естественности")]
    [SerializeField] private Vector2 propScaleRange = new Vector2(0.8f, 1.5f);

    [Header("Biome & World Settings")]
    [SerializeField] private int globalSeed = 1337;
    [SerializeField] private float chunkSize = 50f;
    
    [Tooltip("Чем МЕНЬШЕ значение (напр. 0.02), тем БОЛЬШЕ размер одного биома.")]
    [SerializeField] private float biomeScale = 0.03f; 
    
    [SerializeField] private int viewDistanceAhead = 5;
    [SerializeField] private int viewDistanceBehind = 2;

    // --- Архитектура Zero GC & O(1) ---
    private Dictionary<BiomeType, Queue<GameObject>> chunkPool;
    private Dictionary<GameObject, BiomeType> chunkInstanceToTypeMap;
    private Dictionary<GameObject, Queue<GameObject>> propPool;
    private Dictionary<GameObject, GameObject> propInstanceToPrefabMap;
    private Dictionary<BiomeType, GameObject[]> biomeToPrefabsMap;
    private Dictionary<int, GameObject> activeChunks;
    private Dictionary<int, List<GameObject>> activePropsMap;
    private Stack<List<GameObject>> listPool;
    private List<int> keysToRemoveCache = new List<int>();

    private void Start()
    {
        InitializeStructures();
        ForceUpdateChunks(); 
    }

    private void Update()
    {
        ForceUpdateChunks();
    }

    private void ForceUpdateChunks()
    {
        if (carTransform == null || worldContainer == null) return;

        float virtualDistance = carTransform.position.z - worldContainer.position.z;
        int currentChunkIndex = Mathf.FloorToInt(virtualDistance / chunkSize);

        int startIndex = currentChunkIndex - viewDistanceBehind;
        int endIndex = currentChunkIndex + viewDistanceAhead;

        ManageChunks(startIndex, endIndex);
    }

    private void InitializeStructures()
    {
        chunkPool = new Dictionary<BiomeType, Queue<GameObject>>()
        {
            { BiomeType.Forest, new Queue<GameObject>() },
            { BiomeType.Desert, new Queue<GameObject>() },
            { BiomeType.Town, new Queue<GameObject>() }
        };

        chunkInstanceToTypeMap = new Dictionary<GameObject, BiomeType>();
        propPool = new Dictionary<GameObject, Queue<GameObject>>();
        propInstanceToPrefabMap = new Dictionary<GameObject, GameObject>();
        biomeToPrefabsMap = new Dictionary<BiomeType, GameObject[]>();
        
        activeChunks = new Dictionary<int, GameObject>();
        activePropsMap = new Dictionary<int, List<GameObject>>();
        listPool = new Stack<List<GameObject>>();

        foreach (var config in biomePropsConfigs)
        {
            if (config.prefabs != null && config.prefabs.Length > 0)
            {
                biomeToPrefabsMap[config.biomeType] = config.prefabs;
            }
        }
    }

    private void ManageChunks(int startIndex, int endIndex)
    {
        keysToRemoveCache.Clear();

        foreach (var kvp in activeChunks)
        {
            if (kvp.Key < startIndex || kvp.Key > endIndex)
            {
                DespawnChunk(kvp.Key);
                keysToRemoveCache.Add(kvp.Key);
            }
        }

        foreach (int key in keysToRemoveCache)
        {
            activeChunks.Remove(key);
        }

        for (int i = startIndex; i <= endIndex; i++)
        {
            if (!activeChunks.ContainsKey(i))
            {
                SpawnChunk(i);
            }
        }
    }

    private void SpawnChunk(int index)
    {
        // Умножаем индекс на biomeScale. Чем меньше biomeScale, тем дольше держится один биом.
        float noiseValue = Mathf.PerlinNoise(globalSeed + (index * biomeScale), globalSeed);
        BiomeType nextBiome = DetermineBiome(noiseValue);

        GameObject chunk = GetChunkFromPool(nextBiome);
        float exactZ = index * chunkSize;
        
        chunk.transform.SetLocalPositionAndRotation(new Vector3(0, 0, exactZ), Quaternion.identity);
        activeChunks.Add(index, chunk);
        
        List<GameObject> chunkProps = GetListFromPool();
        activePropsMap.Add(index, chunkProps);

        SpawnProps(index, nextBiome, exactZ, chunkProps);
    }

    private void SpawnProps(int chunkIndex, BiomeType biome, float baseZ, List<GameObject> targetList)
    {
        if (!biomeToPrefabsMap.TryGetValue(biome, out GameObject[] prefabs)) return;

        UnityEngine.Random.InitState(globalSeed + chunkIndex);

        for (int i = 0; i < propsPerChunk; i++)
        {
            GameObject selectedPrefab = prefabs[UnityEngine.Random.Range(0, prefabs.Length)];
            GameObject prop = GetPropFromPool(selectedPrefab);

            // ЛОГИКА БЕЗОПАСНОЙ ДОРОГИ
            float randX;
            float halfRoad = roadWidth / 2f;
            float halfArea = propSpawnArea.x / 2f;

            // С вероятностью 50% кидаем дерево НАПРАВО, иначе НАЛЕВО от дороги
            if (UnityEngine.Random.value > 0.5f)
            {
                randX = UnityEngine.Random.Range(halfRoad, halfArea); // Справа
            }
            else
            {
                randX = UnityEngine.Random.Range(-halfArea, -halfRoad); // Слева
            }

            float randZ = UnityEngine.Random.Range(-propSpawnArea.y * 0.5f, propSpawnArea.y * 0.5f);
            
            Vector3 localPos = new Vector3(randX, 0, baseZ + randZ);
            Quaternion localRot = Quaternion.Euler(0, UnityEngine.Random.Range(0f, 360f), 0);

            // Органичный скейл деревьев
            float randomScale = UnityEngine.Random.Range(propScaleRange.x, propScaleRange.y);
            prop.transform.localScale = new Vector3(randomScale, randomScale, randomScale);

            prop.transform.SetLocalPositionAndRotation(localPos, localRot);
            prop.SetActive(true);

            targetList.Add(prop);
        }
    }

    private GameObject GetPropFromPool(GameObject prefab)
    {
        if (!propPool.ContainsKey(prefab)) propPool[prefab] = new Queue<GameObject>();

        GameObject prop;
        if (propPool[prefab].Count > 0)
        {
            prop = propPool[prefab].Dequeue();
        }
        else
        {
            prop = Instantiate(prefab, propContainer);
            propInstanceToPrefabMap[prop] = prefab; 
        }
        return prop;
    }

    private void DespawnChunk(int index)
    {
        GameObject chunk = activeChunks[index];
        chunk.SetActive(false);

        BiomeType type = chunkInstanceToTypeMap[chunk];
        chunkPool[type].Enqueue(chunk);

        if (activePropsMap.TryGetValue(index, out List<GameObject> props))
        {
            foreach (GameObject prop in props)
            {
                prop.SetActive(false);
                GameObject originalPrefab = propInstanceToPrefabMap[prop];
                propPool[originalPrefab].Enqueue(prop);
            }
            
            ReturnListToPool(props); 
            activePropsMap.Remove(index);
        }
    }

    private GameObject GetChunkFromPool(BiomeType type)
    {
        if (chunkPool[type].Count > 0)
        {
            GameObject chunk = chunkPool[type].Dequeue();
            chunk.SetActive(true);
            return chunk;
        }

        GameObject prefab = type switch
        {
            BiomeType.Forest => forestChunkPrefab,
            BiomeType.Desert => desertChunkPrefab,
            BiomeType.Town => townChunkPrefab,
            _ => forestChunkPrefab
        };

        GameObject newChunk = Instantiate(prefab, worldContainer);
        newChunk.name = $"{type}_Chunk";
        chunkInstanceToTypeMap[newChunk] = type; 
        
        return newChunk;
    }

    private BiomeType DetermineBiome(float noise)
    {
        if (noise < 0.33f) return BiomeType.Desert;
        if (noise < 0.66f) return BiomeType.Forest;
        return BiomeType.Town;
    }

    private List<GameObject> GetListFromPool() => listPool.Count > 0 ? listPool.Pop() : new List<GameObject>(propsPerChunk);
    private void ReturnListToPool(List<GameObject> list) { list.Clear(); listPool.Push(list); }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (worldContainer == null) return;

        if (Application.isPlaying)
        {
            foreach (var kvp in activeChunks)
            {
                float zPos = kvp.Key * chunkSize;
                Vector3 center = worldContainer.TransformPoint(new Vector3(0, 0, zPos));
                
                // Рисуем общую зону (зеленая)
                Gizmos.color = new Color(0, 1, 0, 0.1f);
                Gizmos.DrawCube(center, new Vector3(propSpawnArea.x, 0.1f, propSpawnArea.y));

                // Рисуем чистую дорогу (красная)
                Gizmos.color = new Color(1, 0, 0, 0.3f);
                Gizmos.DrawCube(center, new Vector3(roadWidth, 0.2f, propSpawnArea.y));
            }
        }
    }
#endif
}