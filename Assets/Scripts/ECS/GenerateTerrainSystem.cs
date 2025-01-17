using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor.ShaderGraph;

public partial struct GenerateTerrainSystem : ISystem, ISystemStartStop
{
    private Unity.Mathematics.Random seedRandom;
    private Unity.Mathematics.Random RandomValue;
    private MapComponent currentMapComponent;
    private int mapHeightWidth;
    private static NativeArray<float> currentHeight;

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
        seedRandom = new Unity.Mathematics.Random(34543);
    }

    private void CreateTerrainChunkRenderers (ref SystemState state, EntityCommandBuffer ecb)
    {
        var currentMapSize = currentMapComponent.TileDimension.x;
        currentMapComponent = SystemAPI.GetSingleton<MapComponent>();

        var rendererPrefabEntities = SystemAPI.GetSingleton<RendererPrefabEntities>();
        var terrainChunkEntityBuffer = SystemAPI.GetBuffer<ChunkRendererEntityBuffer>(rendererPrefabEntities.tilePrefab);

        var mapTileComponent = SystemAPI.GetSingletonRW<MapTileComponent>();
        mapTileComponent.ValueRW.Dispose();
        mapTileComponent.ValueRW.TileData = new NativeArray<TileData>(
            currentMapComponent.TileDimension.x * currentMapComponent.TileDimension.y, Allocator.Persistent);
        mapTileComponent.ValueRW.TileHeightMap = new NativeArray<float>(
            (currentMapComponent.TileDimension.x + 1) * (currentMapComponent.TileDimension.y + 1), Allocator.Persistent);

        //Debug.Log("DISPOSE");

        if (currentMapSize == currentMapComponent.TileDimension.x && terrainChunkEntityBuffer.Length > 0)
        {
            return;
        }

        if (terrainChunkEntityBuffer.Length > 0)
        {
            var chunkRendererEntites = new NativeArray<Entity>(terrainChunkEntityBuffer.AsNativeArray().Reinterpret<Entity>(), Allocator.Temp);
            terrainChunkEntityBuffer.Clear();
            // destroy the renderers
            ecb.DestroyEntity(chunkRendererEntites);
            //terrainChunkEntityBuffer.Clear();

            // destroy the chunk renderers
            // it needs to dispose MeshChunkData...

            // destroy individual renderers
        }

        var chunkDimension = currentMapComponent.ChunkDimension;

        var chunkRendererEntities = state.EntityManager.Instantiate(rendererPrefabEntities.chunkTileRenderer,
            chunkDimension.x * chunkDimension.y, Allocator.Temp);

        var chunkIndex = 0;

        for (int i = 0; i < chunkDimension.x; i++)
        {
            for (int j = 0; j < chunkDimension.y; j++)
            {
                state.EntityManager.SetName(chunkRendererEntities[chunkIndex], "Terrain[" + i + "][" + j + "]");
                terrainChunkEntityBuffer.Add(new ChunkRendererEntityBuffer { Value = chunkRendererEntities[chunkIndex++] });
            }
        }
    }

    public void OnStopRunning (ref SystemState state)
    {
    }

    public void OnUpdate (ref SystemState state)
    {
        var controllerData = SystemAPI.GetSingletonRW<ControllerComponent>();

        if (controllerData.ValueRO.State != ControllerState.GenerateTerrain)
        {
            return;
        }

        var ecb = new EntityCommandBuffer(Allocator.Temp);

        CreateTerrainChunkRenderers(ref state, ecb);
        GenerateTerrain(ref state);

        controllerData.ValueRW.State = ControllerState.UpdateTerrain;

        ecb.Playback(state.EntityManager);
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