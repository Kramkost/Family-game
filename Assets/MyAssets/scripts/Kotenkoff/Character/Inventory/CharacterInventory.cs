using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace MyAssets.scripts.Kotenkoff.Character.Inventory
{
    public sealed class CharacterInventory : NetworkBehaviour
    {
        [SerializeField, Tooltip("Родитель для объектов в руке.")]
        private Transform objectsParent;
        
        [SerializeField, Tooltip("Объект, который персонаж держит сейчас в руке."), Space(3)]
        [SyncVar(hook = nameof(OnObjectInHandChanged))] private GameObject objectInHand;
        /// <summary>
        /// Объект, который сейчас находится в руке.
        /// </summary>
        public GameObject ObjectInHand => objectInHand;
        
        [SerializeField, Tooltip("Выбранный слот."), ReadOnly, SyncVar]
        private int currentSlot;
        /// <summary>
        /// Выбранный слот.
        /// </summary>
        public int CurrentSlot => currentSlot;
        
        [SerializeField, Tooltip("Слоты инвентаря.")]
        private List<InventorySlot> inventorySlots;

        private void OnObjectInHandChanged(GameObject oldValue, GameObject newValue)
        {
            if (newValue != null)
            {
                SetObject(newValue);
            }
        }
        
        /// <summary>
        /// Попытка добавить указанный объект в инвентарь.
        /// </summary>
        /// <param name="go">(GameObject) Объект, который нужно добавить.</param>
        //[Command]
        public void CmdTryAddObject(GameObject go) => TryAddObjectToSlot(go);
        
        /// <summary>
        /// Попытка добавить указанный объект в слот инвентаря.
        /// </summary>
        /// <param name="go">Объект, который нужно добавить.</param>
        private void TryAddObjectToSlot(GameObject go)
        {
            if (!inventorySlots[currentSlot].IsOccupied)
            {
                inventorySlots[currentSlot].TryAddToSlot(go);
                ChangeCurrentSlot(inventorySlots[currentSlot], go);
            }
            else
            {
                for (int index = 0; index < inventorySlots.Count; index++)
                {
                    var slot = inventorySlots[index];
                
                    if (!slot.IsOccupied)
                    {
                        slot.TryAddToSlot(go);
                        ChangeCurrentSlot(slot, go);
                    
                        break;
                    }

                    /*if (currentSlot == index)
                    {
                        for (int i = index; i < inventorySlots.Count; i++)
                        {
                            var newSlot = inventorySlots[i];

                            if (!newSlot.IsOccupied)
                            {
                                slot.TryAddToSlot(go);
                                ChangeCurrentSlot(newSlot, go);
                            }
                        }
                    }*/
                }
            }
        }

        /// <summary>
        /// Изменяет выбранный слот в указанном направление.
        /// </summary>
        /// <param name="vector">Направление изменения слота. (См.  <b> пример </b> )</param>
        /// <example> <b> Пример: </b> <br/>
        /// (vector нужно писать с маленькой буквы)
        ///     <code>
        ///         inventory.ChangeCurrentSlot(ChangeSlotVector.Forward);
        ///     </code>
        /// </example>
        [Command]
        public void CmdChangeCurrentSlotWithVector(ChangeSlotVector vector)
        {
            int value = vector == ChangeSlotVector.Forward ? 1 : -1;
            
            if (currentSlot == inventorySlots.Count - 1 && vector == ChangeSlotVector.Forward)
                ChangeCurrentSlot(inventorySlots[0], inventorySlots[0].ObjectInSlot);
            else if (currentSlot == 0 && vector == ChangeSlotVector.Backward)
                ChangeCurrentSlot(inventorySlots[^1], inventorySlots[^1].ObjectInSlot);
            else
                ChangeCurrentSlot(inventorySlots[currentSlot + value], inventorySlots[currentSlot + value].ObjectInSlot);
        }

        /// <summary>
        /// Меняет выбранный слот на указанный.
        /// </summary>
        /// <param name="slot">Слот, который выберется</param>
        /// <param name="go">Объект, который игрок возьмёт в руку.</param>
        private void ChangeCurrentSlot(InventorySlot slot, GameObject go)
        {
            int value = 0;
            inventorySlots[currentSlot].CmdHideObject();

            for (int index = 0; index < inventorySlots.Count; index++)
            {
                if (inventorySlots[index] == slot)
                {
                    value = index;
                    break;
                }
            }
            
            currentSlot = value;
            
            objectInHand = go != null ? go : null;
            
            slot.CmdShowObject();
        }

        /// <summary>
        /// Метод, который просто телепортирует указанный объект к игроку и делает его ребёнком игрока.
        /// </summary>
        /// <param name="go">Объект, который нужно телепортировать.</param>
        private void SetObject(GameObject go)
        {
            if (go == null) return;
            
            go.transform.SetParent(objectsParent);
            go.transform.localPosition = Vector3.zero;
            
            go.GetComponent<Rigidbody>().isKinematic = true;
        }


        #region UnityMethods

        private void Start()
        {
            if (inventorySlots[0] != null)
            {
                currentSlot = 0;
            }
            else
            {
                Debug.LogError($"[CharacterInventory] У игрока ({netId}) в инвентаре нету слотов.");
            }
        }

        #endregion
    }
}