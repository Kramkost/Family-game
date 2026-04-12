using UnityEngine;
using Mirror;
using System.Collections;
using Kotenkoff;

public class RoadMonster : NetworkBehaviour
{
    [Header("Detection")]
    [SerializeField] private float sightRadius = 45f;
    [SerializeField] private float fieldOfView = 50f;
    [SerializeField] private LayerMask obstacleLayer;

    [Header("Physics & Damage")]
    [SerializeField] private float repelForce = 80000f;

    [Header("Visuals & Sound")]
    [SerializeField] private Animator animator;
    [SerializeField] private AudioClip scareSound;
    [SerializeField] private float jumpscareDuration = 1.0f; 

    [Header("Spawn Animation")]
    [SerializeField] private bool useScaleSpawn = true;
    [SerializeField] private float spawnDuration = 1.5f;

    [Header("Temporary Teleport Logic")]
    [SerializeField] private bool useRandomTeleport = false;
    [SerializeField] private float minTeleportTime = 5f;
    [SerializeField] private float maxTeleportTime = 15f;
    [SerializeField] private float teleportDistance = 20f;

    [SyncVar] private bool isTriggered = false;
    private Camera mainCamera;
    private static readonly int JumpscareTrigger = Animator.StringToHash("Jumpscare");
    private static readonly int SpawnTrigger = Animator.StringToHash("Spawn");

    public override void OnStartServer()
    {
        base.OnStartServer();
        if (useRandomTeleport)
        {
            StartCoroutine(TeleportRoutine());
        }
    }

    private IEnumerator TeleportRoutine()
    {
        while (!isTriggered)
        {
            yield return new WaitForSeconds(Random.Range(minTeleportTime, maxTeleportTime));

            if (isTriggered) yield break;

            PlayerEntity[] players = FindObjectsByType<PlayerEntity>(FindObjectsSortMode.None);
            if (players.Length > 0)
            {
                PlayerEntity target = players[Random.Range(0, players.Length)];
                Vector3 spawnPos = target.transform.position + target.transform.forward * teleportDistance;
                
                if (Physics.Raycast(spawnPos + Vector3.up * 10f, Vector3.down, out RaycastHit hit, 20f, obstacleLayer))
                    spawnPos.y = hit.point.y;

                transform.position = spawnPos;
                transform.rotation = Quaternion.Euler(0, target.transform.eulerAngles.y - 180f, 0); // Смотрим на игрока
                
                RpcTeleportVisuals();
            }
        }
    }

    [ClientRpc]
    private void RpcTeleportVisuals()
    {
        if (useScaleSpawn && !isTriggered)
        {
            StartCoroutine(SpawnAnimationRoutine());
        }
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        mainCamera = Camera.main;

        if (animator != null) animator.SetTrigger(SpawnTrigger);
        if (useScaleSpawn) StartCoroutine(SpawnAnimationRoutine());
    }

    private IEnumerator SpawnAnimationRoutine()
    {
        Vector3 targetScale = transform.localScale;
        transform.localScale = Vector3.zero;
        float elapsed = 0f;

        while (elapsed < spawnDuration)
        {
            transform.localScale = Vector3.Lerp(Vector3.zero, targetScale, elapsed / spawnDuration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        transform.localScale = targetScale;
    }

    private void Update()
    {
        if (isServer && isTriggered) return;
        if (!isClient || isTriggered || mainCamera == null) return;

        Vector3 dirToMonster = transform.position - mainCamera.transform.position;
        float distance = dirToMonster.magnitude;

        if (distance <= sightRadius)
        {
            float angle = Vector3.Angle(mainCamera.transform.forward, dirToMonster);
            if (angle <= fieldOfView)
            {
                if (!Physics.Raycast(mainCamera.transform.position, dirToMonster.normalized, distance, obstacleLayer))
                {
                    CmdPlayerSawMe();
                }
            }
        }
    }

    [Command(requiresAuthority = false)]
    private void CmdPlayerSawMe()
    {
        if (isTriggered) return;
        TriggerJumpscareSequence();
    }

    [ServerCallback]
    private void OnTriggerEnter(Collider other)
    {
        if (isTriggered) return;

        CarHybridSystem car = other.GetComponentInParent<CarHybridSystem>();
        if (car != null)
        {
            ApplyPhysicsImpulse(car);
            TriggerJumpscareSequence();
        }
    }

    [Server]
    private void TriggerJumpscareSequence()
    {
        isTriggered = true;
        RpcDoJumpscare();
        // Даем анимации проиграться перед удалением объекта
        Invoke(nameof(DestroyMonster), jumpscareDuration);
    }

    [ClientRpc]
    private void RpcDoJumpscare()
    {
        if (animator != null) animator.SetTrigger(JumpscareTrigger);

        if (scareSound != null && mainCamera != null)
        {
            AudioSource.PlayClipAtPoint(scareSound, mainCamera.transform.position, 1f);
        }
    }

    private void ApplyPhysicsImpulse(CarHybridSystem car)
    {
        Rigidbody carRb = car.GetComponent<Rigidbody>();
        if (carRb != null && !carRb.isKinematic)
        {
            carRb.linearVelocity = Vector3.zero;
            Vector3 pushDir = (carRb.position - transform.position).normalized;
            pushDir.y = 0.5f;
            carRb.AddForce(pushDir * repelForce, ForceMode.Impulse);
        }
        else
        {
            car.UpdateEngineState(3);
        }
    }

    [Server]
    private void DestroyMonster() => NetworkServer.Destroy(gameObject);
}