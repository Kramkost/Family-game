using Kotenkoff;

/// <summary>
/// Общий контракт для всех интерактивных объектов в игре (канистры, двери, бак).
/// Любой класс, реализующий этот интерфейс, обязан иметь метод ServerInteract.
/// </summary>
public interface IInteractable
{
    //void ServerInteract(PlayerEntity player, PlayerInventory inventory);
    
    void ServerInteract(PlayerEntity player, PlayerInventory inventory);
}