using UnityEngine;
using Mirror;

/// <summary>
/// Оптимизированный менеджер смены дня и ночи.
/// Использует NetworkTime для синхронизации без сетевого спама (Zero Bandwidth).
/// </summary>
public class DayNightManager : NetworkBehaviour
{
    [Header("Настройки времени")]
    [Tooltip("Сколько реальных минут длится один игровой день")]
    [SerializeField] private float dayDurationInMinutes = 10f;

    [Tooltip("Начальное время при старте сервера (0 = полночь, 0.5 = полдень)")]
    [SerializeField] [Range(0f, 1f)] private float startHourOffset = 0.35f;

    [Header("Визуал Солнца")]
    [SerializeField] private Transform sunTransform;
    [SerializeField] private Light sunLight;
    
    [Tooltip("Кривая яркости. Настрой, чтобы ночью (0.0 - 0.2 и 0.8 - 1.0) яркость была 0.")]
    [SerializeField] private AnimationCurve sunIntensityCurve; 
    
    [Tooltip("Цвет солнца. Позволяет сделать рассвет/закат оранжевым, а день - белым.")]
    [SerializeField] private Gradient sunColorGradient; 

    
    [SyncVar] private double serverStartTimeAnchor;

    public override void OnStartServer()
    {
       
        float totalSecondsInDay = dayDurationInMinutes * 60f;
        serverStartTimeAnchor = NetworkTime.time - (totalSecondsInDay * startHourOffset);
    }

    private void Update()
    {
        
        if (!NetworkClient.active && !NetworkServer.active) return;

       
        float totalSecondsInDay = dayDurationInMinutes * 60f;
        double elapsedSeconds = NetworkTime.time - serverStartTimeAnchor;
        
        
        float timeOfDay = (float)(elapsedSeconds % totalSecondsInDay) / totalSecondsInDay;

        UpdateLighting(timeOfDay);
    }

    private void UpdateLighting(float timeOfDay)
    {
        if (sunTransform != null)
        {
            
            float sunAngle = (timeOfDay * 360f) - 90f;
            
            
            sunTransform.localRotation = Quaternion.Euler(sunAngle, 170f, 0f); 
        }

        
        if (sunLight != null && sunIntensityCurve != null && sunColorGradient != null)
        {
            sunLight.intensity = sunIntensityCurve.Evaluate(timeOfDay);
            sunLight.color = sunColorGradient.Evaluate(timeOfDay);
        }
    }

    /// <summary>
    /// Пример метода для сна. Вызывается сервером, чтобы перемотать время на утро.
    /// </summary>
    [Server]
    public void SkipToMorning()
    {
        float totalSecondsInDay = dayDurationInMinutes * 60f;
        
        
        serverStartTimeAnchor = NetworkTime.time - (totalSecondsInDay * 0.25f);
    }
}