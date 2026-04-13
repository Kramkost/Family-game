using Kotenkoff;
using UnityEngine;
using Mirror;

/// <summary>
/// Логика посадочного места с безопасной выдачей сетевых прав и диагностикой.
/// </summary>
public class CarSeat : NetworkBehaviour, IInteractable
{
    [Header("Seat Configuration")]
    public bool isDriverSeat = false;
    
    [Tooltip("Ссылка на главную логику машины.")]
    public CarHybridSystem carSystem;

    [SyncVar, ReadOnly] public NetworkIdentity occupant;

    [Tooltip("Точка, куда привязывается игрок (внутри машины)")]
    public Transform viewPoint; 
    
    [Tooltip("Точка, куда игрока телепортирует ПРИ ВЫХОДЕ (возле двери на улице)")]
    public Transform exitPoint; 

    [Server]
    public void ServerInteract(PlayerEntity player, PlayerInventory inventory)
    {
        if (carSystem == null)
        {
            Debug.LogError("[Сервер-Сиденье] ОТКАЗ: Потеряна ссылка на CarHybridSystem! Проверь Инспектор кресла.");
            return;
        }

        // --- УМНАЯ ПРОВЕРКА ЗАНЯТОСТИ ---
        if (occupant != null)
        {
            // Если место занято ЭТИМ ЖЕ игроком (двойной клик) - просто молча выходим
            if (occupant == player.netIdentity) return; 

            Debug.LogWarning($"[Сервер-Сиденье] ОТКАЗ: Место уже занято объектом {occupant.name}!");
            return;
        }

        if (player.heldItem != null)
        {
            Debug.LogWarning("[Сервер-Сиденье] ОТКАЗ: Игрок держит предмет в руках!");
            return;
        }
        // --------------------------------

        occupant = player.netIdentity;
        carSystem.UpdatePassengerCount(1);

        if (isDriverSeat)
        {
            NetworkIdentity carNetId = carSystem.netIdentity;
            if (carNetId.connectionToClient != null && carNetId.connectionToClient != player.connectionToClient)
                carNetId.RemoveClientAuthority();
            
            if (carNetId.connectionToClient != player.connectionToClient)
                carNetId.AssignClientAuthority(player.connectionToClient);
        }

        player.serverCurrentSeat = this;

        player.TargetEnterSeat(carSystem.netIdentity, gameObject.name);
    }

    [Server]
    public void ServerLeave(PlayerEntity player)
    {
        if (occupant != player.netIdentity) return;

        occupant = null;

        player.serverCurrentSeat = null;
        
        if (carSystem != null)
        {
            carSystem.UpdatePassengerCount(-1);

            if (isDriverSeat)
            {
                if (carSystem.netIdentity.connectionToClient == player.connectionToClient)
                {
                    carSystem.netIdentity.RemoveClientAuthority();
                }
            }
        }

        player.TargetLeaveSeat();
    }
}