using UnityEngine;
using Mirror;

[RequireComponent(typeof(Rigidbody))]
public class CarHybridSystem : NetworkBehaviour
{
    [Header("Integration")]
    [Tooltip("Ссылка на менеджер ресурсов (висит на этом же объекте)")]
    [SerializeField] private CarResourceManager resourceManager;
    [SerializeField] private Transform worldContainer;
    
    [Header("Settings")]
    [SerializeField] private float virtualSpeed = 20f;
    [SyncVar] public int playersInCar = 0;

    [Header("Driving Settings")]
    [SerializeField] private float motorForce = 1500f;
    [SerializeField] private float steerForce = 100f;

    private float currentSteer;
    private float currentAccel;
    
    [SyncVar(hook = nameof(OnModeChanged))] 
    public bool isRoadMillMode = false;

    private Rigidbody rb;

    private void Awake() => rb = GetComponent<Rigidbody>();

    [Server]
    public void UpdatePassengerCount(int amount)
    {
        playersInCar += amount;
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
    }

    public void LocalDrive(float steerInput, float accelInput)
    {
        currentSteer = steerInput;
        currentAccel = accelInput;
    }

    private void FixedUpdate()
    {
        // 1. АНТИ-ПРИЗРАК: Если мы вышли из машины, жестко обнуляем инпут
        if (!isOwned)
        {
            currentAccel = 0f;
            currentSteer = 0f;
            return; 
        }

        if (isRoadMillMode) return;

        // 2. ИММОБИЛАЙЗЕР: Если бензина нет, сбрасываем газ в ноль
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
    }
}