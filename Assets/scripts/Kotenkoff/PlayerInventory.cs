using Mirror;
using NaughtyAttributes;
using UnityEngine;

namespace Kotenkoff
{
    public sealed class PlayerInventory : NetworkBehaviour
    {
        [SerializeField, Tooltip("Это инвентарь игрока. Добавляй или убирай элементы, чтобы менять количество слотов")]
        private InventoryItem[] inventory;

        [Server] // Добавляем предмет в слот
        public void AddItem(GameObject item)
        {
            foreach (var inventoryItem in inventory)
            {
                // В поисках пустого слота
                if (!inventoryItem.IsClimed)
                {
                    if (item.TryGetComponent(out JerryCan jerryCan))
                    {
                        inventoryItem.UpdateItemInfo(InventoryItem.ItemType.JerryCan, jerryCan.gameObject);
                        break;
                    }
                }
            }
        }
        [Server] // Очищаем слот
        public void RemoveItem(GameObject item)
        {
            foreach (var inventoryItem in inventory)
            {
                if (inventoryItem.IsClimed && inventoryItem.ItemObject == item)
                {
                    inventoryItem.UpdateItemInfo(InventoryItem.ItemType.None, null);
                    break;
                }
            }
        }
    }

    [System.Serializable]
    public sealed class InventoryItem
    {
        [SerializeField, Tooltip("Занят ли этот слот")]
        private bool isClimed;
        internal bool IsClimed => isClimed;
        
        public enum ItemType { None , JerryCan }
        [SerializeField, Tooltip("Тип предмета")]
        private ItemType itemType;
        
        [SerializeField, ShowAssetPreview, Tooltip("GameObject предмета")]
        private GameObject itemObject;
        public GameObject ItemObject => itemObject;
        
        [Server] // Обновляем информацию слота
        public void UpdateItemInfo(ItemType type, GameObject item)
        {
            // Если у слота тип предмета None (т.е. предмета нет)
            if (itemType == ItemType.None)
            {
                if (type == ItemType.JerryCan)
                {
                    JerryCanItem(item);
                }
                isClimed = true;
            }
            // Если тип не None 
            else
            {
                // Сбрасывание слота до пустого
                
                itemType = ItemType.None;
                itemObject = null;
                
                isClimed = false;
            }
        }
        [Server]
        private void JerryCanItem(GameObject item)
        {
            itemType = ItemType.JerryCan;
            itemObject = item;
        }
    }
}