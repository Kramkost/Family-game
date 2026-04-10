namespace Kotenkoff
{
    public interface ICalculator
    {
        /// <summary>
        /// Метод считает и возвращает значение указанного типа. 
        /// </summary>
        /// <param name="action">То, что надо рассчитать. (Зависит от производного класса)</param>
        /// <typeparam name="T">Возращаемый тип данных.</typeparam>
        /// <returns>Возвращает рассчитанное значение.</returns>
        /// <remarks> <b> action </b> - всегда прописывать с маленькой буквы. </remarks>
        /// <example>
        /// Пример: ( <b> ^ </b> - это типа угловые скобки)
        ///     <code>var value = _calculator.Calculate^float^("damage")</code>
        /// </example>
        public T Calculate<T>(string action);
    }
}