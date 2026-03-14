using Kotenkoff;
using UnityEngine;
using Mirror;
using UnityEngine.InputSystem;

/// <summary>
/// Отвечает за локальное передвижение игрока, управление камерой, посадку в авто и взаимодействие.
/// </summary>
[RequireComponent(typeof(CharacterController))]
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
    private float xRotation = 0f; 
    
    [SyncVar] public NetworkIdentity heldItem;
    
    [Header("Inventory")]
    [SerializeField, Tooltip("Ссылка на инвентарь игрока")]
    private PlayerInventory inventory;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
    }

    public override void OnStartLocalPlayer()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

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

        // Если игрок сидит в машине
        if (isSitting)
        {
            HandleDriving();
            
            // ВАЖНО: Выход на Пробел.
            if (UnityEngine.InputSystem.Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                CmdLeaveSeat();
            }
            return; // Блокируем передвижение пешком
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

    // --- ВОССТАНОВЛЕННЫЕ МЕТОДЫ ПЕШЕХОДА ---

    private void HandleLook()
    {
        if (lookAction == null || cameraTransform == null) return;

        Vector2 lookInput = lookAction.action.ReadValue<Vector2>();
        float mouseX = lookInput.x * lookSensitivity;
        float mouseY = lookInput.y * lookSensitivity;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        cameraTransform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        transform.Rotate(Vector3.up * mouseX);
    }

    private void HandleMovement()
    {
        if (moveAction == null) return;

        Vector2 inputDir = moveAction.action.ReadValue<Vector2>();
        Vector3 move = transform.right * inputDir.x + transform.forward * inputDir.y;
        characterController.Move(move * moveSpeed * Time.deltaTime);
    }

/// <summary>
    /// Локальный луч. Находит корневой объект и ИМЯ детали, на которую мы смотрим.
    /// Это обходит любые баги сетевой сериализации.
    /// </summary>
    private void OnInteractPerformed(InputAction.CallbackContext context)
    {
        if (isSitting || cameraTransform == null) return;

        Debug.DrawRay(cameraTransform.position, cameraTransform.forward * interactRange, Color.magenta, 2f);

        if (Physics.Raycast(cameraTransform.position, cameraTransform.forward, out RaycastHit hit, interactRange, interactLayerMask))
        {
            NetworkIdentity rootIdentity = hit.collider.GetComponentInParent<NetworkIdentity>();
            IInteractable interactable = hit.collider.GetComponentInParent<IInteractable>();

            if (rootIdentity != null && interactable != null)
            {
                // Берем точное имя объекта, на котором висит скрипт логики (например, "DriverSeat")
                string targetName = ((Component)interactable).gameObject.name;
                
                Debug.Log($"[Клиент] Навел на '{targetName}'. Отправляем запрос на сервер...");
                
                // Передаем корень машины и имя детали
                CmdInteract(rootIdentity, targetName);
            }
            else
            {
                Debug.LogWarning($"[Клиент] Объект {hit.collider.name} не интерактивный.");
            }
        }
    }

    /// <summary>
    /// Сервер получает корень машины и имя детали, находит деталь внутри префаба и активирует.
    /// </summary>
    [Command]
    private void CmdInteract(NetworkIdentity rootIdentity, string targetName)
    {
        // Если клиент прислал пустоту (например, читер) — игнорируем
        if (rootIdentity == null || string.IsNullOrEmpty(targetName)) return;

        Debug.Log($"[Сервер] Ищем деталь '{targetName}' внутри машины {rootIdentity.name}...");

        // Ищем объект с таким же именем внутри всей иерархии машины
        Transform[] allChildren = rootIdentity.GetComponentsInChildren<Transform>();
        
        foreach (Transform child in allChildren)
        {
            if (child.name == targetName)
            {
                // Проверяем, есть ли на найденном объекте наш интерфейс
                if (child.TryGetComponent(out IInteractable interactable))
                {
                    Debug.Log($"[Сервер] УСПЕХ! Деталь '{targetName}' найдена. Выполняем действие!");
                    interactable.ServerInteract(this, inventory);
                    return;
                }
            }
        }

        Debug.LogError($"[Сервер] ОШИБКА: Не смогли найти интерактивный объект с именем '{targetName}' внутри машины!");
    }

// --- СЕТЕВОЕ УПРАВЛЕНИЕ ПОСАДКОЙ ---

    [Command]
    private void CmdLeaveSeat()
    {
        if (currentSeat != null)
        {
            currentSeat.ServerLeave(this);
        }
    }

    [TargetRpc]
    public void TargetEnterSeat(NetworkIdentity carIdentity, string seatName)
    {
        Debug.Log($"[Клиент] Получена команда на посадку! Машина: {carIdentity}, Место: {seatName}");

        if (carIdentity == null) return;

        // Ищем ИМЕННО то кресло, на которое нажал игрок
        CarSeat[] seats = carIdentity.GetComponentsInChildren<CarSeat>();
        CarSeat targetSeat = null;
        
        foreach (CarSeat s in seats)
        {
            if (s.gameObject.name == seatName)
            {
                targetSeat = s;
                break;
            }
        }

        if (targetSeat == null)
        {
            Debug.LogError($"[Клиент] КРИТИЧЕСКАЯ ОШИБКА: Кресло '{seatName}' не найдено в машине!");
            return;
        }

        isSitting = true;
        currentSeat = targetSeat;
        
        characterController.enabled = false;
        transform.SetParent(targetSeat.transform);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
    }

    [TargetRpc]
    public void TargetLeaveSeat()
    {
        isSitting = false;
        currentSeat = null;
        
        transform.SetParent(null);
        transform.position += transform.right * 2f; 
        
        characterController.enabled = true;
    }
}