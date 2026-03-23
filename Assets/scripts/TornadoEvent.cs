using UnityEngine;
using Mirror;

namespace Kotenkoff
{
    /// <summary>
    /// Торнадо. Бродит по локации и засасывает игроков (CharacterController) и машины/пропсы (Rigidbody).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class TornadoEvent : NetworkBehaviour
    {
        [Header("Настройки Торнадо")]
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float pullRadius = 40f;
        [SerializeField] private float pullForce = 50f;
        [SerializeField] private float liftForce = 20f;

        [Header("Визуал")]
        [Tooltip("Объект воронки (для вращения)")]
        [SerializeField] private Transform tornadoMesh;
        [SerializeField] private float spinSpeed = 300f;

        private Rigidbody rb;
        private Vector2 moveDirection;
        private float directionChangeTimer;

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
            Collider[] colliders = Physics.OverlapSphere(transform.position, pullRadius);

            foreach (var col in colliders)
            {
                
                Vector3 directionToTornado = transform.position - col.transform.position;
                float distance = directionToTornado.magnitude;
                
              
                if (distance < 0.1f) distance = 0.1f; 
                
                float forceMultiplier = 1f - (distance / pullRadius);

                Vector3 pull = directionToTornado.normalized * pullForce * forceMultiplier;
                Vector3 swirl = Vector3.Cross(directionToTornado.normalized, Vector3.up) * pullForce * forceMultiplier;
                Vector3 lift = Vector3.up * liftForce * forceMultiplier;

                Vector3 totalForce = (pull + swirl + lift) * Time.fixedDeltaTime;

                // --- 1. ЕСЛИ ЭТО ИГРОК (CharacterController) ---
                if (col.TryGetComponent(out CharacterController charController))
                {
                    charController.Move(totalForce);
                    continue; 
                }

                // --- 2. ЕСЛИ ЭТО МАШИНА ИЛИ ФИЗИЧЕСКИЙ ПРОП (Rigidbody) ---
                if (col.TryGetComponent(out Rigidbody targetRb))
                {
                    if (targetRb == rb) continue; 

                   
                    if (targetRb.isKinematic)
                    {
                       
                        if (col.GetComponent<CarHybridSystem>())
                        {
                            targetRb.isKinematic = false; 
                        }
                        else
                        {
                            continue; 
                        }
                    }

                    targetRb.AddForce(totalForce, ForceMode.VelocityChange);
                }
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1, 0, 0, 0.3f);
            Gizmos.DrawWireSphere(transform.position, pullRadius);
        }
#endif
    }
}