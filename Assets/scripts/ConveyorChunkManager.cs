using UnityEngine;
using System.Collections.Generic;

public enum BiomeType { Forest, Desert, Town }

/// <summary>
/// Оптимизированный менеджер чанков с пулом объектов на основе словаря.
/// </summary>
public class ConveyorChunkManager : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Контейнер, который двигает CarHybridSystem")]
    [SerializeField] private Transform worldContainer;
    [SerializeField] private Transform carTransform;

    [Header("Chunk Prefabs (Кириллу на заметку: оптимизировать меши!)")]
    [SerializeField] private GameObject forestChunkPrefab;
    [SerializeField] private GameObject desertChunkPrefab;
    [SerializeField] private GameObject townChunkPrefab;

    [Header("Settings")]
    [SerializeField] private int seed = 1337; // Одинаковый для всех клиентов!
    [SerializeField] private float chunkSize = 50f;
    [SerializeField] private int chunksVisibleAhead = 5;
    [SerializeField] private float chunkDespawnZ = -50f; // Где удалять чанк позади машины

    // Пул объектов: Тип Биома -> Очередь неактивных чанков
    private Dictionary<BiomeType, Queue<GameObject>> chunkPool = new Dictionary<BiomeType, Queue<GameObject>>();
    
    // Активные чанки на сцене
    private List<GameObject> activeChunks = new List<GameObject>();
    
    private float spawnZ = 0f;

    private void Start()
    {
        if (worldContainer == null) Debug.LogError("ОШИБКА: Не назначен World Container!");
        
        InitializePool();
        
        // Спавним стартовые чанки
        for (int i = 0; i < chunksVisibleAhead; i++)
        {
            SpawnNextChunk();
        }
    }

    private void Update()
    {
        if (activeChunks.Count == 0) return;

        // Проверяем самый старый (первый) чанк в списке. Если он уехал далеко назад — в пул его.
        GameObject oldestChunk = activeChunks[0];
        
        // Вычисляем позицию чанка относительно машины
        float relativeZ = oldestChunk.transform.position.z - carTransform.position.z;

        if (relativeZ < chunkDespawnZ)
        {
            DespawnChunk(oldestChunk);
            SpawnNextChunk();
        }
    }

    private void InitializePool()
    {
        chunkPool.Add(BiomeType.Forest, new Queue<GameObject>());
        chunkPool.Add(BiomeType.Desert, new Queue<GameObject>());
        chunkPool.Add(BiomeType.Town, new Queue<GameObject>());
    }

    /// <summary>
    /// Вытаскивает чанк из пула или инстанцирует новый, если пул пуст.
    /// </summary>
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

        // ВАЖНО: Чанки спавнятся ВНУТРИ WorldContainer, чтобы двигаться вместе с ним
        GameObject newChunk = Instantiate(prefabToSpawn, worldContainer);
        newChunk.name = $"{type}_Chunk";
        return newChunk;
    }

    private void SpawnNextChunk()
    {
        // Используем PerlinNoise с нашим Seed для детерминированного выбора биома
        // Делим spawnZ на 200f для плавности переходов (масштаб шума)
        float noiseValue = Mathf.PerlinNoise(seed + (spawnZ / 200f), seed);
        
        BiomeType nextBiome;
        if (noiseValue < 0.33f) nextBiome = BiomeType.Desert;
        else if (noiseValue < 0.66f) nextBiome = BiomeType.Forest;
        else nextBiome = BiomeType.Town;

        GameObject chunk = GetChunkFromPool(nextBiome);
        
        // Ставим чанк. Позиция локальная относительно WorldContainer!
        chunk.transform.localPosition = new Vector3(0, 0, spawnZ);
        activeChunks.Add(chunk);

        // Распределяем пропсы (дома, деревья) внутри чанка по сиду
        DistributeProps(chunk, nextBiome, spawnZ);

        Debug.Log($"[ChunkManager] Заспавнен {nextBiome} на Z:{spawnZ}. Значение шума: {noiseValue:F2}");

        spawnZ += chunkSize;
    }

    private void DespawnChunk(GameObject chunk)
    {
        chunk.SetActive(false);
        activeChunks.RemoveAt(0);
        
        // Определяем тип биома по имени (грубо, но работает для KISS)
        if (chunk.name.Contains("Forest")) chunkPool[BiomeType.Forest].Enqueue(chunk);
        else if (chunk.name.Contains("Desert")) chunkPool[BiomeType.Desert].Enqueue(chunk);
        else if (chunk.name.Contains("Town")) chunkPool[BiomeType.Town].Enqueue(chunk);
    }

    /// <summary>
    /// Простая сид-базированная расстановка объектов внутри чанка.
    /// Гарантирует, что у всех игроков дома и елки будут стоять в одних и тех же координатах.
    /// </summary>
    private void DistributeProps(GameObject chunk, BiomeType type, float zOffset)
    {
        // Инициализируем рандом с жестким сидом для этого конкретного куска карты
        Random.InitState(seed + (int)zOffset);

        // Пример: отключаем/включаем случайные дочерние объекты-пропсы
        // Предполагается, что Кирилл заранее расставит в префабе 10 деревьев, а мы включим только часть из них
        int childCount = chunk.transform.childCount;
        for (int i = 0; i < childCount; i++)
        {
            Transform prop = chunk.transform.GetChild(i);
            
            // Если это не основание дороги (предположим, дорога имеет тег "Road")
            if (!prop.CompareTag("Road"))
            {
                // 50% шанс, что пропс появится
                bool isVisible = Random.value > 0.5f; 
                prop.gameObject.SetActive(isVisible);
            }
        }
    }
}