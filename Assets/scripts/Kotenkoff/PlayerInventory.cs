using Mirror;
using NaughtyAttributes;
using UnityEngine;

namespace Kotenkoff
{
    public sealed class PlayerInventory : NetworkBehaviour
    {
        private PlayerEntity player;

        [SerializeField] private InventoryItem[] inventory;
        
        [Server]
        public void AddItem(GameObject item)
        {
            if (item.TryGetComponent(out JerryCan jerryCan))
            {

                foreach (var inventoryItem in inventory)
                {
                    if (!inventoryItem.IsClimed)
                    {
                        inventoryItem.UpdateItemInfo(InventoryItem.ItemType.JerryCan, jerryCan.gameObject);
                        break;
                    }
                }
            }
        }
        [Server]
        public void RemoveItem(GameObject item)
        {
            foreach (var inventoryItem in inventory)
            {
                if (inventoryItem.IsClimed)
                {
                    inventoryItem.UpdateItemInfo(InventoryItem.ItemType.None, null);
                    break;
                }
            }
        }
    }

    [System.Serializable]
    internal sealed class InventoryItem
    {
        [SerializeField] private bool isClimed;
        internal bool IsClimed => isClimed;
        
        public enum ItemType { None , JerryCan }
        [SerializeField] private ItemType itemType;
        
        [SerializeField, ShowAssetPreview] private GameObject itemObject;
        
        [Server]
        public void UpdateItemInfo(ItemType type, GameObject item)
        {
            if (itemType == ItemType.None)
            {
                if (type == ItemType.JerryCan)
                {
                    JerryCanItem(item);
                }
                isClimed = true;
            }
            else
            {
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