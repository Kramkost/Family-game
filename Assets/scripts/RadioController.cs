using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

public class RadioController : NetworkBehaviour
{
    [Header("Аудиоисточники")]
    public AudioSource musicSource;
    public AudioSource transitionNoiseSource;
    [Tooltip("Источник для воспроизведения постоянных помех при поломке")]
    public AudioSource constantNoiseSource;

    [Header("Аудиоклипы")]
    [Tooltip("Массив музыкальных треков для воспроизведения")]
    [SerializeField] private AudioClip[] musicClips;
    [Tooltip("Короткий клип помех между треками")]
    [SerializeField] private AudioClip transitionNoiseClip;
    [Tooltip("Клип постоянных помех при поломке радио")]
    [SerializeField] private AudioClip constantNoiseClip;

    [Header("Настройки звука")]
    [Tooltip("Минимальная высота тона для эффектов помех")]
    public float minPitch = 0.8f;
    [Tooltip("Максимальная высота тона для эффектов помех")]
    public float maxPitch = 1.2f;
    [Tooltip("Максимальный уровень громкости для эффектов помех")]
    public float minVolume = 0.3f;
    [Tooltip("Минимальный уровень громкости для эффектов помех")]
    public float maxVolume = 0.7f;

    private bool isBroken = false;
    
    [Tooltip("Включить постоянные помехи, когда радио сломано")]
    public bool constantNoiseEnabled = false;

    private List<int> lastPlayedIndices = new();
    private const int maxLastPlayed = 3; // Максимальное количество запоминаемых последних треков

    /// <summary>
    /// Инициализация при старте объекта.
    /// Только сервер запускает воспроизведение радио для синхронизации между клиентами.
    /// </summary>
    void Start()
    {
        if (isServer)
        {
            StartRadio();
        }
    }

    /// <summary>
    /// Запускает корутину воспроизведения радиопоследовательности.
    /// Выполняется только на сервере для обеспечения сетевой синхронизации.
    /// </summary>
    [Server]
    private void StartRadio()
    {
        StartCoroutine(PlayRadioSequence());
    }

    /// <summary>
    /// Основная корутина, управляющая воспроизведением радио.
    /// Логика:
    /// - Если радио работает: воспроизводит случайную музыку → помехи между треками
    /// - Если сломано и включены помехи: воспроизводит постоянные помехи
    /// - Если сломано без помех: ждёт
    /// Периодически (10 % шанс) ломает радио.
    /// </summary>
    [Server]
    private IEnumerator PlayRadioSequence()
    {
        while (true)
        {
            if (!isBroken)
            {
                // Выбираем случайную мелодию, исключая последние 3
                int nextTrackIndex = GetRandomTrackIndex();
                if (nextTrackIndex == -1) continue;

                // Запоминаем индекс для исключения повторов
                lastPlayedIndices.Add(nextTrackIndex);
                if (lastPlayedIndices.Count > maxLastPlayed)
                {
                    lastPlayedIndices.RemoveAt(0);
                }

                // Воспроизводим музыку
                musicSource.clip = musicClips[nextTrackIndex];
                musicSource.Play();

                // Ждём окончания музыки
                yield return new WaitForSeconds(musicClips[nextTrackIndex].length);

                // Воспроизводим помехи между треками
                PlayTransitionNoise();

                // Ждём окончания помех
                yield return new WaitForSeconds(transitionNoiseClip.length);
            }
            else if (constantNoiseEnabled)
            {
                // При поломке и включённом переключателе — бесконечные помехи
                PlayConstantNoise();
                yield return new WaitForSeconds(constantNoiseClip.length);
            }
            else
            {
                // Если сломано и переключатель выключен — просто ждём
                yield return new WaitForSeconds(1f);
            }

            // Периодически ломаем радио (10 % шанс каждую минуту)
            if (Random.Range(0f, 100f) < 10f && !isBroken)
            {
                BreakRadio();
            }
        }
    }

    /// <summary>
    /// Выбирает случайный трек, исключая последние несколько сыгранных.
    /// Если все треки были сыграны, сбрасывает историю и выбирает случайный.
    /// </summary>
    /// <returns>Индекс выбранного трека или -1, если выбор невозможен</returns>
    [Server]
    private int GetRandomTrackIndex()
    {
        List<int> availableIndices = new();

        for (int i = 0; i < musicClips.Length; i++)
        {
            if (!lastPlayedIndices.Contains(i))
            {
                availableIndices.Add(i);
            }
        }

        if (availableIndices.Count == 0)
        {
            // Если все треки были сыграны, сбрасываем историю
            lastPlayedIndices.Clear();
            return Random.Range(0, musicClips.Length);
        }

        return availableIndices[Random.Range(0, availableIndices.Count)];
    }

    /// <summary>
    /// Воспроизводит короткие помехи между музыкальными треками.
    /// Устанавливает случайные параметры высоты тона и громкости.
    /// </summary>
    [Server]
    private void PlayTransitionNoise()
    {
        SetRandomAudioParameters(transitionNoiseSource);
        transitionNoiseSource.clip = transitionNoiseClip;
        transitionNoiseSource.Play();
    }

    /// <summary>
    /// Воспроизводит постоянные помехи при поломке радио.
    /// Устанавливает случайные параметры высоты тона и громкости.
    /// </summary>
    [Server]
    private void PlayConstantNoise()
    {
        SetRandomAudioParameters(constantNoiseSource);
        constantNoiseSource.clip = constantNoiseClip;
        constantNoiseSource.Play();
    }

    /// <summary>
    /// Устанавливает случайные значения высоты тона и громкости для указанного источника звука.
    /// Используется для создания вариативности звучания помех.
    /// </summary>
    /// <param name="audioSource">Аудиоисточник, параметры которого нужно изменить</param>
    private void SetRandomAudioParameters(AudioSource audioSource)
    {
        audioSource.pitch = Random.Range(minPitch, maxPitch);
        audioSource.volume = Random.Range(minVolume, maxVolume);
    }

    /// <summary>
    /// Команда от клиента на починку радио.
    /// Вызывает серверный метод RepairRadio().
    /// </summary>
    [Command]
    public void CmdRepairRadio()
    {
        RepairRadio();
    }

    /// <summary>
    /// Ремонтирует радио: сбрасывает флаги поломки и помех,
    /// останавливает текущие помехи и перезапускает последовательность воспроизведения.
    /// </summary>
    [Server]
    private void RepairRadio()
    {
        isBroken = false;
        constantNoiseEnabled = false;

        // Останавливаем постоянные помехи, если они играют
        if (constantNoiseSource.isPlaying)
        {
            constantNoiseSource.Stop();
        }

        // Перезапускаем последовательность
        StopCoroutine(PlayRadioSequence());
        StartCoroutine(PlayRadioSequence());
    }

    /// <summary>
    /// Ломает радио (устанавливает флаг isBroken = true).
    /// Помехи включаются только если constantNoiseEnabled == true.
    /// </summary>
    [Server]
    private void BreakRadio()
    {
        isBroken = true;
    }

    /// <summary>
    /// Команда от клиента для включения/выключения постоянных помех при поломке.
    /// Обновляет значение constantNoiseEnabled на сервере.
    /// </summary>
    /// <param name="enable">Флаг включения помех (true — включить, false — выключить)</param>
    [Command]
    public void CmdToggleConstantNoise(bool enable)
    {
        constantNoiseEnabled = enable;
    }
}