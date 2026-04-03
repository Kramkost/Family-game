using Mirror;
using UnityEngine;

namespace Kotenkoff.Monsters
{
    public sealed class BlindWeaverTrigger : NetworkBehaviour
    {
        [SerializeField] private TheBlindWeaver monster;

        // ВАЖНО: Для событий Unity (OnTriggerEnter, Update) всегда используем [ServerCallback]
        [ServerCallback]
        private void OnTriggerEnter(Collider other)
        {
            if (other.gameObject.TryGetComponent(out PlayerEntity player))
            {
                if (!monster.IsAggressive && monster.Target == null)
                {
                    monster.ChangeTarget(player.transform);
                    monster.ChangeIsAggressive(true);
                }
            }
        }
    }
}