using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Отвечает ТОЛЬКО за физику и взгляд: CharacterController, WASD, гравитация, прыжок, Look.
/// Drunk-эффект живёт здесь, т.к. он модифицирует входные данные Look.
///
/// НЕ знает о Mirror напрямую — получает isLocalPlayer через ссылку на PlayerEntity.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    // ─── Inspector ────────────────────────────────────────────────────────────
    [Header("References")]
    [SerializeField] private PlayerEntity playerEntity;
    [SerializeField] private Transform cameraTransform;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float jumpForce = 5f;
    [SerializeField] private float gravity = -19.62f;

    [Header("Look")]
    [SerializeField] private float lookSensitivity = 0.5f;
    [SerializeField] private float lookSmoothness = 15f;

    // ─── Кэшированные компоненты (Awake only) ────────────────────────────────
    private CharacterController cc;
    private Animator animator;

    // ─── Состояние ────────────────────────────────────────────────────────────
    private float velocityY;
    private float xRotation;
    private float yRotation;
    private bool wasGrounded;
    private bool isMovementEnabled = true; // false когда игрок в машине

    // ─── Drunk-эффект ─────────────────────────────────────────────────────────
    private float drunkTimer;
    private float drunkIntensity;

    // ─── Input-ссылки (инжектируются из PlayerEntity.Awake) ──────────────────
    private InputActionReference moveAction;
    private InputActionReference lookAction;

    // ─── Хэши аниматора ───────────────────────────────────────────────────────
    private static readonly int SpeedHash = Animator.StringToHash("Speed");

    // ─── Публичные свойства ───────────────────────────────────────────────────
    public Transform CameraTransform => cameraTransform;
    public bool IsGrounded => cc != null && cc.isGrounded;
    public float VelocityY => velocityY;
    public Vector3 HorizontalVelocity
    {
        get { var v = cc.velocity; v.y = 0f; return v; }
    }

    // ─── События ──────────────────────────────────────────────────────────────
    public event System.Action OnJumped;
    public event System.Action OnLanded;

    // ─────────────────────────────────────────────────────────────────────────
    // Unity Messages
    // ─────────────────────────────────────────────────────────────────────────
    private void Awake()
    {
        cc       = GetComponent<CharacterController>();
        animator = GetComponentInChildren<Animator>();
    }

    private void Update()
    {
        if (!playerEntity.isLocalPlayer) return;

        // Look всегда активен (в том числе в машине для обзора)
        HandleLook();

        if (!isMovementEnabled) return;

        HandleMovement();
        HandleGravity();
        CheckLanding();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Вызывается из PlayerEntity.Awake() — инжектирует InputAction-ссылки.
    /// </summary>
    public void Initialize(InputActionReference move, InputActionReference look)
    {
        moveAction = move;
        lookAction = look;
    }

    /// <summary>
    /// Включает/выключает только физическое движение (Look при этом остаётся активным).
    /// Вызывается из PlayerEntity при посадке/выходе из машины.
    /// </summary>
    public void SetMovementEnabled(bool value)
    {
        isMovementEnabled = value;
        cc.enabled = value;
        if (!value) velocityY = 0f;
    }

    /// <summary>Пытается прыгнуть. Вызывается из PlayerEntity по InputAction.</summary>
    public void TryJump()
    {
        if (cc.enabled && cc.isGrounded)
        {
            velocityY = jumpForce;
            OnJumped?.Invoke();
        }
    }

    /// <summary>Сбрасывает поворот камеры (посадка/выход из машины).</summary>
    public void ResetLookRotation()
    {
        xRotation = 0f;
        yRotation = 0f;
        if (cameraTransform != null)
            cameraTransform.localRotation = Quaternion.identity;
    }

    /// <summary>Накладывает drunk-эффект на Look. Вызывается из TargetApplyDrunkEffect.</summary>
    public void ApplyDrunkEffect(float duration, float intensity)
    {
        drunkTimer    += duration;
        drunkIntensity = intensity;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Private Methods
    // ─────────────────────────────────────────────────────────────────────────

    private void HandleMovement()
    {
        if (moveAction == null) return;

        Vector2 input = moveAction.action.ReadValue<Vector2>();
        Vector3 move  = transform.right * input.x + transform.forward * input.y;
        cc.Move(move * moveSpeed * Time.deltaTime);

        if (animator != null)
            animator.SetFloat(SpeedHash, HorizontalVelocity.magnitude, 0.1f, Time.deltaTime);
    }

    private void HandleGravity()
    {
        if (cc.isGrounded && velocityY < 0f) velocityY = -0.1f;
        velocityY += gravity * Time.deltaTime;
        cc.Move(new Vector3(0f, velocityY, 0f) * Time.deltaTime);
    }

    private void CheckLanding()
    {
        bool grounded = cc.isGrounded;
        if (!wasGrounded && grounded) OnLanded?.Invoke();
        wasGrounded = grounded;
    }

    private void HandleLook()
    {
        if (lookAction == null || cameraTransform == null) return;

        Vector2 raw    = lookAction.action.ReadValue<Vector2>();
        float mouseX   = raw.x * lookSensitivity;
        float mouseY   = raw.y * lookSensitivity;

        // ── Drunk Effect ────────────────────────────────────────────────────
        if (drunkTimer > 0f)
        {
            drunkTimer -= Time.deltaTime;
            float fade  = Mathf.Clamp01(drunkTimer / 3f);
            mouseX     += Mathf.Sin(Time.time * 1.2f) * drunkIntensity * fade * Time.deltaTime;
            mouseY     += Mathf.Cos(Time.time * 0.8f) * drunkIntensity * fade * Time.deltaTime;
        }
        // ────────────────────────────────────────────────────────────────────

        xRotation = Mathf.Clamp(xRotation - mouseY, -90f, 90f);

        if (playerEntity.IsSitting)
        {
            // В машине: ограниченный горизонтальный поворот без вращения тела
            yRotation = Mathf.Clamp(yRotation + mouseX, -110f, 110f);
            cameraTransform.localRotation = Quaternion.Slerp(
                cameraTransform.localRotation,
                Quaternion.Euler(xRotation, yRotation, 0f),
                Time.deltaTime * lookSmoothness);
        }
        else
        {
            yRotation = 0f;
            cameraTransform.localRotation = Quaternion.Slerp(
                cameraTransform.localRotation,
                Quaternion.Euler(xRotation, 0f, 0f),
                Time.deltaTime * lookSmoothness);
            transform.Rotate(Vector3.up * mouseX);
        }

        // Уведомляем PlayerJuiceAndIK о движении мыши для weapon sway
        playerEntity.PlayerJuice?.OnLookInput(raw.x, raw.y);
    }
}