using UnityEngine;
using Mirror;

/// <summary>
/// Автономный менеджер ресурсов. Замеряет реальную скорость и жжет топливо.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class CarResourceManager : NetworkBehaviour
{
    [Header("Integration")]
    [Tooltip("Ссылка на скрипт передвижения для проверки режима беговой дорожки")]
    [SerializeField] private CarHybridSystem hybridSystem;

    [Header("Resources (Max 100)")]
    [SyncVar] public float gasoline = 100f;
    [SyncVar] public float water = 100f;
    [SyncVar] public float engineOil = 100f;

    [Header("Consumption Rates (Per Second)")]
    [SerializeField] private float gasConsumeRate = 1f;

    private Rigidbody rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    [ServerCallback]
    private void Update()
    {
        if (hybridSystem == null) return;

        // Машина тратит ресурсы, если включена беговая дорожка ИЛИ ее физическая скорость выше 0.5 юнитов
        bool isMoving = hybridSystem.isRoadMillMode || rb.linearVelocity.magnitude > 0.5f;

        if (isMoving && gasoline > 0)
        {
            gasoline = Mathf.Clamp(gasoline - gasConsumeRate * Time.deltaTime, 0, 100);
            
            if (gasoline <= 0)
            {
                Debug.LogWarning("[CarResources] Бак пуст. Машина заглохла.");
                // Вызов RPC звука глохнущего мотора
            }
        }
    }

    [Server]
    public void Refuel(float amount)
    {
        gasoline = Mathf.Clamp(gasoline + amount, 0, 100);
    }
}