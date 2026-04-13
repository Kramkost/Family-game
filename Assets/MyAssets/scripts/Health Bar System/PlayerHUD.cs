using Kotenkoff;
using Mirror;
using UnityEngine;
using UnityEngine.UI;

namespace Health_Bar_System
{
    public class PlayerHUD : NetworkBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject hudCanvas;
        [SerializeField] private Image healthFill;
        [SerializeField] private Image darknessOverlay; 
        
        [Header("Animation & Fading Settings")]
        [Tooltip("CanvasGroup, висящий на панели здоровья (для прозрачности)")]
        [SerializeField] private CanvasGroup healthCanvasGroup;
        [Tooltip("RectTransform самой панели здоровья (для масштаба)")]
        [SerializeField] private RectTransform healthRectTransform;
        [SerializeField] private float fadeSpeed = 5f;
        
        [Header("Logic References")]
        [SerializeField] private PlayerStats stats;

        [Header("Hunger FX Settings")]
        [SerializeField] private float hungerWarningThreshold = 30f;
        [SerializeField] private float maxDarknessAlpha = 0.85f;
        
        [Header("Hunger Audio")]
        [SerializeField] private AudioSource stomachAudioSource;
        [SerializeField] private AudioClip[] rumbleSounds;
        [SerializeField] private float rumbleInterval = 12f;

        private float rumbleTimer = 0f;
        
        // Переменные для анимации
        private float lastInteractionTime;
        private float hideDelay;
        private float previousHealth;
        private float targetHealthScale = 1f;
        private float targetFillAmount = 1f;

        public override void OnStartClient()
        {
            if (!isLocalPlayer) hudCanvas.SetActive(false);
        }

        public override void OnStartLocalPlayer()
        {
            hudCanvas.SetActive(true);

            if (stats != null)
            {
                stats.ClientOnHealthChanged += OnHealthChangedEvent;
                previousHealth = stats.currentHealth;
                targetFillAmount = stats.currentHealth / 150f; // Учитываем твой макс ХП 150
                if (healthFill != null) healthFill.fillAmount = targetFillAmount;
            }

            rumbleTimer = 2f; 
            ResetVisibilityTimer();
        }

        private void Update()
        {
            if (!isLocalPlayer || stats == null) return;

            HandleHungerEffects();
            HandleHealthAnimations();
        }

        private void HandleHealthAnimations()
        {
            if (healthCanvasGroup == null || healthRectTransform == null) return;

            // Плавное изменение самой полоски ХП
            if (healthFill != null)
            {
                healthFill.fillAmount = Mathf.Lerp(healthFill.fillAmount, targetFillAmount, Time.deltaTime * fadeSpeed);
            }

            // Логика появления / исчезновения

            if (Time.time - lastInteractionTime > hideDelay)
            {
                // Скрываем и уменьшаем
                healthCanvasGroup.alpha = Mathf.Lerp(healthCanvasGroup.alpha, 0f, Time.deltaTime * fadeSpeed);
                healthRectTransform.localScale = Vector3.Lerp(healthRectTransform.localScale, Vector3.one * 0.8f, Time.deltaTime * fadeSpeed);
            }
            else

            {
                // Показываем
                healthCanvasGroup.alpha = Mathf.Lerp(healthCanvasGroup.alpha, 1f, Time.deltaTime * fadeSpeed * 1.5f);
                
                // Плавно возвращаем масштаб к 1 (или применяем импульс урона)
                healthRectTransform.localScale = Vector3.Lerp(healthRectTransform.localScale, Vector3.one * targetHealthScale, Time.deltaTime * fadeSpeed * 2f);
                targetHealthScale = Mathf.Lerp(targetHealthScale, 1f, Time.deltaTime * fadeSpeed);
            }
        }

        private void HandleHungerEffects()
        {
            float currentHunger = stats.currentHunger;

            if (darknessOverlay != null)
            {
                float targetAlpha = 0f;
                if (currentHunger < hungerWarningThreshold)
                {
                    float severity = 1f - (currentHunger / hungerWarningThreshold);
                    targetAlpha = severity * maxDarknessAlpha;
                }
                Color c = darknessOverlay.color;
                c.a = Mathf.Lerp(c.a, targetAlpha, Time.deltaTime * 2f);
                darknessOverlay.color = c;
            }

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

        // Вызывается событием SyncVar из PlayerStats
        private void OnHealthChangedEvent(float currentHealth, float maxHealth)
        {
            targetFillAmount = currentHealth / maxHealth;

            // Если получили урон
            if (currentHealth < previousHealth)
            {
                targetHealthScale = 1.3f; // Импульс увеличения
            }

            previousHealth = currentHealth;
            ResetVisibilityTimer();
        }

        private void ResetVisibilityTimer()
        {
            lastInteractionTime = Time.time;
            hideDelay = Random.Range(2f, 4f); // Рандомная задержка по твоему ТЗ
        }

        private void OnDestroy()
        {
            if (stats != null)
            {
                stats.ClientOnHealthChanged -= OnHealthChangedEvent;
            }
        }
    }
}