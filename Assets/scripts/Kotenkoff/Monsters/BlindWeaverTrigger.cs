using Mirror;
using UnityEngine;

namespace Kotenkoff.Monsters
{
    public sealed class BlindWeaverTrigger : NetworkBehaviour
    {
        [SerializeField] private TheBlindWeaver monster;

        [Server]
        private void OnTriggerEnter(Collider other)
        {
            if (other.gameObject.TryGetComponent(out PlayerEntity player))
            {
                if (!monster.IsAggressive)
                {
                    if (monster.Target == null)
                    {
                        monster.ChangeTarget(player.transform);
                        monster.ChangeIsAggressive(true);
                    }
                }
            }
        }
    }
}