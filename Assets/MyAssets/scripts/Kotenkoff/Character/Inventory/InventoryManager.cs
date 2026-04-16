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

    /// <summary>
    /// Команда сервера: показать объект в указанном слоте.
    /// Вызывается с клиента, выполняется на сервере, транслируется всем клиентам через Rpc.
    /// </summary>
    /// <param name="slotIndex">Индекс слота в массиве inventorySlots.</param>
    [Command]
    public void CmdShowObjectInSlot(int slotIndex)
    {
        Debug.Log($"[CmdShowObjectInSlot] Вызвано сервером для слота {slotIndex}");

        // Проверка корректности индекса
        if (!IsValidSlotIndex(slotIndex))
            return;

        InventorySlot slot = inventorySlots[slotIndex];

        if (slot.ObjectInSlot != null)
        {
            // Локально активируем объект
            slot.ObjectInSlot.SetActive(true);
            // Отправляем RPC всем клиентам с индексом слота вместо ссылки на GameObject
            RpcOnOffObject(slotIndex, true);
        }
        else
        {
            Debug.LogWarning($"[CmdShowObjectInSlot] Слот {slotIndex} пуст, нечего показывать.");
        }
    }

    /// <summary>
    /// Команда сервера: скрыть объект в указанном слоте.
    /// Вызывается с клиента, выполняется на сервере, транслируется всем клиентам через Rpc.
    /// </summary>
    /// <param name="slotIndex">Индекс слота в массиве inventorySlots.</param>
    [Command]
    public void CmdHideObjectInSlot(int slotIndex)
    {
        Debug.Log($"[CmdHideObjectInSlot] Вызвано сервером для слота {slotIndex}");

        // Проверка корректности индекса
        if (!IsValidSlotIndex(slotIndex))
            return;

        InventorySlot slot = inventorySlots[slotIndex];

        if (slot.ObjectInSlot != null)
        {
            // Локально деактивируем объект
            slot.ObjectInSlot.SetActive(false);
            // Отправляем RPC всем клиентам с индексом слота вместо ссылки на GameObject
            RpcOnOffObject(slotIndex, false);
        }
        else
        {
            Debug.LogWarning($"[CmdHideObjectInSlot] Слот {slotIndex} пуст, нечего скрывать.");
        }
    }

    /// <summary>
    /// RPC: синхронизирует состояние видимости объекта во всех слотах для всех клиентов.
    /// Оптимизация: передаётся индекс слота вместо ссылки на GameObject для уменьшения сетевого трафика.
    /// </summary>
    /// <param name="slotIndex">Индекс слота, состояние которого нужно обновить.</param>
    /// <param name="state">Требуемое состояние активности (true — показать, false — скрыть).</param>
    [ClientRpc]
    private void RpcOnOffObject(int slotIndex, bool state)
    {
        Debug.Log($"[RpcOnOffObject] Получено клиентом: слот {slotIndex}, state={state}");

        // Проверка корректности индекса на клиенте
        if (!IsValidSlotIndex(slotIndex))
            return;

        InventorySlot slot = inventorySlots[slotIndex];

        if (slot.ObjectInSlot != null)
        {
            slot.ObjectInSlot.SetActive(state);
        }
        else
        {
            Debug.LogWarning($"[RpcOnOffObject] Клиент: слот {slotIndex} пуст, операция игнорируется.");
        }
    }

    /// <summary>
    /// Вспомогательный метод: проверяет, является ли индекс слота корректным.
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
