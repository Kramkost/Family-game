using Kotenkoff;
using UnityEngine;
using Mirror;
using UnityEngine.InputSystem;

/// <summary>
/// Отвечает за локальное передвижение, управление камерой, посадку в авто, взаимодействие и предметы в руках.
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
    
    [Header("Physics Settings")]
    [SerializeField] private float gravity = -9.81f;
    private float velocityY = 0f;

    [Header("Camera Bobbing")]
    [SerializeField] private float bobbingSpeed = 14f;
    [SerializeField] private float bobbingAmount = 0.05f;
    private float defaultCameraY;
    private float timer = 0f;

    private float lastInteractTime = 0f;

    [Header("Input Actions")]
    [SerializeField] private InputActionReference moveAction;
    [SerializeField] private InputActionReference lookAction;
    [SerializeField] private InputActionReference interactAction;
    [SerializeField] private InputActionReference dropAction; 

    [Header("Hands & Items")]
    [Tooltip("Кость руки. Если пусто, скрипт попытается найти дочерний объект с тегом 'Hand'")]
    [SerializeField] private Transform rightHandSocket;
    
    [SyncVar(hook = nameof(OnHeldItemChanged))] 
    public NetworkIdentity heldItem;

    [Header("Vehicle State")]
    private CarSeat currentSeat;
    private bool isSitting = false;

    private CharacterController characterController;
    private float xRotation = 0f; 
    private float yRotation = 0f; 
    
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

        // Ищем кость руки, если она не задана
        if (rightHandSocket == null)
        {
            foreach (Transform child in GetComponentsInChildren<Transform>(true))
            {
                if (child.CompareTag("Hand"))
                {
                    rightHandSocket = child;
                    break;
                }
            }
        }

        // Отключаем камеру по умолчанию для клонов
        if (cameraTransform != null)
        {
            defaultCameraY = cameraTransform.localPosition.y;
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

        if (dropAction != null)
        {
            dropAction.action.Enable();
            dropAction.action.performed += OnDropPerformed;
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

        if (dropAction != null)
        {
            dropAction.action.performed -= OnDropPerformed;
            dropAction.action.Disable();
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
        ApplyGravity();
        HandleCameraBobbing();
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

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        if (isSitting)
        {
            yRotation += mouseX;
            yRotation = Mathf.Clamp(yRotation, -110f, 110f); 
            cameraTransform.localRotation = Quaternion.Euler(xRotation, yRotation, 0f);
        }
        else
        {
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

    private void ApplyGravity()
    {
        if (characterController.isGrounded) velocityY = -2f;
        else velocityY += gravity * Time.deltaTime;
        characterController.Move(new Vector3(0, velocityY, 0) * Time.deltaTime);
    }

    private void HandleCameraBobbing()
    {
        if (cameraTransform == null) return;

        float speed = new Vector3(characterController.velocity.x, 0, characterController.velocity.z).magnitude;

        if (speed > 0.1f && characterController.isGrounded)
        {
            timer += Time.deltaTime * bobbingSpeed;
            float newY = defaultCameraY + Mathf.Sin(timer) * bobbingAmount;
            cameraTransform.localPosition = new Vector3(cameraTransform.localPosition.x, newY, cameraTransform.localPosition.z);
        }
        else
        {
            timer = 0f;
            float newY = Mathf.Lerp(cameraTransform.localPosition.y, defaultCameraY, Time.deltaTime * bobbingSpeed);
            cameraTransform.localPosition = new Vector3(cameraTransform.localPosition.x, newY, cameraTransform.localPosition.z);
        }
    }

    private void OnInteractPerformed(InputAction.CallbackContext context)
    {
        if (isSitting || cameraTransform == null) return;

        // Анти-спам
        if (Time.time < lastInteractTime + 0.5f) return;
        lastInteractTime = Time.time;

        if (networkAnimator != null) animator.SetTrigger(InteractTriggerHash);

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
            if (child.name == targetName && child.TryGetComponent(out IInteractable interactable))
            {
                interactable.ServerInteract(this, inventory);
                return;
            }
        }
    }

    private void OnDropPerformed(InputAction.CallbackContext context)
    {
        if (isSitting || heldItem == null) return;
        CmdDropItem();
    }

    [Command]
    private void CmdDropItem()
    {
        if (heldItem == null) return;
        
        GameObject itemToDrop = heldItem.gameObject;
        
        if (inventory != null) inventory.RemoveItem(itemToDrop);

        heldItem = null; 

        itemToDrop.transform.position = cameraTransform.position + cameraTransform.forward * 1.5f;
    }

    [Server]
    public void ServerEquipItem(NetworkIdentity item)
    {
        heldItem = item; 
    }

    private void OnHeldItemChanged(NetworkIdentity oldItem, NetworkIdentity newItem)
    {
        if (oldItem != null)
        {
            oldItem.transform.SetParent(null);
            
            if (oldItem.TryGetComponent(out Rigidbody rb)) rb.isKinematic = false;
            foreach (var col in oldItem.GetComponents<Collider>()) col.enabled = true;
        }

        if (newItem != null && rightHandSocket != null)
        {
            newItem.transform.SetParent(rightHandSocket);
            newItem.transform.localPosition = Vector3.zero;
            newItem.transform.localRotation = Quaternion.identity; 

            if (newItem.TryGetComponent(out Rigidbody rb)) rb.isKinematic = true;
            foreach (var col in newItem.GetComponents<Collider>()) col.enabled = false;
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

        Transform targetTransform = currentSeat.viewPoint != null ? currentSeat.viewPoint : currentSeat.transform;

        transform.SetParent(targetTransform);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;

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

        transform.SetParent(null);
        transform.rotation = Quaternion.Euler(0, transform.eulerAngles.y, 0);

        if (currentSeat != null && currentSeat.exitPoint != null)
        {
            transform.position = currentSeat.exitPoint.position;
        }
        else
        {
            transform.position += transform.right * 1.5f; 
        }

        xRotation = 0f;
        yRotation = 0f;
        
        cameraTransform.localRotation = Quaternion.identity;
        cameraTransform.localPosition = new Vector3(cameraTransform.localPosition.x, defaultCameraY, cameraTransform.localPosition.z);
        
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