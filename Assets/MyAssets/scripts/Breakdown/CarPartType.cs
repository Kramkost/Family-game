using UnityEngine;

namespace Breakdown
{
    public enum CarPartType
    {
        Engine,
        Door,
        Wires,
        Wheel,
        /// <summary>
        /// Нечто другое
        /// </summary>
        [Tooltip("Что-то другое.")]
        Other
    }
}