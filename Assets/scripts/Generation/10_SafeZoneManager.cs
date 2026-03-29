// =============================================================================
// FILE 10: SafeZoneManager.cs
// ISafeZoneManager: tracks road distance, fires Safe Zone spawning at intervals.
// Spawning uses NetworkServer.Spawn (Mirror) — server-authoritative.
// =============================================================================

using System;
using System.Collections.Generic;
using Mirror;
using ProceduralTerrain.Core;
using ProceduralTerrain.Debugging;
using ProceduralTerrain.Road;
using UnityEngine;

namespace ProceduralTerrain.SafeZones
{
    /// <summary>
    /// Tracks the total road arc-length and spawns Safe Zone prefabs on the server
    /// every <see cref="SpawnIntervalMeters"/> meters of road generated.
    /// </summary>
    public sealed class SafeZoneManager : ISafeZoneManager
    {
        private const string LOG_TAG = "SafeZoneManager";

        private readonly WorldSettings  _settings;
        private readonly ITerrainLogger _logger;

        // Tracks which multiples of the interval have been triggered
        private float _lastSpawnedAtDistance = 0f;
        private int   _safeZoneCount         = 0;

        // All spawned safe zones for Gizmo drawing
        private readonly List<Vector3> _spawnedPositions = new();
        public  IReadOnlyList<Vector3> SpawnedPositions  => _spawnedPositions.AsReadOnly();

        public float SpawnIntervalMeters => _settings.SafeZoneIntervalMeters;

        // ---- Constructor ----------------------------------------------------

        public SafeZoneManager(WorldSettings settings, ITerrainLogger logger)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _logger   = logger   ?? throw new ArgumentNullException(nameof(logger));

            _logger.Log(LogLevel.Info, LOG_TAG,
                $"SafeZoneManager initialized. " +
                $"Interval = {_settings.SafeZoneIntervalMeters:F0} m. " +
                $"Prefab = {(_settings.SafeZonePrefab != null ? _settings.SafeZonePrefab.name : "NULL")}");
        }

        // ---- ISafeZoneManager -----------------------------------------------

        public void OnRoadExtended(float newTotalLength, IRoadBuilder roadBuilder, int worldSeed)
        {
            if (!NetworkServer.active)
            {
                _logger.Log(LogLevel.Verbose, LOG_TAG,
                    "OnRoadExtended called on client — safe zone spawning skipped.");
                return;
            }

            if (_settings.SafeZonePrefab == null)
            {
                _logger.LogError(LOG_TAG,
                    "SafeZonePrefab is NULL in WorldSettings — cannot spawn Safe Zone!");
                return;
            }

            // Check how many new interval thresholds are crossed
            float interval = _settings.SafeZoneIntervalMeters;
            while (_lastSpawnedAtDistance + interval <= newTotalLength)
            {
                float targetDist = _lastSpawnedAtDistance + interval;
                SpawnSafeZoneAt(targetDist, roadBuilder, worldSeed);
                _lastSpawnedAtDistance = targetDist;
            }
        }

        // ---- Private helpers ------------------------------------------------

        private void SpawnSafeZoneAt(float distanceAlongRoad, IRoadBuilder roadBuilder, int worldSeed)
        {
            _safeZoneCount++;
            string tag = $"SafeZone_{_safeZoneCount}";

            _logger.Log(LogLevel.Info, LOG_TAG,
                $"▶ Spawning {tag} at distance {distanceAlongRoad:F1} m along road.");

            // Sample spline position and forward at this distance
            float t = Mathf.Clamp01(distanceAlongRoad / Mathf.Max(roadBuilder.TotalLength, 1f));
            roadBuilder.SampleSpline(t, out Vector3 splinePos, out Vector3 splineFwd);

            // Offset to the side (prefer left side of road)
            Vector3 right    = Vector3.Cross(splineFwd, Vector3.up).normalized;
            float   sideOffset = _settings.RoadWidth * 0.5f + 12f; // Just off the road edge
            Vector3 spawnPos = splinePos + right * sideOffset;

            // Snap to terrain
            spawnPos.y = SampleTerrainHeight(spawnPos.x, spawnPos.z);

            // Face the road
            Vector3 dirToRoad = (splinePos - spawnPos).normalized;
            Quaternion rot    = Quaternion.LookRotation(new Vector3(dirToRoad.x, 0f, dirToRoad.z));

            _logger.Log(LogLevel.Info, LOG_TAG,
                $"{tag}: Position={spawnPos}, Forward={rot.eulerAngles}");

            // Validate prefab has NetworkIdentity
            if (_settings.SafeZonePrefab.GetComponent<NetworkIdentity>() == null)
            {
                _logger.LogError(LOG_TAG,
                    $"SafeZone prefab '{_settings.SafeZonePrefab.name}' is missing NetworkIdentity! " +
                    $"NetworkServer.Spawn will fail.");
                return;
            }

            var instance = UnityEngine.Object.Instantiate(_settings.SafeZonePrefab, spawnPos, rot);
            instance.name = tag;
            NetworkServer.Spawn(instance);

            _spawnedPositions.Add(spawnPos);

            _logger.Log(LogLevel.Info, LOG_TAG,
                $"✅ {tag} spawned and registered with Mirror NetworkServer. " +
                $"Total safe zones: {_safeZoneCount}");
        }

        private float SampleTerrainHeight(float worldX, float worldZ)
        {
            if (Physics.Raycast(new Vector3(worldX, 1000f, worldZ), Vector3.down,
                    out var hit, 2000f, LayerMask.GetMask("Terrain")))
                return hit.point.y;

            var terrain = Terrain.activeTerrain;
            return terrain != null ? terrain.SampleHeight(new Vector3(worldX, 0, worldZ)) : 0f;
        }
    }
}
