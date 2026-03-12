using UnityEngine;
using Mirror;
using UnityEngine.InputSystem;

/// <summary>
/// Отвечает за локальное передвижение игрока, управление камерой и отправку команд взаимодействия.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerEntity : NetworkBehaviour
{
    [Header("Movement & Look Settings")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float lookSensitivity = 0.5f; // Чувствительность мыши
    [SerializeField] private float interactRange = 3f;
    [SerializeField] private Transform cameraTransform;

    [Header("Input Actions")]
    [Tooltip("Ссылка на действие передвижения (Action Type: Value, Control Type: Vector2)")]
    [SerializeField] private InputActionReference moveAction;
    
    [Tooltip("Ссылка на вращение камеры (Action Type: Value, Control Type: Vector2)")]
    [SerializeField] private InputActionReference lookAction;
    
    [Tooltip("Ссылка на действие взаимодействия (Action Type: Button)")]
    [SerializeField] private InputActionReference interactAction;

    private CharacterController characterController;
    
    // Накопительная переменная для ограничения наклона головы (чтобы не сломать шею)
    private float xRotation = 0f; 
    
    [SyncVar] public NetworkIdentity heldItem;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
    }

    /// <summary>
    /// Включаем инпут и прячем курсор только для локального игрока.
    /// </summary>
    public override void OnStartLocalPlayer()
    {
        // Прячем курсор и блокируем его в центре окна игры
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

    /// <summary>
    /// Очищаем подписки и освобождаем курсор при отключении/уничтожении.
    /// </summary>
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

        HandleLook();
        HandleMovement();
    }

    /// <summary>
    /// Вращает самого игрока влево/вправо и наклоняет камеру вверх/вниз.
    /// </summary>
    private void HandleLook()
    {
        if (lookAction == null || cameraTransform == null) return;

        // Считываем смещение мыши (Delta)
        Vector2 lookInput = lookAction.action.ReadValue<Vector2>();
        
        float mouseX = lookInput.x * lookSensitivity;
        float mouseY = lookInput.y * lookSensitivity;

        // Вычисляем наклон камеры (Pitch) и ограничиваем его
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        // Применяем наклон только к камере (локальная ось X)
        cameraTransform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        
        // Поворачиваем всё тело игрока влево/вправо (глобальная ось Y)
        transform.Rotate(Vector3.up * mouseX);
    }

    private void HandleMovement()
    {
        if (moveAction == null) return;

        Vector2 inputDir = moveAction.action.ReadValue<Vector2>();

        // Двигаемся относительно текущего поворота тела
        Vector3 move = transform.right * inputDir.x + transform.forward * inputDir.y;
        characterController.Move(move * moveSpeed * Time.deltaTime);
    }

private void OnInteractPerformed(InputAction.CallbackContext context)
    {
        if (cameraTransform == null)
        {
            Debug.LogError("ОШИБКА: Поле Camera Transform пустое! Перетащите камеру игрока в скрипт PlayerEntity.");
            return;
        }

        
        Debug.DrawRay(cameraTransform.position, cameraTransform.forward * interactRange, Color.red, 2f);

       
        if (Physics.Raycast(cameraTransform.position, cameraTransform.forward, out RaycastHit hit, interactRange))
        {
            Debug.Log($"[Интеракция] Луч попал в объект: {hit.collider.name}");

            
            NetworkIdentity targetIdentity = hit.collider.GetComponentInParent<NetworkIdentity>();

            if (targetIdentity != null)
            {
                Debug.Log($"[Интеракция] NetworkIdentity найден! Отправляем команду на сервер...");
                CmdInteract(targetIdentity);
            }
            else
            {
                Debug.LogWarning($"[Интеракция] На объекте {hit.collider.name} НЕТ NetworkIdentity!");
            }
        }
        else
        {
            Debug.Log("[Интеракция] Луч ушел в пустоту. Объект слишком далеко или на нем нет Collider.");
        }
    }


    [Command]
    private void CmdInteract(NetworkIdentity target)
    {
        if (target == null)
        {
            Debug.LogWarning("[Сервер] Пришла команда взаимодействия, но target == null!");
            return;
        }

        Debug.Log($"[Сервер] Игрок пытается взаимодействовать с объектом: {target.name}");

        
        IInteractable interactable = target.GetComponentInChildren<IInteractable>();

        if (interactable != null)
        {
            Debug.Log($"[Сервер] Скрипт логики найден на {target.name}! Выполняем ServerInteract...");
            interactable.ServerInteract(this);
        }
        else
        {
          
            Debug.LogError($"[Сервер] ОШИБКА: На объекте {target.name} (или его детях) НЕТ скрипта с интерфейсом IInteractable (например, JerryCan или GasTank)!");
        }
    }
}