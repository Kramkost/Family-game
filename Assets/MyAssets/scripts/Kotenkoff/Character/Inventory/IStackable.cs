using UnityEngine;

/// <summary>
/// Интерфейс для предметов, которые можно складывать в стек. <br/>
/// Реализуется, если предмет поддерживает стеки (например, патроны, еда).
/// </summary>
public interface IStackable
{
    /// <summary>
    /// Максимальное количество в стеке.
    /// </summary>
    int MaxStackSize { get; }
}