using UnityEngine;
using UnityEngine.UI; 
using Mirror;

namespace Kotenkoff
{
    public class PlayerHUD : NetworkBehaviour
    {
        [Header("UI References")]
        [Tooltip("Сам объект Canvas. Мы будем выключать его у чужих игроков.")]
        [SerializeField] private GameObject hudCanvas;
        
        [Tooltip("Та самая красная полоска с режимом Filled")]
        [SerializeField] private Image healthFill;
        
        [Header("Logic References")]
        [SerializeField] private PlayerStats stats;

        public override void OnStartClient()
        {
  
            if (!isLocalPlayer)
            {
                hudCanvas.SetActive(false);
            }
        }

        public override void OnStartLocalPlayer()
        {
       
            hudCanvas.SetActive(true);


            if (stats != null)
            {
                stats.ClientOnHealthChanged += UpdateHealthBar;
                
         
                UpdateHealthBar(stats.currentHealth, 150f); 
            }
        }

        /// <summary>
        /// Эта функция вызывается автоматически КАЖДЫЙ РАЗ, когда ХП меняется на сервере
        /// </summary>
        private void UpdateHealthBar(float currentHealth, float maxHealth)
        {
            if (healthFill != null)
            {
        
                healthFill.fillAmount = currentHealth / maxHealth;
            }
        }

        
        private void OnDestroy()
        {
            if (stats != null)
            {
                stats.ClientOnHealthChanged -= UpdateHealthBar;
            }
        }
    }
}