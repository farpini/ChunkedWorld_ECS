using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine.Rendering;
using UnityEngine;
using Unity.Collections.LowLevel.Unsafe;

public partial struct CreateTerrainSystem : ISystem, ISystemStartStop
{
    private Unity.Mathematics.Random seedRandom;
    private Unity.Mathematics.Random RandomValue;
    private MapComponent2 mapComponent;
    private int MapHeightWidth;
    private static NativeArray<float> CurrentHeight;

    public void OnCreate (ref SystemState state)
    {
        state.RequireForUpdate<MapComponent2>();
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

        /*
        mapComponent = SystemAPI.GetSingleton<MapComponent2>();

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
        */

        CreateTerrainChunkRenderers(ref state);
        GenerateTerrain(ref state, SystemAPI.GetSingletonRW<ControllerComponent>());
    }

    private void CreateTerrainChunkRenderers (ref SystemState state)
    {
        var currentMapSize = mapComponent.TileDimension.x;
        mapComponent = SystemAPI.GetSingleton<MapComponent2>();

        var rendererPrefabEntities = SystemAPI.GetSingleton<RendererPrefabEntities>();
        var terrainChunkEntityBuffer = SystemAPI.GetBuffer<ChunkRendererEntityBuffer>(rendererPrefabEntities.tilePrefab);

        if (currentMapSize == mapComponent.TileDimension.x && terrainChunkEntityBuffer.Length > 0)
        {
            return;
        }

        if (terrainChunkEntityBuffer.Length > 0)
        {
            // destroy the renderers
            state.EntityManager.DestroyEntity(terrainChunkEntityBuffer.AsNativeArray().Reinterpret<Entity>());
            terrainChunkEntityBuffer.Clear();

            // destroy the chunk renderers
            // it needs to dispose MeshChunkData...

            // destroy individual renderers
        }

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

    private int GetHeightIndex (int x, int y)
    {
        return x * MapHeightWidth + y;
    }

    private float RandomFloat ()
    {
        return RandomValue.NextFloat() * 2 - 1;
    }

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

    private void DiamondStep (int x, int y, int half, float roughness, ref float maxValue, ref float minValue)
    {
        var value = 0f;
        value += CurrentHeight[GetHeightIndex(x + half, y - half)];
        value += CurrentHeight[GetHeightIndex(x - half, y + half)];
        value += CurrentHeight[GetHeightIndex(x + half, y + half)];
        value += CurrentHeight[GetHeightIndex(x - half, y - half)];

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

        CurrentHeight[GetHeightIndex(x, y)] = value;

        SquareStep(x - half, y, half, roughness, ref maxValue, ref minValue);
        SquareStep(x + half, y, half, roughness, ref maxValue, ref minValue);
        SquareStep(x, y - half, half, roughness, ref maxValue, ref minValue);
        SquareStep(x, y + half, half, roughness, ref maxValue, ref minValue);
    }

    private void SquareStep (int x, int y, int half, float roughness, ref float maxValue, ref float minValue)
    {
        var value = 0f;
        var count = 0;
        if (x - half >= 0)
        {
            value += CurrentHeight[GetHeightIndex(x - half, y)];
            count++;
        }
        if (x + half < MapHeightWidth)
        {
            value += CurrentHeight[GetHeightIndex(x + half, y)];
            count++;
        }
        if (y - half >= 0)
        {
            value += CurrentHeight[GetHeightIndex(x, y - half)];
            count++;
        }
        if (y + half < MapHeightWidth)
        {
            value += CurrentHeight[GetHeightIndex(x, y + half)];
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

        CurrentHeight[GetHeightIndex(x, y)] = value;
    }

    private void GenerateTerrain (ref SystemState state, RefRW<ControllerComponent> controllerData)
    {
        var terrainEntity = SystemAPI.GetSingleton<RendererPrefabEntities>().tilePrefab;
        var meshBlobInfoComponent = state.EntityManager.GetSharedComponentManaged<MeshBlobInfoComponent>(terrainEntity);

        var mapTileComponent = SystemAPI.GetSingletonRW<MapTileComponent>();
        var mapHeights = mapTileComponent.ValueRW.TileHeightMap;
        var randomValueSeed = seedRandom.NextUInt();

        var groundMesh = new Mesh();
        var groundMeshArray = Mesh.AllocateWritableMeshData(groundMesh);
        var groundData = groundMeshArray[0];

        mapComponent = SystemAPI.GetSingleton<MapComponent2>();

        var quadsCount = (mapComponent.TileDimension.x * 2) + (mapComponent.TileDimension.y * 2);
        var vertexCount = quadsCount * 4; 
        var indexCount = quadsCount * 6;

        groundData.SetVertexBufferParams(vertexCount, meshBlobInfoComponent.meshInfoBlob.Value.attributes.ToArray());
        groundData.SetIndexBufferParams(indexCount, IndexFormat.UInt32);

        CurrentHeight = new NativeArray<float>(mapHeights.Length, Allocator.TempJob);

        MapHeightWidth = mapComponent.TileDimension.x + 1;

        RandomValue = new Unity.Mathematics.Random(randomValueSeed);

        var a = CurrentHeight[GetHeightIndex(0, 0)] = RandomValue.NextFloat() * mapComponent.MaxLevel;
        var b = CurrentHeight[GetHeightIndex(MapHeightWidth - 1, 0)] = RandomValue.NextFloat() * mapComponent.MaxLevel;
        var c = CurrentHeight[GetHeightIndex(0, MapHeightWidth - 1)] = RandomValue.NextFloat() * mapComponent.MaxLevel;
        var d = CurrentHeight[GetHeightIndex(MapHeightWidth - 1, MapHeightWidth - 1)] = RandomValue.NextFloat() * mapComponent.MaxLevel;

        UnityEngine.Debug.LogWarning("a: " + a + " b: " + b + " c: " + c + " d: " + d);

        var maxValue = float.MinValue;
        var minValue = float.MaxValue;

        DiamondSquareExecute(MapHeightWidth, mapComponent.Roughness, ref maxValue, ref minValue);

        //UnityEngine.Debug.LogWarning("Min: " + minValue + " Max: " + maxValue + " Dif: " + (maxValue + ((minValue >= 0) ? -minValue : math.abs(minValue))));

        for (int x = 0; x < MapHeightWidth; x++)
        {
            for (int y = 0; y < MapHeightWidth; y++)
            {
                var heightIndex = GetHeightIndex(x, y);
                var heightValue = math.unlerp(minValue, maxValue, CurrentHeight[heightIndex]) * mapComponent.MaxLevel;
                if (heightValue >= mapComponent.MaxLevel)
                {
                    heightValue = mapComponent.MaxLevel - 1;
                }
                if (heightValue < 0f)
                {
                    heightValue = 0;
                }
                mapHeights[heightIndex] = (int)math.floor(heightValue);
  
                //if (heightValue <= -mapComponent.MaxDepth)
                //{
                //    heightValue = -mapComponent.MaxDepth + 1;
                //}
             
                //mapHeights[heightIndex] = heightValue >= 0 ? (int)math.floor(heightValue) : (int)math.ceil(heightValue);
            }
        }

        CreateBorderingGround(groundData, ref mapHeights);

        CurrentHeight.Dispose();


        /*
        var generateTerrainWithDiamondSquareJob = new GenerateTerrainWithDiamondSquareJob
        {
            MapHeights = mapHeights,
            CurrentHeight = new NativeArray<float>(mapHeights.Length, Allocator.TempJob),
            TileWidth = mapComponent.TileWidth,
            MapHeightWidth = mapComponent.TileDimension.x + 1,
            MaxHeight = mapComponent.MaxHeight,
            MaxDepth = mapComponent.MaxDepth,
            RandomValue = new Unity.Mathematics.Random(randomValueSeed),
            Roughness = (int)mapComponent.Roughness,
            GroundMeshData = groundData
        };
        
        generateTerrainWithDiamondSquareJob.Schedule(state.Dependency).Complete();
        */

        groundData.subMeshCount = 1;
        groundData.SetSubMesh(0, new SubMeshDescriptor(0, indexCount));

        Mesh.ApplyAndDisposeWritableMeshData(groundMeshArray, groundMesh, MeshUpdateFlags.Default);

        groundMesh.RecalculateNormals();
        groundMesh.RecalculateBounds();

        SystemAPI.ManagedAPI.GetSingleton<RefGameObject>().Map.SetMeshGround(groundMesh, mapComponent.TileWidth, mapComponent.MaxDepth);
    }

    private void CreateBorderingGround (Mesh.MeshData groundData, ref NativeArray<int> Heights)
    {
        var vertexesArray = groundData.GetVertexData<float3>();
        var indexArray = groundData.GetIndexData<uint>();

        var vertexCount = 4;
        var vIndex = 0;
        var tIndex = 0;
        var tValue = 0u;

        for (int i = 0; i < MapHeightWidth - 1; i++)
        {
            CreateBordering(new int2(i, 0), new int2(i + 1, 0), tValue,
                ref vIndex, ref tIndex, ref vertexesArray, ref indexArray, ref Heights);
            tValue += (uint)vertexCount;
            CreateBordering(new int2(i + 1, MapHeightWidth - 1), new int2(i, MapHeightWidth - 1), tValue,
                ref vIndex, ref tIndex, ref vertexesArray, ref indexArray, ref Heights);
            tValue += (uint)vertexCount;

            CreateBordering(new int2(0, i + 1), new int2(0, i), tValue,
                ref vIndex, ref tIndex, ref vertexesArray, ref indexArray, ref Heights);
            tValue += (uint)vertexCount;
            CreateBordering(new int2(MapHeightWidth - 1, i), new int2(MapHeightWidth - 1, i + 1), tValue,
                ref vIndex, ref tIndex, ref vertexesArray, ref indexArray, ref Heights);
            tValue += (uint)vertexCount;
        }
    }

    private void CreateBordering (int2 positionA, int2 positionB, uint tValue, ref int vIndex, ref int tIndex,
        ref NativeArray<float3> vertexesArray, ref NativeArray<uint> indexArray, ref NativeArray<int> Heights)
    {
        var v0 = new float3(positionA.x, -1, positionA.y) * mapComponent.TileWidth;
        vertexesArray[vIndex++] = v0;
        vertexesArray[vIndex++] = float3.zero;
        vertexesArray[vIndex++] = new float3(0f, 0f, 0f);

        var v1 = new float3(positionB.x, -1, positionB.y) * mapComponent.TileWidth;
        vertexesArray[vIndex++] = v1;
        vertexesArray[vIndex++] = float3.zero;
        vertexesArray[vIndex++] = new float3(1f, 0f, 0f);

        vertexesArray[vIndex++] = v1.WithY(Heights[GetHeightIndex(positionB.x, positionB.y)] * mapComponent.TileWidth);
        vertexesArray[vIndex++] = float3.zero;
        vertexesArray[vIndex++] = new float3(1f, 1f, 0f);

        vertexesArray[vIndex++] = v0.WithY(Heights[GetHeightIndex(positionA.x, positionA.y)] * mapComponent.TileWidth);
        vertexesArray[vIndex++] = float3.zero;
        vertexesArray[vIndex++] = new float3(0f, 1f, 0f);

        indexArray[tIndex++] = tValue;
        indexArray[tIndex++] = tValue + 2;
        indexArray[tIndex++] = tValue + 1;
        indexArray[tIndex++] = tValue;
        indexArray[tIndex++] = tValue + 3;
        indexArray[tIndex++] = tValue + 2;
    }
}



















[BurstCompile]
public partial struct GenerateTerrainWithDiamondSquareJob : IJob
{
    // RESULT TERRAIN HEIGHT
    public NativeArray<int> MapHeights;

    // TEMPORARY ARRAY FOR CALCULATION
    public NativeArray<float> CurrentHeight;

    [ReadOnly]
    public int TileWidth;

    [ReadOnly]
    public int MapHeightWidth;

    [ReadOnly]
    public int MaxHeight;

    [ReadOnly]
    public int MaxDepth;

    [ReadOnly]
    public Unity.Mathematics.Random RandomValue;

    [ReadOnly]
    public int Roughness;

    [WriteOnly]
    [NativeDisableContainerSafetyRestriction]
    public Mesh.MeshData GroundMeshData;

    [BurstCompile]
    public void Execute ()
    {
        CurrentHeight[GetHeightIndex(0, 0)] = RandomFloat();
        CurrentHeight[GetHeightIndex(MapHeightWidth - 1, 0)] = RandomFloat();
        CurrentHeight[GetHeightIndex(0, MapHeightWidth -1)] = RandomFloat();
        CurrentHeight[GetHeightIndex(MapHeightWidth -1, MapHeightWidth - 1)] = RandomFloat();

        var maxValue = 0f;

        DiamondSquareExecute(MapHeightWidth, Roughness, ref maxValue);

        for (int x = 0; x < MapHeightWidth; x++)
        {
            for (int y = 0; y < MapHeightWidth; y++)
            {
                var heightIndex = GetHeightIndex(x, y);
                var heightValue = math.unlerp(0f, maxValue, CurrentHeight[heightIndex]) * MaxHeight;
                if (heightValue >= MaxHeight)
                {
                    heightValue = MaxHeight - 1;
                }
                if (heightValue <= -MaxDepth)
                {
                    heightValue = -MaxDepth + 1;
                }
                MapHeights[heightIndex] = heightValue >= 0 ? (int)math.floor(heightValue) : (int)math.ceil(heightValue);
            }
        }

        CreateBorderingGround();
    }

    [BurstCompile]
    private void DiamondSquareExecute (int size, float roughness, ref float maxValue)
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
                DiamondStep(x, y, half, roughness, ref maxValue);
            }
        }

        DiamondSquareExecute(half, roughness / 2, ref maxValue);
    }

    [BurstCompile]
    private void DiamondStep (int x, int y, int half, float roughness, ref float maxValue)
    {
        var value = 0f;
        value += CurrentHeight[GetHeightIndex(x + half, y - half)];
        value += CurrentHeight[GetHeightIndex(x - half, y + half)];
        value += CurrentHeight[GetHeightIndex(x + half, y + half)];
        value += CurrentHeight[GetHeightIndex(x - half, y - half)];

        value /= 4;
        value += RandomFloat() * roughness;

        if (value > maxValue)
        {
            maxValue = value;
        }

        CurrentHeight[GetHeightIndex(x, y)] = value;

        SquareStep(x - half, y, half, roughness, ref maxValue);
        SquareStep(x + half, y, half, roughness, ref maxValue);
        SquareStep(x, y - half, half, roughness, ref maxValue);
        SquareStep(x, y + half, half, roughness, ref maxValue);
    }

    [BurstCompile]
    private void SquareStep (int x, int y, int half, float roughness, ref float maxValue)
    {
        var value = 0f;
        var count = 0;
        if (x - half >= 0)
        {
            value += CurrentHeight[GetHeightIndex(x - half, y)];
            count++;
        }
        if (x + half < MapHeightWidth)
        {
            value += CurrentHeight[GetHeightIndex(x + half, y)];
            count++;
        }
        if (y - half >= 0)
        {
            value += CurrentHeight[GetHeightIndex(x, y - half)];
            count++;
        }
        if (y + half < MapHeightWidth)
        {
            value += CurrentHeight[GetHeightIndex(x, y + half)];
            count++;
        }

        value /= count;
        value += RandomFloat() * roughness;

        if (value > maxValue)
        {
            maxValue = value;
        }

        CurrentHeight[GetHeightIndex(x, y)] = value;
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

    [BurstCompile]
    private void CreateBorderingGround ()
    {
        var vertexesArray = GroundMeshData.GetVertexData<float3>();
        var indexArray = GroundMeshData.GetIndexData<uint>();

        var vertexCount = 4;
        var vIndex = 0;
        var tIndex = 0;
        var tValue = 0u;

        for (int i = 0; i < MapHeightWidth - 1; i++)
        {
            CreateBordering(new int2(i, 0), new int2(i + 1, 0), tValue, 
                ref vIndex, ref tIndex, ref vertexesArray, ref indexArray);
            tValue += (uint)vertexCount;
            CreateBordering(new int2(i + 1, MapHeightWidth - 1), new int2(i, MapHeightWidth - 1), tValue, 
                ref vIndex, ref tIndex, ref vertexesArray, ref indexArray);
            tValue += (uint)vertexCount;

            CreateBordering(new int2(0, i + 1), new int2(0, i), tValue,
                ref vIndex, ref tIndex, ref vertexesArray, ref indexArray);
            tValue += (uint)vertexCount;
            CreateBordering(new int2(MapHeightWidth - 1, i), new int2(MapHeightWidth - 1, i + 1), tValue,
                ref vIndex, ref tIndex, ref vertexesArray, ref indexArray);
            tValue += (uint)vertexCount;
        }
    }

    [BurstCompile]
    private void CreateBordering (int2 positionA, int2 positionB, uint tValue, ref int vIndex, ref int tIndex, 
        ref NativeArray<float3> vertexesArray, ref NativeArray<uint> indexArray)
    {
        var v0 = new float3(positionA.x, -MaxDepth, positionA.y) * TileWidth;
        vertexesArray[vIndex++] = v0;
        vertexesArray[vIndex++] = float3.zero;
        vertexesArray[vIndex++] = new float3(0f, 0f, 0f);

        var v1 = new float3(positionB.x, -MaxDepth, positionB.y) * TileWidth;
        vertexesArray[vIndex++] = v1;
        vertexesArray[vIndex++] = float3.zero;
        vertexesArray[vIndex++] = new float3(1f, 0f, 0f);

        vertexesArray[vIndex++] = v1.WithY(MapHeights[GetHeightIndex(positionB.x, positionB.y)] * TileWidth);
        vertexesArray[vIndex++] = float3.zero;
        vertexesArray[vIndex++] = new float3(1f, 1f, 0f);

        vertexesArray[vIndex++] = v0.WithY(MapHeights[GetHeightIndex(positionA.x, positionA.y)] * TileWidth);
        vertexesArray[vIndex++] = float3.zero;
        vertexesArray[vIndex++] = new float3(0f, 1f, 0f);

        indexArray[tIndex++] = tValue;
        indexArray[tIndex++] = tValue + 2;
        indexArray[tIndex++] = tValue + 1;
        indexArray[tIndex++] = tValue;
        indexArray[tIndex++] = tValue + 3;
        indexArray[tIndex++] = tValue + 2;
    }
}