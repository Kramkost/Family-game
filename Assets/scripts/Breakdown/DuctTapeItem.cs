using UnityEngine;
using Mirror;

namespace Kotenkoff
{
    // Наследуем PickupableItem, чтобы её можно было поднять.
    // Добавляем IRepairTool, чтобы открывать мини-игру починки.
    public class DuctTapeItem : PickupableItem, IRepairTool
    {
        public bool CanFix(CarPart part)
        {
            // Изолента чинит ТОЛЬКО двери и провода
            if (part.partType == CarPartType.Door || part.partType == CarPartType.Wires)
            {
                return true;
            }
            
            return false; 
        }
    }
}