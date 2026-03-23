using UnityEngine;

/// <summary>
/// Анимирует челюсть, опираясь на готовые расчеты громкости из VoiceNetworker.
/// </summary>
public class JawSyncer : MonoBehaviour
{
    [Tooltip("Ссылка на скрипт VoiceNetworker, откуда звучит голос")]
    [SerializeField] private VoiceNetworker targetVoice;
    [SerializeField] private Transform jawBone;
    
    [SerializeField] private Vector3 restRotation;
    [SerializeField] private Vector3 maxOpenRotation;
    
    [SerializeField] private float rmsMultiplier = 15f;
    [SerializeField] private float smoothingSpeed = 10f;

    private void Update()
    {
        if (targetVoice == null || jawBone == null) return;

        // Берем готовый RMS напрямую из сети! Никаких GetOutputData.
        float rms = targetVoice.CurrentRMS;
        float mouthOpenWeight = Mathf.Clamp01(rms * rmsMultiplier);

        Quaternion targetRotation = Quaternion.Euler(
            Mathf.Lerp(restRotation.x, maxOpenRotation.x, mouthOpenWeight),
            Mathf.Lerp(restRotation.y, maxOpenRotation.y, mouthOpenWeight),
            Mathf.Lerp(restRotation.z, maxOpenRotation.z, mouthOpenWeight)
        );

        jawBone.localRotation = Quaternion.Slerp(jawBone.localRotation, targetRotation, Time.deltaTime * smoothingSpeed);
    }
}