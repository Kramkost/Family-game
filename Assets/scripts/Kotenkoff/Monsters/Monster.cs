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
       
       /// <summary>
       ///  Экземпляр класса наследуемого от интерфейса <see cref="IAnimationController"/>
       /// </summary>
       protected IAnimationController AnimController;

       /// <summary>
       /// Меняет текущее здоровье монстра. <br/>
       /// </summary>
       /// <param name="amount">Значение, которое добавится к текущему.</param>
       /// <remarks>Ставьте отрицательное число для уменьшения здоровья.</remarks>
       public abstract void ChangeHealth(float amount);

       /// <summary>
       /// Метод, реализующий смерть монстра. 
       /// </summary>
       protected abstract void MonsterDeath();
    }
}
