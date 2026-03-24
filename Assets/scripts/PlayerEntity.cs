using Kotenkoff;
using UnityEngine;
using Mirror;
using UnityEngine.InputSystem;

/// <summary>
/// Отвечает за локальное передвижение, прыжок, управление камерой, посадку в авто, взаимодействие, предметы и ЗВУКИ ШАГОВ.
/// </summary>
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(NetworkAnimator))]
public class PlayerEntity : NetworkBehaviour
{
    [Header("Movement & Look Settings")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float jumpForce = 5f; 
    [SerializeField] private float lookSensitivity = 0.5f; 
    [SerializeField] private float interactRange = 3f;
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private LayerMask interactLayerMask = ~0;
    
    [Header("Physics Settings")]
    [SerializeField] private float gravity = -19.62f; 
    private float velocityY = 0f;

    [Header("Camera Bobbing")]
    [SerializeField] private float bobbingSpeed = 14f;
    [SerializeField] private float bobbingAmount = 0.05f;
    private float defaultCameraY;
    private float timer = 0f;

    [Header("Audio & Footsteps")]
    [SerializeField] private AudioSource footstepAudioSource;
    [SerializeField] private AudioClip[] footstepSounds;
    [SerializeField] private float footstepInterval = 0.4f; // Время между шагами при обычной ходьбе
    private float footstepTimer = 0f;
    private Vector3 lastPosition;

    private float lastInteractTime = 0f;

    [Header("Input Actions")]
    [SerializeField] private InputActionReference moveAction;
    [SerializeField] private InputActionReference lookAction;
    [SerializeField] private InputActionReference interactAction;
    [SerializeField] private InputActionReference dropAction; 
    [SerializeField] private InputActionReference jumpAction; 

    [Header("Hands & Items")]
    [SerializeField] private Transform rightHandSocket;
    
    [SyncVar(hook = nameof(OnHeldItemChanged))] 
    public NetworkIdentity heldItem;

    [Header("Vehicle State")]
    private CarSeat currentSeat;
    private bool isSitting = false;

    private CharacterController characterController;
    private Collider[] allColliders; 
    
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
        allColliders = GetComponentsInChildren<Collider>(); 

        if (animator == null) animator = GetComponentInChildren<Animator>(); 

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

        if (cameraTransform != null)
        {
            defaultCameraY = cameraTransform.localPosition.y;
            Camera cam = cameraTransform.GetComponent<Camera>();
            if (cam != null) cam.enabled = false;

            AudioListener listener = cameraTransform.GetComponent<AudioListener>();
            if (listener != null) listener.enabled = false;
        }

        lastPosition = transform.position;
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

        if (jumpAction != null)
        {
            jumpAction.action.Enable();
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

        if (jumpAction != null)
        {
            jumpAction.action.Disable();
        }
    }

    private void Update()
    {
        // 1. Отрабатываем шаги ДЛЯ ВСЕХ игроков (чтобы слышать чужие)
        HandleFootsteps();

        // 2. Всё что ниже - только для нашего локального персонажа
        if (!isLocalPlayer) return;

        if (isSitting)
        {
            HandleLook();
            HandleDriving();
            
            if (UnityEngine.InputSystem.Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                CmdLeaveSeat();
            }
            
            if (currentSeat != null && currentSeat.isDriverSeat)
            {
                if (UnityEngine.InputSystem.Keyboard.current.fKey.wasPressedThisFrame || 
                    UnityEngine.InputSystem.Keyboard.current.lKey.wasPressedThisFrame)
                {
                    currentSeat.carSystem.CmdToggleLights();
                }
            }
            return; 
        }

        HandleLook();
        HandleMovement();
        ApplyGravityAndJump();
        HandleCameraBobbing();
    }

    // --- ЛОГИКА ШАГОВ ---
    private void HandleFootsteps()
    {
        if (footstepSounds == null || footstepSounds.Length == 0 || footstepAudioSource == null) return;

        // Вычисляем горизонтальную скорость (чтобы звук не играл при падении в пропасть)
        Vector3 horizontalMovement = new Vector3(transform.position.x - lastPosition.x, 0, transform.position.z - lastPosition.z);
        float currentSpeed = horizontalMovement.magnitude / Time.deltaTime;
        lastPosition = transform.position;

        // Простой Raycast, чтобы проверить, стоит ли игрок на земле (Network Safe)
        bool isGroundedNetworkSafe = Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, 0.4f);

        if (currentSpeed > 0.5f && isGroundedNetworkSafe && !isSitting)
        {
            footstepTimer += Time.deltaTime;
            
            // Если игрок бежит быстрее, шаги звучат чаще
            float currentInterval = footstepInterval * (moveSpeed / Mathf.Max(currentSpeed, 1f));
            currentInterval = Mathf.Clamp(currentInterval, 0.2f, footstepInterval);

            if (footstepTimer >= currentInterval)
            {
                PlayRandomFootstep();
                footstepTimer = 0f;
            }
        }
        else
        {
            // Сбрасываем таймер, чтобы первый шаг всегда звучал сразу после начала движения
            footstepTimer = footstepInterval; 
        }
    }

    private void PlayRandomFootstep()
    {
        AudioClip clip = footstepSounds[Random.Range(0, footstepSounds.Length)];
        // Небольшой рандом высоты звука для естественности
        footstepAudioSource.pitch = Random.Range(0.85f, 1.15f); 
        footstepAudioSource.PlayOneShot(clip);
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

    private void ApplyGravityAndJump()
    {
        bool isGrounded = characterController.isGrounded;

        if (isGrounded && velocityY < 0)
        {
           
            velocityY = -0.1f; 
        }

        if (isGrounded && jumpAction != null && jumpAction.action.triggered)
        {
            velocityY = jumpForce;
        }

        velocityY += gravity * Time.deltaTime;
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

        if (Time.time < lastInteractTime + 0.5f) return;
        lastInteractTime = Time.time;

        if (networkAnimator != null) animator.SetTrigger(InteractTriggerHash);

        if (Physics.Raycast(cameraTransform.position, cameraTransform.forward, out RaycastHit hit, interactRange, interactLayerMask))
        {
            if (hit.collider.TryGetComponent(out CarPart brokenPart) || hit.collider.GetComponentInParent<CarPart>())
            {
                CarPart part = hit.collider.GetComponent<CarPart>() ?? hit.collider.GetComponentInParent<CarPart>();
                
                if (part != null && part.isBroken)
                {
                    
                    if (heldItem != null && heldItem.TryGetComponent(out IRepairTool tool))
                    {
                        if (tool.CanFix(part))
                        {
                           
                            RepairUIManager uiManager = FindFirstObjectByType<RepairUIManager>();
                            if (uiManager != null)
                            {
                                uiManager.OpenMiniGame(part, this);
                            }
                            return; 
                        }
                    }
                    else
                    {
                        Debug.Log("Чтобы починить это, нужен правильный инструмент в руках!");
                        return;
                    }
                }
            }
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
        foreach (var col in allColliders) if (col != null) col.enabled = false;

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

        foreach (var col in allColliders) if (col != null) col.enabled = true;
        characterController.enabled = true;
    }

    // =========================================================
    // СЕТЕВАЯ КОМАНДА ДЛЯ UI ПОЧИНКИ
    // =========================================================
    [Command]
    public void CmdFixPart(GameObject partObj)
    {
        // Проверяем, что объект не пустой и на нем действительно есть CarPart
        if (partObj != null && partObj.TryGetComponent(out CarPart part))
        {
            part.RepairPart(); // Сервер чинит деталь (меняет модельку и переменную isBroken у всех)
        }
    }
}