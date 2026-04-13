// =============================================================================
// FILE 3: TerrainDataStructures.cs
// Pure data: structs, enums, and ScriptableObject definitions.
// No MonoBehaviour behaviour. No logic. "S" in SOLID for data containers.
// =============================================================================

using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace ProceduralTerrain.Core
{
    // =========================================================================
    // Enums
    // =========================================================================

    public enum ChunkState
    {
        Uninitialized,
        Queued,
        GeneratingHeightmap,
        GeneratingSplatmap,
        CarveRoad,
        BuildingMesh,
        SpawningFoliage,
        SpawningPOIs,
        Active,
        Deactivated,
        Unloaded
    }

    public enum BiomeType
    {
        Desert      = 0,
        GreenPlains = 1,
        // Extend as needed — system auto-detects from BiomeDefinition list
    }

    // =========================================================================
    // Plain structs (Burst-compatible where possible)
    // =========================================================================

    /// <summary>
    /// All static data that describes one chunk.
    /// NativeArrays are NOT owned here — the owning system disposes them.
    /// </summary>
    public struct ChunkData
    {
        public Vector2Int  Coord;           // Grid coordinate (integer)
        public int         Resolution;      // Heightmap samples per side
        public float       WorldSize;       // Real-world side length (e.g. 200 m)
        public int         Seed;            // Chunk-local seed derived from world seed
        public Vector3     WorldOrigin;     // Bottom-left corner in world space
        public ChunkState  State;

        /// <summary>Heightmap samples — row-major, Y-up.</summary>
        public NativeArray<float> Heightmap;

        /// <summary>
        /// Splatmap channel weights per vertex — layout: [v * numLayers + layer].
        /// </summary>
        public NativeArray<float> Splatmap;

        /// <summary>Cached biome weights per vertex — [v * numBiomes + biome].</summary>
        public NativeArray<float> BiomeWeights;

        /// <summary>Derive a chunk-local seed from the world seed and coordinates.</summary>
        public static int ComputeLocalSeed(int worldSeed, Vector2Int coord) =>
            worldSeed ^ (coord.x * 73856093) ^ (coord.y * 19349663);

        /// <summary>World-space AABB of this chunk (ignores height).</summary>
        public Rect WorldRect => new Rect(WorldOrigin.x, WorldOrigin.z, WorldSize, WorldSize);
    }

    // -------------------------------------------------------------------------
    // Road spline sample — Burst-safe
    // -------------------------------------------------------------------------
    public struct SplinePoint
    {
        public float3 Position;
        public float3 Tangent;
        public float3 Normal;    // Surface normal at this point
        public float  DistanceAlongSpline; // Arc-length from start
        public float  RoadWidth;
    }

    // -------------------------------------------------------------------------
    // Foliage placement — used during job scheduling
    // -------------------------------------------------------------------------
    public struct FoliagePlacementData
    {
        public float3 WorldPosition;
        public float  Rotation;      // Y-axis rotation in radians
        public float  Scale;
        public int    PrefabIndex;   // Index into biome's foliage array
    }

    // -------------------------------------------------------------------------
    // POI request — queued from the spawner job, executed on main thread
    // -------------------------------------------------------------------------
    public struct POISpawnRequest
    {
        public float3 WorldPosition;
        public float  YRotation;     // Degrees
        public int    PrefabIndex;
        public bool   IsSafeZone;
    }

    // -------------------------------------------------------------------------
    // Road corridor — used for exclusion-zone computations
    // -------------------------------------------------------------------------
    public struct RoadCorridorSample
    {
        public float3 Center;
        public float  HalfWidth;
        public float3 Right;         // Unit vector perpendicular to road

        public bool Contains(float3 point)
        {
            float3 delta = point - Center;
            float  side  = math.dot(delta, Right);
            return math.abs(side) <= HalfWidth;
        }
    }
}
