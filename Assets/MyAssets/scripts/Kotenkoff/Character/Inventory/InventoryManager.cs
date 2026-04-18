using Mirror;
using MyAssets.scripts.Kotenkoff.Character.Inventory;
using UnityEngine;

/// <summary>
/// Сетевой менеджер инвентаря. Управляет видимостью объектов в слотах с синхронизацией между клиентами.
/// Наследуется от NetworkBehaviour для поддержки сетевых вызовов (Command, ClientRpc).
/// </summary>
public class InventoryManager : NetworkBehaviour
{
    [SerializeField]
    private InventorySlot[] inventorySlots;
    
    // Синхронизируемое состояние видимости слотов
    [SyncVar]
    private int[] visibleSlots;

    private void Awake()
    {
        if (visibleSlots == null || visibleSlots.Length != inventorySlots.Length)
        {
            visibleSlots = new int[inventorySlots.Length];
        }
    }
    
    /// <summary>
    /// Показать объект в указанном слоте для всех клиентов.
    /// Вызывается с клиента, выполняется локально.
    /// Активирует GameObject в указанном слоте и синхронизирует состояние через SyncVar (если необходимо).
    /// </summary>
    /// <param name="slotIndex">Индекс слота в массиве inventorySlots.</param>
    public void ShowObjectInSlot(int slotIndex)
    {
        Debug.Log($"[ShowObjectInSlot] Попытка показать объект в слоте {slotIndex} для игрока {netId}");

        if (!IsValidSlotIndex(slotIndex))
            return;

        InventorySlot slot = inventorySlots[slotIndex];
        if (slot != null && slot.ObjectInSlot != null)
        {
            slot.ObjectInSlot.SetActive(true);
            visibleSlots[slotIndex] = 1; // 1 = виден
            Debug.Log($"[ShowObjectInSlot] Объект в слоте {slotIndex} активирован.");
        }
        else
        {
            Debug.LogWarning($"[ShowObjectInSlot] Слот {slotIndex} пуст или не инициализирован.");
        }
    }

    /// <summary>
    /// Скрыть объект в указанном слоте для всех клиентов.
    /// Деактивирует GameObject в указанном слоте.
    /// </summary>
    /// <param name="slotIndex">Индекс слота в массиве inventorySlots.</param>
    public void HideObjectInSlot(int slotIndex)
    {
        Debug.Log($"[HideObjectInSlot] Попытка скрыть объект в слоте {slotIndex} для игрока {netId}");

        if (!IsValidSlotIndex(slotIndex))
            return;

        InventorySlot slot = inventorySlots[slotIndex];
        if (slot.ObjectInSlot != null)
        {
            slot.ObjectInSlot.SetActive(false);
            visibleSlots[slotIndex] = 0; // 0 = скрыт
            Debug.Log($"[HideObjectInSlot] Объект в слоте {slotIndex} деактивирован.");
        }
        else
        {
            Debug.LogWarning($"[HideObjectInSlot] Слот {slotIndex} пуст.");
        }
    }

    /// <summary>
    /// Хук, вызываемый при изменении visibleSlots.
    /// Обновляет видимость объектов на всех клиентах.
    /// </summary>
    private void OnVisibleSlotsChanged(int[] oldValue, int[] newValue)
    {
        Debug.Log($"[OnVisibleSlotsChanged] Обновление видимости слотов для игрока {netId}");
        for (int i = 0; i < newValue.Length; i++)
        {
            if (!IsValidSlotIndex(i)) continue;

            InventorySlot slot = inventorySlots[i];
            if (slot != null && slot.ObjectInSlot != null)
            {
                slot.ObjectInSlot.SetActive(newValue[i] == 1);
            }
        }
    }
    
    /// <summary>
    /// Синхронизирует состояние указанного слота (объект внутри) для всех клиентов.
    /// Обновляет ссылку на объект в слоте и обеспечивает согласованность данных между клиентами.
    /// </summary>
    /// <param name="slotIndex">Индекс слота.</param>
    /// <param name="obj">Объект, находящийся в слоте (может быть null).</param>
    public void SyncSlotState(int slotIndex, GameObject obj)
    {
        Debug.Log($"[SyncSlotState] Синхронизация слота {slotIndex} с объектом {obj?.name ?? "null"} для игрока {netId}");

        if (!IsValidSlotIndex(slotIndex))
            return;

        InventorySlot slot = inventorySlots[slotIndex];
        if (slot != null)
        {
            slot.ObjectInSlot = obj;
            Debug.Log($"[SyncSlotState] Слот {slotIndex} обновлён.");
        }
    }

    /// <summary>
    /// Вспомогательный метод: проверяет, является ли индекс слота корректным.
    /// Проверяет, что индекс находится в допустимом диапазоне (0 ≤ index < длина массива слотов).
    /// При ошибке выводит сообщение в консоль.
    /// </summary>
    /// <param name="slotIndex">Проверяемый индекс.</param>
    /// <returns>true, если индекс корректен, иначе false.</returns>
    private bool IsValidSlotIndex(int slotIndex)
    {
        bool isValid = slotIndex >= 0 && slotIndex < inventorySlots.Length;
        if (!isValid)
        {
            Debug.LogError($"[IsValidSlotIndex] Некорректный индекс слота: {slotIndex}. Допустимый диапазон: 0–{inventorySlots.Length - 1}");
        }
        return isValid;
    }
}
