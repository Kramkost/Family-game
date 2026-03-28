using UnityEngine;
using Mirror;

public class MonsterDirector : NetworkBehaviour
{
    [Header("Настройки спавна")]
    [Tooltip("Префаб монстра (ОБЯЗАТЕЛЬНО добавь его в Registered Spawnable Prefabs в NetworkManager)")]
    [SerializeField] private GameObject monsterPrefab;
    
    [Tooltip("Минимальное время между появлениями (сек)")]
    [SerializeField] private float minSpawnInterval = 60f;
    [Tooltip("Максимальное время между появлениями (сек)")]
    [SerializeField] private float maxSpawnInterval = 180f;

    public override void OnStartServer()
    {
        // Запускаем таймер первого появления
        ScheduleNextSpawn();
    }

    [Server]
    private void ScheduleNextSpawn()
    {
        float waitTime = Random.Range(minSpawnInterval, maxSpawnInterval);
        Invoke(nameof(TrySpawnMonster), waitTime);
    }

    [Server]
    private void TrySpawnMonster()
    {
        // Находим все участки дороги на сцене
        GameObject[] roads = GameObject.FindGameObjectsWithTag("Road");
        
        if (roads.Length > 0 && monsterPrefab != null)
        {
            // Берем случайный кусок дороги
            GameObject randomRoad = roads[Random.Range(0, roads.Length)];
            
            // Ставим монстра чуть выше дороги, чтобы он не провалился в текстуры
            Vector3 spawnPos = randomRoad.transform.position + Vector3.up * 1.5f; 
            
            // Разворачиваем его лицом к машине (допустим, машина всегда едет по оси Z)
            Quaternion spawnRot = Quaternion.LookRotation(Vector3.back);

            GameObject monster = Instantiate(monsterPrefab, spawnPos, spawnRot);
            NetworkServer.Spawn(monster);
            
            // Авто-удаление, если игроки свернули или не доехали до него за 2 минуты
            Destroy(monster, 120f); 
        }

        ScheduleNextSpawn();
    }
}