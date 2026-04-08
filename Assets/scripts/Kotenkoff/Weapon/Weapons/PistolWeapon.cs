using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Kotenkoff.Weapon.Weapons
{
    [RequireComponent(typeof(NetworkIdentity))]
    public sealed class PistolWeapon : Weapon
    {
        [Header("Другое:")]
        [SerializeField, Tooltip("Слои, с которыми взаимодействует raycast оружия.")]
        private LayerMask raycastLayerMask;
        
        [SerializeField] InputActionReference shootAction;
        [SerializeField] InputActionReference reloadAction;
        
        [SerializeField] private Camera raycastCamera;
        private Ray ray;
        
        private ISoundSource testSoundSource;

        private void OnEnable()
        {
            if (shootAction != null)
            {
                shootAction.action.performed += OnShoot;
                shootAction.action.Enable();
            }

            if (reloadAction != null)
            {
                reloadAction.action.performed += OnReload;
                reloadAction.action.Enable();
            }
        }

        private void OnDisable()
        {
            if (shootAction != null)
            {
                shootAction.action.performed -= OnShoot;
                shootAction.action.Disable();
            }
            
            if (reloadAction != null)
            {
                reloadAction.action.performed -= OnReload;
                reloadAction.action.Disable();
            }
        }

        private void Start()
        {
            weaponMode = WeaponModes.Single;
            
            currentAmmo = maxAmmo;
            canShoot = true;

            SoundSource = new WeaponSoundSource(this);
        }

        private void OnShoot(InputAction.CallbackContext ctx)
        {
            if (ctx.performed)
            {
               RaySetup(ctx.ReadValue<Vector2>());
               Shoot(); 
            }
        }

        private void OnReload(InputAction.CallbackContext ctx)
        {
            if (ctx.performed)
            {
                Reload();
            }
        }
        
        public override void Shoot()
        {
            if (weaponMode != WeaponModes.Single) return;
            
            Debug.DrawRay(ray.origin, ray.direction * 10, Color.yellow);

            if (currentAmmo > 0 && canShoot && !isReloading)
            {
                if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, raycastLayerMask))
                {
                    currentAmmo--;
                    canShoot = false;
                    Debug.DrawRay(hit.point, hit.normal * 10, Color.yellow);
                    Invoke(nameof(ShootInvoke), shootingDelay);
                }
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
            ray = raycastCamera.ScreenPointToRay(point);
        }
    }
}