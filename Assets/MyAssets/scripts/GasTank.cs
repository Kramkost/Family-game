using Kotenkoff;
using UnityEngine;
using Mirror;

/// <summary>
/// Бензобак автомобиля. Ждет, когда к нему подойдут с канистрой.
/// </summary>
public class GasTank : NetworkBehaviour, IInteractable
{
    [Tooltip("Ссылка на скрипт CarResourceManager (висит на корне машины)")]
    [SerializeField] private CarResourceManager carManager;
    
    [SerializeField] private float refuelAmount = 25f;

    [Server]
    public void ServerInteract(PlayerEntity player, PlayerInventory inventory)
    {
        Debug.Log($"[Бензобак] Игрок пытается заправиться. Держит предмет: {(player.heldItem != null ? player.heldItem.name : "Ничего")}");

        
        if (carManager == null)
        {
            Debug.LogError("[Бензобак] ОШИБКА: В инспекторе не назначен CarResourceManager! Перетащите корень машины в поле Car Manager скрипта GasTank.");
            return;
        }

        
        if (player.heldItem == null)
        {
            Debug.LogWarning("[Бензобак] Игрок пришел с пустыми руками! Нужно сначала взять канистру.");
            return;
        }

               if (player.heldItem.TryGetComponent(out JerryCan can))
        {
            float oldGas = carManager.gasoline;
            carManager.Refuel(refuelAmount);
            
            Debug.Log($"[Бензобак] УСПЕХ! Бензин залит: {oldGas} -> {carManager.gasoline}");
            
            can.RemoveFromInventory(inventory);
            NetworkServer.Destroy(can.gameObject);
            
            
            player.heldItem = null; 
        }
        else
        {
            Debug.LogWarning($"[Бензобак] Игрок держит {player.heldItem.name}, а не JerryCan!");
        }
    }
}