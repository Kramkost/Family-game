namespace Health_Bar_System
{
    public interface IDamageable
    {
        void TakeDamage(float amount);
        void Heal(float amount);
    }
}