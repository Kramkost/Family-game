using Mirror;
using MyAssets.scripts.Kotenkoff.Character.Inventory;
using MyAssets.scripts.Kotenkoff.Items;
using UnityEngine;

/// <summary>
/// Абстрактный базовый класс для всех предметов. <br/>
/// Содержит: имя, описание, иконку, подсказку. <br/>
/// Реализует: подбор, использование, выброс. <br/>
/// Клиент вызывает методы, но все изменения происходят на сервере.
/// </summary>
[RequireComponent(typeof(NetworkIdentity))]
public abstract class Item : NetworkBehaviour
{
    /// <summary>
    /// Тип предмета (например, Food, Weapon).
    /// </summary>
    [Header("Основные:"), SerializeField, Tooltip("Тип предмета.")]
    protected ItemType itemType;
    public ItemType ItemType => itemType;

    [SerializeField, Tooltip("Имя предмета"), Header("Дополнительные:")]
    protected string itemName = "Не определено";
    public string ItemName => itemName;

    [SerializeField, Tooltip("Описание предмета"), TextArea]
    protected string itemDescription = "...";
    public string ItemDescription => itemDescription;

    [SerializeField, Tooltip("Подсказка для отображения в UI (например, [E] Подобрать).")]
    protected string itemTooltip = "[E] Использовать";
    public string ItemTooltip => itemTooltip;

    [SerializeField, Tooltip("Иконка предмета для отображения в инвентаре.")]
    protected Sprite itemIcon;

    [SerializeField, Tooltip("Спрейт-рендерер для автоматического получения иконки (если иконка не задана).")]
    private SpriteRenderer spriteRenderer;

    /// <summary>
    /// Возвращает иконку: сначала из itemIcon, иначе — из SpriteRenderer. <br/>
    /// Если иконка не найдена — возвращает null и выводит предупреждение.
    /// </summary>
    /// <returns>Sprite иконки или null.</returns>
    public Sprite GetIcon()
    {
        if (itemIcon != null)
        {
            Debug.Log($"[Item.GetIcon] Используется заданная иконка для {itemName}");
            return itemIcon;
        }

        if (spriteRenderer != null && spriteRenderer.sprite != null)
        {
            Debug.Log($"[Item.GetIcon] Используется спрайт из SpriteRenderer для {itemName}");
            return spriteRenderer.sprite;
        }

        Debug.LogWarning($"[Item.GetIcon] У предмета {gameObject.name} нет иконки!");
        return null;
    }

    /// <summary>
    /// Вызывается при взаимодействии (например, подборе). <br/>
    /// Передаёт объект в инвентарь. <br/>
    /// Выполняется на клиенте, но инвентарь изменяется только на сервере.
    /// </summary>
    /// <param name="inventory">Инвентарь игрока.</param>
    public void TryInteract(CharacterInventory inventory)
    {
        Debug.Log($"[Item.TryInteract] Попытка взаимодействия с {itemName} (тип: {itemType})");
        CmdTryPickup(inventory.netId);
    }

    /// <summary>
    /// Команда: запрос на подбор предмета. <br/>
    /// Выполняется на сервере. <br/>
    /// Проверяет, может ли инвентарь добавить предмет, и вызывает ServerAdd.
    /// </summary>
    /// <param name="inventoryNetId">Сетевой ID инвентаря.</param>
    [Command]
    private void CmdTryPickup(uint inventoryNetId)
    {
        Debug.Log($"[Item.CmdTryPickup] Сервер получил запрос на подбор {itemName} от игрока");

        if (!NetworkServer.spawned.TryGetValue(inventoryNetId, out NetworkIdentity identity))
        {
            Debug.LogError($"[Item.CmdTryPickup] Инвентарь с ID {inventoryNetId} не найден.");
            return;
        }

        if (!identity.TryGetComponent(out CharacterInventory inventory))
        {
            Debug.LogError($"[Item.CmdTryPickup] Объект с ID {inventoryNetId} не имеет CharacterInventory.");
            return;
        }

        bool added = inventory.ServerAddItem(gameObject);
        if (added)
        {
            Debug.Log($"[Item.CmdTryPickup] Предмет {itemName} добавлен в инвентарь {inventoryNetId}");
            NetworkServer.Destroy(gameObject); // Уничтожаем оригинал
        }
        else
        {
            Debug.Log($"[Item.CmdTryPickup] Инвентарь полон — {itemName} не добавлен");
        }
    }

    /// <summary>
    /// Вызывается, когда игрок хочет выбросить этот предмет. <br/>
    /// Только клиентская часть — отправляет запрос на сервер через Cmd.
    /// </summary>
    /// <param name="inventory">Инвентарь, из которого выбрасывают.</param>
    public void TryDrop(CharacterInventory inventory)
    {
        Debug.Log($"[Item.TryDrop] Запрос на выброс {itemName} из инвентаря {inventory.netId}");
        CmdRequestDrop(inventory.netId);
    }

    /// <summary>
    /// Команда: запрос на выброс предмета. <br/>
    /// Выполняется на сервере. <br/>
    /// Проверяет валидность инвентаря, удаляет предмет и создаёт его копию на земле.
    /// </summary>
    /// <param name="inventoryNetId">Сетевой ID инвентаря, из которого выбрасывают.</param>
    [Command]
    private void CmdRequestDrop(uint inventoryNetId)
    {
        Debug.Log($"[Item.CmdRequestDrop] Сервер получил запрос на выброс {itemName} от игрока");

        if (!NetworkServer.spawned.TryGetValue(inventoryNetId, out NetworkIdentity identity))
        {
            Debug.LogError($"[Item.CmdRequestDrop] Инвентарь с ID {inventoryNetId} не найден.");
            return;
        }

        if (!identity.TryGetComponent(out CharacterInventory inventory))
        {
            Debug.LogError($"[Item.CmdRequestDrop] Объект с ID {inventoryNetId} не имеет CharacterInventory.");
            return;
        }

        bool removed = inventory.ServerRemoveItem(gameObject);
        if (!removed)
        {
            Debug.LogWarning($"[Item.CmdRequestDrop] Не удалось удалить {itemName} из инвентаря.");
            return;
        }

        GameObject droppedItem = Instantiate(gameObject, Vector3.zero, Quaternion.identity);
        Transform cam = inventory.GetComponent<PlayerJuice>()?.CameraTransform;
        Vector3 dropPos = cam ? cam.position + cam.forward * 1.5f : inventory.transform.position + inventory.transform.forward * 1.5f;

        if (Physics.Raycast(cam.position, cam.forward, out RaycastHit hit, 3f))
            dropPos = hit.point;

        droppedItem.transform.position = dropPos;
        droppedItem.transform.rotation = Quaternion.LookRotation(Random.onUnitSphere, Vector3.up);

        Rigidbody rb = droppedItem.GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = false;
        foreach (Collider col in droppedItem.GetComponents<Collider>()) col.enabled = true;

        NetworkServer.Spawn(droppedItem);
        NetworkServer.Destroy(gameObject);

        TargetPlayDropAnimation(inventory.connectionToClient);
    }

    /// <summary>
    /// TargetRpc: воспроизводит анимацию выброса только у клиента владельца.
    /// </summary>
    /// <param name="target">Соединение с клиентом, который выбрасывает.</param>
    [TargetRpc]
    private void TargetPlayDropAnimation(NetworkConnection target)
    {
        Debug.Log($"[Item.TargetPlayDropAnimation] Воспроизведение анимации выброса у клиента");
        
        if (target.identity != null && target.identity.TryGetComponent<PlayerJuice>(out PlayerJuice juice))
        {
            juice.PlayDropAnimation();
            Debug.Log("[Item.TargetPlayDropAnimation] Анимация выброса запущена");
        }
        else
        {
            Debug.LogWarning("[Item.TargetPlayDropAnimation] PlayerJuice не найден на клиенте");
        }
    }
}
