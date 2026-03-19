using UnityEngine;
using System.Collections.Generic;
using Mirror; // Обязательно для NetworkTime

/// <summary>
/// Процедурный генератор мира с динамическим количеством биомов, 
/// весовым спавном пропсов, типизированными сокетами, защитой от наложения
/// и системой Временных Ивентов (смена биома по таймеру сервера).
/// </summary>
public class ConveyorChunkManager : MonoBehaviour
{
    #region STRUCTS & CONFIGS

    [System.Serializable]
    public struct PropConfig
    {
        [Tooltip("Префаб объекта (Дерево, Камень, Пустышка-Дом)")]
        public GameObject prefab;
        
        [Tooltip("Вес вероятности внутри биома (например: Трава = 500, Дом = 10)")]
        public int weight;
        
        [Tooltip("Если True - ищет PropSocket на чанке. Если False - спавнится в случайной зоне.")]
        public bool requiresSocket;
    }

    [System.Serializable]
    public struct BiomeConfig
    {
        [Tooltip("Название для удобства в Инспекторе (Forest, Desert, Town)")]
        public string biomeName;
        
        [Tooltip("Префаб платформы чанка")]
        public GameObject chunkPrefab;
        
        [Tooltip("Шанс генерации этого биома по сравнению с другими (например: Лес = 100, Пустыня = 30)")]
        public int spawnWeight;
        
        [Tooltip("Список объектов, которые могут тут появиться")]
        public PropConfig[] props;
    }

    #endregion

    #region INSPECTOR VARIABLES

    [Header("Core References")]
    [SerializeField] private Transform worldContainer;
    [SerializeField] private Transform carTransform;
    [SerializeField] private Transform propContainer;

    [Header("Biome Setup")]
    [Tooltip("Список биомов. Настрой им шансы (Spawn Weight)")]
    [SerializeField] private BiomeConfig[] biomes;

    [Header("Time Events (Ивенты)")]
    [Tooltip("Индекс биома (от 0), который будет появляться по таймеру (напр. 2 - Город)")]
    [SerializeField] private int eventBiomeIndex = 2;
    
    [Tooltip("Каждые сколько минут реального времени запускать этот биом?")]
    [SerializeField] private float eventIntervalMinutes = 15f;
    
    [Tooltip("Сколько минут длится ивент? (Например, 1 минута езды через город)")]
    [SerializeField] private float eventDurationMinutes = 1f;

    [Header("Generation Settings")]
    [SerializeField] private int globalSeed = 1337;
    [SerializeField] private float chunkSize = 50f;
    [SerializeField] private float biomeScale = 0.03f; 
    [SerializeField] private int viewDistanceAhead = 5;
    [SerializeField] private int viewDistanceBehind = 2;

    [Header("Prop Rules")]
    [SerializeField] private int propsPerChunk = 40; 
    [SerializeField] private Vector2 propSpawnArea = new Vector2(60f, 50f);
    [SerializeField] private float roadWidth = 12f;
    [SerializeField] private Vector2 propScaleRange = new Vector2(0.8f, 1.5f);

    [Header("Spatial Clearance (Защита от наложения)")]
    [Tooltip("Минимальное расстояние между обычными пропсами")]
    [SerializeField] private float minPropDistance = 2.5f;
    [Tooltip("Сколько раз пытаться найти свободное место, прежде чем сдаться")]
    [SerializeField] private int maxSpawnAttempts = 5;

    #endregion

    #region INTERNAL CACHE (Zero GC)

    private Dictionary<int, Queue<GameObject>> chunkPool;
    private Dictionary<GameObject, int> chunkInstanceToBiomeIndexMap;
    private Dictionary<GameObject, Queue<GameObject>> propPool;
    private Dictionary<GameObject, GameObject> propInstanceToPrefabMap;
    
    private Dictionary<int, int> biomeTotalWeightMap;
    private Dictionary<GameObject, List<Transform>> chunkSocketsMap;

    private Dictionary<int, GameObject> activeChunks;
    private Dictionary<int, List<GameObject>> activePropsMap;
    
    private Stack<List<GameObject>> listPool;
    private List<int> keysToRemoveCache = new List<int>();
    private List<Transform> usedSocketsCache = new List<Transform>();
    
    private List<Vector2> placedPositionsCache = new List<Vector2>();

    private int highestWeightBiomeIndex = 0; // Кэш для стартовой зоны

    #endregion

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
        if (carTransform == null || worldContainer == null || biomes.Length == 0) return;

        float virtualDistance = carTransform.position.z - worldContainer.position.z;
        int currentChunkIndex = Mathf.FloorToInt(virtualDistance / chunkSize);

        int startIndex = currentChunkIndex - viewDistanceBehind;
        int endIndex = currentChunkIndex + viewDistanceAhead;

        ManageChunks(startIndex, endIndex);
    }

    private void InitializeStructures()
    {
        chunkPool = new Dictionary<int, Queue<GameObject>>();
        chunkInstanceToBiomeIndexMap = new Dictionary<GameObject, int>();
        propPool = new Dictionary<GameObject, Queue<GameObject>>();
        propInstanceToPrefabMap = new Dictionary<GameObject, GameObject>();
        biomeTotalWeightMap = new Dictionary<int, int>();
        chunkSocketsMap = new Dictionary<GameObject, List<Transform>>();
        
        activeChunks = new Dictionary<int, GameObject>();
        activePropsMap = new Dictionary<int, List<GameObject>>();
        listPool = new Stack<List<GameObject>>();

        int maxWeight = -1; 

        for (int i = 0; i < biomes.Length; i++)
        {
            chunkPool[i] = new Queue<GameObject>();
            
            if (biomes[i].props != null && biomes[i].props.Length > 0)
            {
                int totalWeight = 0;
                foreach (var prop in biomes[i].props)
                {
                    totalWeight += prop.weight;
                }
                biomeTotalWeightMap[i] = totalWeight;
            }

            // Запоминаем биом с самым большим весом для стартовой зоны
            if (biomes[i].spawnWeight > maxWeight)
            {
                maxWeight = biomes[i].spawnWeight;
                highestWeightBiomeIndex = i;
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
        int nextBiomeIndex = DetermineBiomeIndex(noiseValue, index); // Передаем индекс чанка

        GameObject chunk = GetChunkFromPool(nextBiomeIndex);
        float exactZ = index * chunkSize;
        
        chunk.transform.SetLocalPositionAndRotation(new Vector3(0, 0, exactZ), Quaternion.identity);
        activeChunks.Add(index, chunk);
        
        List<GameObject> chunkProps = GetListFromPool();
        activePropsMap.Add(index, chunkProps);

        SpawnProps(index, nextBiomeIndex, exactZ, chunk, chunkProps);
    }

    private void SpawnProps(int chunkIndex, int biomeIndex, float baseZ, GameObject chunk, List<GameObject> targetList)
    {
        if (!biomeTotalWeightMap.TryGetValue(biomeIndex, out int totalWeight)) return;

        UnityEngine.Random.InitState(globalSeed + chunkIndex);

        List<Transform> availableSockets = chunkSocketsMap[chunk];
        
        usedSocketsCache.Clear();
        placedPositionsCache.Clear();

        PropConfig[] currentProps = biomes[biomeIndex].props;
        float sqrMinDist = minPropDistance * minPropDistance; 

        for (int i = 0; i < propsPerChunk; i++)
        {
            PropConfig selectedConfig = GetWeightedRandomProp(currentProps, totalWeight);
            GameObject prop = GetPropFromPool(selectedConfig.prefab);

            Vector3 localPos = Vector3.zero;
            Quaternion localRot = Quaternion.identity;
            bool spawnSuccess = false;

            if (selectedConfig.requiresSocket)
            {
                Transform selectedSocket = null;
                foreach (Transform socket in availableSockets)
                {
                    if (!usedSocketsCache.Contains(socket)) 
                    {
                        selectedSocket = socket;
                        break;
                    }
                }

                if (selectedSocket != null)
                {
                    usedSocketsCache.Add(selectedSocket); 
                    localPos = new Vector3(selectedSocket.localPosition.x, selectedSocket.localPosition.y, baseZ + selectedSocket.localPosition.z);
                    localRot = selectedSocket.localRotation;
                    prop.transform.localScale = Vector3.one; 
                    spawnSuccess = true;
                }
            }
            else
            {
                float halfRoad = roadWidth / 2f;
                float halfArea = propSpawnArea.x / 2f;

                for (int attempt = 0; attempt < maxSpawnAttempts; attempt++)
                {
                    float randX = UnityEngine.Random.value > 0.5f 
                        ? UnityEngine.Random.Range(halfRoad, halfArea) 
                        : UnityEngine.Random.Range(-halfArea, -halfRoad); 

                    float randZ = UnityEngine.Random.Range(-propSpawnArea.y * 0.5f, propSpawnArea.y * 0.5f);
                    Vector2 testPos2D = new Vector2(randX, randZ);

                    bool hasClearance = true;
                    for (int p = 0; p < placedPositionsCache.Count; p++)
                    {
                        if ((placedPositionsCache[p] - testPos2D).sqrMagnitude < sqrMinDist)
                        {
                            hasClearance = false;
                            break;
                        }
                    }

                    if (hasClearance)
                    {
                        placedPositionsCache.Add(testPos2D);
                        localPos = new Vector3(randX, 0, baseZ + randZ);
                        localRot = Quaternion.Euler(0, UnityEngine.Random.Range(0f, 360f), 0);
                        
                        float randomScale = UnityEngine.Random.Range(propScaleRange.x, propScaleRange.y);
                        prop.transform.localScale = new Vector3(randomScale, randomScale, randomScale);
                        
                        spawnSuccess = true;
                        break; 
                    }
                }
            }

            if (spawnSuccess)
            {
                prop.transform.SetLocalPositionAndRotation(localPos, localRot);
                prop.SetActive(true);
                targetList.Add(prop);
            }
            else
            {
                propPool[selectedConfig.prefab].Enqueue(prop);
            }
        }
    }

    private PropConfig GetWeightedRandomProp(PropConfig[] configs, int totalWeight)
    {
        if (totalWeight <= 0) return configs[0];

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
        return configs[0]; 
    }

    private GameObject GetPropFromPool(GameObject prefab)
    {
        if (!propPool.ContainsKey(prefab)) propPool[prefab] = new Queue<GameObject>();

        if (propPool[prefab].Count > 0) return propPool[prefab].Dequeue();
        
        GameObject prop = Instantiate(prefab, propContainer);
        propInstanceToPrefabMap[prop] = prefab; 
        return prop;
    }

    private void DespawnChunk(int index)
    {
        GameObject chunk = activeChunks[index];
        chunk.SetActive(false);

        int typeIndex = chunkInstanceToBiomeIndexMap[chunk];
        chunkPool[typeIndex].Enqueue(chunk);

        if (activePropsMap.TryGetValue(index, out List<GameObject> props))
        {
            foreach (GameObject prop in props)
            {
                prop.SetActive(false);
                GameObject originalPrefab = propInstanceToPrefabMap[prop];
                propPool[originalPrefab].Enqueue(prop);
            }
            
            props.Clear();
            listPool.Push(props); 
            activePropsMap.Remove(index);
        }
    }

    private GameObject GetChunkFromPool(int biomeIndex)
    {
        if (chunkPool[biomeIndex].Count > 0)
        {
            GameObject chunk = chunkPool[biomeIndex].Dequeue();
            chunk.SetActive(true);
            return chunk;
        }

        GameObject prefab = biomes[biomeIndex].chunkPrefab;
        GameObject newChunk = Instantiate(prefab, worldContainer);
        newChunk.name = $"{biomes[biomeIndex].biomeName}_Chunk";
        chunkInstanceToBiomeIndexMap[newChunk] = biomeIndex; 
        
        List<Transform> sockets = new List<Transform>();
        foreach (Transform child in newChunk.transform)
        {
            if (child.CompareTag("PropSocket")) sockets.Add(child);
        }
        chunkSocketsMap[newChunk] = sockets;

        return newChunk;
    }

    /// <summary>
    /// Математическое распределение биомов: Safe Zone -> Таймер -> Веса.
    /// </summary>
    private int DetermineBiomeIndex(float noise, int chunkIndex)
    {
        int count = biomes.Length;
        if (count == 0) return 0;

        // --- 0. СТАРТОВАЯ ЗОНА (Safe Zone) 100% гарантия ---
        // Если чанк находится в зоне начальной прорисовки (вокруг нуля), 
        // мы ЖЕСТКО спавним самый частый биом.
        if (Mathf.Abs(chunkIndex) <= viewDistanceAhead)
        {
            return highestWeightBiomeIndex;
        }

        // --- 1. ПРОВЕРКА ВРЕМЕННОГО ИВЕНТА ---
        if (NetworkClient.active || NetworkServer.active)
        {
            float currentMinutes = (float)NetworkTime.time / 60f;
            
            // Защита от старта: Ивент не начнется на 0-й минуте. Только после первого интервала.
            if (currentMinutes >= eventIntervalMinutes)
            {
                if (currentMinutes % eventIntervalMinutes < eventDurationMinutes)
                {
                    return Mathf.Clamp(eventBiomeIndex, 0, count - 1);
                }
            }
        }

        // --- 2. СТАНДАРТНАЯ ГЕНЕРАЦИЯ (По весам) ---
        int totalWeight = 0;
        for (int i = 0; i < count; i++)
        {
            totalWeight += biomes[i].spawnWeight;
        }

        if (totalWeight <= 0) return 0;

        float targetWeight = noise * totalWeight;
        int currentWeight = 0;

        for (int i = 0; i < count; i++)
        {
            currentWeight += biomes[i].spawnWeight;
            if (targetWeight <= currentWeight)
            {
                return i;
            }
        }

        return 0; // Fallback
    }

    private List<GameObject> GetListFromPool() => listPool.Count > 0 ? listPool.Pop() : new List<GameObject>(propsPerChunk);

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
                
                Gizmos.color = new Color(0, 1, 0, 0.05f);
                Gizmos.DrawCube(center, new Vector3(propSpawnArea.x, 0.1f, propSpawnArea.y));

                Gizmos.color = new Color(1, 0, 0, 0.2f);
                Gizmos.DrawCube(center, new Vector3(roadWidth, 0.2f, propSpawnArea.y));
            }
        }
    }
#endif
}