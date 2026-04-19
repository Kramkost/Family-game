using Mirror;
using MyAssets.scripts.Kotenkoff.Character.Interfaces;
using MyAssets.scripts.Kotenkoff.Character.Inventory;
using UnityEngine;

namespace MyAssets.scripts.Kotenkoff.Vehicle
{
    public class VehicleSeat : NetworkBehaviour, IInteractableTest
    {
        [SerializeField, Tooltip("Водительское ли это сиденье?")] private bool isDriverSeat;
        /// <summary>Водительское ли это сиденье?</summary>
        public bool IsDriverSeat => isDriverSeat;
        
        [SerializeField, Tooltip("Пассажир сиденья."), SyncVar] private NetworkIdentity passenger;
        /// <summary>Текущий пассажир сиденья.</summary>
        public NetworkIdentity Passenger => passenger;
        
        [SerializeField, Tooltip("Место, куда телепортируется игрока когда сядет.")] private Transform seatPosition;

        public void TryInteract(CharacterInventory inventory)
        {
            passenger = inventory.netIdentity;
        }

        private void OnPassengerChanged(NetworkIdentity oldPas, NetworkIdentity newPas)
        {
            if (oldPas == newPas) return;
            
            newPas.transform.position = seatPosition.position;
        }

        #region Unity Methods

        private void Start() => seatPosition = seatPosition == null ? transform : seatPosition;

        #endregion
    }
}