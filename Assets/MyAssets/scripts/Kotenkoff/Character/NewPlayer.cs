using Mirror;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MyAssets.scripts.Kotenkoff.Character
{
    /// <summary>
    /// Основная логика игрока.
    /// </summary>
    [RequireComponent(typeof(NewCharacterController)), RequireComponent(typeof(NetworkIdentity))]
    public class NewPlayer : NetworkBehaviour
    {
        [Header("Компоненты:")]
        [SerializeField, Tooltip("Ссылка на компонент 'NewCharacterController'")]
        private NewCharacterController newCharacterController;
        
        #region InputHandling

        private void OnMove(InputValue value)
        {
            if (!isLocalPlayer) return;
            newCharacterController.moveInput = value.Get<Vector2>();
        }

        private void OnLook(InputValue value)
        {
            if (!isLocalPlayer) return;
            newCharacterController.lookInput = value.Get<Vector2>();
        }

        private void OnSprint(InputValue value)
        {
            if (!isLocalPlayer) return;
            newCharacterController.sprintInput = value.isPressed;
        }

        private void OnJump(InputValue value)
        {
            if (value.isPressed)
            {
                if (!isLocalPlayer) return;
                newCharacterController.TryJump();
            }
        }
        #endregion
        
        #region Unity Methods

        new void  OnValidate()
        {
            if (newCharacterController == null) newCharacterController = GetComponent<NewCharacterController>();
        }

        public override void OnStartLocalPlayer()
        {
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