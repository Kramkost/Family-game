using Mirror;
using MyAssets.scripts.Kotenkoff.Character.Inventory;
using UnityEngine;

/// <summary>
/// Отвечает за обнаружение и взаимодействие с предметами. <br/>
/// Например: подбор по нажатию E. <br/>
/// Использует Raycast для определения объекта в зоне видимости.
/// </summary>
[RequireComponent(typeof(CharacterInventory))]
public class CharacterInteract : NetworkBehaviour
{
    [Header("Настройки")]
    [SerializeField, Tooltip("Максимальная дистанция взаимодействия.")]
    private float interactionDistance = 3f;

    [SerializeField, Tooltip("Клавиша для взаимодействия (по умолчанию E).")]
    private KeyCode interactKey = KeyCode.E;

    [SerializeField, Tooltip("Слой, содержащий предметы для взаимодействия.")]
    private LayerMask interactLayer;

    [SerializeField, Tooltip("Точка камеры для направления взаимодействия.")]
    private Transform cameraTransform;

    private CharacterInventory inventory;

    /// <summary>
    /// Вызывается при старте. Получает ссылку на инвентарь.
    /// </summary>
    private void Awake()
    {
        inventory = GetComponent<CharacterInventory>();
        if (cameraTransform == null)
        {
            Debug.LogError($"[CharacterInteract.Awake] Не задана точка камеры для {gameObject.name}");
        }
    }

    /// <summary>
    /// Вызывается каждый кадр. <br/>
    /// Проверяет нажатие клавиши взаимодействия и выполняет попытку подбора.
    /// </summary>
    private void Update()
    {
        if (!isLocalPlayer) return;

        if (Input.GetKeyDown(interactKey))
        {
            TryInteract();
        }
    }

    /// <summary>
    /// Пытается взаимодействовать с объектом перед игроком. <br/>
    /// Использует Raycast для поиска предмета.
    /// </summary>
    public void TryInteract()
    {
        Debug.Log($"[CharacterInteract.TryInteract] Попытка взаимодействия на расстоянии {interactionDistance}");

        if (!cameraTransform)
        {
            Debug.LogWarning("[CharacterInteract.TryInteract] Камера не назначена — взаимодействие невозможно");
            return;
        }

        Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, interactionDistance, interactLayer))
        {
            Debug.Log($"[CharacterInteract.TryInteract] Обнаружен объект: {hit.collider.name}");

            if (hit.collider.TryGetComponent<Item>(out Item item))
            {
                Debug.Log($"[CharacterInteract.TryInteract] Объект является предметом: {item.ItemName}");
                item.TryInteract(inventory);
            }
            else
            {
                Debug.Log($"[CharacterInteract.TryInteract] Объект {hit.collider.name} не является предметом");
            }
        }
        else
        {
            Debug.Log("[CharacterInteract.TryInteract] Нет объектов в зоне взаимодействия");
        }
    }
    
    /// <summary>
    /// Отрисовывает луч взаимодействия в редакторе Unity (Scene View). <br/>
    /// Зелёный — если объект найден, красный — если нет. <br/>
    /// Помогает визуально настроить дистанцию и направление.
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        if (cameraTransform == null) return;

        Vector3 rayStart = cameraTransform.position;
        Vector3 rayDir = cameraTransform.forward;

        Ray ray = new Ray(rayStart, rayDir);
        if (Physics.Raycast(ray, out RaycastHit hit, interactionDistance, interactLayer))
        {
            // Луч до объекта — зелёный
            Gizmos.color = Color.green;
            Gizmos.DrawRay(rayStart, rayDir * hit.distance);
            Gizmos.DrawSphere(hit.point, 0.1f);

            // Остаток — пунктир до максимальной дистанции
            Gizmos.color = Color.red;
            Gizmos.DrawRay(rayStart + rayDir * hit.distance, rayDir * (interactionDistance - hit.distance));
        }
        else
        {
            // Полный луч — красный (объект не найден)
            Gizmos.color = Color.red;
            Gizmos.DrawRay(rayStart, rayDir * interactionDistance);
        }

        // Отображение дистанции (конус)
        Vector3 left = Quaternion.AngleAxis(5f, Vector3.up) * rayDir;
        Vector3 right = Quaternion.AngleAxis(-5f, Vector3.up) * rayDir;
        Vector3 up = Quaternion.AngleAxis(5f, Vector3.right) * rayDir;
        Vector3 down = Quaternion.AngleAxis(-5f, Vector3.right) * rayDir;

        Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f); // Оранжевый полупрозрачный
        Gizmos.DrawLine(rayStart, rayStart + left * interactionDistance);
        Gizmos.DrawLine(rayStart, rayStart + right * interactionDistance);
        Gizmos.DrawLine(rayStart, rayStart + up * interactionDistance);
        Gizmos.DrawLine(rayStart, rayStart + down * interactionDistance);
    }
}
