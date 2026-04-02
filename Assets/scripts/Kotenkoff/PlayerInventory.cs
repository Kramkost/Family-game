using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro; // Обязательно для красивого текста
using System.Collections;

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

        [Header("UI: Slots Settings (Local Only)")]
        [SerializeField] private RectTransform[] uiSlotContainers;
        [SerializeField] private float activeScale = 1.2f;
        [SerializeField] private float normalScale = 1.0f;

        [Header("UI: Item Info Settings (Local Only)")]
        [SerializeField, Tooltip("Группа, на которой висит текст названия и описания")] 
        private CanvasGroup infoCanvasGroup; 
        [SerializeField] private RectTransform infoRectTransform; // Для сдвига вверх
        [SerializeField] private TextMeshProUGUI itemNameText;
        [SerializeField] private TextMeshProUGUI itemDescText;
        
        [SerializeField, Tooltip("Отдельный текст справа на экране")] 
        private TextMeshProUGUI actionHintText;

        public readonly SyncList<SyncInventorySlot> slots = new SyncList<SyncInventorySlot>();

        private int currentSelectedIndex = 0;
        private Coroutine uiAnimationCoroutine;
        private Vector2 baseInfoPosition; // Базовая позиция для анимации подъема

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

            UpdateUI(); 
        }

        private void Update()
        {
            if (!isLocalPlayer || playerEntity.IsSitting) return;
            HandleScrollInput();
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

                UpdateUI();
                UpdateItemInfoUI(); // Запускаем анимацию текста
                CmdSelectSlot(currentSelectedIndex); 
            }
        }

        private void UpdateUI()
        {
            if (uiSlotContainers == null || uiSlotContainers.Length == 0) return;

            for (int i = 0; i < uiSlotContainers.Length; i++)
            {
                if (uiSlotContainers[i] != null)
                {
                    float targetScale = (i == currentSelectedIndex) ? activeScale : normalScale;
                    uiSlotContainers[i].localScale = Vector3.one * targetScale;
                }
            }
        }

        // --- ЛОГИКА АНИМАЦИИ ТЕКСТА ---

        private void UpdateItemInfoUI()
        {
            if (uiAnimationCoroutine != null) StopCoroutine(uiAnimationCoroutine);

            // Проверяем, есть ли предмет в выбранном слоте
            var slot = slots[currentSelectedIndex];
            if (slot.isClaimed && slot.itemNetId != null && slot.itemNetId.TryGetComponent(out ItemDetails details))
            {
                // Применяем текст
                if (itemNameText != null) itemNameText.text = details.itemName;
                if (itemDescText != null) itemDescText.text = details.itemDescription;
                
                // Подсказка справа (включаем только если текст не пустой)
                if (actionHintText != null)
                {
                    bool hasHint = !string.IsNullOrEmpty(details.actionHint);
                    actionHintText.text = details.actionHint;
                    actionHintText.gameObject.SetActive(hasHint);
                }

                // Запускаем появление
                uiAnimationCoroutine = StartCoroutine(AnimateInfoUI(true));
            }
            else
            {
                // Пустой слот — прячем текст
                if (actionHintText != null) actionHintText.gameObject.SetActive(false);
                uiAnimationCoroutine = StartCoroutine(AnimateInfoUI(false));
            }
        }

        private IEnumerator AnimateInfoUI(bool show)
        {
            if (infoCanvasGroup == null || infoRectTransform == null) yield break;

            float duration = 0.15f; // Скорость анимации
            float elapsed = 0f;
            
            float startAlpha = infoCanvasGroup.alpha;
            float targetAlpha = show ? 0.95f : 0f; // Ограничиваем opacity до 95%

            // Если показываем текст, опускаем его чуть ниже перед подъемом
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
                
                // Сглаживание EaseOut
                float smoothT = t * (2f - t); 

                infoCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, smoothT);
                if (show)
                {
                    infoRectTransform.anchoredPosition = Vector2.Lerp(startPos, targetPos, smoothT);
                }

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
            CmdSelectSlot(currentSelectedIndex);
            UpdateItemInfoUI(); // Обновляем текст при авто-экипировке
        }
    }
}