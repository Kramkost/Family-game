// =============================================================================
// FILE 12: ARCHITECTURE_README.cs
// This file is not compiled — it is a documentation artifact.
// Rename to .txt or open as a text file in your IDE.
// =============================================================================

/*
╔══════════════════════════════════════════════════════════════════════════════╗
║          PROCEDURAL TERRAIN SYSTEM — PACIFIC DRIVE STYLE                   ║
║          Architecture Reference & Integration Guide                         ║
╚══════════════════════════════════════════════════════════════════════════════╝

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
FILE STRUCTURE
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

  1_TerrainDebugLogger.cs         → ITerrainLogger + StopwatchScope
  2_TerrainInterfaces.cs          → All public contracts (ISP-compliant)
  3_TerrainDataStructures.cs      → Pure data: structs, enums, ScriptableObjects
  4_TerrainJobs.cs                → [BurstCompile] IJobParallelFor jobs
  5_SplineMath.cs                 → CatmullRomSpline + RoadPathfinder (A*)
  6_BiomeSystem.cs                → BiomeProvider (IBiomeProvider)
  7_RoadBuilder.cs                → RoadBuilder (IRoadBuilder)
  8_TerrainChunk.cs               → TerrainChunk (IChunkLifecycle)
  9_FoliageAndPOISpawner.cs       → FoliageSpawner + POISpawner
  10_SafeZoneManager.cs           → SafeZoneManager (ISafeZoneManager)
  11_TerrainChunkManager.cs       → Main NetworkBehaviour (Mirror)

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
DEPENDENCY GRAPH (arrows = "depends on")
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

  TerrainChunkManager
      ├── BiomeProvider     ← WorldSettings, TerrainJobs (BiomeWeightJob)
      ├── RoadBuilder       ← WorldSettings, SplineMath, TerrainJobs (RoadCarvingJob)
      │       └── CatmullRomSpline
      │       └── RoadPathfinder (A*)
      ├── TerrainChunk      ← BiomeProvider, RoadBuilder, TerrainJobs
      ├── FoliageSpawner    ← WorldSettings, RoadBuilder, TerrainJobs (FoliageCandidateJob)
      ├── POISpawner        ← WorldSettings, Mirror (NetworkServer.Spawn)
      └── SafeZoneManager   ← WorldSettings, Mirror (NetworkServer.Spawn)

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
SOLID PRINCIPLE MAPPING
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

  S — Single Responsibility
      Each class has exactly one reason to change:
      · RoadPathfinder: only road routing logic
      · BiomeProvider:  only biome weight calculations
      · SafeZoneManager: only safe zone distance tracking

  O — Open/Closed
      · New biome types: add BiomeDefinition ScriptableObject — zero code changes
      · New POI types:   add prefab to BiomeDefinition.HousePrefabs — zero code changes
      · New jobs:        implement IJobParallelFor, pass to existing pipeline

  L — Liskov Substitution
      · All ITerrainGenerator implementors are interchangeable
      · IBiomeProvider can be swapped (e.g., editor-time preview biome provider)

  I — Interface Segregation
      · ITerrainGenerator (heightmap only)
      · IRoadBuilder      (road spline only)
      · IBiomeProvider    (biome weights only)
      · IFoliageSpawner   (foliage only)
      · IPOISpawner       (POI only)
      · ISafeZoneManager  (safe zones only)
      · IChunkLifecycle   (chunk state machine only)

  D — Dependency Inversion
      · TerrainChunkManager depends on interfaces, not concrete types
      · All subsystems injected via constructor (manual DI — no framework needed)

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
BURST JOB PIPELINE (per chunk)
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

  Frame N:   BiomeWeightJob.Schedule()           → NativeArray<float> biomeWeights
  Frame N+?: BiomeHeightBlendJob.Schedule()      → NativeArray<float> heightmap
             RoadCarvingJob.Schedule()           → modifies heightmap (road flat)
             SplatmapGenerationJob.Schedule()    → NativeArray<float> splatmap
  Main thread: SetHeights(), SetAlphamaps()      → commits to UnityEngine.TerrainData
  Frame N+?: FoliageCandidateJob.Schedule()      → NativeArray<FoliagePlacement>
  Main thread: TreeInstance[], DetailLayer apply

  All jobs use Allocator.TempJob or Allocator.Persistent.
  Persistent arrays are disposed in TerrainChunk.Dispose().

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
MIRROR NETWORK AUTHORITY MODEL
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

  SERVER responsibilities:
    · Owns _syncedSeed (SyncVar)
    · Calls RoadBuilder.ExtendRoadThroughChunk()
    · Calls SafeZoneManager.OnRoadExtended()
    · NetworkServer.Spawn(housePrefab, conn)
    · NetworkServer.Spawn(safeZonePrefab, conn)
    · ClientRpc → sends road control points to all clients

  CLIENT responsibilities:
    · Receives seed via SyncVar OnSeedChanged hook
    · Receives road control points via ClientRpc
    · Rebuilds CatmullRomSpline locally (same seed → deterministic)
    · Generates terrain mesh locally (saves massive bandwidth)
    · Receives spawned NetworkIdentity objects automatically via Mirror

  BANDWIDTH SAVED:
    · Heightmap (129*129 * 4 bytes) = ~66 KB per chunk — NOT sent
    · Only control points (≈30 Vector3 per chunk = 360 bytes) are sent
    · NetworkIdentity objects (houses, safe zones) are Mirror-managed

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
UNITY SETUP CHECKLIST
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

  Packages required:
    [ ] com.unity.jobs                 (C# Job System)
    [ ] com.unity.burst                (Burst Compiler)
    [ ] com.unity.collections         (NativeArray, NativeList)
    [ ] com.unity.mathematics          (Unity.Mathematics)
    [ ] Mirror Networking              (via OpenUPM or .unitypackage)

  Assembly Definition (ProceduralTerrain.asmdef):
    References:
      · Unity.Jobs
      · Unity.Burst
      · Unity.Collections
      · Unity.Mathematics
      · Mirror

  WorldSettings ScriptableObject:
    Create → Assets → Right-click → Create → ProceduralTerrain → World Settings
    Assign:
      · WorldSeed, ChunkResolution (129), ChunkWorldSize (200)
      · BiomeDefinitions[] — create Desert + GreenPlains BiomeDef assets
      · TerrainLayers[] — assign your TerrainLayer assets
      · SafeZonePrefab — must have NetworkIdentity component
      · RoadWidth, SafeZoneIntervalMeters

  BiomeDefinition ScriptableObjects (one per biome):
    Create → Assets → Right-click → Create → ProceduralTerrain → Biome Definition
    Desert:       MoistureMin=0, MoistureMax=0.4, TemperatureMin=0.6, TemperatureMax=1.0
    GreenPlains:  MoistureMin=0.4, MoistureMax=1.0, TemperatureMin=0, TemperatureMax=0.7

  House/POI Prefabs:
    · Must have NetworkIdentity component
    · Register in Mirror's NetworkManager → Registered Spawnable Prefabs list
    · Assign to BiomeDefinition.HousePrefabs[]

  Layer Setup:
    · Create a "Terrain" physics layer
    · Assign all Terrain GameObjects to this layer
    · Required for POI/SafeZone terrain height sampling via Physics.Raycast

  TerrainChunkManager GameObject:
    · Add TerrainChunkManager component
    · Assign WorldSettings
    · Assign PlayerTransform (or set at runtime)
    · Must also have a NetworkIdentity component (Mirror requirement)
    · Add to NetworkManager → Auto-create Objects

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
DEBUG LOGGING EXAMPLES (what you'll see in Console)
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

  [Info]  [TerrainChunkManager] ━━━ BEGIN Chunk [0,0] generation ━━━
  [Info]  [RoadPathfinder] Pathfinding from (100,0) to (100,200) | grid=129x129
  [Info]  [RoadPathfinder] Path found after 847 iterations.
  [Info]  [RoadBuilder] Chunk [0,0]: Road extended +12 control points. Total: 218.3 m  (4.21 ms)
  [Info]  [TerrainChunk] Chunk [0,0]: Biome weights computed.  (1.83 ms)
  [Info]  [TerrainChunk] Chunk [0,0]: Heightmap generated.     (6.47 ms)
  [Info]  [RoadBuilder] Chunk [0,0]: Road carved into heightmap. (2.11 ms)
  [Info]  [TerrainChunk] Chunk [0,0]: Splatmap generated.      (3.95 ms)
  [Info]  [TerrainChunk] Chunk [0,0]: ✅ Generation COMPLETE. State → Active
  [Info]  [FoliageSpawner] Chunk [0,0]: Spawned 187 foliage instances (from 512 candidates). (8.33 ms)
  [Info]  [POISpawner] Chunk [0,0]: Spawning 'HousePrefab_A' at (112.4, 2.1, 95.7) ...
  [Info]  [POISpawner] Chunk [0,0]: 4 POI(s) spawned.
  [Warn]  [RoadPathfinder] A* exhausted 50000 iterations. Using straight-line fallback.
  [Info]  [SafeZoneManager] ▶ Spawning SafeZone_1 at distance 1000.0 m along road.
  [Info]  [SafeZoneManager] ✅ SafeZone_1 spawned and registered with Mirror NetworkServer.

*/
