using Mirror;
using TMPro;
using UnityEngine;

// Используем TextMeshPro, так как это стандарт Unity

namespace vISUALS
{
    /// <summary>
    /// Локальный сканер игрока. Ищет объекты с InteractableTooltip и выводит их текст на экран.
    /// </summary>
    public class PlayerTooltipScanner : NetworkBehaviour
    {
        [Header("References")]
        [Tooltip("Камера игрока (откуда пускаем луч)")]
        [SerializeField] private Transform cameraTransform;
        
        [Tooltip("UI Текст на экране (TextMeshPro)")]
        [SerializeField] private TextMeshProUGUI tooltipTextUI;

        [Header("Settings")]
        [SerializeField] private float scanRange = 3f;
        [SerializeField] private LayerMask interactLayerMask = ~0;

        private void Start()
        {
            // Прячем текст при старте
            if (tooltipTextUI != null)
            {
                tooltipTextUI.gameObject.SetActive(false);
            }
        }

        private void Update()
        {
            // Сканируем ТОЛЬКО для локального игрока (чтобы не сломать UI другим)
            if (!isLocalPlayer || cameraTransform == null || tooltipTextUI == null) return;

            // Пускаем луч из центра камеры
            if (Physics.Raycast(cameraTransform.position, cameraTransform.forward, out RaycastHit hit, scanRange, interactLayerMask))
            {
                // Ищем компонент-ярлык на объекте или его родителях
                InteractableTooltip tooltip = hit.collider.GetComponentInParent<InteractableTooltip>();
                
                if (tooltip != null)
                {
                    // Нашли! Показываем текст и выходим из метода
                    tooltipTextUI.text = tooltip.promptText;
                    if (!tooltipTextUI.gameObject.activeSelf) tooltipTextUI.gameObject.SetActive(true);
                    return;
                }
            }

            // Если луч улетел в пустоту или на объекте нет ярлыка — прячем текст
            if (tooltipTextUI.gameObject.activeSelf)
            {
                tooltipTextUI.gameObject.SetActive(false);
            }
        }
    }
}