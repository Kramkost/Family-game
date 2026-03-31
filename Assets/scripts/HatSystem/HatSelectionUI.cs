using UnityEngine;
using Mirror;

namespace Kotenkoff
{
    public class HatSelectionUI : MonoBehaviour
    {
        /// <summary>
        /// Метод для UI кнопок. Передай в инспекторе ID нужной шапки.
        /// </summary>
        public void OnEquipHatButtonClicked(int hatId)
        {
            NetworkIdentity localPlayer = NetworkClient.localPlayer;
            if (localPlayer == null) return;

         
            if (localPlayer.TryGetComponent(out PlayerHatController hatController))
            {
               
                hatController.CmdEquipHat(hatId);
            }
            else
            {
                Debug.LogWarning("PlayerHatController не найден на локальном игроке.");
            }
        }

        public void OnRemoveHatButtonClicked()
        {
            NetworkIdentity localPlayer = NetworkClient.localPlayer;
            if (localPlayer != null && localPlayer.TryGetComponent(out PlayerHatController hatController))
            {
                hatController.CmdRemoveHat();
            }
        }
    }
}