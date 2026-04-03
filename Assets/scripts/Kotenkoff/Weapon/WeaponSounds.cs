using System.Collections.Generic;
using UnityEngine;

namespace Kotenkoff.Weapon
{
    public class WeaponSounds : MonoBehaviour
    {
        [SerializeField, Tooltip("Список звуков для выстрела.")]
        private List<AudioClip> shotSounds;
        
        /// <summary>
        ///  <para>Список звуков для выстрела.</para>
        /// </summary>
        public List<AudioClip> ShotSounds => shotSounds;
        
        [SerializeField, Tooltip("Список звуков для перезарядки.")]
        private List<AudioClip> reloadSounds;
        
        /// <summary>
        ///  <para>Список звуков для перезарядки.</para>
        /// </summary>
        public List<AudioClip> ReloadSounds => reloadSounds;
    }
}