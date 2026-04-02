using Mirror;
using UnityEngine;
using UnityEngine.UI;

using UnityEngine.InputSystem;

namespace Kotenkoff
{
    // 1. Структура слота. Обязательно struct, чтобы Mirror мог её синхронизировать.
    public struct SyncInventorySlot
    {
        public bool isClaimed;
        public NetworkIdentity itemNetId; // Ссылка на предмет в сети
    }

    public sealed class PlayerInventory : NetworkBehaviour
    {
        [Header("References")]
        [SerializeField, Tooltip("Ссылка на хаб игрока")] 
        private PlayerEntity playerEntity;

        [Header("Inventory Settings")]
        [SerializeField, Tooltip("Количество слотов")] 
        private int maxSlots = 5;

        [Header("UI Settings (Local Player Only)")]
        [SerializeField, Tooltip("Закинь сюда UI-контейнеры (RectTransform) слотов")] 
        private RectTransform[] uiSlotContainers;
        [SerializeField] private float activeScale = 1.2f;  // Размер выбранного слота
        [SerializeField] private float normalScale = 1.0f;  // Размер обычного слота

        // 2. SyncList — магический список Mirror. Сервер меняет, клиенты видят.
        public readonly SyncList<SyncInventorySlot> slots = new SyncList<SyncInventorySlot>();

        // Локальная переменная для UI (какой слот сейчас выбран колесиком)
        private int currentSelectedIndex = 0;

        public override void OnStartServer()
        {
            // При старте сервера забиваем инвентарь пустыми слотами
            for (int i = 0; i < maxSlots; i++)
            {
                slots.Add(new SyncInventorySlot { isClaimed = false });
            }
        }

        public override void OnStartLocalPlayer()
        {
            UpdateUI(); // Обновляем скейл UI при спавне
        }

        private void Update()
        {
            // UI и инпуты обрабатываем ТОЛЬКО для локального игрока
            if (!isLocalPlayer || playerEntity.IsSitting) return;

            HandleScrollInput();
        }

        // --- УПРАВЛЕНИЕ UI И КОЛЕСИКОМ ---

    private void HandleScrollInput()
    {
    
    if (Mouse.current == null) return;


    float scroll = Mouse.current.scroll.ReadValue().y;

    if (scroll != 0)
    {
        
        if (scroll > 0) currentSelectedIndex--;
        else currentSelectedIndex++;

        // Круговая прокрутка (от последнего к первому и наоборот)
        if (currentSelectedIndex < 0) currentSelectedIndex = maxSlots - 1;
        if (currentSelectedIndex >= maxSlots) currentSelectedIndex = 0;

        UpdateUI();
        CmdSelectSlot(currentSelectedIndex); // Сервер, дай мне предмет из этого слота
    }
    }

        private void UpdateUI()
        {
            if (uiSlotContainers == null || uiSlotContainers.Length == 0) return;

            for (int i = 0; i < uiSlotContainers.Length; i++)
            {
                if (uiSlotContainers[i] != null)
                {
                    // Делаем активный слот больше, остальные — стандартного размера
                    float targetScale = (i == currentSelectedIndex) ? activeScale : normalScale;
                    uiSlotContainers[i].localScale = Vector3.one * targetScale;
                }
            }
        }

        // --- СЕТЕВАЯ ЛОГИКА ---

        [Command]
        private void CmdSelectSlot(int index)
        {
            if (index < 0 || index >= slots.Count) return;

            var slot = slots[index];
            
            // Если в слоте есть предмет, берем его. Если пусто — прячем текущий.
            if (slot.isClaimed && slot.itemNetId != null)
            {
                playerEntity.ServerEquipItem(slot.itemNetId);
            }
            else
            {
                playerEntity.ServerEquipItem(null); 
            }
        }

        [Server]
        public bool AddItem(GameObject itemObj)
        {
            if (!itemObj.TryGetComponent(out NetworkIdentity netId)) return false;

            for (int i = 0; i < slots.Count; i++)
            {
                // Ищем первый пустой слот
                if (!slots[i].isClaimed)
                {
                    slots[i] = new SyncInventorySlot
                    {
                        isClaimed = true,
                        itemNetId = netId
                    };

                    // Если мы подобрали предмет в ТОТ ЖЕ слот, который сейчас выбран в UI,
                    // заставляем клиента дернуть предмет в руку.
                    TargetCheckAutoEquip(netId.connectionToClient);

                    return true;
                }
            }
            Debug.LogWarning("[Inventory] Инвентарь полон!");
            return false;
        }

        [Server]
        public void RemoveItem(GameObject itemObj)
        {
            if (!itemObj.TryGetComponent(out NetworkIdentity netId)) return;

            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i].isClaimed && slots[i].itemNetId == netId)
                {
                    slots[i] = new SyncInventorySlot { isClaimed = false };
                    break;
                }
            }
        }

        // Вызываем у клиента, чтобы он обновил руки, если предмет упал в активный слот
        [TargetRpc]
        private void TargetCheckAutoEquip(NetworkConnection target)
        {
            CmdSelectSlot(currentSelectedIndex);
        }
    }
}