using System.Collections;
using UnityEngine;
using Mirror;
using UnityEngine.AI;
using Health_Bar_System;

namespace Kotenkoff.Monsters
{
    [RequireComponent(typeof(NavMeshAgent), typeof(Animator), typeof(NetworkAnimator))]
    public sealed class TheBlindWeaver : Monster
    {
        [Header("Статистика")]
        [SerializeField] private float maxHealth = 100f;
        public override float MaxHealth => maxHealth;

        [SyncVar] 
        [SerializeField] private float health;
        public override float Health => health;

        [Header("Наведение и Агрессия")]
        [SerializeField] private Transform target;
        public Transform Target => target;
        
        [SerializeField, ReadOnly] private float distance;

        [SyncVar]
        [SerializeField] private bool isAggressive;
        public bool IsAggressive => isAggressive;

        [SerializeField] private float aggressiveTime = 5f;
        [SerializeField] private float aggressiveDistance = 15f;
        
        private bool isBreakingAggressive; 

        [Header("Бой")]
        [SerializeField] private float attackRange = 2f;    
        [SerializeField] private float damage = 15f;         
        [SerializeField] private float attackCooldown = 1.5f;
        private float lastAttackTime;                       

        private NavMeshAgent agent;
        private Animator animator;
        private NetworkAnimator networkAnimator; // ВАЖНО: Нужен для синхронизации триггеров

        private const string IsWalkingParameter = "IsWalking";
        private const string AttackTrigger = "Attack";
        private const string FoundEnemyTrigger = "FoundEnemy";

        public override void OnStartServer()
        {
            agent = GetComponent<NavMeshAgent>();
            animator = GetComponent<Animator>();
            networkAnimator = GetComponent<NetworkAnimator>();
            
            health = maxHealth;
            isAggressive = false;
        }

        [ServerCallback] 
        void Update()
        {
            // Защита: если игрок вышел с сервера, сбрасываем таргет
            if (target == null && isAggressive)
            {
                ChangeTarget(null);
                ChangeIsAggressive(false);
            }

            if (target != null)
            {
                distance = Vector3.Distance(transform.position, target.position);
            }

            HandleAggressionLoss();

            if (isAggressive && target != null)
            {
                if (distance <= attackRange)
                {
                    agent.isStopped = true;
                    animator.SetBool(IsWalkingParameter, false);

                    if (Time.time >= lastAttackTime + attackCooldown)
                    {
                        PerformAttack();
                    }
                }
                else
                {
                    agent.isStopped = false;
                    agent.SetDestination(target.position);
                    animator.SetBool(IsWalkingParameter, true);
                }
            }
            else
            {
                // Убеждаемся, что монстр точно стоит, если нет агрессии
                if (agent.isOnNavMesh) agent.isStopped = true; 
                animator.SetBool(IsWalkingParameter, false);
            }
        }

        [Server]
        private void HandleAggressionLoss()
        {
            if (!isAggressive || target == null) return;

            if (distance > aggressiveDistance && !isBreakingAggressive)
            {
                StartCoroutine(nameof(BreakAggressiveCoroutine));
                isBreakingAggressive = true;
            }
            else if (distance <= aggressiveDistance && isBreakingAggressive)
            {
                StopCoroutine(nameof(BreakAggressiveCoroutine));
                isBreakingAggressive = false;
            }
        }

        [Server]
        private void PerformAttack()
        {
            lastAttackTime = Time.time;
            
            // ВАЖНО: Используем NetworkAnimator, чтобы удар увидели все клиенты
            networkAnimator.SetTrigger(AttackTrigger);

            if (target.TryGetComponent(out IDamageable damageableTarget))
            {
                damageableTarget.TakeDamage(damage); 
            }
        }

        [Server]
        public void ChangeTarget(Transform newTarget)
        {
            if (target == null && newTarget != null)
            {
                // ВАЖНО: Синхронизируем рев/замечание врага через сеть
                networkAnimator.SetTrigger(FoundEnemyTrigger);
            }
            target = newTarget;
        }
        
        [Server]
        public void ChangeIsAggressive(bool newIsAggressive)
        {
            isAggressive = newIsAggressive;
        }

        private IEnumerator BreakAggressiveCoroutine() 
        {
            yield return new WaitForSeconds(aggressiveTime);
            isBreakingAggressive = false;
            ChangeIsAggressive(false);
            ChangeTarget(null);
        }

        [Server]
        public override void ChangeHealth(float amount)
        {
            health = Mathf.Clamp(health + amount, 0, maxHealth);
            if (health == 0)
            {
                // Логика смерти
                MonsterDeath();
            }
        }

        protected override void MonsterDeath()
        {
        }
    }
}