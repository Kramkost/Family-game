using System.Collections;
using UnityEngine;

/// <summary>
/// Отвечает за всю «сочность»: звуки, процедурные анимации, IK.
///
/// ВАЖНО: OnAnimatorIK вызывается Unity только на MonoBehaviour,
/// расположенном на том же GameObject, что и Animator.
/// Если Animator на дочернем объекте — добавьте туда AnimatorIKBridge (см. ниже).
/// </summary>
public class PlayerJuiceAndIK : MonoBehaviour
{
    // ─── Inspector ────────────────────────────────────────────────────────────
    [Header("References (кэш заполняется через Awake)")]
    [SerializeField] private PlayerEntity playerEntity;
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private Animator animator;

    [Header("Camera Bobbing")]
    [SerializeField] private float bobbingSpeed  = 14f;
    [SerializeField] private float bobbingAmount = 0.05f;

    [Header("Footsteps")]
    [SerializeField] private AudioSource footstepAudioSource;
    [SerializeField] private AudioClip[] footstepSounds;
    [SerializeField] private float footstepInterval = 0.4f;

    [Header("Jump & Landing")]
    [SerializeField] private AudioSource effectsAudioSource;
    [SerializeField] private AudioClip jumpSound;
    [SerializeField] private AudioClip landSound;
    [SerializeField] private float landingJoltAmount   = 0.08f;
    [SerializeField] private float landingJoltDuration = 0.3f;

    [Header("Weapon Sway")]
    [SerializeField] private Transform rightHandSocket;
    [SerializeField] private float swayAmount              = 0.02f;
    [SerializeField] private float maxSway                 = 0.06f;
    [SerializeField] private float swaySmoothness          = 6f;
    [SerializeField] private float swayRotationAmount      = 2f;
    [SerializeField] private float maxRotationSway         = 5f;
    [SerializeField] private float swayRotationSmoothness  = 8f;

    [Header("Arm Raise (при взятии предмета)")]
    [SerializeField] private Transform rightArmBone;
    [SerializeField] private Vector3 raisedArmRotation = new Vector3(-60f, 0f, 0f);
    [SerializeField] private float armRaiseSpeed = 8f;

    [Header("Head Bone IK")]
    [SerializeField] private Transform headBone;
    [SerializeField] private Vector3 headRotationOffset;
    [SerializeField, Range(0f, 1f)] private float headLookWeight = 1f;

    // ─── Состояние ────────────────────────────────────────────────────────────
    private Transform cameraTransform; // кэш из PlayerMovement

    // Bobbing
    private Vector3 defaultCameraPos;
    private float bobbingTimer;
    private Vector3 landingJoltOffset; // смещение при приземлении

    // Footsteps
    private float footstepTimer;
    private Vector3 lastPosition;

    // Sway
    private Vector3 initialHandLocalPos;
    private Quaternion initialHandLocalRot;

    // Arm raise
    private float holdWeight; // 0..1, плавно через Lerp

    // IK: grab point (статичный, от held item)
    private Transform currentGrabPoint;

    // IK: procedural reach (временный, по вызову)
    private Transform reachTarget;
    private float reachWeight;

    // ─────────────────────────────────────────────────────────────────────────
    // Unity Messages
    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        // Кэшируем всё в Awake — никаких GetComponent в Update/LateUpdate!
        cameraTransform = playerMovement.CameraTransform;
        if (animator == null) animator = GetComponentInChildren<Animator>();

        if (rightHandSocket != null)
        {
            initialHandLocalPos = rightHandSocket.localPosition;
            initialHandLocalRot = rightHandSocket.localRotation;
        }

        if (cameraTransform != null)
            defaultCameraPos = cameraTransform.localPosition;

        lastPosition = transform.position;
    }

    private void OnEnable()
    {
        // Подписываемся на события из PlayerMovement
        playerMovement.OnJumped += HandleJumped;
        playerMovement.OnLanded += HandleLanded;
    }

    private void OnDisable()
    {
        playerMovement.OnJumped -= HandleJumped;
        playerMovement.OnLanded -= HandleLanded;
    }

    private void Update()
    {
        // Шаги — работают для ВСЕХ игроков (RemotePlayer тоже издаёт звук)
        HandleFootsteps();

        if (!playerEntity.isLocalPlayer) return;

        HandleCameraBobbing();
    }

    private void LateUpdate()
    {
        if (!playerEntity.isLocalPlayer) return;

        HandleHeadBoneIK();
        HandleArmRaise();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public API (вызывается из PlayerEntity)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Уведомление о вводе мыши от PlayerMovement для weapon sway.
    /// Вызывается каждый кадр из PlayerMovement.HandleLook().
    /// </summary>
    public void OnLookInput(float mouseX, float mouseY)
    {
        if (!playerEntity.isLocalPlayer) return;
        HandleWeaponSway(mouseX, mouseY);
    }

    /// <summary>
    /// Обновляет GrabPoint (точку захвата IK) при смене held item.
    /// Вызывается из PlayerEntity.OnHeldItemChanged().
    /// </summary>
    public void SetGrabPoint(Transform grabPoint)
    {
        currentGrabPoint = grabPoint;
    }

    /// <summary>
    /// Процедурное IK-взаимодействие:
    /// рука плавно тянется к target → держит → плавно возвращается.
    /// Вызывается из PlayerEntity после успешного raycast-взаимодействия.
    /// </summary>
    public void ProceduralReachFor(Transform target)
    {
        if (target == null) return;
        StopCoroutine(nameof(ReachForRoutine));
        StartCoroutine(ReachForRoutine(target));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Event Handlers
    // ─────────────────────────────────────────────────────────────────────────

    private void HandleJumped()
    {
        if (jumpSound != null && effectsAudioSource != null)
            effectsAudioSource.PlayOneShot(jumpSound);
    }

    private void HandleLanded()
    {
        if (landSound != null && effectsAudioSource != null)
            effectsAudioSource.PlayOneShot(landSound);

        // Микро-рывок камеры вниз для ощущения веса
        StartCoroutine(LandingJoltRoutine());
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Private: Footsteps
    // ─────────────────────────────────────────────────────────────────────────

    private void HandleFootsteps()
    {
        if (footstepSounds == null || footstepSounds.Length == 0 || footstepAudioSource == null) return;

        Vector3 delta  = transform.position - lastPosition;
        delta.y        = 0f;
        float speed    = delta.magnitude / Time.deltaTime;
        lastPosition   = transform.position;

        // Raycast вместо cc.isGrounded — безопасен для non-local игроков
        bool grounded = Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, 0.4f);

        if (speed > 0.5f && grounded && !playerEntity.IsSitting)
        {
            footstepTimer += Time.deltaTime;
            // Интервал динамически сжимается при высокой скорости
            float interval = Mathf.Clamp(footstepInterval * (5f / Mathf.Max(speed, 1f)), 0.2f, footstepInterval);

            if (footstepTimer >= interval)
            {
                PlayRandomFootstep();
                footstepTimer = 0f;
            }
        }
        else
        {
            footstepTimer = footstepInterval; // сброс, чтобы первый шаг был мгновенным
        }
    }

    private void PlayRandomFootstep()
    {
        footstepAudioSource.pitch = Random.Range(0.9f, 1.1f);
        footstepAudioSource.PlayOneShot(footstepSounds[Random.Range(0, footstepSounds.Length)]);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Private: Camera Bobbing (с учётом landing jolt)
    // ─────────────────────────────────────────────────────────────────────────

    private void HandleCameraBobbing()
    {
        if (cameraTransform == null) return;

        float speed = playerMovement.HorizontalVelocity.magnitude;

        if (speed > 0.1f && playerMovement.IsGrounded)
        {
            bobbingTimer += Time.deltaTime * bobbingSpeed;
            float y = defaultCameraPos.y + Mathf.Sin(bobbingTimer * 2f) * bobbingAmount + landingJoltOffset.y;
            float x = defaultCameraPos.x + Mathf.Cos(bobbingTimer)      * bobbingAmount * 0.5f;
            cameraTransform.localPosition = new Vector3(x, y, cameraTransform.localPosition.z);
        }
        else
        {
            bobbingTimer = 0f;
            Vector3 target = defaultCameraPos + landingJoltOffset;
            cameraTransform.localPosition = Vector3.Lerp(
                cameraTransform.localPosition, target, Time.deltaTime * bobbingSpeed);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Private: Weapon Sway
    // ─────────────────────────────────────────────────────────────────────────

    private void HandleWeaponSway(float mouseX, float mouseY)
    {
        if (rightHandSocket == null) return;

        float moveX = Mathf.Clamp(-mouseX * swayAmount, -maxSway, maxSway);
        float moveY = Mathf.Clamp(-mouseY * swayAmount, -maxSway, maxSway);
        Vector3 targetPos = initialHandLocalPos + new Vector3(moveX, moveY, 0f);

        float rotX  = Mathf.Clamp( mouseY * swayRotationAmount, -maxRotationSway, maxRotationSway);
        float rotY  = Mathf.Clamp(-mouseX * swayRotationAmount, -maxRotationSway, maxRotationSway);
        Quaternion targetRot = initialHandLocalRot * Quaternion.Euler(rotX, rotY, 0f);

        rightHandSocket.localPosition = Vector3.Lerp(
            rightHandSocket.localPosition, targetPos, Time.deltaTime * swaySmoothness);
        rightHandSocket.localRotation = Quaternion.Slerp(
            rightHandSocket.localRotation, targetRot, Time.deltaTime * swayRotationSmoothness);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Private: Head Bone (LateUpdate)
    // ─────────────────────────────────────────────────────────────────────────

    private void HandleHeadBoneIK()
    {
        if (headBone == null || cameraTransform == null) return;

        Quaternion target = cameraTransform.rotation * Quaternion.Euler(headRotationOffset);
        headBone.rotation = Quaternion.Slerp(headBone.rotation, target, headLookWeight);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Private: Arm Raise (LateUpdate)
    // ─────────────────────────────────────────────────────────────────────────

    private void HandleArmRaise()
    {
        if (rightArmBone == null) return;

        float targetWeight = playerEntity.HeldItem != null ? 1f : 0f;
        holdWeight = Mathf.Lerp(holdWeight, targetWeight, Time.deltaTime * armRaiseSpeed);

        if (holdWeight > 0.01f)
        {
            rightArmBone.localRotation = Quaternion.Slerp(
                rightArmBone.localRotation,
                Quaternion.Euler(raisedArmRotation),
                holdWeight);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Animator IK — ВАЖНО: вызывается Unity только если этот MonoBehaviour
    // находится на том же GameObject, что и Animator.
    // Если Animator на child — используй AnimatorIKBridge (см. конец файла).
    // ─────────────────────────────────────────────────────────────────────────

    public void HandleAnimatorIK(int layerIndex)
    {
        if (animator == null) return;

        // Приоритет: ProceduralReach > GrabPoint
        if (reachTarget != null && reachWeight > 0.001f)
        {
            animator.SetIKPositionWeight(AvatarIKGoal.RightHand, reachWeight);
            animator.SetIKRotationWeight(AvatarIKGoal.RightHand, reachWeight);
            animator.SetIKPosition(AvatarIKGoal.RightHand, reachTarget.position);
            animator.SetIKRotation(AvatarIKGoal.RightHand, reachTarget.rotation);
        }
        else if (currentGrabPoint != null)
        {
            animator.SetIKPositionWeight(AvatarIKGoal.RightHand, 1f);
            animator.SetIKRotationWeight(AvatarIKGoal.RightHand, 1f);
            animator.SetIKPosition(AvatarIKGoal.RightHand, currentGrabPoint.position);
            animator.SetIKRotation(AvatarIKGoal.RightHand, currentGrabPoint.rotation);
        }
        else
        {
            animator.SetIKPositionWeight(AvatarIKGoal.RightHand, 0f);
            animator.SetIKRotationWeight(AvatarIKGoal.RightHand, 0f);
        }
    }

    // Unity автоматически вызовет этот метод если мы на том же GameObject, что и Animator
    private void OnAnimatorIK(int layerIndex) => HandleAnimatorIK(layerIndex);

    // ─────────────────────────────────────────────────────────────────────────
    // Coroutines
    // ─────────────────────────────────────────────────────────────────────────

    private IEnumerator LandingJoltRoutine()
    {
        float elapsed = 0f;
        while (elapsed < landingJoltDuration)
        {
            float t = elapsed / landingJoltDuration;
            // Синусоида: быстрый рывок вниз с плавным затуханием
            float jolt = Mathf.Sin(t * Mathf.PI) * landingJoltAmount * (1f - t * 0.6f);
            landingJoltOffset = new Vector3(0f, -jolt, 0f);
            elapsed += Time.deltaTime;
            yield return null;
        }
        landingJoltOffset = Vector3.zero;
    }

    private IEnumerator ReachForRoutine(Transform target)
    {
        reachTarget = target;
        const float reachDuration = 0.25f;
        const float holdDuration  = 0.15f;

        // Фаза 1: тянемся (SmoothStep = плавное начало и конец)
        for (float t = 0f; t < reachDuration; t += Time.deltaTime)
        {
            reachWeight = Mathf.SmoothStep(0f, 1f, t / reachDuration);
            yield return null;
        }
        reachWeight = 1f;

        // Фаза 2: держим
        yield return new WaitForSeconds(holdDuration);

        // Фаза 3: возвращаемся
        for (float t = 0f; t < reachDuration; t += Time.deltaTime)
        {
            reachWeight = Mathf.SmoothStep(1f, 0f, t / reachDuration);
            yield return null;
        }

        reachWeight = 0f;
        reachTarget = null;
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// AnimatorIKBridge — прикрепи к тому же GameObject, что и Animator,
// если Animator живёт на дочернем объекте (например, на модели).
// ─────────────────────────────────────────────────────────────────────────────
public class AnimatorIKBridge : MonoBehaviour
{
    [SerializeField] private PlayerJuiceAndIK juiceAndIK;

    private void OnAnimatorIK(int layerIndex) => juiceAndIK?.HandleAnimatorIK(layerIndex);
}