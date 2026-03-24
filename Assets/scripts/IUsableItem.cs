using Kotenkoff;

/// <summary>
/// Контракт для предметов, которые можно использовать, держа в руках (ЛКМ).
/// </summary>
public interface IUsableItem
{
    void ServerUse(PlayerEntity player);
}