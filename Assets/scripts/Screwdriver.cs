/// <summary>
/// Класс отвёртки.
/// </summary>
public class Screwdriver : PickupableItem, IRepairTool
{
    public bool CanFix(CarPart part)
    {
        bool value = false;
        
        // Проверяем, радио ли это
        if (part.gameObject.TryGetComponent(out RadioController radio))
        {
            // Если радио сломано
            if (radio.IsBroken)
            {
                // Ставим 'true' чтобы починить радио
                value = true;
            }
        }
        
        return value;
    }
}
