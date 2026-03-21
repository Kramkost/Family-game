using UnityEngine;
using Mirror;

namespace Kotenkoff
{
    /// <summary>
    /// Физическая модель тягача. Догоняет игроков, крутит колеса, издает звуки.
    /// Работает в связке с NetworkTransform для плавной синхронизации движений.
    /// </summary>

    [RequireComponent(typeof(Rigidbody))]
    public class TractorEntity : NetworkBehaviour
    {
        [Header("Движение")]
        [Tooltip("Скорость сближения с машиной игроков (насколько он быстрее их)")]
        [SerializeField] private float catchUpSpeed = 6f;
        
        [Header("Визуал")]
        [Tooltip("Массив колес для вращения")]
        [SerializeField] private Transform[] wheels;
        [SerializeField] private float wheelRadius = 1f;

        [Header("Звук")]
        [SerializeField] private AudioSource engineAudio;
        [SerializeField] private AudioSource hornAudio;
        [SerializeField] private AudioClip appearHornSound;

        private Rigidbody rb;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.isKinematic = true; 
        }

        private void Start()
        {
            // При спавне (у всех клиентов) воспроизводим рев сирены/гудка
            if (hornAudio != null && appearHornSound != null)
            {
                hornAudio.PlayOneShot(appearHornSound);
            }

            if (engineAudio != null)
            {
                engineAudio.loop = true;
                engineAudio.Play();
            }
        }

        private void Update()
        {
            // --- ЛОКАЛЬНЫЙ ВИЗУАЛ (Колеса и звук) ---
            
      
            float distanceThisFrame = catchUpSpeed * Time.deltaTime;
            float rotationAngle = (distanceThisFrame / (2 * Mathf.PI * wheelRadius)) * 360f;

            foreach (Transform wheel in wheels)
            {
                if (wheel != null)
                {
                    
                    wheel.Rotate(Vector3.right, rotationAngle, Space.Self);
                }
            }
        }

        private void FixedUpdate()
        {
            // --- СЕРВЕРНАЯ ФИЗИКА (Движение) ---
            if (!isServer) return;

            
            rb.MovePosition(rb.position + transform.forward * catchUpSpeed * Time.fixedDeltaTime);
        }

        // --- ЛОГИКА СТОЛКНОВЕНИЯ ---
        [ServerCallback]
        private void OnTriggerEnter(Collider other)
        {
            // Если тягач догнал машину игроков (предполагаем, что у нее тег "PlayerCar")
            if (other.CompareTag("PlayerCar"))
            {
                Debug.Log("ТЯГАЧ ДОГНАЛ МАШИНУ! КОНЕЦ ИГРЫ!");
                
                // Здесь ты можешь вызвать взрыв, нанести урон или перезапустить уровень
                // например: other.GetComponent<CarHybridSystem>().Explode();
            }
        }
    }
}