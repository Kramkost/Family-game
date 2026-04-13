using Mirror;
using UnityEngine;

namespace Game_Multiplayer_System
{
    [RequireComponent(typeof(Rigidbody))]
    public class TornadoEvent : NetworkBehaviour
    {
        [Header("Настройки Торнадо")]
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float pullRadius = 40f;
        [SerializeField] private float pullForce = 50f;
        [SerializeField] private float liftForce = 20f;

        [Header("Визуал")]
        [SerializeField] private Transform tornadoMesh;
        [SerializeField] private float spinSpeed = 300f;

        private Rigidbody rb;
        private Vector2 moveDirection;
        private float directionChangeTimer;

        // Zero GC массив для физики
        private Collider[] overlapResults = new Collider[20];

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.isKinematic = true; 
        }

        private void Update()
        {
            if (tornadoMesh != null)
            {
                tornadoMesh.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.Self);
            }
        }

        private void FixedUpdate()
        {
            if (!isServer) return; 

            HandleRandomMovement();
            SuckObjects();
        }

        [Server]
        private void HandleRandomMovement()
        {
            directionChangeTimer -= Time.fixedDeltaTime;
            if (directionChangeTimer <= 0)
            {
                moveDirection = Random.insideUnitCircle.normalized;
                directionChangeTimer = Random.Range(3f, 6f);
            }

            Vector3 movement = new Vector3(moveDirection.x, 0, moveDirection.y) * moveSpeed * Time.fixedDeltaTime;
            rb.MovePosition(rb.position + movement);
        }

        [Server]
        private void SuckObjects()
        {
            int hitCount = Physics.OverlapSphereNonAlloc(transform.position, pullRadius, overlapResults);

            for (int i = 0; i < hitCount; i++)
            {
                Collider col = overlapResults[i];
                if (col == null) continue;

                Vector3 directionToTornado = transform.position - col.transform.position;
                float distance = Mathf.Max(directionToTornado.magnitude, 0.1f);
                float forceMultiplier = 1f - (distance / pullRadius);

                Vector3 pull = directionToTornado.normalized * pullForce * forceMultiplier;
                Vector3 swirl = Vector3.Cross(directionToTornado.normalized, Vector3.up) * pullForce * forceMultiplier;
                Vector3 lift = Vector3.up * liftForce * forceMultiplier;

                Vector3 totalForce = (pull + swirl + lift) * Time.fixedDeltaTime;

                // --- 1. ИГРОКИ ---
                // Вместо прямого Move, отправляем силу на клиент
                if (col.TryGetComponent(out PlayerEntity player))
                {
                    // Вызываем метод у игрока (тебе нужно будет добавить его в PlayerEntity)
                    player.TargetApplyExternalForce(player.connectionToClient, totalForce);
                    continue; 
                }

                // --- 2. МАШИНА И ПРОПСЫ ---
                if (col.TryGetComponent(out Rigidbody targetRb))
                {
                    if (targetRb == rb) continue; 

                    if (targetRb.isKinematic)
                    {
                        // Твоя логика гибридной машины
                        if (col.GetComponent("CarHybridSystem")) // Проверка строкой, если нет using
                        {
                            targetRb.isKinematic = false; 
                        }
                        else continue; 
                    }

                    targetRb.AddForce(totalForce, ForceMode.VelocityChange);
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1, 0, 0, 0.3f);
            Gizmos.DrawWireSphere(transform.position, pullRadius);
        }
    }
}