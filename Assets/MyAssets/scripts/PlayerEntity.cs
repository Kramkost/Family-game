using Breakdown;
using Kotenkoff;
using UnityEngine;
using Mirror;
using UnityEngine.InputSystem;
using Breakdown;
/// <summary>
/// Центральный хаб игрока. Отвечает ТОЛЬКО за:
/// — Mirror (SyncVar, Command, TargetRpc)
/// — Инвентарь и управление предметами
/// — Логику взаимодействия (raycast → CmdInteract)
/// — Input System (enable/disable, routing в PlayerMovement)
/// — Логику посадки в машину
///
/// Физика → PlayerMovement.  Сочность → PlayerJuiceAndIK.
/// </summary>
[RequireComponent(typeof(PlayerMovement))]
[RequireComponent(typeof(PlayerJuiceAndIK))]
[RequireComponent(typeof(NetworkAnimator))]
public class PlayerEntity : NetworkBehaviour
{
    // ─── Sibling Components ───────────────────────────────────────────────────
    [Header("Sibling Components")]
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private PlayerJuiceAndIK playerJuice;

    // ─── Inventory & Items ────────────────────────────────────────────────────
    [Header("Inventory & Items")]
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private Transform rightHandSocket;
    [SerializeField] private float maxDropDistance = 1.5f;

    [SyncVar(hook = nameof(OnHeldItemChanged))]
    public NetworkIdentity heldItem;

    // ─── Interaction ──────────────────────────────────────────────────────────
    [Header("Interaction")]
    [SerializeField] private float interactRange = 3f;
    [SerializeField] private LayerMask interactLayerMask = ~0;
    private float lastInteractTime;

    // ─── Animation ────────────────────────────────────────────────────────────
    [Header("Animation")]
    [SerializeField] private Animator animator;
    private NetworkAnimator networkAnimator;

    private static readonly int InteractTriggerHash = Animator.StringToHash("Interact");
    private static readonly int IsSittingHash       = Animator.StringToHash("IsSitting");
    private static readonly int SitTriggerHash      = Animator.StringToHash("Sit");
    private static readonly int StandTriggerHash    = Animator.StringToHash("Stand");

    // ─── Vehicle State ────────────────────────────────────────────────────────
    [HideInInspector] public CarSeat serverCurrentSeat; // только сервер
    private CarSeat currentSeat;
    private Collider[] allColliders;
    private bool isSitting;

    // ─── Input Actions ────────────────────────────────────────────────────────
    [Header("Input Actions")]
    [SerializeField] private InputActionReference moveAction;
    [SerializeField] private InputActionReference lookAction;
    [SerializeField] private InputActionReference interactAction;
    [SerializeField] private InputActionReference dropAction;
    [SerializeField] private InputActionReference jumpAction;
    [SerializeField] private InputActionReference useAction;
    [SerializeField] private InputActionReference toggleLightsAction;
    [SerializeField] private InputActionReference hornAction;

    // Делегаты для корректного unsubscribe
    private System.Action<InputAction.CallbackContext> onJumpHandler;
    private System.Action<InputAction.CallbackContext> onInteractHandler;
    private System.Action<InputAction.CallbackContext> onDropHandler;
    private System.Action<InputAction.CallbackContext> onUseHandler;
    private System.Action<InputAction.CallbackContext> onToggleLightsHandler;
    private System.Action<InputAction.CallbackContext> onHornHandler;

    // ─── Public Properties ────────────────────────────────────────────────────
    public bool IsSitting => isSitting;
    public PlayerJuiceAndIK PlayerJuice => playerJuice;
    public NetworkIdentity HeldItem => heldItem;

    private Vector3 originalScale;

    // ─────────────────────────────────────────────────────────────────────────
    // Awake
    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        networkAnimator = GetComponent<NetworkAnimator>();
        allColliders    = GetComponentsInChildren<Collider>();

        if (animator       == null) animator       = GetComponentInChildren<Animator>();
        if (playerMovement == null) playerMovement = GetComponent<PlayerMovement>();
        if (playerJuice    == null) playerJuice    = GetComponent<PlayerJuiceAndIK>();

        // Автопоиск rightHandSocket по тегу
        if (rightHandSocket == null)
        {
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
            {
                if (t.CompareTag("Hand")) { rightHandSocket = t; break; }
            }
        }

        // Инжектируем InputAction-ссылки в PlayerMovement
        playerMovement.Initialize(moveAction, lookAction);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Mirror Lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    public override void OnStartLocalPlayer()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
        EnableBaseInput();
    }

    public override void OnStopLocalPlayer()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
        DisableBaseInput();
        DisableVehicleInput();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Update — только логика хаба
    // ─────────────────────────────────────────────────────────────────────────

    private void Update()
    {
        if (!isLocalPlayer || !isSitting) return;
        HandleDriving();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Input Management
    // ─────────────────────────────────────────────────────────────────────────

    private void EnableBaseInput()
    {
        moveAction?.action.Enable();
        lookAction?.action.Enable();

        Bind(jumpAction,     ref onJumpHandler,     OnJumpPerformed);
        Bind(interactAction, ref onInteractHandler, OnInteractPerformed);
        Bind(dropAction,     ref onDropHandler,     OnDropPerformed);
        Bind(useAction,      ref onUseHandler,      OnUsePerformed);
    }

    private void DisableBaseInput()
    {
        moveAction?.action.Disable();
        lookAction?.action.Disable();

        Unbind(jumpAction,     ref onJumpHandler);
        Unbind(interactAction, ref onInteractHandler);
        Unbind(dropAction,     ref onDropHandler);
        Unbind(useAction,      ref onUseHandler);
    }

    private void EnableVehicleInput()
    {
        Bind(toggleLightsAction, ref onToggleLightsHandler, OnToggleLights);
        Bind(hornAction,         ref onHornHandler,         OnHorn);
    }

    private void DisableVehicleInput()
    {
        Unbind(toggleLightsAction, ref onToggleLightsHandler);
        Unbind(hornAction,         ref onHornHandler);
    }

    /// <summary>Подписывает и включает InputAction одной строкой.</summary>
    private static void Bind(InputActionReference actionRef,
        ref System.Action<InputAction.CallbackContext> field,
        System.Action<InputAction.CallbackContext> handler)
    {
        if (actionRef == null) return;
        field = handler;
        actionRef.action.performed += field;
        actionRef.action.Enable();
    }

    /// <summary>Отписывает и выключает InputAction одной строкой.</summary>
    private static void Unbind(InputActionReference actionRef,
        ref System.Action<InputAction.CallbackContext> field)
    {
        if (actionRef == null || field == null) return;
        actionRef.action.performed -= field;
        actionRef.action.Disable();
        field = null;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Input Handlers
    // ─────────────────────────────────────────────────────────────────────────

    private void OnJumpPerformed(InputAction.CallbackContext ctx)
    {
        if (isSitting) { CmdLeaveSeat(); return; }
        playerMovement.TryJump();
    }

    private void OnInteractPerformed(InputAction.CallbackContext ctx)
    {
        if (isSitting || Time.time < lastInteractTime + 0.5f) return;
        lastInteractTime = Time.time;

        Transform cam = playerMovement.CameraTransform;
        if (cam == null) return;

        animator?.SetTrigger(InteractTriggerHash);
        Debug.DrawRay(cam.position, cam.forward * interactRange, Color.red, 2f);

        if (!Physics.Raycast(cam.position, cam.forward, out RaycastHit hit, interactRange, interactLayerMask))
        {
            Debug.Log("[Interact] Raycast missed.");
            return;
        }

        Debug.Log($"[Interact] Hit: {hit.collider.gameObject.name}");

        // Ремонт сломанной детали машины
        CarPart part = hit.collider.GetComponent<CarPart>() ?? hit.collider.GetComponentInParent<CarPart>();
        if (part != null && part.isBroken)
        {
            if (heldItem != null && heldItem.TryGetComponent(out IRepairTool tool) && tool.CanFix(part))
            {
                if (part.gameObject.TryGetComponent(out RadioController radio))
                {
                    CmdFixPart(part.gameObject);
                }
                else
                {
                    FindFirstObjectByType<RepairUIManager>()?.OpenMiniGame(part, this);
                    return;
                }
            }
        }

        NetworkIdentity rootIdentity = hit.collider.GetComponentInParent<NetworkIdentity>();
        IInteractable interactable   = hit.collider.GetComponentInParent<IInteractable>();

        if (rootIdentity != null && interactable != null)
        {
            CmdInteract(rootIdentity, ((Component)interactable).gameObject.name);
            // Процедурный IK-reach: рука тянется к точке взаимодействия
            playerJuice?.ProceduralReachFor(hit.transform);
        }
        else
        {
            Debug.LogWarning("[Interact] IInteractable или NetworkIdentity не найден.");
        }
    }

    private void OnDropPerformed(InputAction.CallbackContext ctx)
    {
        if (isSitting || heldItem == null) return;
        CmdDropItem();
    }

    private void OnUsePerformed(InputAction.CallbackContext ctx)
    {
        if (isSitting || heldItem == null) return;
        CmdUseItem();
    }

    private void OnToggleLights(InputAction.CallbackContext ctx)
    {
        if (currentSeat != null && currentSeat.isDriverSeat)
            currentSeat.carSystem.CmdToggleLights();
    }

    private void OnHorn(InputAction.CallbackContext ctx)
    {
        if (currentSeat != null && currentSeat.isDriverSeat)
            currentSeat.carSystem.CmdHonkHorn();
    }

    private void HandleDriving()
    {
        if (currentSeat == null || !currentSeat.isDriverSeat || currentSeat.carSystem == null) return;
        Vector2 input = moveAction.action.ReadValue<Vector2>();
        currentSeat.carSystem.LocalDrive(input.x, input.y);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Commands
    // ─────────────────────────────────────────────────────────────────────────

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

    [Command]
    private void CmdDropItem()
    {
        if (heldItem == null) return;

        Transform cam     = playerMovement.CameraTransform;
        GameObject toDrop = heldItem.gameObject;
        inventory?.RemoveItem(toDrop);
        heldItem = null;

        if (cam != null && Physics.Raycast(cam.position, cam.forward, out RaycastHit hit, maxDropDistance))
            toDrop.transform.position = hit.point;
        else
            toDrop.transform.position = cam != null
                ? cam.position + cam.forward * maxDropDistance
                : transform.position + transform.forward * maxDropDistance;
    }

    [Command]
    private void CmdLeaveSeat() => serverCurrentSeat?.ServerLeave(this);

    [Command]
    private void CmdUseItem()
    {
        if (heldItem != null && heldItem.TryGetComponent(out IUsableItem usable))
            usable.ServerUse(this);
    }

    [Command]
    public void CmdFixPart(GameObject partObj)
    {
        if (partObj == null || !partObj.TryGetComponent(out CarPart part)) return;

        if (partObj.TryGetComponent(out RadioController radio))
        {
            radio.CmdRepairRadio();
        }
        else
        {
            part.RepairPart();
        }
        
        if (heldItem != null && heldItem.TryGetComponent(out DuctTapeItem _))
            DestroyHeldItem();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Server Methods
    // ─────────────────────────────────────────────────────────────────────────

    [Server]
    public void ServerEquipItem(NetworkIdentity item) => heldItem = item;

    [Server]
    public void DestroyHeldItem()
    {
        if (heldItem == null) return;
        GameObject obj = heldItem.gameObject;
        heldItem = null;
        inventory?.RemoveItem(obj);
        NetworkServer.Destroy(obj);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Target RPCs
    // ─────────────────────────────────────────────────────────────────────────

    [TargetRpc]
    public void TargetEnterSeat(NetworkIdentity carNetId, string seatPath)
    {
        originalScale = transform.localScale;
        EnableVehicleInput();

        GameObject seatObj = GameObject.Find(seatPath);
        if (seatObj == null || !seatObj.TryGetComponent(out currentSeat)) return;

        // Только движение отключается — Look продолжает работать для обзора в машине
        playerMovement.SetMovementEnabled(false);

        foreach (var col in allColliders)
            if (col != null) col.enabled = false;

        isSitting = true;
        animator?.SetBool(IsSittingHash, true);
        animator?.SetTrigger(SitTriggerHash);

        Transform mountPoint = currentSeat.viewPoint != null ? currentSeat.viewPoint : currentSeat.transform;
        transform.SetParent(mountPoint);
        transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);

        playerMovement.ResetLookRotation();
    }

[TargetRpc]
public void TargetLeaveSeat()
{
    DisableVehicleInput();
    isSitting = false;

    animator?.SetBool(IsSittingHash, false);
    animator?.SetTrigger(StandTriggerHash);

    transform.SetParent(null);
    transform.localScale = originalScale;
    
    // 1. Ставим новую позицию
    transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
    transform.position = currentSeat?.exitPoint != null
        ? currentSeat.exitPoint.position
        : transform.position + transform.right * 1.5f;

    // ФИКС КАМЕРЫ 1: Принудительно синхронизируем физику ДО включения коллайдеров.
    // Это не даст координатам камеры улететь в бесконечность (NaN) при выходе.
    Physics.SyncTransforms();

    playerMovement.ResetLookRotation();
    currentSeat = null;

    // ФИКС КАМЕРЫ 2: Включаем всё, КРОМЕ CharacterController.
    // Его включит только SetMovementEnabled(true) чуть ниже, чтобы избежать конфликта.
    foreach (var col in allColliders)
    {
        if (col != null && !(col is CharacterController)) 
        {
            col.enabled = true;
        }
    }

    // 2. Безопасно включаем движение
    playerMovement.SetMovementEnabled(true);
}
    [TargetRpc]
    public void TargetApplyDrunkEffect(NetworkConnection target, float duration, float intensity)
    {
        // Drunk-эффект живёт в PlayerMovement, т.к. он модифицирует Look
        playerMovement.ApplyDrunkEffect(duration, intensity);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // SyncVar Hook
    // ─────────────────────────────────────────────────────────────────────────

    private void OnHeldItemChanged(NetworkIdentity oldItem, NetworkIdentity newItem)
    {
        // Отпускаем старый предмет
        if (oldItem != null)
        {
            oldItem.transform.SetParent(null);
            
            // ФИКС: Включаем физику ТОЛЬКО если предмета больше нет в инвентаре (выбросили)
            // Если он есть в инвентаре, мы просто его прячем
            bool isStillInInventory = false;
            foreach (var slot in inventory.slots)
            {
                if (slot.isClaimed && slot.itemNetId == oldItem)
                {
                    isStillInInventory = true;
                    break;
                }
            }

            if (!isStillInInventory)
            {
                if (oldItem.TryGetComponent(out Rigidbody oldRb)) oldRb.isKinematic = false;
                foreach (var col in oldItem.GetComponents<Collider>()) col.enabled = true;
                foreach (var ren in oldItem.GetComponentsInChildren<Renderer>()) ren.enabled = true;
            }
            else
            {
                // Предмет убрали в рюкзак. Выключаем рендер, чтобы он не висел в воздухе невидимым
                foreach (var ren in oldItem.GetComponentsInChildren<Renderer>()) ren.enabled = false;
            }
        }

        // Берём новый предмет (Тут твой старый код без изменений)
        Transform grabPoint = null;
        if (newItem != null && rightHandSocket != null)
        {
            newItem.transform.SetParent(rightHandSocket);
            newItem.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);

            if (newItem.TryGetComponent(out Rigidbody newRb)) newRb.isKinematic = true;
            foreach (var col in newItem.GetComponents<Collider>()) col.enabled = false;
            foreach (var ren in newItem.GetComponentsInChildren<Renderer>()) ren.enabled = true; // Обязательно включаем рендер!

            foreach (Transform child in newItem.GetComponentsInChildren<Transform>())
            {
                if (child.CompareTag("GrabPoint")) { grabPoint = child; break; }
            }
        }

        playerJuice?.SetGrabPoint(grabPoint);
    }

    [TargetRpc]
    public void TargetApplyExternalForce(NetworkConnection target, Vector3 force)
    {
        // Проверяем, что мы не сидим в машине, иначе нас вырвет прямо из кресла
        if (isSitting || playerMovement == null) return;

        // Передаем силу в скрипт передвижения
        playerMovement.ApplyExternalForce(force);
    }
}