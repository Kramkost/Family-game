using Mirror;
using MyAssets.scripts.Kotenkoff.Items;
using TMPro;
using UnityEngine;

namespace MyAssets.scripts.Kotenkoff.Character
{
    public class CharacterTooltipScanner : NetworkBehaviour
    {
        [Header("UI:")]
        [SerializeField, Tooltip("Текст, который выводит имя предмета.")] private TextMeshProUGUI itemNameTextUI;
        [SerializeField, Tooltip("Текст, который выводит описание предмета.")] private TextMeshProUGUI descriptionTextUI;
        [SerializeField, Tooltip("Текст, который выводит подсказку для предмета.")] private TextMeshProUGUI tooltipTextUI;

        [Header("Настройки:")]
        [SerializeField] private float scanRange = 3f;
        [SerializeField] private LayerMask scanLayerMasks;
        private Transform cameraTransform;
        
        [Header("Компоненты:")]
        [SerializeField] private CharacterBase characterBase;

        private void Start()
        {
            if (characterBase == null) characterBase = GetComponent<CharacterBase>();
            if (cameraTransform == null) cameraTransform = characterBase.FpCamera.transform;
            
            if (itemNameTextUI != null) itemNameTextUI.gameObject.SetActive(false);
            if (descriptionTextUI != null) descriptionTextUI.gameObject.SetActive(false);
            if (tooltipTextUI != null) tooltipTextUI.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (!isLocalPlayer || cameraTransform == null || itemNameTextUI == null || descriptionTextUI == null || tooltipTextUI == null) return;

            if (Physics.Raycast(cameraTransform.position, cameraTransform.forward, out var hit, scanRange,
                    scanLayerMasks))
            {
                var itemInfo = hit.collider.GetComponentInParent<IItemTooltip>();

                if (itemInfo != null)
                {
                    itemNameTextUI.text = itemInfo.ItemName;
                    descriptionTextUI.text = itemInfo.ItemDescription;
                    tooltipTextUI.text = itemInfo.ItemTooltip;
                    
                    if (!tooltipTextUI.gameObject.activeSelf) tooltipTextUI.gameObject.SetActive(true);
                    if (!itemNameTextUI.gameObject.activeSelf) itemNameTextUI.gameObject.SetActive(true);
                    if (!descriptionTextUI.gameObject.activeSelf) descriptionTextUI.gameObject.SetActive(true);
                    return;
                }
            }
            
            if (itemNameTextUI.gameObject.activeSelf) itemNameTextUI?.gameObject.SetActive(false);
            if (descriptionTextUI.gameObject.activeSelf) descriptionTextUI?.gameObject.SetActive(false);
            if (tooltipTextUI.gameObject.activeSelf) tooltipTextUI?.gameObject.SetActive(false);
        }
    }
}