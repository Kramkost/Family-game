namespace Kotenkoff
{
    public interface ISoundSource
    {
        /// <summary>
        /// Проигрывает определённый звук, зависимый от введённого значения.
        /// </summary>
        /// <param name="action">Какой звук надо проиграть (Вводить с маленькой буквы).</param>
        /// <remarks>Нужный <b> <c> action </c> </b> зависит от производного класса.</remarks>
        /// <example>
        /// <b> Пример: </b>
        ///  <code>
        ///     PlaySound("reload");
        ///  </code>
        /// </example>
        public void PlaySound(string action);
    }
}