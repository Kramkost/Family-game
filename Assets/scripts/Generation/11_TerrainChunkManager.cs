// =============================================================================
// FILE 11: TerrainChunkManager.cs  (MODIFIED — Requirement 1)
// Changes marked // NEW [REQ-1]:
//   - _roadMeshBuilder field added
//   - InitializeSubsystems: instantiates RoadMeshBuilder
//   - GenerateChunk: calls _roadMeshBuilder.BuildChunkRoadMesh() after terrain is built
//   - OnDestroy: disposes _roadMeshBuilder
//   - DeactivateChunk: destroys road mesh for deactivated chunk
// Mirror logic, interfaces, and all other subsystem calls: UNCHANGED.
// =============================================================================

using System;
using System.Collections;
using System.Collections.Generic;
using Mirror;
using ProceduralTerrain.Biomes;
using ProceduralTerrain.Chunks;
using ProceduralTerrain.Core;
using ProceduralTerrain.Debugging;
using ProceduralTerrain.Road;
using ProceduralTerrain.SafeZones;
using ProceduralTerrain.Spawning;
using UnityEngine;

namespace ProceduralTerrain
{
    public sealed class TerrainChunkManager : NetworkBehaviour
    {
        // ---- Inspector ------------------------------------------------------
        [Header("Configuration")]
        [SerializeField] private WorldSettings _worldSettings;
        [SerializeField] private Transform     _playerTransform;

        [Header("Debug")]
        [SerializeField] private bool _drawGizmos    = true;
        [SerializeField] private bool _verboseLogging = false;

        // ---- Mirror SyncVar -------------------------------------------------
        [SyncVar(hook = nameof(OnSeedChanged))]
        private int _syncedSeed;

        // ---- Subsystems -----------------------------------------------------
        private ITerrainLogger  _logger;
        private BiomeProvider   _biomeProvider;
        private RoadBuilder     _roadBuilder;
        private FoliageSpawner  _foliageSpawner;
        private POISpawner      _poiSpawner;
        private SafeZoneManager _safeZoneManager;

        // NEW [REQ-1] — Road mesh builder subsystem
        private RoadMeshBuilder _roadMeshBuilder;
        // END NEW

        // ---- Chunk tracking -------------------------------------------------
        private readonly Dictionary<Vector2Int, TerrainChunk> _activeChunks    = new();
        private readonly HashSet<Vector2Int>                   _queuedChunks    = new();
        private readonly Queue<Vector2Int>                     _generationQueue = new();

        private const string LOG_TAG = "TerrainChunkManager";

        // ---- Lifecycle ------------------------------------------------------

        private void Awake()
        {
            if (_worldSettings == null)
            {
                Debug.LogError($"[{LOG_TAG}] WorldSettings not assigned. Aborting.");
                enabled = false;
                return;
            }
            _logger = TerrainDebugLogger.Instance;
            if (_verboseLogging)
                ((TerrainDebugLogger)_logger).SetMinimumLevel(LogLevel.Verbose);

            _logger.Log(LogLevel.Info, LOG_TAG,
                $"TerrainChunkManager Awake. IsServer={isServer}, IsClient={isClient}");
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            _syncedSeed = _worldSettings.WorldSeed;
            _logger.Log(LogLevel.Info, LOG_TAG, $"Server started. World seed: {_syncedSeed}");
            InitializeSubsystems();
            StartCoroutine(ServerGenerationLoop());
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            _logger.Log(LogLevel.Info, LOG_TAG, $"Client started. Seed={_syncedSeed}");
            if (!isServer) InitializeSubsystems();
        }

        private void OnDestroy()
        {
            _logger.Log(LogLevel.Info, LOG_TAG, "TerrainChunkManager destroyed. Cleaning up...");
            UnloadAllChunks();
            _biomeProvider?.Dispose();
            _roadBuilder?.Dispose();
            _roadMeshBuilder?.Dispose(); // NEW [REQ-1]
        }

        // ---- Subsystem Initialization ---------------------------------------

        private void InitializeSubsystems()
        {
            using var scope = _logger.BeginTimed(LOG_TAG, "Subsystem initialization");

            _biomeProvider   = new BiomeProvider(_worldSettings, _logger);
            _roadBuilder     = new RoadBuilder(_worldSettings, _logger);
            _foliageSpawner  = new FoliageSpawner(_worldSettings, _logger, _roadBuilder);
            _poiSpawner      = new POISpawner(_worldSettings, _logger);
            _safeZoneManager = new SafeZoneManager(_worldSettings, _logger);

            // NEW [REQ-1] — RoadMeshBuilder instantiated here alongside other subsystems
            _roadMeshBuilder = new RoadMeshBuilder(_worldSettings, _logger);
            // END NEW

            _logger.Log(LogLevel.Info, LOG_TAG, "All subsystems initialized.");
        }

        // ---- Server: Main Generation Loop -----------------------------------

        [Server]
        private IEnumerator ServerGenerationLoop()
        {
            _logger.Log(LogLevel.Info, LOG_TAG, "Server generation loop started.");
            yield return StartCoroutine(GenerateInitialChunks());

            while (true)
            {
                if (_playerTransform != null)
                    yield return StartCoroutine(UpdateChunkStreaming());
                yield return new WaitForSeconds(0.5f);
            }
        }

        [Server]
        private IEnumerator GenerateInitialChunks()
        {
            using var scope = _logger.BeginTimed(LOG_TAG, "Initial chunk generation");
            int radius = _worldSettings.ViewDistanceChunks;
            for (int z = 0; z <= radius; z++)
            for (int x = -radius / 2; x <= radius / 2; x++)
                QueueChunk(new Vector2Int(x, z));
            yield return StartCoroutine(ProcessGenerationQueue());
        }

        [Server]
        private IEnumerator UpdateChunkStreaming()
        {
            Vector3 playerPos    = _playerTransform.position;
            int     playerChunkX = Mathf.FloorToInt(playerPos.x / _worldSettings.ChunkWorldSize);
            int     playerChunkZ = Mathf.FloorToInt(playerPos.z / _worldSettings.ChunkWorldSize);
            int     radius       = _worldSettings.ViewDistanceChunks;

            for (int dz = -1; dz <= radius; dz++)
            for (int dx = -radius / 2; dx <= radius / 2; dx++)
            {
                var coord = new Vector2Int(playerChunkX + dx, playerChunkZ + dz);
                if (!_activeChunks.ContainsKey(coord) && !_queuedChunks.Contains(coord))
                    QueueChunk(coord);
            }

            var toDeactivate = new List<Vector2Int>();
            foreach (var (coord, _) in _activeChunks)
            {
                if (Mathf.Abs(coord.x - playerChunkX) > radius + 1 ||
                    Mathf.Abs(coord.y - playerChunkZ) > radius + 2)
                    toDeactivate.Add(coord);
            }
            foreach (var coord in toDeactivate) DeactivateChunk(coord);

            yield return StartCoroutine(ProcessGenerationQueue());
        }

        // ---- Chunk Queue Processing -----------------------------------------

        private void QueueChunk(Vector2Int coord)
        {
            if (_activeChunks.ContainsKey(coord) || _queuedChunks.Contains(coord)) return;
            _queuedChunks.Add(coord);
            _generationQueue.Enqueue(coord);
            _logger.Log(LogLevel.Verbose, LOG_TAG,
                $"Chunk [{coord.x},{coord.y}] queued. Depth: {_generationQueue.Count}");
        }

        private IEnumerator ProcessGenerationQueue()
        {
            while (_generationQueue.Count > 0)
            {
                var coord = _generationQueue.Dequeue();
                _queuedChunks.Remove(coord);
                if (!_activeChunks.ContainsKey(coord))
                    yield return StartCoroutine(GenerateChunk(coord));
            }
        }

        // ---- Single Chunk Generation Pipeline -------------------------------

        private IEnumerator GenerateChunk(Vector2Int coord)
        {
            _logger.Log(LogLevel.Info, LOG_TAG,
                $"━━━ BEGIN Chunk [{coord.x},{coord.y}] generation ━━━");

            int localSeed = ChunkData.ComputeLocalSeed(_syncedSeed, coord);

            var chunkData = new ChunkData
            {
                Coord       = coord,
                Resolution  = _worldSettings.ChunkResolution,
                WorldSize   = _worldSettings.ChunkWorldSize,
                Seed        = localSeed,
                WorldOrigin = new Vector3(
                    coord.x * _worldSettings.ChunkWorldSize, 0f,
                    coord.y * _worldSettings.ChunkWorldSize),
                State = ChunkState.Queued
            };

            var chunk = new TerrainChunk(
                chunkData, _worldSettings, _logger, _biomeProvider, _roadBuilder);

            // ---- Road pathfinding (server only) ----
            if (isServer)
            {
                using var roadScope = _logger.BeginTimed(LOG_TAG,
                    $"Road pathfinding for chunk [{coord.x},{coord.y}]");

                float[] roughHeightmap = GenerateRoughHeightmap(coord,
                    _worldSettings.ChunkResolution, _worldSettings.ChunkWorldSize);

                _roadBuilder.ExtendRoadThroughChunk(
                    coord, roughHeightmap,
                    _worldSettings.ChunkResolution,
                    _worldSettings.ChunkWorldSize,
                    GetAverageHeightScale());

                _safeZoneManager.OnRoadExtended(_roadBuilder.TotalLength, _roadBuilder, _syncedSeed);

                var cpList  = _roadBuilder.ControlPoints;
                var cpArray = new Vector3[cpList.Count];
                for (int i = 0; i < cpList.Count; i++) cpArray[i] = cpList[i];
                RpcReceiveRoadControlPoints(cpArray);
            }

            // ---- Full async generation (heightmap → biome → splatmap → mesh) ----
            yield return StartCoroutine(chunk.GenerateAsync());

            _activeChunks[coord] = chunk;

            // NEW [REQ-1] — Build road mesh AFTER terrain generation completes.
            // At this point the heightmap has already been carved flat under the road,
            // so the mesh will sit flush on the levelled surface.
            // Runs on both server and client — each generates its own road mesh locally.
            {
                var splinePoints = _roadBuilder.GetSampledPointsForChunk(coord);

                if (splinePoints.Count >= 2)
                {
                    _logger.Log(LogLevel.Info, LOG_TAG,
                        $"Chunk [{coord.x},{coord.y}]: Building road mesh " +
                        $"({splinePoints.Count} spline samples)...");

                    _roadMeshBuilder.BuildChunkRoadMesh(coord, splinePoints, chunk.GameObject);
                }
                else
                {
                    _logger.Log(LogLevel.Verbose, LOG_TAG,
                        $"Chunk [{coord.x},{coord.y}]: No road segments in this chunk — mesh skipped.");
                }
            }
            // END NEW

            // ---- Server-only spawning ----
            if (isServer)
            {
                var exclusionZones = new List<Rect>();
                _foliageSpawner.SpawnFoliage(chunk.Data, chunk.UnityTerrain, exclusionZones);
                yield return null;

                _poiSpawner.SpawnPOIs(chunk.Data, _roadBuilder, _biomeProvider, _syncedSeed);
                yield return null;
            }

            _logger.Log(LogLevel.Info, LOG_TAG,
                $"━━━ END Chunk [{coord.x},{coord.y}] generation — State={chunk.State} ━━━");
        }

        // ---- Client-side: receive road data ---------------------------------

        [ClientRpc]
        private void RpcReceiveRoadControlPoints(Vector3[] controlPoints)
        {
            if (isServer) return;
            _logger.Log(LogLevel.Info, LOG_TAG,
                $"[CLIENT] Received {controlPoints.Length} road control points from server.");
        }

        // ---- Mirror SyncVar hook --------------------------------------------

        private void OnSeedChanged(int oldSeed, int newSeed)
        {
            _logger.Log(LogLevel.Info, LOG_TAG, $"World seed synced: {oldSeed} → {newSeed}");
        }

        // ---- Chunk Deactivation / Cleanup -----------------------------------

        private void DeactivateChunk(Vector2Int coord)
        {
            if (!_activeChunks.TryGetValue(coord, out var chunk)) return;
            chunk.Deactivate();
            // NEW [REQ-1] — Also destroy the road mesh when chunk deactivates
            _roadMeshBuilder.DestroyChunkMesh(coord);
            // END NEW
        }

        private void UnloadAllChunks()
        {
            foreach (var chunk in _activeChunks.Values) chunk.Unload();
            _activeChunks.Clear();
            _queuedChunks.Clear();
            _generationQueue.Clear();
        }

        // ---- Utility --------------------------------------------------------

        private float[] GenerateRoughHeightmap(Vector2Int coord, int resolution, float worldSize)
        {
            float[] hm   = new float[resolution * resolution];
            var     biome = _worldSettings.BiomeDefinitions.Length > 0
                ? _worldSettings.BiomeDefinitions[0] : null;
            float freq  = biome?.HeightFrequency ?? 0.003f;
            float ox    = coord.x * worldSize;
            float oz    = coord.y * worldSize;

            for (int z = 0; z < resolution; z++)
            for (int x = 0; x < resolution; x++)
            {
                float wx = ox + (float)x / (resolution - 1) * worldSize;
                float wz = oz + (float)z / (resolution - 1) * worldSize;
                hm[z * resolution + x] = Mathf.PerlinNoise(
                    wx * freq + _syncedSeed * 0.1f,
                    wz * freq + _syncedSeed * 0.1f);
            }
            return hm;
        }

        private float GetAverageHeightScale()
        {
            if (_worldSettings.BiomeDefinitions.Length == 0) return 50f;
            float t = 0f;
            foreach (var b in _worldSettings.BiomeDefinitions) t += b.HeightScale;
            return t / _worldSettings.BiomeDefinitions.Length;
        }

        // ---- Gizmos ---------------------------------------------------------

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!_drawGizmos || _roadBuilder == null) return;
            DrawRoadSplineGizmo();
            DrawChunkBoundaryGizmos();
            DrawSafeZoneGizmos();
            DrawBiomeBoundaryGizmos();
        }

        private void DrawRoadSplineGizmo()
        {
            if (_roadBuilder.ControlPoints.Count < 4) return;
            Gizmos.color = Color.yellow;
            const int steps = 200;
            Vector3 prev = _roadBuilder.ControlPoints[0];
            for (int i = 1; i <= steps; i++)
            {
                float t = (float)i / steps;
                _roadBuilder.SampleSpline(t, out Vector3 pos, out _);
                Gizmos.DrawLine(prev, pos);
                prev = pos;
            }

            Gizmos.color = Color.cyan;
            foreach (var cp in _roadBuilder.ControlPoints) Gizmos.DrawSphere(cp, 1.5f);

            // NEW [REQ-1] — Draw total exclusion radius (road + shoulder + buffer)
            float totalExclusion = (_worldSettings?.RoadWidth ?? 8f) * 0.5f +
                                   (_worldSettings?.RoadShoulderWidth ?? 4f) +
                                   (_worldSettings?.FoliageExclusionBuffer ?? 4f);
            Gizmos.color = new Color(1f, 0.3f, 0f, 0.15f); // Orange — foliage exclusion
            const int corridorSteps = 50;
            for (int i = 0; i < corridorSteps; i++)
            {
                float t = (float)i / corridorSteps;
                _roadBuilder.SampleSpline(t, out Vector3 pos, out Vector3 fwd);
                Vector3 right = Vector3.Cross(fwd, Vector3.up).normalized;
                Gizmos.DrawLine(pos - right * totalExclusion, pos + right * totalExclusion);
            }
            // END NEW
        }

        private void DrawChunkBoundaryGizmos()
        {
            float ws = _worldSettings?.ChunkWorldSize ?? 200f;
            Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.3f);
            foreach (var (coord, chunk) in _activeChunks)
            {
                Vector3 origin = new Vector3(coord.x * ws, 0f, coord.y * ws);
                Vector3 size   = new Vector3(ws, 1f, ws);
                Gizmos.DrawWireCube(origin + size * 0.5f, size);
                UnityEditor.Handles.Label(
                    origin + new Vector3(ws * 0.5f, 5f, ws * 0.5f),
                    $"[{coord.x},{coord.y}]\n{chunk.State}",
                    new GUIStyle { normal = { textColor = Color.cyan }, fontSize = 10 });
            }
        }

        private void DrawSafeZoneGizmos()
        {
            if (_safeZoneManager == null) return;
            Gizmos.color = Color.green;
            foreach (var pos in _safeZoneManager.SpawnedPositions)
                Gizmos.DrawWireSphere(pos, 10f);
        }

        private void DrawBiomeBoundaryGizmos()
        {
            if (_biomeProvider == null || _worldSettings == null) return;
            float ws   = _worldSettings.ChunkWorldSize;
            float step = ws * 0.1f;
            foreach (var (coord, _) in _activeChunks)
            {
                float ox = coord.x * ws;
                float oz = coord.y * ws;
                for (float x = ox; x < ox + ws; x += step)
                for (float z = oz; z < oz + ws; z += step)
                {
                    int biome  = _biomeProvider.GetDominantBiome(x, z);
                    Gizmos.color = biome == 0
                        ? new Color(1f, 0.9f, 0.3f, 0.15f)
                        : new Color(0.2f, 0.8f, 0.2f, 0.15f);
                    float terrainY = Terrain.activeTerrain != null
                        ? Terrain.activeTerrain.SampleHeight(new Vector3(x, 0, z)) + 0.5f : 0.5f;
                    Gizmos.DrawCube(new Vector3(x, terrainY, z), new Vector3(step, 0.3f, step));
                }
            }
        }
#endif
    }
}
