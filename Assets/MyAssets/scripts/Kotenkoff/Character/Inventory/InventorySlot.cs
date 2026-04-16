using System;
using UnityEngine;

namespace MyAssets.scripts.Kotenkoff.Character.Inventory
{
    /// <summary>
    /// Класс, представляющий слот инвентаря. Хранит данные о содержимом слота без сетевой логики.
    /// </summary>
    [Serializable]
    public class InventorySlot
    {
        [SerializeField]
        private GameObject objectInSlot;
        /// <summary>
        /// Объект, находящийся в слоте.
        /// </summary>
        public GameObject ObjectInSlot
        {
            get => objectInSlot;
            set
            {
                objectInSlot = value;
                isOccupied = value != null; // Автоматически обновляем флаг занятости
            }
        }

        [SerializeField]
        private bool isOccupied;
        /// <summary>
        /// Флаг занятости слота (только для чтения).
        /// </summary>
        public bool IsOccupied => isOccupied;

        /// <summary>
        /// Попытка добавления указанного объекта в слот.
        /// </summary>
        /// <param name="go">Объект, который мы хотим добавить.</param>
        public void TryAddToSlot(GameObject go)
        {
            if (isOccupied)
            {
                Debug.LogWarning($"[InventorySlot] Предупреждение! Невозможно добавить предмет в слот, т.к. он занят ({objectInSlot}).");
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
    }
}