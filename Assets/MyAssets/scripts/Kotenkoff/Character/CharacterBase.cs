using System;
using Mirror;
using MyAssets.scripts.Kotenkoff.Character.Inventory;
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
        
        [Header("Компоненты:")]
        [SerializeField, Tooltip("Ссылка на компонент 'CharacterMovement'"), FormerlySerializedAs("newCharacterController")]
        private CharacterMovement characterMovement;
        [SerializeField, Tooltip("Ссылка на компонент 'CharacterInventory'")]
        private CharacterInventory characterInventory;
        [SerializeField, Tooltip("Ссылка на компонент 'CharacterInteract'")]
        private  CharacterInteract characterInteract;

        
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
                characterMovement.TryJump();
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
        
        #endregion
        
        #region Unity Methods

        public override void OnStartLocalPlayer()
        {
            if (isLocalPlayer)
            {
                if (characterMovement == null) characterMovement = gameObject.GetComponent<CharacterMovement>();
                if (characterInventory == null) characterInventory = gameObject.GetComponent<CharacterInventory>();
                if (characterInteract == null) characterInteract = gameObject.GetComponent<CharacterInteract>();
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
    }
}