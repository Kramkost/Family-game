using Mirror;
using UnityEngine;
using Unity.AI.Navigation;

namespace Kotenkoff
{
    /// <summary>
    ///  <para>Менеджер для NavMesh Surface</para>
    /// </summary>
    [RequireComponent(typeof(NavMeshSurface)), RequireComponent(typeof(NetworkIdentity))]
    public class NavMeshSurfaceManager : NetworkBehaviour
    {
        [SerializeField] private NavMeshSurface surface;

        [Server]
        private void OnEnable()
        {
            ConveyorChunkManager.OnEndGeneration += UpdateNavMeshSurface;
        }

        [Server]
        private void OnDisable()
        {
            ConveyorChunkManager.OnEndGeneration -= UpdateNavMeshSurface;
        }

        [Server]
        private void UpdateNavMeshSurface()
        {
            surface.BuildNavMesh();
        }
    }
}
