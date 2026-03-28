using UnityEngine;
using Mirror;
using Kotenkoff; // Подключаем твой неймспейс

public class RoadMonster : NetworkBehaviour
{
    [Header("Настройки видимости")]
    [Tooltip("Как близко нужно подойти/подъехать, чтобы монстр среагировал на взгляд")]
    [SerializeField] private float sightRadius = 45f;
    [Tooltip("Угол обзора камеры, при котором монстр считается замеченным")]
    [SerializeField] private float fieldOfView = 50f;
    [Tooltip("Слои препятствий (чтобы не замечать монстра сквозь деревья)")]
    [SerializeField] private LayerMask obstacleLayer;

    [Header("Столкновение с авто")]
    [Tooltip("Сила, с которой машину отбросит назад при ДТП с монстром")]
    [SerializeField] private float repelForce = 80000f;

    [Header("Аудио-Скример")]
    [SerializeField] private AudioClip scareSound;

    // Синхронизируем состояние, чтобы никто не вызвал скример дважды
    [SyncVar] private bool isTriggered = false;

    private void Update()
    {
        // Проверка зрения работает ТОЛЬКО локально у каждого клиента
        if (isServer && isTriggered) return; 
        if (!isClient || isTriggered) return;

        Camera cam = Camera.main;
        if (cam == null) return;

        Vector3 dirToMonster = transform.position - cam.transform.position;
        float distance = dirToMonster.magnitude;

        // Если мы достаточно близко...
        if (distance <= sightRadius)
        {
            // ...и смотрим примерно в его сторону
            float angle = Vector3.Angle(cam.transform.forward, dirToMonster);
            if (angle <= fieldOfView)
            {
                // Проверяем, нет ли между камерой и монстром других объектов (деревьев, камней)
                if (!Physics.Raycast(cam.transform.position, dirToMonster.normalized, distance, obstacleLayer))
                {
                    CmdPlayerSawMe(); // Бьем тревогу на сервер!
                }
            }
        }
    }

    // Command(requiresAuthority = false) позволяет ЛЮБОМУ игроку отправить эту команду,
    // даже если он не "владелец" этого монстра по сети.
    [Command(requiresAuthority = false)]
    private void CmdPlayerSawMe()
    {
        if (isTriggered) return;
        isTriggered = true;
        
        RpcDoJumpscare();
        Invoke(nameof(DestroyMonster), 0.1f); 
    }

    [ServerCallback]
    private void OnTriggerEnter(Collider other)
    {
        if (isTriggered) return;

        // Ищем твою систему машины в объекте, который в нас врезался
        CarHybridSystem car = other.GetComponentInParent<CarHybridSystem>();
        
        if (car != null)
        {
            isTriggered = true;
            Rigidbody carRb = car.GetComponent<Rigidbody>();

            // Если машина использует физику (не на беговой дорожке) - откидываем ее
            if (carRb != null && !carRb.isKinematic)
            {
                carRb.linearVelocity = Vector3.zero; // Жестко гасим скорость
                
           
                Vector3 pushDir = (carRb.position - transform.position).normalized;
                pushDir.y = 0.5f; 
                
                carRb.AddForce(pushDir * repelForce, ForceMode.Impulse);
            }
            else if (car != null)
            {
                // Если включен RoadMill, просто глушим движок от страха
                car.UpdateEngineState(3); 
            }

            RpcDoJumpscare();
            DestroyMonster();
        }
    }

    [ClientRpc]
    private void RpcDoJumpscare()
    {
        if (scareSound != null && Camera.main != null)
        {
          
            AudioSource.PlayClipAtPoint(scareSound, Camera.main.transform.position, 1f);
        }

        
        foreach (Renderer r in GetComponentsInChildren<Renderer>()) r.enabled = false;
    }

    [Server]
    private void DestroyMonster()
    {
        NetworkServer.Destroy(gameObject);
    }
}