using System.Collections;
using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// Отвечает за Juice: звуки, процедурные анимации, IK и визуальные эффекты.
/// Адаптировано под Mirror: локальные эффекты (камера) отделены от глобальных (партиклы, звук).
/// </summary>
public class PlayerJuiceAndIK : MonoBehaviour
{
    // ─── Inspector ────────────────────────────────────────────────────────────
    [Header("References")]
    [SerializeField] private PlayerEntity playerEntity;
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private Animator animator;

    [Header("Camera & Bobbing")]
    [SerializeField] private float bobbingSpeed = 14f;
    [SerializeField] private float bobbingAmount = 0.05f;
    [SerializeField] private Transform cameraPivot; // ВАЖНО: вращать саму камеру нельзя (перебьет мышь), нужен родительский пивот для Tilt/Bob

    [Header("Camera Tilt (Fall)")]
    [SerializeField] private float fallTiltMultiplier = 1.5f;
    [SerializeField] private float maxFallTilt = 15f;
    [SerializeField] private float tiltSmoothTime = 0.15f;

    [Header("Effects (Pooling)")]
    [SerializeField] private ParticleSystem walkDustPrefab;
    [SerializeField] private ParticleSystem jumpDustPrefab;
    [SerializeField] private ParticleSystem landDustPrefab;
    [SerializeField] private Transform feetTransform; // Точка спавна эффектов

    [Header("Audio")]
    [SerializeField] private AudioSource footstepAudioSource;
    [SerializeField] private AudioClip[] footstepSounds;
    [SerializeField] private float footstepInterval = 0.4f;
    [SerializeField] private AudioSource effectsAudioSource;
    [SerializeField] private AudioClip jumpSound;
    [SerializeField] private AudioClip landSound;

    [Header("Weapon Sway")]
    [SerializeField] private Transform rightHandSocket; 
    [SerializeField] private float swayAmount = 0.02f;
    [SerializeField] private float maxSway = 0.06f;
    [SerializeField] private float swaySmoothTime = 0.1f;
    [SerializeField] private float swayRotationAmount = 2f;
    [SerializeField] private float maxRotationSway = 5f;

    [Header("Arm Raise & IK")]
    [SerializeField] private Transform rightArmBone;
    [SerializeField] private Vector3 raisedArmRotation = new Vector3(-60f, 0f, 0f);
    [SerializeField] private float armRaiseSpeed = 8f;
    [SerializeField] private Transform headBone;
    [SerializeField] private Vector3 headRotationOffset;
    [SerializeField, Range(0f, 1f)] private float headLookWeight = 1f;

    // ─── Состояние ────────────────────────────────────────────────────────────
    private Transform cameraTransform;
    private Vector3 defaultCameraPos;
    private float bobbingTimer;
    
    // Fall Tilt
    private float currentTiltVelocity;
    private float currentTiltAngle;

    // Footsteps & Velocity
    private float footstepTimer;
    private Vector3 lastPosition;
    private float currentVerticalVelocity;

    // Sway (Используем SmoothDamp для лучшего feel'а)
    private Vector3 initialHandLocalPos;
    private Quaternion initialHandLocalRot;
    private Vector3 currentSwayPos;
    private Vector3 swayPosVelocity;
    private Vector2 targetSwayInput;
    private Vector2 currentSwayInput;
    private Vector2 swayInputVelocity;

    // IK
    private float holdWeight;
    private Transform currentGrabPoint;
    private Transform reachTarget;
    private float reachWeight;

    // Object Pools
    private ObjectPool<ParticleSystem> walkPool;
    private ObjectPool<ParticleSystem> jumpPool;
    private ObjectPool<ParticleSystem> landPool;

    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        cameraTransform = playerMovement.CameraTransform;
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (feetTransform == null) feetTransform = transform;

        if (rightHandSocket != null)
        {
            initialHandLocalPos = rightHandSocket.localPosition;
            initialHandLocalRot = rightHandSocket.localRotation;
        }

        if (cameraPivot != null)
            defaultCameraPos = cameraPivot.localPosition;

        lastPosition = transform.position;

        // Инициализация пулов (Unity 2021+)
        InitPool(ref walkPool, walkDustPrefab);
        InitPool(ref jumpPool, jumpDustPrefab);
        InitPool(ref landPool, landDustPrefab);
    }

    private void OnEnable()
    {
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
        CalculateVelocities();
        HandleFootsteps(); // Звуки и пыль от шагов работают для всех клиентов

        if (!playerEntity.isLocalPlayer) return;

        HandleCameraBobbingAndTilt();
    }

    private void LateUpdate()
    {
        // ВАЖНО: Sway и IK должны быть в LateUpdate, чтобы перезаписывать трансформации после Animator'а
        if (!playerEntity.isLocalPlayer) return;

        HandleWeaponSway();
        HandleHeadBoneIK();
        HandleArmRaise();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Object Pooling Setup
    // ─────────────────────────────────────────────────────────────────────────

    private void InitPool(ref ObjectPool<ParticleSystem> pool, ParticleSystem prefab)
    {
        if (prefab == null) return;
        pool = new ObjectPool<ParticleSystem>(
            createFunc: () => Instantiate(prefab),
            actionOnGet: obj => obj.gameObject.SetActive(true),
            actionOnRelease: obj => obj.gameObject.SetActive(false),
            actionOnDestroy: Destroy,
            defaultCapacity: 5,
            maxSize: 20
        );
    }

    private void SpawnEffect(ObjectPool<ParticleSystem> pool, Vector3 position)
    {
        if (pool == null) return;
        var ps = pool.Get();
        ps.transform.position = position;
        ps.Play();
        StartCoroutine(ReturnToPoolRoutine(pool, ps));
    }

    private IEnumerator ReturnToPoolRoutine(ObjectPool<ParticleSystem> pool, ParticleSystem ps)
    {
        yield return new WaitForSeconds(ps.main.duration + ps.main.startLifetime.constantMax);
        pool.Release(ps);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public API (Inputs)
    // ─────────────────────────────────────────────────────────────────────────

    public void OnLookInput(float mouseX, float mouseY)
    {
        if (!playerEntity.isLocalPlayer) return;
        targetSwayInput = new Vector2(mouseX, mouseY);
    }

    public void SetGrabPoint(Transform grabPoint) => currentGrabPoint = grabPoint;

    public void ProceduralReachFor(Transform target)
    {
        if (target == null) return;
        StopCoroutine(nameof(ReachForRoutine));
        StartCoroutine(ReachForRoutine(target));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Logic
    // ─────────────────────────────────────────────────────────────────────────

    private void CalculateVelocities()
    {
        Vector3 delta = transform.position - lastPosition;
        currentVerticalVelocity = delta.y / Time.deltaTime;
        lastPosition = transform.position;
    }

    private void HandleJumped()
    {
        if (jumpSound) effectsAudioSource?.PlayOneShot(jumpSound);
        SpawnEffect(jumpPool, feetTransform.position);
    }

    private void HandleLanded()
    {
        if (landSound) effectsAudioSource?.PlayOneShot(landSound);
        SpawnEffect(landPool, feetTransform.position);
    }

    private void HandleFootsteps()
    {
        if (footstepSounds == null || footstepSounds.Length == 0 || footstepAudioSource == null) return;

        Vector3 horizontalVelocity = playerMovement.HorizontalVelocity;
        float speed = horizontalVelocity.magnitude;

        // Надежнее проверять статус через стейт-машину, а не пускать рейкасты каждый кадр
        if (speed > 0.5f && playerMovement.IsGrounded && !playerEntity.IsSitting)
        {
            footstepTimer += Time.deltaTime;
            float interval = Mathf.Clamp(footstepInterval * (5f / Mathf.Max(speed, 1f)), 0.2f, footstepInterval);

            if (footstepTimer >= interval)
            {
                footstepAudioSource.pitch = Random.Range(0.9f, 1.1f);
                footstepAudioSource.PlayOneShot(footstepSounds[Random.Range(0, footstepSounds.Length)]);
                
                SpawnEffect(walkPool, feetTransform.position);
                footstepTimer = 0f;
            }
        }
        else
        {
            footstepTimer = footstepInterval;
        }
    }

    private void HandleCameraBobbingAndTilt()
    {
        if (cameraPivot == null) return;

        // 1. Bobbing
        float speed = playerMovement.HorizontalVelocity.magnitude;
        Vector3 targetLocalPos = defaultCameraPos;

        if (speed > 0.1f && playerMovement.IsGrounded)
        {
            bobbingTimer += Time.deltaTime * bobbingSpeed;
            targetLocalPos.y += Mathf.Sin(bobbingTimer * 2f) * bobbingAmount;
            targetLocalPos.x += Mathf.Cos(bobbingTimer) * bobbingAmount * 0.5f;
        }
        else
        {
            bobbingTimer = 0f;
        }

        cameraPivot.localPosition = Vector3.Lerp(cameraPivot.localPosition, targetLocalPos, Time.deltaTime * 10f);

        // 2. Fall Tilt (Наклон камеры по оси X при быстром падении)
        float targetTilt = 0f;
        if (!playerMovement.IsGrounded && currentVerticalVelocity < -2f)
        {
            targetTilt = Mathf.Clamp(currentVerticalVelocity * -fallTiltMultiplier, 0f, maxFallTilt);
        }

        currentTiltAngle = Mathf.SmoothDamp(currentTiltAngle, targetTilt, ref currentTiltVelocity, tiltSmoothTime);
        cameraPivot.localRotation = Quaternion.Euler(-currentTiltAngle, 0f, 0f);
    }

    private void HandleWeaponSway()
    {
        if (rightHandSocket == null) return;

        // Сглаживаем сам инпут (важно для резких движений мыши)
        currentSwayInput = Vector2.SmoothDamp(currentSwayInput, targetSwayInput, ref swayInputVelocity, swaySmoothTime);

        float moveX = Mathf.Clamp(-currentSwayInput.x * swayAmount, -maxSway, maxSway);
        float moveY = Mathf.Clamp(-currentSwayInput.y * swayAmount, -maxSway, maxSway);
        Vector3 targetPos = initialHandLocalPos + new Vector3(moveX, moveY, 0f);

        float rotX = Mathf.Clamp(currentSwayInput.y * swayRotationAmount, -maxRotationSway, maxRotationSway);
        float rotY = Mathf.Clamp(-currentSwayInput.x * swayRotationAmount, -maxRotationSway, maxRotationSway);
        Quaternion targetRot = initialHandLocalRot * Quaternion.Euler(rotX, rotY, 0f);

        // Применяем поверх анимации (в LateUpdate)
        rightHandSocket.localPosition = targetPos;
        rightHandSocket.localRotation = targetRot;

        // Сброс инпута (чтобы рука возвращалась в центр, если мышь не двигается)
        targetSwayInput = Vector2.zero; 
    }

    private void HandleHeadBoneIK()
    {
        if (headBone == null || cameraTransform == null) return;
        Quaternion target = cameraTransform.rotation * Quaternion.Euler(headRotationOffset);
        headBone.rotation = Quaternion.Slerp(headBone.rotation, target, headLookWeight);
    }

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

    public void HandleAnimatorIK(int layerIndex)
    {
        if (animator == null) return;

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

    private void OnAnimatorIK(int layerIndex) => HandleAnimatorIK(layerIndex);

    private IEnumerator ReachForRoutine(Transform target)
    {
        reachTarget = target;
        const float reachDuration = 0.25f;
        const float holdDuration = 0.15f;

        for (float t = 0f; t < reachDuration; t += Time.deltaTime)
        {
            reachWeight = Mathf.SmoothStep(0f, 1f, t / reachDuration);
            yield return null;
        }
        reachWeight = 1f;

        yield return new WaitForSeconds(holdDuration);

        for (float t = 0f; t < reachDuration; t += Time.deltaTime)
        {
            reachWeight = Mathf.SmoothStep(1f, 0f, t / reachDuration);
            yield return null;
        }

        reachWeight = 0f;
        reachTarget = null;
    }
}