using UnityEngine;
using Mirror;
using System.Collections.Generic;

/// <summary>
/// Спавнер сетевого лута. Вешается на префабы зданий (Дома, Заправки).
/// Работает ТОЛЬКО на сервере. Автоматически убирает за собой лут при деспавне здания.
/// </summary>
public class ServerLootSpawner : NetworkBehaviour
{
    [Header("Настройки лута")]
    [Tooltip("Точки внутри дома, где могут лежать предметы")]
    [SerializeField] private Transform[] lootSockets;
    
    [Tooltip("Префабы сетевого лута (Канистры, Аптечки и т.д.)")]
    [SerializeField] private GameObject[] lootPrefabs;

    [Tooltip("Шанс появления предмета в каждом сокете (0.0 - 1.0)")]
    [SerializeField] [Range(0f, 1f)] private float spawnChancePerSocket = 0.5f;

    // Храним ссылки на заспавненный лут, чтобы удалить его, когда дом скроется
    private List<GameObject> spawnedItems = new List<GameObject>();

    /// <summary>
    /// OnEnable вызывается каждый раз, когда Конвейер достает дом из пула и включает его.
    /// </summary>
    private void OnEnable()
    {
        // Только сервер имеет право создавать интерактивные предметы!
        if (!NetworkServer.active) return;

        SpawnLoot();
    }

    /// <summary>
    /// OnDisable вызывается, когда Конвейер прячет дом обратно в пул.
    /// </summary>
    private void OnDisable()
    {
        if (!NetworkServer.active) return;

        ClearLoot();
    }

    private void SpawnLoot()
    {
        if (lootSockets.Length == 0 || lootPrefabs.Length == 0) return;

        foreach (Transform socket in lootSockets)
        {
            // Бросаем кубик на шанс спавна
            if (Random.value <= spawnChancePerSocket)
            {
                // Выбираем случайный префаб лута
                GameObject prefabToSpawn = lootPrefabs[Random.Range(0, lootPrefabs.Length)];
                
                // Создаем объект
                GameObject lootInstance = Instantiate(prefabToSpawn, socket.position, socket.rotation);
                
                // СПАВНИМ В СЕТЬ (чтобы все клиенты его увидели)
                NetworkServer.Spawn(lootInstance);
                
                spawnedItems.Add(lootInstance);
            }
        }
    }

    private void ClearLoot()
    {
        foreach (GameObject item in spawnedItems)
        {
            if (item != null)
            {
                
                NetworkServer.Destroy(item);
            }
        }
        spawnedItems.Clear();
    }
}