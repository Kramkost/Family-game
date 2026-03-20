using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Captures local audio from the microphone, perfectly matching system sample rate,
/// applies VAD with a "hang time" to prevent stuttering, and safely packets data.
/// </summary>
[RequireComponent(typeof(VoiceNetworker))]
public class VoiceRecorder : MonoBehaviour
{
    [SerializeField] private string pttKeyName = "v"; 
    [SerializeField] private bool useVAD = true;
    [SerializeField] private float vadThreshold = 0.01f;
    [Tooltip("Сколько секунд продолжать запись после того, как голос стих (сглаживает прерывания)")]
    [SerializeField] private float vadHangTime = 0.5f; 
    
    [Tooltip("20ms гарантирует, что размер пакета не превысит MTU лимит UDP")]
    [SerializeField] private int chunkLengthMs = 20;

    private VoiceNetworker networker;
    private AudioClip micClip;
    private string deviceName;
    private int lastMicPosition;
    private int chunkSize;
    private float[] chunkBuffer;
    private bool isRecording = false;
    
    private int systemSampleRate;
    private float currentVadHangTimer = 0f;

    private void Start()
    {   
        networker = GetComponent<VoiceNetworker>();
        if (!networker.isLocalPlayer)
        {
            enabled = false;
            return;
        }

        // 1. АВТО-ГЕРЦОВКА: Берем системную частоту (спасает от высокого "бурундучьего" питча)
        systemSampleRate = AudioSettings.outputSampleRate;

        chunkSize = systemSampleRate * chunkLengthMs / 1000;
        chunkBuffer = new float[chunkSize];

        if (Microphone.devices.Length > 0)
        {
            deviceName = null; 
            Debug.Log($"[Voice] Запуск микрофона. Частота: {systemSampleRate}Hz, Размер чанка: {chunkSize} сэмплов.");
            StartMicrophone();
        }
        else
        {
            Debug.LogError("КРИТИЧЕСКАЯ ОШИБКА: Микрофоны не найдены в системе!");
        }
    }   

    private void StartMicrophone()
    {
        micClip = Microphone.Start(deviceName, true, 1, systemSampleRate);
        lastMicPosition = Microphone.GetPosition(deviceName);
        isRecording = true;
    }

    private void Update()
    {
        if (!isRecording) return;

        int currentMicPosition = Microphone.GetPosition(deviceName);
        int diff = currentMicPosition - lastMicPosition;

        if (diff < 0) diff += micClip.samples;

        // Отправляем чанки, если накопилось достаточно данных
        while (diff >= chunkSize)
        {
            micClip.GetData(chunkBuffer, lastMicPosition);
            lastMicPosition = (lastMicPosition + chunkSize) % micClip.samples;
            diff -= chunkSize;

            ProcessAndTransmitChunk();
        }
    }

    private void ProcessAndTransmitChunk()
    {
        bool isPttPressed = Keyboard.current != null && Keyboard.current.vKey.isPressed;
        bool isSpeaking = isPttPressed;

        // Умный VAD с "хвостом"
        if (useVAD && !isPttPressed)
        {
            float maxVolume = 0f;
            for (int i = 0; i < chunkSize; i++)
            {
                float absVal = Mathf.Abs(chunkBuffer[i]);
                if (absVal > maxVolume) maxVolume = absVal;
            }

            if (maxVolume >= vadThreshold)
            {
                isSpeaking = true;
                currentVadHangTimer = vadHangTime; // Сбрасываем таймер удержания
            }
            else if (currentVadHangTimer > 0f)
            {
                // Голос стих, но мы продолжаем запись еще долю секунды
                isSpeaking = true;
                currentVadHangTimer -= (float)chunkLengthMs / 1000f; 
            }
        }

        if (!isSpeaking) return;

        networker.TransmitAudio(chunkBuffer);
    }

    private void OnDisable()
    {
        if (isRecording)
        {
            Microphone.End(deviceName);
            isRecording = false;
        }
    }
}