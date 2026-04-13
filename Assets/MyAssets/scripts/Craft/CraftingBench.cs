using UnityEngine;
using Mirror;
using System.Collections.Generic;
using Kotenkoff;
using TMPro;

/// <summary>
/// Основной класс верстака для крафта предметов.
/// Управляет размещением предметов, проверкой рецептов, созданием новых предметов и взаимодействием с UI.
/// </summary>
public class CraftingBench : NetworkBehaviour, IInteractable
{
    [Header("Crafting Settings")]
    [SerializeField] private Vector3 craftingPositionOffset = new Vector3(0, 1, 0);
    [SerializeField] private float craftingRadius = 0.5f;

    [Header("UI References")]
    [SerializeField] private GameObject craftingUIGameObject;
    [SerializeField] private TMP_Text itemsListText;
    [SerializeField] private TMP_Text recipeHintText;
    [SerializeField] private Transform craftingPoint;

    [Header("Recipes")]
    [SerializeField] private CraftingRecipe[] availableRecipes;

    private List<GameObject> itemsOnBench = new List<GameObject>();
    private PlayerEntity interactingPlayer;
    private PlayerInventory playerInventory;
    // Хранит игрока, которому показан UI
    private NetworkConnectionToClient currentUIClient;

    public void ServerInteract(PlayerEntity player, PlayerInventory inventory)
    {
        if (!isServer) return;
        interactingPlayer = player;
        playerInventory = inventory;
    }

    [Command]
    public void CmdPlaceItemOnBench(GameObject item, NetworkConnectionToClient placingPlayer)
    {
        // Ограничение: не больше 2-х предметов
        if (itemsOnBench.Count >= 2)
        {
            RpcShowError(placingPlayer, "На верстаке уже 2 предмета!");
            return;
        }

        if (item == null || !IsItemInCraftingRange(item)) return;

        itemsOnBench.Add(item);
        item.transform.position = craftingPoint != null
            ? craftingPoint.position
            : transform.position + craftingPositionOffset;

        // Обновляем UI для всех игроков
        RpcUpdateCraftingUI();

        // Если это второй предмет — показываем UI только тому игроку, который его разместил
        if (itemsOnBench.Count == 2)
        {
            currentUIClient = placingPlayer;
            RpcShowCraftingUI(placingPlayer);
            UpdateRecipeHint();
        }
    }

    [Command]
    public void CmdRemoveItemFromBench(GameObject item, NetworkConnectionToClient removingPlayer)
    {
        if (!itemsOnBench.Contains(item))
        {
            RpcShowError(removingPlayer, "Этот предмет не на верстаке!");
            return;
        }

        itemsOnBench.Remove(item);

        // Возвращаем предмет в инвентарь игрока
        playerInventory.AddItem(item);

        RpcUpdateCraftingUI();

        // Если после удаления осталось меньше 2 предметов — скрываем UI
        if (itemsOnBench.Count < 2 && currentUIClient != null)
        {
            RpcHideCraftingUI(currentUIClient);
            currentUIClient = null;
        }
        else
        {
            // Обновляем подсказку о рецепте, если осталось 2 предмета
            UpdateRecipeHint();
        }
    }

    private bool IsItemInCraftingRange(GameObject item)
    {
        Vector3 itemPos = item.transform.position;
        Vector3 benchPos = craftingPoint != null
            ? craftingPoint.position
            : transform.position + craftingPositionOffset;
        return Vector3.Distance(itemPos, benchPos) <= craftingRadius;
    }

    [Command]
    public void CmdConfirmCraft()
    {
        if (itemsOnBench.Count != 2)
        {
            if (currentUIClient != null)
                RpcShowError(currentUIClient, "Нужно ровно 2 предмета для крафта!");
            return;
        }

        CraftResult result = CheckCraftingRecipe(itemsOnBench);
        if (result.success)
        {
            SpawnCraftedItem(result.craftedItemPrefab);
            RemoveItemsFromBench();
        }
        else if (currentUIClient != null)
        {
            RpcShowError(currentUIClient, "Рецепт не найден!");
        }

        if (currentUIClient != null)
        {
            RpcHideCraftingUI(currentUIClient);
            currentUIClient = null;
        }
    }

    [Command]
    public void CmdCancelCraft()
    {
        if (currentUIClient != null)
        {
            // Возвращаем все предметы в инвентарь
            foreach (var item in itemsOnBench)
            {
                playerInventory.AddItem(item);
            }
            itemsOnBench.Clear();
            RpcUpdateCraftingUI(); // Обновляем список для всех
            RpcHideCraftingUI(currentUIClient);
            currentUIClient = null;
        }
    }

    /// <summary>
    /// Проверяет комбинацию предметов на соответствие любому из доступных рецептов
    /// </summary>
    private CraftResult CheckCraftingRecipe(List<GameObject> items)
    {
        foreach (var recipe in availableRecipes)
        {
            bool recipeMatches = true;
            var itemTags = new Dictionary<string, int>();

            // Собираем теги всех предметов на верстаке
            foreach (var item in items)
            {
                if (item == null) continue;
                string tag = item.tag;
                if (!itemTags.ContainsKey(tag))
                    itemTags[tag] = 0;
                itemTags[tag]++;
            }

            // Проверяем соответствие по каждому ингредиенту
            foreach (var ingredient in recipe.GetIngredients())
            {
                if (!itemTags.ContainsKey(ingredient.GetItemTag()) ||
                    itemTags[ingredient.GetItemTag()] < ingredient.GetRequiredCount())
                {
                    recipeMatches = false;
            break;
                }
            }

            if (recipeMatches)
            {
                return new CraftResult
                {
                    success = true,
                    craftedItemPrefab = recipe.GetResultItem()
                };
            }
        }

        return new CraftResult { success = false };
    }

    /// <summary>
    /// Обновляет подсказку о рецепте для текущего игрока с UI
    /// </summary>
    private void UpdateRecipeHint()
    {
        CraftResult result = CheckCraftingRecipe(itemsOnBench);
        string hint = result.success
            ? $"Рецепт распознан: {GetRecipeNameForItems(itemsOnBench)}"
            : "Рецепт не распознан";

        if (currentUIClient != null)
        {
            RpcShowRecipeHint(currentUIClient, hint);
        }
    }

    /// <summary>
    /// Получает название рецепта для текущей комбинации предметов
    /// </summary>
    private string GetRecipeNameForItems(List<GameObject> items)
    {
        foreach (var recipe in availableRecipes)
        {
            bool recipeMatches = true;
            var itemTags = new Dictionary<string, int>();

            foreach (var item in items)
            {
                if (item == null) continue;
                string tag = item.tag;
                if (!itemTags.ContainsKey(tag)) itemTags[tag] = 0;
                itemTags[tag]++;
            }

            foreach (var ingredient in recipe.GetIngredients())
            {
                if (!itemTags.ContainsKey(ingredient.GetItemTag()) ||
                itemTags[ingredient.GetItemTag()] < ingredient.GetRequiredCount())
                {
                    recipeMatches = false;
                    break;
                }
            }

            if (recipeMatches)
            {
                return recipe.GetRecipeName();
            }
        }

        return "Неизвестный рецепт";
    }

    /// <summary>
    /// Создаёт новый предмет на основе префаба результата рецепта
    /// </summary>
    /// <param name="itemPrefab">Префаб создаваемого предмета</param>
    private void SpawnCraftedItem(GameObject itemPrefab)
    {
        if (itemPrefab == null) return;
        GameObject craftedItem = Instantiate(itemPrefab);
        craftedItem.transform.position = craftingPoint != null
            ? craftingPoint.position
            : transform.position + craftingPositionOffset;
        NetworkServer.Spawn(craftedItem);
    }

     /// <summary>
    /// Удаляет использованные предметы с верстака и из инвентаря
    /// </summary>
    private void RemoveItemsFromBench()
    {
        foreach (var item in itemsOnBench)
        {
            if (item != null)
            {
                // Сначала удаляем из инвентаря игрока
                playerInventory.RemoveItem(item);
                // Затем уничтожаем объект на сервере
                NetworkServer.Destroy(item);
            }
        }
        itemsOnBench.Clear();
    }

    /// <summary>
    /// Отображает UI крафта целевому клиенту
    /// Вызывается только для игрока, который разместил второй предмет
    /// </summary>
    /// <param name="target">Соединение клиента, которому показать UI</param>
    [TargetRpc]
    private void RpcShowCraftingUI(NetworkConnectionToClient target)
    {
        craftingUIGameObject.SetActive(true);
        RpcUpdateCraftingUI(); // Сразу обновляем список предметов
    }

    /// <summary>
    /// Скрывает UI крафта у целевого клиента
    /// </summary>
    /// <param name="target">Соединение клиента, у которого скрыть UI</param>
    [TargetRpc]
    private void RpcHideCraftingUI(NetworkConnectionToClient target)
    {
        craftingUIGameObject.SetActive(false);
    }

    /// <summary>
    /// Обновляет список предметов на верстаке для всех клиентов
    /// Синхронизирует состояние верстака между игроками
    /// </summary>
    [TargetRpc]
    private void RpcUpdateCraftingUI()
    {
        if (itemsListText == null) return;

        string itemsText = "Предметы на верстаке:\n";
        for (int i = 0; i < itemsOnBench.Count; i++)
        {
            itemsText += $"{i + 1}. {itemsOnBench[i].name}\n";
        }
        itemsListText.text = itemsText;
    }

    /// <summary>
    /// Показывает подсказку о распознанном рецепте целевому игроку
    /// </summary>
    /// <param name="target">Соединение клиента, которому показать подсказку</param>
    /// <param name="hint">Текст подсказки</param>
    [TargetRpc]
    private void RpcShowRecipeHint(NetworkConnectionToClient target, string hint)
    {
        if (recipeHintText != null)
            recipeHintText.text = hint;
    }

    /// <summary>
    /// Показывает сообщение об ошибке целевому игроку
    /// Используется для информирования о проблемах с крафтом
    /// </summary>
    /// <param name="target">Соединение клиента, которому показать ошибку</param>
    /// <param name="errorMessage">Текст ошибки</param>
    [TargetRpc]
    private void RpcShowError(NetworkConnectionToClient target, string errorMessage)
    {
        if (recipeHintText != null)
            recipeHintText.text = $"Ошибка: {errorMessage}";
    }
}

/// <summary>
/// Структура результата проверки рецепта крафта
/// Содержит флаг успеха и префаб создаваемого предмета
/// </summary>
public struct CraftResult
{
    public bool success;
    public GameObject craftedItemPrefab;
}