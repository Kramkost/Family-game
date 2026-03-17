using UnityEngine;
using Mirror;
using System.Collections.Generic;

/// <summary>
/// Handles the compression, transmission, and decompression of voice data over the network.
/// Plays incoming voice data dynamically using OnAudioFilterRead.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class VoiceNetworker : NetworkBehaviour
{
    private AudioSource playbackSource;
    private Queue<float> jitterBuffer = new Queue<float>();

    /// <summary>
    /// Initializes the AudioSource for procedural streaming.
    /// </summary>
    private void Awake()
    {
        playbackSource = GetComponent<AudioSource>();
        playbackSource.loop = true;
        playbackSource.clip = AudioClip.Create("StreamBuffer", 1024, 1, 24000, false);
        playbackSource.Play();
    }

    /// <summary>
    /// Entry point for local audio chunks. Compresses and sends to the server.
    /// </summary>
    [ClientCallback]
    public void TransmitAudio(float[] rawAudio)
    {
        if (!isLocalPlayer) return;

        byte[] compressedData = CompressAudio(rawAudio);
        CmdSendVoice(compressedData);
    }

    /// <summary>
    /// Placeholder wrapper for audio compression (e.g., Opus).
    /// </summary>
    private byte[] CompressAudio(float[] rawAudio)
    {
        byte[] dummyCompressed = new byte[rawAudio.Length];
        for (int i = 0; i < rawAudio.Length; i++)
        {
            dummyCompressed[i] = (byte)(Mathf.Clamp(rawAudio[i] * 127f + 128f, 0, 255));
        }
        return dummyCompressed;
    }

    /// <summary>
    /// Placeholder wrapper for audio decompression.
    /// </summary>
    private float[] DecompressAudio(byte[] compressedAudio)
    {
        float[] dummyDecompressed = new float[compressedAudio.Length];
        for (int i = 0; i < compressedAudio.Length; i++)
        {
            dummyDecompressed[i] = (compressedAudio[i] - 128f) / 127f;
        }
        return dummyDecompressed;
    }

    /// <summary>
    /// Relays the compressed audio from the speaking client to the server via the unreliable channel.
    /// </summary>
    [Command(channel = Channels.Unreliable)]
    private void CmdSendVoice(byte[] compressedData)
    {
        RpcReceiveVoice(compressedData);
    }

    /// <summary>
    /// Distributes the compressed audio from the server to all clients via the unreliable channel.
    /// </summary>
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
            for (int i = 0; i < data.Length; i += channels)
            {
                float sample = jitterBuffer.Count > 0 ? jitterBuffer.Dequeue() : 0f;
                
                for (int c = 0; c < channels; c++)
                {
                    data[i + c] = sample;
                }
            }
        }
    }
}