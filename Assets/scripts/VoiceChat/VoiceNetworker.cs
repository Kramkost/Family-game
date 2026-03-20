using UnityEngine;
using Mirror;
using System.Collections.Generic;

/// <summary>
/// Handles compression, transmission, and safe buffered playback to prevent audio stuttering.
/// </summary>
public class VoiceNetworker : NetworkBehaviour
{
    private AudioSource playbackSource;
    private Queue<float> jitterBuffer = new Queue<float>();
    
   
    private bool isBuffering = true;
    private int minBufferSize;

    private void Awake()
    {
     
        minBufferSize = (int)(AudioSettings.outputSampleRate * 0.1f);

        playbackSource = GetComponent<AudioSource>();
        playbackSource.loop = true;
      
        playbackSource.clip = AudioClip.Create("StreamBuffer", 1024, 1, AudioSettings.outputSampleRate, false);
        playbackSource.Play();
        
        if (playbackSource == null) Debug.LogError("[VoiceNetworker] No AudioSource found!");
    }

    [ClientCallback]
    public void TransmitAudio(float[] rawAudio)
    {
        if (!isLocalPlayer) return;

        byte[] compressedData = CompressAudio(rawAudio);
        CmdSendVoice(compressedData);
    }

   
    private byte[] CompressAudio(float[] rawAudio)
    {
        byte[] dummyCompressed = new byte[rawAudio.Length];
        for (int i = 0; i < rawAudio.Length; i++)
        {
            dummyCompressed[i] = (byte)(Mathf.Clamp(rawAudio[i] * 127f + 128f, 0, 255));
        }
        return dummyCompressed;
    }

    private float[] DecompressAudio(byte[] compressedAudio)
    {
        float[] dummyDecompressed = new float[compressedAudio.Length];
        for (int i = 0; i < compressedAudio.Length; i++)
        {
            dummyDecompressed[i] = (compressedAudio[i] - 128f) / 127f;
        }
        return dummyDecompressed;
    }

    [Command(channel = Channels.Unreliable)]
    private void CmdSendVoice(byte[] compressedData)
    {
        RpcReceiveVoice(compressedData);
    }

    [ClientRpc(channel = Channels.Unreliable)]
    private void RpcReceiveVoice(byte[] compressedData)
    {
        if (isLocalPlayer) return;

        float[] decompressedData = DecompressAudio(compressedData);
        
        lock (jitterBuffer)
        {
            for (int i = 0; i < decompressedData.Length; i++)
            {
                jitterBuffer.Enqueue(decompressedData[i]);
            }
        }
    }

    /// <summary>
    /// Feeds the decompressed audio from the jitter buffer directly into the Unity audio pipeline.
    /// </summary>
    private void OnAudioFilterRead(float[] data, int channels)
    {
        if (isLocalPlayer) return;

        lock (jitterBuffer)
        {
           
            if (jitterBuffer.Count == 0)
            {
                isBuffering = true;
            }
          
            else if (isBuffering && jitterBuffer.Count >= minBufferSize)
            {
                isBuffering = false;
            }

            for (int i = 0; i < data.Length; i += channels)
            {
                
                float sample = (!isBuffering && jitterBuffer.Count > 0) ? jitterBuffer.Dequeue() : 0f;
                
                for (int c = 0; c < channels; c++)
                {
                    data[i + c] = sample;
                }
            }
        }
    }
}