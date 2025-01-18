/*
 * Written by Fernando Arpini Ferretto
 * https://github.com/farpini/ProceduralTileChunkedMap_ECS
 */

using UnityEngine;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine.Rendering;
using Unity.Collections.LowLevel.Unsafe;

[UpdateAfter(typeof(GenerateTerrainSystem))]
public partial struct UpdateTerrainSystem : ISystem, ISystemStartStop
{
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
    }

    public void OnStartRunning (ref SystemState state)
    {
    }

    public void OnStopRunning (ref SystemState state)
    {
    }

    public void OnUpdate (ref SystemState state)
    {
        var controllerData = SystemAPI.GetSingletonRW<ControllerComponent>();

        if (controllerData.ValueRO.State != ControllerState.UpdateTerrain)
        {
            return;
        }

        mapComponent = SystemAPI.GetSingleton<MapComponent>();
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

                var updateTerrainInChunkRectJob = new UpdateTerrainInChunkRectJob
                {
                    MapTiles = mapTiles,
                    MapHeights = heigthMap,
                    MaxLevel = mapComponent.MaxLevel,
                    MapHeightMaxMinValue = mapTileComponent.ValueRO.MapHeightMaxMinValue,
                    MapHeightTileTypeMapping = meshBlobTerrainTileMappingComponent,
                    MapTileDimension = mapComponent.TileDimension,
                    TileWidth = mapComponent.TileWidth,
                    ChunkWidth = mapComponent.ChunkWidth,
                    RectPosition = chunkRectPosition,
                    RectSize = chunkRectSize,
                    MeshData = meshBlobDataComponent,
                    MeshChunkData = chunkData
                };

                updateTerrainInChunkRectJob.Schedule(state.Dependency).Complete();

                var rendererChunkEntity = rendererChunkBuffer[chunkIndex];

                var materialMeshInfoComponent = SystemAPI.GetComponentRW<MaterialMeshInfo>(rendererChunkEntity);
                materialMeshInfoComponent.ValueRW.Mesh = 0;

                UpdateMesh(ref state, entitiesGraphicsSystem, rendererChunkEntity, materialMeshInfoComponent, meshBlobInfoComponent,
                    chunkDataArray, chunkMesh, chunkTileCount);
            }
        }

        var groundMesh = new Mesh();
        var groundMeshArray = Mesh.AllocateWritableMeshData(groundMesh);
        var groundData = groundMeshArray[0];

        var quadsCount = (mapComponent.TileDimension.x * 2) + (mapComponent.TileDimension.y * 2);
        var vertexCount = quadsCount * 4;
        var indexCount = quadsCount * 6;

        groundData.SetVertexBufferParams(vertexCount, meshBlobInfoComponent.meshInfoBlob.Value.attributes.ToArray());
        groundData.SetIndexBufferParams(indexCount, IndexFormat.UInt32);

        CreateBorderingGround(groundData, mapTileComponent.ValueRO.MapHeightMaxMinValue, ref heigthMap);

        groundData.subMeshCount = 1;
        groundData.SetSubMesh(0, new SubMeshDescriptor(0, indexCount));

        Mesh.ApplyAndDisposeWritableMeshData(groundMeshArray, groundMesh, MeshUpdateFlags.Default);

        groundMesh.RecalculateNormals();
        groundMesh.RecalculateBounds();

        SystemAPI.ManagedAPI.GetSingleton<RefGameObject>().Map.SetMeshMap(groundMesh, mapComponent);

        controllerData.ValueRW.State = ControllerState.None;
    }

    [BurstCompile]
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

    [BurstCompile]
    private void CreateBorderingGround (Mesh.MeshData groundData, float2 heightMaxMinValue, ref NativeArray<float> heights)
    {
        var vertexesArray = groundData.GetVertexData<float3>();
        var indexArray = groundData.GetIndexData<uint>();

        var vertexCount = 4;
        var vIndex = 0;
        var tIndex = 0;
        var tValue = 0u;

        var mapHeightWidth = mapComponent.TileDimension.x + 1;

        for (int i = 0; i < mapHeightWidth - 1; i++)
        {
            CreateBordering(new int2(i, 0), new int2(i + 1, 0), tValue, mapComponent, mapHeightWidth, heightMaxMinValue,
                ref vIndex, ref tIndex, ref vertexesArray, ref indexArray, ref heights);
            tValue += (uint)vertexCount;
            CreateBordering(new int2(i + 1, mapHeightWidth - 1), new int2(i, mapHeightWidth - 1), tValue, mapComponent, mapHeightWidth, heightMaxMinValue,
                ref vIndex, ref tIndex, ref vertexesArray, ref indexArray, ref heights);
            tValue += (uint)vertexCount;
            CreateBordering(new int2(0, i + 1), new int2(0, i), tValue, mapComponent, mapHeightWidth, heightMaxMinValue,
                ref vIndex, ref tIndex, ref vertexesArray, ref indexArray, ref heights);
            tValue += (uint)vertexCount;
            CreateBordering(new int2(mapHeightWidth - 1, i), new int2(mapHeightWidth - 1, i + 1), tValue, mapComponent, mapHeightWidth, heightMaxMinValue,
                ref vIndex, ref tIndex, ref vertexesArray, ref indexArray, ref heights);
            tValue += (uint)vertexCount;
        }
    }

    [BurstCompile]
    private void CreateBordering (int2 positionA, int2 positionB, uint tValue,  MapComponent mapComponent, int mapHeightWidth, float2 heightMaxMinValue, 
        ref int vIndex, ref int tIndex, ref NativeArray<float3> vertexesArray, ref NativeArray<uint> indexArray, ref NativeArray<float> heights)
    {
        var v0 = new float3(positionA.x, -1, positionA.y) * mapComponent.TileWidth;
        vertexesArray[vIndex++] = v0;
        vertexesArray[vIndex++] = float3.zero;
        vertexesArray[vIndex++] = new float3(0f, 0f, 0f);

        var v1 = new float3(positionB.x, -1, positionB.y) * mapComponent.TileWidth;
        vertexesArray[vIndex++] = v1;
        vertexesArray[vIndex++] = float3.zero;
        vertexesArray[vIndex++] = new float3(1f, 0f, 0f);

        vertexesArray[vIndex++] = v1.WithY(
            mapComponent.GetHeightNormalized(heights[positionB.x * mapHeightWidth + positionB.y], heightMaxMinValue) * mapComponent.TileWidth);
        vertexesArray[vIndex++] = float3.zero;
        vertexesArray[vIndex++] = new float3(1f, 1f, 0f);

        vertexesArray[vIndex++] = v0.WithY(
            mapComponent.GetHeightNormalized(heights[positionA.x * mapHeightWidth + positionA.y], heightMaxMinValue) * mapComponent.TileWidth);
        vertexesArray[vIndex++] = float3.zero;
        vertexesArray[vIndex++] = new float3(0f, 1f, 0f);

        indexArray[tIndex++] = tValue;
        indexArray[tIndex++] = tValue + 2;
        indexArray[tIndex++] = tValue + 1;
        indexArray[tIndex++] = tValue;
        indexArray[tIndex++] = tValue + 3;
        indexArray[tIndex++] = tValue + 2;
    }

    [BurstCompile]
    private int GetHeightNormalized (float heightValue, int maxLevel, float2 heightMaxMinValue)
    {
        heightValue = math.unlerp(heightMaxMinValue.x, heightMaxMinValue.y, heightValue) * maxLevel;
        if (heightValue >= maxLevel)
        {
            heightValue = maxLevel - 1;
        }
        if (heightValue < 0f)
        {
            heightValue = 0;
        }
        return (int)math.floor(heightValue);
    }
}

[BurstCompile]
public partial struct UpdateTerrainInChunkRectJob : IJob
{
    public NativeArray<TileData> MapTiles;
    public NativeArray<float> MapHeights;

    [ReadOnly]
    public int2 MapTileDimension;

    [ReadOnly]
    public int TileWidth;

    [ReadOnly]
    public int MaxLevel;

    [ReadOnly]
    public int2 RectPosition;

    [ReadOnly]
    public int2 RectSize;

    [ReadOnly]
    public int ChunkWidth;

    [ReadOnly]
    public float2 MapHeightMaxMinValue;

    [ReadOnly]
    public MeshBlobDataComponent MeshData;

    [ReadOnly]
    public MeshBlobTileTerrainMappingComponent MapHeightTileTypeMapping;

    [WriteOnly]
    [NativeDisableContainerSafetyRestriction]
    public Mesh.MeshData MeshChunkData;


    [BurstCompile]
    public void Execute ()
    {
        var vertexesArray = MeshChunkData.GetVertexData<float3>();
        var indexArray = MeshChunkData.GetIndexData<uint>();

        ref var vertexBlobArray = ref MeshData.meshDataBlob.Value.vertexes;
        ref var indexBlobArray = ref MeshData.meshDataBlob.Value.indexes;

        var vertexTileCount = 4;
        var vertexAttributeCount = 3;
        var indexTileCount = 6;

        for (int i = 0; i < RectSize.x; i++)
        {
            for (int j = 0; j < RectSize.y; j++)
            {
                var tilePosition = RectPosition + new int2(i, j);
                var tileIndex = GetTileIndexFromTilePosition(tilePosition);

                var heights = int4.zero;
                heights.x = GetHeightNormalized(MapHeights[GetHeigthMapIndexFromHeightMapPosition(tilePosition)]);
                heights.y = GetHeightNormalized(MapHeights[GetHeigthMapIndexFromHeightMapPosition(tilePosition.GetEast())]);
                heights.z = GetHeightNormalized(MapHeights[GetHeigthMapIndexFromHeightMapPosition(tilePosition.GetNorthEast())]);
                heights.w = GetHeightNormalized(MapHeights[GetHeigthMapIndexFromHeightMapPosition(tilePosition.GetNorth())]);
                var heightMin = math.min(heights.x, math.min(heights.y, math.min(heights.z, heights.w)));

                heights.x -= heightMin;
                heights.y -= heightMin;
                heights.z -= heightMin;
                heights.w -= heightMin;

                var tileData = MapTiles[tileIndex];
                tileData.terrainLevel = heightMin;
                tileData.terrainHeight = (heights.x + heights.y + heights.z + heights.w) / 4f;
                MapTiles[tileIndex] = tileData;

                if (!MapHeightTileTypeMapping.mapping.Value.TryGetValue(heights, out var tileTerrainType))
                {
                    tileTerrainType = 0;
                }
                var tileWorldPosition = new float3(tilePosition.x, tileData.terrainLevel, tilePosition.y);

                var vertexIndex = GetVertexIndexFromTilePosition(tilePosition);

                var vRead = vertexTileCount * vertexAttributeCount * tileTerrainType;
                var vStart = vertexIndex * vertexAttributeCount * vertexTileCount;

                for (int v = 0; v < vertexTileCount; v++)
                {
                    // Position
                    vertexesArray[vStart++] = (vertexBlobArray[vRead++] + tileWorldPosition) * TileWidth;
                    // Normal
                    vertexesArray[vStart++] = vertexBlobArray[vRead++];
                    // UV
                    vertexesArray[vStart++] = vertexBlobArray[vRead++];
                }

                var tRead = indexTileCount * tileTerrainType;
                var tStart = vertexIndex * indexTileCount;
                var tValue = vertexIndex * vertexTileCount;

                for (int t = 0; t < indexTileCount; t++)
                {
                    indexArray[tStart++] = indexBlobArray[tRead++] + (uint)tValue;
                }
            }
        }
    }

    [BurstCompile]
    private int GetHeightNormalized (float heightValue)
    {
        heightValue = math.unlerp(MapHeightMaxMinValue.x, MapHeightMaxMinValue.y, heightValue) * MaxLevel;
        if (heightValue >= MaxLevel)
        {
            heightValue = MaxLevel - 1;
        }
        if (heightValue < 0f)
        {
            heightValue = 0;
        }
        return (int)math.floor(heightValue);
    }

    [BurstCompile]
    private int GetTileIndexFromTilePosition (int2 tilePosition)
    {
        return tilePosition.x * MapTileDimension.y + tilePosition.y;
    }

    [BurstCompile]
    private int GetHeigthMapIndexFromHeightMapPosition (int2 heighMapPosition)
    {
        return heighMapPosition.x * (MapTileDimension.y + 1) + heighMapPosition.y;
    }

    [BurstCompile]
    private int GetVertexIndexFromTilePosition (int2 tilePosition)
    {
        var vertexPosition = new int2(tilePosition.x % ChunkWidth, tilePosition.y % ChunkWidth);
        return vertexPosition.x * ChunkWidth + vertexPosition.y;
    }
}