using System;
using Mirror;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

namespace MyAssets.scripts.Kotenkoff.Character
{
    /// <summary>
    /// Класс, отвечающий за передвижения персонажа.
    /// </summary>
    [RequireComponent(typeof(CharacterController)), RequireComponent(typeof(NetworkIdentity))]
    public class CharacterMovement : NetworkBehaviour
    {
        [Header("Параметры Передвижения:")]
        private float MaxSpeed => sprintInput ? sprintSpeed : walkSpeed;
        [SerializeField, Tooltip("Ускорение.")] private float acceleration = 15f;
        
        [SerializeField, Tooltip("Скорость ходьбы.")]
        private float walkSpeed;

        [SerializeField, Tooltip("Скорость бега.")]
        private float sprintSpeed;
        
        [SerializeField, Tooltip("Насколько высоко сможет прыгнуть игрок."), Space(3)]
        private float jumpHeight;

        private bool Sprinting => sprintInput && CurrentSpeed >0.1f;


        [SerializeField, Header("Параметры Обзора:"), Tooltip("Чувствительность обзора.")]
        private Vector2 lookSensitivity = new Vector2(0.1f, 0.1f);
        
        [SerializeField, Tooltip("Ограничение угла вертикального обзора.")]
        private float pitchLimit = 85f;
        [SerializeField, ReadOnly, Tooltip("Текущий угол вертикального обзора.")]
        private float currentPitch;
        
        [SerializeField, Tooltip("Может ли игрок передвигаться?"), Space(5)] private bool canMove = true;
        /// <summary> Может ли игрок передвигаться?/// </summary>
        public bool CanMove
        {
            get => canMove1;
            set => canMove1 = value;
        }

        [SerializeField, Tooltip("Может ли игрок смотреть")] private bool canLook = true;
        /// <summary>Может ли игрок смотреть?</summary>
        public bool CanLook
        {
            get => canLook;
            set => canLook = value;
        }
        
        [SerializeField, Tooltip("Может ли игрок прыгать?")] private bool canJump = true;
        /// <summary>Может ли игрок прыгать?</summary>
        public bool CanJump
        {
            get => canJump;
            set => canJump = value;
        }

        private float CurrentPitch
        {
            get => currentPitch;

            set => currentPitch = Mathf.Clamp(value, -pitchLimit, pitchLimit);
        }
        
        
        [Header("Параметры Камеры:")]
        
        [SerializeField, Tooltip("FOV камеры при ходьбе.")] float cameraNormalFov = 60f;
        [SerializeField, Tooltip("FOV камеры при беге.")] float cameraSprintFov = 80f;
        [SerializeField, Tooltip("Мягкость изменения FOV.")] float cameraFovSmoothing = 1f;


        [Header("Параметры Физики:")]
        
        [SerializeField, Tooltip("Гравитация.")]
        private float gravityScale = 3f;
        
        [SerializeField, Tooltip("Вертикальная скорость."), ReadOnly]
        private float verticalVelocity;

        private Vector3 CurrentVelocity { get; set; }
        private float CurrentSpeed { get; set; }

        [SerializeField, Tooltip("Прыгнул ли игрок."), ReadOnly, Space(3)]
        private bool wasJumped;
        private bool IsGrounded => characterController.isGrounded;
        
        [Header("Ввод:  (Read Only)")]
        
        [Tooltip("Input передвижения."), ReadOnly]
        public Vector2 moveInput;
        [Tooltip("Input обзора."), ReadOnly]
        public Vector2 lookInput;
        [Tooltip("Бежит ли игрок?"), ReadOnly]
        public bool sprintInput;
        
        [Header("Компоненты:")]
        
        [SerializeField, Tooltip("Ссылка на компонент 'CharacterController'.'")]
        private CharacterController characterController;
        [SerializeField, Tooltip("Ссылка на компонент 'CharacterBase'")]
        private CharacterBase characterBase;

        [SerializeField] private bool canMove1;


        #region Unity Methods

        private void Start()
        {
            if (characterController == null) characterController = gameObject.GetComponent<CharacterController>();
            
            if (characterBase == null) characterBase = gameObject.GetComponent<CharacterBase>();
            
            ExceptionsOnStart();
        }

        private void Update()
        {
            if (!isLocalPlayer) return;
            
            if (canMove) MoveUpdate();
            if (canLook) LookUpdate();
            if (canLook) CameraUpdate();
        }
        
        #endregion

        #region Controller Methods

        /// <summary>
        /// Попытка прыжка персонажа.
        /// </summary>
        public void TryJump()
        {
            if (!wasJumped)
            {
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * Physics.gravity.y * gravityScale);
                wasJumped = true;
            }
        }

        private void MoveUpdate()
        {
            Vector3 motion = transform.forward * moveInput.y + transform.right * moveInput.x;
            motion.y = 0f;
            motion.Normalize();

            if (motion.sqrMagnitude > 0.01f)
            {
                CurrentVelocity = Vector3.MoveTowards(CurrentVelocity, motion * MaxSpeed,
                    Time.deltaTime * acceleration);
            }
            else
            {
                CurrentVelocity = Vector3.MoveTowards(CurrentVelocity, Vector3.zero,
                    acceleration * Time.deltaTime);
            }
            
            if (IsGrounded && verticalVelocity < 0.01f)
            {
                verticalVelocity = -3f;
            }
            else
            {
                verticalVelocity += Physics.gravity.y * gravityScale * Time.deltaTime;
            }
            
            Vector3 fullVelocity = new Vector3(CurrentVelocity.x, verticalVelocity, CurrentVelocity.z);
            
            CollisionFlags flags = characterController.Move(fullVelocity * Time.deltaTime);

            if ((flags & CollisionFlags.Above) != 0 && verticalVelocity > 0.1f)
            {
                verticalVelocity = 0f;
            }
            
            if (wasJumped && IsGrounded) wasJumped = false;
            
            //Обновляем скорость.
            CurrentSpeed = CurrentVelocity.magnitude;
        }
        
        private void LookUpdate()
        {
            Vector3 input = new Vector2(lookInput.x * lookSensitivity.x, lookInput.y *  lookSensitivity.y);

            // Обзор вверх и вниз
            CurrentPitch -= input.y;
            
            characterBase.FpCamera.transform.localRotation = Quaternion.Euler(CurrentPitch,  0f, 0f);
            
            // Обзор на лево и на право
            transform.Rotate(Vector3.up * input.x);
        }
        
        private void CameraUpdate()
        {
            float targetFov = cameraNormalFov;

            if (Sprinting)
            {
                float speedRatio = CurrentPitch / sprintSpeed;
                
                targetFov = Mathf.Lerp(cameraNormalFov, cameraSprintFov, speedRatio);
            }
            
            characterBase.FpCamera.Lens.FieldOfView = Mathf.Lerp(characterBase.FpCamera.Lens.FieldOfView, targetFov, cameraFovSmoothing * Time.deltaTime);
        }

        private void ExceptionsOnStart()
        {
            if (walkSpeed <= 0) Debug.LogError($"[CharacterMovement] Скорость ходьбы игрока ({netId}) меньше или равняется нулю!");
            
            if (sprintSpeed <= 0) Debug.LogError($"[CharacterMovement] Скорость бега игрока ({netId}) меньше или равняется нулю!");
        }
        
        #endregion
    }
}