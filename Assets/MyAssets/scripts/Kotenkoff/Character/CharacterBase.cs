using System;
using Mirror;
using MyAssets.scripts.Kotenkoff.Character.Inventory;
using MyAssets.scripts.Kotenkoff.Vehicle.Vehicle_Parts;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace MyAssets.scripts.Kotenkoff.Character
{
    /// <summary>
    /// Основной класс персонажа. <br/>
    /// Управляет движением, инвентарём, взаимодействием, вводом и посадкой в транспорт. <br/>
    /// Работает только на локальном игроке. <br/>
    /// Использует Input System для обработки ввода.
    /// </summary>
    [RequireComponent(typeof(CharacterMovement)), 
     RequireComponent(typeof(NetworkIdentity)),
     RequireComponent(typeof(CharacterInventory)), 
     RequireComponent(typeof(CharacterInteract)),
     RequireComponent(typeof(CharacterCam))]
    public class CharacterBase : NetworkBehaviour
    {
        /// <summary>
        /// Текущее сиденье, на котором находится игрок. <br/>
        /// Используется для управления транспортом.
        /// </summary>
        [SerializeField] private VehicleSeat currentSeat;

        /// <summary>
        /// Флаг: находится ли игрок в сиденье. <br/>
        /// Используется для блокировки ходьбы и прыжков.
        /// </summary>
        [SerializeField] private bool isSeat;

        [Header("Компоненты:")]
        
        /// <summary>
        /// Ссылка на компонент движения персонажа. <br/>
        /// Управляет перемещением, прыжками, поворотом.
        /// </summary>
        [SerializeField, Tooltip("Ссылка на компонент 'CharacterMovement'"), FormerlySerializedAs("newCharacterController")]
        private CharacterMovement characterMovement;

        /// <summary>
        /// Ссылка на инвентарь персонажа. <br/>
        /// Управляет слотами, текущим предметом, переключением.
        /// </summary>
        [SerializeField, Tooltip("Ссылка на компонент 'CharacterInventory'.")]
        private CharacterInventory characterInventory;

        /// <summary>
        /// Ссылка на систему взаимодействия. <br/>
        /// Отвечает за подбор предметов по нажатию E.
        /// </summary>
        [SerializeField, Tooltip("Ссылка на компонент 'CharacterInteract'.")]
        private CharacterInteract characterInteract;

        /// <summary>
        /// Ссылка на систему ввода (Input System). <br/>
        /// Используется для переключения между картами действий (игрок / транспорт).
        /// </summary>
        [SerializeField, Tooltip("Ссылка на компонент 'PlayerInput'.")]
        private PlayerInput playerInput;

        /// <summary>
        /// Ссылка на CharacterController. <br/>
        /// Используется для включения/отключения при посадке.
        /// </summary>
        [SerializeField, Tooltip("Ссылка на компонент 'CharacterController'.")]
        private CharacterController characterController;

        /// <summary>
        /// Ссылка на основную камеру персонажа (Cinemachine). <br/>
        /// Используется для UI, взаимодействия, поворота.
        /// </summary>
        public CinemachineCamera FpCamera { get; set; }

        #region InputHandling

        /// <summary>
        /// Вызывается при изменении ввода движения (WASD). <br/>
        /// Передаёт вектор в <see cref="CharacterMovement.moveInput"/>.
        /// </summary>
        /// <param name="value">Ввод движения в виде Vector2 (x — боковое, y — вперёд/назад).</param>
        private void OnMove(InputValue value)
        {
            if (!isLocalPlayer) return;
            characterMovement.moveInput = value.Get<Vector2>();
            Debug.Log($"[CharacterBase.OnMove] Движение: {characterMovement.moveInput}");
        }

        /// <summary>
        /// Вызывается при изменении ввода взгляда (мышка). <br/>
        /// Передаёт вектор в <see cref="CharacterMovement.lookInput"/>.
        /// </summary>
        /// <param name="value">Ввод взгляда в виде Vector2 (x — поворот, y — наклон).</param>
        private void OnLook(InputValue value)
        {
            if (!isLocalPlayer) return;
            characterMovement.lookInput = value.Get<Vector2>();
            Debug.Log($"[CharacterBase.OnLook] Взгляд: {characterMovement.lookInput}");
        }

        /// <summary>
        /// Вызывается при нажатии/отпускании клавиши спринта (Shift). <br/>
        /// Передаёт состояние в <see cref="CharacterMovement.sprintInput"/>.
        /// </summary>
        /// <param name="value">Состояние клавиши (нажата/отпущена).</param>
        private void OnSprint(InputValue value)
        {
            if (!isLocalPlayer) return;
            characterMovement.sprintInput = value.isPressed;
            Debug.Log($"[CharacterBase.OnSprint] Спринт: {characterMovement.sprintInput}");
        }

        /// <summary>
        /// Вызывается при нажатии клавиши прыжка (Пробел). <br/>
        /// Если игрок не в транспорте — прыгает. <br/>
        /// Если в транспорте — выходит из сиденья.
        /// </summary>
        /// <param name="value">Состояние клавиши.</param>
        private void OnJump(InputValue value)
        {
            if (value.isPressed)
            {
                if (!isLocalPlayer) return;

                if (!isSeat)
                {
                    characterMovement.TryJump();
                    Debug.Log("[CharacterBase.OnJump] Прыжок");
                }
                else
                {
                    ExitSeat();
                    Debug.Log("[CharacterBase.OnJump] Выход из транспорта");
                }
            }
        }

        /// <summary>
        /// Вызывается при нажатии клавиши взаимодействия (E). <br/>
        /// Запускает попытку подбора предмета через <see cref="CharacterInteract.TryInteract"/>.
        /// </summary>
        /// <param name="value">Состояние клавиши.</param>
        private void OnInteract(InputValue value)
        {
            if (value.isPressed)
            {
                if (isLocalPlayer)
                {
                    characterInteract.TryInteract();
                    Debug.Log("[CharacterBase.OnInteract] Запрос взаимодействия (E)");
                }
            }
        }

        /// <summary>
        /// Вызывается при нажатии кнопки "Следующий слот" (например, Mouse Wheel Up). <br/>
        /// Переключается на следующий непустой слот по кругу.
        /// </summary>
        /// <param name="value">Состояние ввода.</param>
        private void OnNext(InputValue value)
        {
            if (!value.isPressed || !isLocalPlayer || !isOwned) return;
            ChangeCurrentSlot(1);
        }

        /// <summary>
        /// Вызывается при нажатии кнопки "Предыдущий слот" (например, Mouse Wheel Down). <br/>
        /// Переключается на предыдущий непустой слот по кругу.
        /// </summary>
        /// <param name="value">Состояние ввода.</param>
        private void OnPrevious(InputValue value)
        {
            if (!value.isPressed || !isLocalPlayer || !isOwned) return;
            ChangeCurrentSlot(-1);
        }

        /// <summary>
        /// Обрабатывает скролл колёсиком мыши для переключения слотов. <br/>
        /// Положительное значение — вперёд, отрицательное — назад.
        /// </summary>
        /// <param name="value">Ввод колёсика (обычно от -1 до 1).</param>
        private void OnMouseScroll(InputValue value)
        {
            float scroll = value.Get<float>();
            if (scroll == 0f || !isLocalPlayer || !isOwned) return;

            Debug.Log($"[CharacterBase.OnMouseScroll] Скролл: {scroll}");
            int direction = scroll > 0f ? 1 : -1;
            ChangeCurrentSlot(direction);
        }

        /// <summary>
        /// Переключает текущий слот в указанном направлении. <br/>
        /// Ищет ближайший непустой слот по кругу. <br/>
        /// Если все слоты пусты — не меняет.
        /// </summary>
        /// <param name="direction">+1 — вперёд, -1 — назад.</param>
        private void ChangeCurrentSlot(int direction)
        {
            string directionName = direction > 0 ? "вперёд" : "назад";
            Debug.Log($"[CharacterBase.ChangeCurrentSlot] Переключение {directionName}");

            int slotCount = characterInventory.slots.Count;
            if (slotCount == 0)
            {
                Debug.LogWarning("[CharacterBase.ChangeCurrentSlot] Нет слотов в инвентаре");
                return;
            }

            int current = characterInventory.currentSlot;
            int newSlot = current;

            do
            {
                newSlot = (newSlot + direction + slotCount) % slotCount;
            } while (newSlot != current && characterInventory.slots[newSlot].isEmpty);

            if (newSlot != current && !characterInventory.slots[newSlot].isEmpty)
            {
                Debug.Log($"[CharacterBase.ChangeCurrentSlot] Слот изменён: {newSlot}");
                characterInventory.ChangeCurrentSlot(newSlot);
            }
            else
            {
                Debug.Log("[CharacterBase.ChangeCurrentSlot] Все слоты пусты — переключение невозможно");
            }
        }

        /// <summary>
        /// Вызывается при изменении ввода движения в транспорте (WASD). <br/>
        /// Передаёт вектор в <see cref="VehicleMovement.moveInput"/>.
        /// </summary>
        /// <param name="value">Ввод движения в виде Vector2.</param>
        private void OnMovement(InputValue value)
        {
            if (currentSeat == null) return;
            currentSeat.VehicleBase.VehicleMovement.moveInput = value.Get<Vector2>();
            Debug.Log($"[CharacterBase.OnMovement] Управление транспортом: {currentSeat.VehicleBase.VehicleMovement.moveInput}");
        }

        /// <summary>
        /// Вызывается при нажатии клавиши тормоза (например, Space в транспорте). <br/>
        /// Переключает состояние тормоза.
        /// </summary>
        /// <param name="value">Состояние клавиши.</param>
        private void OnBreak(InputValue value)
        {
            if (currentSeat == null) return;
            currentSeat.VehicleBase.VehicleMovement.isBreak = !currentSeat.VehicleBase.VehicleMovement.isBreak;
            Debug.Log($"[CharacterBase.OnBreak] Тормоз: {currentSeat.VehicleBase.VehicleMovement.isBreak}");
        }

        #endregion

        #region Unity Methods

        /// <summary>
        /// Вызывается при подключении локального игрока. <br/>
        /// Инициализирует компоненты и блокирует курсор.
        /// </summary>
        public override void OnStartLocalPlayer()
        {
            if (isLocalPlayer)
            {
                characterMovement ??= GetComponent<CharacterMovement>();
                characterInventory ??= GetComponent<CharacterInventory>();
                characterInteract ??= GetComponent<CharacterInteract>();
                playerInput ??= GetComponent<PlayerInput>();
                characterController ??= GetComponent<CharacterController>();

                Debug.Log($"[CharacterBase.OnStartLocalPlayer] Игрок {netId} инициализирован");
            }

            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }

        /// <summary>
        /// Вызывается при отключении локального игрока. <br/>
        /// Разблокирует курсор.
        /// </summary>
        public override void OnStopLocalPlayer()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Debug.Log("[CharacterBase.OnStopLocalPlayer] Курсор разблокирован");
        }

        #endregion

        #region Vehicle

        /// <summary>
        /// Посадка игрока в сиденье транспорта. <br/>
        /// Блокирует движение, отключает CharacterController, меняет родителя.
        /// </summary>
        /// <param name="enterPos">Точка входа в транспорт.</param>
        /// <param name="seat">Сиденье, в которое садится игрок.</param>
        public void EnterSeat(Transform enterPos, VehicleSeat seat)
        {
            characterMovement.CanMove = false;
            characterMovement.CanJump = false;

            transform.position = enterPos.position;
            characterController.enabled = false;

            transform.parent = seat.transform;
            currentSeat = seat;
            isSeat = true;

            EnableDriveInput();
            Debug.Log($"[CharacterBase.EnterSeat] Игрок сел в транспорт: {seat.name}");
        }

        /// <summary>
        /// Выход из транспорта. <br/>
        /// Восстанавливает движение, включает CharacterController, отключает родителя.
        /// </summary>
        private void ExitSeat()
        {
            if (currentSeat == null) return;

            transform.parent = null;
            transform.position = currentSeat.ExitPosition.position;
            characterController.enabled = true;

            characterMovement.CanMove = true;
            characterMovement.CanJump = true;

            currentSeat.TryInteract(characterInventory);
            currentSeat = null;
            isSeat = false;

            DisableDriveInput();
            Debug.Log("[CharacterBase.ExitSeat] Игрок вышел из транспорта");
        }

        /// <summary>
        /// Переключает карту действий на "Vehicle". <br/>
        /// Активирует управление транспортом.
        /// </summary>
        private void EnableDriveInput() 
        {
            playerInput.SwitchCurrentActionMap("Vehicle");
            Debug.Log("[CharacterBase.EnableDriveInput] Переключено на управление транспортом");
        }

        /// <summary>
        /// Переключает карту действий на "Gameplay". <br/>
        /// Активирует управление персонажем.
        /// </summary>
        private void DisableDriveInput() 
        {
            playerInput.SwitchCurrentActionMap("Gameplay");
            Debug.Log("[CharacterBase.DisableDriveInput] Переключено на управление персонажем");
        }

        #endregion
    }
}
