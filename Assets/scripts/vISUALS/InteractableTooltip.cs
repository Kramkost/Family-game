using UnityEngine;

namespace vISUALS
{
    /// <summary>
    /// Информационный ярлык. Вешается на объекты вместе с коллайдером.
    /// </summary>
    public class InteractableTooltip : MonoBehaviour
    {
        [Tooltip("Текст, который появится на экране при наведении")]
        public string promptText = "[E] Взаимодействовать";
    }
}