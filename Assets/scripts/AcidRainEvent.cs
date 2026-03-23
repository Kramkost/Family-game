using UnityEngine;
using Mirror;
using System.Collections;

namespace Kotenkoff
{
    /// <summary>
    /// Кислотный дождь. Наносит урон игрокам, если они не находятся под укрытием.
    /// </summary>
    public class AcidRainEvent : NetworkBehaviour
    {
        [Header("Настройки Дождя")]
        [Tooltip("Радиус действия дождя (чтобы не бить игроков на другом конце карты)")]
        [SerializeField] private float rainRadius = 200f;
        
        [Tooltip("Урон в секунду")]
        [SerializeField] private int damagePerSecond = 5;
        
        [Tooltip("Слой укрытий (машина, крыши зданий). Дождь не пробьет эти слои.")]
        [SerializeField] private LayerMask roofLayerMask;

        [Header("Визуал и Звук")]
        [SerializeField] private AudioSource rainAudio;

        public override void OnStartServer()
        {

            StartCoroutine(DamageLoop());
        }

        private void Start()
        {
          
            if (rainAudio != null)
            {
                rainAudio.loop = true;
                rainAudio.Play();
            }
        }

        [Server]
        private IEnumerator DamageLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(1f);

           
                Collider[] hits = Physics.OverlapSphere(transform.position, rainRadius);
                foreach (var hit in hits)
                {
                    if (hit.TryGetComponent(out PlayerEntity player))
                    {
                       
                        bool isUnderRoof = Physics.Raycast(player.transform.position, Vector3.up, 20f, roofLayerMask);

                        if (!isUnderRoof)
                        {
                            // TODO: ЕСЛИ ВЫ ЭТО ЧИТАЕТЕ ДОБАВЬТЕ СЮДА систему здоровья
                          
                            Debug.Log($"[AcidRain] Игрок {player.name} обжигается кислотой! Урон: {damagePerSecond}");
                        }
                    }
                }
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0, 1, 0, 0.2f);
            Gizmos.DrawWireSphere(transform.position, rainRadius);
        }
#endif
    }
}