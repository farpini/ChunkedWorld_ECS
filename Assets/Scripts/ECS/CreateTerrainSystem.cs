using System.Diagnostics;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine.Rendering;
using UnityEngine;
using System.Drawing;
using Unity.Collections.LowLevel.Unsafe;
using static UnityEditor.PlayerSettings;

public partial struct CreateTerrainSystem : ISystem, ISystemStartStop
{
    private Unity.Mathematics.Random seedRandom;
    private MapComponent mapComponent;

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

        mapComponent = SystemAPI.GetSingleton<MapComponent>();

        var rendererPrefabEntities = SystemAPI.GetSingleton<RendererPrefabEntities>();

        var terrainChunkEntityBuffer = SystemAPI.GetBuffer<ChunkRendererEntityBuffer>(rendererPrefabEntities.tilePrefab);

        if (terrainChunkEntityBuffer.Length == 0)
        {
            var chunkDimension = mapComponent.ChunkDimension;

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

        GenerateTerrain(ref state, SystemAPI.GetSingletonRW<ControllerComponent>());
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

        GenerateTerrain(ref state, controllerData);

        var mapTileComponent = SystemAPI.GetSingletonRW<MapTileComponent>();
        var mapTiles = mapTileComponent.ValueRW.TileData;
        var heigthMap = mapTileComponent.ValueRW.TileHeightMap;

        var terrainEntity = SystemAPI.GetSingleton<RendererPrefabEntities>().tilePrefab;

        var meshBlobInfoComponent = state.EntityManager.GetSharedComponentManaged<MeshBlobInfoComponent>(terrainEntity);
        var meshBlobDataComponent = state.EntityManager.GetSharedComponentManaged<MeshBlobDataComponent>(terrainEntity);
        var meshBlobTerrainTileMappingComponent = state.EntityManager.GetSharedComponentManaged<MeshBlobTileTerrainMappingComponent>(terrainEntity);

        var rendererChunkBuffer = SystemAPI.GetBuffer<ChunkRendererEntityBuffer>(terrainEntity);

        var entitiesGraphicsSystem = state.World.GetExistingSystemManaged<EntitiesGraphicsSystem>();

        var chunkDimension = mapComponent.ChunkDimension;
        var chunkTileCount = mapComponent.ChunkWidth * mapComponent.ChunkWidth;
        var chunkRectSize = new int2(mapComponent.ChunkWidth, mapComponent.ChunkWidth);

        for (int i = 0; i < chunkDimension.x; i++)
        {
            for (int j = 0; j < chunkDimension.y; j++)
            {
                var chunkPosition = new int2(i, j);
                var chunkIndex = mapComponent.GetChunkIndexFromChunkPosition(chunkPosition);
                var chunkRectPosition = chunkPosition * mapComponent.ChunkWidth;

                var chunkMesh = new Mesh();
                var chunkDataArray = Mesh.AllocateWritableMeshData(chunkMesh);
                var chunkData = chunkDataArray[0];

                chunkData.SetVertexBufferParams(chunkTileCount * meshBlobInfoComponent.vertexCount,
                    meshBlobInfoComponent.meshInfoBlob.Value.attributes.ToArray());
                chunkData.SetIndexBufferParams(chunkTileCount * meshBlobInfoComponent.indexCount, IndexFormat.UInt32);

                var setTerrainInChunkRectJob = new SetTerrainInChunkRectJob
                {
                    MapTiles = mapTiles,
                    MapHeights = heigthMap,
                    MapHeightTileTypeMapping = meshBlobTerrainTileMappingComponent,
                    MapTileDimension = mapComponent.TileDimension,
                    TileWidth = mapComponent.TileWidth,
                    ChunkWidth = mapComponent.ChunkWidth,
                    RectPosition = chunkRectPosition,
                    RectSize = chunkRectSize,
                    MeshData = meshBlobDataComponent,
                    MeshChunkData = chunkData
                };

                setTerrainInChunkRectJob.Schedule(state.Dependency).Complete();

                var rendererChunkEntity = rendererChunkBuffer[chunkIndex];

                var materialMeshInfoComponent = SystemAPI.GetComponentRW<MaterialMeshInfo>(rendererChunkEntity);
                materialMeshInfoComponent.ValueRW.Mesh = 0;

                UpdateMesh(ref state, entitiesGraphicsSystem, rendererChunkEntity, materialMeshInfoComponent, meshBlobInfoComponent,
                    chunkDataArray, chunkMesh, chunkTileCount);
            }
        }

        controllerData.ValueRW.State = ControllerState.None;
    }

    private void UpdateMesh (ref SystemState state, EntitiesGraphicsSystem entitiesGraphicsSystem, Entity rendererEntity,
        RefRW<MaterialMeshInfo> materialMeshInfoComponent, MeshBlobInfoComponent meshInfoComponent,
        Mesh.MeshDataArray meshDataArray, Mesh mesh, int chunkMeshCount)
    {
        if (materialMeshInfoComponent.ValueRO.Mesh > 0)
        {
            entitiesGraphicsSystem.UnregisterMesh(materialMeshInfoComponent.ValueRO.MeshID);
        }

        var vertexCount = chunkMeshCount * meshInfoComponent.vertexCount;
        var indexCount = chunkMeshCount * meshInfoComponent.indexCount;

        var meshData = meshDataArray[0];

        meshData.SetVertexBufferParams(vertexCount, meshInfoComponent.meshInfoBlob.Value.attributes.ToArray());
        meshData.SetIndexBufferParams(indexCount, IndexFormat.UInt32);

        meshData.subMeshCount = 1;
        meshData.SetSubMesh(0, new SubMeshDescriptor(0, indexCount));

        Mesh.ApplyAndDisposeWritableMeshData(meshDataArray, mesh, MeshUpdateFlags.Default);

        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        SystemAPI.GetComponentRW<RenderBounds>(rendererEntity).ValueRW.Value = mesh.bounds.ToAABB();

        materialMeshInfoComponent.ValueRW.MeshID = entitiesGraphicsSystem.RegisterMesh(mesh);
    }

    private void GenerateTerrain (ref SystemState state, RefRW<ControllerComponent> controllerData)
    {
        var mapTileComponent = SystemAPI.GetSingletonRW<MapTileComponent>();
        var mapHeights = mapTileComponent.ValueRW.TileHeightMap;

        /*
        var terrainHeightMapSettings = new TerrainHeightMapSettings
        {
            noiseScale = 4f,
            frequency = 5f,
            lacunarity = 1f,
            octaves = 3,
            weight = 1f,
            falloffOffset = 0,
            falloffSteepness = 0
        };
        */

        //var randomValue = new Random(9184718);
        //var mapSeed = new int2(randomValue.NextInt(), randomValue.NextInt());

        //UnityEngine.Debug.Log("Generated: " + controllerData.ValueRO.MapMagnification);

        var result = new NativeArray<int>(4, Allocator.TempJob);
        var randomValueSeed = seedRandom.NextUInt();

        UnityEngine.Debug.LogWarning("RandomSeed: " + randomValueSeed);

        var generateTerrainHeightMapJob = new GenerateTerrainHeightMapJob
        {
            MapHeights = mapHeights,
            CurrentHeight = new NativeArray<double>(mapHeights.Length, Allocator.TempJob),
            MapTileDimension = mapComponent.TileDimension,
            MaxHeight = mapComponent.MaxHeight,
            MapOffset = controllerData.ValueRO.MapOffset,
            MapSeed = int2.zero,
            RandomValue = new Unity.Mathematics.Random(randomValueSeed),
            TerrainSettings = new TerrainHeightMapSettings
            {
                noiseScale = 2,
                frequency = controllerData.ValueRO.MapMagnification,
                octaves = 3,
                lacunarity = 0.2f,
                weight = 1f,
                falloffOffset = 10,
                falloffSteepness = 0
            },
            outtt = result
        };

        generateTerrainHeightMapJob.Schedule(state.Dependency).Complete();

        //UnityEngine.Debug.LogWarning("0: " + result[0] + " 1: " + result[1] + " 2: " + result[2] + " 3: " + result[3]);
    }
}

// 	Created by Jacob Milligan on 10/10/2016.
// 	Copyright (c) Jacob Milligan All rights reserved

[BurstCompile]
public partial struct GenerateTerrainHeightMapJob : IJob
{
    public NativeArray<int> MapHeights;
    public NativeArray<double> CurrentHeight;

    [ReadOnly]
    public int2 MapTileDimension;

    [ReadOnly]
    public float2 MapOffset;

    [ReadOnly]
    public int MaxHeight;

    [ReadOnly]
    public int2 MapSeed;

    [ReadOnly]
    public Unity.Mathematics.Random RandomValue;

    [ReadOnly]
    public float RoughNess;

    [ReadOnly]
    public TerrainHeightMapSettings TerrainSettings;

    [WriteOnly]
    public NativeArray<int> outtt;


    [BurstCompile]
    private int GetHeightIndexFromHeighPosition (int2 heightPosition)
    {
        return heightPosition.x * (MapTileDimension.y + 1) + heightPosition.y;
    }

    [BurstCompile]
    public void Execute ()
    {
        var instanceSize = (int)MapOffset.x;
        var scale = (double)TerrainSettings.frequency;

        /*
        CurrentHeight[GetHeightIndexFromHeighPosition(int2.zero)] = -1;
        CurrentHeight[GetHeightIndexFromHeighPosition(new int2(MapTileDimension.x - 1, 0))] = -1;
        CurrentHeight[GetHeightIndexFromHeighPosition(new int2(0, MapTileDimension.y - 1))] = -1;
        CurrentHeight[GetHeightIndexFromHeighPosition(new int2(MapTileDimension.x - 1, MapTileDimension.y - 1))] = -1;
        CurrentHeight[GetHeightIndexFromHeighPosition(new int2(MapTileDimension.x / 2, MapTileDimension.y / 2))] = 1;
        */

        CurrentHeight[GetHeightIndexFromHeighPosition(int2.zero)] = RandomDouble();
        CurrentHeight[GetHeightIndexFromHeighPosition(new int2(MapTileDimension.x - 1, 0))] = RandomDouble();
        CurrentHeight[GetHeightIndexFromHeighPosition(new int2(0, MapTileDimension.y - 1))] = RandomDouble();
        CurrentHeight[GetHeightIndexFromHeighPosition(new int2(MapTileDimension.x - 1, MapTileDimension.y - 1))] = RandomDouble();

        while (instanceSize > 1)
        {
            Iterate(instanceSize, scale);

            instanceSize /= 2;
            scale /= 2.0;
        }

        //var zeros = 0;
        //var ones = 0;
        //var twos = 0; 
        //var threes = 0;
        var p = int2.zero;
        var pIndex = 0;

        for (int i = 0; i < MapTileDimension.x; i++)
        {
            for (int j = 0; j < MapTileDimension.y; j++)
            {
                p = new int2(i, j);
                pIndex = GetHeightIndexFromHeighPosition(p);
                var heightValue = math.clamp(CurrentHeight[pIndex], 0f, 1f) * MaxHeight;
                if (heightValue == MaxHeight)
                {
                    heightValue = MaxHeight - 1;
                }
                var h = (int)math.floor(heightValue);
                /*
                if (h == 0)
                    zeros++;
                else if (h == 1)
                    ones++;
                else if (h == 2)
                    twos++;
                else
                    threes++;
                */

                MapHeights[pIndex] = h;
            }
        }

        // fix last row&column tile height to be a copy from the previous
        p = new int2(MapTileDimension.x, MapTileDimension.y);
        pIndex = GetHeightIndexFromHeighPosition(p);
        var pO = new int2(MapTileDimension.x - 1, MapTileDimension.y - 1);
        var pOIndex = GetHeightIndexFromHeighPosition(pO);
        MapHeights[pIndex] = MapHeights[pOIndex];

        for (int i = 0; i < MapTileDimension.x; i++)
        {
            p = new int2(i, MapTileDimension.y);
            pIndex = GetHeightIndexFromHeighPosition(p);
            pO = new int2(i, MapTileDimension.y - 1);
            pOIndex = GetHeightIndexFromHeighPosition(pO);
            MapHeights[pIndex] = MapHeights[pOIndex];
        }

        for (int j = 0; j < MapTileDimension.y; j++)
        {
            p = new int2(MapTileDimension.x, j);
            pIndex = GetHeightIndexFromHeighPosition(p);
            pO = new int2(MapTileDimension.x - 1, j);
            pOIndex = GetHeightIndexFromHeighPosition(pO);
            MapHeights[pIndex] = MapHeights[pOIndex];
        }

        

        //outtt[0] = zeros;
        //outtt[1] = ones;
        //outtt[2] = twos;
        //outtt[3] = threes;
    }

    [BurstCompile]
    private double GetValue (int x, int y)
    {
        var pos = WrapGrid(x, y, MapTileDimension.x, MapTileDimension.y);
        return CurrentHeight[GetHeightIndexFromHeighPosition(pos)];
    }

    [BurstCompile]
    private double RandomDouble ()
    {
        return RandomValue.NextDouble() * 2 - 1;
    }

    [BurstCompile]
    private void Iterate (int step, double currentScale)
    {
        var halfStep = step / 2;

        // Handle squares
        for (int y = halfStep; y < halfStep + MapTileDimension.y; y += step)
        {
            for (int x = halfStep; x < halfStep + MapTileDimension.x; x += step)
            {
                HandleSquare(x, y, step, RandomDouble() * currentScale);
            }
        }

        // Handle diamonds
        for (int y = 0; y < MapTileDimension.y; y += step)
        {
            for (int x = 0; x < MapTileDimension.x; x += step)
            {
                HandleDiamond(x + halfStep, y, step, RandomDouble() * currentScale);
                HandleDiamond(x, y + halfStep, step, RandomDouble() * currentScale);
            }
        }
    }

    [BurstCompile]
    private void HandleSquare (int x, int y, int step, double newValue)
    {
        var halfStep = step / 2;

        var a = GetValue(x - halfStep, y - halfStep);
        var b = GetValue(x + halfStep, y - halfStep);
        var c = GetValue(x - halfStep, y + halfStep);
        var d = GetValue(x + halfStep, y + halfStep);

        var pos = WrapGrid(x, y, MapTileDimension.x, MapTileDimension.y);
        CurrentHeight[GetHeightIndexFromHeighPosition(pos)] = ((a + b + c + d) / 4.0) + newValue;

        //_values[pos.X, pos.Y] = ((a + b + c + d) / 4.0) + newValue;
    }

    [BurstCompile]
    private void HandleDiamond (int x, int y, int step, double newValue)
    {
        var halfStep = step / 2;

        var b = GetValue(x + halfStep, y);
        var d = GetValue(x - halfStep, y);
        var a = GetValue(x, y - halfStep);
        var c = GetValue(x, y + halfStep);

        var pos = WrapGrid(x, y, MapTileDimension.x, MapTileDimension.y);
        CurrentHeight[GetHeightIndexFromHeighPosition(pos)] = ((a + b + c + d) / 4.0) + newValue;

        //_values[pos.X, pos.Y] = ((a + b + c + d) / 4.0) + newValue;
    }

    [BurstCompile]
    public int2 WrapGrid (int x, int y, int width, int height)
    {
        var xResult = 0;
        var yResult = 0;

        if (x >= 0)
        {
            xResult = x % width; // wrap right
        }
        else
        {
            xResult = (width + x % width) % width; // wrap left
        }

        if (y >= 0)
        {
            yResult = y % height; // wrap down
        }
        else
        {
            yResult = (height + y % height) % height; // wrap up
        }

        return new int2(xResult, yResult);
    }
}