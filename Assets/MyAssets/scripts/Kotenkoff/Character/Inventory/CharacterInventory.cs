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
        
        [SerializeField, Tooltip("Выбранный слот."), ReadOnly, SyncVar(hook = nameof(OnCurrentSlotChanged))]
        private int currentSlot;
        /// <summary>
        /// Выбранный слот.
        /// </summary>
        public int CurrentSlot => currentSlot;
        
        [SerializeField, Tooltip("Слоты инвентаря.")]
        private List<InventorySlot> inventorySlots;

        // Ссылка на менеджера инвентаря
        [SerializeField] private InventoryManager inventoryManager;
        
        private void OnCurrentSlotChanged(int oldValue, int newValue)
        {
            Debug.Log($"[OnCurrentSlotChanged] Слот изменён: {oldValue} → {newValue} для игрока {netId}");
            // Здесь можно обновить UI, подсветить слот и т. д.
            //UpdateUIForCurrentSlot(newValue);
        }
        
        private void OnObjectInHandChanged(GameObject oldValue, GameObject newValue)
        {
            if (newValue != null)
            {
                SetObject(newValue);
            }
        }

        /// <summary>
        /// Попытка добавить указанный объект в инвентарь. Метод вызывается локально с проверкой isOwned.
        /// Если игрок владеет объектом, пытается добавить предмет в текущий слот или первый свободный слот.
        /// </summary>
        /// <param name="go">Объект, который нужно добавить в инвентарь.</param>
        public void TryAddObject(GameObject go)
        {
            Debug.Log($"[TryAddObject] Попытка добавить объект {go.name} в инвентарь игрока {netId}");

            TryAddObjectToSlot(go);
        }

        /// <summary>
        /// Внутренняя логика добавления объекта в слот инвентаря.
        /// Сначала пытается добавить в текущий слот, если он свободен.
        /// Если текущий слот занят, ищет первый свободный слот в инвентаре.
        /// </summary>
        /// <param name="go">Объект, который нужно добавить.</param>
        private void TryAddObjectToSlot(GameObject go)
        {
            Debug.Log($"[TryAddObjectToSlot] Попытка добавить {go.name} в слот {currentSlot}");

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
                Debug.Log($"[TryAddObjectToSlot] Объект {go.name} добавлен в слот {index}");
                break;
            }
                }
            }
        }

        /// <summary>
        /// Изменяет выбранный слот в указанном направлении (вперёд или назад).
        /// Использует арифметику по модулю для циклического переключения слотов.
        /// </summary>
        /// <param name="vector">Направление изменения слота (Forward или Backward).</param>
        public void ChangeCurrentSlotWithVector(ChangeSlotVector vector)
        {
            Debug.Log($"[ChangeCurrentSlotWithVector] Изменение слота для игрока {netId}, направление: {vector}");

            int delta = vector == ChangeSlotVector.Forward ? 1 : -1;
            int newSlotIndex = (currentSlot + delta + inventorySlots.Count) % inventorySlots.Count;
            ChangeCurrentSlot(inventorySlots[newSlotIndex], inventorySlots[newSlotIndex].ObjectInSlot);
        }

        /// <summary>
        /// Меняет выбранный слот на указанный и обновляет состояние инвентаря.
        /// Скрывает объект в предыдущем слоте через InventoryManager и показывает в новом.
        /// Обновляет SyncVar currentSlot и objectInHand.
        /// </summary>
        /// <param name="slot">Слот, который будет выбран.</param>
        /// <param name="go">Объект, который игрок возьмёт в руку (может быть null).</param>
        private void ChangeCurrentSlot(InventorySlot slot, GameObject go)
        {
            int value = 0;

            // Скрываем объект в текущем слоте через InventoryManager
            if (inventoryManager != null)
            {
                inventoryManager.HideObjectInSlot(currentSlot);
            }
            else
            {
                Debug.LogWarning("[ChangeCurrentSlot] InventoryManager не инициализирован!");
            }

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

            // Синхронизируем состояние слотов с менеджером
            if (inventoryManager != null)
            {
                inventoryManager.SyncSlotState(currentSlot, slot.ObjectInSlot);
                inventoryManager.ShowObjectInSlot(currentSlot);
            }

            Debug.Log($"[ChangeCurrentSlot] Слот изменён на {currentSlot}, объект в руке: {objectInHand?.name ?? "null"}");
        }

        /// <summary>
        /// Метод, который просто телепортирует указанный объект к игроку и делает его ребёнком игрока.
        /// Устанавливает позицию в ноль относительно родителя и отключает физику.
        /// </summary>
        /// <param name="go">Объект, который нужно телепортировать.</param>
        private void SetObject(GameObject go)
        {
            if (go == null) return;

            go.transform.SetParent(objectsParent);
            go.transform.localPosition = Vector3.zero;

            Rigidbody rb = go.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
            }

            Debug.Log($"[SetObject] Объект {go.name} перемещён в руку игрока {netId}");
        }

        #region UnityMethods

        private void Awake()
        {
            if (inventoryManager == null)
            {
                // Находим менеджер инвентаря при инициализации
                inventoryManager = FindObjectOfType<InventoryManager>();

                if (inventoryManager == null)
                {
                    Debug.LogError("[CharacterInventory] Не найден InventoryManager в сцене!");
                }
            }
        }

        private void Start()
        {
            if (inventorySlots.Count > 0)
            {
                currentSlot = 0;
            }
            else
            {
                Debug.LogError($"[CharacterInventory] У игрока ({netId}) в инвентаре нет слотов.");
            }
        }

        #endregion
    }
}
