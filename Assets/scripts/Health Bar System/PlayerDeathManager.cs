using Kotenkoff;
using Mirror;
using UnityEngine;

namespace Health_Bar_System
{
    [RequireComponent(typeof(PlayerStats))]
    public class PlayerDeathManager : NetworkBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerStats stats;
        [SerializeField] private CharacterController characterController;
        [SerializeField] private PlayerEntity playerMovement;
        [SerializeField] private GameObject playerModel; 
        
        [Header("Spectator & Corpse")]
        [SerializeField] private GameObject corpsePrefab;
        [SerializeField] private SpectatorController spectatorCamera; 

        [Header("Prototype Settings")]
        [Tooltip("Если включено, при смерти появится кнопка быстрого возрождения")]
        [SerializeField] private bool isPrototype = false;
        [Tooltip("Ссылка на Canvas или Panel с кнопкой 'Перезагрузиться'")]
        [SerializeField] private GameObject prototypeDeathUI;

        private GameObject activeCorpse;

        public override void OnStartServer()
        {
            stats.ServerOnDeath += HandleDeathOnServer;
        }

        [Server]
        private void HandleDeathOnServer()
        {
            activeCorpse = Instantiate(corpsePrefab, transform.position, transform.rotation);
            
            if (activeCorpse.TryGetComponent(out PlayerCorpse corpseLogic))
            {
                corpseLogic.ownerPlayer = this.gameObject;
            }
            NetworkServer.Spawn(activeCorpse);

            RpcHandleDeathClient();
        }

        [ClientRpc]
        private void RpcHandleDeathClient()
        {
            if (characterController != null) characterController.enabled = false;
            if (playerMovement != null) playerMovement.enabled = false;
            if (playerModel != null) playerModel.SetActive(false);

            if (isLocalPlayer)
            {
                if (spectatorCamera != null) spectatorCamera.EnableSpectator();

                // --- ЛОГИКА ПРОТОТИПА ---
                if (isPrototype && prototypeDeathUI != null)
                {
                    prototypeDeathUI.SetActive(true);
                    
                    // Освобождаем курсор, чтобы можно было нажать на кнопку
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
            }
        }

        // ==========================================
        // МЕТОД ДЛЯ КНОПКИ В UI (Unity Event)
        // ==========================================
        public void UI_PrototypeRestart()
        {
            if (isLocalPlayer)
            {
                CmdPrototypeRespawn();
            }
        }

        [Command]
        private void CmdPrototypeRespawn()
        {
            // Удаляем труп с сервера
            if (activeCorpse != null)
            {
                NetworkServer.Destroy(activeCorpse);
            }

            // Возрождаем с полным ХП (1.0f = 100%)
            stats.Revive(1f); 
            
            // Чуть приподнимаем, чтобы не застрять в полу
            transform.position += Vector3.up * 1f; 

            TargetReviveClient();
        }

        /// <summary>
        /// Вызывается Дефибриллятором (через скрипт трупа)
        /// </summary>
        [Server]
        public void ServerReviveFromCorpse(Vector3 revivePosition)
        {
            stats.Revive(0.3f); 
            transform.position = revivePosition + Vector3.up * 0.5f; 
            
            TargetReviveClient();
        }

        [TargetRpc]
        private void TargetReviveClient()
        {
            if (isLocalPlayer)
            {
                if (spectatorCamera != null) spectatorCamera.DisableSpectator();
                
                // Прячем UI прототипа, если он был включен
                if (prototypeDeathUI != null) prototypeDeathUI.SetActive(false);

                // Возвращаем курсор в игровой режим
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            if (playerModel != null) playerModel.SetActive(true);
            if (characterController != null) characterController.enabled = true;
            if (playerMovement != null) playerMovement.enabled = true;
        }
    }
}