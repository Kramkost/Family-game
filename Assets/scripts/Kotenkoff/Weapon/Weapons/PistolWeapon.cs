using Health_Bar_System;
using Kotenkoff.Monsters;
using Mirror;
using Mirror.Examples.Tanks;
using UnityEngine;

namespace Kotenkoff.Weapon.Weapons
{
    [RequireComponent(typeof(NetworkIdentity))]
    public sealed class PistolWeapon : Weapon
    {
        [Header("Другое:")]
        [SerializeField, Tooltip("Слои, с которыми взаимодействует raycast оружия.")]
        private LayerMask raycastLayerMask;
        private Camera raycastCamera;
        
        private Ray ray;
        private ISoundSource testSoundSource;
        private GameObject targetObject;
        private ICalculator _calculator;
        
        private void Start()
        {
            weaponMode = WeaponModes.Single;
            
            currentAmmo = maxAmmo;
            canShoot = true;

            SoundSource = new WeaponSoundSource(this);
            _calculator = new PistolWeaponDamageCalculator(this);
        }

        private void Update()
        {
            Debug.DrawRay(ray.origin, ray.direction * 10, Color.purple);
        }

        public override void ServerInteract(PlayerEntity player, PlayerInventory inventory)
        {
            base.ServerInteract(player, inventory);
            
            Player = player;
            
            RaySetup();
        }

        public override void Shoot()
        {
            if (weaponMode != WeaponModes.Single) return;
            
            RaySetup(Player.SingleShotMousePosition);
            
            if (currentAmmo > 0 && canShoot && !isReloading)
            {
                // играть эффекты разные
                
                
                
                if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, raycastLayerMask))
                {
                    Debug.Log($"Пистолет попал в {hit.transform.name}");
                    
                    Debug.Log(hit.transform.name);
                    
                    bool isMonster = hit.collider.TryGetComponent(out Monster monster);
                    bool isPlayer = hit.collider.TryGetComponent(out PlayerStats playerStats);
                    
                    
                    if (isMonster && !isPlayer) DamageMonster(monster);
                    else if (isPlayer && !isMonster) DamagePlayer(playerStats);
                }
                
                currentAmmo--;
                canShoot = false;
                Invoke(nameof(ShootInvoke), shootingDelay);
            }
        }
        
        private void ShootInvoke() => canShoot = true;

        public override void Reload()
        {
            isReloading = true;
            canShoot = false;
            Invoke(nameof(ReloadInvoke), reloadTime);
        }

        private void ReloadInvoke()
        {
            currentAmmo = maxAmmo;
            isReloading = false;
            canShoot = true;
        }
        
        
        private void RaySetup(Vector2 point = default)
        {
            raycastCamera = Player.PublicPlayerMovement.PublicCamera;
            
            ray = raycastCamera.ScreenPointToRay(point);
        }

        private void DamageMonster(Monster monster)
        {
            var calculatedDamage = _calculator.Calculate<float>("damage");
            
            monster.ChangeHealth(-calculatedDamage);
        }

        private void DamagePlayer(PlayerStats playerStats)
        {
            // Наносим игроку урон

            if (playerStats.gameObject.GetComponent<PlayerEntity>() != Player)
            {
                var calculatedDamage = _calculator.Calculate<float>("damage");
                
                playerStats.TakeDamage(calculatedDamage);
            }
            else
            {
                Debug.LogWarning("Игрок стреляет сам в себя.");
            }
        }
    }
}