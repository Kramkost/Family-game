using Mirror;
using UnityEngine;
using System.Collections.Generic;

public class InteriorGenerator : NetworkBehaviour
{
    [Header("Настройки генерации")]
    public int minTotalItems = 1;
    public int maxTotalItems = 3;

    [Header("Предметы для спавна")]
    [SerializeField] private SpawnableItem[] spawnableItems;
    
    [Header("Контейнер для предметов")]
    [Tooltip("Prop Container — пустой объект на сцене, куда будут складываться все созданные предметы.\n" +
             "Если поле пустое, скрипт автоматически найдёт или создаст контейнер с именем 'Prop Container'")]
    [SerializeField] private Transform propContainer;
    [SerializeField] private string propContainerName = "Prop Container";

    [Header("Отладка")]
    public bool debugMode = true;

    [System.Serializable]
    public class SpawnableItem
    {
        public GameObject prefab;
        [Range(0, 100)] public float spawnChance = 50f;
        public int minCount = 1;
        public int maxCount = 3;
        public Transform[] spawnPoints; // Индивидуальные точки спавна для этого предмета
    }

    private Dictionary<string, int> spawnedItemCounts = new Dictionary<string, int>();
    private List<GameObject> spawnedObjects = new List<GameObject>();

    /// <summary>
    /// Вызывается при активации объекта (когда объект становится активным в сцене).
    /// Запускает генерацию интерьера, если скрипт выполняется на сервере.
    /// Это позволяет генерировать интерьер при появлении дома в игре или при его активации.
    /// </summary>
    private void OnEnable()
    {
       
        // Только сервер имеет право создавать интерактивные предметы!
        if (!NetworkServer.active) return;

        if (debugMode) Debug.Log($"[InteriorGenerator] Запуск генерации для дома {gameObject.name} при активации объекта");
        
        // Предварительная проверка всех данных перед генерацией
        if (!ValidateSpawnData())
        {
            if (debugMode) Debug.LogError("[InteriorGenerator] Генерация прервана из‑за некорректных данных спавна");
            return;
        }

        ManagePropContainer();
        GenerateInterior();
    }

    /// <summary>
    /// Основной метод генерации интерьера. Создаёт предметы в случайных точках спавна
    /// согласно настройкам каждого предмета. Содержит защиту от бесконечного цикла.
    /// </summary>
    [Server]
    private void GenerateInterior()
    {
        spawnedItemCounts.Clear();

        int totalItemsSpawned = 0;
        int maxAttempts = 100; // Защита от бесконечного цикла

        while (totalItemsSpawned < minTotalItems && maxAttempts > 0)
        {
            maxAttempts--;

            // Выбираем случайный предмет из списка
            SpawnableItem randomItem = GetRandomSpawnableItem();
            if (randomItem == null) continue;

            // Проверяем, можем ли мы заспавнить этот предмет
            if (!CanSpawnItem(randomItem, totalItemsSpawned)) continue;

            // Получаем случайную точку спавна ИЗ точек этого предмета
            Transform spawnPoint = GetRandomPointForItem(randomItem);
            if (spawnPoint == null) continue;

            // Спавним предмет
            GameObject spawnedObject = SpawnItemAtPoint(randomItem, spawnPoint);
            if (spawnedObject != null)
            {
                totalItemsSpawned++;
                UpdateItemCount(randomItem.prefab.name);
                spawnedObjects.Add(spawnedObject);
            }
        }

        if (debugMode)
        {
            Debug.Log($"[InteriorGenerator] Сгенерировано {totalItemsSpawned} предметов в доме {gameObject.name}");
            PrintSpawnedItems();
        }
    }

    /// <summary>
    /// Получает случайный предмет из массива spawnableItems.
    /// Если массив пуст или не инициализирован, возвращает null.
    /// </summary>
    /// <returns>Случайный элемент из массива spawnableItems или null</returns>
    [Server]
    private SpawnableItem GetRandomSpawnableItem()
    {
        if (spawnableItems == null || spawnableItems.Length == 0) return null;
        return spawnableItems[Random.Range(0, spawnableItems.Length)];
    }

    /// <summary>
    /// Получает случайную точку спавна для указанного предмета из его собственного массива точек.
    /// Проверяет наличие точек спавна у предмета. Если точек нет, выводит предупреждение в консоль.
    /// </summary>
    /// <param name="item">Предмет, для которого нужно найти точку спавна</param>
    /// <returns>Случайная точка спавна из массива spawnPoints предмета или null, если точек нет</returns>
    [Server]
    private Transform GetRandomPointForItem(SpawnableItem item)
    {
        // Проверяем, есть ли точки спавна у этого предмета
        if (item.spawnPoints == null || item.spawnPoints.Length == 0)
        {
            if (debugMode) Debug.LogWarning($"[InteriorGenerator] У предмета {item.prefab.name} нет точек спавна!");
            return null;
        }

        // Возвращаем случайную точку из списка этого предмета
        return item.spawnPoints[Random.Range(0, item.spawnPoints.Length)];
    }
    
    /// <summary>
    /// Проверяет все данные для спавна на корректность перед началом генерации.
    /// Проверяет: наличие массива предметов, валидность префабов, наличие точек спавна, корректность шансов.
    /// </summary>
    /// <returns>true, если все данные валидны; false, если есть ошибки</returns>
    [Server]
private bool ValidateSpawnData()
{
    if (spawnableItems == null)
    {
        if (debugMode) Debug.LogError("[InteriorGenerator] Массив spawnableItems не инициализирован!");
        return false;
    }

    bool isValid = true;

    foreach (var item in spawnableItems)
    {
        // Проверка префаба
        if (item.prefab == null)
        {
            if (debugMode) Debug.LogError($"[InteriorGenerator] У одного из предметов отсутствует префаб!");
            isValid = false;
        }

        // Проверка точек спавна
        if (item.spawnPoints == null || item.spawnPoints.Length == 0)
        {
            if (debugMode) Debug.LogError($"[InteriorGenerator] У предмета {item.prefab?.name ?? "Unknown"} отсутствуют точки спавна!");
            isValid = false;
        }
        else
        {
            // Проверка валидности каждой точки спавна
            foreach (var point in item.spawnPoints)
            {
                if (point == null)
                {
                    if (debugMode) Debug.LogError($"[InteriorGenerator] Обнаружена невалидная точка спавна у предмета {item.prefab?.name ?? "Unknown"}!");
                    isValid = false;
                }
            }
        }

        // Проверка шанса спавна
        if (item.spawnChance < 0f || item.spawnChance > 100f)
        {
            if (debugMode) Debug.LogError($"[InteriorGenerator] Некорректный шанс спавна ({item.spawnChance}%) у предмета {item.prefab?.name ?? "Unknown"}!");
            isValid = false;
        }

        // Проверка лимитов количества
        if (item.minCount < 0 || item.maxCount < item.minCount)
        {
            if (debugMode) Debug.LogError($"[InteriorGenerator] Некорректные лимиты количества у предмета {item.prefab?.name ?? "Unknown"} (min: {item.minCount}, max: {item.maxCount})!");
            isValid = false;
        }
    }

    return isValid;
}

    /// <summary>
    /// Проверяет, можно ли заспавнить указанный предмет в текущей ситуации.
    /// Учитывает: общее количество уже созданных предметов, лимиты для конкретного предмета и шанс спавна.
    /// </summary>
    /// <param name="item">Проверяемый предмет</param>
    /// <param name="totalSpawned">Общее количество уже созданных предметов</param>
    /// <returns>true, если предмет можно заспавнить; false в противном случае</returns>
    [Server]
    private bool CanSpawnItem(SpawnableItem item, int totalSpawned)
    {
        // Проверяем общее количество предметов
        if (totalSpawned >= maxTotalItems) return false;

        // Проверяем минимальное и максимальное количество для этого предмета
        int currentCount = GetItemCount(item.prefab.name);
        if (currentCount >= item.maxCount) return false;

        // Проверяем шанс спавна
        if (Random.Range(0f, 100f) > item.spawnChance) return false;
        
        // Дополнительная проверка на наличие префаба (дублирующая, но для надёжности)
        if (item.prefab == null)
        {
            if (debugMode) Debug.LogWarning($"[InteriorGenerator] Попытка спавна предмета без префаба: {item.prefab?.name ?? "Unknown"}");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Создаёт экземпляр префаба предмета в указанной точке спавна.
    /// Позиция и поворот берутся из Transform точки спавна. Объект регистрируется на сервере через NetworkServer.Spawn().
    /// </summary>
    /// <param name="item">Предмет для спавна</param>
    /// <param name="point">Точка спавна (Transform), где будет создан предмет</param>
    /// <returns>Созданный GameObject или null при ошибке</returns>
    [Server]
    private GameObject SpawnItemAtPoint(SpawnableItem item, Transform point)
    {
        if (item.prefab == null) return null;

        Vector3 spawnPosition = point.position;
        Quaternion spawnRotation = point.rotation;

        GameObject spawnedObject = Instantiate(item.prefab, spawnPosition, spawnRotation);
        
        // Устанавливаем контейнер как родителя для созданного объекта
        if (propContainer != null)
        {
            spawnedObject.transform.SetParent(propContainer);
        }
        else
        {
            if (debugMode) Debug.LogWarning($"[InteriorGenerator] Prop Container не найден! Объект {item.prefab.name} будет создан без родителя.");
        }
        
        NetworkServer.Spawn(spawnedObject);

        if (debugMode)
        {
            Debug.Log($"[InteriorGenerator] Заспавнен {item.prefab.name} в точке {point.name}");
        }

        return spawnedObject;
    }

    /// <summary>
    /// Обновляет счётчик количества созданных экземпляров указанного предмета.
    /// Если предмет уже есть в словаре, увеличивает счётчик на 1.
    /// Если предмета нет в словаре, добавляет его со значением 1.
    /// </summary>
    /// <param name="itemName">Имя предмета (обычно имя префаба)</param>
    [Server]
    private void UpdateItemCount(string itemName)
    {
        if (spawnedItemCounts.ContainsKey(itemName))
        {
            spawnedItemCounts[itemName]++;
        }
        else
        {
            spawnedItemCounts[itemName] = 1;
        }
    }

    /// <summary>
    /// Возвращает текущее количество созданных экземпляров указанного предмета.
    /// Если предмет не найден в словаре, возвращает 0.
    /// </summary>
    /// <param name="itemName">Имя предмета для проверки</param>
    /// <returns>Количество созданных экземпляров предмета</returns>
    private int GetItemCount(string itemName)
    {
        return spawnedItemCounts.TryGetValue(itemName, out int count) ? count : 0;
    }

    /// <summary>
    /// Удаляет все ранее созданные предметы интерьера.
    /// Перебирает список spawnedObjects, уничтожает каждый объект через NetworkServer.Destroy()
    /// и очищает список. Используется перед новой генерацией для предотвращения дублирования объектов.
    /// </summary>
    [Server]
    private void ClearPreviousInterior()
    {
        foreach (GameObject obj in spawnedObjects)
        {
            if (obj != null)
            {
                NetworkServer.Destroy(obj);
            }
        }
        spawnedObjects.Clear();

        if (debugMode)
        {
            Debug.Log($"[InteriorGenerator] Очищено предыдущее содержимое дома {gameObject.name}");
        }
    }

    /// <summary>
    /// Выводит в консоль список всех созданных предметов и их количество.
    /// Используется для отладки — позволяет проверить, какие предметы и в каком количестве были созданы.
    /// Форматирует вывод в виде таблицы для лучшей читаемости.
    /// </summary>
    private void PrintSpawnedItems()
    {
        Debug.Log($"\n[InteriorGenerator] Сгенерированные предметы в доме {gameObject.name}:");
        Debug.Log("------------------------------------------------");

        foreach (var kvp in spawnedItemCounts)
        {
            Debug.Log($"  {kvp.Key}: {kvp.Value} шт.");
        }

        Debug.Log("------------------------------------------------\n");
    }

    /// <summary>
    /// Вызывается при деактивации объекта (когда объект становится неактивным в сцене).
    /// Очищает сгенерированный интерьер, чтобы при повторной активации создать новый набор предметов.
    /// Это предотвращает накопление объектов при многократной активации/деактивации дома.
    /// </summary>
    private void OnDisable()
    {
        if (!isServer) return;

        if (debugMode) Debug.Log($"[InteriorGenerator] Объект {gameObject.name} деактивирован, очищаем интерьер");
        ClearPreviousInterior();
    }

    /// <summary>
    /// Вызывается при уничтожении объекта. Гарантированно очищает все созданные объекты
    /// даже в случае неожиданного удаления дома из сцены.
    /// Предотвращает утечки памяти и «зависшие» объекты на сервере.
    /// </summary>
    private void OnDestroy()
    {
        if (!isServer) return;

        ClearPreviousInterior();
        if (debugMode) Debug.Log($"[InteriorGenerator] Дом {gameObject.name} уничтожен, все объекты интерьера очищены");
    }
    
    private void ManagePropContainer()
    {
        // Если контейнер уже назначен в инспекторе, используем его и выходим
        if (propContainer != null)
        {
            Debug.Log($"[InteriorGenerator] Используется Prop Container из инспектора: {propContainer.name}");
            propContainerName  = propContainer.name;
            return;
        }
    
        // Пытаемся найти существующий контейнер на сцене
        propContainer = GameObject.Find(propContainerName)?.transform;
    
        if (propContainer != null)
        {
            Debug.Log("[InteriorGenerator] Найден существующий Prop Container на сцене");
            return;
        }
    
        // Если не найден — создаём новый
        GameObject containerObject = new GameObject(propContainerName);
        propContainer = containerObject.transform;
    
        Debug.Log("[InteriorGenerator] Создан новый Prop Container на сцене");
    }
    
#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        DrawSpawnPoints();
    }

    /// <summary>
    /// Визуализирует точки спавна синими квадратами в редакторе Unity.
    /// Для каждой точки спавна из всех SpawnableItem рисуется синий квадрат.
    /// </summary>
    private void DrawSpawnPoints()
    {
        if (spawnableItems == null) return;

        Gizmos.color = Color.blue;

        foreach (var item in spawnableItems)
        {
            if (item.spawnPoints == null) continue;

            foreach (var point in item.spawnPoints)
            {
                if (point == null) continue;

                // Рисуем синий квадрат (куб в 3D) в позиции точки спавна
                Gizmos.DrawCube(point.position, Vector3.one * 0.2f);

                // Дополнительно можно нарисовать wireframe‑куб для лучшей видимости
                Gizmos.matrix = Matrix4x4.TRS(point.position, point.rotation, Vector3.one);
                Gizmos.DrawWireCube(Vector3.zero, Vector3.one * 0.3f);
            }
        }
    }
#endif
}