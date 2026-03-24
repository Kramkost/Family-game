namespace Kotenkoff
{
    // Любой предмет, который может чинить детали, должен наследовать этот интерфейс
    public interface IRepairTool
    {
        // Можно добавить тип поломки, если ключ чинит только двигатель, а изолента - проводку
        bool CanFix(CarPart part); 
    }
}