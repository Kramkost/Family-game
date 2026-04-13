using UnityEngine;

/// <summary>
/// Автоматически регистрирует этот объект в системе оптимизации.
/// Вешать на префабы рядом с компонентом MeshRenderer.
/// </summary>
[RequireComponent(typeof(Renderer))]
public class OptimizedMesh : MonoBehaviour
{
    private Renderer _renderer;

    private void OnEnable()
    {
        _renderer = GetComponent<Renderer>();
        
        
        if (SmartMeshOptimizer.Instance != null && _renderer != null)
        {
            SmartMeshOptimizer.Instance.Register(_renderer);
        }
    }

    private void OnDisable()
    {
        
        if (SmartMeshOptimizer.Instance != null && _renderer != null)
        {
            SmartMeshOptimizer.Instance.Unregister(_renderer);
        }
    }
}