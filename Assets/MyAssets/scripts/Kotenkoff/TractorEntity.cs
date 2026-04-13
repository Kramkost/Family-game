using UnityEngine;
using Mirror;
using System; // Нужно для событий

namespace Kotenkoff
{
    [RequireComponent(typeof(Rigidbody))]
    public class TractorEntity : NetworkBehaviour
    {
        [Header("Движение")]
        [SerializeField] private float catchUpSpeed = 6f;
        
        [Header("Визуал")]
        [SerializeField] private Transform[] wheels;
        [SerializeField] private float wheelRadius = 1f;

        [Header("Звук")]
        [SerializeField] private AudioSource engineAudio;
        [SerializeField] private AudioSource hornAudio;
        [SerializeField] private AudioClip appearHornSound;

        private Rigidbody rb;

        // --- СЕНЬОРСКАЯ ФИШКА: Статическое событие ---
        // Любой скрипт в игре (например, UI) сможет подписаться на это и узнать, есть ли тягач на карте.
        public static event Action<bool> OnTractorPresenceChanged;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.isKinematic = true; 
        }

        // Вызывается Mirror у ВСЕХ клиентов, когда объект появляется в их зоне видимости
        public override void OnStartClient()
        {
            base.OnStartClient();
            
            // Сообщаем всему локальному UI: "ТЯГАЧ ЗДЕСЬ! ПОКАЖИ ТЕКСТ!"
            OnTractorPresenceChanged?.Invoke(true);

            if (hornAudio != null && appearHornSound != null)
                hornAudio.PlayOneShot(appearHornSound);

            if (engineAudio != null)
            {
                engineAudio.loop = true;
                engineAudio.Play();
            }
        }

        // Вызывается Mirror, когда объект уничтожается или игрок отходит далеко
        public override void OnStopClient()
        {
            base.OnStopClient();
            
            // Сообщаем локальному UI: "Тягач пропал, прячь текст"
            OnTractorPresenceChanged?.Invoke(false);
        }

        private void Update()
        {
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
            if (!isServer) return;
            rb.MovePosition(rb.position + transform.forward * catchUpSpeed * Time.fixedDeltaTime);
        }

        [ServerCallback]
        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("PlayerCar"))
            {
                Debug.Log("ТЯГАЧ ДОГНАЛ МАШИНУ! КОНЕЦ ИГРЫ!");
            }
        }
    }
}