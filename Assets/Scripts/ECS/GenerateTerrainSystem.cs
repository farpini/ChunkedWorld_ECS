/*
 * Written by Fernando Arpini Ferretto
 * https://github.com/farpini/ProceduralTileChunkedMap_ECS
 */

using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Collections.LowLevel.Unsafe;

public partial struct GenerateTerrainSystem : ISystem, ISystemStartStop
{
    private Random seedRandom;
    private MapComponent currentMapComponent;
    private EntityQuery terrainChunkRendererQuery;
    private EntityQuery terrainChunkedModelRendererQuery;

    public void OnCreate (ref SystemState state)
    {
        state.RequireForUpdate<MapComponent>();
        state.RequireForUpdate<ControllerComponent>();
        state.RequireForUpdate<ModelDataEntityBuffer>();
        state.RequireForUpdate<RendererPrefabEntities>();
    }

    public void OnDestroy (ref SystemState state)
    {
        foreach (var meshData in SystemAPI.Query<MeshChunkData>())
        {
            meshData.Dispose();
        }

        SystemAPI.GetSingleton<MapTileComponent>().Dispose();
    }

    public void OnStartRunning (ref SystemState state)
    {
        System.DateTime epochStart = new System.DateTime(1970, 1, 1, 0, 0, 0, System.DateTimeKind.Utc);
        uint cur_time = (uint)(System.DateTime.UtcNow - epochStart).TotalSeconds;

        seedRandom = new Random(cur_time);

        terrainChunkRendererQuery = state.EntityManager.CreateEntityQuery(typeof(TerrainComponent));
        terrainChunkedModelRendererQuery = state.EntityManager.CreateEntityQuery(typeof(ChunkedModelComponent), typeof(MeshChunkData));
    }

    public void OnStopRunning (ref SystemState state)
    {
    }

    private void CreateTerrain (ref SystemState state, EntityCommandBuffer ecb, int chunkedModelCount)
    {
        currentMapComponent = SystemAPI.GetSingleton<MapComponent>();

        // dispose previous and create new map tile data
        var mapTileComponent = SystemAPI.GetSingletonRW<MapTileComponent>();
        mapTileComponent.ValueRW.Dispose();
        mapTileComponent.ValueRW.TileData = new NativeArray<TileData>(
            currentMapComponent.TileDimension.x * currentMapComponent.TileDimension.y, Allocator.Persistent);
        mapTileComponent.ValueRW.TileHeightMap = new NativeArray<float>(
            (currentMapComponent.TileDimension.x + 1) * (currentMapComponent.TileDimension.y + 1), Allocator.Persistent);

        // destroy previous and create terrain renderers
        ecb.DestroyEntity(terrainChunkRendererQuery.ToEntityArray(Allocator.Temp));
        var mapEntity = SystemAPI.GetSingletonEntity<MapComponent>();
        var rendererPrefabEntities = SystemAPI.GetSingleton<RendererPrefabEntities>();
        var chunkCount = currentMapComponent.ChunkCount;
        var terrainEntities = state.EntityManager.Instantiate(rendererPrefabEntities.tilePrefab, chunkCount, Allocator.Temp);
        var terrainChunkRendererBuffer = SystemAPI.GetBuffer<ChunkRendererEntityBuffer>(mapEntity);
        terrainChunkRendererBuffer.Length = chunkCount;

        // destroy previous terrain chunked model renderers
        var terrainChunkedModelMeshChunkDataComponentArray = terrainChunkedModelRendererQuery.ToComponentArray<MeshChunkData>();
        for (int i = 0; i < terrainChunkedModelMeshChunkDataComponentArray.Length; i++)
        {
            terrainChunkedModelMeshChunkDataComponentArray[i].Dispose();
        }
        ecb.DestroyEntity(terrainChunkedModelRendererQuery.ToEntityArray(Allocator.Temp));

        // store terrain renderers in the map buffer and create terrain chunked model renderers buffer
        var chunkIndex = 0;
        for (int i = 0; i < currentMapComponent.ChunkDimension.x; i++)
        {
            for (int j = 0; j < currentMapComponent.ChunkDimension.y; j++)
            {
                terrainChunkRendererBuffer[chunkIndex] = new ChunkRendererEntityBuffer { Value = terrainEntities[chunkIndex] };

                var terrainChunkedModelRendererBuffer = state.EntityManager.GetBuffer<ChunkRendererEntityBuffer>(terrainEntities[chunkIndex]);
                terrainChunkedModelRendererBuffer.Clear();
                terrainChunkedModelRendererBuffer.Length = chunkedModelCount;
                for (int c = 0; c < terrainChunkedModelRendererBuffer.Length; c++)
                {
                    terrainChunkedModelRendererBuffer[c] = new ChunkRendererEntityBuffer { Value = Entity.Null };
                }

                state.EntityManager.SetName(terrainEntities[chunkIndex++], "Terrain[" + i + "][" + j + "]");
            }
        }
    }

    public void OnUpdate (ref SystemState state)
    {
        var controllerData = SystemAPI.GetSingletonRW<ControllerComponent>();

        if (controllerData.ValueRO.State != ControllerState.GenerateTerrain)
        {
            return;
        }

        var ecb = new EntityCommandBuffer(Allocator.Temp);

        //CreateTerrainChunkRenderers(ref state, ecb);
        CreateTerrain(ref state, ecb, controllerData.ValueRO.ModelCount);
        //SetupRenderingModels(ref state, ecb);
        GenerateTerrain(ref state);

        controllerData.ValueRW.State = ControllerState.UpdateTerrain;
        //controllerData.ValueRW.State = ControllerState.None;

        ecb.Playback(state.EntityManager);
    }

    private void CreateTerrainChunkRenderers (ref SystemState state, EntityCommandBuffer ecb)
    {
        var currentMapSize = currentMapComponent.TileDimension.x;
        currentMapComponent = SystemAPI.GetSingleton<MapComponent>();

        var rendererPrefabEntities = SystemAPI.GetSingleton<RendererPrefabEntities>();
        //var terrainChunkEntityBuffer = SystemAPI.GetBuffer<ChunkRendererEntityBuffer>(rendererPrefabEntities.tilePrefab);

        var mapTileComponent = SystemAPI.GetSingletonRW<MapTileComponent>();
        mapTileComponent.ValueRW.Dispose();
        mapTileComponent.ValueRW.TileData = new NativeArray<TileData>(
            currentMapComponent.TileDimension.x * currentMapComponent.TileDimension.y, Allocator.Persistent);
        mapTileComponent.ValueRW.TileHeightMap = new NativeArray<float>(
            (currentMapComponent.TileDimension.x + 1) * (currentMapComponent.TileDimension.y + 1), Allocator.Persistent);

        // it needs to dispose MeshChunkData...
        var chunkModelQuery = state.EntityManager.CreateEntityQuery(typeof(MeshChunkData));
        var chunkModelEntities = chunkModelQuery.ToEntityArray(Allocator.Temp);
        var chunkModelMeshData = chunkModelQuery.ToComponentArray<MeshChunkData>();
        for (int i = 0; i < chunkModelMeshData.Length; i++)
        {
            chunkModelMeshData[i].Dispose();
        }

        ecb.DestroyEntity(chunkModelEntities);

        


        // destroy individual renderers
        // HERE...
        // .....

        // create/destroy the chunk terrain renderers only if mapsize changed of it is the first generation
        //if (currentMapSize == currentMapComponent.TileDimension.x && terrainChunkEntityBuffer.Length > 0)
        //{
        //    return;
        //}

        // destroy the chunk terrain renderers
        //if (terrainChunkEntityBuffer.Length > 0)
        //{
            // destroy the chunk terrain renderers
        //    var chunkRendererEntites = new NativeArray<Entity>(terrainChunkEntityBuffer.AsNativeArray().Reinterpret<Entity>(), Allocator.Temp);
        //    terrainChunkEntityBuffer.Clear();
        //    ecb.DestroyEntity(chunkRendererEntites);
        //}

        //var chunkDimension = currentMapComponent.ChunkDimension;

        //var chunkRendererEntities = state.EntityManager.Instantiate(rendererPrefabEntities.chunkTileRenderer,
        //    chunkDimension.x * chunkDimension.y, Allocator.Temp);

        //var chunkIndex = 0;

        // create the chunk terrain renderers
        /*
        for (int i = 0; i < chunkDimension.x; i++)
        {
            for (int j = 0; j < chunkDimension.y; j++)
            {
                state.EntityManager.SetName(chunkRendererEntities[chunkIndex], "Terrain[" + i + "][" + j + "]");
                terrainChunkEntityBuffer.Add(new ChunkRendererEntityBuffer { Value = chunkRendererEntities[chunkIndex++] });
            }
        }
        */
    }

    [BurstCompile]
    private void GenerateTerrain (ref SystemState state)
    {
        var mapTileComponent = SystemAPI.GetSingletonRW<MapTileComponent>();
        var mapHeights = mapTileComponent.ValueRW.TileHeightMap;
        var randomValueSeed = seedRandom.NextUInt();

        currentMapComponent = SystemAPI.GetSingleton<MapComponent>();
        var heightMaxMinValue = new NativeArray<float>(2, Allocator.TempJob);

        var generateTerrainWithDiamondSquareJob = new GenerateTerrainWithDiamondSquareJob
        {
            MapHeights = mapHeights,
            TileWidth = currentMapComponent.TileWidth,
            MapHeightWidth = currentMapComponent.TileDimension.x + 1,
            MaxHeight = currentMapComponent.MaxHeight,
            MaxDepth = currentMapComponent.MaxDepth,
            Roughness = (int)currentMapComponent.Roughness,
            RandomValue = new Unity.Mathematics.Random(randomValueSeed),
            HeightMaxMinValue = heightMaxMinValue
        };

        generateTerrainWithDiamondSquareJob.Schedule(state.Dependency).Complete();

        mapTileComponent.ValueRW.MapHeightMaxMinValue = new float2(heightMaxMinValue[0], heightMaxMinValue[1]);
        heightMaxMinValue.Dispose();
    }
}

[BurstCompile]
public partial struct GenerateTerrainWithDiamondSquareJob : IJob
{
    public NativeArray<float> MapHeights;

    [ReadOnly]
    public int TileWidth;

    [ReadOnly]
    public int MapHeightWidth;

    [ReadOnly]
    public int MaxHeight;

    [ReadOnly]
    public int MaxDepth;

    [ReadOnly]
    public int Roughness;

    [ReadOnly]
    public Unity.Mathematics.Random RandomValue;

    [WriteOnly]
    [NativeDisableContainerSafetyRestriction]
    public NativeArray<float> HeightMaxMinValue;


    [BurstCompile]
    public void Execute ()
    {
        MapHeights[GetHeightIndex(0, 0)] = RandomFloat();
        MapHeights[GetHeightIndex(MapHeightWidth - 1, 0)] = RandomFloat();
        MapHeights[GetHeightIndex(0, MapHeightWidth -1)] = RandomFloat();
        MapHeights[GetHeightIndex(MapHeightWidth -1, MapHeightWidth - 1)] = RandomFloat();

        var maxValue = 0f;
        var minValue = 0f;

        DiamondSquareExecute(MapHeightWidth, Roughness, ref maxValue, ref minValue);

        HeightMaxMinValue[0] = minValue;
        HeightMaxMinValue[1] = maxValue;
    }

    [BurstCompile]
    private void DiamondSquareExecute (int size, float roughness, ref float maxValue, ref float minValue)
    {
        int half = size / 2;
        if (half < 1)
        {
            return;
        }

        for (int x = half; x < MapHeightWidth; x += size)
        {
            for (int y = half; y < MapHeightWidth; y += size)
            {
                DiamondStep(x, y, half, roughness, ref maxValue, ref minValue);
            }
        }

        DiamondSquareExecute(half, roughness / 2, ref maxValue, ref minValue);
    }

    [BurstCompile]
    private void DiamondStep (int x, int y, int half, float roughness, ref float maxValue, ref float minValue)
    {
        var value = 0f;
        value += MapHeights[GetHeightIndex(x + half, y - half)];
        value += MapHeights[GetHeightIndex(x - half, y + half)];
        value += MapHeights[GetHeightIndex(x + half, y + half)];
        value += MapHeights[GetHeightIndex(x - half, y - half)];

        value /= 4;
        value += RandomFloat() * roughness;

        if (value > maxValue)
        {
            maxValue = value;
        }
        if (value < minValue)
        {
            minValue = value;
        }

        MapHeights[GetHeightIndex(x, y)] = value;

        SquareStep(x - half, y, half, roughness, ref maxValue, ref minValue);
        SquareStep(x + half, y, half, roughness, ref maxValue, ref minValue);
        SquareStep(x, y - half, half, roughness, ref maxValue, ref minValue);
        SquareStep(x, y + half, half, roughness, ref maxValue, ref minValue);
    }

    [BurstCompile]
    private void SquareStep (int x, int y, int half, float roughness, ref float maxValue, ref float minValue)
    {
        var value = 0f;
        var count = 0;
        if (x - half >= 0)
        {
            value += MapHeights[GetHeightIndex(x - half, y)];
            count++;
        }
        if (x + half < MapHeightWidth)
        {
            value += MapHeights[GetHeightIndex(x + half, y)];
            count++;
        }
        if (y - half >= 0)
        {
            value += MapHeights[GetHeightIndex(x, y - half)];
            count++;
        }
        if (y + half < MapHeightWidth)
        {
            value += MapHeights[GetHeightIndex(x, y + half)];
            count++;
        }

        value /= count;
        value += RandomFloat() * roughness;

        if (value > maxValue)
        {
            maxValue = value;
        }
        if (value < minValue)
        {
            minValue = value;
        }

        MapHeights[GetHeightIndex(x, y)] = value;
    }

    [BurstCompile]
    private int GetHeightIndex (int x, int y)
    {
        return x * MapHeightWidth + y;
    }

    [BurstCompile]
    private float RandomFloat ()
    {
        return RandomValue.NextFloat() * 2 - 1;
    }
}