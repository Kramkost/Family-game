using System;
using Mirror;
using UnityEngine;

namespace MyAssets.scripts.Kotenkoff.Character.Inventory
{
    [Serializable]
    public class InventorySlot
    {
        [SerializeField]
        private GameObject objectInSlot;
        public GameObject ObjectInSlot => objectInSlot;

        [SerializeField]
        private bool isOccupied;
        public bool IsOccupied => isOccupied;
        
        
        /// <summary>
        /// Попытка добавления указанного объекта в слот.
        /// </summary>
        /// <param name="go">Объект, который мы хотим добавить.</param>
        public void TryAddToSlot(GameObject go)
        {
            if (isOccupied)
            {
                Debug.LogWarning($"[InventorySlot] Предупреждение! не возможно добавить предмет в слот, т.к. он занят ({objectInSlot}).");
            }
            else
            {
                objectInSlot = go;
                isOccupied = true;
            }
        }

        /// <summary>
        /// Попытка очистки слота.
        /// </summary>
        public void TryRemoveFromSlot()
        {
            if (isOccupied)
            {
                objectInSlot = null;
                isOccupied = false;
            }
            else
            {
                Debug.LogError($"[InventorySlot] Ошибка! Невозможно очистить слот, т.к. он пустой.");
            }
        }

        /// <summary>
        /// Просто включает объект слота, чтобы все видели.
        /// </summary>
        [Command]
        public void CmdShowObject()
        {
            if (objectInSlot != null)
            {
                objectInSlot.SetActive(true);
                RpcOnOffObject(objectInSlot, true);
            }
        }
        
        /// <summary>
        /// Просто отключает объект слота, чтобы его не видели. 
        /// </summary>
        [Command]
        public void CmdHideObject()
        {
            if (objectInSlot != null)
            {
                objectInSlot.SetActive(false);
                RpcOnOffObject(objectInSlot, false);
            }
        }
        
        [ClientRpc]
        private void RpcOnOffObject(GameObject go,  bool state)
        {
            go.SetActive(state);
        }
    }
}