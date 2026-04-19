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
    /// Основная логика игрока.
    /// </summary>
    [RequireComponent(typeof(CharacterMovement)), RequireComponent(typeof(NetworkIdentity)),
    RequireComponent(typeof(CharacterInventory)), RequireComponent(typeof(CharacterInteract)),
    RequireComponent(typeof(CharacterCam))]
    public class CharacterBase : NetworkBehaviour
    {
        [SerializeField] private VehicleSeat currentSeat;
        [SerializeField] private bool isSeat;
        
        [Header("Компоненты:")]
        [SerializeField, Tooltip("Ссылка на компонент 'CharacterMovement'"), FormerlySerializedAs("newCharacterController")]
        private CharacterMovement characterMovement;
        [SerializeField, Tooltip("Ссылка на компонент 'CharacterInventory'.")]
        private CharacterInventory characterInventory;
        [SerializeField, Tooltip("Ссылка на компонент 'CharacterInteract'.")]
        private  CharacterInteract characterInteract;
        [SerializeField, Tooltip("Ссылка на компонент 'PlayerInput'.")]
        private PlayerInput playerInput;
        [SerializeField, Tooltip("Ссылка на компонент 'CharacterController'.")]
        private CharacterController characterController;
        
        public CinemachineCamera FpCamera { get; set; }
        
        
        #region InputHandling

        private void OnMove(InputValue value)
        {
            if (!isLocalPlayer) return;
            characterMovement.moveInput = value.Get<Vector2>();
        }

        private void OnLook(InputValue value)
        {
            if (!isLocalPlayer) return;
            characterMovement.lookInput = value.Get<Vector2>();
        }

        private void OnSprint(InputValue value)
        {
            if (!isLocalPlayer) return;
            characterMovement.sprintInput = value.isPressed;
        }

        private void OnJump(InputValue value)
        {
            if (value.isPressed)
            {
                if (!isLocalPlayer) return;
                
                if (!isSeat) characterMovement.TryJump();
                else ExitSeat();
            }
        }
        
        private void OnInteract(InputValue value)
        {
            if (value.isPressed)
            {
                if (isLocalPlayer)
                {
                    characterInteract.TryInteract(); 
                }
            }
        }

        private void OnNext(InputValue value)
        {
            if (!value.isPressed) return;
            if (!isLocalPlayer) return;
            if (!isOwned) return;
            
            characterInventory.ChangeCurrentSlotWithVector(ChangeSlotVector.Forward);
        }

        private void OnPrevious(InputValue value)
        {
            if (!value.isPressed) return;
            if (!isLocalPlayer) return;
            if (!isOwned) return;
            
            characterInventory.ChangeCurrentSlotWithVector(ChangeSlotVector.Backward);
        }

        private void OnMovement(InputValue value)
        {
            if (currentSeat == null) return;
            
            currentSeat.VehicleBase.VehicleMovement.moveInput = value.Get<Vector2>();
        }

        private void OnBreak(InputValue value)
        {
            if (currentSeat == null) return;

            currentSeat.VehicleBase.VehicleMovement.isBreak = !currentSeat.VehicleBase.VehicleMovement.isBreak;
        }
        
        #endregion
        
        #region Unity Methods

        public override void OnStartLocalPlayer()
        {
            if (isLocalPlayer)
            {
                if (characterMovement == null) characterMovement = gameObject.GetComponent<CharacterMovement>();
                if (characterInventory == null) characterInventory = gameObject.GetComponent<CharacterInventory>();
                if (characterInteract == null) characterInteract = gameObject.GetComponent<CharacterInteract>();
                if (playerInput == null) playerInput = GetComponent<PlayerInput>();
                if (characterController == null) characterController = GetComponent<CharacterController>();
            }
            
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }

        public override void OnStopLocalPlayer()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible   = true;
        }
        
        #endregion

        #region Vehicle
        
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
        }

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
        }
        
        private void EnableDriveInput() => playerInput.SwitchCurrentActionMap("Vehicle");
        
        private void DisableDriveInput() => playerInput.SwitchCurrentActionMap("Gameplay");
        
        #endregion

        
    }
}