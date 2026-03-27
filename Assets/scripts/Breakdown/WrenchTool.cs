namespace Kotenkoff
{
    public class WrenchTool : PickupableItem, IRepairTool
    {
        public bool CanFix(CarPart part)
        {
           
            if (part.partType == CarPartType.Engine && part.currentStage == 3) return true;
            
            if (part.partType == CarPartType.Wheel) return true;
            return false; 
        }
    }
}