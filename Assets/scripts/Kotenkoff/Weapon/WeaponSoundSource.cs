using UnityEngine;

namespace Kotenkoff.Weapon
{
    public class WeaponSoundSource
    {
        private readonly Weapon weapon;

        public WeaponSoundSource(Weapon weapon)
        {
            this.weapon = weapon;
        }
        
        public void PlayShotSound()
        {
            var list = weapon.WeaponSoundsList.ShotSounds;
            var clip = list[Random.Range(0, list.Count - 1)];
            weapon.AudioSource.PlayOneShot(clip);
        }

        public void PlayReloadSound()
        {
            var list = weapon.WeaponSoundsList.ReloadSounds;
            var clip = list[Random.Range(0, list.Count - 1)];
            weapon.AudioSource.PlayOneShot(clip);
        }
    }
}