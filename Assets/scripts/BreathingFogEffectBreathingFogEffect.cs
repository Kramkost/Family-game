using UnityEngine;

[ExecuteInEditMode]
[RequireComponent(typeof(Camera))]
public class BreathingFogEffect : MonoBehaviour
{
    [Tooltip("Материал с нашим кастомным шейдером тумана")]
    public Material fogMaterial;

    private Camera cam;

    private void OnEnable()
    {
        cam = GetComponent<Camera>();
        cam.depthTextureMode |= DepthTextureMode.Depth; 
    }

    
    [ImageEffectOpaque] // Рендерим ДО прозрачных объектов и UI, чтобы не сломать их
    private void OnRenderImage(RenderTexture src, RenderTexture dest)
    {
        if (fogMaterial != null)
        {
            
            Matrix4x4 viewProj = cam.projectionMatrix * cam.worldToCameraMatrix;
            fogMaterial.SetMatrix("_InverseViewProj", viewProj.inverse);
            
            
            Graphics.Blit(src, dest, fogMaterial);
        }
        else
        {
            Graphics.Blit(src, dest);
        }
    }
}