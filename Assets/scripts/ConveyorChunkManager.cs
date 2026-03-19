using UnityEngine;
using System.Collections.Generic;

public enum BiomeType { Forest, Desert, Town }

/// <summary>
/// Персистентный менеджер чанков. 
/// Поддерживает движение вперед/назад и сохраняет одинаковую генерацию по сиду.
/// </summary>
public class ConveyorChunkManager : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Контейнер, который двигает CarHybridSystem")]
    [SerializeField] private Transform worldContainer;
    [SerializeField] private Transform carTransform;

    [Header("Chunk Prefabs")]
    [SerializeField] private GameObject forestChunkPrefab;
    [SerializeField] private GameObject desertChunkPrefab;
    [SerializeField] private GameObject townChunkPrefab;

    [Header("Settings & Persistence")]
    [SerializeField] private int globalSeed = 1337; // Одинаковый для всех клиентов!
    [SerializeField] private float chunkSize = 50f;
    
    [Tooltip("Сколько чанков прорисовывать спереди")]
    [SerializeField] private int viewDistanceAhead = 5;
    
    [Tooltip("Сколько чанков оставлять позади (для движения задним ходом)")]
    [SerializeField] private int viewDistanceBehind = 2;

    // Пул объектов: Тип Биома -> Очередь неактивных чанков
    private Dictionary<BiomeType, Queue<GameObject>> chunkPool = new Dictionary<BiomeType, Queue<GameObject>>();
    
    // Активные чанки на сцене. Ключ — это абсолютный индекс чанка.
    private Dictionary<int, GameObject> activeChunks = new Dictionary<int, GameObject>();

    private void Start()
    {
        if (worldContainer == null || carTransform == null) 
        {
            Debug.LogError("ОШИБКА: Не назначены ссылки в ConveyorChunkManager!");
            return;
        }
        
        InitializePool();
    }

    private void Update()
    {
        if (carTransform == null || worldContainer == null) return;

        // Универсальная виртуальная дистанция (работает и для физики, и для RoadMill)
        float virtualDistance = carTransform.position.z - worldContainer.position.z;
        
        // Вычисляем, в каком чанке сейчас находится машина
        int currentChunkIndex = Mathf.FloorToInt(virtualDistance / chunkSize);

        // Определяем "окно видимости"
        int startIndex = currentChunkIndex - viewDistanceBehind;
        int endIndex = currentChunkIndex + viewDistanceAhead;

        ManageChunks(startIndex, endIndex);
    }

    private void InitializePool()
    {
        chunkPool.Add(BiomeType.Forest, new Queue<GameObject>());
        chunkPool.Add(BiomeType.Desert, new Queue<GameObject>());
        chunkPool.Add(BiomeType.Town, new Queue<GameObject>());
    }

    /// <summary>
    /// Управляет включением и выключением чанков на основе скользящего окна.
    /// </summary>
    private void ManageChunks(int startIndex, int endIndex)
    {
        // 1. Деспавним чанки, которые вышли за пределы видимости (слишком далеко сзади или спереди)
        List<int> keysToRemove = new List<int>();
        foreach (var kvp in activeChunks)
        {
            if (kvp.Key < startIndex || kvp.Key > endIndex)
            {
                DespawnChunk(kvp.Value);
                keysToRemove.Add(kvp.Key);
            }
        }

        // Очищаем словарь от удаленных чанков
        foreach (int key in keysToRemove)
        {
            activeChunks.Remove(key);
        }

        // 2. Спавним недостающие чанки внутри окна видимости
        for (int i = startIndex; i <= endIndex; i++)
        {
            if (!activeChunks.ContainsKey(i))
            {
                SpawnChunk(i);
            }
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

        GameObject prefabToSpawn = type switch
        {
            BiomeType.Forest => forestChunkPrefab,
            BiomeType.Desert => desertChunkPrefab,
            BiomeType.Town => townChunkPrefab,
            _ => forestChunkPrefab
        };

        GameObject newChunk = Instantiate(prefabToSpawn, worldContainer);
        newChunk.name = $"{type}_Chunk";
        return newChunk;
    }

    private void SpawnChunk(int index)
    {
        // Определяем биом детерминированно по индексу
        float noiseValue = Mathf.PerlinNoise(globalSeed + (index * 0.1f), globalSeed);
        
        BiomeType nextBiome;
        if (noiseValue < 0.33f) nextBiome = BiomeType.Desert;
        else if (noiseValue < 0.66f) nextBiome = BiomeType.Forest;
        else nextBiome = BiomeType.Town;

        GameObject chunk = GetChunkFromPool(nextBiome);
        
        // Позиция чанка строго привязана к его математическому индексу
        float exactZ = index * chunkSize;
        chunk.transform.localPosition = new Vector3(0, 0, exactZ);
        
        activeChunks.Add(index, chunk);

        // Наполняем чанк объектами
        SpawnProps(chunk, index);
    }

    private void DespawnChunk(GameObject chunk)
    {
        chunk.SetActive(false);
        
        // Определяем тип биома по имени для возврата в правильный пул
        if (chunk.name.Contains("Forest")) chunkPool[BiomeType.Forest].Enqueue(chunk);
        else if (chunk.name.Contains("Desert")) chunkPool[BiomeType.Desert].Enqueue(chunk);
        else if (chunk.name.Contains("Town")) chunkPool[BiomeType.Town].Enqueue(chunk);
    }

    /// <summary>
    /// Рандомизация пропсов (растения, дома).
    /// Гарантирует, что при возвращении назад объекты останутся на своих местах.
    /// </summary>
    private void SpawnProps(GameObject chunk, int index)
    {
        // Инициализируем рандом уникальным ключом (общий сид + индекс куска карты)
        Random.InitState(globalSeed + index);

        int childCount = chunk.transform.childCount;
        for (int i = 0; i < childCount; i++)
        {
            Transform prop = chunk.transform.GetChild(i);
            
            // Тег "Road" защищает основание дороги от случайного удаления
            if (!prop.CompareTag("Road"))
            {
                // Шанс появления объекта 50%
                bool isVisible = Random.value > 0.5f; 
                prop.gameObject.SetActive(isVisible);
                
                // Здесь можно добавить вращение:
                // prop.localRotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
            }
        }
    }
}