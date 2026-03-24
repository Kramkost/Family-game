using UnityEngine;
using Mirror;

namespace Kotenkoff
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

           
            if (isLocalPlayer && spectatorCamera != null)
            {
                spectatorCamera.EnableSpectator();
            }
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
           
            if (isLocalPlayer && spectatorCamera != null)
            {
                spectatorCamera.DisableSpectator();
            }

            if (playerModel != null) playerModel.SetActive(true);
            if (characterController != null) characterController.enabled = true;
            if (playerMovement != null) playerMovement.enabled = true;
        }
    }
}