using UnityEngine;
using UnityEngine.InputSystem;
/// <summary>
/// Captures local audio from the microphone in chunks, applies Voice Activity Detection (VAD) or Push-To-Talk (PTT),
/// and passes the raw float array to the VoiceNetworker.
/// </summary>
[RequireComponent(typeof(VoiceNetworker))]
public class VoiceRecorder : MonoBehaviour
{
    [SerializeField] private string pttKeyName = "v"; 
    [SerializeField] private bool useVAD = true;
    [SerializeField] private float vadThreshold = 0.01f;
    [SerializeField] private int sampleRate = 24000;
    [SerializeField] private int chunkLengthMs = 40;

    private VoiceNetworker networker;
    private AudioClip micClip;
    private string deviceName;
    private int lastMicPosition;
    private int chunkSize;
    private float[] chunkBuffer;
    private bool isRecording = false;

    /// <summary>
    /// Initializes the microphone and pre-allocates the chunk buffer.
    /// </summary>
    private void Start()
    {   
    networker = GetComponent<VoiceNetworker>();
    if (!networker.isLocalPlayer)
    {
        enabled = false;
        return;
    }

    chunkSize = sampleRate * chunkLengthMs / 1000;
    chunkBuffer = new float[chunkSize];

    // Проверяем, есть ли устройства вообще
    if (Microphone.devices.Length > 0)
    {
        // Передаем null — это заставит Unity использовать микрофон по умолчанию в системе
        deviceName = null; 
        Debug.Log("Попытка запуска микрофона по умолчанию...");
        StartMicrophone();
    }
    else
    {
        Debug.LogError("КРИТИЧЕСКАЯ ОШИБКА: Микрофоны не найдены в системе!");
    }
    }   

    /// <summary>
    /// Starts the looping microphone recording.
    /// </summary>
    private void StartMicrophone()
    {
        micClip = Microphone.Start(deviceName, true, 1, sampleRate);
        lastMicPosition = Microphone.GetPosition(deviceName);
        isRecording = true;
    }

    /// <summary>
    /// Polls the microphone position and extracts data if a full chunk is ready.
    /// </summary>
    private void Update()
    {
        if (!isRecording) return;

        int currentMicPosition = Microphone.GetPosition(deviceName);
        int diff = currentMicPosition - lastMicPosition;

        if (diff < 0) diff += micClip.samples;

        if (diff >= chunkSize)
        {
            micClip.GetData(chunkBuffer, lastMicPosition);
            lastMicPosition = (lastMicPosition + chunkSize) % micClip.samples;

            ProcessAndTransmitChunk();
        }
    }

    /// <summary>
    /// Evaluates PTT and VAD conditions before sending data to the networker.
    /// </summary>
    private void ProcessAndTransmitChunk()
    {
        
        bool isPttPressed = false;

        if (Keyboard.current != null)
        {
            
            isPttPressed = Keyboard.current.vKey.isPressed; 
        }
        
        if (!isPttPressed && !useVAD) return;

        if (useVAD && !isPttPressed)
        {
            float maxVolume = 0f;
            for (int i = 0; i < chunkSize; i++)
            {
                float absVal = Mathf.Abs(chunkBuffer[i]);
                if (absVal > maxVolume) maxVolume = absVal;
            }

            if (maxVolume < vadThreshold) return;
        }

        networker.TransmitAudio(chunkBuffer);
        Debug.Log("Данные микрофона отправлены в сеть!");
    }

    /// <summary>
    /// Safely stops the microphone when the script is disabled or destroyed.
    /// </summary>
    private void OnDisable()
    {
        if (isRecording)
        {
            Microphone.End(deviceName);
            isRecording = false;
        }
    }
}