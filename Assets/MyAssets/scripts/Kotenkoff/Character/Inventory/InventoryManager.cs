using System;
using Mirror;
using MyAssets.scripts.Kotenkoff.Character.Inventory;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Менеджер UI инвентаря. <br/>
/// Отвечает за:
/// - Создание визуальных слотов
/// - Обработку ввода: цифры (1–N), колёсико мыши, G — выброс
/// - Синхронизацию с CharacterInventory
/// </summary>
[RequireComponent(typeof(CharacterInventory))]
public class InventoryManager : NetworkBehaviour
{
    [Header("Настройки")]
    [SerializeField, Tooltip("Префаб визуального слота инвентаря.")]
    private GameObject inventorySlotPrefab;

    [SerializeField, Tooltip("Контейнер для слотов (например, GridLayoutGroup).")]
    private Transform slotsContainer;

    [SerializeField, Tooltip("Клавиша для выброса предмета (по умолчанию G).")]
    private KeyCode dropKey = KeyCode.G;

    [Header("UI Элементы")]
    [SerializeField, Tooltip("Текст для отображения количества в стеке.")]
    private Text itemCountText;

    private CharacterInventory inventory;
    private InventorySlot[] slots;
    private int lastMouseScrollSlot = 0;

    /// <summary>
    /// Вызывается при старте. Получает ссылку на инвентарь.
    /// </summary>
    private void Awake()
    {
        Debug.Log($"[InventoryManager.Awake] Инициализация для {netId}");
        inventory = GetComponent<CharacterInventory>();
    }

    /// <summary>
    /// Вызывается на клиенте при подключении. <br/>
    /// Создаёт визуальные слоты и подписывается на изменения инвентаря.
    /// </summary>
    public override void OnStartClient()
    {
        Debug.Log($"[InventoryManager.OnStartClient] Создание UI инвентаря для клиента {netId}");

        if (!isLocalPlayer)
        {
            Debug.Log("[InventoryManager.OnStartClient] Не локальный игрок — UI не создаётся.");
            return;
        }

        if (inventorySlotPrefab == null || slotsContainer == null)
        {
            Debug.LogError("[InventoryManager.OnStartClient] Не задан префаб слота или контейнер!");
            return;
        }

        int slotCount = inventory.slots.Count;
        slots = new InventorySlot[slotCount];
        for (int i = 0; i < slotCount; i++)
        {
            GameObject slotObj = Instantiate(inventorySlotPrefab, slotsContainer);
            slots[i] = slotObj.GetComponent<InventorySlot>();
            slots[i].SetSlotIndex(i);
        }

        inventory.slots.Callback += OnInventoryChanged;
        UpdateAllSlots();
    }

    /// <summary>
    /// Вызывается каждый кадр. <br/>
    /// Обрабатывает:
    /// - Нажатие клавиш 1–N для выбора слота (N = количество слотов)
    /// - Скролл колёсиком мыши
    /// - Нажатие G для выброса
    /// </summary>
    private void Update()
    {
        if (!isLocalPlayer || !isOwned) return;

        // Выбор слота по цифрам (1–N)
        int slotCount = inventory.slots.Count;
        for (int i = 0; i < slotCount; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i) || Input.GetKeyDown(KeyCode.Keypad1 + i))
            {
                OnSlotNumberPressed(i);
                break;
            }
        }

        // Переключение колёсиком мыши
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0f)
        {
            OnMouseScroll(scroll);
        }

        // Выброс по G
        if (Input.GetKeyDown(dropKey))
        {
            OnDropButtonClicked();
        }
    }

    /// <summary>
    /// Вызывается при нажатии цифровой клавиши (1–N). <br/>
    /// Меняет текущий слот.
    /// </summary>
    /// <param name="slotIndex">Индекс слота (0–N-1).</param>
    private void OnSlotNumberPressed(int slotIndex)
    {
        Debug.Log($"[InventoryManager.OnSlotNumberPressed] Нажата клавиша {slotIndex + 1}");
        if (slotIndex < inventory.slots.Count && !inventory.slots[slotIndex].isEmpty)
        {
            inventory.ChangeCurrentSlot(slotIndex);
        }
        else
        {
            Debug.Log($"[InventoryManager.OnSlotNumberPressed] Слот {slotIndex} пуст — не выбран");
        }
    }

    /// <summary>
    /// Вызывается при скролле колёсиком мыши. <br/>
    /// Переключает слот вперёд/назад.
    /// </summary>
    /// <param name="scroll">Направление скролла (положительное — вверх).</param>
    private void OnMouseScroll(float scroll)
    {
        int direction = scroll > 0f ? -1 : 1; // Вверх — следующий, вниз — предыдущий
        int newSlot = inventory.currentSlot;

        do
        {
            newSlot = (newSlot + direction + inventory.slots.Count) % inventory.slots.Count;
        } while (newSlot != inventory.currentSlot && inventory.slots[newSlot].isEmpty);

        if (newSlot != inventory.currentSlot)
        {
            Debug.Log($"[InventoryManager.OnMouseScroll] Скролл: переключение на слот {newSlot}");
            inventory.ChangeCurrentSlot(newSlot);
        }
        else
        {
            Debug.Log("[InventoryManager.OnMouseScroll] Все слоты пусты — переключение невозможно");
        }
    }

    /// <summary>
    /// Вызывается при изменении содержимого инвентаря. <br/>
    /// Обновляет соответствующий визуальный слот.
    /// </summary>
    /// <param name="operation">Тип операции (Add, Remove и т.д.).</param>
    /// <param name="index">Индекс изменённого слота.</param>
    /// <param name="oldValue">Старое значение (не используется).</param>
    /// <param name="newValue">Новое значение (ItemStack).</param>
    private void OnInventoryChanged(SyncList<ItemStack>.Operation operation, int index, ItemStack oldValue, ItemStack newValue)
    {
        Debug.Log($"[InventoryManager.OnInventoryChanged] Слот {index} изменён: {operation}");

        if (slots != null && index < slots.Length)
        {
            slots[index].UpdateSlot(newValue);
        }
    }

    /// <summary>
    /// Обновляет все визуальные слоты на основе текущего состояния инвентаря.
    /// </summary>
    private void UpdateAllSlots()
    {
        Debug.Log("[InventoryManager.UpdateAllSlots] Обновление всех слотов");

        for (int i = 0; i < inventory.slots.Count; i++)
        {
            slots[i].UpdateSlot(inventory.slots[i]);
        }
    }

    /// <summary>
    /// Показывает, что предмет находится в руках (например, выделяет слот). <br/>
    /// Вызывается через хук SyncVar.
    /// </summary>
    /// <param name="newSlot">Индекс нового активного слота.</param>
    /// <param name="oldSlot">Индекс предыдущего активного слота.</param>
    public void ShowObjectInSlot(int newSlot, int oldSlot)
    {
        Debug.Log($"[InventoryManager.ShowObjectInSlot] Слот {newSlot} выделен");

        if (slots == null || newSlot < 0 || newSlot >= slots.Length) return;

        slots[newSlot].SetSelected(true);

        var stack = inventory.slots[newSlot];
        itemCountText.text = stack.isEmpty ? "" : stack.count.ToString();
    }

    /// <summary>
    /// Скрывает выделение с предыдущего слота. <br/>
    /// Вызывается через хук SyncVar.
    /// </summary>
    /// <param name="newSlot">Индекс нового активного слота.</param>
    /// <param name="oldSlot">Индекс предыдущего активного слота.</param>
    public void HideObjectInSlot(int newSlot, int oldSlot)
    {
        Debug.Log($"[InventoryManager.HideObjectInSlot] Слот {oldSlot} снят с выделения");

        if (slots == null || oldSlot < 0 || oldSlot >= slots.Length) return;

        slots[oldSlot].SetSelected(false);
    }

    /// <summary>
    /// Вызывается при клике по визуальному слоту. <br/>
    /// Меняет текущий слот на выбранный.
    /// </summary>
    /// <param name="index">Индекс выбранного слота.</param>
    public void OnSlotClicked(int index)
    {
        Debug.Log($"[InventoryManager.OnSlotClicked] Клик по слоту {index}");

        if (index < 0 || index >= inventory.slots.Count)
        {
            Debug.LogError($"[InventoryManager.OnSlotClicked] Некорректный индекс: {index}");
            return;
        }

        if (inventory.slots[index].isEmpty)
        {
            Debug.Log($"[InventoryManager.OnSlotClicked] Слот {index} пуст — выбор не изменён");
            return;
        }

        inventory.ChangeCurrentSlot(index);
    }

    /// <summary>
    /// Вызывается при нажатии клавиши выброса. <br/>
    /// Отправляет запрос на выброс текущего предмета через компонент Item.
    /// </summary>
    public void OnDropButtonClicked()
    {
        Debug.Log("[InventoryManager.OnDropButtonClicked] Попытка выбросить предмет");

        NetworkIdentity currentItem = inventory.GetItemInCurrentSlot();
        if (currentItem == null)
        {
            Debug.Log("[InventoryManager.OnDropButtonClicked] Нет предмета для выброса");
            return;
        }

        Item itemComponent = currentItem.GetComponent<Item>();
        if (itemComponent != null)
        {
            itemComponent.TryDrop(inventory);
        }
        else
        {
            Debug.LogWarning("[InventoryManager.OnDropButtonClicked] Предмет не имеет компонента Item");
        }
    }
}
