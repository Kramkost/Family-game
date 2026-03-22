using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Асинхронный оптимизатор мешей. 
/// Отключает рендер у объектов, которые далеко или вне поля зрения камеры, 
/// размазывая нагрузку на несколько кадров.
/// </summary>
public class SmartMeshOptimizer : MonoBehaviour
{
    public static SmartMeshOptimizer Instance;

    [Header("Настройки оптимизации")]
    [Tooltip("На каком расстоянии полностью отключать рендер объектов")]
    public float cullDistance = 120f;
    
    [Tooltip("Сколько объектов проверять за один кадр (защита от лагов)")]
    public int checksPerFrame = 100;

    // Список всех зарегистрированных объектов
    private List<Renderer> managedRenderers = new List<Renderer>();
    private int currentIndex = 0;
    
    private Camera mainCam;
    private Plane[] cameraPlanes;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        mainCam = Camera.main;
    }

   
    public void Register(Renderer r)
    {
        if (r != null && !managedRenderers.Contains(r))
        {
            managedRenderers.Add(r);
        }
    }

    
    public void Unregister(Renderer r)
    {
        managedRenderers.Remove(r);
    }

    private void Update()
    {
        if (managedRenderers.Count == 0 || mainCam == null) return;

        
        cameraPlanes = GeometryUtility.CalculateFrustumPlanes(mainCam);
        
        float cullDistSq = cullDistance * cullDistance;
        Vector3 camPos = mainCam.transform.position;

      
        int checks = Mathf.Min(checksPerFrame, managedRenderers.Count);

        for (int i = 0; i < checks; i++)
        {
            
            if (currentIndex >= managedRenderers.Count) currentIndex = 0;

            Renderer r = managedRenderers[currentIndex];
            
            if (r != null)
            {
                bool shouldBeVisible = true;
                
                
                if ((r.transform.position - camPos).sqrMagnitude > cullDistSq)
                {
                    shouldBeVisible = false;
                }
                // ПРОВЕРКА КАМЕРЫ (Находится ли объект в поле зрения?)
                else if (!GeometryUtility.TestPlanesAABB(cameraPlanes, r.bounds))
                {
                    shouldBeVisible = false;
                }

                
                if (r.enabled != shouldBeVisible)
                {
                    r.enabled = shouldBeVisible;
                }
            }
            else
            {
               
                managedRenderers.RemoveAt(currentIndex);
                currentIndex--; 
            }

            currentIndex++;
        }
    }
}