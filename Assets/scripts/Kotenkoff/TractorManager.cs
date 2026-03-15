using System.Collections;
using Mirror;
using UnityEngine;

namespace Kotenkoff
{
    public sealed class TractorManager : NetworkBehaviour
    {
        [Tooltip("В движении ли Тягач?"), SerializeField]
        private bool isMoving;
        
        [Space]
        
        
        [Tooltip("Расстояние до Тягача"), SerializeField]
        private float distanceToTractor = 5000;
        
        [Space]
        
        [Tooltip("Время между шагами (сек.)"), SerializeField]
        private float timeBetweenSteps = 1;
        [Tooltip("Расстояние, которое пройдёт Тягач за один шаг"), SerializeField]
        private float stepRange = 1f;
        
        
        
        private void OnEnable()
        {
            CarResourceManager.OnTractorIsMoving += TractorMoving;
        }

        private void OnDisable()
        {
            CarResourceManager.OnTractorIsMoving -= TractorMoving;
        }

        [Server]
        private void TractorMoving(bool moving)
        {
            isMoving = moving;

            if (isMoving)
            {
                StartCoroutine(Moving());
            }
            else
            {
                StopAllCoroutines();
            }
        }

        [Server]
        private IEnumerator Moving()
        {
            while (isMoving)
            {
                distanceToTractor -= stepRange;
                
                yield return new WaitForSeconds(timeBetweenSteps);
            }
        }

        [Server]
        public float GetDistanceToTractor()
        {
            return distanceToTractor;
        }
    }
}
