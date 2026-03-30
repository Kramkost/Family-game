using UnityEngine;

/// <summary>
/// Плавно оглушает звуки улицы, когда игрок заходит в фургон.
/// </summary>
public class AudioZoneTrigger : MonoBehaviour
{
    [Header("Настройки фильтра")]
    public AudioLowPassFilter lowPassFilter; 
    public float muffleFrequency = 1500f; // Глухой звук в салоне
    public float normalFrequency = 22000f; // Чистый звук на улице
    public float transitionSpeed = 5f; // Скорость перехода

    private bool isInside = false;

    private void Update()
    {
        if (lowPassFilter == null) return;

      
        float targetFreq = isInside ? muffleFrequency : normalFrequency;
        lowPassFilter.cutoffFrequency = Mathf.Lerp(lowPassFilter.cutoffFrequency, targetFreq, Time.deltaTime * transitionSpeed);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("IN_Room"))
        {
            isInside = true;
            lowPassFilter.enabled = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("IN_Room"))
        {
            isInside = false;
            // Фильтр выключится сам, когда частота дойдет до максимума. 22000 - это и так чистый звук.
        }
    }
}