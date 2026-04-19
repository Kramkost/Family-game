using Mirror;
using UnityEngine;

namespace MyAssets.scripts.Kotenkoff.Vehicle
{
    public class VehicleBase : NetworkBehaviour
    {
        [SerializeField, Tooltip("Текущий водитель транспорта."), SyncVar] private NetworkIdentity driver;
        /// <summary>Текущий водитель транспорта.</summary>
        public NetworkIdentity Driver
        {
            get => driver;
            set => driver = value;
        }

        [Header("Компоненты:")]
        [SerializeField, Tooltip("Ссылка на компонент 'VehicleMovement'.")] private VehicleMovement vehicleMovement;
        /// <summary>Компонент '<b>VehicleMovement</b>'.</summary>
        public VehicleMovement VehicleMovement => vehicleMovement;

        #region Unity Methods

        private void Start()
        {
            if (vehicleMovement == null) vehicleMovement = GetComponent<VehicleMovement>();
        }

        #endregion
    }
}