// =============================================================================
// FILE 8: TerrainChunk.cs
// Manages a single chunk's Unity Terrain object: lifecycle, job scheduling,
// mesh commit, splatmap upload.
// Follows IChunkLifecycle + logs every state transition.
// =============================================================================

using System;
using System.Collections;
using ProceduralTerrain.Biomes;
using ProceduralTerrain.Core;
using ProceduralTerrain.Debugging;
using ProceduralTerrain.Jobs;
using ProceduralTerrain.Road;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace ProceduralTerrain.Chunks
{
    /// <summary>
    /// Represents and manages a single terrain chunk.
    /// Chunk owns its NativeArrays and disposes them on Unload.
    /// All heavy work (heightmap, biome weights, road carving) is scheduled
    /// as Burst jobs; the coroutine yields until they complete.
    /// </summary>
    public sealed class TerrainChunk : IChunkLifecycle, IDisposable
    {
        // ---- Identity -------------------------------------------------------
        public  ChunkData         Data         { get; private set; }
        public  ChunkState        State        => Data.State;
        public  Terrain           UnityTerrain { get; private set; }
        public  TerrainData       TerrainData  { get; private set; }
        public  GameObject        GameObject   { get; private set; }

        // ---- Dependencies ---------------------------------------------------
        private readonly WorldSettings  _settings;
        private readonly ITerrainLogger _logger;
        private readonly BiomeProvider  _biomeProvider;
        private readonly RoadBuilder    _roadBuilder;

        private const string LOG_TAG = "TerrainChunk";

        // ---- Native memory (owned by this chunk) ----------------------------
        private NativeArray<float>  _heightmapNA;
        private NativeArray<float>  _splatmapNA;
        private NativeArray<float>  _biomeWeightsNA;
        private NativeArray<float>  _tempBiomeHeightmapsNA;

        // ---- Constructor ---------------------------------------------------

        public TerrainChunk(
            ChunkData data,
            WorldSettings settings,
            ITerrainLogger logger,
            BiomeProvider biomeProvider,
            RoadBuilder roadBuilder)
        {
            Data          = data;
            _settings     = settings;
            _logger       = logger;
            _biomeProvider= biomeProvider;
            _roadBuilder  = roadBuilder;
        }

        // ---- IChunkLifecycle ------------------------------------------------

        public void BeginGeneration()
        {
            SetState(ChunkState.Queued);
            _logger.Log(LogLevel.Info, LOG_TAG,
                $"Chunk [{Data.Coord.x},{Data.Coord.y}]: Generation queued.");
        }

        /// <summary>
        /// Full async generation pipeline.
        /// Must be driven as a Unity Coroutine (yield after each job batch).
        /// </summary>
        public IEnumerator GenerateAsync()
        {
            int res       = _settings.ChunkResolution;
            int vertCount = res * res;
            int numBiomes = _biomeProvider.Biomes.Count;
            int numLayers = _settings.TerrainLayers?.Length ?? 1;

            // ---- Allocate NativeArrays --------------------------------------
            _heightmapNA       = new NativeArray<float>(vertCount, Allocator.Persistent);
            _splatmapNA        = new NativeArray<float>(vertCount * numLayers, Allocator.Persistent);
            _biomeWeightsNA    = new NativeArray<float>(vertCount * numBiomes, Allocator.Persistent);

            // ================================================================
            // STEP 1: Biome Weights
            // ================================================================
            SetState(ChunkState.GeneratingHeightmap);
            var sw1 = System.Diagnostics.Stopwatch.StartNew();

            var biomeHandle = _biomeProvider.ScheduleBiomeWeightJob(
                Data.Coord, res, _settings.ChunkWorldSize, _biomeWeightsNA);

            // Yield until complete (we check every frame to avoid blocking)
            while (!biomeHandle.IsCompleted) yield return null;
            biomeHandle.Complete();
            sw1.Stop();

            _logger.Log(LogLevel.Info, LOG_TAG,
                $"Chunk [{Data.Coord.x},{Data.Coord.y}]: Biome weights computed.",
                sw1.Elapsed.TotalMilliseconds);

            // ================================================================
            // STEP 2: Blended Heightmap (per-biome heights weighted together)
            // ================================================================
            var sw2 = System.Diagnostics.Stopwatch.StartNew();

            var heightHandle = _biomeProvider.ScheduleBlendedHeightmapJob(
                Data.Coord, res, _settings.ChunkWorldSize,
                _biomeWeightsNA, _heightmapNA,
                out _tempBiomeHeightmapsNA, biomeHandle);

            while (!heightHandle.IsCompleted) yield return null;
            heightHandle.Complete();
            _tempBiomeHeightmapsNA.Dispose(); // Temp only needed during job
            sw2.Stop();

            _logger.Log(LogLevel.Info, LOG_TAG,
                $"Chunk [{Data.Coord.x},{Data.Coord.y}]: Heightmap generated.",
                sw2.Elapsed.TotalMilliseconds);

            // ================================================================
            // STEP 3: Road Carving into Heightmap
            // ================================================================
            SetState(ChunkState.CarveRoad);
            var sw3 = System.Diagnostics.Stopwatch.StartNew();

            // RoadBuilder.ExtendRoad must be called before this (from TerrainManager)
            _roadBuilder.CarveRoadIntoHeightmap(Data.Coord, res, ref _heightmapNA);
            sw3.Stop();

            _logger.Log(LogLevel.Info, LOG_TAG,
                $"Chunk [{Data.Coord.x},{Data.Coord.y}]: Road carved into heightmap.",
                sw3.Elapsed.TotalMilliseconds);

            // ================================================================
            // STEP 4: Splatmap Generation
            // ================================================================
            SetState(ChunkState.GeneratingSplatmap);
            var sw4 = System.Diagnostics.Stopwatch.StartNew();

            // Prepare biome splatmap layer indices
// Prepare biome splatmap layer indices
            var biomeLayerPairs = new NativeArray<Unity.Mathematics.int2>(
                numBiomes, Allocator.TempJob);
            for (int b = 0; b < numBiomes; b++)
            {
                var biome = _biomeProvider.Biomes[b];
                biomeLayerPairs[b] = new Unity.Mathematics.int2(
                    biome.PrimarySplatLayer, biome.SecondarySplatLayer);
            }

            float avgHeightScale = GetAverageHeightScale();
            
            // ИСПРАВЛЕНИЕ ЗДЕСЬ: Используем Data.Coord
            var splineList = _roadBuilder.GetSampledPointsForChunk(Data.Coord);
            var nativeSplines = new NativeArray<Unity.Mathematics.float3>(splineList.Count, Allocator.TempJob);
            for (int i = 0; i < splineList.Count; i++) 
            {
               nativeSplines[i] = splineList[i].Position;
            }

            var splatJob = new SplatmapGenerationJob
            {
                Resolution        = res,
                // ИСПРАВЛЕНИЕ ЗДЕСЬ: Передаем WorldSize и ChunkOriginXZ
                WorldSize         = _settings.ChunkWorldSize,
                ChunkOriginXZ     = new Unity.Mathematics.float2(Data.Coord.x * _settings.ChunkWorldSize, Data.Coord.y * _settings.ChunkWorldSize),
                RoadHalfWidth     = _settings.RoadWidth * 0.5f,
                ShoulderHalfWidth = _settings.RoadShoulderWidth,
                SplinePoints      = nativeSplines,
                NumLayers         = numLayers,
                NumBiomes         = numBiomes,
                HeightScale       = avgHeightScale,
                Heightmap         = _heightmapNA,
                BiomeWeights      = _biomeWeightsNA,
                BiomeSplatLayers  = biomeLayerPairs,
                Splatmap          = _splatmapNA
            };

            var splatHandle = splatJob.Schedule(vertCount, 64);
            while (!splatHandle.IsCompleted) yield return null;
            splatHandle.Complete();
            
            nativeSplines.Dispose();
            biomeLayerPairs.Dispose();
            sw4.Stop();

            _logger.Log(LogLevel.Info, LOG_TAG,
                $"Chunk [{Data.Coord.x},{Data.Coord.y}]: Splatmap generated.",
                sw4.Elapsed.TotalMilliseconds);

            // ================================================================
            // STEP 5: Build Unity Terrain Mesh (main thread)
            // ================================================================
            SetState(ChunkState.BuildingMesh);
            BuildMesh();

            yield return null; // One frame for terrain to initialize

            // ================================================================
            // STEP 6: Upload splatmap to TerrainData
            // ================================================================
            ApplySplatmap(numLayers);

            // Update chunk data state
            var d = Data;
            d.Heightmap    = _heightmapNA;
            d.Splatmap     = _splatmapNA;
            d.BiomeWeights = _biomeWeightsNA;
            Data = d;

            _logger.Log(LogLevel.Info, LOG_TAG,
                $"Chunk [{Data.Coord.x},{Data.Coord.y}]: ✅ Generation COMPLETE. " +
                $"State → {ChunkState.Active}");

            SetState(ChunkState.Active);
        }

        // ---- IChunkLifecycle: sync variants ---------------------------------

        public void BuildMesh()
        {
            using var scope = _logger.BeginTimed(LOG_TAG,
                $"Chunk [{Data.Coord.x},{Data.Coord.y}] mesh build");

            // Create TerrainData
            TerrainData = new TerrainData();
            int res = _settings.ChunkResolution;
            TerrainData.heightmapResolution = res;
            TerrainData.size = new Vector3(_settings.ChunkWorldSize,
                                           GetAverageHeightScale(),
                                           _settings.ChunkWorldSize);

            // Apply TerrainLayers
            if (_settings.TerrainLayers != null && _settings.TerrainLayers.Length > 0)
                TerrainData.terrainLayers = _settings.TerrainLayers;

            // Upload heightmap (NativeArray → float[,])
            float[,] heights = new float[res, res];
            for (int z = 0; z < res; z++)
            for (int x = 0; x < res; x++)
                heights[z, x] = _heightmapNA[z * res + x];

            TerrainData.SetHeights(0, 0, heights);

            // Create Terrain GameObject
            Vector3 origin = new Vector3(
                Data.Coord.x * _settings.ChunkWorldSize, 0f,
                Data.Coord.y * _settings.ChunkWorldSize);

            GameObject = Terrain.CreateTerrainGameObject(TerrainData);
            GameObject.name = $"Chunk_{Data.Coord.x}_{Data.Coord.y}";
            UnityTerrain = GameObject.GetComponent<Terrain>();
            UnityTerrain.transform.position = origin;
            UnityTerrain.drawInstanced  = true;
            UnityTerrain.heightmapPixelError = 5f;
        }

        public void Activate()
        {
            if (GameObject != null) GameObject.SetActive(true);
            SetState(ChunkState.Active);
            _logger.Log(LogLevel.Verbose, LOG_TAG,
                $"Chunk [{Data.Coord.x},{Data.Coord.y}]: Activated.");
        }

        public void Deactivate()
        {
            if (GameObject != null) GameObject.SetActive(false);
            SetState(ChunkState.Deactivated);
            _logger.Log(LogLevel.Verbose, LOG_TAG,
                $"Chunk [{Data.Coord.x},{Data.Coord.y}]: Deactivated.");
        }

        public void Unload()
        {
            _logger.Log(LogLevel.Info, LOG_TAG,
                $"Chunk [{Data.Coord.x},{Data.Coord.y}]: Unloading...");
            Dispose();
            if (GameObject != null) UnityEngine.Object.Destroy(GameObject);
            SetState(ChunkState.Unloaded);
        }

        // ---- Private helpers ------------------------------------------------

        private void ApplySplatmap(int numLayers)
        {
            if (TerrainData == null) return;
            int res = _settings.ChunkResolution;

            // Unity expects alphamaps as [z, x, layer]
            float[,,] alphamap = new float[res, res, numLayers];
            for (int z = 0; z < res; z++)
            for (int x = 0; x < res; x++)
            for (int l = 0; l < numLayers; l++)
                alphamap[z, x, l] = _splatmapNA[(z * res + x) * numLayers + l];

            TerrainData.SetAlphamaps(0, 0, alphamap);

            _logger.Log(LogLevel.Verbose, LOG_TAG,
                $"Chunk [{Data.Coord.x},{Data.Coord.y}]: Splatmap uploaded ({numLayers} layers).");
        }

        private float GetAverageHeightScale()
        {
            if (_biomeProvider.Biomes.Count == 0) return 50f;
            float total = 0f;
            foreach (var b in _biomeProvider.Biomes) total += b.HeightScale;
            return total / _biomeProvider.Biomes.Count;
        }

        private void SetState(ChunkState state)
        {
            var d   = Data;
            d.State = state;
            Data    = d;
        }

        // ---- IDisposable ----------------------------------------------------

        private bool _disposed;
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_heightmapNA.IsCreated)         _heightmapNA.Dispose();
            if (_splatmapNA.IsCreated)           _splatmapNA.Dispose();
            if (_biomeWeightsNA.IsCreated)       _biomeWeightsNA.Dispose();
            if (_tempBiomeHeightmapsNA.IsCreated)_tempBiomeHeightmapsNA.Dispose();
        }
    }
}
