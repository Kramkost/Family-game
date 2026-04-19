using System;
using Mirror;
using MyAssets.scripts.Kotenkoff.Character.Interfaces;
using MyAssets.scripts.Kotenkoff.Character.Inventory;
using MyAssets.scripts.Kotenkoff.General;
using NaughtyAttributes;
using UnityEngine;

namespace MyAssets.scripts.Kotenkoff.Vehicle
{
    /// <summary>
    /// Класс для объектов транспорта типа двери (Дверцы, шкафы и т.д.)
    /// </summary>
    [SelectionBase, AddComponentMenu("Kotenkoff/Vehicle/Car Door")]
    public class VehicleDoor : NetworkBehaviour, IInteractableTest
    {
        [SerializeField, Tooltip("Открыта ли дверь?"), SyncVar(hook = nameof(OnStateChanged))] private bool isOpen;
        /// <summary>Открыта ли дверь?</summary> 
        public bool IsOpen => isOpen;
        
        [SerializeField, Tooltip("Использовать анимацию?")] private bool useAnimation;
        [SerializeField, Tooltip("Скорость анимации.")] private float animationSpeed;
        [SerializeField, Tooltip("Тип анимации.")] private Enums.AnimationType animationType;
        
        [SerializeField] private Vector3 openRotation;
        [SerializeField] private Vector3 closedRotation;
        
        [SerializeField] private Vector3 openPosition;
        [SerializeField] private Vector3 closedPosition;

        [Server]
        public void TryInteract(CharacterInventory inventory)
        {
            ChangeState();
        }
        
        private void ChangeState()
        {
            isOpen = !isOpen;
        }

        private void OnStateChanged(bool oldState, bool newState)
        {
            if (oldState == newState) return;
        }

        private void ApplyState()
        {
            if (!useAnimation) return;
            if (animationType is Enums.AnimationType.RotateOnly or Enums.AnimationType.Both)
                transform.localRotation = Quaternion.Euler(isOpen ? openRotation : closedRotation);
            if (animationType is Enums.AnimationType.MoveOnly or Enums.AnimationType.Both)
                transform.localPosition = isOpen ? openPosition : closedPosition;
        }


        #region Unity Methods

        private void Start() => ApplyState();

        private void Update()
        {
            var dt = Time.deltaTime * animationSpeed;

            if (animationType is Enums.AnimationType.RotateOnly or Enums.AnimationType.Both)
            {
                var targetRot = Quaternion.Euler(isOpen ? openRotation : closedRotation);

                transform.localRotation = useAnimation ? Quaternion.Lerp(transform.localRotation, targetRot, dt) : targetRot;
            }

            if (animationType is Enums.AnimationType.MoveOnly or Enums.AnimationType.Both)
            {
                var targetPos = isOpen ? openPosition : closedPosition;

                transform.localPosition = useAnimation ? Vector3.Lerp(transform.localPosition, targetPos, dt) : targetPos;
            }
        }
        
        #endregion
    }
}