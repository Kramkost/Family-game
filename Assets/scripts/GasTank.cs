using UnityEngine;
using Mirror;

/// <summary>
/// Бензобак автомобиля. Ждет, когда к нему подойдут с канистрой.
/// </summary>
public class GasTank : NetworkBehaviour, IInteractable
{
    [SerializeField] private CarResourceManager carManager;
    [SerializeField] private float refuelAmount = 25f;

    [Server]
    public void ServerInteract(PlayerEntity player)
    {
        // Проверяем, держит ли игрок канистру
        if (player.heldItem != null && player.heldItem.TryGetComponent(out JerryCan can))
        {
            carManager.Refuel(refuelAmount);
            
            // Уничтожаем или прячем канистру после использования
            NetworkServer.Destroy(can.gameObject);
        }
    }
}