using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Записывает голос, применяет VAD и СЖИМАЕТ трафик (Downsampling),
/// чтобы пакеты гарантированно пролезали в сетевые лимиты Mirror (MTU).
/// </summary>
[RequireComponent(typeof(VoiceNetworker))]
public class VoiceRecorder : MonoBehaviour
{
    [SerializeField] private string pttKeyName = "v"; 
    [SerializeField] private bool useVAD = true;
    [SerializeField] private float vadThreshold = 0.01f;
    [SerializeField] private float vadHangTime = 0.5f; 
    [SerializeField] private int chunkLengthMs = 20;

    [Tooltip("Фактор сжатия трафика. Значение 4 уменьшит вес пакета в 4 раза (гарантия обхода ошибки Mirror)")]
    [SerializeField] private int downsampleFactor = 4;

    private VoiceNetworker networker;
    private AudioClip micClip;
    private string deviceName;
    private int lastMicPosition;
    private int chunkSize;
    private float[] chunkBuffer;
    private float[] downsampledBuffer; // Уменьшенный буфер для сети
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

        systemSampleRate = AudioSettings.outputSampleRate;
        chunkSize = systemSampleRate * chunkLengthMs / 1000;
        
        chunkBuffer = new float[chunkSize];
        // Подготавливаем крошечный буфер для отправки по сети
        downsampledBuffer = new float[chunkSize / downsampleFactor]; 

        if (Microphone.devices.Length > 0)
        {
            deviceName = null; 
            StartMicrophone();
        }
        else
        {
            Debug.LogError("КРИТИЧЕСКАЯ ОШИБКА: Микрофоны не найдены!");
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
                currentVadHangTimer = vadHangTime; 
            }
            else if (currentVadHangTimer > 0f)
            {
                isSpeaking = true;
                currentVadHangTimer -= (float)chunkLengthMs / 1000f; 
            }
        }

        if (!isSpeaking) return;

        // --- МАГИЯ СЖАТИЯ (DOWNSAMPLING) ---
        // Берем только каждый 4-й сэмпл. Качество голоса остается понятным (как в рации), 
        // но пакет становится легким, и Mirror перестает ругаться!
        for (int i = 0; i < downsampledBuffer.Length; i++)
        {
            downsampledBuffer[i] = chunkBuffer[i * downsampleFactor];
        }

        networker.TransmitAudio(downsampledBuffer);
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