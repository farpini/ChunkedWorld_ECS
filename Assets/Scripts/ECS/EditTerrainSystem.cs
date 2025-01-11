using System.Collections;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

[UpdateAfter(typeof(CreateTerrainSystem))]
public partial struct EditTerrainSystem : ISystem, ISystemStartStop
{
    private MapComponent mapComponent;
    private Entity terrainEntity;

    public void OnCreate (ref SystemState state)
    {
        state.RequireForUpdate<MapComponent>();
        state.RequireForUpdate<ControllerComponent>();
        state.RequireForUpdate<ModelDataEntityBuffer>();
        //state.RequireForUpdate<TerrainComponent>();
    }

    public void OnDestroy (ref SystemState state)
    {
    }

    public void OnStartRunning (ref SystemState state)
    {
        mapComponent = SystemAPI.GetSingleton<MapComponent>();

        terrainEntity = SystemAPI.GetSingleton<RendererPrefabEntities>().tilePrefab;
        //terrainEntity = SystemAPI.GetSingletonEntity<TerrainComponent>();

        var meshBlobInfoComponent = state.EntityManager.GetSharedComponentManaged<MeshBlobInfoComponent>(terrainEntity);
        var meshBlobDataComponent = state.EntityManager.GetSharedComponentManaged<MeshBlobDataComponent>(terrainEntity);

        //Debug.Log("VertexCount:" + meshBlobInfoComponent.vertexCount);
        //Debug.Log("IndexCount:" + meshBlobInfoComponent.indexCount);

        //var meshBlobInfoComponent = SystemAPI.GetComponent<MeshBlobInfoComponent>(terrainEntity);
        //var meshBlobDataComponent = SystemAPI.GetComponent<MeshBlobDataComponent>(terrainEntity);

        var rendererChunkBuffer = SystemAPI.GetBuffer<ChunkRendererEntityBuffer>(terrainEntity);

        var entitiesGraphicsSystem = state.World.GetExistingSystemManaged<EntitiesGraphicsSystem>();

        //Debug.Log("OnStartRunning");

        var chunkDimension = mapComponent.ChunkDimension;
        //var chunkDimension = new int2(1, 1);
        var chunkTileCount = mapComponent.ChunkWidth * mapComponent.ChunkWidth;
        var chunkRectSize = new int2(mapComponent.ChunkWidth, mapComponent.ChunkWidth);

        //Debug.Log("VertexCount:" + (chunkTileCount * meshBlobInfoComponent.vertexCount));
        //Debug.Log("IndexCount:" + (chunkTileCount * meshBlobInfoComponent.indexCount));

        var mapTiles = SystemAPI.GetSingletonRW<MapTileComponent>().ValueRW.TileData;

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
                    MapTileDimension = mapComponent.TileDimension,
                    TileWidth = mapComponent.TileWidth,
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

                //Debug.Log("Chunk " + chunkPosition);
            }
        }
    }

    public void OnStopRunning (ref SystemState state)
    {
    }

    public void OnUpdate (ref SystemState state)
    {
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
}

[BurstCompile]
public partial struct SetTerrainInChunkRectJob : IJob
{
    public NativeArray<int3> MapTiles;

    [ReadOnly]
    public int2 MapTileDimension;

    [ReadOnly]
    public int TileWidth;

    [ReadOnly]
    public int2 RectPosition;

    [ReadOnly]
    public int2 RectSize;

    [ReadOnly]
    public MeshBlobDataComponent MeshData;

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
        var vertexIndex = 0;

        for (int i = 0; i < RectSize.x; i++)
        {
            for (int j = 0; j < RectSize.y; j++)
            {
                var tilePosition = RectPosition + new int2(i, j);
                var tileIndex = GetTileIndexFromTilePosition(tilePosition);
                var tileTerrainType = MapTiles[tileIndex].x;
                var tileHeight = MapTiles[tileIndex].z;
                var tileWorldPosition = new float3(tilePosition.x, tileHeight, tilePosition.y);

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

                vertexIndex++;
            }
        }
    }

    [BurstCompile]
    private int GetTileIndexFromTilePosition (int2 tilePosition)
    {
        return tilePosition.x * MapTileDimension.y + tilePosition.y;
    }
}