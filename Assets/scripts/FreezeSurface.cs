using UnityEngine;
using Mirror;

public class FreezeSurface : NetworkBehaviour
{
    [Header("Настройки Ледокола")]
    [Tooltip("Сила, с которой машина отшвыривает замороженный мусор при столкновении")]
    public float pushForce = 15f;
    
    [Tooltip("Немного подкидывать объекты вверх при ударе (для кинематографичности)")]
    public float upwardLift = 0.5f;

    // Обработка коллизий происходит только на сервере
    [ServerCallback]
    private void OnCollisionEnter(Collision collision)
    {
        GameObject obj = collision.gameObject;
        Rigidbody rb = obj.GetComponent<Rigidbody>();
        NetworkIdentity netIdentity = obj.GetComponent<NetworkIdentity>();

        // Проверяем: если у объекта есть физика, он сетевой, и он сейчас "ЗАМОРОЖЕН" (isKinematic == true)
        if (rb != null && netIdentity != null && rb.isKinematic)
        {
            // 1. РАЗМОРАЖИВАЕМ физику на сервере
            rb.isKinematic = false;

            // 2. Вычисляем направление удара (от центра машины к объекту)
            Vector3 pushDirection = obj.transform.position - transform.position;
            pushDirection.y = upwardLift; // Добавляем немного подъемной силы
            
            // 3. Придаем мощный импульс, чтобы объект отлетел в сторону
            rb.AddForce(pushDirection.normalized * pushForce, ForceMode.Impulse);
            // Добавим немного вращения для красоты
            rb.AddTorque(Random.insideUnitSphere * pushForce, ForceMode.Impulse);

            // 4. Отправляем команду всем клиентам разморозить этот объект у себя, 
            // чтобы они увидели, как он отлетает, без сетевых лагов
            RpcUnfreezeObject(netIdentity);
        }
    }

    // ClientRpc заставляет этот метод выполниться на всех клиентах
    [ClientRpc]
    private void RpcUnfreezeObject(NetworkIdentity netIdentity)
    {
        // Проверяем, существует ли объект на клиенте
        if (netIdentity != null)
        {
            Rigidbody rb = netIdentity.GetComponent<Rigidbody>();
            if (rb != null)
            {
                // Размораживаем физику на стороне клиента
                rb.isKinematic = false;
            }
        }
    }
}