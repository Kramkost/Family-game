using UnityEngine;
using Mirror;

namespace Kotenkoff
{
    public class WrenchTool : PickupableItem, IRepairTool
    {
        public bool CanFix(CarPart part)
        {
            // Ключ чинит ТОЛЬКО двигатель и колеса
            if (part.partType == CarPartType.Engine || part.partType == CarPartType.Wheel)
            {
                return true;
            }
            
            return false; 
        }
    }
}