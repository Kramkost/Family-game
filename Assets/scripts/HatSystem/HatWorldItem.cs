using UnityEngine;
using Mirror;

namespace Kotenkoff
{
    [RequireComponent(typeof(Collider))]
    public class HatWorldItem : MonoBehaviour, IInteractable
    {
        [Tooltip("ID шапки из HatDatabase, которая наденется при взаимодействии")]
        [SerializeField] private int hatId;

        // Этот метод вызывается на СЕРВЕРЕ из твоего PlayerEntity -> CmdInteract
        public void ServerInteract(PlayerEntity player, PlayerInventory inventory)
        {
            if (player.TryGetComponent(out PlayerHatController hatController))
            {
                // Поскольку мы УЖЕ на сервере, нам не нужно вызывать CmdEquipHat.
                // Мы просто напрямую меняем SyncVar. 
                // Mirror сам разошлет изменения всем клиентам и вызовет OnHatIdChanged!
                hatController.currentHatId = hatId;
            }
        }
    }
}