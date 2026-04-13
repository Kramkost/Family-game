using UnityEngine;
using System.Collections;

/// <summary>
/// Зона эмбиента. Воспроизводит звук (ветер, город, музыка), когда локальный игрок заходит внутрь.
/// Плавное затухание (Fade) гарантирует отсутствие резких обрывов звука.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
[RequireComponent(typeof(AudioSource))]
public class AmbientZone : MonoBehaviour
{
    [Tooltip("Сколько секунд звук будет плавно появляться/исчезать")]
    [SerializeField] private float fadeDuration = 2f;
    
    [Tooltip("Максимальная громкость эмбиента в этой зоне")]
    [SerializeField] [Range(0f, 1f)] private float maxVolume = 1f;

    private AudioSource audioSource;
    private Coroutine fadeCoroutine;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.loop = true;
        audioSource.volume = 0f;
        audioSource.spatialBlend = 0f; 
        
        GetComponent<BoxCollider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
       
        if (other.TryGetComponent(out PlayerEntity player) && player.isLocalPlayer)
        {
            if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
            if (!audioSource.isPlaying) audioSource.Play();
            
            fadeCoroutine = StartCoroutine(FadeVolume(maxVolume));
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.TryGetComponent(out PlayerEntity player) && player.isLocalPlayer)
        {
            if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
            
            fadeCoroutine = StartCoroutine(FadeVolume(0f, stopAudio: true));
        }
    }

    private IEnumerator FadeVolume(float targetVolume, bool stopAudio = false)
    {
        float startVolume = audioSource.volume;
        float time = 0;

        while (time < fadeDuration)
        {
            time += Time.deltaTime;
            audioSource.volume = Mathf.Lerp(startVolume, targetVolume, time / fadeDuration);
            yield return null;
        }

        audioSource.volume = targetVolume;
        if (stopAudio && audioSource.volume == 0)
        {
            audioSource.Stop();
        }
    }
}