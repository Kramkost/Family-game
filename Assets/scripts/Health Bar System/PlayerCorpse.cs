using UnityEngine;
using Mirror;

namespace Kotenkoff
{
    [RequireComponent(typeof(Rigidbody))]
    public class PlayerCorpse : NetworkBehaviour
    {
        [Tooltip("Ссылка на оригинального игрока (синхронизируется по сети)")]
        [SyncVar] public GameObject ownerPlayer;

        [Server]
        public void ApplyDefibrillator()
        {
            if (ownerPlayer != null && ownerPlayer.TryGetComponent(out PlayerDeathManager deathManager))
            {
                Debug.Log("Воскрешаем игрока!");
               
                deathManager.ServerReviveFromCorpse(transform.position);
                
               
                NetworkServer.Destroy(gameObject);
            }
        }
    }
}