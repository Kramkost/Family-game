using UnityEngine;
using Mirror;
/// <summary>
/// Канистра, которую можно взять в руки.
/// </summary>
public class JerryCan : NetworkBehaviour, IInteractable
{
    [Server]
    public void ServerInteract(PlayerEntity player)
    {
        if (player.heldItem == null)
        {
            // Логика взятия предмета в руки
            player.heldItem = this.netIdentity;
            
            // В реальном проекте здесь вы прикрепите объект к руке игрока 
            // через ClientRpc или изменение иерархии (Server -> Client)
            RpcAttachToPlayer(player.netIdentity);
        }
    }

    [ClientRpc]
    private void RpcAttachToPlayer(NetworkIdentity playerIdentity)
    {
        // Простая визуальная привязка канистры
        transform.SetParent(playerIdentity.transform);
        transform.localPosition = new Vector3(0.5f, 0.5f, 1f); 
    }
}