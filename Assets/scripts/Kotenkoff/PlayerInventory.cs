using Mirror;
using NaughtyAttributes;
using UnityEngine;

namespace Kotenkoff
{
    /// <summary>
    ///  <para>Класс инвентаря игрока.</para>
    /// </summary>
    public sealed class PlayerInventory : NetworkBehaviour
    {
        /// <summary>
        ///  <para>Инвентарь игрока.</para>
        /// </summary>
        [SerializeField, Tooltip("Это инвентарь игрока. Добавляй или убирай элементы, чтобы менять количество слотов")]
        private InventoryItem[] inventory;

        private void OnEnable()
        {
            if (inventory.Length <= 0)
            {
                Debug.LogWarning("[Server] В инвентаре нет слотов!");
            }
        }

        /// <summary>
        /// <para>Добавляет указанный предмет в свободный слот инвентаря.</para>
        /// </summary>
        /// <param name="item">Предмет, который будет добавлен в инвентарь.</param>
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
        
        /// <summary>
        ///  <para>Удаляет указанный предмет из инвентаря.</para>
        /// </summary>
        /// <param name="item">Предмет, который будет удалён из инвентаря.</param>
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
        /// <summary>
        ///  <para>Занят ли этот слот.</para>
        /// </summary>
        internal bool IsClimed => isClimed;
        
        /// <summary>
        ///  <para>Список возможных предметов в инвентаре.</para>
        /// </summary>
        public enum ItemType { None , JerryCan }
        [SerializeField, Tooltip("Тип предмета")]
        private ItemType itemType;
        
        [SerializeField, ShowAssetPreview, Tooltip("GameObject предмета")]
        private GameObject itemObject;
        /// <summary>
        ///  <para>Предмет, который занимает этот слот.</para>
        /// </summary>
        public GameObject ItemObject => itemObject;
        
        /// <summary>
        ///  <para>Обновляет информацию слота.</para>
        /// </summary>
        /// <param name="type">Тип предмета, который будет добавлен ('None' если нужно очистить слот).</param>
        /// <param name="item">Предмет, который будет добавлен (или удален).</param>
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
        
        /// <summary>
        ///  <para>Метод, который добавляет JerryCan в слот.</para>
        /// </summary>
        /// <param name="item">Предмет, который займет слот</param>
        [Server]
        private void JerryCanItem(GameObject item)
        {
            itemType = ItemType.JerryCan;
            itemObject = item;
        }
    }
}