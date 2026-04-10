using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using System.Collections;
using vISUALS;

namespace Kotenkoff
{
    public struct SyncInventorySlot
    {
        public bool isClaimed;
        public NetworkIdentity itemNetId;
    }

    public sealed class PlayerInventory : NetworkBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerEntity playerEntity;
        [SerializeField] private int maxSlots = 5;

        [Header("UI: Inventory Panel Animations")]
        [Tooltip("Группа для скрытия всего инвентаря (слотов)")]
        [SerializeField] private CanvasGroup mainInventoryCanvasGroup;
        [SerializeField] private float uiFadeSpeed = 5f;

        [Header("UI: Slots Settings (Local Only)")]
        [SerializeField] private RectTransform[] uiSlotContainers;
        [SerializeField] private float activeScale = 1.2f;
        [SerializeField] private float normalScale = 1.0f;

        [Header("UI: Item Info Settings (Local Only)")]
        [SerializeField] private CanvasGroup infoCanvasGroup; 
        [SerializeField] private RectTransform infoRectTransform; 
        [SerializeField] private TextMeshProUGUI itemNameText;
        [SerializeField] private TextMeshProUGUI itemDescText;
        [SerializeField] private TextMeshProUGUI actionHintText;

        public readonly SyncList<SyncInventorySlot> slots = new SyncList<SyncInventorySlot>();

        private int currentSelectedIndex = 0;
        private Coroutine uiAnimationCoroutine;
        private Vector2 baseInfoPosition; 
        
        // Переменные для анимаций инвентаря
        private float lastInteractionTime;
        private float hideDelay;
        private float[] currentSlotScales;

        public override void OnStartServer()
        {
            for (int i = 0; i < maxSlots; i++)
            {
                slots.Add(new SyncInventorySlot { isClaimed = false });
            }
        }

        public override void OnStartLocalPlayer()
        {
            if (infoRectTransform != null)
            {
                baseInfoPosition = infoRectTransform.anchoredPosition;
            }
            
            if (infoCanvasGroup != null) infoCanvasGroup.alpha = 0f;
            if (actionHintText != null) actionHintText.gameObject.SetActive(false);

            // Инициализация массивов для плавного скейла
            if (uiSlotContainers != null)
            {
                currentSlotScales = new float[uiSlotContainers.Length];
                for (int i = 0; i < currentSlotScales.Length; i++)
                {
                    currentSlotScales[i] = normalScale;
                }
            }

            ResetVisibilityTimer();
        }

        private void Update()
        {
            if (!isLocalPlayer) return;

            // Обрабатываем ввод, если не сидим
            if (!playerEntity.IsSitting)
            {
                HandleScrollInput();
            }

            HandleInventoryAnimations();
        }

        private void HandleInventoryAnimations()
        {
            // 1. Общее затухание инвентаря при бездействии
            if (mainInventoryCanvasGroup != null)
            {
                float targetAlpha = (Time.time - lastInteractionTime > hideDelay) ? 0f : 1f;
                mainInventoryCanvasGroup.alpha = Mathf.Lerp(mainInventoryCanvasGroup.alpha, targetAlpha, Time.deltaTime * uiFadeSpeed);
            }

            // 2. Плавный скейл слотов (вместо резкого UpdateUI)
            if (uiSlotContainers == null || currentSlotScales == null) return;

            for (int i = 0; i < uiSlotContainers.Length; i++)
            {
                if (uiSlotContainers[i] != null)
                {
                    float targetScale = (i == currentSelectedIndex) ? activeScale : normalScale;
                    currentSlotScales[i] = Mathf.Lerp(currentSlotScales[i], targetScale, Time.deltaTime * uiFadeSpeed * 1.5f);
                    uiSlotContainers[i].localScale = Vector3.one * currentSlotScales[i];
                }
            }
        }

        private void HandleScrollInput()
        {
            if (Mouse.current == null) return;

            float scroll = Mouse.current.scroll.ReadValue().y;

            if (scroll != 0)
            {
                if (scroll > 0) currentSelectedIndex--;
                else currentSelectedIndex++;

                if (currentSelectedIndex < 0) currentSelectedIndex = maxSlots - 1;
                if (currentSelectedIndex >= maxSlots) currentSelectedIndex = 0;

                ResetVisibilityTimer(); // Пробуждаем инвентарь
                UpdateItemInfoUI(); 
                CmdSelectSlot(currentSelectedIndex); 
            }
        }

        private void ResetVisibilityTimer()
        {
            lastInteractionTime = Time.time;
            hideDelay = Random.Range(2f, 4f);
        }

        // --- ЛОГИКА АНИМАЦИИ ТЕКСТА (Осталась без изменений) ---
        private void UpdateItemInfoUI()
        {
            if (uiAnimationCoroutine != null) StopCoroutine(uiAnimationCoroutine);

            var slot = slots[currentSelectedIndex];
            if (slot.isClaimed && slot.itemNetId != null && slot.itemNetId.TryGetComponent(out ItemDetails details))
            {
                if (itemNameText != null) itemNameText.text = details.itemName;
                if (itemDescText != null) itemDescText.text = details.itemDescription;
                
                if (actionHintText != null)
                {
                    bool hasHint = !string.IsNullOrEmpty(details.actionHint);
                    actionHintText.text = details.actionHint;
                    actionHintText.gameObject.SetActive(hasHint);
                }

                uiAnimationCoroutine = StartCoroutine(AnimateInfoUI(true));
            }
            else
            {
                if (actionHintText != null) actionHintText.gameObject.SetActive(false);
                uiAnimationCoroutine = StartCoroutine(AnimateInfoUI(false));
            }
        }

        private IEnumerator AnimateInfoUI(bool show)
        {
            if (infoCanvasGroup == null || infoRectTransform == null) yield break;

            float duration = 0.15f; 
            float elapsed = 0f;
            
            float startAlpha = infoCanvasGroup.alpha;
            float targetAlpha = show ? 0.95f : 0f; 

            Vector2 startPos = infoRectTransform.anchoredPosition;
            Vector2 targetPos = baseInfoPosition;
            if (show)
            {
                startPos = baseInfoPosition + new Vector2(0, -20f); 
            }

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float smoothT = t * (2f - t); 

                infoCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, smoothT);
                if (show) infoRectTransform.anchoredPosition = Vector2.Lerp(startPos, targetPos, smoothT);

                yield return null;
            }

            infoCanvasGroup.alpha = targetAlpha;
            if (show) infoRectTransform.anchoredPosition = targetPos;
        }

        // --- СЕТЕВАЯ ЛОГИКА ---
        [Command]
        private void CmdSelectSlot(int index)
        {
            if (index < 0 || index >= slots.Count) return;

            var slot = slots[index];
            if (slot.isClaimed && slot.itemNetId != null)
            {
                playerEntity.ServerEquipItem(slot.itemNetId);
            }
            else
            {
                playerEntity.ServerEquipItem(null); 
            }
        }

        [Server]
        public bool AddItem(GameObject itemObj)
        {
            if (!itemObj.TryGetComponent(out NetworkIdentity netId)) return false;

            for (int i = 0; i < slots.Count; i++)
            {
                if (!slots[i].isClaimed)
                {
                    slots[i] = new SyncInventorySlot { isClaimed = true, itemNetId = netId };
                    TargetCheckAutoEquip(netId.connectionToClient);
                    return true;
                }
            }
            Debug.LogWarning("[Inventory] Инвентарь полон!");
            return false;
        }

        [Server]
        public void RemoveItem(GameObject itemObj)
        {
            if (!itemObj.TryGetComponent(out NetworkIdentity netId)) return;

            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i].isClaimed && slots[i].itemNetId == netId)
                {
                    slots[i] = new SyncInventorySlot { isClaimed = false };
                    break;
                }
            }
        }

        [TargetRpc]
        private void TargetCheckAutoEquip(NetworkConnection target)
        {
            ResetVisibilityTimer(); // Пробуждаем инвентарь при получении предмета
            CmdSelectSlot(currentSelectedIndex);
            UpdateItemInfoUI(); 
        }
    }
}