using UnityEngine;
using Mirror;
using Kotenkoff;
#if UNITY_EDITOR
using UnityEditor;
#endif

public enum InteractActionType { RotateOnly, TranslateOnly, Both }

/// <summary>
/// Универсальный сетевой скрипт для дверей, багажников и кнопок с аудио-откликом.
/// </summary>
public class CarInteractivePart : NetworkBehaviour, IInteractable
{
    [Header("Settings")]
    public InteractActionType actionType = InteractActionType.RotateOnly;
    public float animationSpeed = 5f;

    [Header("Rotation (Двери, Багажник)")]
    public Vector3 closedRotation;
    public Vector3 openRotation;

    [Header("Translation (Кнопки, Выдвижные ящики)")]
    public Vector3 closedPosition;
    public Vector3 openPosition;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip[] openSounds;
    [SerializeField] private AudioClip[] closeSounds;

    // Добавили hook, чтобы звук проигрывался у всех клиентов в момент изменения состояния
    [SyncVar(hook = nameof(OnStateChanged))] 
    public bool isOpen = false;

    private void Start()
    {
        ApplyStateInstantly();
    }

    [Server]
    public void ServerInteract(PlayerEntity player, PlayerInventory inventory)
    {
        isOpen = !isOpen;
    }

    private void OnStateChanged(bool oldState, bool newState)
    {
        // Предотвращаем двойное проигрывание
        if (oldState == newState) return;

        if (audioSource != null)
        {
            // Выбираем нужный массив звуков в зависимости от того, открываем мы или закрываем
            AudioClip[] currentSounds = newState ? openSounds : closeSounds;
            
            if (currentSounds != null && currentSounds.Length > 0)
            {
                // Тот самый хак с питчем для устранения эффекта "синтетики"
                audioSource.pitch = Random.Range(0.85f, 1.15f);
                audioSource.PlayOneShot(currentSounds[Random.Range(0, currentSounds.Length)]);
            }
        }
    }

    private void Update()
    {
        float dt = Time.deltaTime * animationSpeed;

        if (actionType == InteractActionType.RotateOnly || actionType == InteractActionType.Both)
        {
            Quaternion targetRot = Quaternion.Euler(isOpen ? openRotation : closedRotation);
            transform.localRotation = Quaternion.Lerp(transform.localRotation, targetRot, dt);
        }

        if (actionType == InteractActionType.TranslateOnly || actionType == InteractActionType.Both)
        {
            Vector3 targetPos = isOpen ? openPosition : closedPosition;
            transform.localPosition = Vector3.Lerp(transform.localPosition, targetPos, dt);
        }
    }

    private void ApplyStateInstantly()
    {
        if (actionType == InteractActionType.RotateOnly || actionType == InteractActionType.Both)
            transform.localRotation = Quaternion.Euler(isOpen ? openRotation : closedRotation);

        if (actionType == InteractActionType.TranslateOnly || actionType == InteractActionType.Both)
            transform.localPosition = isOpen ? openPosition : closedPosition;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (transform.parent == null) return;

        Vector3 startPos = transform.parent.TransformPoint(closedPosition);
        Vector3 endPos = transform.parent.TransformPoint(openPosition);

        if (actionType == InteractActionType.TranslateOnly || actionType == InteractActionType.Both)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(startPos, endPos);
            Gizmos.DrawWireSphere(endPos, 0.02f);
        }

        if (actionType == InteractActionType.RotateOnly || actionType == InteractActionType.Both)
        {
            Handles.color = new Color(0f, 1f, 0f, 0.2f);
            Vector3 pivot = transform.position;
            Quaternion closedQ = transform.parent.rotation * Quaternion.Euler(closedRotation);
            Quaternion openQ = transform.parent.rotation * Quaternion.Euler(openRotation);
            
            Vector3 closedForward = closedQ * Vector3.forward;
            Vector3 openForward = openQ * Vector3.forward;
            Vector3 upAxis = closedQ * Vector3.up;

            float angle = Quaternion.Angle(closedQ, openQ);
            Handles.DrawSolidArc(pivot, upAxis, closedForward, angle, 0.5f);
            
            Handles.color = Color.green;
            Handles.DrawLine(pivot, pivot + closedForward * 0.5f);
            Handles.color = Color.red;
            Handles.DrawLine(pivot, pivot + openForward * 0.5f);
        }
    }
#endif
}