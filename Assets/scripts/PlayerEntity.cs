using Kotenkoff;
using UnityEngine;
using Mirror;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

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
    private float velocityY;

    [Header("Camera Bobbing")]
    [SerializeField] private float bobbingSpeed = 14f;
    [SerializeField] private float bobbingAmount = 0.05f;
    private Vector3 defaultCameraPos;
    private float bobbingTimer;

    [Header("Audio & Footsteps")]
    [SerializeField] private AudioSource footstepAudioSource;
    [SerializeField] private AudioClip[] footstepSounds;
    [SerializeField] private float footstepInterval = 0.4f;
    private float footstepTimer;
    private Vector3 lastPosition;

    private float lastInteractTime;

    [Header("Input Actions")]
    [SerializeField] private InputActionReference moveAction;
    [SerializeField] private InputActionReference lookAction;
    [SerializeField] private InputActionReference interactAction;
    [SerializeField] private InputActionReference dropAction; 
    [SerializeField] private InputActionReference jumpAction; 
    [SerializeField] private InputActionReference useAction; 
    
    [SerializeField] private InputActionReference toggleLightsAction;
    [SerializeField] private InputActionReference hornAction;

    [Header("Hands & Items")]
    [SerializeField] private Transform rightHandSocket;
    
    [SyncVar(hook = nameof(OnHeldItemChanged))] 
    public NetworkIdentity heldItem;

    [Header("Vehicle State")]
    private CarSeat currentSeat;
    private bool isSitting;

    private CharacterController characterController;
    private Collider[] allColliders; 
    private float xRotation; 
    private float yRotation; 
    
    [Header("Inventory")]
    [SerializeField] private PlayerInventory inventory;

    [Header("Visuals & Animation")]
    [SerializeField] private Animator animator; 
    private NetworkAnimator networkAnimator;


    [Header("Smoothness & Sway (Game Feel)")]
    [SerializeField] private float lookSmoothness = 15f; 
    [SerializeField] private float swayAmount = 0.02f;  
    [SerializeField] private float maxSway = 0.06f;     
    [SerializeField] private float swaySmoothness = 6f;  

    private float drunkTimer;
    private float currentDrunkIntensity;
    private Vector3 initialHandPosition;

    [Header("IK / Bone Tracking")]
    [SerializeField] private Transform headBone; 
    [SerializeField] private Vector3 headRotationOffset; 
    [SerializeField] [Range(0, 1)] private float headLookWeight = 1f;
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int IsSittingHash = Animator.StringToHash("IsSitting");
    private static readonly int InteractTriggerHash = Animator.StringToHash("Interact");
    private static readonly int SitTriggerHash = Animator.StringToHash("Sit");
    private static readonly int StandTriggerHash = Animator.StringToHash("Stand");
    
    [Header("Drop settings")]
    [SerializeField] private float maxDropDistance = 1.5f;

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

        if (rightHandSocket != null)
        {
            initialHandPosition = rightHandSocket.localPosition;
        }

        if (cameraTransform != null)
        {
            defaultCameraPos = cameraTransform.localPosition;
            if (cameraTransform.TryGetComponent(out Camera cam)) cam.enabled = false;
            if (cameraTransform.TryGetComponent(out AudioListener listener)) listener.enabled = false;
        }

        lastPosition = transform.position;
    }

    private void LateUpdate()
    {
        if (!isLocalPlayer || headBone == null || isSitting) return;

        
  
        Quaternion targetHeadRotation = cameraTransform.rotation * Quaternion.Euler(headRotationOffset);

      
        headBone.rotation = Quaternion.Slerp(headBone.rotation, targetHeadRotation, headLookWeight);
    }

    public override void OnStartLocalPlayer()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (cameraTransform != null)
        {
            if (cameraTransform.TryGetComponent(out Camera cam)) cam.enabled = true;
            if (cameraTransform.TryGetComponent(out AudioListener listener)) listener.enabled = true;
        }

        moveAction?.action.Enable();
        lookAction?.action.Enable();

        if (jumpAction != null)
        {
            jumpAction.action.Enable();
            jumpAction.action.performed += OnJump;
        }
        
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

        if (useAction != null) 
        {
            useAction.action.Enable();
            useAction.action.performed += OnUsePerformed;
        }
    }

    public override void OnStopLocalPlayer()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        moveAction?.action.Disable();
        lookAction?.action.Disable();

        if (jumpAction != null)
        {
            jumpAction.action.performed -= OnJump;
            jumpAction.action.Disable();
        }
        
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

        if (useAction != null) 
        {
            useAction.action.performed -= OnUsePerformed;
            useAction.action.Disable();
        }

        if (toggleLightsAction != null)
        {
            toggleLightsAction.action.performed -= OnToggleLights;
            toggleLightsAction.action.Disable();
        }

        if (hornAction != null)
        {
            hornAction.action.performed -= OnHorn;
            hornAction.action.Disable();
        }
    }

    private void Update()
    {
        HandleFootsteps();

        if (!isLocalPlayer) return;

        if (isSitting)
        {
            HandleLook();
            HandleDriving();
            
            if (Keyboard.current.spaceKey.wasPressedThisFrame) CmdLeaveSeat();
            
            if (currentSeat != null && currentSeat.isDriverSeat)
            {
                if (Keyboard.current.fKey.wasPressedThisFrame || Keyboard.current.lKey.wasPressedThisFrame)
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

    private void HandleFootsteps()
    {
        if (footstepSounds == null || footstepSounds.Length == 0 || footstepAudioSource == null) return;

        Vector3 delta = transform.position - lastPosition;
        delta.y = 0;
        float currentSpeed = delta.magnitude / Time.deltaTime;
        lastPosition = transform.position;

        bool isGroundedNetworkSafe = Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, 0.4f);

        if (currentSpeed > 0.5f && isGroundedNetworkSafe && !isSitting)
        {
            footstepTimer += Time.deltaTime;
            float currentInterval = Mathf.Clamp(footstepInterval * (moveSpeed / Mathf.Max(currentSpeed, 1f)), 0.2f, footstepInterval);

            if (footstepTimer >= currentInterval)
            {
                PlayRandomFootstep();
                footstepTimer = 0f;
            }
        }
        else
        {
            footstepTimer = footstepInterval; 
        }
    }

    private void PlayRandomFootstep()
    {
        footstepAudioSource.pitch = Random.Range(0.85f, 1.15f); 
        footstepAudioSource.PlayOneShot(footstepSounds[Random.Range(0, footstepSounds.Length)]);
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


        // --- ЛОГИКА ОПЬЯНЕНИЯ ---
        if (drunkTimer > 0)
        {
            drunkTimer -= Time.deltaTime;
            
            // Плавное затухание эффекта в последние 3 секунды
            float fadeMultiplier = Mathf.Clamp01(drunkTimer / 3f); 
            
            // Фигуры Лиссажу для пьяного "плавания" камеры
            float drunkSwayX = Mathf.Sin(Time.time * 1.2f) * currentDrunkIntensity * fadeMultiplier * Time.deltaTime;
            float drunkSwayY = Mathf.Cos(Time.time * 0.8f) * currentDrunkIntensity * fadeMultiplier * Time.deltaTime;

            mouseX += drunkSwayX;
            mouseY += drunkSwayY;
        }
        // -------------------------

        xRotation = Mathf.Clamp(xRotation - mouseY, -90f, 90f);

        if (isSitting)
        {
            yRotation = Mathf.Clamp(yRotation + mouseX, -110f, 110f);
            Quaternion targetCamRot = Quaternion.Euler(xRotation, yRotation, 0f);
          
            cameraTransform.localRotation = Quaternion.Slerp(cameraTransform.localRotation, targetCamRot, Time.deltaTime * lookSmoothness);
        }
        else
        {
            yRotation = 0f;
            Quaternion targetCamRot = Quaternion.Euler(xRotation, 0f, 0f);
            
          
            cameraTransform.localRotation = Quaternion.Slerp(cameraTransform.localRotation, targetCamRot, Time.deltaTime * lookSmoothness);
            
        
            transform.Rotate(Vector3.up * mouseX);
        }

    
        HandleWeaponSway(lookInput.x, lookInput.y);
    }

    private void HandleWeaponSway(float mouseX, float mouseY)
    {
        if (rightHandSocket == null) return;

   
        float moveX = -mouseX * swayAmount;
        float moveY = -mouseY * swayAmount;

  
        moveX = Mathf.Clamp(moveX, -maxSway, maxSway);
        moveY = Mathf.Clamp(moveY, -maxSway, maxSway);


        Vector3 finalPosition = new Vector3(moveX, moveY, 0) + initialHandPosition;

        rightHandSocket.localPosition = Vector3.Lerp(rightHandSocket.localPosition, finalPosition, Time.deltaTime * swaySmoothness);
    }

    private void HandleMovement()
    {
        if (moveAction == null) return;
        Vector2 inputDir = moveAction.action.ReadValue<Vector2>();
        Vector3 move = transform.right * inputDir.x + transform.forward * inputDir.y;
        
        characterController.Move(move * moveSpeed * Time.deltaTime);

        if (animator != null)
        {
            Vector3 horizVelocity = characterController.velocity;
            horizVelocity.y = 0;
            animator.SetFloat(SpeedHash, horizVelocity.magnitude, 0.1f, Time.deltaTime);
        }
    }

    private void ApplyGravityAndJump()
    {
        if (characterController.isGrounded && velocityY < 0) velocityY = -0.1f;

        velocityY += gravity * Time.deltaTime;
        characterController.Move(new Vector3(0, velocityY, 0) * Time.deltaTime);
    }
    
    private void OnJump(InputAction.CallbackContext ctx)
    {
        if (characterController.isGrounded)
        {
            velocityY = jumpForce;
        }
    }

    private void HandleCameraBobbing()
    {
        if (cameraTransform == null) return;

        Vector3 horizVelocity = characterController.velocity;
        horizVelocity.y = 0;
        float speed = horizVelocity.magnitude;

        if (speed > 0.1f && characterController.isGrounded)
        {
            bobbingTimer += Time.deltaTime * bobbingSpeed;
            float newY = defaultCameraPos.y + Mathf.Sin(bobbingTimer * 2f) * bobbingAmount;
            float newX = defaultCameraPos.x + Mathf.Cos(bobbingTimer) * bobbingAmount * 0.5f;
            cameraTransform.localPosition = new Vector3(newX, newY, cameraTransform.localPosition.z);
        }
        else
        {
            bobbingTimer = 0f;
            cameraTransform.localPosition = Vector3.Lerp(cameraTransform.localPosition, defaultCameraPos, Time.deltaTime * bobbingSpeed);
        }
    }

    private void OnInteractPerformed(InputAction.CallbackContext context)
    {
        if (isSitting || cameraTransform == null || Time.time < lastInteractTime + 0.5f) return;
        lastInteractTime = Time.time;

        if (networkAnimator != null) animator.SetTrigger(InteractTriggerHash);

        // 1. РИСУЕМ ЛУЧ В SCENE VIEW (КРАСНЫЙ)
        Debug.DrawRay(cameraTransform.position, cameraTransform.forward * interactRange, Color.red, 2f);

        if (Physics.Raycast(cameraTransform.position, cameraTransform.forward, out RaycastHit hit, interactRange, interactLayerMask))
        {
            // 2. ПИШЕМ В КОНСОЛЬ АБСОЛЮТНО ВСЕ, ВО ЧТО ПОПАЛИ
            Debug.Log($"[ОТЛАДКА] Луч врезался в объект: {hit.collider.gameObject.name} (Тег: {hit.collider.tag})");

            CarPart part = hit.collider.GetComponent<CarPart>() ?? hit.collider.GetComponentInParent<CarPart>();
            if (part != null && part.isBroken)
            {
                if (heldItem != null && heldItem.TryGetComponent(out IRepairTool tool) && tool.CanFix(part))
                {
                    FindFirstObjectByType<RepairUIManager>()?.OpenMiniGame(part, this);
                    return; 
                }
            }

            NetworkIdentity rootIdentity = hit.collider.GetComponentInParent<NetworkIdentity>();
            IInteractable interactable = hit.collider.GetComponentInParent<IInteractable>();

            if (rootIdentity != null && interactable != null)
            {
                Debug.Log($"[ОТЛАДКА] Найден IInteractable на объекте: {((Component)interactable).gameObject.name}. Отправляем команду на сервер!");
                CmdInteract(rootIdentity, ((Component)interactable).gameObject.name);
            }
            else
            {
                Debug.LogWarning("[ОТЛАДКА] Компонент IInteractable ИЛИ NetworkIdentity не найден на этом объекте или его родителях!");
            }
        }
        else
        {
            Debug.Log("[ОТЛАДКА] Луч пролетел мимо и ни во что не попал. Проверь дистанцию или слой!");
        }
    }

    [Command]
    private void CmdInteract(NetworkIdentity rootIdentity, string targetName)
    {
        if (rootIdentity == null || string.IsNullOrEmpty(targetName)) return;

        foreach (Transform child in rootIdentity.GetComponentsInChildren<Transform>())
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
        inventory?.RemoveItem(itemToDrop);
        heldItem = null;

        if (Physics.Raycast(cameraTransform.position, cameraTransform.forward, out RaycastHit hit, maxDropDistance))
        {
            if (hit.distance < maxDropDistance)
            {
                itemToDrop.transform.position = hit.point;
            }
        }
        else
        {
            itemToDrop.transform.position = cameraTransform.position + cameraTransform.forward * maxDropDistance;
        }
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
            newItem.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity); 

            if (newItem.TryGetComponent(out Rigidbody rb)) rb.isKinematic = true;
            foreach (var col in newItem.GetComponents<Collider>()) col.enabled = false;
        }
    }

    [Command]
    private void CmdLeaveSeat()
    {
        currentSeat?.ServerLeave(this);
    }

    [TargetRpc]
    public void TargetEnterSeat(NetworkIdentity carNetId, string seatPath)
    {
        EnterVehicle();
        
        GameObject seatObj = GameObject.Find(seatPath);
        if (seatObj == null || !seatObj.TryGetComponent(out currentSeat)) return;

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
        transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);

        xRotation = 0f;
        yRotation = 0f;
        cameraTransform.localRotation = Quaternion.identity;
    }

    [TargetRpc]
    public void TargetLeaveSeat()
    {
        ExitVehicle();
        
        isSitting = false;
        
        if (animator != null)
        {
            animator.SetBool(IsSittingHash, false);
            animator.SetTrigger(StandTriggerHash);
        }

        transform.SetParent(null);
        transform.rotation = Quaternion.Euler(0, transform.eulerAngles.y, 0);

        transform.position = currentSeat != null && currentSeat.exitPoint != null 
            ? currentSeat.exitPoint.position 
            : transform.position + transform.right * 1.5f;

        xRotation = 0f;
        yRotation = 0f;
        
        cameraTransform.localRotation = Quaternion.identity;
        cameraTransform.localPosition = defaultCameraPos;
        
        currentSeat = null;

        foreach (var col in allColliders) if (col != null) col.enabled = true;
        characterController.enabled = true;
    }

    [Command]
    public void CmdFixPart(GameObject partObj)
    {
        if (partObj != null && partObj.TryGetComponent(out CarPart part))
        {
         
            part.RepairPart(); 
            
            // Если в руках была изолента - мы её потратили (уничтожаем предмет)!
            if (heldItem != null && heldItem.TryGetComponent(out DuctTapeItem tape))
            {
                DestroyHeldItem(); 
            }
        }
    }

    private void OnUsePerformed(InputAction.CallbackContext context)
    {
        if (isSitting || heldItem == null) return;
        CmdUseItem(); 
    }

    [Command]
    private void CmdUseItem()
    {
        if (heldItem != null && heldItem.TryGetComponent(out IUsableItem usableItem))
        {
            usableItem.ServerUse(this);
        }
    }

    [Server]
    public void DestroyHeldItem()
    {
        if (heldItem == null) return;
        
        GameObject itemObj = heldItem.gameObject;
        heldItem = null; 
        
        inventory?.RemoveItem(itemObj); 
        NetworkServer.Destroy(itemObj); 
    }
    
    [TargetRpc]
    public void TargetApplyDrunkEffect(NetworkConnection target, float duration, float intensity)
    {
  
        drunkTimer += duration; 
        currentDrunkIntensity = intensity;
    }

    private void EnterVehicle()
    {
        if (toggleLightsAction != null)
        {
            toggleLightsAction.action.Enable();
            toggleLightsAction.action.performed += OnToggleLights;
        }

        if (hornAction != null)
        {
            hornAction.action.Enable();
            hornAction.action.performed += OnHorn;
        }
    }

    private void ExitVehicle()
    {
        if (toggleLightsAction != null)
        {
            toggleLightsAction.action.performed -= OnToggleLights;
            toggleLightsAction.action.Disable();
        }

        if (hornAction != null)
        {
            hornAction.action.performed -= OnHorn;
            hornAction.action.Disable();
        }
    }
    
    private void OnToggleLights(InputAction.CallbackContext ctx)
    {
        Debug.Log("ToggleLights");
        
        if (currentSeat.isDriverSeat)
        {
            currentSeat.carSystem.lightsOn = !currentSeat.carSystem.lightsOn;
            currentSeat.carSystem.CmdToggleLights();
        }
    }
    
    private void OnHorn(InputAction.CallbackContext ctx)
    {
        if (currentSeat.isDriverSeat)
        {
            currentSeat?.carSystem.CmdHonkHorn();
        }
    }
}