using UnityEngine;

namespace Kotenkoff.Weapon
{
    /// <summary>
    ///  <para>Режимы стрельбы для оружия.</para>
    /// </summary>
    public enum WeaponModes
    {
        /// <summary>
        ///  <para>Автоматический режим стрельбы.</para>
        /// </summary>
        [Tooltip("Автоматический режим стрельбы.")]
        Automatic,
        /// <summary>
        ///  <para>Одиночный режим стрельбы.</para>
        /// </summary>
        [Tooltip("Одиночный режим стрельбы.")]
        Single
    }
}