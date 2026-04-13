using UnityEngine;
using Mirror;
using System.Collections.Generic;
using System.Linq;

public class MusicManager : NetworkBehaviour
{
    [Header("Audio Settings")]
    public AudioSource audioSource;
    public List<AudioClip> musicClips = new List<AudioClip>();

    [Header("Network Sync")]
    [SyncVar(hook = nameof(OnCurrentTrackChanged))]
    private int currentTrackIndex = -1;

    private List<int> playedTracks = new List<int>();
    private bool isInitialized = false;

    /// <summary>
    /// Инициализация при запуске объекта.
    /// Устанавливает AudioSource, если он не назначен, делает объект устойчивым к смене сцен,
    /// и запускает систему воспроизведения музыки.
    /// </summary>
    void Start()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
        
        InitializeMusicSystem();
    }

    /// <summary>
    /// Основная инициализация системы музыки.
    /// Загружает историю сыгранных треков, перемешивает оставшиеся треки,
    /// устанавливает флаг инициализации. Если это сервер — запускает воспроизведение.
    /// </summary>
    private void InitializeMusicSystem()
    {
        if (isInitialized) return;
        
        LoadPlayedTracks();
        ShuffleUnplayedTracks();
        isInitialized = true;

        if (isServer)
        {
            StartPlayingNextTrack();
        }
    }

    /// <summary>
    /// Загружает из PlayerPrefs список уже сыгранных треков.
    /// Если данных нет, оставляет список пустым.
    /// PlayerPrefs — постоянное хранилище Unity между запусками игры.
    /// </summary>
    private void LoadPlayedTracks()
    {
        string playedTracksData = PlayerPrefs.GetString("PlayedMusicTracks", "");
        if (!string.IsNullOrEmpty(playedTracksData))
        {
            playedTracks = playedTracksData.Split(',')
                .Select(int.Parse)
                .ToList();
        }
    }

    /// <summary>
    /// Сохраняет текущий список сыгранных треков в PlayerPrefs.
    /// Используется после каждого воспроизведения трека.
    /// </summary>
    private void SavePlayedTracks()
    {
        string data = string.Join(",", playedTracks);
        PlayerPrefs.SetString("PlayedMusicTracks", data);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Создаёт список ещё не сыгранных треков, перемешивает их случайным образом.
    /// Если все треки уже сыграны — очищает историю и начинает заново.
    /// Устанавливает currentTrackIndex на первый трек из перемешанного списка.
    /// </summary>
    private void ShuffleUnplayedTracks()
    {
        List<int> unplayedIndices = Enumerable.Range(0, musicClips.Count)
            .Where(i => !playedTracks.Contains(i))
            .ToList();

        System.Random rng = new System.Random();
        unplayedIndices = unplayedIndices
            .OrderBy(x => rng.Next())
            .ToList();

        if (unplayedIndices.Count == 0)
        {
            // Все треки сыграны — начинаем заново
            playedTracks.Clear();
            SavePlayedTracks();
            ShuffleUnplayedTracks(); // Рекурсивный вызов для нового цикла
        }
        else
        {
            currentTrackIndex = unplayedIndices[0];
        }
    }

    /// <summary>
    /// Запускает воспроизведение следующего трека.
    /// Добавляет текущий трек в историю, сохраняет её, перемешивает оставшиеся треки,
    /// отправляет команду на воспроизведение всем клиентам через сеть.
    /// </summary>
    private void StartPlayingNextTrack()
    {
        if (musicClips.Count == 0) return;

        // Добавляем текущий трек в историю, если он есть и не был добавлен
        if (currentTrackIndex != -1 && !playedTracks.Contains(currentTrackIndex))
        {
            playedTracks.Add(currentTrackIndex);
            SavePlayedTracks();
        }

        ShuffleUnplayedTracks();

        // Проверяем, есть ли власть для вызова команды
        if (isOwned)
        {
            CmdPlayTrack(currentTrackIndex);
        }
        else if (isServer)
        {
            // Если это сервер, но нет власти — просто выполняем логику сервера
            RpcPlayTrack(currentTrackIndex); // Прямой вызов RPC вместо команды
        }
    }

    /// <summary>
    /// Команда (Command) — вызывается клиентом, выполняется на сервере.
    /// Отправляет RPC-вызов для воспроизведения трека всем клиентам.
    /// Обеспечивает синхронизацию воспроизведения между игроками.
    /// </summary>
    /// <param name="trackIndex">Индекс трека в списке musicClips</param>
    [Command]
    private void CmdPlayTrack(int trackIndex)
    {
        RpcPlayTrack(trackIndex);
    }

    /// <summary>
    /// RPC-вызов (ClientRpc) — вызывается сервером, выполняется на всех клиентах.
    /// Устанавливает аудиоклип на AudioSource и запускает воспроизведение.
    /// Гарантирует, что все игроки слышат один и тот же трек одновременно.
    /// </summary>
    /// <param name="trackIndex">Индекс трека в списке musicClips</param>
    [ClientRpc]
    private void RpcPlayTrack(int trackIndex)
    {
        Debug.Log($"RpcPlayTrack called with index: {trackIndex}");

        if (trackIndex < 0 || trackIndex >= musicClips.Count)
        {
            Debug.LogError($"Invalid track index: {trackIndex}. Music clips count: {musicClips.Count}");
            return;
        }

        AudioClip clip = musicClips[trackIndex];
        if (clip == null)
        {
            Debug.LogError($"AudioClip at index {trackIndex} is null!");
            return;
        }

        Debug.Log($"Playing clip: {clip.name}");
        audioSource.clip = clip;
        audioSource.Play();
    }

    /// <summary>
    /// Хук для SyncVar — вызывается при изменении currentTrackIndex.
    /// Можно использовать для отладки или дополнительных действий при смене трека.
    /// В данном случае просто выводит сообщение в консоль.
    /// </summary>
    /// <param name="oldIndex">Старый индекс трека</param>
    /// <param name="newIndex">Новый индекс трека</param>
    private void OnCurrentTrackChanged(int oldIndex, int newIndex)
    {
        if (!isLocalPlayer) return;
        Debug.Log($"Track changed: {oldIndex} -> {newIndex}");
    }

    /// <summary>
    /// Основной игровой цикл — проверяет, закончился ли текущий трек.
    /// Если трек закончился и это сервер, запускает следующий трек через задержку.
    /// Это обеспечивает автоматическое воспроизведение следующего трека после окончания текущего.
    /// </summary>
    void Update()
    {
        if (!isServer) return;

        if (!audioSource.isPlaying && currentTrackIndex != -1)
        {
            Invoke(nameof(StartPlayingNextTrack), 1f); // Задержка 1 секунда перед следующим треком
        }
    }

    /// <summary>
    /// Публичный метод для принудительной смены трека.
    /// Может быть вызван из других скриптов или UI-кнопки.
    /// Если вызывается на клиенте — отправляет команду на сервер.
    /// Если вызывается на сервере — сразу запускает следующий трек.
    /// </summary>
    public void ForceNextTrack()
    {
        if (isServer)
        {
            StartPlayingNextTrack();
        }
        else
        {
            CmdForceNextTrack();
        }
    }

    /// <summary>
    /// Команда для принудительной смены трека — вызывается клиентом, выполняется на сервере.
    /// Вызывает StartPlayingNextTrack() на сервере, что запускает смену трека для всех игроков.
    /// </summary>
    [Command]
    private void CmdForceNextTrack()
    {
        StartPlayingNextTrack();
    }
}