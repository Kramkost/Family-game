using Kotenkoff;
using Mirror;
using UnityEngine;
using UnityEngine.UI;

namespace Health_Bar_System
{
    public class PlayerHUD : NetworkBehaviour
    {
        [Header("UI References")]
        [Tooltip("Сам объект Canvas. Мы будем выключать его у чужих игроков.")]
        [SerializeField] private GameObject hudCanvas;
        
        [Tooltip("Та самая красная полоска с режимом Filled")]
        [SerializeField] private Image healthFill;

        [Tooltip("Черный или темно-красный экран на весь Canvas для эффекта голода")]
        [SerializeField] private Image darknessOverlay; 
        
        [Header("Logic References")]
        [SerializeField] private PlayerStats stats;

        [Header("Hunger FX Settings")]
        [Tooltip("При каком уровне голода начинаются эффекты (например, при 30 из 100)")]
        [SerializeField] private float hungerWarningThreshold = 30f;
        [Tooltip("Максимальная непрозрачность тьмы (1 = ничего не видно, 0.8 = почти ничего)")]
        [SerializeField] private float maxDarknessAlpha = 0.85f;
        
        [Header("Hunger Audio")]
        [SerializeField] private AudioSource stomachAudioSource;
        [SerializeField] private AudioClip[] rumbleSounds;
        [Tooltip("Как часто урчит живот (в секундах)")]
        [SerializeField] private float rumbleInterval = 12f;

        private float rumbleTimer = 0f;

        public override void OnStartClient()
        {
            if (!isLocalPlayer)
            {
                hudCanvas.SetActive(false);
            }
        }

        public override void OnStartLocalPlayer()
        {
            hudCanvas.SetActive(true);

            if (stats != null)
            {
                stats.ClientOnHealthChanged += UpdateHealthBar;
                UpdateHealthBar(stats.currentHealth, 150f); 
            }

            // Делаем так, чтобы первый звук урчания проигрался почти сразу, как наступит голод
            rumbleTimer = 2f; 
        }

        private void Update()
        {
            // Обрабатываем эффекты только для себя
            if (!isLocalPlayer || stats == null) return;

            HandleHungerEffects();
        }

        private void HandleHungerEffects()
        {
            float currentHunger = stats.currentHunger;

            // --- 1. ЗАТЕМНЕНИЕ ЭКРАНА ---
            if (darknessOverlay != null)
            {
                float targetAlpha = 0f;

                if (currentHunger < hungerWarningThreshold)
                {
                    // Высчитываем "тяжесть" голода от 0 до 1
                    float severity = 1f - (currentHunger / hungerWarningThreshold);
                    targetAlpha = severity * maxDarknessAlpha;
                }

                // Плавно меняем прозрачность, чтобы экран не "моргал" при резких скачках статов
                Color c = darknessOverlay.color;
                c.a = Mathf.Lerp(c.a, targetAlpha, Time.deltaTime * 2f);
                darknessOverlay.color = c;
            }

            // --- 2. УРЧАНИЕ В ЖИВОТЕ ---
            if (currentHunger < hungerWarningThreshold && rumbleSounds != null && rumbleSounds.Length > 0 && stomachAudioSource != null)
            {
                rumbleTimer -= Time.deltaTime;
                if (rumbleTimer <= 0f)
                {
                    PlayRumbleSound();
                    rumbleTimer = rumbleInterval; 
                }
            }
            else
            {
              
                rumbleTimer = 2f; 
            }
        }

        private void PlayRumbleSound()
        {
            
            stomachAudioSource.pitch = Random.Range(0.9f, 1.1f);
            
            AudioClip clip = rumbleSounds[Random.Range(0, rumbleSounds.Length)];
            stomachAudioSource.PlayOneShot(clip);
        }

        private void UpdateHealthBar(float currentHealth, float maxHealth)
        {
            if (healthFill != null)
            {
                healthFill.fillAmount = currentHealth / maxHealth;
            }
        }

        private void OnDestroy()
        {
            if (stats != null)
            {
                stats.ClientOnHealthChanged -= UpdateHealthBar;
            }
        }
    }
}