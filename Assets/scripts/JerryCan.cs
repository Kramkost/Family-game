using Kotenkoff;
using Mirror;
using UnityEngine;

/// <summary>
/// Канистра, которую можно взять в руки.
/// </summary>
public class JerryCan : NetworkBehaviour, IInteractable
{
    [Server]
    public void ServerInteract(PlayerEntity player, PlayerInventory inventory)
    {
        // Проверяем, что руки у игрока свободны
        if (player.heldItem == null)
        {
            // 1. Добавляем предмет в логический инвентарь
            inventory.AddItem(gameObject);
            
            // 2. Передаем предмет в руку игрока!
            // Этот метод внутри PlayerEntity обновит SyncVar, и магия Mirror 
            // автоматически прикрепит канистру к кости руки у ВСЕХ клиентов на сервере.
            player.ServerEquipItem(this.netIdentity);
        }
    }

    [Server]
    public void RemoveFromInventory(PlayerInventory inventory)
    {
        inventory.RemoveItem(gameObject);
    }
}