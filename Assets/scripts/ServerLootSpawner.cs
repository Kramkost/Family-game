using UnityEngine;
using Mirror;
using System.Collections.Generic;

/// <summary>
/// Спавнер сетевого лута с расширенной функциональностью. Вешается на префабы зданий.
/// Работает ТОЛЬКО на сервере. Автоматически убирает за собой лут при деспавне здания.
/// </summary>
public class ServerLootSpawner : NetworkBehaviour
{
    [Header("Настройки лута")]
    [Tooltip("Точки внутри дома, где могут лежать предметы")]
    [SerializeField] private Transform[] lootSockets;
    
    [System.Serializable]
    public class LootItem
    {
        public GameObject prefab;
        [Range(0f, 1f)] public float spawnWeight; // Весовой коэффициент
        [Range(0f, 1f)] public float spawnChance; // Индивидуальный шанс спавна
    }
    
    [Tooltip("Префабы сетевого лута с настройками веса и шанса")]
    [SerializeField] private LootItem[] lootItems;
    
    [Header("Контейнер для предметов")]
    [Tooltip("Prop Container — пустой объект на сцене, куда будут складываться все созданные предметы.\n" +
             "Если поле пустое, скрипт автоматически найдёт или создаст контейнер с именем 'Prop Container'")]
    [SerializeField] private Transform propContainer;
    
    [Tooltip("Название объекта Prop Container на сцене")]
    [SerializeField] private string propContainerName;
    
    [Header("Ограничения")]
    [Tooltip("Учитывать суммарный вес предметов при спавне?")]
    [SerializeField] private bool useWeightedSelection = true; // Переключатель для учёта веса
    
    [Tooltip("Максимальный общий вес лута на здание (0 = без ограничений)")]
    [SerializeField] private float maxTotalWeight = 0;
    
    [Tooltip("Максимальное количество предметов на здание (0 = без ограничений)")]
    [SerializeField] private int maxItemsPerBuilding = 0;
    
    [Header("Минимальное количество предметов")]
    [Tooltip("Гарантированное минимальное количество предметов, которые должны появиться")]
    [SerializeField] private int minItemsGuaranteed = 1;

    [Tooltip("Если предметов спавнится меньше минимума, принудительно добавить их в случайные точки")]
    [SerializeField] private bool enforceMinItems = true;
    
    // Храним ссылки на заспавненный лут, чтобы удалить его, когда дом скроется
    private List<GameObject> spawnedItems = new List<GameObject>();
    
    // Кэш для оптимизации
    private float totalSpawnWeight;
    private bool isInitialized;

    /// <summary>
    /// Вызывается при старте компонента. Инициализирует необходимые данные для работы.
    /// </summary>
    private void Start()
    {
        Initialize();
    }

    /// <summary>
    /// Инициализирует компонент: проверяет данные, рассчитывает суммарный вес лута (если нужно).
    /// Предотвращает повторную инициализацию через флаг isInitialized.
    /// Логирует предупреждения при отсутствии точек спавна или префабов лута.
    /// </summary>
    private void Initialize()
    {
        if (isInitialized) return;
        
        // Валидация данных
        if (lootSockets == null || lootSockets.Length == 0)
        {
            Debug.LogWarning($"[ServerLootSpawner] Нет точек спавна для лута в объекте {gameObject.name}");
            return;
        }
        
        if (lootItems == null || lootItems.Length == 0)
        {
            Debug.LogWarning($"[ServerLootSpawner] Нет префабов лута для объекта {gameObject.name}");
            return;
        }

        // Расчёт общего веса для весового распределения (только если включён переключатель)
        totalSpawnWeight = 0f;
        if (useWeightedSelection)
        {
            foreach (var item in lootItems)
            {
                if (item.prefab != null && item.spawnWeight > 0f)
                {
                    totalSpawnWeight += item.spawnWeight;
                }
            }
        }

        isInitialized = true;
        
        // Автоматически находим или создаём Prop Container, если не назначен в инспекторе
        ManagePropContainer();
    }

    /// <summary>
    /// Вызывается каждый раз, когда объект активируется (например, извлекается из пула).
    /// Проверяет выполнение на сервере, инициализирует компонент (если нужно) и запускает спавн лута.
    /// </summary>
    private void OnEnable()
    {
        // Только сервер имеет право создавать интерактивные предметы!
        if (!NetworkServer.active) return;

        if (!isInitialized)
            Initialize();
            
        SpawnLoot();
    }

    /// <summary>
    /// Вызывается, когда объект деактивируется (например, возвращается в пул).
    /// Проверяет выполнение на сервере и очищает созданный лут.
    /// </summary>
    private void OnDisable()
    {
        if (!NetworkServer.active) return;

        ClearLoot();
    }

    /// <summary>
    /// Основной метод спавна лута: выбирает случайным образом точки спавна, выбирает предметы, создаёт их и регистрирует в сети.
    /// Учитывает ограничения по количеству предметов и суммарному весу.
    /// Использует весовое распределение или случайный выбор в зависимости от useWeightedSelection.
    /// Применяет индивидуальный шанс спавна для каждого предмета.
    /// </summary>
    private void SpawnLoot()
{
    if (!isInitialized || lootSockets.Length == 0 || lootItems.Length == 0) return;

    int spawnedCount = 0;
    float currentTotalWeight = 0f;

    // Создаём список доступных точек, которые ещё не были использованы
    List<Transform> availableSockets = new List<Transform>(lootSockets);

    // Цикл продолжается, пока есть доступные точки и не достигнуты ограничения
    while (availableSockets.Count > 0 &&
           (maxItemsPerBuilding == 0 || spawnedCount < maxItemsPerBuilding) &&
           (maxTotalWeight == 0 || currentTotalWeight < maxTotalWeight))
    {
        // Случайным образом выбираем индекс точки из оставшихся
        int randomIndex = Random.Range(0, availableSockets.Count);
        Transform socket = availableSockets[randomIndex];

        // Удаляем выбранную точку из списка доступных, чтобы она не повторилась
        availableSockets.RemoveAt(randomIndex);

        GameObject prefabToSpawn;
        float itemWeight = 1f; // По умолчанию вес 1, если не используем весовое распределение

        // Используем весовое распределение, только если переключатель включён
        if (useWeightedSelection && totalSpawnWeight > 0f)
        {
            prefabToSpawn = SelectWeightedLootItem(out itemWeight);
        }
        else
        {
            // Случайный выбор без учёта весов
            prefabToSpawn = GetRandomLootItem();
        }

        if (prefabToSpawn == null) continue;

        // Используем индивидуальный шанс спавна
        LootItem selectedItem = GetLootItemByPrefab(prefabToSpawn);
        if (selectedItem == null) continue;

        if (Random.value > selectedItem.spawnChance)
            continue;
        
        // Создаём объект в выбранной случайной точке
        GameObject lootInstance = Instantiate(prefabToSpawn, socket.position, socket.rotation);
        Debug.Log("[ServerLootSpawner] Объект создан с шансом "+ selectedItem.spawnChance * 100 + "%: " + lootInstance.name);

        // Устанавливаем родителя — Prop Container (гарантированно существует после Initialize)
        lootInstance.transform.SetParent(propContainer);

        // СПАВНИМ В СЕТЬ (чтобы все клиенты его увидели)
        NetworkServer.Spawn(lootInstance);
        spawnedItems.Add(lootInstance);

        spawnedCount++;
        currentTotalWeight += itemWeight;
    }

    // Принудительное добавление предметов, если их меньше минимального количества
    if (enforceMinItems && spawnedCount < minItemsGuaranteed)
    {
        Debug.LogWarning($"[ServerLootSpawner] Принудительно добавляем {minItemsGuaranteed - spawnedCount} предметов для достижения минимума");

        // Берём все точки спавна (включая уже использованные)
        List<Transform> allSockets = new List<Transform>(lootSockets);

        for (int i = spawnedCount; i < minItemsGuaranteed; i++)
        {
            if (allSockets.Count == 0) break;

            // Выбираем случайную точку
            int randomSocketIndex = Random.Range(0, allSockets.Count);
            Transform socket = allSockets[randomSocketIndex];
            allSockets.RemoveAt(randomSocketIndex);

            // Гарантированно спавним предмет без учёта шанса спавна
            GameObject prefabToSpawn;
            float itemWeight = 1f;

            if (useWeightedSelection && totalSpawnWeight > 0f)
            {
                prefabToSpawn = SelectWeightedLootItem(out itemWeight);
            }
            else
            {
                prefabToSpawn = GetRandomLootItem();
            }

            if (prefabToSpawn == null) continue;

            // Принудительно создаём объект в выбранной случайной точке
            GameObject lootInstance = Instantiate(prefabToSpawn, socket.position, socket.rotation);
            Debug.Log("[ServerLootSpawner] Принудительно создан объект: " + lootInstance.name);
            
            // Устанавливаем родителя — Prop Container
            lootInstance.transform.SetParent(propContainer);

            // СПАВНИМ В СЕТЬ
            NetworkServer.Spawn(lootInstance);
            spawnedItems.Add(lootInstance);

            spawnedCount++;
            currentTotalWeight += itemWeight;
        }
    }
}

    /// <summary>
    /// Выбирает предмет с учётом весовых коэффициентов.
    /// Генерирует случайное число в диапазоне [0, totalSpawnWeight], затем последовательно суммирует веса,
    /// пока не достигнет или не превысит случайное число. Возвращает префаб выбранного предмета и его вес.
    /// </summary>
    /// <param name="selectedWeight">Выходной параметр: вес выбранного предмета</param>
    /// <returns>Префаб выбранного предмета или null, если выбор невозможен</returns>
    private GameObject SelectWeightedLootItem(out float selectedWeight)
    {
        selectedWeight = 0f;
        if (totalSpawnWeight <= 0f) return null;

        float randomPoint = Random.Range(0f, totalSpawnWeight);
        float currentPoint = 0f;

        foreach (var item in lootItems)
        {
            if (item.prefab == null || item.spawnWeight <= 0f) continue;
            
            currentPoint += item.spawnWeight;
            if (randomPoint <= currentPoint)
            {
                selectedWeight = item.spawnWeight;
                return item.prefab;
            }
        }

        return null;
    }

    /// <summary>
    /// Случайный выбор предмета без учёта весов (используется, когда useWeightedSelection = false).
    /// Формирует список валидных префабов и возвращает случайный элемент из него.
    /// </summary>
    /// <returns>Случайный префаб предмета или null, если список пуст</returns>
    private GameObject GetRandomLootItem()
    {
        List<GameObject> validPrefabs = new List<GameObject>();
        foreach (var item in lootItems)
        {
            if (item.prefab != null)
            {
                validPrefabs.Add(item.prefab);
            }
        }

        if (validPrefabs.Count == 0)
            return null;

        return validPrefabs[Random.Range(0, validPrefabs.Count)];
    }

    /// <summary>
    /// Находит описание предмета (LootItem) по его префабу.
    /// Используется для получения настроек предмета (шанс спавна, вес) после его выбора.
    /// </summary>
    /// <param name="prefab">Префаб предмета, для которого нужно найти описание</param>
    /// <returns>Объект LootItem с настройками предмета или null, если предмет не найден</returns>
    private LootItem GetLootItemByPrefab(GameObject prefab)
    {
        foreach (var item in lootItems)
        {
            if (item.prefab == prefab)
                return item;
        }
        return null;
    }

    /// <summary>
    /// Очищает весь созданный лут при деактивации здания или удалении объекта.
    /// Проходит по списку spawnedItems в обратном порядке для безопасного удаления элементов.
    /// Удаляет объекты из сети через NetworkServer.Destroy(), что синхронизирует удаление со всеми клиентами.
    /// Обрабатывает возможные ошибки при удалении и очищает список ссылок на объекты.
    /// </summary>
    private void ClearLoot()
    {
        // Проходим в обратном порядке, чтобы безопасно удалять элементы из списка
        for (int i = spawnedItems.Count - 1; i >= 0; i--)
        {
            GameObject item = spawnedItems[i];
            
            // Обработка ошибок при удалении (улучшение №8)
            if (item != null && NetworkServer.active)
            {
                try
                {
                    NetworkServer.Destroy(item);
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[ServerLootSpawner] Ошибка при удалении лута: {e.Message}");
                }
            }
            else if (item == null)
            {
                // Удаляем null-ссылки из списка, если объект был удалён другим скриптом
                spawnedItems.RemoveAt(i);
            }
        }
        // Полностью очищаем список после обработки всех элементов
        spawnedItems.Clear();
    }
    
    /// <summary>
    /// Находит или создаёт Prop Container для группировки созданных предметов.
    /// Приоритет:
    /// 1. Если propContainer уже назначен в инспекторе — используем его.
    /// 2. Иначе ищем существующий объект с именем "Prop Container" на сцене.
    /// 3. Если не найден — создаём новый пустой GameObject.
    /// </summary>
    private void ManagePropContainer()
    {
        // Если контейнер уже назначен в инспекторе, используем его и выходим
        if (propContainer != null)
        {
            Debug.Log($"[ServerLootSpawner] Используется Prop Container из инспектора: {propContainer.name}");
            propContainerName  = propContainer.name;
            return;
        }
    
        // Пытаемся найти существующий контейнер на сцене
        propContainer = GameObject.Find(propContainerName)?.transform;
    
        if (propContainer != null)
        {
            Debug.Log("[ServerLootSpawner] Найден существующий Prop Container на сцене");
            return;
        }
    
        // Если не найден — создаём новый
        GameObject containerObject = new GameObject(propContainerName);
        propContainer = containerObject.transform;
    
        Debug.Log("[ServerLootSpawner] Создан новый Prop Container на сцене");
    }

#if UNITY_EDITOR
    /// <summary>
    /// Визуализирует точки спавна лута в редакторе Unity (Scene View).
    /// Метод вызывается автоматически редактором каждый кадр — только в режиме редактора.
    /// Рисует жёлтые каркасные кубы в позициях всех lootSockets для наглядной проверки их расположения.
    /// Позволяет быстро проверить, все ли точки спавна назначены корректно и не находятся ли они в неподходящих местах.
    /// Код компилируется только в редакторе (благодаря UNITY_EDITOR) и не попадает в финальную сборку игры.
    /// </summary>
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        if (lootSockets != null)
        {
            foreach (Transform socket in lootSockets)
            {
                if (socket != null)
                {
                    // Рисуем каркас куба размером 0.2×0.2×0.2 единицы в позиции точки спавна
                    Gizmos.DrawWireCube(socket.position, Vector3.one * 0.2f);
                }
            }
        }
    }
#endif
}