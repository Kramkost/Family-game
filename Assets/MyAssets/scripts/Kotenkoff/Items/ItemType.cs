using UnityEngine;

namespace MyAssets.scripts.Kotenkoff.Items
{
    public enum ItemType
    {
        [Tooltip("Еда")] Food,
        [Tooltip("Оружие")] Weapon,
        [Tooltip("Патроны")] Ammo,
        [Tooltip("Инструмент")] Tool,
        Key,
        Misc,
        [Tooltip("Что-то другое.")] Other
    }
}