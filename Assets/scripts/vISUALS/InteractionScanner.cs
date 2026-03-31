using UnityEngine;
using TMPro;
using Kotenkoff; 

public class InteractionScanner : MonoBehaviour
{
    [Header("Настройки луча")]
    public Transform playerCamera; 
    public float interactRange = 3f;
    public LayerMask interactLayerMask;

    [Header("UI")]
    public TMP_Text promptText; 

    void Update()
    {
        if (playerCamera == null || promptText == null) return;

        if (Physics.Raycast(playerCamera.position, playerCamera.forward, out RaycastHit hit, interactRange, interactLayerMask))
        {
            
            NetworkSceneChanger sceneChanger = hit.collider.GetComponentInParent<NetworkSceneChanger>();
            
            if (sceneChanger != null)
            {
                promptText.text = sceneChanger.promptMessage;
                promptText.gameObject.SetActive(true);
                return;
            }
        }

        if (promptText.gameObject.activeSelf)
        {
            promptText.gameObject.SetActive(false);
        }
    }
}