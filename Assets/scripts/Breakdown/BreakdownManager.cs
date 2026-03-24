using System.Collections;
using UnityEngine;
using Mirror;

namespace Kotenkoff
{
    public class BreakdownManager : NetworkBehaviour
    {
        [Tooltip("Список всех ломающихся деталей в машине")]
        [SerializeField] private CarPart[] allParts;
        
        [SerializeField] private float minTimeBetweenBreakdowns = 30f;
        [SerializeField] private float maxTimeBetweenBreakdowns = 120f;

        public override void OnStartServer()
        {
            if (allParts.Length > 0)
            {
                StartCoroutine(BreakdownLoop());
            }
        }

        [Server]
        private IEnumerator BreakdownLoop()
        {
            while (true)
            {
               
                float waitTime = Random.Range(minTimeBetweenBreakdowns, maxTimeBetweenBreakdowns);
                yield return new WaitForSeconds(waitTime);

                // Ищем все целые детали
                System.Collections.Generic.List<CarPart> workingParts = new System.Collections.Generic.List<CarPart>();
                foreach (var part in allParts)
                {
                    if (!part.isBroken) workingParts.Add(part);
                }

                if (workingParts.Count > 0)
                {
                    CarPart partToBreak = workingParts[Random.Range(0, workingParts.Count)];
                    partToBreak.BreakPart();
                }
            }
        }
    }
}