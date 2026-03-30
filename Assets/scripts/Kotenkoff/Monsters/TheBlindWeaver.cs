using System.Collections;
using UnityEngine;
using Mirror;
using UnityEngine.AI;

namespace Kotenkoff.Monsters
{
    public sealed class TheBlindWeaver : Monster
    {
        [SerializeField] private float maxHealth;
        public override float MaxHealth => maxHealth;

        [SerializeField] private float health;
        public override float Health =>  health;


        private GameObject triggerObject;

        [SerializeField] private Transform target;
        public Transform Target => target;
    
        
        private NavMeshAgent agent;
        [SerializeField, ReadOnly] private float distance;
        
        
        
        [SerializeField] private bool isAggressive;
        public bool IsAggressive => isAggressive;

        [SerializeField] private float aggressiveTime;
        [SerializeField] private float aggressiveDistance;
        
        [SerializeField, ReadOnly] private bool isCoroutine;

        public override void OnStartServer()
        {
            agent = GetComponent<NavMeshAgent>();
            
            isAggressive = false;
        }

        void Update()
        {
            if (Target != null) distance = Vector3.Distance(transform.position, target.position);

            if (distance > aggressiveDistance && IsAggressive && !isCoroutine)
            {
                StartCoroutine(nameof(BreakAggressive));
                isCoroutine = true;
            }

            if (distance < aggressiveDistance && IsAggressive && isCoroutine)
            {
                StopCoroutine(nameof(BreakAggressive));
                isCoroutine = false;
            }
            
            if (IsAggressive && Target != null)
            {
                agent.SetDestination(target.position);
            }
        }
        
        /// <summary>
        ///   <para>Меняет текущую цель монстра.</para>
        /// </summary>
        /// <param name="newTarget">Новая цель монстра</param>
        [Server]
        public void ChangeTarget(Transform newTarget)
        {
            target = newTarget;
        }
        
        /// <summary>
        ///   <para>Изменяет состояние агрессии монстра.</para>
        /// </summary>
        /// <param name="newIsAggressive">Новое значение агрессии</param>
        [Server]
        public void ChangeIsAggressive(bool newIsAggressive)
        {
            isAggressive = newIsAggressive;
        }

        private IEnumerator BreakAggressive()
        {
            yield return new WaitForSeconds(aggressiveTime);
            
            isCoroutine = false;
            
            ChangeIsAggressive(false);
            ChangeTarget(null);
        }

        /// <summary>
        ///   <para>Изменяет здоровье монстра.</para>
        /// </summary>
        /// <param name="value">Значение, которое прибавится к текущему значению здоровья (вводите отрицательное число, чтобы уменьшить значение здоровья)</param>
        [Server]
        public void ChangeHealth(float value)
        {
            health = Mathf.Clamp(health + value, 0, maxHealth);
        }
    }
}
