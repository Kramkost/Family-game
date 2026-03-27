namespace Kotenkoff
{
    public class DuctTapeItem : PickupableItem, IRepairTool
    {
        public bool CanFix(CarPart part)
        {
            // Изолента чинит легкие поломки - 1 и 2 стадию!
            if (part.partType == CarPartType.Engine && (part.currentStage == 1 || part.currentStage == 2)) return true;
            
            if (part.partType == CarPartType.Door || part.partType == CarPartType.Wires) return true;
            return false; 
        }
    }
}