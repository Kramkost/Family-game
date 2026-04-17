using System.Collections;
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

        // Ссылка на менеджер инвентаря
        [SerializeField] private InventoryManager inventoryManager;
        
        private void OnCurrentSlotChanged(int oldValue, int newValue)
        {
            Debug.Log($"[OnCurrentSlotChanged] Слот изменён: {oldValue} → {newValue}");
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
        /// Попытка добавить указанный объект в инвентарь.
        /// </summary>
        /// <param name="go">(GameObject) Объект, который нужно добавить.</param>
        [Command]
        public void CmdTryAddObject(GameObject go)
        {
            if (!isServer)
            {
                Debug.LogWarning("[CmdTryAddObject] Вызов с клиента без прав сервера.");
                return;
            }
            
            if (inventoryManager == null)
            {
                Debug.LogError("[CmdTryAddObject] InventoryManager не инициализирован!");
                return;
            }
            
            TryAddObjectToSlot(go);
        }

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
            // Проверка владения объектом
            if (!isOwned)
            {
                Debug.LogWarning("Нет прав для изменения слота.isOwned: " + isOwned);
                return;
            }
            
            // Критическая проверка: InventoryManager должен существовать
            if (inventoryManager == null)
            {
                Debug.LogError("[CmdChangeCurrentSlotWithVector] InventoryManager не инициализирован!");
                return;
            }

            // Проверка валидности списка слотов
            if (inventorySlots == null || inventorySlots.Count == 0)
            {
                Debug.LogError("[CmdChangeCurrentSlotWithVector] Список слотов не инициализирован или пуст!");
                return;
            }
            
            int delta = vector == ChangeSlotVector.Forward ? 1 : -1;
            int newSlotIndex = (currentSlot + delta + inventorySlots.Count) % inventorySlots.Count;
             
            // Дополнительная проверка на валидность индекса
            if (newSlotIndex < 0 || newSlotIndex >= inventorySlots.Count)
            {
                Debug.LogError($"[CmdChangeCurrentSlotWithVector] Некорректный индекс слота: {newSlotIndex}");
                return;
            }

            // Получаем слот по индексу
            InventorySlot newSlot = inventorySlots[newSlotIndex];
            if (newSlot == null)
            {
                Debug.LogError($"[CmdChangeCurrentSlotWithVector] Слот по индексу {newSlotIndex} не инициализирован!");
                return;
            }

            // Безопасный вызов ChangeCurrentSlot с проверкой объекта в слоте
            GameObject slotObject = newSlot.ObjectInSlot;
            ChangeCurrentSlot(newSlot, slotObject);
            /*if (currentSlot == inventorySlots.Count - 1 && vector == ChangeSlotVector.Forward)
                ChangeCurrentSlot(inventorySlots[0], inventorySlots[0].ObjectInSlot);
            else if (currentSlot == 0 && vector == ChangeSlotVector.Backward)
                ChangeCurrentSlot(inventorySlots[^1], inventorySlots[^1].ObjectInSlot);
            else
                ChangeCurrentSlot(inventorySlots[currentSlot + value], inventorySlots[currentSlot + value].ObjectInSlot);*/
        }

        /// <summary>
        /// Меняет выбранный слот на указанный.
        /// </summary>
        /// <param name="slot">Слот, который выберется</param>
        /// <param name="go">Объект, который игрок возьмёт в руку.</param>
        private void ChangeCurrentSlot(InventorySlot slot, GameObject go)
        {
            int value = -1;

            for (int index = 0; index < inventorySlots.Count; index++)
            {
                if (inventorySlots[index] == slot)
                {
                    value = index;
                    break;
                }
            }
            
            if (value == -1)
            {
                Debug.LogError("[ChangeCurrentSlot] Слот не найден в списке!");
                return;
            }

            currentSlot = value;
            objectInHand = go != null ? go : null;

            // Отправляем команду на сервер для синхронизации
            if (isLocalPlayer && isClient)
            {
                CmdChangeCurrentSlotOnServer(currentSlot, objectInHand);
            }
            else if (isServer)
            {
                // Если мы сервер, напрямую обновляем состояние
                if (inventoryManager != null)
                {
                    inventoryManager.CmdSyncSlotState(currentSlot, objectInHand);
                    inventoryManager.CmdShowObjectInSlot(currentSlot);
                }
            }
        }
        
        private bool IsValidSlotIndex(int slotIndex)
        {
            return slotIndex >= 0 && slotIndex < inventorySlots.Count;
        }
        
        /// <summary>
        /// Команда для изменения текущего слота на сервере.
        /// Вызывается с клиента, выполняется на сервере.
        /// Обеспечивает централизованное управление инвентарём:
        /// 1. Устанавливает текущий слот и объект в руке.
        /// 2. Синхронизирует состояние слота для всех клиентов через InventoryManager.
        /// 3. Показывает объект в новом слоте.
        /// </summary>
        /// <param name="slotIndex">Индекс слота, который нужно сделать текущим.</param>
        /// <param name="obj">Объект, который будет в руке после смены слота.</param>
        [Command]
        private void CmdChangeCurrentSlotOnServer(int slotIndex, GameObject obj)
        {
            currentSlot = slotIndex;
            objectInHand = obj;
    
            // Синхронизируем состояние слота для всех клиентов
            inventoryManager.CmdSyncSlotState(slotIndex, obj);
            inventoryManager.CmdShowObjectInSlot(slotIndex);
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

        public override void OnStartLocalPlayer()
        {
            base.OnStartLocalPlayer();
            StartCoroutine(InitializeInventoryManager());
        }
        
        private IEnumerator InitializeInventoryManager()
        {
            yield return new WaitUntil(() => NetworkClient.isConnected && NetworkClient.ready);

            if (inventoryManager == null)
            {
                Debug.Log($"[CharacterInventory.InitializeInventoryManager] Создаём InventoryManager для локального игрока {netId}");

                GameObject managerGO = new GameObject($"InventoryManager_{netId}");
                managerGO.transform.SetParent(transform);
                managerGO.AddComponent<NetworkIdentity>();
                inventoryManager = managerGO.AddComponent<InventoryManager>();
                inventoryManager.SetInventorySlots(inventorySlots.ToArray());
                
                // Синхронизируем создание менеджера на сервере
                if (isServer && connectionToClient != null)
                {
                    NetworkServer.Spawn(managerGO,  connectionToClient);
                    Debug.Log($"[CharacterInventory.InitializeInventoryManager] InventoryManager создан с правами для клиента {connectionToClient.connectionId}");
                }
                
                Debug.Log($"[CharacterInventory.InitializeInventoryManager] InventoryManager успешно создан и инициализирован");
            }
            else
            {
                if (inventoryManager != null)
                    Debug.Log("[CharacterInventory.InitializeInventoryManager] InventoryManager уже существует");
                else
                    Debug.Log($"[CharacterInventory.InitializeInventoryManager] Не создан: isLocalPlayer = {isLocalPlayer}");
            }
        }


        #region UnityMethods

        private void Awake()
        {
            Debug.Log($"[CharacterInventory.Awake] Начало инициализации для игрока {netId}. isLocalPlayer: {isLocalPlayer}");
        }
        
        private void Start()
        {
            StartCoroutine(InitializeInventoryManager());
            if (inventorySlots.Count > 0)
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