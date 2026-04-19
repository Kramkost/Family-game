using MyAssets.scripts.Kotenkoff.Character.Inventory;

namespace MyAssets.scripts.Kotenkoff.Character.Interfaces
{
    public interface IInteractableTest
    {
        /// <summary>
        /// Попытка взаимодействия с производным классом.
        /// </summary>
        /// <param name="inventory">Инвентарь игрока, который взаимодействует с классом.</param>
        public void TryInteract(CharacterInventory inventory);
    }
}