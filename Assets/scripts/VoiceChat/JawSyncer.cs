using UnityEngine;

/// <summary>
/// Reads local AudioSource output data to calculate RMS volume and procedurally animates a jaw bone.
/// </summary>
public class JawSyncer : MonoBehaviour
{
    [SerializeField] private AudioSource targetSource;
    [SerializeField] private Transform jawBone;
    
    [SerializeField] private Vector3 restRotation;
    [SerializeField] private Vector3 maxOpenRotation;
    
    [SerializeField] private float rmsMultiplier = 15f;
    [SerializeField] private float smoothingSpeed = 10f;
    [SerializeField] private int sampleWindow = 256;

    private float[] audioSamples;

    /// <summary>
    /// Allocates the array for audio sample extraction.
    /// </summary>
    private void Start()
    {
        audioSamples = new float[sampleWindow];
    }

    /// <summary>
    /// Calculates the RMS volume and interpolates the jaw's rotation.
    /// </summary>
    private void Update()
    {
     
        if (targetSource == null || jawBone == null) return;

        float rms = CalculateRMS();
        float mouthOpenWeight = Mathf.Clamp01(rms * rmsMultiplier);

        Quaternion targetRotation = Quaternion.Euler(
            Mathf.Lerp(restRotation.x, maxOpenRotation.x, mouthOpenWeight),
            Mathf.Lerp(restRotation.y, maxOpenRotation.y, mouthOpenWeight),
            Mathf.Lerp(restRotation.z, maxOpenRotation.z, mouthOpenWeight)
        );

        jawBone.localRotation = Quaternion.Slerp(jawBone.localRotation, targetRotation, Time.deltaTime * smoothingSpeed);
    }

    /// <summary>
    /// Extracts output data from the AudioSource and computes the Root Mean Square.
    /// </summary>
    private float CalculateRMS()
    {
        targetSource.GetOutputData(audioSamples, 0);

        float sum = 0f;
        for (int i = 0; i < sampleWindow; i++)
        {
            sum += audioSamples[i] * audioSamples[i];
        }

        return Mathf.Sqrt(sum / sampleWindow);
    }
}