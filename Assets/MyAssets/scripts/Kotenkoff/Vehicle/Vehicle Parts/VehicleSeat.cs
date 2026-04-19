using Mirror;
using MyAssets.scripts.Kotenkoff.Character;
using MyAssets.scripts.Kotenkoff.Character.Interfaces;
using MyAssets.scripts.Kotenkoff.Character.Inventory;
using UnityEngine;

namespace MyAssets.scripts.Kotenkoff.Vehicle.Vehicle_Parts
{
    public class VehicleSeat : NetworkBehaviour, IInteractableTest
    {
        [SerializeField, Tooltip("Водительское ли это сиденье?")] private bool isDriverSeat;
        /// <summary>Водительское ли это сиденье?</summary>
        public bool IsDriverSeat => isDriverSeat;
        
        [SerializeField, Tooltip("Пассажир сиденья."), SyncVar(hook = nameof(OnPassengerChanged))] private NetworkIdentity passenger;
        /// <summary>Текущий пассажир сиденья.</summary>
        public NetworkIdentity Passenger => passenger;
        
        [SerializeField, Tooltip("Место, куда телепортируется игрок когда сядет.")] private Transform seatPosition;
        [SerializeField, Tooltip("Место, куда телепортируется игрок когда выйдет.")] private Transform exitPosition;
        public Transform ExitPosition => exitPosition;
        
        [Header("Компоненты:")]
        [SerializeField] private VehicleBase vehicleBase;
        /// <summary>Компонент '<b>VehicleBase</b>'.</summary>
        public VehicleBase VehicleBase => vehicleBase;

        public void TryInteract(CharacterInventory inventory)
        {
            passenger = passenger == inventory.netIdentity ? null : inventory.netIdentity;
        }

        private void OnPassengerChanged(NetworkIdentity oldPas, NetworkIdentity newPas)
        {
            if (oldPas == newPas) return;
            if (newPas == null) return;
            
            if (newPas.TryGetComponent(out CharacterBase player)) player.EnterSeat(seatPosition, this);
            if (isDriverSeat) vehicleBase.Driver = newPas;
        }

        #region Unity Methods

        private void Start()
        {
            seatPosition = seatPosition == null ? transform : seatPosition;
            
            if (vehicleBase == null) Debug.LogError("Ошибка: на сиденье не указана ссылка на компонент 'VehicleBase'.");
        }

        #endregion
    }
}