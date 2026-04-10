using UnityEngine;

namespace vISUALS
{
    /// <summary>
    /// Хранит визуальную информацию о предмете для UI инвентаря.
    /// </summary>
    public class ItemDetails : MonoBehaviour
    {
        [Header("UI Info")]
        public string itemName = "Неизвестный предмет";
        
        [Tooltip("Появляется под названием (например: 'Drink!' или 'Оружие')")]
        public string itemDescription = "Описание";
        
        [Tooltip("Подсказка справа. Оставь пустым, если подсказка не нужна.")]
        public string actionHint = "[ЛКМ] Использовать";
    }
}