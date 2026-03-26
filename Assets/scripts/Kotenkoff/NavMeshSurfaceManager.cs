using Mirror;
using UnityEngine;
using Unity.AI.Navigation;

namespace Kotenkoff
{
    public class NavMeshSurfaceManager : NetworkBehaviour
    {
        [SerializeField] private NavMeshSurface surface;

        public delegate void UpdateSurface();

        [Server]
        private void OnEnable()
        {
            ConveyorChunkManager.OnUpdateSurface += UpdateNavMeshSurface;
        }

        [Server]
        private void OnDisable()
        {
            ConveyorChunkManager.OnUpdateSurface -= UpdateNavMeshSurface;
        }

        [Server]
        private void UpdateNavMeshSurface()
        {
            surface.BuildNavMesh();
        }
    }
}
