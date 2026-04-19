using Mirror;
using UnityEngine;

/// <summary>
/// Отвечает за визуальные и анимационные эффекты игрока: <br/>
/// - Анимация выброса предмета
/// - Camera shake (опционально)
/// </summary>
[RequireComponent(typeof(NetworkIdentity))]
public class PlayerJuice : NetworkBehaviour
{
    [Header("Настройки")]
    [SerializeField, Tooltip("Точка камеры для определения направления выброса.")]
    private Transform cameraTransform;

    [SerializeField, Tooltip("Сила толчка при выбросе (в анимации).")]
    private float throwForce = 1.5f;

    public Transform CameraTransform => cameraTransform;

    /// <summary>
    /// Воспроизводит анимацию выброса на клиенте.
    /// </summary>
    public void PlayDropAnimation()
    {
        Debug.Log("[PlayerJuice.PlayDropAnimation] Воспроизведение анимации выброса");

        // Здесь можно вызвать аниматор, эффект, звук и т.д.
        // Пример:
        // animator.SetTrigger("Drop");
        // CameraShake.Shake(0.1f, throwForce);
    }
}