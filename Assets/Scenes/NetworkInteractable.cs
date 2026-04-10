using Kotenkoff;
using Mirror;
using UnityEngine;

// Убедись, что IInteractable находится здесь

namespace Scenes
{
    [RequireComponent(typeof(Collider))]
    public class NetworkSceneChanger : NetworkBehaviour, IInteractable // <-- Добавили IInteractable
    {
        [Header("Настройки")]
        [Tooltip("Имя сцены для загрузки (должна быть в Build Settings)")]
        public string targetScene = "NextLevel";

        [Tooltip("Текст, который покажем при наведении")]
        public string promptMessage = "Нажмите [E], чтобы переместить всех";

        // Метод из интерфейса IInteractable
        public void ServerInteract(PlayerEntity player, PlayerInventory inventory)
        {
            if (!isServer) return;

            Debug.Log($"[SceneChanger] Игрок {player.gameObject.name} запустил переход на сцену: {targetScene}");
            NetworkManager.singleton.ServerChangeScene(targetScene);
        }
    }
}