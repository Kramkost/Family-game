using Mirror;
using UnityEngine;

/// <summary>
/// Представляет стек предметов в слоте инвентаря. <br/>
/// Хранит ссылку на сетевой объект и количество.
/// </summary>
public struct ItemStack : NetworkMessage
{
    /// <summary>
    /// Сетевой объект предмета.
    /// </summary>
    public NetworkIdentity itemNetId;

    /// <summary>
    /// Количество предметов в стеке.
    /// </summary>
    public int count;

    /// <summary>
    /// Пустой стек.
    /// </summary>
    public static ItemStack Empty => new ItemStack { itemNetId = null, count = 0 };

    /// <summary>
    /// Является ли стек пустым.
    /// </summary>
    public bool isEmpty => itemNetId == null || count <= 0;

    /// <summary>
    /// Создаёт новый стек.
    /// </summary>
    /// <param name="item">Сетевой объект предмета.</param>
    /// <param name="count">Количество.</param>
    public ItemStack(NetworkIdentity item, int count)
    {
        this.itemNetId = item;
        this.count = count;
    }

    /// <summary>
    /// Проверяет, совпадает ли предмет в стеке с другим объектом.
    /// </summary>
    /// <param name="other">Объект для сравнения.</param>
    /// <returns>True — тот же тип предмета.</returns>
    public bool Matches(GameObject other)
    {
        if (isEmpty || other == null) return false;
        Item thisItem = itemNetId.GetComponent<Item>();
        Item otherItem = other.GetComponent<Item>();
        return thisItem != null && otherItem != null && thisItem.ItemType == otherItem.ItemType;
    }
}