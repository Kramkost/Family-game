using UnityEngine;
using System.Collections.Generic;

public enum BiomeType { Forest, Desert, Town }

/// <summary>
/// Высокооптимизированный менеджер процедурной генерации с поддержкой взвешенных пулов пропсов
/// и типизированных сокетов (PropSockets) для размещения крупных структур (Houses) без коллизий.
/// </summary>
public class ConveyorChunkManager : MonoBehaviour
{
    /// <summary>
    /// Конфигурация отдельного пропса с весом (Rarity).
    /// </summary>
    [System.Serializable]
    public struct PropConfig
    {
        [Tooltip("Префаб объекта (Дерево, Дом, Камень)")]
        public GameObject prefab;
        
        [Tooltip("Вероятность появления. Чем больше, тем чаще (Трава: 500, Дом: 10)")]
        public int weight;
        
        [Tooltip("Если True, спавнится ТОЛЬКО в спец. сокетах на чанке. Если False - в случайной зоне.")]
        public bool requiresSocket;
    }

    /// <summary>
    /// Коллекция пропсов для конкретного биома.
    /// </summary>
    [System.Serializable]
    public struct BiomeProps
    {
        public BiomeType biomeType;
        public PropConfig[] props;
    }

    [Header("Core References")]
    [SerializeField] private Transform worldContainer;
    [SerializeField] private Transform carTransform;

    [Header("Chunk Prefabs")]
    [Tooltip("Префаб чанка может содержать пустышки с тегом 'PropSocket' для спавна больших объектов.")]
    [SerializeField] private GameObject forestChunkPrefab;
    [SerializeField] private GameObject desertChunkPrefab;
    [SerializeField] private GameObject townChunkPrefab;

    [Header("Prop Configuration")]
    [SerializeField] private BiomeProps[] biomePropsConfigs;
    [SerializeField] private Transform propContainer;
    [SerializeField] private int propsPerChunk = 40; 
    [SerializeField] private Vector2 propSpawnArea = new Vector2(60f, 50f);
    [SerializeField] private float roadWidth = 12f;
    [SerializeField] private Vector2 propScaleRange = new Vector2(0.8f, 1.5f);

    [Header("Biome & World Settings")]
    [SerializeField] private int globalSeed = 1337;
    [SerializeField] private float chunkSize = 50f;
    [SerializeField] private float biomeScale = 0.03f; 
    [SerializeField] private int viewDistanceAhead = 5;
    [SerializeField] private int viewDistanceBehind = 2;

    // --- Архитектура Zero GC & O(1) ---
    private Dictionary<BiomeType, Queue<GameObject>> chunkPool;
    private Dictionary<GameObject, BiomeType> chunkInstanceToTypeMap;
    private Dictionary<GameObject, Queue<GameObject>> propPool;
    private Dictionary<GameObject, GameObject> propInstanceToPrefabMap;
    
    // Кэш для взвешенного рандома и сокетов
    private Dictionary<BiomeType, PropConfig[]> biomeToPropsMap;
    private Dictionary<BiomeType, int> biomeTotalWeightMap;
    
    // Кэш сокетов для каждого инстанса чанка
    private Dictionary<GameObject, List<Transform>> chunkSocketsMap;

    private Dictionary<int, GameObject> activeChunks;
    private Dictionary<int, List<GameObject>> activePropsMap;
    private Stack<List<GameObject>> listPool;
    private List<int> keysToRemoveCache = new List<int>();
    private List<Transform> usedSocketsCache = new List<Transform>();

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
        biomeToPropsMap = new Dictionary<BiomeType, PropConfig[]>();
        biomeTotalWeightMap = new Dictionary<BiomeType, int>();
        chunkSocketsMap = new Dictionary<GameObject, List<Transform>>();
        
        activeChunks = new Dictionary<int, GameObject>();
        activePropsMap = new Dictionary<int, List<GameObject>>();
        listPool = new Stack<List<GameObject>>();

        foreach (var config in biomePropsConfigs)
        {
            if (config.props != null && config.props.Length > 0)
            {
                biomeToPropsMap[config.biomeType] = config.props;
                
                int totalWeight = 0;
                foreach (var prop in config.props)
                {
                    totalWeight += prop.weight;
                }
                biomeTotalWeightMap[config.biomeType] = totalWeight;
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
        float noiseValue = Mathf.PerlinNoise(globalSeed + (index * biomeScale), globalSeed);
        BiomeType nextBiome = DetermineBiome(noiseValue);

        GameObject chunk = GetChunkFromPool(nextBiome);
        float exactZ = index * chunkSize;
        
        chunk.transform.SetLocalPositionAndRotation(new Vector3(0, 0, exactZ), Quaternion.identity);
        activeChunks.Add(index, chunk);
        
        List<GameObject> chunkProps = GetListFromPool();
        activePropsMap.Add(index, chunkProps);

        SpawnProps(index, nextBiome, exactZ, chunk, chunkProps);
    }

/// <summary>
    /// Интеллектуальный спавн с учетом сокетов чанка и взвешенной редкости объектов.
    /// </summary>
    private void SpawnProps(int chunkIndex, BiomeType biome, float baseZ, GameObject chunk, List<GameObject> targetList)
    {
        if (!biomeToPropsMap.TryGetValue(biome, out PropConfig[] propConfigs)) return;
        if (!biomeTotalWeightMap.TryGetValue(biome, out int totalWeight)) return;

        UnityEngine.Random.InitState(globalSeed + chunkIndex);

        // Получаем доступные сокеты для этого конкретного чанка
        List<Transform> availableSockets = chunkSocketsMap[chunk];
        
        // Очищаем наш глобальный кэш перед спавном (Zero GC)
        usedSocketsCache.Clear();

        for (int i = 0; i < propsPerChunk; i++)
        {
            PropConfig selectedConfig = GetWeightedRandomProp(propConfigs, totalWeight);
            GameObject prop = GetPropFromPool(selectedConfig.prefab);

            Vector3 localPos = Vector3.zero;
            Quaternion localRot = Quaternion.identity;
            bool spawnSuccess = true;

            if (selectedConfig.requiresSocket)
            {
                // Логика спавна Домов/Крупных объектов
                Transform selectedSocket = null;
                
                // Ищем свободный сокет детерминированно
                foreach (Transform socket in availableSockets)
                {
                    if (!usedSocketsCache.Contains(socket)) // <--- ИСПОЛЬЗУЕМ КЭШ
                    {
                        selectedSocket = socket;
                        break;
                    }
                }

                if (selectedSocket != null)
                {
                    usedSocketsCache.Add(selectedSocket); // <--- ИСПОЛЬЗУЕМ КЭШ
                    // Сокеты имеют локальную позицию относительно чанка
                    localPos = new Vector3(selectedSocket.localPosition.x, selectedSocket.localPosition.y, baseZ + selectedSocket.localPosition.z);
                    localRot = selectedSocket.localRotation;
                    
                    prop.transform.localScale = Vector3.one; 
                }
                else
                {
                    // Сокетов нет, отменяем спавн этого редкого объекта, возвращаем в пул
                    ReturnPropToPool(prop, selectedConfig.prefab);
                    spawnSuccess = false;
                }
            }
            else
            {

                // Логика спавна случайной Природы
                float randX;
                float halfRoad = roadWidth / 2f;
                float halfArea = propSpawnArea.x / 2f;

                if (UnityEngine.Random.value > 0.5f) randX = UnityEngine.Random.Range(halfRoad, halfArea); 
                else randX = UnityEngine.Random.Range(-halfArea, -halfRoad); 

                float randZ = UnityEngine.Random.Range(-propSpawnArea.y * 0.5f, propSpawnArea.y * 0.5f);
                
                localPos = new Vector3(randX, 0, baseZ + randZ);
                localRot = Quaternion.Euler(0, UnityEngine.Random.Range(0f, 360f), 0);

                float randomScale = UnityEngine.Random.Range(propScaleRange.x, propScaleRange.y);
                prop.transform.localScale = new Vector3(randomScale, randomScale, randomScale);
            }

            if (spawnSuccess)
            {
                prop.transform.SetLocalPositionAndRotation(localPos, localRot);
                prop.SetActive(true);
                targetList.Add(prop);
            }
        }
    }

    /// <summary>
    /// Выбор префаба на основе весового распределения.
    /// </summary>
    private PropConfig GetWeightedRandomProp(PropConfig[] configs, int totalWeight)
    {
        int randomWeight = UnityEngine.Random.Range(0, totalWeight);
        int currentWeight = 0;

        foreach (var config in configs)
        {
            currentWeight += config.weight;
            if (randomWeight < currentWeight)
            {
                return config;
            }
        }
        return configs[0]; // Fallback
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

    private void ReturnPropToPool(GameObject prop, GameObject prefab)
    {
        propPool[prefab].Enqueue(prop);
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
        
        // Кэшируем сокеты при инстанциации
        List<Transform> sockets = new List<Transform>();
        foreach (Transform child in newChunk.transform)
        {
            if (child.CompareTag("PropSocket"))
            {
                sockets.Add(child);
            }
        }
        chunkSocketsMap[newChunk] = sockets;

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
                
                Gizmos.color = new Color(0, 1, 0, 0.1f);
                Gizmos.DrawCube(center, new Vector3(propSpawnArea.x, 0.1f, propSpawnArea.y));

                Gizmos.color = new Color(1, 0, 0, 0.3f);
                Gizmos.DrawCube(center, new Vector3(roadWidth, 0.2f, propSpawnArea.y));
            }
        }
    }
#endif
}