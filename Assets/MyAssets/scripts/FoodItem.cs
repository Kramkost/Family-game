using Health_Bar_System;
using UnityEngine;
using Mirror;

/// <summary>
/// Еда. Можно подобрать (Е) и съесть (ЛКМ).
/// </summary>
public class FoodItem : PickupableItem, IUsableItem
{
    [Header("Food Stats")]
    [SerializeField] private float nutritionValue = 30f;

    [SerializeField] private AudioClip EatSound;
    

    [Server]
    public void ServerUse(PlayerEntity player)
    {
       
        if (player.TryGetComponent(out PlayerStats stats))
        {

        if (EatSound != null)
        {
            RpcPlayEatSound();
        }
            
            stats.Eat(nutritionValue);


            
          
            player.DestroyHeldItem(); 
        }
    }


        [ClientRpc]
    private void RpcPlayEatSound()
    {
            
        if (TryGetComponent(out AudioSource audioSource) && EatSound != null)
        {
            audioSource.pitch = Random.Range(0.9f, 1.1f);
            audioSource.PlayOneShot(EatSound);
        }
    }
}