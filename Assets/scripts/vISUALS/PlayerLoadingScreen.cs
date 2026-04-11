using System.Collections;
using Mirror;
using UnityEngine;

public class PlayerLoadingScreen : NetworkBehaviour
{
    [Header("UI References")]
    [Tooltip("Ссылка на CanvasGroup твоего экрана загрузки")]
    [SerializeField] private CanvasGroup loadingScreenGroup;
    
    [Header("Settings")]
    [SerializeField] private float waitBeforeFade = 0.5f; // Даем миру полсекунды прогрузиться
    [SerializeField] private float fadeDuration = 1.5f;   // Длительность затухания

    public override void OnStartLocalPlayer()
    {
        // Убеждаемся, что экран включен и непрозрачен при старте
        loadingScreenGroup.alpha = 1f;
        loadingScreenGroup.gameObject.SetActive(true);
        loadingScreenGroup.blocksRaycasts = true; // Блокируем клики под экраном

        // Запускаем корутину плавного исчезновения
        StartCoroutine(FadeOutRoutine());
    }

    private IEnumerator FadeOutRoutine()
    {
        // 1. Ждем, пока прогрузятся тяжелые объекты (оружие, другие игроки)
        yield return new WaitForSeconds(waitBeforeFade);

        // 2. Плавно уменьшаем alpha от 1 до 0
        float elapsedTime = 0f;
        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.deltaTime;
            // Mathf.Lerp плавно меняет значение между 1 и 0
            loadingScreenGroup.alpha = Mathf.Lerp(1f, 0f, elapsedTime / fadeDuration);
            yield return null; // Ждем следующий кадр
        }

        // 3. Гарантированно ставим 0 и отключаем объект для экономии ресурсов
        loadingScreenGroup.alpha = 0f;
        loadingScreenGroup.blocksRaycasts = false;
        loadingScreenGroup.gameObject.SetActive(false);
    }
}