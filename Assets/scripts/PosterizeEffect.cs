using UnityEngine;

[ExecuteInEditMode] 
[RequireComponent(typeof(Camera))]
public class PosterizeEffect : MonoBehaviour
{
    [Tooltip("Закинь сюда материал с шейдером Hidden/RetroPosterize")]
    public Material effectMaterial;

    [Range(2, 256)]
    [Tooltip("Чем меньше число, тем хуже и ретровее графика")]
    public int colorSteps = 16;

    void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (effectMaterial != null)
        {
           
            effectMaterial.SetFloat("_ColorSteps", colorSteps);
           
            Graphics.Blit(source, destination, effectMaterial);
        }
        else
        {
           
            Graphics.Blit(source, destination);
        }
    }
}