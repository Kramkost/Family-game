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
    public sealed class TerrainChunk : IChunkLifecycle, IDisposable
    {
        public  ChunkData         Data         { get; private set; }
        public  ChunkState        State        => Data.State;
        public  Terrain           UnityTerrain { get; private set; }
        public  TerrainData       TerrainData  { get; private set; }
        public  GameObject        GameObject   { get; private set; }

        private readonly WorldSettings  _settings;
        private readonly ITerrainLogger _logger;
        private readonly BiomeProvider  _biomeProvider;
        private readonly RoadBuilder    _roadBuilder;

        private const string LOG_TAG = "TerrainChunk";

        private NativeArray<float>  _heightmapNA;
        private NativeArray<float>  _splatmapNA;
        private NativeArray<float>  _biomeWeightsNA;
        private NativeArray<float>  _tempBiomeHeightmapsNA;

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

        public void BeginGeneration()
        {
            SetState(ChunkState.Queued);
            _logger.Log(LogLevel.Info, LOG_TAG, $"Chunk [{Data.Coord.x},{Data.Coord.y}]: Generation queued.");
        }

        public IEnumerator GenerateAsync()
        {
            int res       = _settings.ChunkResolution;
            int vertCount = res * res;
            int numBiomes = _biomeProvider.Biomes.Count;
            int numLayers = _settings.TerrainLayers?.Length ?? 1;

            _heightmapNA       = new NativeArray<float>(vertCount, Allocator.Persistent);
            _splatmapNA        = new NativeArray<float>(vertCount * numLayers, Allocator.Persistent);
            _biomeWeightsNA    = new NativeArray<float>(vertCount * numBiomes, Allocator.Persistent);

            // ================================================================
            // STEP 1: Biome Weights
            // ================================================================
            SetState(ChunkState.GeneratingHeightmap);
            var biomeHandle = _biomeProvider.ScheduleBiomeWeightJob(Data.Coord, res, _settings.ChunkWorldSize, _biomeWeightsNA);
            while (!biomeHandle.IsCompleted) yield return null;
            biomeHandle.Complete();

            // ================================================================
            // STEP 2: Blended Heightmap
            // ================================================================
            var heightHandle = _biomeProvider.ScheduleBlendedHeightmapJob(
                Data.Coord, res, _settings.ChunkWorldSize,
                _biomeWeightsNA, _heightmapNA,
                out _tempBiomeHeightmapsNA, biomeHandle);

            while (!heightHandle.IsCompleted) yield return null;
            heightHandle.Complete();
            _tempBiomeHeightmapsNA.Dispose(); 

            // ================================================================
            // STEP 3: Road Carving into Heightmap
            // ================================================================
            SetState(ChunkState.CarveRoad);
            
            var splineList = _roadBuilder.GetSampledPointsForChunk(Data.Coord);
            var nativeSplines = new NativeArray<float3>(splineList.Count, Allocator.TempJob);
            for (int i = 0; i < splineList.Count; i++) nativeSplines[i] = splineList[i].Position;

            var carvingJob = new RoadCarvingJob
            {
                Resolution        = res,
                WorldSize         = _settings.ChunkWorldSize,
                ChunkOriginXZ     = new float2(Data.Coord.x * _settings.ChunkWorldSize, Data.Coord.y * _settings.ChunkWorldSize),
                HeightScale       = GetAverageHeightScale(),
                RoadHalfWidth     = _settings.RoadWidth * 0.5f,
                ShoulderHalfWidth = _settings.RoadShoulderWidth,
                ClearanceRadius   = _settings.RoadClearanceRadius,
                EmbankmentHeight  = _settings.RoadEmbankmentHeight,
                SplinePoints      = nativeSplines,
                Heightmap         = _heightmapNA
            };

            var carvingHandle = carvingJob.Schedule(vertCount, 64);
            while (!carvingHandle.IsCompleted) yield return null;
            carvingHandle.Complete();
            
            // ================================================================
            // STEP 4: Splatmap Generation
            // ================================================================
            SetState(ChunkState.GeneratingSplatmap);

            var biomeLayerPairs = new NativeArray<int2>(numBiomes, Allocator.TempJob);
            for (int b = 0; b < numBiomes; b++)
            {
                var biome = _biomeProvider.Biomes[b];
                biomeLayerPairs[b] = new int2(biome.PrimarySplatLayer, biome.SecondarySplatLayer);
            }

            var splatJob = new SplatmapGenerationJob
            {
                Resolution        = res,
                WorldSize         = _settings.ChunkWorldSize,
                ChunkOriginXZ     = new float2(Data.Coord.x * _settings.ChunkWorldSize, Data.Coord.y * _settings.ChunkWorldSize),
                RoadHalfWidth     = _settings.RoadWidth * 0.5f,
                ShoulderHalfWidth = _settings.RoadShoulderWidth,
                SplinePoints      = nativeSplines,
                NumLayers         = numLayers,
                NumBiomes         = numBiomes,
                HeightScale       = GetAverageHeightScale(),
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

            // ================================================================
            // STEP 5: Build Unity Terrain Mesh
            // ================================================================
            SetState(ChunkState.BuildingMesh);
            BuildMesh();
            yield return null; 

            // ================================================================
            // STEP 6: Upload splatmap to TerrainData
            // ================================================================
            ApplySplatmap(numLayers);

            var d = Data;
            d.Heightmap    = _heightmapNA;
            d.Splatmap     = _splatmapNA;
            d.BiomeWeights = _biomeWeightsNA;
            Data = d;

            SetState(ChunkState.Active);
        }

        public void BuildMesh()
        {
            TerrainData = new TerrainData();
            int res = _settings.ChunkResolution;
            TerrainData.heightmapResolution = res;
            TerrainData.size = new Vector3(_settings.ChunkWorldSize, GetAverageHeightScale(), _settings.ChunkWorldSize);

            if (_settings.TerrainLayers != null && _settings.TerrainLayers.Length > 0)
                TerrainData.terrainLayers = _settings.TerrainLayers;

            float[,] heights = new float[res, res];
            for (int z = 0; z < res; z++)
            for (int x = 0; x < res; x++)
                heights[z, x] = _heightmapNA[z * res + x];

            TerrainData.SetHeights(0, 0, heights);

            Vector3 origin = new Vector3(Data.Coord.x * _settings.ChunkWorldSize, 0f, Data.Coord.y * _settings.ChunkWorldSize);

            GameObject = Terrain.CreateTerrainGameObject(TerrainData);
            GameObject.name = $"Chunk_{Data.Coord.x}_{Data.Coord.y}";
            UnityTerrain = GameObject.GetComponent<Terrain>();
            UnityTerrain.transform.position = origin;
            UnityTerrain.drawInstanced  = true;
            UnityTerrain.heightmapPixelError = 5f;
            
            // Фикс для физики деревьев и террейна
            UnityTerrain.drawTreesAndFoliage = true;
            var terrainCollider = GameObject.GetComponent<TerrainCollider>();
            if (terrainCollider == null) terrainCollider = GameObject.AddComponent<TerrainCollider>();
            terrainCollider.terrainData = TerrainData;
        }

        public void Activate()
        {
            if (GameObject != null) GameObject.SetActive(true);
            SetState(ChunkState.Active);
        }

        public void Deactivate()
        {
            if (GameObject != null) GameObject.SetActive(false);
            SetState(ChunkState.Deactivated);
        }

        public void Unload()
        {
            Dispose();
            if (GameObject != null) UnityEngine.Object.Destroy(GameObject);
            SetState(ChunkState.Unloaded);
        }

        private void ApplySplatmap(int numLayers)
        {
            if (TerrainData == null) return;
            int res = _settings.ChunkResolution;

            float[,,] alphamap = new float[res, res, numLayers];
            for (int z = 0; z < res; z++)
            for (int x = 0; x < res; x++)
            for (int l = 0; l < numLayers; l++)
                alphamap[z, x, l] = _splatmapNA[(z * res + x) * numLayers + l];

            TerrainData.SetAlphamaps(0, 0, alphamap);
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