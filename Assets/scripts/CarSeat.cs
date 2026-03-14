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

    [SyncVar] public NetworkIdentity occupant;

    [Server]
    public void ServerInteract(PlayerEntity player, PlayerInventory inventory)
    {
        Debug.Log($"[Сервер-Сиденье] Начинаем посадку игрока {player.name}...");

        // --- РАСШИРЕННАЯ ДИАГНОСТИКА ---
        if (carSystem == null)
        {
            Debug.LogError("[Сервер-Сиденье] ОТКАЗ: Потеряна ссылка на CarHybridSystem! Проверь Инспектор кресла.");
            return;
        }
        if (occupant != null)
        {
            Debug.LogWarning($"[Сервер-Сиденье] ОТКАЗ: Место уже занято объектом {occupant.name}! (Возможно это баг прошлого теста)");
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
            {
                carNetId.RemoveClientAuthority();
            }
            
            if (carNetId.connectionToClient != player.connectionToClient)
            {
                carNetId.AssignClientAuthority(player.connectionToClient);
            }
        }

        Debug.Log($"[Сервер-Сиденье] ВСЕ ОТЛИЧНО! Отправляем клиенту RPC сесть в {gameObject.name}");
        player.TargetEnterSeat(carSystem.netIdentity, gameObject.name);
    }

    [Server]
    public void ServerLeave(PlayerEntity player)
    {
        if (occupant != player.netIdentity) return;

        occupant = null;
        
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