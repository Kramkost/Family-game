namespace Kotenkoff
{
    public interface IAnimationController
    {
        /// <summary>
        /// Проигрывает указанную анимацию.
        /// </summary>
        /// <param name="action">Какую анимацию надо проиграть. (Зависит от производного класса)</param>
        /// <remarks> <b> action </b> надо всегда прописывать маленькими буквами. </remarks>
        public void PlayAnimation(string action);
    }
}