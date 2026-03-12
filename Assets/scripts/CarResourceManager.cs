using UnityEngine;
using Mirror;

/// <summary>
/// Серверный менеджер состояния автомобиля. Контролирует расход жидкостей.
/// </summary>
public class CarResourceManager : NetworkBehaviour
{
    [Header("Car State")]
    [SyncVar] public bool isDriving;

    [Header("Resources (Max 100)")]
    [SyncVar] public float gasoline = 100f;
    [SyncVar] public float water = 100f;
    [SyncVar] public float engineOil = 100f;

    [Header("Consumption Rates (Per Second)")]
    [SerializeField] private float gasConsumeRate = 1f;
    [SerializeField] private float waterConsumeRate = 0.2f;
    [SerializeField] private float oilConsumeRate = 0.1f;

    [ServerCallback]
    private void Update()
    {
        // Тратим ресурсы только если машина заведена/едет
        if (isDriving)
        {
            gasoline = Mathf.Clamp(gasoline - gasConsumeRate * Time.deltaTime, 0, 100);
            water = Mathf.Clamp(water - waterConsumeRate * Time.deltaTime, 0, 100);
            engineOil = Mathf.Clamp(engineOil - oilConsumeRate * Time.deltaTime, 0, 100);

            // Если бензин закончился — глохнем
            if (gasoline <= 0)
            {
                isDriving = false;
                // Тут можно добавить вызов RPC для звука заглохшего мотора
            }
        }
    }

    /// <summary>
    /// Метод для заправки машины. Вызывается сервером.
    /// </summary>
    [Server]
    public void Refuel(float amount)
    {
        gasoline = Mathf.Clamp(gasoline + amount, 0, 100);
    }
}