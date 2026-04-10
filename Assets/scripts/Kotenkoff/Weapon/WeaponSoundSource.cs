using UnityEngine;

namespace Kotenkoff.Weapon
{
    public sealed class WeaponSoundSource : ISoundSource
    {
        private readonly Weapon weapon;

        public WeaponSoundSource(Weapon weapon)
        {
            this.weapon = weapon;
        }

        public void PlaySound(string action)
        {
            switch (action)
            {
                case "reload":
                    PlayReloadSound();
                    break;
                case "shoot":
                    PlayShootSound();
                    break;
            }
        }
        
        private void PlayShootSound()
        {
            var list = weapon.WeaponSoundsList.ShotSounds;

            if (list.Count <= 0)
            {
                var clip = list[Random.Range(0, list.Count - 1)]; 
                weapon.AudioSource?.PlayOneShot(clip); 
            }
            else
            {
                Debug.LogWarning("Лист звуков для стрельбы пуст.");
            }
        }

        private void PlayReloadSound()
        {
            var list = weapon.WeaponSoundsList.ReloadSounds;
            if (list.Count <= 0)
            {
                var clip = list[Random.Range(0, list.Count - 1)];
                weapon.AudioSource?.PlayOneShot(clip);
            }
            else
            {
                Debug.LogWarning("Лист звуков для перезарядки пуст.");
            }
        }
    }
}