using Kotenkoff;
using Mirror;
/// <summary>
/// Канистра, которую можно взять в руки.
/// </summary>
public class JerryCan : NetworkBehaviour, IInteractable
{
    [Server]
    public void ServerInteract(PlayerEntity player, PlayerInventory inventory)
    {
        if (player.heldItem == null)
        {
            // Логика взятия предмета в руки
            player.heldItem = netIdentity;
            
            inventory.AddItem(gameObject);
            
            // В реальном проекте здесь вы прикрепите объект к руке игрока 
            // через ClientRpc или изменение иерархии (Server -> Client)
            RpcAttachToPlayer(player);
        }
    }

    [ClientRpc]
    private void RpcAttachToPlayer(PlayerEntity player)
    {
        // Простая визуальная привязка канистры
        transform.SetParent(player.heldItemProxy);
        transform.localPosition = player.heldItemProxy.transform.localPosition; 
    }

    [Server]
    public void RemoveFromInventory(PlayerInventory inventory)
    {
        inventory.RemoveItem(gameObject);
    }
}