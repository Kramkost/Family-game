using System;
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
    /// Устанавливает слоты инвентаря для конкретного игрока.
    /// Вызывается при создании менеджера для игрока в CharacterInventory.Awake().
    /// Инициализирует массив слотов инвентаря, который будет использоваться
    /// для отображения и управления содержимым инвентаря данного игрока.
    /// </summary>
    /// <param name="slots">Массив слотов инвентаря для установки.</param>
    public void SetInventorySlots(InventorySlot[] slots)
    {
        inventorySlots = slots;
    }

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

        if (slot != null && slot.ObjectInSlot != null)
        {
            // Локально активируем объект
            slot.ObjectInSlot.SetActive(true);
            // Отправляем RPC всем клиентам с индексом слота вместо ссылки на GameObject
            RpcOnOffObject(slotIndex, true);
        }
        else
        {
            Debug.LogWarning($"[CmdShowObjectInSlot] Слот {slotIndex} пуст или не инициализирован, нечего показывать.");
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

        if (slot != null && slot.ObjectInSlot != null)
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
        // Добавляем проверку на инициализацию массива
        if (inventorySlots == null)
        {
            Debug.LogError("[IsValidSlotIndex] Массив слотов не инициализирован!");
            return false;
        }
        
        bool isValid = slotIndex >= 0 && slotIndex < inventorySlots.Length;
        if (!isValid)
        {
            Debug.LogError($"[IsValidSlotIndex] Некорректный индекс слота: {slotIndex}. Допустимый диапазон: 0–{inventorySlots.Length - 1}");
        }
        return isValid;
    }
    
    /// <summary>
    /// Команда сервера: синхронизирует состояние указанного слота (объект внутри).
    /// Вызывается с клиента, выполняется на сервере, транслирует состояние всем клиентам.
    /// </summary>
    /// <param name="slotIndex">Индекс слота.</param>
    /// <param name="obj">Объект, находящийся в слоте (может быть null).</param>
    [Command]
    public void CmdSyncSlotState(int slotIndex, GameObject obj)
    {
        if (!isServer) return;
        Debug.Log($"[CmdSyncSlotState] Синхронизация слота {slotIndex} с объектом {obj?.name ?? "null"}");

        if (!IsValidSlotIndex(slotIndex))
            return;

        InventorySlot slot = inventorySlots[slotIndex];
        if (slot != null)
        {
            // Обновляем состояние слота на сервере
            slot.ObjectInSlot = obj;
            uint netId = obj != null && obj.GetComponent<NetworkIdentity>() != null
                ? obj.GetComponent<NetworkIdentity>().netId
                : 0;
            // Передаём netId вместо ссылки на GameObject — это эффективнее и безопаснее
            RpcSyncSlotState(slotIndex, obj != null ? obj.GetComponent<NetworkIdentity>().netId : 0);
        }
    }

    /// <summary>
    /// RPC-метод для синхронизации состояния слота по netId.
    /// Получает netId объекта вместо ссылки на GameObject.
    /// Находит объект через NetworkIdentity.spawned и устанавливает в слот.
    /// Выполняется на всех клиентах после вызова с сервера.
    /// Логика:
    /// 1. Проверяет валидность индекса слота.
    /// 2. Если netId != 0, находит объект по netId в словаре NetworkIdentity.spawned.
    /// 3. Устанавливает найденный объект в слот или null, если netId = 0.
    /// </summary>
    /// <param name="slotIndex">Индекс слота, состояние которого нужно обновить.</param>
    /// <param name="netId">Сетевой идентификатор (netId) объекта, который должен быть в слоте.</param>
    [ClientRpc]
    private void RpcSyncSlotState(int slotIndex, uint netId)
    {
        Debug.Log($"[RpcSyncSlotState] Клиент получил обновление для слота {slotIndex}: netId {netId}");

        if (!IsValidSlotIndex(slotIndex))
            return;

        InventorySlot slot = inventorySlots[slotIndex];
        if (slot != null)
        {
            if (netId != 0)
            {
                // Находим объект по netId в словаре NetworkClient.spawned
                if (NetworkClient.spawned.TryGetValue(netId, out NetworkIdentity identity))
                {
                    slot.ObjectInSlot = identity.gameObject;
                }
                else
                {
                    Debug.LogWarning($"[RpcSyncSlotState] Объект с netId {netId} не найден в NetworkClient.spawned");
                    slot.ObjectInSlot = null;
                }
            }
            else
            {
                slot.ObjectInSlot = null;
            }
        }
    }
}
