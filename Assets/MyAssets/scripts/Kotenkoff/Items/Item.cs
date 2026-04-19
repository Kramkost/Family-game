using Mirror;
using MyAssets.scripts.Kotenkoff.Character.Interfaces;
using MyAssets.scripts.Kotenkoff.Character.Inventory;
using NaughtyAttributes;
using UnityEngine;

namespace MyAssets.scripts.Kotenkoff.Items
{
    [RequireComponent(typeof(NetworkIdentity))]
    public abstract class Item : NetworkBehaviour, IInteractableTest, IItemTooltip
    {
        /// <summary>
        /// Тип предмета.
        /// </summary>
        [Header("Основные:"), SerializeField, Tooltip("Тип предмета.")]
        protected ItemType itemType;
        public ItemType ItemType => itemType;
        
        
        [SerializeField, Tooltip("Имя предмета"), Header("Дополнительные:")]
        protected string itemName = "Не определено";
        public string ItemName => itemName;
        
        [SerializeField, Tooltip("Описание предмета"), ResizableTextArea]
        protected string itemDescription = "...";
        public string ItemDescription => itemDescription;
        
        [SerializeField, Tooltip("Подсказка предмета.")] protected string itemTooltip = "[E] Использовать";
        public string ItemTooltip => itemTooltip;
        
        public void TryInteract(CharacterInventory inventory) => inventory.TryAddObject(gameObject);
    }
}