using Mirror;

namespace Kotenkoff.Monsters
{
    public abstract class Monster : NetworkBehaviour
    {
        /// <summary>
        ///  <para>Максимальное значение здоровья</para>
        /// </summary>
        public abstract float MaxHealth { get; }
        
        /// <summary>
        ///  <para>Текущее значение здоровья монстра.</para>
        /// </summary>
       public abstract float Health { get; }
    }
}
