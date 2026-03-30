using System.Collections;
using Mirror;
using UnityEngine;

namespace Kotenkoff
{
    public sealed class TractorManager : NetworkBehaviour
    {
        [Header("Настройки спавна")]
        [Tooltip("Префаб физического тягача (с NetworkIdentity)")]
        [SerializeField] private GameObject tractorPrefab;
        [Tooltip("Дистанция, на которой физически появляется тягач (в метрах)")]
        [SerializeField] private float spawnDistance = 150f;
        
        [Header("Виртуальная симуляция")]
        [Tooltip("Текущее расстояние до тягача")]
        [SyncVar] public float distanceToTractor = 1000f;
        [Tooltip("Сколько метров тягач проезжает за 1 секунду, пока его не видно")]
        [SerializeField] private float virtualSpeed = 5f;

        [SyncVar] private bool isMoving;
        private bool hasSpawned = false;
        private Coroutine moveCoroutine;

    
        [SerializeField] private Transform carTransform; 

        private void OnEnable()
        {
            CarResourceManager.OnTractorIsMoving += HandleTractorEvent;
        }

        private void OnDisable()
        {
            CarResourceManager.OnTractorIsMoving -= HandleTractorEvent;
        }

        private void HandleTractorEvent(bool moving)
        {
            if (!NetworkServer.active) return; 
            SetTractorMoving(moving);
        }

        [Server]
        private void SetTractorMoving(bool moving)
        {
            isMoving = moving;

            if (isMoving && moveCoroutine == null && !hasSpawned)
            {
                moveCoroutine = StartCoroutine(MovingRoutine());
            }
            else if (!isMoving && moveCoroutine != null)
            {
                StopCoroutine(moveCoroutine);
                moveCoroutine = null;
            }
        }

        [Server]
        private IEnumerator MovingRoutine()
        {
            while (isMoving && !hasSpawned)
            {
                distanceToTractor -= virtualSpeed * Time.deltaTime;

               
                if (distanceToTractor <= spawnDistance)
                {
                    SpawnPhysicalTractor();
                    yield break; 
                }
                
                yield return null; 
            }
        }

        [Server]
        private void SpawnPhysicalTractor()
        {
            if (tractorPrefab == null || carTransform == null) return;

            hasSpawned = true;

            
            Vector3 spawnPos = carTransform.position - (carTransform.forward * spawnDistance);
            
            GameObject tractorInstance = Instantiate(tractorPrefab, spawnPos, carTransform.rotation);
            
            
            NetworkServer.Spawn(tractorInstance);
        }
    }
}