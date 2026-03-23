using UnityEngine;
using Mirror;

/// <summary>
/// Серверно-авторитетная система управления автомобилем с полным сетевым визуалом
/// (звук, фары, руль, педали) и защитой от "игрока-халка".
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class CarHybridSystem : NetworkBehaviour
{
    [Header("Integration")]
    [SerializeField] private CarResourceManager resourceManager;
    [SerializeField] private Transform worldContainer;
    
    [Header("Core Settings")]
    [SerializeField] private bool useRoadMill = false;
    [SerializeField] private float virtualSpeed = 20f;
    [SyncVar] public int playersInCar = 0;

    [Header("Driving Settings")]
    [SerializeField] private float motorForce = 1500f;
    [SerializeField] private float steerForce = 100f;

    [Header("Network Visuals (Фары, Звук, Салон)")]
    [SerializeField] private AudioSource engineAudio;
    [SerializeField] private float idlePitch = 0.8f;
    [SerializeField] private float maxPitch = 2.0f;
    
    [SerializeField] private GameObject[] headlights; 
    
    [SerializeField] private Transform steeringWheel;
    [SerializeField] private float maxSteeringAngle = 90f; 
    
    [SerializeField] private Transform gasPedal;
    [SerializeField] private Transform brakePedal;
    [SerializeField] private Vector3 pedalTravel = new Vector3(0.05f, 0, 0); 

    [Header("Visual Polish (Sway & Pitch)")]
    [SerializeField] private Transform carModel;
    [SerializeField] private float pitchMultiplier = 3f; 
    [SerializeField] private float rollMultiplier = 4f;  
    [SerializeField] private float tiltLerpSpeed = 5f;

    [Header("Camera Polish")]
    [SerializeField] private float minFOV = 60f;
    [SerializeField] private float maxFOV = 85f;
    [SerializeField] private float speedForMaxFOV = 25f;
    [SerializeField] private float fovLerpSpeed = 3f;

    [SyncVar(hook = nameof(OnLightsChanged))] public bool lightsOn = false;
    [SyncVar] private float syncSteer;
    [SyncVar] private float syncAccel;
    
    [SyncVar(hook = nameof(OnModeChanged))] 
    public bool isRoadMillMode = false;

    private float currentSteer;
    private float currentAccel;
    
    private float lastSentSteer;
    private float lastSentAccel;

    private Rigidbody rb;
    private Camera mainCam;

    // Исходные позиции и повороты для анимации салона
    private Vector3 initialGasPos;
    private Vector3 initialBrakePos;
    private Vector3 initialSteeringEuler; 
    private Quaternion initialSteeringRot;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        
        rb.mass = 3500f; 
        rb.centerOfMass = new Vector3(0, -1.5f, 0); 
        rb.angularDamping = 2f; 

        if (gasPedal != null) initialGasPos = gasPedal.localPosition;
        if (brakePedal != null) initialBrakePos = brakePedal.localPosition;
        
     
        if (steeringWheel != null) initialSteeringRot = steeringWheel.localRotation;
    }

    private void Start()
    {
        mainCam = Camera.main;
        
        if (engineAudio != null && !engineAudio.isPlaying)
        {
            engineAudio.loop = true;
            engineAudio.Play();
        }
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

    private void OnLightsChanged(bool oldState, bool newState)
    {
        if (headlights == null) return;
        foreach (GameObject light in headlights)
        {
            if (light != null) light.SetActive(newState);
        }
    }

    private void Update()
    {
        if (isRoadMillMode && worldContainer != null && resourceManager != null && resourceManager.gasoline > 0)
        {
            worldContainer.Translate(-transform.forward * virtualSpeed * Time.deltaTime, Space.World);
        }

        HandleVisualPolish();
        HandleInteriorAnimation();
        HandleEngineSound();
        
        if (isOwned)
        {
            HandleCameraFOV();
        }
    }

    public void LocalDrive(float steerInput, float accelInput)
    {
        currentSteer = steerInput;
        currentAccel = accelInput;

        if (Mathf.Abs(lastSentSteer - steerInput) > 0.05f || Mathf.Abs(lastSentAccel - accelInput) > 0.05f)
        {
            CmdSyncInputs(steerInput, accelInput);
            lastSentSteer = steerInput;
            lastSentAccel = accelInput;
        }
    }

    [Command]
    private void CmdSyncInputs(float steer, float accel)
    {
        syncSteer = steer;
        syncAccel = accel;
    }

    [Command]
    public void CmdToggleLights()
    {
        lightsOn = !lightsOn;
    }

    private void FixedUpdate()
    {
        if (!isOwned) return; 
        if (isRoadMillMode) return;

        if (resourceManager != null && resourceManager.gasoline <= 0)
        {
            currentAccel = 0f;
        }

        Vector3 force = transform.forward * currentAccel * motorForce * Time.fixedDeltaTime;
        rb.AddForce(force, ForceMode.Acceleration);

        float forwardSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);
        float turnMultiplier = Mathf.Clamp(Mathf.Abs(forwardSpeed) / 5f, 0f, 1f);
        float directionMultiplier = Mathf.Sign(forwardSpeed);

        float turn = currentSteer * steerForce * turnMultiplier * directionMultiplier * Time.fixedDeltaTime;
        rb.MoveRotation(rb.rotation * Quaternion.Euler(0f, turn, 0f));

        ApplyLateralFriction();
    }

    // --- ЖЕЛЕЗОБЕТОННЫЙ БЛОК АНИМАЦИИ САЛОНА ---
    private void HandleInteriorAnimation()
    {
        // Руль (Вращение строго по оси Z)
        if (steeringWheel != null)
        {
            float targetAngle = syncSteer * -maxSteeringAngle;
            
            // Создаем вращение ТОЛЬКО вокруг локальной оси Z
            Quaternion zRotation = Quaternion.AngleAxis(targetAngle, Vector3.forward);
            
            // Накладываем это чистое Z-вращение поверх изначального наклона рулевой колонки
            Quaternion targetRotation = initialSteeringRot * zRotation;
            
            steeringWheel.localRotation = Quaternion.Lerp(steeringWheel.localRotation, targetRotation, Time.deltaTime * 10f);
        }

        // Газ
        if (gasPedal != null)
        {
            Vector3 targetGas = initialGasPos + (syncAccel > 0 ? pedalTravel : Vector3.zero);
            gasPedal.localPosition = Vector3.Lerp(gasPedal.localPosition, targetGas, Time.deltaTime * 10f);
        }

        // Тормоз
        if (brakePedal != null)
        {
            Vector3 targetBrake = initialBrakePos + (syncAccel < 0 ? pedalTravel : Vector3.zero);
            brakePedal.localPosition = Vector3.Lerp(brakePedal.localPosition, targetBrake, Time.deltaTime * 10f);
        }
    }

    private void HandleEngineSound()
    {
        if (engineAudio == null) return;
        
        float speed = rb.linearVelocity.magnitude;
        float pitchTarget = Mathf.Lerp(idlePitch, maxPitch, speed / speedForMaxFOV);
        
        if (Mathf.Abs(syncAccel) > 0.1f && speed < 5f) pitchTarget += 0.3f;

        engineAudio.pitch = Mathf.Lerp(engineAudio.pitch, pitchTarget, Time.deltaTime * 5f);
    }

    private void HandleVisualPolish()
    {
        if (carModel == null) return;
        float targetPitch = syncAccel * -pitchMultiplier;
        float targetRoll = syncSteer * -rollMultiplier;
        Quaternion targetRotation = Quaternion.Euler(targetPitch, 0f, targetRoll);
        carModel.localRotation = Quaternion.Lerp(carModel.localRotation, targetRotation, Time.deltaTime * tiltLerpSpeed);
    }

    private void HandleCameraFOV()
    {
        if (mainCam == null) return;
        float speed = rb.linearVelocity.magnitude;
        float speedRatio = Mathf.Clamp01(speed / speedForMaxFOV);
        float targetFOV = Mathf.Lerp(minFOV, maxFOV, speedRatio);
        mainCam.fieldOfView = Mathf.Lerp(mainCam.fieldOfView, targetFOV, Time.deltaTime * fovLerpSpeed);
    }

    private void ApplyLateralFriction()
    {
        Vector3 lateralVelocity = transform.right * Vector3.Dot(rb.linearVelocity, transform.right);
        rb.AddForce(-lateralVelocity * rb.mass * 2f, ForceMode.Force);
    }
}