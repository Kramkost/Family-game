using UnityEngine;
using Mirror;
using System.Collections;
using System.Collections.Generic;

namespace Kotenkoff
{
    /// <summary>
    /// Серверный менеджер случайных ивентов. 
    /// С определенным интервалом выбирает случайный ивент на основе весов (редкости) 
    /// и спавнит его префаб.
    /// </summary>
    public class RandomEventManager : NetworkBehaviour
    {
        [System.Serializable]
        public struct EventConfig
        {
            [Tooltip("Название ивента (для логов и удобства)")]
            public string eventName;
            
            [Tooltip("Префаб ивента. Должен иметь NetworkIdentity, если влияет на геймплей всех игроков.")]
            public GameObject eventPrefab;
            
            [Tooltip("Вес вероятности (чем больше число, тем чаще падает)")]
            public int spawnWeight;
            
            [Tooltip("Длительность ивента в секундах. По истечении префаб будет уничтожен.")]
            public float duration;
        }

        [Header("Настройки спавна")]
        [Tooltip("Минимальное время между ивентами (в секундах)")]
        [SerializeField] private float minInterval = 60f;
        
        [Tooltip("Максимальное время между ивентами (в секундах)")]
        [SerializeField] private float maxInterval = 180f;

        [Tooltip("Точка, вокруг которой будут спавниться локальные ивенты (например, машина игроков)")]
        [SerializeField] private Transform referencePoint;

        [Header("Список Ивентов")]
        [SerializeField] private EventConfig[] events;

        private int totalWeight = 0;
        private GameObject currentActiveEvent;

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
                 
                    yield return new WaitForSeconds(10f); 
                }

                TriggerRandomEvent();
            }
        }

        [Server]
        private void TriggerRandomEvent()
        {
            EventConfig selectedEvent = GetWeightedRandomEvent();

            if (selectedEvent.eventPrefab == null)
            {
                Debug.LogWarning($"[RandomEventManager] Префаб для ивента {selectedEvent.eventName} не назначен!");
                return;
            }

            Debug.Log($"[RandomEventManager] Запуск ивента: {selectedEvent.eventName}");


            Vector3 spawnPos = referencePoint != null ? referencePoint.position : Vector3.zero;
            
            currentActiveEvent = Instantiate(selectedEvent.eventPrefab, spawnPos, Quaternion.identity);

            
            if (currentActiveEvent.TryGetComponent(out NetworkIdentity netId))
            {
                NetworkServer.Spawn(currentActiveEvent);
            }

            
            StartCoroutine(CleanupEvent(currentActiveEvent, selectedEvent.duration));
        }

        [Server]
        private EventConfig GetWeightedRandomEvent()
        {
            int randomWeight = Random.Range(0, totalWeight);
            int currentWeight = 0;

            foreach (var evt in events)
            {
                currentWeight += evt.spawnWeight;
                if (randomWeight < currentWeight)
                {
                    return evt;
                }
            }

            return events[0]; 
        }

        [Server]
        private IEnumerator CleanupEvent(GameObject eventInstance, float duration)
        {
            yield return new WaitForSeconds(duration);

            if (eventInstance != null)
            {
               
                if (eventInstance.TryGetComponent(out NetworkIdentity netId))
                {
                    NetworkServer.Destroy(eventInstance);
                }
                else
                {
                    Destroy(eventInstance);
                }
                
                Debug.Log("[RandomEventManager] Ивент завершен и очищен.");
            }
        }
    }
}