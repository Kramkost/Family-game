using System.Collections;
using Mirror;
using UnityEngine;
using Health_Bar_System; 

namespace Game_Multiplayer_System
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class CartMonster : NetworkBehaviour
    {
        public enum MonsterState { Idle, Chasing, Exploding }

        [Header("Sensors & Vision")]
        [Tooltip("Радиус обзора 32 лучей")]
        [SerializeField] private float visionRange = 15f;
        [Tooltip("Угол обзора в градусах (180 = полукруг спереди)")]
        [SerializeField] private float visionAngle = 180f;
        [Tooltip("Слои, которые преграждают зрение (стены) или являются игроком")]
        [SerializeField] private LayerMask visionMask;
        [Tooltip("Слой самого игрока (для точной идентификации)")]
        [SerializeField] private LayerMask playerLayer;

        [Header("Movement & Combat")]
        [SerializeField] private float moveSpeed = 12f;
        [SerializeField] private float rotationSpeed = 8f;
        [SerializeField] private float explosionRadius = 6f;
        [Tooltip("Урон, который пойдет в метод TakeDamage твоего PlayerStats")]
        [SerializeField] private float explosionDamage = 100f; 
        [SerializeField] private float explosionForce = 1500f;

        [Header("Game Feel: Audio")]
        [SerializeField] private AudioSource sfxSource;
        [SerializeField] private AudioSource engineSource;
        [SerializeField] private AudioClip[] alertSounds;
        [SerializeField] private AudioClip[] explosionSounds;
        [SerializeField] private AudioClip[] engineLoops;

        [Header("Game Feel: Visuals")]
        [SerializeField] private Transform[] wheels;
        [SerializeField] private float wheelRotationSpeedMultiplier = 50f;
        [SerializeField] private GameObject explosionVfxPrefab;

        [Header("Debug")]
        [SerializeField] private bool showDebug = true;
        [SerializeField] private Transform sensorOrigin; 

        [SyncVar]
        private MonsterState currentState = MonsterState.Idle;

        private Rigidbody rb;
        private Transform targetPlayer;
        
        // Zero GC кэширование
        private Vector3[] cachedRayDirections = new Vector3[32];
        private Collider[] overlapResults = new Collider[10];
        private float currentSpeed;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            if (sensorOrigin == null) sensorOrigin = transform;
            
            PrecalculateRays();
        }

        public override void OnStartClient()
        {
            if (engineLoops != null && engineLoops.Length > 0 && engineSource != null)
            {
                engineSource.clip = engineLoops[Random.Range(0, engineLoops.Length)];
                engineSource.loop = true;
                engineSource.pitch = Random.Range(0.9f, 1.1f);
                engineSource.Play();
            }
        }

        // ===================================================================================
        // SERVER LOGIC (Authority)
        // ===================================================================================

        [ServerCallback]
        private void FixedUpdate()
        {
            switch (currentState)
            {
                case MonsterState.Idle:
                    ServerHandleSensors();
                    break;
                case MonsterState.Chasing:
                    ServerHandleChasing();
                    break;
                case MonsterState.Exploding:
                    // Ждем уничтожения
                    break;
            }
        }

        [Server]
        private void ServerHandleSensors()
        {
            for (int i = 0; i < cachedRayDirections.Length; i++)
            {
                Vector3 worldDir = transform.rotation * cachedRayDirections[i];
                
                if (Physics.Raycast(sensorOrigin.position, worldDir, out RaycastHit hit, visionRange, visionMask))
                {
                    // Битовая маска: проверяем, что луч попал именно в игрока
                    if (((1 << hit.collider.gameObject.layer) & playerLayer) != 0)
                    {
                        targetPlayer = hit.collider.transform;
                        currentState = MonsterState.Chasing;
                        RpcPlayAlert();
                        break;
                    }
                }
            }
        }

        [Server]
        private void ServerHandleChasing()
        {
            if (targetPlayer == null)
            {
                currentState = MonsterState.Idle;
                return;
            }

            Vector3 directionToTarget = targetPlayer.position - rb.position;
            directionToTarget.y = 0; // Игнорируем Y, чтобы тележка ехала ровно по полу

            float distance = directionToTarget.magnitude;

            if (distance <= explosionRadius * 0.5f)
            {
                ServerTriggerExplosion();
                return;
            }

            if (directionToTarget.sqrMagnitude > 0.01f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(directionToTarget);
                rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRotation, Time.fixedDeltaTime * rotationSpeed));
            }

            rb.MovePosition(rb.position + transform.forward * (moveSpeed * Time.fixedDeltaTime));
        }

        [Server]
        private void ServerTriggerExplosion()
        {
            currentState = MonsterState.Exploding;
            RpcPlayExplosion();

            // Взрыв без GC аллокаций
            int hitCount = Physics.OverlapSphereNonAlloc(rb.position, explosionRadius, overlapResults);
            
            for (int i = 0; i < hitCount; i++)
            {
                Collider col = overlapResults[i];
                if (col == null) continue;

                // 1. Отбрасываем физикой (если у игрока/объекта есть Rigidbody)
                if (col.TryGetComponent<Rigidbody>(out Rigidbody targetRb))
                {
                    targetRb.AddExplosionForce(explosionForce, rb.position, explosionRadius, 1.5f, ForceMode.Impulse);
                }

                // 2. Связка с твоим PlayerStats (наносим урон)
                // GetComponentInParent нужен на случай, если коллайдер игрока висит на дочернем объекте, а скрипт в корне
                IDamageable damageable = col.GetComponentInParent<IDamageable>();
                if (damageable != null)
                {
                    // При желании можно сделать Falloff урона: чем дальше от эпицентра, тем меньше урон
                    float distanceToTarget = Vector3.Distance(rb.position, col.transform.position);
                    float damagePercent = 1f - Mathf.Clamp01(distanceToTarget / explosionRadius);
                    float finalDamage = explosionDamage * damagePercent;

                    // Вызов твоего метода TakeDamage(float amount)
                    damageable.TakeDamage(Mathf.Max(finalDamage, 10f)); // Минимум 10 урона, если задело краем
                }
            }

            StartCoroutine(DestroyAfterDelay());
        }

        [Server]
        private IEnumerator DestroyAfterDelay()
        {
            yield return new WaitForSeconds(0.1f);
            NetworkServer.Destroy(gameObject);
        }

        // ===================================================================================
        // CLIENT LOGIC (Visuals & Game Feel)
        // ===================================================================================

        private void Update()
        {
            if (isServer) return; 

            ClientHandleAnimations();
        }

        [Client]
        private void ClientHandleAnimations()
        {
            float targetSpeed = (currentState == MonsterState.Chasing) ? moveSpeed : 0f;
            currentSpeed = Mathf.Lerp(currentSpeed, targetSpeed, Time.deltaTime * 5f);

            if (engineSource != null)
            {
                engineSource.pitch = 0.8f + (currentSpeed / moveSpeed) * 0.4f;
            }

            if (wheels != null)
            {
                float rotationStep = currentSpeed * wheelRotationSpeedMultiplier * Time.deltaTime;
                foreach (var wheel in wheels)
                {
                    if (wheel != null)
                    {
                        wheel.Rotate(Vector3.right, rotationStep, Space.Self);
                    }
                }
            }
        }

        [ClientRpc]
        private void RpcPlayAlert()
        {
            PlayRandomSfx(alertSounds);
        }

        [ClientRpc]
        private void RpcPlayExplosion()
        {
            PlayRandomSfx(explosionSounds);

            if (explosionVfxPrefab != null)
            {
                Instantiate(explosionVfxPrefab, transform.position, Quaternion.identity);
            }
        }

        private void PlayRandomSfx(AudioClip[] clips)
        {
            if (clips == null || clips.Length == 0 || sfxSource == null) return;
            
            sfxSource.pitch = Random.Range(0.9f, 1.1f);
            sfxSource.PlayOneShot(clips[Random.Range(0, clips.Length)]);
        }

        // ===================================================================================
        // UTILS & DEBUG
        // ===================================================================================

        private void PrecalculateRays()
        {
            float angleStep = visionAngle / (cachedRayDirections.Length - 1);
            float startAngle = -visionAngle / 2f;

            for (int i = 0; i < cachedRayDirections.Length; i++)
            {
                float currentAngle = startAngle + (angleStep * i);
                cachedRayDirections[i] = Quaternion.Euler(0, currentAngle, 0) * Vector3.forward;
            }
        }

        private void OnDrawGizmos()
        {
            if (!showDebug || sensorOrigin == null) return;

            Gizmos.color = new Color(1f, 0f, 0f, 0.2f);
            Gizmos.DrawSphere(transform.position, explosionRadius);

            Gizmos.color = currentState == MonsterState.Chasing ? Color.yellow : Color.cyan;
            
            if (Application.isPlaying)
            {
                foreach (var dir in cachedRayDirections)
                {
                    Vector3 worldDir = transform.rotation * dir;
                    Gizmos.DrawRay(sensorOrigin.position, worldDir * visionRange);
                }
            }
            else
            {
                float angleStep = visionAngle / 31f;
                float startAngle = -visionAngle / 2f;
                for (int i = 0; i < 32; i++)
                {
                    float angle = startAngle + (angleStep * i);
                    Vector3 dir = transform.rotation * Quaternion.Euler(0, angle, 0) * Vector3.forward;
                    Gizmos.DrawRay(sensorOrigin.position, dir * visionRange);
                }
            }
        }
    }
}