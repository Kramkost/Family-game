using UnityEngine;
using Mirror;

public class FreezeSurface : NetworkBehaviour
{
    [Header("Настройки")]
    [Tooltip("Тэг вашего автомобиля. Объекты с этим тэгом замораживаться НЕ будут.")]
    public string vehicleTag = "Vehicle";

 
    [ServerCallback]
    private void OnCollisionEnter(Collision collision)
    {
        GameObject obj = collision.gameObject;


        if (obj.CompareTag(vehicleTag))
            return;

        Rigidbody rb = obj.GetComponent<Rigidbody>();
        NetworkIdentity netIdentity = obj.GetComponent<NetworkIdentity>();

        if (rb != null && netIdentity != null && !rb.isKinematic)
        {
           
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true; 
            
            RpcFreezeObject(netIdentity);
        }
    }

    // ClientRpc заставляет этот метод выполниться на всех клиентах
    [ClientRpc]
    private void RpcFreezeObject(NetworkIdentity netIdentity)
    {
        
        if (netIdentity != null)
        {
            Rigidbody rb = netIdentity.GetComponent<Rigidbody>();
            if (rb != null)
            {
                
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
            }
        }
    }
}