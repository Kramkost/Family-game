using UnityEngine;
using Mirror;

public class FreezeSurface : NetworkBehaviour
{
    [Header("Настройки Ледокола")]
    [Tooltip("Сила, с которой машина отшвыривает замороженный мусор при столкновении")]
    [SerializeField] private float pushForce = 15f;
    
    [Tooltip("Немного подкидывать объекты вверх при ударе (для кинематографичности)")]
    [SerializeField] private float upwardLift = 0.5f;


    [ServerCallback]
    private void OnCollisionEnter(Collision collision)
    {
  
        if (!collision.gameObject.TryGetComponent(out Rigidbody rb)) return;

        
        if (!rb.isKinematic) return;

      
        if (!collision.gameObject.TryGetComponent(out NetworkIdentity netIdentity)) return;

        // --- ЛОГИКА РАЗМОРОЗКИ И УДАРА ---
        
        rb.isKinematic = false;

    
        Vector3 pushDirection = rb.position - transform.position;
        pushDirection.y = upwardLift; 
   
        pushDirection.Normalize(); 
        
        rb.AddForce(pushDirection * pushForce, ForceMode.Impulse);
        rb.AddTorque(Random.insideUnitSphere * pushForce, ForceMode.Impulse);

     
        RpcUnfreezeObject(netIdentity);
    }

    // Добавляем channel = Channels.Unreliable. 
    // Это спасает сеть от засорения, если мы сбиваем кучу объектов разом.
    [ClientRpc(channel = Channels.Unreliable)]
    private void RpcUnfreezeObject(NetworkIdentity netIdentity)
    {
        if (netIdentity != null && netIdentity.TryGetComponent(out Rigidbody rb))
        {
            rb.isKinematic = false;
        }
    }
}