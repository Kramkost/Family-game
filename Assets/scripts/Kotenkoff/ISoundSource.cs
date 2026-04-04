namespace Kotenkoff
{
    public interface ISoundSource
    {
        /// <summary>
        /// Проигрывает указанный звук.
        /// </summary>
        /// <param name="action">Какой звук надо проиграть (Вводить с маленькой буквы).</param>
        /// <remarks>Нужный 'action' зависит от производного класса.</remarks>
        public void PlaySound(string action)
        {
        }
    }
}