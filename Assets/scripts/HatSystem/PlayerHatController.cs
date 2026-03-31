using UnityEngine;
using Mirror;

namespace Kotenkoff
{
    public class PlayerHatController : NetworkBehaviour
    {
        [Header("References")]
        [Tooltip("База данных со всеми шапками")]
        [SerializeField] private HatDatabase hatDatabase;
        
        [Tooltip("Пустой объект на голове игрока, куда будет крепиться визуал")]
        [SerializeField] private Transform hatPlace;

        // Синхронизируем ID шапки. При изменении на сервере, у всех клиентов сработает hook.
        // -1 означает, что шапки нет.
        [SyncVar(hook = nameof(OnHatIdChanged))]
        public int currentHatId = -1;

        private GameObject currentLocalHatInstance;

        public override void OnStartClient()
        {
            base.OnStartClient();
            // Применяем шапку при спавне игрока (для тех, кто подключился позже)
            UpdateLocalHatVisual(currentHatId);
        }

        // --- SERVER LOGIC ---

        /// <summary>
        /// Вызывается клиентом для запроса смены шапки у сервера.
        /// </summary>
        [Command]
        public void CmdEquipHat(int hatId)
        {
            // Здесь можно добавить валидацию (например, куплена ли эта шапка у игрока)
            // Если всё ок, меняем SyncVar.
            currentHatId = hatId;
        }

        [Command]
        public void CmdRemoveHat()
        {
            currentHatId = -1;
        }

        // --- CLIENT LOGIC ---

        private void OnHatIdChanged(int oldId, int newId)
        {
            UpdateLocalHatVisual(newId);
        }

        private void UpdateLocalHatVisual(int hatId)
        {
            // 1. Удаляем старую шапку, если она была
            if (currentLocalHatInstance != null)
            {
                Destroy(currentLocalHatInstance);
            }

            // 2. Если шапка снята (-1), выходим
            if (hatId == -1 || hatDatabase == null || hatPlace == null) return;

            // 3. Находим префаб в базе и спавним локально
            GameObject prefab = hatDatabase.GetHatPrefab(hatId);
            if (prefab != null)
            {
                currentLocalHatInstance = Instantiate(prefab, hatPlace);
                
                
                currentLocalHatInstance.transform.localPosition = Vector3.zero;
                currentLocalHatInstance.transform.localRotation = Quaternion.identity;
            }
        }
    }
}