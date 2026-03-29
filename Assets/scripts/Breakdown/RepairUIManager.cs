using UnityEngine;

namespace Breakdown
{
    public class RepairUIManager : MonoBehaviour
    {
        [Header("UI Panel")]
        [SerializeField] private GameObject repairPanel; 
        
        [Header("Minigame Logic")]
        [Tooltip("Сколько квадратиков нужно вставить для победы?")]
        [SerializeField] private int squaresNeededToWin = 3; 
        
        private int currentMatchedSquares = 0;

        private CarPart currentPartBeingFixed;
        private PlayerEntity localPlayer; 

        public void OpenMiniGame(CarPart part, PlayerEntity player)
        {
            currentPartBeingFixed = part;
            localPlayer = player;
            
            repairPanel.SetActive(true);
            ResetPuzzle();
            
       
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void CloseMiniGame()
        {
            repairPanel.SetActive(false);
            currentPartBeingFixed = null;
            
          
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

      
        public void ReportSquareMatched()
        {
            currentMatchedSquares++;
            
          
            if (currentMatchedSquares >= squaresNeededToWin)
            {
                OnMiniGameWon();
            }
        }

        public void OnMiniGameWon()
        {
            Debug.Log("Мини-игра пройдена!");
            
            if (currentPartBeingFixed != null && localPlayer != null)
            {
                
                localPlayer.CmdFixPart(currentPartBeingFixed.gameObject);
            }
            
            CloseMiniGame();
        }

        private void ResetPuzzle()
        {
            currentMatchedSquares = 0; 
            
            // TODO: Добавить возврат на старые места квадратики
        }
    }
}