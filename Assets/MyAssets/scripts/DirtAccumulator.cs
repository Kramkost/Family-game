using UnityEngine;

public class DirtAccumulator : MonoBehaviour
{
    [Header("Shader Settings")]
    public Renderer targetRenderer; // Меш, на котором лежит материал
    public string dirtPropertyName = "_DirtLevel"; // Имя свойства в шейдере

    [Header("Accumulation Settings")]
    [Range(0, 1)] public float currentDirt = 0f; // Текущий уровень грязи (0-1)
    public float accumulationSpeed = 0.05f; // Как быстро копится (в секунду на макс скорости)
    public float cleaningSpeed = 0.1f;    // Как быстро очищается (если стоим)
    
    [Header("Speed Dependency")]
    public float minSpeedToAccumulate = 1f; // Минимальная скорость для грязи
    public float maxSpeedForScale = 20f;   // Скорость, при которой грязь копится макс быстро

    private Material targetMaterial;
    private Vector3 lastPosition;

    void Start()
    {
        // Получаем материал. Использование targetRenderer.material создает КOПИЮ материала.
        // Если материалов несколько, используй targetRenderer.materials[0].
        if (targetRenderer != null)
        {
            targetMaterial = targetRenderer.material;
        }
        
        lastPosition = transform.position;
    }

    void Update()
    {
        if (targetMaterial == null) return;

        // 1. Рассчитываем текущую скорость (или используй данные от Rigidbody)
        float currentSpeed = (transform.position - lastPosition).magnitude / Time.deltaTime;
        lastPosition = transform.position;

        // 2. Логика накопления/очистки
        if (currentSpeed > minSpeedToAccumulate)
        {
            // Чем быстрее едем, тем быстрее пачкаемся
            float speedFactor = Mathf.Clamp01(currentSpeed / maxSpeedForScale);
            currentDirt += accumulationSpeed * speedFactor * Time.deltaTime;
        }
        else
        {
            // Если стоим или едем медленно - грязь не копится (можно добавить очистку)
            // currentDirt -= cleaningSpeed * Time.deltaTime; // Раскомменть для автоочистки
        }

        // 3. Ограничиваем значение от 0 до 1
        currentDirt = Mathf.Clamp01(currentDirt);

        // 4. Передаем значение в шейдер
        targetMaterial.SetFloat(dirtPropertyName, currentDirt);
    }
}