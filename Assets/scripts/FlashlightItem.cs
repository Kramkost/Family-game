using Mirror;
using UnityEngine;

// ЧЕ ВЫЛУПИЛСЯ? ИДИ РАБОТАЙ.
public class FlashlightItem : PickupableItem, IUsableItem
{
    [Header("Flashlight Settings")]
    [SerializeField] private Light spotLight;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip toggleSound;

    [SyncVar(hook = nameof(OnLightStateChanged))]
    private bool isLightOn = false;

    public override void OnStartClient()
    {
        base.OnStartClient();
        UpdateLightVisuals(isLightOn);
    }

    [Server]
    public void ServerUse(PlayerEntity player)
    {
        //Debug.Log(isLightOn);
        
        isLightOn = !isLightOn;
    }

    private void OnLightStateChanged(bool oldState, bool newState)
    {
        UpdateLightVisuals(newState);

        if (audioSource != null && toggleSound != null)
        {
            audioSource.pitch = Random.Range(0.9f, 1.1f);
            audioSource.PlayOneShot(toggleSound);
        }
    }

    private void UpdateLightVisuals(bool state)
    {
        if (spotLight != null)
        {
            spotLight.enabled = state;
        }
    }
}