using UnityEngine;

[CreateAssetMenu(menuName = "Crafting/Recipe", fileName = "New Crafting Recipe")]
public class CraftingRecipe : ScriptableObject
{
    [Tooltip("Название рецепта для отображения в UI")]
    public string recipeName;

    [Tooltip("Предметы-ингредиенты")]
    [SerializeField] private CraftingIngredient[] ingredients;

    [Tooltip("Префаб создаваемого предмета")]
    [SerializeField] private GameObject resultItem;

    /// <summary>
    /// Вложенный класс, описывающий один ингредиент рецепта.
    /// Содержит тег предмета и требуемое количество.
    /// </summary>
    [System.Serializable]
    public class CraftingIngredient
    {
        [Tooltip("Тег предмета (например, 'Wood', 'Stone')")]
        [SerializeField] private string itemTag;

        [Tooltip("Требуемое количество")]
        [SerializeField] private int requiredCount;

        /// <summary>
        /// Возвращает тег ингредиента.
        /// </summary>
        public string GetItemTag() => itemTag;

        /// <summary>
        /// Возвращает требуемое количество ингредиента.
        /// </summary>
        public int GetRequiredCount() => requiredCount;
    }

    /// <summary>
    /// Возвращает название рецепта для отображения в интерфейсе.
    /// </summary>
    public string GetRecipeName() => recipeName;

    /// <summary>
    /// Возвращает массив ингредиентов рецепта.
    /// Используется системой крафта для проверки соответствия предметов.
    /// </summary>
    public CraftingIngredient[] GetIngredients() => ingredients;

    /// <summary>
    /// Возвращает префаб предмета, который будет создан при успешном крафте.
    /// </summary>
    public GameObject GetResultItem() => resultItem;
}