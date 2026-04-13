using System.Collections;
using Mirror;
using UnityEngine;

namespace Events
{
    public class LightningStormEvent : NetworkBehaviour
    {
        [SerializeField] private GameObject lightningBoltPrefab;
        [SerializeField] private float minInterval = 1f;
        [SerializeField] private float maxInterval = 3f;
        [SerializeField] private float spawnRadius = 25f;
        [SerializeField] private LayerMask groundLayer = ~0;

        public override void OnStartServer()
        {
            StartCoroutine(StormLoop());
        }

        [Server]
        private IEnumerator StormLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(Random.Range(minInterval, maxInterval));

                GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
                if (players.Length == 0) continue;

                Transform targetPlayer = players[Random.Range(0, players.Length)].transform;
                Vector2 randomCircle = Random.insideUnitCircle * spawnRadius;
                Vector3 spawnPos = targetPlayer.position + new Vector3(randomCircle.x, 50f, randomCircle.y);

                if (Physics.Raycast(spawnPos, Vector3.down, out RaycastHit hit, 100f, groundLayer))
                {
                    spawnPos = hit.point;
                }
                else
                {
                    spawnPos.y = targetPlayer.position.y;
                }

                GameObject bolt = Instantiate(lightningBoltPrefab, spawnPos, Quaternion.identity);
                NetworkServer.Spawn(bolt);
            }
        }
    }
}