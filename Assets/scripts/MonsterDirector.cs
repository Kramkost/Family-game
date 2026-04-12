using UnityEngine;
using Mirror;
using Kotenkoff;

public class MonsterDirector : NetworkBehaviour
{
    [Header("Настройки спавна")]
    [Tooltip("Префаб монстра (ОБЯЗАТЕЛЬНО добавь его в Registered Spawnable Prefabs в NetworkManager)")]
    [SerializeField] private GameObject monsterPrefab;
    
    [Tooltip("Минимальное время между появлениями (сек)")]
    [SerializeField] private float minSpawnInterval = 60f;
    [Tooltip("Максимальное время между появлениями (сек)")]
    [SerializeField] private float maxSpawnInterval = 180f;

    [Header("Temporary Overrides")]
    [Tooltip("Если включено, будет спавниться перед случайным игроком, а не на дороге")]
    [SerializeField] private bool spawnNearPlayer = false;
    [Tooltip("Расстояние перед игроком")]
    [SerializeField] private float spawnDistance = 20f;

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
        if (monsterPrefab == null)
        {
            ScheduleNextSpawn();
            return;
        }

        if (spawnNearPlayer)
        {
            PlayerEntity[] players = FindObjectsByType<PlayerEntity>(FindObjectsSortMode.None);
            if (players.Length > 0)
            {
                PlayerEntity target = players[Random.Range(0, players.Length)];
                Vector3 spawnPos = target.transform.position + target.transform.forward * spawnDistance;
                
                if (Physics.Raycast(spawnPos + Vector3.up * 10f, Vector3.down, out RaycastHit hit, 20f))
                {
                    spawnPos.y = hit.point.y;
                }

                Quaternion spawnRot = Quaternion.Euler(0, target.transform.eulerAngles.y - 180f, 0); // Лицом к игроку

                GameObject monster = Instantiate(monsterPrefab, spawnPos, spawnRot);
                NetworkServer.Spawn(monster);
                Destroy(monster, 120f);
            }
        }
        else
        {
            // Original road logic
            // Находим все участки дороги на сцене
            GameObject[] roads = GameObject.FindGameObjectsWithTag("Road");
            
            if (roads.Length > 0)
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
        }

        ScheduleNextSpawn();
    }
}