// =============================================================================
// FILE 2: TerrainInterfaces.cs
// All public-facing contracts used throughout the system.
// Follows Interface Segregation Principle — small, focused interfaces.
// =============================================================================

using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace ProceduralTerrain.Core
{
    // =========================================================================
    // Terrain Generation
    // =========================================================================

    /// <summary>
    /// Generates a raw heightmap (and optional normal map) for a single chunk.
    /// Implementation lives in TerrainGeneratorJob + TerrainGenerator service.
    /// </summary>
    public interface ITerrainGenerator
    {
        /// <summary>
        /// Schedules and completes the heightmap generation for a chunk.
        /// Writes into the supplied NativeArray (caller owns the allocation).
        /// </summary>
        /// <param name="chunkCoord">Grid coordinate of the chunk.</param>
        /// <param name="resolution">Number of height samples per side.</param>
        /// <param name="seed">Global world seed (deterministic).</param>
        /// <param name="heightmap">Output — must be pre-allocated to resolution*resolution.</param>
        void GenerateHeightmap(Vector2Int chunkCoord, int resolution, int seed,
                               ref NativeArray<float> heightmap);

        /// <summary>Generates a splatmap (texture weights) for the chunk.</summary>
        void GenerateSplatmap(Vector2Int chunkCoord, int resolution, int seed,
                              ref NativeArray<float> splatmap, int numLayers);
    }

    // =========================================================================
    // Road Building
    // =========================================================================

    /// <summary>
    /// Builds and manages the main road spline across the world.
    /// Clients receive the control-point list from the server; road mesh is built locally.
    /// </summary>
    public interface IRoadBuilder
    {
        /// <summary>Returns all spline control points in world space.</summary>
        IReadOnlyList<Vector3> GetControlPoints();

        /// <summary>
        /// Given a chunk coordinate, returns the subset of control points whose
        /// influence intersects that chunk. Used for heightmap carving.
        /// </summary>
        IReadOnlyList<Vector3> GetControlPointsForChunk(Vector2Int chunkCoord);

        /// <summary>
        /// Carves the road corridor into a heightmap NativeArray.
        /// Flattens terrain beneath the road and blends shoulders.
        /// </summary>
        void CarveRoadIntoHeightmap(Vector2Int chunkCoord, int resolution,
                                    ref NativeArray<float> heightmap);

        /// <summary>
        /// Samples the spline at parameter t ∈ [0, 1].
        /// Returns world-space position and forward direction.
        /// </summary>
        void SampleSpline(float t, out Vector3 position, out Vector3 forward);

        /// <summary>Total arc-length of the road spline in world units.</summary>
        float TotalLength { get; }

        /// <summary>Nearest point on the spline to a world-space position.</summary>
        Vector3 NearestPointOnSpline(Vector3 worldPos, out float distanceAlongSpline);
    }

    // =========================================================================
    // Biome System
    // =========================================================================

    /// <summary>
    /// Provides biome information at any world-space coordinate.
    /// The implementation uses a secondary noise map (moisture / temperature).
    /// </summary>
    public interface IBiomeProvider
    {
        /// <summary>
        /// Returns a normalized blend weight array for all registered biomes
        /// at the given world XZ position. Sum of weights == 1.
        /// </summary>
        float[] GetBiomeWeights(float worldX, float worldZ);

        /// <summary>Returns the dominant biome index at a position.</summary>
        int GetDominantBiome(float worldX, float worldZ);

        /// <summary>All biome definitions registered with this provider.</summary>
        IReadOnlyList<BiomeDefinition> Biomes { get; }
    }

    // =========================================================================
    // Foliage Spawning
    // =========================================================================

    /// <summary>
    /// Populates a chunk with trees, bushes, and grass details.
    /// Must respect road corridors and POI footprints (exclusion zones).
    /// </summary>
    public interface IFoliageSpawner
    {
        /// <summary>
        /// Generates foliage placements for a chunk and applies them
        /// to the supplied UnityEngine.Terrain object.
        /// </summary>
        void SpawnFoliage(ChunkData chunk, Terrain terrain,
                          IReadOnlyList<Rect> exclusionZones);
    }

    // =========================================================================
    // POI Spawning
    // =========================================================================

    /// <summary>
    /// Handles placement and network-spawning of road-side points of interest
    /// (houses, ruins, gas stations, etc.).
    /// </summary>
    public interface IPOISpawner
    {
        /// <summary>
        /// Calculates candidate POI positions along the road for a chunk,
        /// then calls NetworkServer.Spawn on each selected prefab.
        /// Must only be called on the server.
        /// </summary>
        void SpawnPOIs(ChunkData chunk, IRoadBuilder roadBuilder,
                       IBiomeProvider biomeProvider, int seed);
    }

    // =========================================================================
    // Safe Zone
    // =========================================================================

    /// <summary>
    /// Tracks distance driven along the road and triggers Safe Zone spawning
    /// at fixed intervals.
    /// </summary>
    public interface ISafeZoneManager
    {
        /// <summary>Interval in world-space meters between Safe Zones.</summary>
        float SpawnIntervalMeters { get; }

        /// <summary>
        /// Called every time a new road segment is committed.
        /// Internally checks whether a new Safe Zone threshold has been crossed.
        /// </summary>
        void OnRoadExtended(float newTotalLength, IRoadBuilder roadBuilder,
                            int worldSeed);
    }

    // =========================================================================
    // Chunk Lifecycle
    // =========================================================================

    /// <summary>
    /// Manages the lifecycle (generate → build mesh → activate → unload)
    /// of a single terrain chunk.
    /// </summary>
    public interface IChunkLifecycle
    {
        ChunkState State { get; }
        void BeginGeneration();
        void BuildMesh();
        void Activate();
        void Deactivate();
        void Unload();
    }
}
