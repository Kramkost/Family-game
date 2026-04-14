using System.Collections;
using Health_Bar_System;
using Mirror;
using UnityEngine;

namespace Events
{
    public class LightningBolt : NetworkBehaviour
    {
        [SerializeField] private float warningTime = 1.5f;
        [SerializeField] private float strikeRadius = 4f;
        [SerializeField] private float explosionForce = 20f;
        [SerializeField] private float damage = 40f;

        [Header("Visuals")]
        [SerializeField] private GameObject warningDecal;
        [SerializeField] private GameObject strikeParticles;
        [SerializeField] private AudioSource thunderAudio;

        public override void OnStartServer()
        {
            StartCoroutine(StrikeRoutine());
        }

        private void Start()
        {
            if (warningDecal != null) warningDecal.SetActive(true);
            if (strikeParticles != null) strikeParticles.SetActive(false);
        }

        [Server]
        private IEnumerator StrikeRoutine()
        {
            yield return new WaitForSeconds(warningTime);
            RpcPlayStrikeEffects();

            Collider[] colliders = Physics.OverlapSphere(transform.position, strikeRadius);
            
            foreach (Collider col in colliders)
            {
                if (col.TryGetComponent(out IDamageable damageable))
                {
                    damageable.TakeDamage(damage);
                }

                if (col.TryGetComponent(out Rigidbody rb) && !rb.isKinematic)
                {
                    rb.AddExplosionForce(explosionForce * 50f, transform.position, strikeRadius, 1f, ForceMode.Impulse);
                }
                else if (col.TryGetComponent(out CharacterController cc))
                {
                    Vector3 pushDir = (col.transform.position - transform.position).normalized;
                    pushDir.y = 0.5f; 
                    cc.Move(pushDir * explosionForce * Time.fixedDeltaTime * 10f);
                }
            }

            yield return new WaitForSeconds(3f); 
            NetworkServer.Destroy(gameObject);
        }

        [ClientRpc]
        private void RpcPlayStrikeEffects()
        {
            if (warningDecal != null) warningDecal.SetActive(false);
            if (strikeParticles != null) strikeParticles.SetActive(true);
            
            if (thunderAudio != null) 
            {
                thunderAudio.pitch = Random.Range(0.85f, 1.15f);
                thunderAudio.Play();
            }
        }
    }
}