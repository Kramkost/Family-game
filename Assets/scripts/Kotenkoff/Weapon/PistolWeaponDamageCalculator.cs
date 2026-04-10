using Kotenkoff.Weapon.Weapons;

namespace Kotenkoff.Weapon
{
    public class PistolWeaponDamageCalculator : ICalculator
    {
        private readonly PistolWeapon weapon;

        public PistolWeaponDamageCalculator(PistolWeapon weapon)
        {
            this.weapon = weapon;
        }
        
        public T Calculate<T>(string action)
        {
            T result = default(T);

            switch (action)
            {
                case "damage":
                    result = (T)(object)CalculateDamage();
                    break;
            }

            return result;
        }

        private float CalculateDamage()
        {
            float returnValue = 0;

            returnValue = weapon.WeaponDamage;
            
            return returnValue;
        }
    }
}