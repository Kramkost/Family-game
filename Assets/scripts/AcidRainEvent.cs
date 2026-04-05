using System.Collections;
using Mirror;
using UnityEngine;
using Health_Bar_System; // Подключаем систему здоровья

namespace Game_Multiplayer_System
{
    public class AcidRainEvent : NetworkBehaviour
    {
        [Header("Настройки Дождя")]
        [SerializeField] private float rainRadius = 200f;
        [SerializeField] private float damagePerSecond = 5f; // Float для IDamageable
        
        [Tooltip("Слой укрытий (крыши зданий, кузов машины).")]
        [SerializeField] private LayerMask roofLayerMask;

        [Header("Визуал и Звук")]
        [SerializeField] private AudioSource rainAudio;
        
        // Zero GC массив
        private Collider[] overlapResults = new Collider[20];

        public override void OnStartServer()
        {
            StartCoroutine(DamageLoop());
        }

        private void Start()
        {
            // Start отрабатывает и на сервере, и на клиенте. Звук будет у всех.
            if (rainAudio != null)
            {
                rainAudio.loop = true;
                rainAudio.pitch = Random.Range(0.95f, 1.05f); // Немного рандома для атмосферы
                rainAudio.Play();
            }
        }

        [Server]
        private IEnumerator DamageLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(1f);

                int hitCount = Physics.OverlapSphereNonAlloc(transform.position, rainRadius, overlapResults);
                
                for (int i = 0; i < hitCount; i++)
                {
                    Collider hit = overlapResults[i];
                    if (hit == null) continue;

                    // Проверяем наличие интерфейса здоровья (сработает и на игроках, и на ломающихся пропсах)
                    IDamageable damageable = hit.GetComponentInParent<IDamageable>();
                    
                    if (damageable != null)
                    {
                        // Смещаем старт луча на 1 метр вверх (уровень груди), чтобы не попасть в собственные ноги
                        Vector3 rayStart = hit.transform.position + Vector3.up * 1f;

                        // Пускаем луч вверх на 20 метров. Если ничего из roofLayerMask не задели — получаем урон
                        if (!Physics.Raycast(rayStart, Vector3.up, 20f, roofLayerMask))
                        {
                            damageable.TakeDamage(damagePerSecond);
                            // Debug.Log($"[AcidRain] Цель {hit.name} получает урон от кислоты!");
                        }
                    }
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0, 1, 0, 0.2f);
            Gizmos.DrawWireSphere(transform.position, rainRadius);
        }
    }
}