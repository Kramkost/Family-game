using UnityEngine;
using Mirror;

/// <summary>
/// Компонент для отдельных деталей авто. Синхронизирует ХП и меняет визуал при поломке.
/// </summary>
public class CarPart : NetworkBehaviour
{
    [Header("Health")]
    [SerializeField] private float maxHealth = 100f;
    [SyncVar] private float currentHealth;

    [Header("Visuals & Audio")]
    [SerializeField] private GameObject intactModel;
    [SerializeField] private GameObject brokenModel;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip breakSound;

    // Хук вызывается у всех клиентов автоматически при изменении переменной isBroken
    [SyncVar(hook = nameof(OnBrokenStateChanged))]
    private bool isBroken = false;

    public override void OnStartServer()
    {
        currentHealth = maxHealth;
    }

    /// <summary>
    /// Нанесение урона детали. Вызывается только на сервере (например, при столкновении).
    /// </summary>
    [Server]
    public void TakeDamage(float damageAmount)
    {
        if (isBroken) return; // Уже сломано

        currentHealth -= damageAmount;

        if (currentHealth <= 0)
        {
            isBroken = true; // Триггерит хук у всех клиентов
        }
    }

    /// <summary>
    /// Клиентская логика: смена мешей и воспроизведение звука.
    /// </summary>
    private void OnBrokenStateChanged(bool oldState, bool newState)
    {
        if (newState == true)
        {
            if (intactModel != null) intactModel.SetActive(false);
            if (brokenModel != null) brokenModel.SetActive(true);
            
            if (audioSource != null && breakSound != null)
            {
                audioSource.PlayOneShot(breakSound);
            }
        }
    }
}