using UnityEngine;
using Mirror;
using Kotenkoff;

/// <summary>
/// Networked manager for vehicle breakdowns. 
/// Handles syncing visual effects (smoke/fire) and acts as the target for the repair minigame.
/// </summary>
public class BreakdownManager : NetworkBehaviour, IInteractable
{
    [System.Serializable]
    public struct BreakdownZone
    {
        [Tooltip("Name for inspector organization (e.g., 'Engine', 'Left Wheel')")]
        public string zoneName;
        public Transform zoneTransform;
    }

    [Header("Visuals")]
    [Tooltip("Prefab spawned when a breakdown occurs (e.g., Smoke Particle System)")]
    [SerializeField] private GameObject breakdownVfxPrefab;

    [Header("Zones Setup")]
    [SerializeField] private BreakdownZone[] zones;

    // --- NETWORKED STATE ---

    [Header("Network State (Read Only)")]
    [SyncVar(hook = nameof(OnBreakdownStateChanged))]
    public bool isBroken = false;

    [SyncVar] 
    private int activeZoneIndex = -1;

    private GameObject activeVfxInstance;

    // --- SERVER LOGIC ---

    /// <summary>
    /// Triggers a breakdown at a specific zone. Called by the server (e.g., on collision).
    /// </summary>
    [Server]
    public void TriggerBreakdown(int zoneIndex)
    {
        if (isBroken || zones.Length == 0) return;

        activeZoneIndex = zoneIndex;
        isBroken = true; // Triggers the hook on all clients
    }

    /// <summary>
    /// Debug method to trigger a random breakdown. 
    /// Right-click the component in the Inspector to use it.
    /// </summary>
    [ContextMenu("Debug: Trigger Random Breakdown")]
    private void DebugTriggerBreakdown()
    {
        if (!Application.isPlaying || !isServer) return;
        TriggerBreakdown(Random.Range(0, zones.Length));
    }

    /// <summary>
    /// Clears the breakdown. Called via Command from the player who won the minigame.
    /// </summary>
    [Server]
    public void RepairBreakdown()
    {
        if (!isBroken) return;
        
        isBroken = false;
        activeZoneIndex = -1;
    }

    // --- CLIENT LOGIC ---

    /// <summary>
    /// Automatically spawns or destroys the VFX on all clients when the state changes.
    /// </summary>
    private void OnBreakdownStateChanged(bool oldState, bool newState)
    {
        if (newState == true && activeZoneIndex >= 0 && activeZoneIndex < zones.Length)
        {
            Transform targetTransform = zones[activeZoneIndex].zoneTransform;
            if (breakdownVfxPrefab != null && targetTransform != null)
            {
                // Spawn local VFX attached to the car part
                activeVfxInstance = Instantiate(breakdownVfxPrefab, targetTransform.position, targetTransform.rotation, targetTransform);
            }
        }
        else
        {
            if (activeVfxInstance != null)
            {
                Destroy(activeVfxInstance);
            }
        }
    }

    /// <summary>
    /// Triggered by PlayerEntity when pressing 'E' on the broken car.
    /// </summary>
    [Server]
    public void ServerInteract(PlayerEntity player, PlayerInventory inventory)
    {
        if (!isBroken) return;

        // Открываем мини-игру конкретному клиенту
        TargetOpenMinigame(player.connectionToClient);
    }

    [TargetRpc]
    private void TargetOpenMinigame(NetworkConnection target)
    {
        // Opens the local minigame and passes 'this' so the UI knows which car to fix
        RepairUIManager.Instance.OpenMinigame(this);
    }
}