using Kotenkoff;
using UnityEngine;
using Mirror;
using UnityEngine.InputSystem;

/// <summary>
/// Отвечает за локальное передвижение игрока, управление камерой, посадку в авто, взаимодействие и синхронизацию анимаций.
/// </summary>
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(NetworkAnimator))]
public class PlayerEntity : NetworkBehaviour
{
    [Header("Movement & Look Settings")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float lookSensitivity = 0.5f; 
    [SerializeField] private float interactRange = 3f;
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private LayerMask interactLayerMask = ~0;

    [Header("Input Actions")]
    [SerializeField] private InputActionReference moveAction;
    [SerializeField] private InputActionReference lookAction;
    [SerializeField] private InputActionReference interactAction;

    [Header("Vehicle State")]
    private CarSeat currentSeat;
    private bool isSitting = false;

    private CharacterController characterController;
    
    // Переменные для вращения головы
    private float xRotation = 0f; 
    private float yRotation = 0f; 
    
    [SyncVar] public NetworkIdentity heldItem;
    
    [Header("Inventory")]
    [SerializeField] private PlayerInventory inventory;

    [Header("Visuals & Animation")]
    [SerializeField] private Animator animator; 
    private NetworkAnimator networkAnimator;

    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int IsSittingHash = Animator.StringToHash("IsSitting");
    private static readonly int InteractTriggerHash = Animator.StringToHash("Interact");
    private static readonly int SitTriggerHash = Animator.StringToHash("Sit");
    private static readonly int StandTriggerHash = Animator.StringToHash("Stand");

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        networkAnimator = GetComponent<NetworkAnimator>();
        if (animator == null) animator = GetComponentInChildren<Animator>(); 

        if (cameraTransform != null)
        {
            Camera cam = cameraTransform.GetComponent<Camera>();
            if (cam != null) cam.enabled = false;

            AudioListener listener = cameraTransform.GetComponent<AudioListener>();
            if (listener != null) listener.enabled = false;
        }
    }

    public override void OnStartLocalPlayer()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (cameraTransform != null)
        {
            Camera cam = cameraTransform.GetComponent<Camera>();
            if (cam != null) cam.enabled = true;

            AudioListener listener = cameraTransform.GetComponent<AudioListener>();
            if (listener != null) listener.enabled = true;
        }

        if (moveAction != null) moveAction.action.Enable();
        if (lookAction != null) lookAction.action.Enable();
        
        if (interactAction != null)
        {
            interactAction.action.Enable();
            interactAction.action.performed += OnInteractPerformed;
        }
    }

    public override void OnStopLocalPlayer()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (moveAction != null) moveAction.action.Disable();
        if (lookAction != null) lookAction.action.Disable();
        
        if (interactAction != null)
        {
            interactAction.action.performed -= OnInteractPerformed;
            interactAction.action.Disable();
        }
    }

    private void Update()
    {
        if (!isLocalPlayer) return;

        if (isSitting)
        {
            HandleLook();
            HandleDriving();
            
            if (UnityEngine.InputSystem.Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                CmdLeaveSeat();
            }
            return; 
        }

        HandleLook();
        HandleMovement();
    }

    private void HandleDriving()
    {
        if (currentSeat != null && currentSeat.isDriverSeat && currentSeat.carSystem != null)
        {
            Vector2 inputDir = moveAction.action.ReadValue<Vector2>();
            currentSeat.carSystem.LocalDrive(inputDir.x, inputDir.y);
        }
    }

    private void HandleLook()
    {
        if (lookAction == null || cameraTransform == null) return;

        Vector2 lookInput = lookAction.action.ReadValue<Vector2>();
        float mouseX = lookInput.x * lookSensitivity;
        float mouseY = lookInput.y * lookSensitivity;

        // Вверх/Вниз (одинаково для пешехода и водителя)
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        if (isSitting)
        {
            // --- РЕЖИМ ВОДИТЕЛЯ ---
            // Крутим ТОЛЬКО камеру (голову) влево/вправо. Тело неподвижно.
            yRotation += mouseX;
            yRotation = Mathf.Clamp(yRotation, -110f, 110f); // Ограничитель шеи
            
            cameraTransform.localRotation = Quaternion.Euler(xRotation, yRotation, 0f);
        }
        else
        {
            // --- РЕЖИМ ПЕШЕХОДА ---
            // Горизонтальное вращение сбрасывается для головы, крутим всё тело.
            yRotation = 0f;
            cameraTransform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
            transform.Rotate(Vector3.up * mouseX);
        }
    }

    private void HandleMovement()
    {
        if (moveAction == null) return;

        Vector2 inputDir = moveAction.action.ReadValue<Vector2>();
        Vector3 move = transform.right * inputDir.x + transform.forward * inputDir.y;
        characterController.Move(move * moveSpeed * Time.deltaTime);

        if (animator != null)
        {
            float horizontalSpeed = new Vector3(characterController.velocity.x, 0, characterController.velocity.z).magnitude;
            animator.SetFloat(SpeedHash, horizontalSpeed, 0.1f, Time.deltaTime);
        }
    }

    private void OnInteractPerformed(InputAction.CallbackContext context)
    {
        if (isSitting || cameraTransform == null) return;

        if (networkAnimator != null)
        {
            animator.SetTrigger(InteractTriggerHash);
        }

        if (Physics.Raycast(cameraTransform.position, cameraTransform.forward, out RaycastHit hit, interactRange, interactLayerMask))
        {
            NetworkIdentity rootIdentity = hit.collider.GetComponentInParent<NetworkIdentity>();
            IInteractable interactable = hit.collider.GetComponentInParent<IInteractable>();

            if (rootIdentity != null && interactable != null)
            {
                string targetName = ((Component)interactable).gameObject.name;
                CmdInteract(rootIdentity, targetName);
            }
        }
    }

    [Command]
    private void CmdInteract(NetworkIdentity rootIdentity, string targetName)
    {
        if (rootIdentity == null || string.IsNullOrEmpty(targetName)) return;

        Transform[] allChildren = rootIdentity.GetComponentsInChildren<Transform>();
        
        foreach (Transform child in allChildren)
        {
            if (child.name == targetName)
            {
                if (child.TryGetComponent(out IInteractable interactable))
                {
                    interactable.ServerInteract(this, inventory);
                    return;
                }
            }
        }
    }

    [Command]
    private void CmdLeaveSeat()
    {
        if (currentSeat != null)
        {
            currentSeat.ServerLeave(this);
        }
    }

    [TargetRpc]
    public void TargetEnterSeat(NetworkIdentity carNetId, string seatPath)
    {
        GameObject seatObj = GameObject.Find(seatPath);
        if (seatObj == null) return;
        
        currentSeat = seatObj.GetComponent<CarSeat>();
        if (currentSeat == null) return;

        characterController.enabled = false; 
        isSitting = true; 

        if (animator != null)
        {
            animator.SetBool(IsSittingHash, true);
            animator.SetTrigger(SitTriggerHash);
        }

        // Мы сажаем только ИГРОКА. Камера поедет за ним сама, так как она его ребенок!
        Transform targetTransform = currentSeat.viewPoint != null ? currentSeat.viewPoint : currentSeat.transform;

        transform.SetParent(targetTransform);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;

        // Сбрасываем взгляд прямо перед собой
        xRotation = 0f;
        yRotation = 0f;
        cameraTransform.localRotation = Quaternion.identity;
    }

    [TargetRpc]
    public void TargetLeaveSeat()
    {
        isSitting = false;
        
        if (animator != null)
        {
            animator.SetBool(IsSittingHash, false);
            animator.SetTrigger(StandTriggerHash);
        }

        // Выходим из машины: отвязываем игрока
        transform.SetParent(null);
        
        // Гарантируем, что игрок стоит ровно, а не завален набок из-за крена машины
        transform.rotation = Quaternion.Euler(0, transform.eulerAngles.y, 0);

        // Хак высадки: чуть вбок
        transform.position += transform.right * 1.5f; 

        // Сбрасываем шею и взгляд
        xRotation = 0f;
        yRotation = 0f;
        cameraTransform.localRotation = Quaternion.identity;
        
        currentSeat = null;
        characterController.enabled = true;
    }

    [Command]
    public void CmdFixBreakdown(NetworkIdentity carIdentity)
    {
        if (carIdentity != null && carIdentity.TryGetComponent(out BreakdownManager breakdownManager))
        {
            breakdownManager.RepairBreakdown();
        }
    }
}