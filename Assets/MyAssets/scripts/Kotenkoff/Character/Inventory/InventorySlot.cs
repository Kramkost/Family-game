using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Компонент визуального слота инвентаря. <br/>
/// Отображает иконку, количество, состояние выделения. <br/>
/// Обрабатывает клик.
/// </summary>
public class InventorySlot : MonoBehaviour
{
    [Header("UI Элементы")]
    [SerializeField, Tooltip("Изображение иконки предмета.")]
    private Image itemIconImage;

    [SerializeField, Tooltip("Текст для отображения количества.")]
    private Text itemCountText;

    [SerializeField, Tooltip("Объект-индикатор выделения (например, рамка).")]
    private GameObject selectedIndicator;

    private int slotIndex;

    /// <summary>
    /// Устанавливает индекс слота.
    /// </summary>
    /// <param name="index">Индекс слота.</param>
    public void SetSlotIndex(int index)
    {
        this.slotIndex = index;
        Debug.Log($"[InventorySlot.SetSlotIndex] Слоту {gameObject.name} присвоен индекс {index}");
    }

    /// <summary>
    /// Обновляет визуальное состояние слота на основе ItemStack. <br/>
    /// Получает иконку и данные через компонент Item.
    /// </summary>
    /// <param name="itemStack">Текущий стек в слоте.</param>
    public void UpdateSlot(ItemStack itemStack)
    {
        if (itemStack.isEmpty)
        {
            itemIconImage.enabled = false;
            itemCountText.text = "";
            Debug.Log($"[InventorySlot.UpdateSlot] Слот {slotIndex} пуст");
            return;
        }

        itemIconImage.enabled = true;

        // Получаем компонент Item из сетевого объекта
        Item itemComponent = itemStack.itemNetId.GetComponent<Item>();
        if (itemComponent != null)
        {
            Sprite icon = itemComponent.GetIcon();
            if (icon != null)
            {
                itemIconImage.sprite = icon;
                Debug.Log($"[InventorySlot.UpdateSlot] Установлена иконка для {itemComponent.ItemName}");
            }
            else
            {
                itemIconImage.color = Color.gray;
                Debug.LogWarning($"[InventorySlot.UpdateSlot] Иконка не найдена для {itemStack.itemNetId.name}");
            }
        }
        else
        {
            itemIconImage.color = Color.magenta; // Визуальная ошибка (нет Item)
            Debug.LogError($"[InventorySlot.UpdateSlot] У объекта {itemStack.itemNetId.name} нет компонента Item!");
        }

        // Обновляем текст количества
        itemCountText.text = itemStack.count > 1 ? itemStack.count.ToString() : "";
    }

    /// <summary>
    /// Устанавливает состояние выделения слота. <br/>
    /// Активирует индикатор (например, рамку).
    /// </summary>
    /// <param name="selected">True — слот выбран.</param>
    public void SetSelected(bool selected)
    {
        if (selectedIndicator != null)
        {
            selectedIndicator.SetActive(selected);
            Debug.Log($"[InventorySlot.SetSelected] Слот {slotIndex} {(selected ? "выделен" : "снят с выделения")}");
        }
    }

    /// <summary>
    /// Вызывается при клике по слоту в UI. <br/>
    /// Передаёт событие в родительский InventoryManager.
    /// </summary>
    public void OnClick()
    {
        Debug.Log($"[InventorySlot.OnClick] Клик по слоту {slotIndex}");
        InventoryManager manager = GetComponentInParent<InventoryManager>();
        if (manager != null)
        {
            manager.OnSlotClicked(slotIndex);
        }
        else
        {
            Debug.LogError($"[InventorySlot.OnClick] Не найден InventoryManager для слота {slotIndex}");
        }
    }
}
