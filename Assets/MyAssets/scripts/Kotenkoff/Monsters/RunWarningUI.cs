using UnityEngine;

namespace Kotenkoff
{
    /// <summary>
    /// Вешается на Canvas или на сам текст "RUN!". 
    /// Автоматически скрывается при старте.
    /// </summary>
    public class RunWarningUI : MonoBehaviour
    {
        [Tooltip("Сюда закинь объект текста (TextMeshPro или обычный), который должен появляться")]
        [SerializeField] private GameObject runTextObject;

        private void OnEnable()
        {
            // Прячем текст на всякий случай при загрузке сцены
            if (runTextObject != null) runTextObject.SetActive(false);

            // Подписываемся на событие тягача
            TractorEntity.OnTractorPresenceChanged += HandlePresence;
        }

        private void OnDisable()
        {
            // Обязательно отписываемся, чтобы не было утечек памяти
            TractorEntity.OnTractorPresenceChanged -= HandlePresence;
        }

        private void HandlePresence(bool isPresent)
        {
            if (runTextObject != null)
            {
                runTextObject.SetActive(isPresent);
            }
        }
    }
}