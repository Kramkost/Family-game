using UnityEngine;
using Mirror;
using System;

namespace Kotenkoff
{
    public class PlayerStats : NetworkBehaviour, IDamageable
    {
        [Header("Health Settings")]
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private float maxOverheal = 150f; 
        [Header("Hunger Settings (Hidden)")]
        [SerializeField] private float maxHunger = 100f;
        [SerializeField] private float hungerDepletionRate = 2f; 
        [SerializeField] private float starvationDamageRate = 5f; 
        [Header("SyncVars (Read Only)")]
        [SyncVar(hook = nameof(OnHealthChanged))] public float currentHealth;
        [SyncVar] public float currentHunger;
        [SyncVar] public bool isDead = false;

      
        public event Action<float, float> ClientOnHealthChanged; 
        public event Action ServerOnDeath;

        public override void OnStartServer()
        {
            currentHealth = maxHealth;
            currentHunger = maxHunger;
        }

        private void Update()
        {
            if (!isServer || isDead) return;

            HandleHunger();
        }

        [Server]
        private void HandleHunger()
        {
            if (currentHunger > 0)
            {
                currentHunger -= hungerDepletionRate * Time.deltaTime;
            }
            else
            {
     
                TakeDamage(starvationDamageRate * Time.deltaTime);
            }
        }

        [Server]
        public void TakeDamage(float amount)
        {
            if (isDead) return;

            currentHealth -= amount;
            if (currentHealth <= 0)
            {
                currentHealth = 0;
                Die();
            }
        }

        [Server]
        public void Heal(float amount)
        {
            if (isDead) return;
            currentHealth = Mathf.Min(currentHealth + amount, maxOverheal);
        }

        [Server]
        public void Eat(float foodValue)
        {
            if (isDead) return;

            currentHunger += foodValue;
            
            // Если голод переполнен, излишки идут в ХП (бонус за сытость)
            if (currentHunger > maxHunger)
            {
                float excess = currentHunger - maxHunger;
                currentHunger = maxHunger;
                Heal(excess * 0.5f); 
            }
        }

        [Server]
        private void Die()
        {
            isDead = true;
            ServerOnDeath?.Invoke();
        }

        [Server]
        public void Revive(float healthPercentage)
        {
            isDead = false;
            currentHealth = maxHealth * healthPercentage;
            currentHunger = maxHunger * 0.5f;
        }

       
        private void OnHealthChanged(float oldVal, float newVal)
        {
            ClientOnHealthChanged?.Invoke(newVal, maxOverheal);
        }
    }
}