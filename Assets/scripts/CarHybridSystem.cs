using UnityEngine;
using Mirror;

/// <summary>
/// Серверно-авторитетная система управления автомобилем.
/// Поддерживает гибридный режим (честная физика / RoadMill),
/// а также локальный визуальный полишинг (крен кузова, динамический FOV).
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class CarHybridSystem : NetworkBehaviour
{
    [Header("Integration")]
    [SerializeField] private CarResourceManager resourceManager;
    [SerializeField] private Transform worldContainer;
    
    [Header("Core Settings")]
    [Tooltip("Включить иллюзию беговой дорожки. Выключи для честной GTA-подобной физики.")]
    [SerializeField] private bool useRoadMill = false;
    [SerializeField] private float virtualSpeed = 20f;
    [SyncVar] public int playersInCar = 0;

    [Header("Driving Settings")]
    [SerializeField] private float motorForce = 1500f;
    [SerializeField] private float steerForce = 100f;

    [Header("Visual Polish (Sway & Pitch)")]
    [Tooltip("Ссылка на саму 3D-модель машины (вложенный объект без физики).")]
    [SerializeField] private Transform carModel;
    [SerializeField] private float pitchMultiplier = 3f; // Наклон вперед/назад
    [SerializeField] private float rollMultiplier = 4f;  // Крен влево/вправо (Sway)
    [SerializeField] private float tiltLerpSpeed = 5f;

    [Header("Camera Polish")]
    [SerializeField] private float minFOV = 60f;
    [SerializeField] private float maxFOV = 85f;
    [SerializeField] private float speedForMaxFOV = 25f;
    [SerializeField] private float fovLerpSpeed = 3f;

    private float currentSteer;
    private float currentAccel;
    
    [SyncVar(hook = nameof(OnModeChanged))] 
    public bool isRoadMillMode = false;

    private Rigidbody rb;
    private Camera mainCam;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        
        
        rb.centerOfMass = new Vector3(0, -0.5f, 0); 
    }

    private void Start()
    {
        mainCam = Camera.main;
    }

    [Server]
    public void UpdatePassengerCount(int amount)
    {
        playersInCar += amount;
        CheckMode();
    }

    [Server]
    private void CheckMode()
    {
        if (!useRoadMill)
        {
            if (isRoadMillMode) isRoadMillMode = false;
            return;
        }

        int totalPlayers = NetworkServer.connections.Count;
        if (totalPlayers == 0) return;

        bool shouldBeRoadMill = (playersInCar >= totalPlayers);
        if (isRoadMillMode != shouldBeRoadMill) isRoadMillMode = shouldBeRoadMill;
    }

    private void OnModeChanged(bool oldMode, bool newMode)
    {
        rb.isKinematic = newMode;
        if (newMode)
        {
            transform.position = new Vector3(0, transform.position.y, 0);
            rb.linearVelocity = Vector3.zero;
        }
    }

    private void Update()
    {
        if (isRoadMillMode && worldContainer != null && resourceManager != null && resourceManager.gasoline > 0)
        {
            worldContainer.Translate(-transform.forward * virtualSpeed * Time.deltaTime, Space.World);
        }

        
        if (isOwned)
        {
            HandleVisualPolish();
            HandleCameraFOV();
        }
    }

    public void LocalDrive(float steerInput, float accelInput)
    {
        currentSteer = steerInput;
        currentAccel = accelInput;
    }

    private void FixedUpdate()
    {
        if (!isOwned)
        {
            currentAccel = 0f;
            currentSteer = 0f;
            return; 
        }

        if (isRoadMillMode) return;

        if (resourceManager != null && resourceManager.gasoline <= 0)
        {
            currentAccel = 0f;
        }

        // Физическое движение
        Vector3 force = transform.forward * currentAccel * motorForce * Time.fixedDeltaTime;
        rb.AddForce(force, ForceMode.Acceleration);

        // Рулежка
        float forwardSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);
        float turnMultiplier = Mathf.Clamp(Mathf.Abs(forwardSpeed) / 5f, 0f, 1f);
        float directionMultiplier = Mathf.Sign(forwardSpeed);

        float turn = currentSteer * steerForce * turnMultiplier * directionMultiplier * Time.fixedDeltaTime;
        rb.MoveRotation(rb.rotation * Quaternion.Euler(0f, turn, 0f));

        
        ApplyLateralFriction();
    }

    /// <summary>
    /// Анимация наклонов кузова (Pitch и Roll).
    /// </summary>
    private void HandleVisualPolish()
    {
        if (carModel == null) return;

        
        float targetPitch = currentAccel * -pitchMultiplier;
        
       
        float targetRoll = currentSteer * -rollMultiplier;

        Quaternion targetRotation = Quaternion.Euler(targetPitch, 0f, targetRoll);
        carModel.localRotation = Quaternion.Lerp(carModel.localRotation, targetRotation, Time.deltaTime * tiltLerpSpeed);
    }

    /// <summary>
    /// Эффект скорости через расширение угла обзора.
    /// </summary>
    private void HandleCameraFOV()
    {
        if (mainCam == null) return;

        float speed = rb.linearVelocity.magnitude;
        float speedRatio = Mathf.Clamp01(speed / speedForMaxFOV);
        float targetFOV = Mathf.Lerp(minFOV, maxFOV, speedRatio);

        mainCam.fieldOfView = Mathf.Lerp(mainCam.fieldOfView, targetFOV, Time.deltaTime * fovLerpSpeed);
    }

    /// <summary>
    /// Простая симуляция сцепления шин с дорогой (KISS-подход).
    /// </summary>
    private void ApplyLateralFriction()
    {
        
        Vector3 lateralVelocity = transform.right * Vector3.Dot(rb.linearVelocity, transform.right);
        
        
        rb.AddForce(-lateralVelocity * rb.mass * 2f, ForceMode.Force);
    }
}