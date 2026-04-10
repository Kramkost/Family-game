using System;
using Mirror;
using UnityEngine;

namespace Kotenkoff.Weapon
{
    public abstract class Weapon : PickupableItem
    {
        /// <summary>
        ///  <para>Текущий режим стрельбы оружия.</para>
        /// </summary>
        [Header("Стрельба:")]
        
        [SerializeField, Tooltip("Текущий режим стрельбы оружия.")]
        protected WeaponModes weaponMode; // Возможные режимы стрельбы надо прописывать в производном классе.

        /// <summary>
        ///  <para>Время между выстрелами.</para>
        /// </summary>
        [SerializeField, Tooltip("Время между выстрелами."), Space(3)]
        protected float shootingDelay;

        /// <summary>
        /// Базовый урон оружия.
        /// </summary>
        [SerializeField, Space(3), Tooltip("Урон оружия.")]
        protected float damage;
        /// <summary>
        /// Урон оружия.
        /// </summary>
        public float WeaponDamage => damage;
        
        /// <summary>
        ///  <para>Может ли оружие сейчас стрелять?</para>
        /// </summary>
        [SerializeField, ReadOnly,Tooltip("Может ли оружие сейчас стрелять?"), Space(5)]
        protected bool canShoot;
        
        
        /// <summary>
        ///  <para>Максимальное количество патронов.</para>
        /// </summary>
        [Header("Патроны:")]
        
        [SerializeField, Tooltip("Максимальное количество патронов")]
        protected int maxAmmo;
        
        /// <summary>
        ///  <para>Текущее количество патронов.</para>
        /// </summary>
        [SerializeField, Tooltip("Текущее количество патронов.")]
        protected int currentAmmo;
        
        /// <summary>
        ///  <para>Время перезарядки.</para>
        /// </summary>
        [Header("Перезарядка:")]
        
        [SerializeField, Tooltip("Время перезарядки")]
        protected float reloadTime;
        
        /// <summary>
        ///  <para>Перезаряжается ли сейчас оружие?</para>
        /// </summary>
        [SerializeField, ReadOnly, Tooltip("Перезаряжается ли сейчас оружие?"), Space(5)]
        protected bool isReloading;
        
        /// <summary>
        ///  <para>Источник звука оружия.</para>
        /// </summary>
        [SerializeField, Tooltip("Источник звука оружия."), Header("Аудио:")]
        protected AudioSource audioSource;
        public AudioSource AudioSource => audioSource;

        [SerializeField, Tooltip("Ссылка на компонент 'WeaponSounds'")]
        protected WeaponSounds weaponSounds;
        public WeaponSounds WeaponSoundsList => weaponSounds;
        
        /// <summary>
        /// Экземпляр класса наследуемого от интерфейса <b>
        /// <see cref="ISoundSource"/> </b> .
        /// </summary>
        protected ISoundSource SoundSource;

        /// <summary>
        /// Ссылка на игрока, который владеет данным оружием.
        /// </summary>
        [SerializeField, ReadOnly, Tooltip("Игрок, который владеет этим оружием.")]
        protected PlayerEntity Player;
        
        /// <summary>
        ///  <para>Метод выстрела оружия.</para>
        /// </summary>
        public abstract void Shoot();
        
        /// <summary>
        ///  <para>Метод перезарядки оружия.</para>
        /// </summary>
        public abstract void Reload();

        /// <summary>
        /// Просто метод, который очищает ссылку на игрока у оружия.
        /// </summary>
        public void DropWeapon()
        {
            Player = null;
        }

        private void Start()
        {
            if (weaponSounds == null) weaponSounds = GetComponent<WeaponSounds>(); 
        }
    }
}