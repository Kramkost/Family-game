using UnityEngine;
using Mirror;
using Kotenkoff;

/// <summary>
/// Универсальный класс для всех предметов, которые можно взять в руки.
/// </summary>
[RequireComponent(typeof(NetworkIdentity))]
public class PickupableItem : NetworkBehaviour, IInteractable
{
    [Server]
    public virtual void ServerInteract(PlayerEntity player, PlayerInventory inventory)
    {
        // Если руки свободны — берём предмет
        if (player.heldItem == null)
        {
            inventory.AddItem(gameObject);
            player.ServerEquipItem(this.netIdentity);
        }
    }
}