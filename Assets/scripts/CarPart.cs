using UnityEngine;
using Mirror;

namespace Kotenkoff
{
    public class CarPart : NetworkBehaviour
    {
        [Header("Базовые настройки")]
        public CarPartType partType;
        
        [Tooltip("Визуал для стадий: 0-Целая, 1-Легкая, 2-Дым, 3-Заглохла")]
        public GameObject[] stageVisuals;


        [SyncVar(hook = nameof(OnStageChanged))]
        public int currentStage = 0; 

        public bool isBroken => currentStage > 0;

        private CarHybridSystem carSystem;

        private void Awake()
        {
  
            carSystem = GetComponentInParent<CarHybridSystem>();
        }

        public override void OnStartClient()
        {
            UpdateVisuals(currentStage);
        }

        [Server]
        public void BreakPart()
        {
           
            if (currentStage < stageVisuals.Length - 1)
            {
                currentStage++;
                ApplyEffectsToCar(currentStage);
            }
        }

        [Server]
        public void RepairPart()
        {
     
            if (currentStage > 0)
            {
                currentStage--;
                ApplyEffectsToCar(currentStage);
            }
        }

        [Server]
        private void ApplyEffectsToCar(int stage)
        {
       
            if (partType == CarPartType.Engine && carSystem != null)
            {
                carSystem.UpdateEngineState(stage);
            }
        }

        private void OnStageChanged(int oldStage, int newStage)
        {
            UpdateVisuals(newStage);
        }

        private void UpdateVisuals(int stage)
        {
            if (stageVisuals == null || stageVisuals.Length == 0) return;
            
            for (int i = 0; i < stageVisuals.Length; i++)
            {
                if (stageVisuals[i] != null)
                    stageVisuals[i].SetActive(i == stage);
            }
        }
    }
}