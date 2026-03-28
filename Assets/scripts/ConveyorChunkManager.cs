using UnityEngine;
using System.Collections.Generic;
using Mirror;

public class ConveyorChunkManager : MonoBehaviour
{
    [System.Serializable]
    public struct PropConfig
    {
        public GameObject prefab;
        public int weight;
        public bool requiresSocket;
        [Tooltip("Индивидуальный разброс размера (X - мин, Y - макс). Если оставить 0,0 - используется глобальный Prop Scale Range.")]
        public Vector2 scaleRange;
    }

    [System.Serializable]
    public struct BiomeConfig
    {
        public string biomeName;
        public GameObject chunkPrefab;
        public int spawnWeight;
        public PropConfig[] props;
    }

    [System.Serializable]
    public struct LootConfig
    {
        public GameObject prefab;
        public int weight;
        [Tooltip("Разброс размера лута (X - мин, Y - макс). Если оставить 0,0 - размер будет стандартным (1,1).")]
        public Vector2 scaleRange;
    }

    [Header("Core References")]
    [SerializeField] private Transform worldContainer;
    [SerializeField] private Transform carTransform;
    [SerializeField] private Transform propContainer;
    [SerializeField] private Transform lootContainer;

    [Header("Biome Setup")]
    [SerializeField] private BiomeConfig[] biomes;

    [Header("Loot Settings")]
    [SerializeField] private LootConfig[] globalLootTable;
    [SerializeField] [Range(0f, 1f)] private float lootSpawnChance = 0.5f;

    [Header("Time Events")]
    [SerializeField] private int eventBiomeIndex = 2;
    [SerializeField] private float eventIntervalMinutes = 15f;
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
    
    [Tooltip("Глобальный размер для пропсов, у которых Scale Range оставлен по умолчанию (0,0)")]
    [SerializeField] private Vector2 propScaleRange = new Vector2(0.8f, 1.5f);
    
    [SerializeField] private float minPropDistance = 2.5f;
    [SerializeField] private int maxSpawnAttempts = 5;

    private Dictionary<int, Queue<GameObject>> chunkPool;
    private Dictionary<GameObject, int> chunkInstanceToBiomeIndexMap;
    private Dictionary<GameObject, Queue<GameObject>> propPool;
    private Dictionary<GameObject, GameObject> propInstanceToPrefabMap;
    
    private Dictionary<GameObject, Queue<GameObject>> lootPool;
    private Dictionary<GameObject, GameObject> lootInstanceToPrefabMap;
    private Dictionary<GameObject, List<Transform>> propLootSocketsMap; 
    private Dictionary<int, List<GameObject>> activeLootMap; 
    private int totalLootWeight = 0;

    private Dictionary<int, int> biomeTotalWeightMap;
    private Dictionary<GameObject, List<Transform>> chunkSocketsMap;

    private Dictionary<int, GameObject> activeChunks;
    private Dictionary<int, List<GameObject>> activePropsMap;
    
    private Stack<List<GameObject>> listPool;
    private List<int> keysToRemoveCache = new List<int>();
    private List<Transform> usedSocketsCache = new List<Transform>();
    private List<Vector2> placedPositionsCache = new List<Vector2>();

    private int highestWeightBiomeIndex = 0; 

    private void Start()
    {
        InitializeStructures();
        ForceUpdateChunks(); 
    }

    private void Update() => ForceUpdateChunks();

    private void ForceUpdateChunks()
    {
        if (carTransform == null || worldContainer == null || biomes.Length == 0) return;

        float virtualDistance = carTransform.position.z - worldContainer.position.z;
        int currentChunkIndex = Mathf.FloorToInt(virtualDistance / chunkSize);

        ManageChunks(currentChunkIndex - viewDistanceBehind, currentChunkIndex + viewDistanceAhead);
    }

    private void InitializeStructures()
    {
        chunkPool = new Dictionary<int, Queue<GameObject>>();
        chunkInstanceToBiomeIndexMap = new Dictionary<GameObject, int>();
        propPool = new Dictionary<GameObject, Queue<GameObject>>();
        propInstanceToPrefabMap = new Dictionary<GameObject, GameObject>();
        biomeTotalWeightMap = new Dictionary<int, int>();
        chunkSocketsMap = new Dictionary<GameObject, List<Transform>>();
        
        lootPool = new Dictionary<GameObject, Queue<GameObject>>();
        lootInstanceToPrefabMap = new Dictionary<GameObject, GameObject>();
        propLootSocketsMap = new Dictionary<GameObject, List<Transform>>();
        activeLootMap = new Dictionary<int, List<GameObject>>();
        
        activeChunks = new Dictionary<int, GameObject>();
        activePropsMap = new Dictionary<int, List<GameObject>>();
        listPool = new Stack<List<GameObject>>();

        int maxWeight = -1; 

        for (int i = 0; i < biomes.Length; i++)
        {
            chunkPool[i] = new Queue<GameObject>();
            
            if (biomes[i].props != null)
            {
                int totalWeight = 0;
                foreach (var prop in biomes[i].props) totalWeight += prop.weight;
                biomeTotalWeightMap[i] = totalWeight;
            }

            if (biomes[i].spawnWeight > maxWeight)
            {
                maxWeight = biomes[i].spawnWeight;
                highestWeightBiomeIndex = i;
            }
        }

        foreach (var loot in globalLootTable) totalLootWeight += loot.weight;
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

        foreach (int key in keysToRemoveCache) activeChunks.Remove(key);

        for (int i = startIndex; i <= endIndex; i++)
        {
            if (!activeChunks.ContainsKey(i)) SpawnChunk(i);
        }
    }

    private void SpawnChunk(int index)
    {
        float noiseValue = Mathf.PerlinNoise(globalSeed + (index * biomeScale), globalSeed);
        int nextBiomeIndex = DetermineBiomeIndex(noiseValue, index); 

        GameObject chunk = GetChunkFromPool(nextBiomeIndex);
        float exactZ = index * chunkSize;
        
        chunk.transform.SetLocalPositionAndRotation(new Vector3(0, 0, exactZ), Quaternion.identity);
        activeChunks.Add(index, chunk);
        
        Physics.SyncTransforms();

        List<GameObject> chunkProps = GetListFromPool();
        List<GameObject> chunkLoot = GetListFromPool();
        
        activePropsMap.Add(index, chunkProps);
        activeLootMap.Add(index, chunkLoot);

        SpawnProps(index, nextBiomeIndex, exactZ, chunk, chunkProps, chunkLoot);
    }

    private void SpawnProps(int chunkIndex, int biomeIndex, float baseZ, GameObject chunk, List<GameObject> targetPropList, List<GameObject> targetLootList)
    {
        if (!biomeTotalWeightMap.TryGetValue(biomeIndex, out int totalWeight)) return;

        UnityEngine.Random.InitState(globalSeed + chunkIndex);
        List<Transform> availableSockets = chunkSocketsMap[chunk];
        
        usedSocketsCache.Clear();
        placedPositionsCache.Clear();

        PropConfig[] currentProps = biomes[biomeIndex].props;
        float sqrMinDist = minPropDistance * minPropDistance; 

        // Высчитываем половину ширины дороги. Все объекты должны быть ДАЛЬШЕ этой зоны по оси X.
        float halfRoad = roadWidth / 2f;
        float halfArea = propSpawnArea.x / 2f;

        for (int i = 0; i < propsPerChunk; i++)
        {
            PropConfig selectedConfig = GetWeightedRandomProp(currentProps, totalWeight);
            GameObject prop = GetPropFromPool(selectedConfig.prefab);

            Vector3 localPos = Vector3.zero;
            Quaternion localRot = Quaternion.identity;
            bool spawnSuccess = false;

            Vector2 activeScaleRange = selectedConfig.scaleRange != Vector2.zero ? selectedConfig.scaleRange : propScaleRange;
            float randomScale = UnityEngine.Random.Range(activeScaleRange.x, activeScaleRange.y);

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
                    prop.transform.localScale = new Vector3(randomScale, randomScale, randomScale); 
                    spawnSuccess = true;
                }
            }
            else
            {
                for (int attempt = 0; attempt < maxSpawnAttempts; attempt++)
                {
                    // МАТЕМАТИЧЕСКАЯ ГАРАНТИЯ: Мы выбираем координату X строго слева или строго справа от дороги.
                    // Знак > 0.5f определяет сторону. Мы никогда не выберем координату внутри roadWidth.
                    float randX = UnityEngine.Random.value > 0.5f 
                        ? UnityEngine.Random.Range(halfRoad + (randomScale / 2f), halfArea) // Справа от дороги
                        : UnityEngine.Random.Range(-halfArea, -halfRoad - (randomScale / 2f)); // Слева от дороги
                        
                    float randZ = UnityEngine.Random.Range(-propSpawnArea.y * 0.5f, propSpawnArea.y * 0.5f);
                    Vector2 testPos2D = new Vector2(randX, randZ);
                    
                    bool hasClearance = true;

                    // Проверяем наложение только с уже сгенерированными на этом чанке деревьями/камнями
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
                targetPropList.Add(prop);
                
                SpawnLootForProp(prop, targetLootList);
            }
            else
            {
                propPool[selectedConfig.prefab].Enqueue(prop);
            }
        }
    }

    private void SpawnLootForProp(GameObject prop, List<GameObject> targetLootList)
    {
        if (totalLootWeight <= 0 || globalLootTable.Length == 0) return;
        if (!propLootSocketsMap.TryGetValue(prop, out List<Transform> lootSockets)) return;

        foreach (Transform socket in lootSockets)
        {
            if (UnityEngine.Random.value <= lootSpawnChance)
            {
                LootConfig selectedLoot = GetWeightedRandomLoot();
                GameObject lootObj = GetLootFromPool(selectedLoot.prefab);
                
                lootObj.transform.SetPositionAndRotation(socket.position, socket.rotation);

                // Индивидуальный размер лута
                Vector2 activeLootScale = selectedLoot.scaleRange != Vector2.zero ? selectedLoot.scaleRange : Vector2.one;
                float rScale = UnityEngine.Random.Range(activeLootScale.x, activeLootScale.y);
                lootObj.transform.localScale = new Vector3(rScale, rScale, rScale);

                lootObj.SetActive(true);
                
                targetLootList.Add(lootObj);
            }
        }
    }

    private LootConfig GetWeightedRandomLoot()
    {
        int randomWeight = UnityEngine.Random.Range(0, totalLootWeight);
        int currentWeight = 0;

        foreach (var loot in globalLootTable)
        {
            currentWeight += loot.weight;
            if (randomWeight < currentWeight) return loot;
        }
        return globalLootTable[0];
    }

    private PropConfig GetWeightedRandomProp(PropConfig[] configs, int totalWeight)
    {
        if (totalWeight <= 0) return configs[0];
        int randomWeight = UnityEngine.Random.Range(0, totalWeight);
        int currentWeight = 0;

        foreach (var config in configs)
        {
            currentWeight += config.weight;
            if (randomWeight < currentWeight) return config;
        }
        return configs[0]; 
    }

    private GameObject GetPropFromPool(GameObject prefab)
    {
        if (!propPool.ContainsKey(prefab)) propPool[prefab] = new Queue<GameObject>();
        
        if (propPool[prefab].Count > 0) return propPool[prefab].Dequeue();
        
        GameObject prop = Instantiate(prefab, propContainer);
        propInstanceToPrefabMap[prop] = prefab; 
        
        List<Transform> sockets = new List<Transform>();
        foreach (Transform child in prop.GetComponentsInChildren<Transform>(true))
        {
            if (child.CompareTag("LootSocket")) sockets.Add(child);
        }
        propLootSocketsMap[prop] = sockets;
        
        return prop;
    }

    private GameObject GetLootFromPool(GameObject prefab)
    {
        if (!lootPool.ContainsKey(prefab)) lootPool[prefab] = new Queue<GameObject>();

        if (lootPool[prefab].Count > 0) return lootPool[prefab].Dequeue();
        
        GameObject loot = Instantiate(prefab, lootContainer);
        lootInstanceToPrefabMap[loot] = prefab; 
        
        if (NetworkServer.active && loot.TryGetComponent(out NetworkIdentity netId))
        {
            NetworkServer.Spawn(loot);
        }
        
        return loot;
    }

    private void DespawnChunk(int index)
    {
        GameObject chunk = activeChunks[index];
        chunk.SetActive(false);
        chunkPool[chunkInstanceToBiomeIndexMap[chunk]].Enqueue(chunk);

        if (activePropsMap.TryGetValue(index, out List<GameObject> props))
        {
            foreach (GameObject prop in props)
            {
                prop.SetActive(false);
                propPool[propInstanceToPrefabMap[prop]].Enqueue(prop);
            }
            props.Clear();
            listPool.Push(props); 
            activePropsMap.Remove(index);
        }

        if (activeLootMap.TryGetValue(index, out List<GameObject> loots))
        {
            foreach (GameObject lootObj in loots)
            {
                if (lootObj != null && lootObj.transform.parent == lootContainer)
                {
                    lootObj.SetActive(false);
                    lootPool[lootInstanceToPrefabMap[lootObj]].Enqueue(lootObj);
                }
            }
            loots.Clear();
            listPool.Push(loots);
            activeLootMap.Remove(index);
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

        GameObject newChunk = Instantiate(biomes[biomeIndex].chunkPrefab, worldContainer);
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

    private int DetermineBiomeIndex(float noise, int chunkIndex)
    {
        int count = biomes.Length;
        if (count == 0) return 0;

        if (Mathf.Abs(chunkIndex) <= viewDistanceAhead) return highestWeightBiomeIndex;

        if (NetworkClient.active || NetworkServer.active)
        {
            float currentMinutes = (float)NetworkTime.time / 60f;
            if (currentMinutes >= eventIntervalMinutes && currentMinutes % eventIntervalMinutes < eventDurationMinutes)
            {
                return Mathf.Clamp(eventBiomeIndex, 0, count - 1);
            }
        }

        int totalWeight = 0;
        for (int i = 0; i < count; i++) totalWeight += biomes[i].spawnWeight;
        if (totalWeight <= 0) return 0;

        float targetWeight = noise * totalWeight;
        int currentWeight = 0;

        for (int i = 0; i < count; i++)
        {
            currentWeight += biomes[i].spawnWeight;
            if (targetWeight <= currentWeight) return i;
        }

        return 0; 
    }

    private List<GameObject> GetListFromPool() => listPool.Count > 0 ? listPool.Pop() : new List<GameObject>(propsPerChunk);

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (worldContainer == null || !Application.isPlaying) return;

        foreach (var kvp in activeChunks)
        {
            Vector3 center = worldContainer.TransformPoint(new Vector3(0, 0, kvp.Key * chunkSize));
            Gizmos.color = new Color(0, 1, 0, 0.05f);
            Gizmos.DrawCube(center, new Vector3(propSpawnArea.x, 0.1f, propSpawnArea.y));
            Gizmos.color = new Color(1, 0, 0, 0.2f);
            Gizmos.DrawCube(center, new Vector3(roadWidth, 0.2f, propSpawnArea.y));
        }
    }
#endif
}