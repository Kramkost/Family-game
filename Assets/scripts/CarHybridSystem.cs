using UnityEngine;
using Mirror;
using System;
using Random = UnityEngine.Random;

/// <summary>
/// Серверно-авторитетная система управления автомобилем с сетевым визуалом
/// и улучшенной физикой подвески/колес.
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
    [SerializeField] private float crashThreshold = 5f;

    [Header("Network Visuals & Audio")]
    [SerializeField] private AudioSource engineAudio;
    [SerializeField] private float idlePitch = 0.8f;
    [SerializeField] private float maxPitch = 2.0f;
    
    [SerializeField] private AudioSource fxAudioSource;
    [SerializeField] private AudioClip hornSound;
    [SerializeField] private AudioClip lightSwitchSound;
    [SerializeField] private AudioClip brakeSquealSound;
    [SerializeField] private AudioClip engineStartSound;
    [SerializeField] private AudioClip engineStopSound;
    [SerializeField] private AudioClip[] crashSounds;
    
    [SerializeField] private GameObject[] headlights; 

    [Header("Wheels & Steering")]
    [SerializeField] private Transform steeringWheel;
    [SerializeField] private float maxSteeringAngle = 90f;
    [SerializeField] private Transform[] frontWheels; // ВАЖНО: оси колес должны быть выровнены по локальному Z вперед
    [SerializeField] private Transform[] rearWheels;
    [SerializeField] private float wheelVisualRadius = 0.35f;
    
    [Header("Pedals")]
    [SerializeField] private Transform gasPedal;
    [SerializeField] private Transform brakePedal;
    [SerializeField] private Vector3 pedalTravel = new Vector3(0.05f, 0, 0); 

    [Header("Visual Polish (Body Sway)")]
    [SerializeField] private Transform carModel;
    [SerializeField] private float maxPitchAngle = 4f; 
    [SerializeField] private float maxRollAngle = 6f;  
    [SerializeField] private float tiltSmoothTime = 0.15f;

    [Header("Camera Polish")]
    [SerializeField] private float minFOV = 60f;
    [SerializeField] private float maxFOV = 85f;
    [SerializeField] private float speedForMaxFOV = 25f;
    [SerializeField] private float fovLerpSpeed = 3f;

    [Header("Engine Health States")]
    [SyncVar] public float engineSpeedModifier = 1f;
    [SyncVar(hook = nameof(OnEngineDeadChanged))] public bool isEngineDead = false;      
    [SyncVar(hook = nameof(OnLightsChanged))] public bool lightsOn;
    [SyncVar(hook = nameof(OnEngineOnChanged))] public bool isEngineOn = false;
    [SyncVar(hook = nameof(OnModeChanged))] public bool isRoadMillMode = false;

    // Синхронизируемые инпуты
    [SyncVar] private float syncSteer;
    [SyncVar] private float syncAccel;

    // Локальные переменные
    private float currentSteer;
    private float currentAccel;
    private float lastSentSteer;
    private float lastSentAccel;
    private float nextSyncTime;
    private const float SYNC_RATE = 0.1f; // Лимит отправки пакетов ввода

    private Rigidbody rb;
    private Camera mainCam;

    private Vector3 initialGasPos;
    private Vector3 initialBrakePos;
    private Quaternion initialSteeringRot;

    // Сглаживание крена
    private float currentPitch;
    private float currentRoll;
    private float pitchVelocity;
    private float rollVelocity;

    // Вращение колес
    private float wheelSpinAngle;
    private bool isBraking = false;

    // --- ПУБЛИЧНЫЕ СВОЙСТВА ДЛЯ UI ---
    public float CurrentSpeedKMH => rb != null ? rb.linearVelocity.magnitude * 3.6f : 0f;
    public float FuelNormalized => resourceManager != null ? Mathf.Clamp01(resourceManager.gasoline / resourceManager.maxGasoline) : 0f;

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
        if (engineAudio != null)
        {
            engineAudio.loop = true;
            engineAudio.Stop(); 
        }
    }

    [ServerCallback]
    private void OnCollisionEnter(Collision collision)
    {
        if (collision.relativeVelocity.magnitude > crashThreshold)
        {
            RpcPlayCrashSound();
        }
    }

    [ClientRpc]
    private void RpcPlayCrashSound()
    {
        if (fxAudioSource != null && crashSounds != null && crashSounds.Length > 0)
        {
            PlayRandomizedFX(crashSounds[Random.Range(0, crashSounds.Length)]);
        }
    }

    [Server]
    public void UpdatePassengerCount(int amount)
    {
        playersInCar += amount;
        
        if (playersInCar > 0 && !isEngineOn && !isEngineDead) isEngineOn = true;
        else if (playersInCar == 0 && isEngineOn) isEngineOn = false;
        
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

    private void OnEngineOnChanged(bool oldState, bool newState)
    {
        if (newState)
        {
            if (engineStartSound != null) PlayRandomizedFX(engineStartSound);
            if (engineAudio != null && !isEngineDead) engineAudio.Play();
        }
        else
        {
            if (engineStopSound != null) PlayRandomizedFX(engineStopSound);
            if (engineAudio != null) engineAudio.Stop();
        }
    }

    private void OnEngineDeadChanged(bool oldState, bool newState)
    {
        if (newState && isEngineOn) 
        {
            if (engineStopSound != null) PlayRandomizedFX(engineStopSound);
            if (engineAudio != null) engineAudio.Stop();
        }
        else if (!newState && isEngineOn) 
        {
            if (engineStartSound != null) PlayRandomizedFX(engineStartSound);
            if (engineAudio != null) engineAudio.Play();
        }
    }

    private void OnLightsChanged(bool oldState, bool newState)
    {
        if (headlights != null)
        {
            foreach (GameObject light in headlights)
            {
                if (light != null) light.SetActive(newState);
            }
        }
        if (lightSwitchSound != null) PlayRandomizedFX(lightSwitchSound);
    }

    private void PlayRandomizedFX(AudioClip clip, float volume = 1f)
    {
        if (fxAudioSource != null && clip != null)
        {
            fxAudioSource.pitch = Random.Range(0.9f, 1.1f);
            fxAudioSource.PlayOneShot(clip, volume);
        }
    }

    private void Update()
    {
        if (isRoadMillMode && worldContainer != null && resourceManager != null && resourceManager.gasoline > 0 && !isEngineDead && isEngineOn)
        {
            worldContainer.Translate(-transform.forward * (virtualSpeed * engineSpeedModifier) * Time.deltaTime, Space.World);
        }

        // Визуал работает для всех клиентов на основе синхронизированных переменных
        HandleVisualPolish();
        HandleWheelsAnimation();
        HandleInteriorAnimation();
        HandleEngineSound();
        HandleBrakeSoundFX();
        
        if (isOwned)
        {
            HandleCameraFOV();
        }
    }

    public void ToggleLightsUI()
    {
        if (isOwned) CmdToggleLights();
    }

    public void HonkUI()
    {
        if (isOwned) CmdHonkHorn();
    }

    [Command]
    public void CmdToggleLights()
    {
        lightsOn = !lightsOn; // Раньше тут было пусто
    }

    [Command]
    public void CmdHonkHorn() => RpcHonkHorn();

    [ClientRpc]
    private void RpcHonkHorn()
    {
        if (hornSound != null) PlayRandomizedFX(hornSound);
    }

    public void LocalDrive(float steerInput, float accelInput)
    {
        currentSteer = steerInput;
        currentAccel = accelInput;

        // Rate Limiting для сети, чтобы не спамить RPC каждый кадр
        if (Time.time > nextSyncTime)
        {
            if (Mathf.Abs(lastSentSteer - steerInput) > 0.05f || Mathf.Abs(lastSentAccel - accelInput) > 0.05f)
            {
                CmdSyncInputs(steerInput, accelInput);
                lastSentSteer = steerInput;
                lastSentAccel = accelInput;
                nextSyncTime = Time.time + SYNC_RATE;
            }
        }
    }

    [Command]
    private void CmdSyncInputs(float steer, float accel)
    {
        syncSteer = steer;
        syncAccel = accel;
    }

    private void FixedUpdate()
    {
        if (!isOwned || isRoadMillMode) return; 

        if (resourceManager != null && resourceManager.gasoline <= 0 || isEngineDead || !isEngineOn)
        {
            currentAccel = 0f;
        }

        // Физика
        Vector3 force = transform.forward * currentAccel * (motorForce * engineSpeedModifier) * Time.fixedDeltaTime;
        rb.AddForce(force, ForceMode.Acceleration);

        float forwardSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);
        float turnMultiplier = Mathf.Clamp(Mathf.Abs(forwardSpeed) / 5f, 0f, 1f);
        float directionMultiplier = Mathf.Sign(forwardSpeed);

        float turn = currentSteer * steerForce * turnMultiplier * directionMultiplier * Time.fixedDeltaTime;
        rb.MoveRotation(rb.rotation * Quaternion.Euler(0f, turn, 0f));

        ApplyLateralFriction();
    }

    private void HandleWheelsAnimation()
    {
        // Вычисляем вращение колес на основе реальной скорости машины
        float forwardSpeed = isRoadMillMode ? (virtualSpeed * engineSpeedModifier) : Vector3.Dot(rb.linearVelocity, transform.forward);
        float spinDelta = (forwardSpeed / wheelVisualRadius) * Mathf.Rad2Deg * Time.deltaTime;
        wheelSpinAngle += spinDelta;

        float steerAngle = syncSteer * 35f; // Максимальный угол поворота передних колес

        if (frontWheels != null)
        {
            foreach (var wheel in frontWheels)
            {
                if (wheel != null) wheel.localRotation = Quaternion.Euler(wheelSpinAngle, steerAngle, 0f);
            }
        }

        if (rearWheels != null)
        {
            foreach (var wheel in rearWheels)
            {
                if (wheel != null) wheel.localRotation = Quaternion.Euler(wheelSpinAngle, 0f, 0f);
            }
        }
    }

    private void HandleInteriorAnimation()
    {
        if (steeringWheel != null)
        {
            float targetAngle = syncSteer * -maxSteeringAngle;
            Quaternion zRotation = Quaternion.AngleAxis(targetAngle, Vector3.forward);
            steeringWheel.localRotation = Quaternion.Lerp(steeringWheel.localRotation, initialSteeringRot * zRotation, Time.deltaTime * 10f);
        }

        if (gasPedal != null)
        {
            Vector3 targetGas = initialGasPos + (syncAccel > 0 ? pedalTravel : Vector3.zero);
            gasPedal.localPosition = Vector3.Lerp(gasPedal.localPosition, targetGas, Time.deltaTime * 10f);
        }

        if (brakePedal != null)
        {
            Vector3 targetBrake = initialBrakePos + (syncAccel < 0 ? pedalTravel : Vector3.zero);
            brakePedal.localPosition = Vector3.Lerp(brakePedal.localPosition, targetBrake, Time.deltaTime * 10f);
        }
    }

    private void HandleEngineSound()
    {
        if (engineAudio == null || !engineAudio.isPlaying) return;
        
        float speed = rb.linearVelocity.magnitude;
        float pitchTarget = Mathf.Lerp(idlePitch, maxPitch, speed / speedForMaxFOV);
        
        if (Mathf.Abs(syncAccel) > 0.1f && speed < 5f) pitchTarget += 0.2f;

        // Плавное изменение питча, без резких скачков от Random
        engineAudio.pitch = Mathf.Lerp(engineAudio.pitch, pitchTarget, Time.deltaTime * 5f);
    }

    private void HandleBrakeSoundFX()
    {
        if (fxAudioSource == null || brakeSquealSound == null) return;

        float forwardSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);
        bool shouldBrake = forwardSpeed > 8f && syncAccel < -0.1f;

        if (shouldBrake && !isBraking)
        {
            isBraking = true;
            PlayRandomizedFX(brakeSquealSound, 0.7f);
        }
        else if (!shouldBrake && isBraking)
        {
            isBraking = false; 
        }
    }

    private void HandleVisualPolish()
    {
        if (carModel == null) return;

        // Крен зависит от ускорения и скорости поворота
        float targetPitchAngle = syncAccel * -maxPitchAngle;
        
        float forwardSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);
        float speedFactor = Mathf.Clamp01(Mathf.Abs(forwardSpeed) / 15f);
        float targetRollAngle = syncSteer * -maxRollAngle * speedFactor;

        // SmoothDamp дает мягкое затухание, имитируя пружины подвески
        currentPitch = Mathf.SmoothDamp(currentPitch, targetPitchAngle, ref pitchVelocity, tiltSmoothTime);
        currentRoll = Mathf.SmoothDamp(currentRoll, targetRollAngle, ref rollVelocity, tiltSmoothTime);

        carModel.localRotation = Quaternion.Euler(currentPitch, 0f, currentRoll);
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

    [Server]
    public void UpdateEngineState(int stage)
    {
        switch (stage)
        {
            case 0: 
                engineSpeedModifier = 1f;
                isEngineDead = false;
                break;
            case 1: 
                engineSpeedModifier = 0.7f;
                isEngineDead = false;
                break;
            case 2: 
                engineSpeedModifier = 0.4f;
                isEngineDead = false;
                break;
            case 3: 
                engineSpeedModifier = 0f;
                isEngineDead = true;
                break;
        }
    }
}