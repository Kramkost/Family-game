using UnityEngine;
using Mirror;
using Kotenkoff;

/// <summary>
/// Еда. Можно подобрать (Е) и съесть (ЛКМ).
/// </summary>
public class FoodItem : PickupableItem, IUsableItem
{
    [Header("Food Stats")]
    [SerializeField] private float nutritionValue = 30f;
    

    [Server]
    public void ServerUse(PlayerEntity player)
    {
       
        if (player.TryGetComponent(out PlayerStats stats))
        {
            
            stats.Eat(nutritionValue);
            
          
            player.DestroyHeldItem(); 
        }
    }
}