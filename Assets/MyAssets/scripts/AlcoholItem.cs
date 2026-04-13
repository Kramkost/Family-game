using Mirror;
using UnityEngine;

public class AlcoholItem : PickupableItem, IUsableItem
{
    [Header("Alcohol Settings")]
    [Tooltip("Сколько секунд длится эффект опьянения")]
    [SerializeField] private float drunkDuration = 15f;
        
    [Tooltip("Сила покачивания камеры")]
    [SerializeField] private float drunkIntensity = 2f;

    [Header("Audio")]
    [SerializeField] private AudioClip drinkSound;

    [Server]
    public void ServerUse(PlayerEntity player)
    {
            
        if (drinkSound != null)
        {
            RpcPlayDrinkSound();
        }

           
        player.TargetApplyDrunkEffect(player.connectionToClient, drunkDuration, drunkIntensity);

            
        player.DestroyHeldItem();
    }

    [ClientRpc]
    private void RpcPlayDrinkSound()
    {
            
        if (TryGetComponent(out AudioSource audioSource) && drinkSound != null)
        {
            audioSource.pitch = Random.Range(0.9f, 1.1f);
            audioSource.PlayOneShot(drinkSound);
        }
    }
}