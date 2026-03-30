using UnityEngine;
using Mirror;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Плавно оглушает звуки улицы и приглушает громкость, когда игрок заходит в фургон.
/// Использует AudioLowPassFilter только для игроков внутри зоны.
/// </summary>
public class AudioZoneController : NetworkBehaviour
{
    [Header("Настройки фильтра низких частот")]
    public float muffleFrequency = 1500f; // Глухой звук в салоне
    public float normalFrequency = 22000f; // Чистый звук на улице
    public float filterTransitionSpeed = 5f; // Скорость перехода фильтра

    [Header("Настройки приглушения громкости")]
    public Collider zoneCollider; // Коллайдер зоны фургона
    public float volumeFadeDuration = 1f; // Длительность плавного изменения громкости
    public float muteLevel = 0.4f; // Уровень приглушения (0.0 — полностью тихо, 1.0 — полная громкость)

    [Header("Чёрный список звуков (не приглушаются)")]
    public AudioSource[] blacklistedAudioSources; // Звуки из чёрного списка

    private AudioSource[] allAudioSources; // Все найденные AudioSource на сцене
    private float[] originalVolumes; // Исходные уровни громкости
    private HashSet<AudioSource> blacklistSet; // Оптимизированная коллекция для быстрого поиска
    private Coroutine currentVolumeFadeCoroutine; // Для управления корутинами приглушения громкости

    // Храним информацию о фильтрах игроков в зоне
    private Dictionary<GameObject, AudioLowPassFilter> playerFiltersInZone = new Dictionary<GameObject, AudioLowPassFilter>();

    void Start()
    {
        // Создаём HashSet для быстрого поиска звуков из чёрного списка
        blacklistSet = new HashSet<AudioSource>(blacklistedAudioSources);

        // Находим все AudioSource на сцене
        allAudioSources = FindObjectsOfType<AudioSource>();

        // Сохраняем исходные уровни громкости
        originalVolumes = new float[allAudioSources.Length];
        for (int i = 0; i < allAudioSources.Length; i++)
        {
            originalVolumes[i] = allAudioSources[i].volume;
        }

        // Если коллайдер не назначен, используем коллайдер этого объекта
        if (zoneCollider == null)
            zoneCollider = GetComponent<Collider>();
    }

    void Update()
    {
        // Обновляем фильтры для всех игроков в зоне
        foreach (var kvp in playerFiltersInZone)
        {
            AudioLowPassFilter filter = kvp.Value;
            if (filter != null)
            {
                float targetFreq = muffleFrequency; // Игрок внутри зоны — глухой звук
                filter.cutoffFrequency = Mathf.Lerp(
                    filter.cutoffFrequency,
            targetFreq,
            Time.deltaTime * filterTransitionSpeed
        );
            }
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            // Находим AudioLowPassFilter на игроке
            AudioLowPassFilter playerFilter = other.GetComponentInChildren<AudioLowPassFilter>();
            if (playerFilter != null)
            {
                // Добавляем игрока в словарь с его фильтром
                playerFiltersInZone[other.gameObject] = playerFilter;
                playerFilter.enabled = true;

                // Запускаем приглушение звуков (если ещё не запущено)
                if (currentVolumeFadeCoroutine == null)
                {
                    currentVolumeFadeCoroutine = StartCoroutine(FadeAudio(muteLevel));
                }
            }
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            // Удаляем игрока из словаря фильтров
            if (playerFiltersInZone.ContainsKey(other.gameObject))
            {
                playerFiltersInZone.Remove(other.gameObject);
            }

            // Если в зоне больше нет игроков, восстанавливаем громкость
            if (currentVolumeFadeCoroutine == null)
            {
                currentVolumeFadeCoroutine = StartCoroutine(FadeAudio(1f));
            }
        }
    }

    IEnumerator FadeAudio(float targetMultiplier)
    {
        float elapsedTime = 0f;

        while (elapsedTime < volumeFadeDuration)
        {
            elapsedTime += Time.deltaTime;
            float currentMultiplier = Mathf.Lerp(
                targetMultiplier == muteLevel ? 1f : muteLevel,
                targetMultiplier,
                elapsedTime / volumeFadeDuration
            );

            // Применяем новый уровень громкости ко всем звукам, кроме тех, что в чёрном списке
            for (int i = 0; i < allAudioSources.Length; i++)
            {
                // Пропускаем звуки из чёрного списка
                if (blacklistSet.Contains(allAudioSources[i]))
                    continue;

                allAudioSources[i].volume = originalVolumes[i] * currentMultiplier;
            }

            yield return null;
        }

        // Финальное применение
        for (int i = 0; i < allAudioSources.Length; i++)
        {
            if (blacklistSet.Contains(allAudioSources[i]))
                continue;

            allAudioSources[i].volume = originalVolumes[i] * targetMultiplier;
        }

        currentVolumeFadeCoroutine = null;
    }
}