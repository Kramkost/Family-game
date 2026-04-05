using System.Collections;
using Mirror;
using UnityEngine;

namespace Game_Multiplayer_System
{
    /// <summary>
    /// Серверный менеджер случайных ивентов.
    /// Управляет спавном, звуковыми оповещениями по сети и трекингом позиции.
    /// </summary>
    public class RandomEventManager : NetworkBehaviour
    {
        [System.Serializable]
        public struct EventConfig
        {
            [Tooltip("Название ивента (покажем игрокам в UI)")]
            public string eventName;
            
            [Tooltip("Префаб ивента (NetworkIdentity обязателен)")]
            public GameObject eventPrefab;
            
            [Tooltip("Вес вероятности (чем больше, тем чаще)")]
            public int spawnWeight;
            
            [Tooltip("Длительность ивента в секундах")]
            public float duration;

            [Header("Positioning")]
            [Tooltip("Смещение от Reference Point (например, Y = 20 для дождя)")]
            public Vector3 spawnOffset;
            [Tooltip("Должен ли ивент лететь за Reference Point? (для дождя над едущей машиной)")]
            public bool followReference;

            [Header("Game Feel: Audio")]
            [Tooltip("Звуки ПРИ СТАРТЕ ивента (сирена, раскат грома)")]
            public AudioClip[] startAlertSounds;
            [Tooltip("Звуки ПРИ ОКОНЧАНИИ ивента (затихание, вздох облегчения)")]
            public AudioClip[] endAlertSounds;
        }

        [Header("Настройки спавна")]
        [SerializeField] private float minInterval = 60f;
        [SerializeField] private float maxInterval = 180f;
        [Tooltip("Обычно это трансформ вашей машины (RV)")]
        [SerializeField] private Transform referencePoint;

        [Header("Глобальное Аудио (Client)")]
        [Tooltip("AudioSource для воспроизведения сирен/грома (не 3D, а 2D звук на весь мир)")]
        [SerializeField] private AudioSource globalAudioSource;

        [Header("Список Ивентов")]
        [SerializeField] private EventConfig[] events;

        private int totalWeight = 0;
        private GameObject currentActiveEvent;
        private Coroutine trackingCoroutine;

        public override void OnStartServer()
        {
            foreach (var evt in events)
            {
                totalWeight += evt.spawnWeight;
            }

            if (totalWeight > 0 && events.Length > 0)
            {
                StartCoroutine(EventLoop());
            }
            else
            {
                Debug.LogWarning("[RandomEventManager] Нет ивентов или общий вес равен нулю!");
            }
        }

        [Server]
        private IEnumerator EventLoop()
        {
            while (true)
            {
                float waitTime = Random.Range(minInterval, maxInterval);
                yield return new WaitForSeconds(waitTime);

                if (currentActiveEvent != null)
                {
                    yield return new WaitUntil(() => currentActiveEvent == null);
                    yield return new WaitForSeconds(10f); // Небольшая передышка между ивентами
                }

                TriggerRandomEvent();
            }
        }

        [Server]
        private void TriggerRandomEvent()
        {
            int eventIndex = GetWeightedRandomEventIndex();
            EventConfig selectedEvent = events[eventIndex];

            if (selectedEvent.eventPrefab == null) return;

            Vector3 basePos = referencePoint != null ? referencePoint.position : Vector3.zero;
            Vector3 spawnPos = basePos + selectedEvent.spawnOffset;
            
            // ФИКС ВРАЩЕНИЯ: Используем вращение самого префаба, а не Quaternion.identity
            currentActiveEvent = Instantiate(selectedEvent.eventPrefab, spawnPos, selectedEvent.eventPrefab.transform.rotation);

            if (currentActiveEvent.TryGetComponent(out NetworkIdentity netId))
            {
                NetworkServer.Spawn(currentActiveEvent);
            }

            // Запускаем трекинг, если нужно (чтобы туча летела за машиной)
            if (selectedEvent.followReference && referencePoint != null)
            {
                if (trackingCoroutine != null) StopCoroutine(trackingCoroutine);
                trackingCoroutine = StartCoroutine(TrackReferencePoint(currentActiveEvent.transform, selectedEvent.spawnOffset));
            }

            // Оповещаем всех клиентов о начале
            RpcAnnounceEventStart(eventIndex);

            StartCoroutine(CleanupEvent(currentActiveEvent, selectedEvent.duration, eventIndex));
        }

        [Server]
        private IEnumerator TrackReferencePoint(Transform eventTransform, Vector3 offset)
        {
            // Обновляем позицию ивента каждый кадр, пока он жив.
            // При этом вращение остается неизменным (дождь всегда падает ровно вниз).
            while (eventTransform != null && referencePoint != null)
            {
                eventTransform.position = referencePoint.position + offset;
                yield return null;
            }
        }

        [Server]
        private int GetWeightedRandomEventIndex()
        {
            int randomWeight = Random.Range(0, totalWeight);
            int currentWeight = 0;

            for (int i = 0; i < events.Length; i++)
            {
                currentWeight += events[i].spawnWeight;
                if (randomWeight < currentWeight) return i;
            }
            return 0; 
        }

        [Server]
        private IEnumerator CleanupEvent(GameObject eventInstance, float duration, int eventIndex)
        {
            yield return new WaitForSeconds(duration);

            if (eventInstance != null)
            {
                if (eventInstance.TryGetComponent(out NetworkIdentity netId))
                    NetworkServer.Destroy(eventInstance);
                else
                    Destroy(eventInstance);

                // Оповещаем клиентов о завершении
                RpcAnnounceEventEnd(eventIndex);
            }
        }

        // ===================================================================================
        // CLIENT LOGIC (Visuals & Audio)
        // ===================================================================================

        [ClientRpc]
        private void RpcAnnounceEventStart(int eventIndex)
        {
            if (eventIndex < 0 || eventIndex >= events.Length) return;
            var evt = events[eventIndex];

            // 1. Проигрываем случайный звук начала (сирена, раскат грома)
            PlayRandomGlobalSound(evt.startAlertSounds);

            // 2. Здесь можно добавить вызов UI менеджера:
            // UIManager.Instance.ShowWarningText($"Внимание: {evt.eventName}!");
            Debug.Log($"[Client] Начался ивент: {evt.eventName}");
        }

        [ClientRpc]
        private void RpcAnnounceEventEnd(int eventIndex)
        {
            if (eventIndex < 0 || eventIndex >= events.Length) return;
            var evt = events[eventIndex];

            // 1. Проигрываем звук окончания (если есть)
            PlayRandomGlobalSound(evt.endAlertSounds);

            // UIManager.Instance.ShowWarningText($"{evt.eventName} завершился.");
        }

        [Client]
        private void PlayRandomGlobalSound(AudioClip[] clips)
        {
            if (clips == null || clips.Length == 0 || globalAudioSource == null) return;
            
            globalAudioSource.pitch = Random.Range(0.95f, 1.05f); // Легкий рандом для разнообразия
            globalAudioSource.PlayOneShot(clips[Random.Range(0, clips.Length)]);
        }

        // Отрисовка смещений в редакторе для удобства настройки
        private void OnDrawGizmosSelected()
        {
            if (referencePoint == null || events == null) return;

            Gizmos.color = new Color(0, 1, 1, 0.5f); // Голубой
            foreach (var evt in events)
            {
                Vector3 previewPos = referencePoint.position + evt.spawnOffset;
                Gizmos.DrawWireSphere(previewPos, 2f);
                Gizmos.DrawLine(referencePoint.position, previewPos);
            }
        }
    }
}