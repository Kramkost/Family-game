using Mirror;
using UnityEngine;

namespace Kotenkoff.Monsters
{
    public class ShadowShape : NetworkBehaviour
    {
        private void OnCollisionEnter(Collision other)
        {
            // Если столкнётся с машиной
            if (other.gameObject.GetComponentInParent<CarHybridSystem>())
            {
                // Сломать фары
            }
        }
    }
}
