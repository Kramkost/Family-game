using System.Collections.Generic;
using Mirror;
using MyAssets.scripts.Kotenkoff.Items;
using UnityEngine;

/// <summary>
/// Сетевой инвентарь игрока. <br/>
/// Хранит слоты, текущий слот, обрабатывает добавление/удаление предметов. <br/>
/// Все изменения происходят на сервере — клиент только отправляет запросы.
/// </summary>
[RequireComponent(typeof(NetworkIdentity))]
public class CharacterInventory : NetworkBehaviour
{
    /// <summary>
    /// Синхронизированная переменная: текущий активный слот. <br/>
    /// Изменяется на сервере, обновляет UI через хук <see cref="ShowObjectInSlot"/>.
    /// </summary>
    [SyncVar(hook = nameof(ShowObjectInSlot))]
    public int currentSlot = 0;

    /// <summary>
    /// Синхронизированная переменная: предыдущий слот. <br/>
    /// Используется для снятия выделения с прошлого слота через хук <see cref="HideObjectInSlot"/>.
    /// </summary>
    [SyncVar(hook = nameof(HideObjectInSlot))]
    private int previousSlot = 0;

    /// <summary>
    /// Синхронизированная переменная: сетевой ID предмета в текущем слоте. <br/>
    /// Используется для UI (например, отображение имени).
    /// </summary>
    [SyncVar] public uint selectedItemId;

    /// <summary>
    /// Синхронизированная переменная: количество предметов в текущем слоте. <br/>
    /// Используется для отображения числа в UI.
    /// </summary>
    [SyncVar] public int selectedItemAmount;

    /// <summary>
    /// Количество слотов инвентаря. <br/>
    /// Настраивается в инспекторе. <br/>
    /// Клавиши 1, 2, 3... будут соответствовать слотам от 0 до slotCount-1.
    /// </summary>
    [SerializeField, Tooltip("Количество слотов инвентаря (настраивается в инспекторе).")]
    private int slotCount = 6;

    /// <summary>
    /// Синхронизированный список стеков предметов. <br/>
    /// Автоматически синхронизируется между сервером и клиентами.
    /// </summary>
    public SyncList<ItemStack> slots = new SyncList<ItemStack>();

    /// <summary>
    /// Ссылка на менеджер UI инвентаря (InventoryManager). <br/>
    /// Инициализируется на клиенте при старте.
    /// </summary>
    private InventoryManager uiManager;

    /// <summary>
    /// Вызывается при инициализации компонента. <br/>
    /// Заполняет слоты пустыми значениями.
    /// </summary>
    private void Awake()
    {
        Debug.Log($"[CharacterInventory.Awake] Инициализация инвентаря с {slotCount} слотами");

        slots.Clear();
        for (int i = 0; i < slotCount; i++)
        {
            slots.Add(ItemStack.Empty);
        }
    }

    /// <summary>
    /// Вызывается на клиенте при подключении к сети. <br/>
    /// Сохраняет ссылку на <see cref="InventoryManager"/> для обновления UI.
    /// </summary>
    public override void OnStartClient()
    {
        Debug.Log($"[CharacterInventory.OnStartClient] Инвентарь инициализирован для {netId}");
        uiManager = GetComponent<InventoryManager>();
    }

    /// <summary>
    /// Запрашивает смену текущего слота. <br/>
    /// Вызывается клиентом, обрабатывается на сервере через <see cref="CmdChangeCurrentSlot"/>.
    /// </summary>
    /// <param name="index">Индекс нового слота (0-based).</param>
    public void ChangeCurrentSlot(int index)
    {
        Debug.Log($"[CharacterInventory.ChangeCurrentSlot] Запрос на смену слота: {index}");

        if (index < 0 || index >= slots.Count)
        {
            Debug.LogWarning($"[CharacterInventory.ChangeCurrentSlot] Индекс {index} вне диапазона (0–{slots.Count - 1})");
            return;
        }

        if (slots[index].isEmpty)
        {
            Debug.Log($"[CharacterInventory.ChangeCurrentSlot] Слот {index} пуст — смена невозможна");
            return;
        }

        CmdChangeCurrentSlot(index);
    }

    /// <summary>
    /// Команда: смена текущего слота на сервере. <br/>
    /// Проверяет валидность и обновляет <see cref="currentSlot"/> и <see cref="previousSlot"/>.
    /// </summary>
    /// <param name="index">Индекс нового слота.</param>
    [Command]
    private void CmdChangeCurrentSlot(int index)
    {
        Debug.Log($"[CharacterInventory.CmdChangeCurrentSlot] Сервер меняет слот на {index}");

        if (index >= 0 && index < slots.Count && !slots[index].isEmpty)
        {
            previousSlot = currentSlot;
            currentSlot = index;

            selectedItemId = slots[index].itemNetId?.netId ?? 0;
            selectedItemAmount = slots[index].count;

            Debug.Log($"[CharacterInventory.CmdChangeCurrentSlot] Активный слот изменён на {index}, предмет: {selectedItemId}, количество: {selectedItemAmount}");
        }
        else
        {
            Debug.LogWarning($"[CharacterInventory.CmdChangeCurrentSlot] Невозможно установить слот {index} — пуст или некорректный");
        }
    }

    /// <summary>
    /// Добавляет предмет в инвентарь на сервере. <br/>
    /// Пытается сложить в существующий стек, иначе — в первый пустой слот.
    /// </summary>
    /// <param name="itemObject">Объект предмета, который нужно добавить.</param>
    /// <returns>True — предмет успешно добавлен.</returns>
    [Server]
    public bool ServerAddItem(GameObject itemObject)
    {
        Debug.Log($"[CharacterInventory.ServerAddItem] Попытка добавить предмет: {itemObject.name}");

        if (!itemObject.TryGetComponent(out NetworkIdentity itemNetId))
        {
            Debug.LogError($"[CharacterInventory.ServerAddItem] Объект {itemObject.name} не имеет NetworkIdentity");
            return false;
        }

        Item itemComponent = itemNetId.GetComponent<Item>();
        if (itemComponent == null)
        {
            Debug.LogError($"[CharacterInventory.ServerAddItem] Объект {itemObject.name} не имеет компонента Item");
            return false;
        }

        // Попытка добавить в существующий стек (если предмет стекуемый)
        for (int i = 0; i < slots.Count; i++)
        {
            if (!slots[i].isEmpty && slots[i].Matches(itemObject))
            {
                slots[i] = new ItemStack(slots[i].itemNetId, slots[i].count + 1);
                Debug.Log($"[CharacterInventory.ServerAddItem] Предмет добавлен в стек {i}, теперь: {slots[i].count}");
                return true;
            }
        }

        // Поиск первого пустого слота
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i].isEmpty)
            {
                slots[i] = new ItemStack(itemNetId, 1);
                Debug.Log($"[CharacterInventory.ServerAddItem] Предмет добавлен в слот {i}");
                return true;
            }
        }

        Debug.Log("[CharacterInventory.ServerAddItem] Инвентарь полон — предмет не добавлен");
        return false;
    }

    /// <summary>
    /// Удаляет предмет из инвентаря на сервере. <br/>
    /// Уменьшает счётчик в стеке или очищает слот.
    /// </summary>
    /// <param name="itemObject">Объект предмета, который нужно удалить.</param>
    /// <returns>True — предмет успешно удалён.</returns>
    [Server]
    public bool ServerRemoveItem(GameObject itemObject)
    {
        Debug.Log($"[CharacterInventory.ServerRemoveItem] Попытка удалить предмет: {itemObject.name}");

        for (int i = 0; i < slots.Count; i++)
        {
            if (!slots[i].isEmpty && slots[i].itemNetId.gameObject == itemObject)
            {
                if (slots[i].count <= 1)
                {
                    slots[i] = ItemStack.Empty;
                    Debug.Log($"[CharacterInventory.ServerRemoveItem] Слот {i} очищен");
                }
                else
                {
                    slots[i] = new ItemStack(slots[i].itemNetId, slots[i].count - 1);
                    Debug.Log($"[CharacterInventory.ServerRemoveItem] Счётчик в слоте {i} уменьшен до {slots[i].count}");
                }
                return true;
            }
        }

        Debug.LogWarning($"[CharacterInventory.ServerRemoveItem] Предмет {itemObject.name} не найден в инвентаре");
        return false;
    }

    /// <summary>
    /// Возвращает NetworkIdentity предмета в текущем слоте. <br/>
    /// Используется для взаимодействия (например, выброс).
    /// </summary>
    /// <returns>Сетевой объект предмета или null.</returns>
    public NetworkIdentity GetItemInCurrentSlot()
    {
        NetworkIdentity item = !slots[currentSlot].isEmpty ? slots[currentSlot].itemNetId : null;
        Debug.Log($"[CharacterInventory.GetItemInCurrentSlot] Возвращён предмет: {(item ? item.name : "пусто")}");
        return item;
    }

    /// <summary>
    /// Хук, вызываемый при изменении <see cref="currentSlot"/>. <br/>
    /// Обновляет UI — выделяет новый слот.
    /// </summary>
    /// <param name="newSlot">Новый активный слот.</param>
    /// <param name="oldSlot">Предыдущий активный слот.</param>
    public void ShowObjectInSlot(int newSlot, int oldSlot)
    {
        Debug.Log($"[CharacterInventory.ShowObjectInSlot] Слот {newSlot} выделен");

        if (uiManager != null)
        {
            uiManager.ShowObjectInSlot(newSlot, oldSlot);
        }
    }

    /// <summary>
    /// Хук, вызываемый при изменении <see cref="currentSlot"/>. <br/>
    /// Обновляет UI — снимает выделение с предыдущего слота.
    /// </summary>
    /// <param name="newSlot">Новый активный слот.</param>
    /// <param name="oldSlot">Предыдущий активный слот.</param>
    public void HideObjectInSlot(int newSlot, int oldSlot)
    {
        Debug.Log($"[CharacterInventory.HideObjectInSlot] Слот {oldSlot} снят с выделения");

        if (uiManager != null)
        {
            uiManager.HideObjectInSlot(newSlot, oldSlot);
        }
    }
}
