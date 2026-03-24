using UnityEngine;
using Mirror;

namespace Kotenkoff
{
    public class CarPart : NetworkBehaviour
    {
        [Header("Визуал")]
        [SerializeField] private GameObject workingModel;
        [SerializeField] private GameObject brokenModel;
        
        [Header("Стэйт")]
        [SyncVar(hook = nameof(OnPartStateChanged))] 
        public bool isBroken = false;

        private void Start()
        {
            UpdateVisuals(isBroken);
        }

        // Вызывается Менеджером Поломок на сервере
        [Server]
        public void BreakPart()
        {
            if (isBroken) return;
            isBroken = true;
            Debug.Log($"[CarPart] Деталь {gameObject.name} сломалась!");
            
            // Тут можно отправить сигнал машине, чтобы она начала дымиться или заглохла
        }

    
        [Server]
        public void RepairPart()
        {
            if (!isBroken) return;
            isBroken = false;
            Debug.Log($"[CarPart] Деталь {gameObject.name} починена!");
        }

        
        private void OnPartStateChanged(bool oldState, bool newState)
        {
            UpdateVisuals(newState);
        }

        private void UpdateVisuals(bool broken)
        {
            if (workingModel != null) workingModel.SetActive(!broken);
            if (brokenModel != null) brokenModel.SetActive(broken);
        }
    }
}