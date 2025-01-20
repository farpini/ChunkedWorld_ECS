/*
 * Written by Fernando Arpini Ferretto
 * https://github.com/farpini/ProceduralTileChunkedMap_ECS
 */

using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

[UpdateAfter(typeof(GetChunksFromRectSystem))]
public partial struct UpdateChunkedModelSystem : ISystem, ISystemStartStop
{
    private MapComponent mapComponent;

    public void OnCreate (ref SystemState state)
    {
        state.RequireForUpdate<MapComponent>();
        state.RequireForUpdate<ControllerComponent>();
        state.RequireForUpdate<ModelDataEntityBuffer>();
        state.RequireForUpdate<RectChunkEntityBuffer>();
    }

    public void OnDestroy (ref SystemState state)
    {
    }

    public void OnStartRunning (ref SystemState state)
    {
        mapComponent = SystemAPI.GetSingleton<MapComponent>();
    }

    public void OnStopRunning (ref SystemState state)
    {
    }

    public void OnUpdate (ref SystemState state)
    {
        var controllerData = SystemAPI.GetSingletonRW<ControllerComponent>();

        if (controllerData.ValueRO.OnRectSelecting)
        {
            return;
        }

        if (controllerData.ValueRO.State != ControllerState.ChunkedModelSelectPlacement && controllerData.ValueRO.State != ControllerState.ChunkedModelRemove)
        {
            return;
        }

        var rectBuffer = SystemAPI.GetSingletonBuffer<RectChunkEntityBuffer>();

        if (rectBuffer.Length == 0)
        {
            return;
        }

        mapComponent = SystemAPI.GetSingleton<MapComponent>();

        var modelDataEntityBuffer = SystemAPI.GetSingletonBuffer<ModelDataEntityBuffer>();

        var modelId = controllerData.ValueRO.ModelSelectedId;

        if (modelId < modelDataEntityBuffer.Length)
        {
            if (controllerData.ValueRO.State == ControllerState.ChunkedModelSelectPlacement)
            {
                CreateModelInChunks(ref state, modelId, controllerData.ValueRO.ModelCount, modelDataEntityBuffer,
                    rectBuffer.AsNativeArray().Reinterpret<RectChunkData>());
            }
            else if (controllerData.ValueRO.State == ControllerState.ChunkedModelRemove)
            {
                RemoveModelInChunks(ref state, controllerData.ValueRO.ModelCount, modelDataEntityBuffer,
                    rectBuffer.AsNativeArray().Reinterpret<RectChunkData>());
            }
        }

        rectBuffer.Clear();

        //controllerData.ValueRW.State = ControllerState.None;
        controllerData.ValueRW.OnRectSelecting = true;
    }

    private void RemoveModelInChunks (ref SystemState state, int modelCount,
        DynamicBuffer<ModelDataEntityBuffer> modelDataEntityBuffer, NativeArray<RectChunkData> chunksRect)
    {
        var entitiesGraphicsSystem = state.World.GetExistingSystemManaged<EntitiesGraphicsSystem>();

        var chunkTileCount = mapComponent.ChunkWidth * mapComponent.ChunkWidth;

        var modelArrayOnTilesToRemove = new NativeArray<NativeList<int>>(modelCount, Allocator.Persistent);
        for (int i = 0; i < modelArrayOnTilesToRemove.Length; i++)
        {
            modelArrayOnTilesToRemove[i] = new NativeList<int>(chunkTileCount, Allocator.Persistent);
        }

        var terrainChunkedRenderersBuffer = SystemAPI.GetBuffer<ChunkRendererEntityBuffer>(SystemAPI.GetSingletonEntity<MapComponent>());

        foreach (var chunkRect in chunksRect)
        {
            var chunkIndex = mapComponent.GetChunkIndexFromChunkPosition(chunkRect.chunkPosition);
            var chunkedModelRendererBuffer = SystemAPI.GetBuffer<ChunkRendererEntityBuffer>(terrainChunkedRenderersBuffer[chunkIndex].Value);

            var getModelArrayOnTileToRemoveJob = new GetModelArrayOnTileToRemoveJob
            {
                MapTiles = SystemAPI.GetSingletonRW<MapTileComponent>().ValueRW.TileData,

                RectPosition = chunkRect.chunkRect.xy,
                RectSize = chunkRect.chunkRect.zw,
                MapTileDimension = mapComponent.TileDimension,
                ModelArrayOnTilesToRemove = modelArrayOnTilesToRemove
            };

            getModelArrayOnTileToRemoveJob.Schedule(state.Dependency).Complete();

            for (int i = 0; i < modelArrayOnTilesToRemove.Length; i++)
            {
                if (modelArrayOnTilesToRemove[i].Length > 0)
                {
                    var modelId = i;
                    var modelEntity = modelDataEntityBuffer[modelId].Value;
                    var chunkedModelRendererEntity = chunkedModelRendererBuffer[modelId].Value;

                    var meshBloblInfoComponent_ToRemove = state.EntityManager.GetSharedComponentManaged<MeshBlobInfoComponent>(modelEntity);

                    RemoveModelInChunk(ref state, modelArrayOnTilesToRemove[i].AsArray(), chunkedModelRendererEntity, meshBloblInfoComponent_ToRemove,
                        entitiesGraphicsSystem);
                }

                modelArrayOnTilesToRemove[i].Clear();
            }
        }

        for (int i = 0; i < modelArrayOnTilesToRemove.Length; i++)
        {
            modelArrayOnTilesToRemove[i].Dispose();
        }
        modelArrayOnTilesToRemove.Dispose();
    }

    private void RemoveModelInChunk (ref SystemState state, NativeArray<int> tilesToRemove, Entity chunkedModelRendererEntity,
        MeshBlobInfoComponent meshDataComponent, EntitiesGraphicsSystem entitiesGraphicsSystem)
    {
        var meshChunkData = state.EntityManager.GetComponentObject<MeshChunkData>(chunkedModelRendererEntity);

        var materialMeshInfoComponent = SystemAPI.GetComponentRW<MaterialMeshInfo>(chunkedModelRendererEntity);
        var chunkMeshId = materialMeshInfoComponent.ValueRO.MeshID;

        var chunkMesh = chunkMeshId.value > 0 ? entitiesGraphicsSystem.GetMesh(chunkMeshId) : new Mesh();
        var chunkDataArray = Mesh.AllocateWritableMeshData(chunkMesh);
        var chunkData = chunkDataArray[0];

        var removeModelInChunkJob = new RemoveModelInChunkJob
        {
            Mapping = meshChunkData.mapping,
            InvMapping = meshChunkData.invMapping,
            ModelTilesToRemove = tilesToRemove,
            VertexCount = meshDataComponent.vertexCount,
            VertexAttributeDimension = meshDataComponent.vertexAttributeDimension,
            MeshChunkData = chunkData
        };

        removeModelInChunkJob.Schedule(state.Dependency).Complete();

        UpdateMesh(ref state, entitiesGraphicsSystem, chunkedModelRendererEntity, materialMeshInfoComponent, meshDataComponent, chunkDataArray,
            chunkMesh, meshChunkData.mapping.Count);
    }

    private void CreateModelInChunks (ref SystemState state, int modelId, int modelCount, 
        DynamicBuffer<ModelDataEntityBuffer> modelDataEntityBuffer, NativeArray<RectChunkData> chunksRect)
    {
        var entitiesGraphicsSystem = state.World.GetExistingSystemManaged<EntitiesGraphicsSystem>();

        var chunkTileCount = mapComponent.ChunkWidth * mapComponent.ChunkWidth;

        var modelArrayOnTilesToRemove = new NativeArray<NativeList<int>>(modelCount, Allocator.Persistent);
        for (int i = 0; i < modelArrayOnTilesToRemove.Length; i++)
        {
            modelArrayOnTilesToRemove[i] = new NativeList<int>(chunkTileCount, Allocator.Persistent);
        }

        var modelEntity = modelDataEntityBuffer[modelId].Value;
        var terrainChunkedRenderersBuffer = SystemAPI.GetBuffer<ChunkRendererEntityBuffer>(SystemAPI.GetSingletonEntity<MapComponent>());

        var modelRenderMeshArray = state.EntityManager.GetSharedComponentManaged<RenderMeshArray>(modelEntity);
        var modelDataArray = Mesh.AcquireReadOnlyMeshData(modelRenderMeshArray.GetMesh(SystemAPI.GetComponent<MaterialMeshInfo>(modelEntity)));

        var meshBlobInfoComponent= state.EntityManager.GetSharedComponentManaged<MeshBlobInfoComponent>(modelEntity);

        //Debug.LogWarning("VertexCount:" + meshBlobInfoComponent.vertexCount);
        //Debug.LogWarning("IndexCount:" + meshBlobInfoComponent.indexCount);

        foreach (var chunkRect in chunksRect)
        {
            var chunkIndex = mapComponent.GetChunkIndexFromChunkPosition(chunkRect.chunkPosition);
            var chunkedModelRendererBuffer = SystemAPI.GetBuffer<ChunkRendererEntityBuffer>(terrainChunkedRenderersBuffer[chunkIndex].Value);
            var chunkedModelRendererEntity = chunkedModelRendererBuffer[modelId].Value;

            if (chunkedModelRendererEntity == Entity.Null)
            {
                var modelName = meshBlobInfoComponent.meshInfoBlob.Value.meshName.BlobCharToString();
                Debug.LogWarning("Its null " + chunkIndex + " " + modelName);
                continue;
            }

            CreateModelInChunk(ref state, modelId, chunkIndex, chunkRect.chunkRect, chunkedModelRendererEntity, meshBlobInfoComponent, 
                entitiesGraphicsSystem, modelDataArray, ref modelArrayOnTilesToRemove);

            for (int i = 0; i < modelArrayOnTilesToRemove.Length; i++)
            {
                if (modelArrayOnTilesToRemove[i].Length > 0 && i != modelId)
                {
                    var modelId_ToRemove = i;
                    var modelEntity_ToRemove = modelDataEntityBuffer[modelId_ToRemove].Value;

                    var chunkedModelRendererEntity_ToRemove = chunkedModelRendererBuffer[modelId_ToRemove].Value;

                    var meshBloblInfoComponent_ToRemove = state.EntityManager.GetSharedComponentManaged<MeshBlobInfoComponent>(modelEntity_ToRemove);

                    RemoveModelInChunk(ref state, modelArrayOnTilesToRemove[i].AsArray(), chunkedModelRendererEntity_ToRemove,
                        meshBloblInfoComponent_ToRemove, entitiesGraphicsSystem);
                }

                modelArrayOnTilesToRemove[i].Clear();
            }
        }

        for (int i = 0; i < modelArrayOnTilesToRemove.Length; i++)
        {
            modelArrayOnTilesToRemove[i].Dispose();
        }
        modelArrayOnTilesToRemove.Dispose();

        modelDataArray.Dispose();
    }

    private void CreateModelInChunk (ref SystemState state, int modelId, int chunkIndex, int4 chunkRect, Entity chunkRendererEntity, 
        MeshBlobInfoComponent meshDataComponent, EntitiesGraphicsSystem entitiesGraphicsSystem, Mesh.MeshDataArray modelDataArray,
        ref NativeArray<NativeList<int>> modelArrayOnTilesToRemove)
    {
        var meshChunkData = state.EntityManager.GetComponentObject<MeshChunkData>(chunkRendererEntity);

        var materialMeshInfoComponent = SystemAPI.GetComponentRW<MaterialMeshInfo>(chunkRendererEntity);
        var chunkMeshId = materialMeshInfoComponent.ValueRO.MeshID;

        var chunkMesh = chunkMeshId.value > 0 ? entitiesGraphicsSystem.GetMesh(chunkMeshId) : new Mesh();
        var chunkDataArray = Mesh.AllocateWritableMeshData(chunkMesh);
        var chunkData = chunkDataArray[0];

        var possibleMaxModelCount = mapComponent.ChunkWidth * mapComponent.ChunkWidth;

        chunkData.SetVertexBufferParams(possibleMaxModelCount * meshDataComponent.vertexCount,
            meshDataComponent.meshInfoBlob.Value.attributes.ToArray());
        chunkData.SetIndexBufferParams(possibleMaxModelCount * meshDataComponent.indexCount, IndexFormat.UInt32);

        var createModelInChunkRectJob = new CreateModelInChunkRectJob
        {
            MapTiles = SystemAPI.GetSingletonRW<MapTileComponent>().ValueRW.TileData,

            Mapping = meshChunkData.mapping,
            InvMapping = meshChunkData.invMapping,
            RectPosition = chunkRect.xy,
            RectSize = chunkRect.zw,
            ModelId = modelId + 1,
            MapTileDimension = mapComponent.TileDimension,
            MaxDepth = mapComponent.MaxDepth,
            MapTileWidth = mapComponent.TileWidth,
            VertexCount = meshDataComponent.vertexCount,
            IndexCount = meshDataComponent.indexCount,
            VertexAttributeDimension = meshDataComponent.vertexAttributeDimension,
            MeshModelData = modelDataArray[0],
            MeshChunkData = chunkData,
            ModelArrayOnTilesToRemove = modelArrayOnTilesToRemove
        };

        createModelInChunkRectJob.Schedule(state.Dependency).Complete();

        UpdateMesh(ref state, entitiesGraphicsSystem, chunkRendererEntity, materialMeshInfoComponent, meshDataComponent, chunkDataArray,
            chunkMesh, meshChunkData.mapping.Count);
    }

    private void UpdateMesh (ref SystemState state, EntitiesGraphicsSystem entitiesGraphicsSystem, Entity rendererEntity,
    RefRW<MaterialMeshInfo> materialMeshInfoComponent, MeshBlobInfoComponent meshDataComponent,
    Mesh.MeshDataArray meshDataArray, Mesh mesh, int modelCount)
    {
        if (materialMeshInfoComponent.ValueRO.Mesh > 0)
        {
            entitiesGraphicsSystem.UnregisterMesh(materialMeshInfoComponent.ValueRO.MeshID);
        }

        var chunkModelCount = modelCount;

        var vertexCount = chunkModelCount * meshDataComponent.vertexCount;
        var indexCount = chunkModelCount * meshDataComponent.indexCount;

        var meshData = meshDataArray[0];

        meshData.SetVertexBufferParams(vertexCount, meshDataComponent.meshInfoBlob.Value.attributes.ToArray());
        meshData.SetIndexBufferParams(indexCount, IndexFormat.UInt32);

        meshData.subMeshCount = 1;
        meshData.SetSubMesh(0, new SubMeshDescriptor(0, indexCount));

        Mesh.ApplyAndDisposeWritableMeshData(meshDataArray, mesh, MeshUpdateFlags.Default);

        mesh.RecalculateBounds();

        SystemAPI.GetComponentRW<RenderBounds>(rendererEntity).ValueRW.Value = mesh.bounds.ToAABB();

        materialMeshInfoComponent.ValueRW.MeshID = entitiesGraphicsSystem.RegisterMesh(mesh);
    }
}

[BurstCompile]
public partial struct GetModelArrayOnTileToRemoveJob : IJob
{
    public NativeArray<TileData> MapTiles;

    [ReadOnly]
    public int2 RectPosition;

    [ReadOnly]
    public int2 RectSize;

    [ReadOnly]
    public int2 MapTileDimension;

    [WriteOnly]
    [NativeDisableContainerSafetyRestriction]
    public NativeArray<NativeList<int>> ModelArrayOnTilesToRemove;


    [BurstCompile]
    public void Execute ()
    {
        for (int i = 0; i < RectSize.x; i++)
        {
            for (int j = 0; j < RectSize.y; j++)
            {
                var tilePosition = RectPosition + new int2(i, j);
                var tileIndex = GetTileIndexFromTilePosition(tilePosition);

                var tileData = MapTiles[tileIndex];

                if (tileData.modelType != 0)
                {
                    ModelArrayOnTilesToRemove[tileData.modelType - 1].Add(tileIndex);
                    tileData.modelType = 0;
                    MapTiles[tileIndex] = tileData;
                }
            }
        }
    }

    [BurstCompile]
    private int GetTileIndexFromTilePosition (int2 tilePosition)
    {
        return tilePosition.x * MapTileDimension.y + tilePosition.y;
    }

    [BurstCompile]
    public int2 GetTilePositionFromTileIndex (int tileIndex)
    {
        return new int2(tileIndex / MapTileDimension.y, tileIndex % MapTileDimension.y);
    }
}

[BurstCompile]
public partial struct CreateModelInChunkRectJob : IJob
{
    public NativeArray<TileData> MapTiles;
    public NativeHashMap<int, int> Mapping;
    public NativeHashMap<int, int> InvMapping;

    [ReadOnly]
    public int2 RectPosition;

    [ReadOnly]
    public int2 RectSize;

    [ReadOnly]
    public int ModelId;

    [ReadOnly]
    public int2 MapTileDimension;

    [ReadOnly]
    public int MaxDepth;

    [ReadOnly]
    public int MapTileWidth;

    [ReadOnly]
    public int VertexCount;

    [ReadOnly]
    public int IndexCount;

    [ReadOnly]
    public int VertexAttributeDimension;

    [ReadOnly]
    public Mesh.MeshData MeshModelData;

    [WriteOnly]
    [NativeDisableContainerSafetyRestriction]
    public Mesh.MeshData MeshChunkData;

    [WriteOnly]
    [NativeDisableContainerSafetyRestriction]
    public NativeArray<NativeList<int>> ModelArrayOnTilesToRemove;


    [BurstCompile]
    public void Execute ()
    {
        var verticeArray = MeshChunkData.GetVertexData<float>();
        var indexArray = MeshChunkData.GetIndexData<uint>();

        var modelVerticeArray = MeshModelData.GetVertexData<float>();
        var modelIndexArray = MeshModelData.GetIndexData<uint>();

        var currentModelCount = Mapping.Count;

        var currentVertexCount = currentModelCount * VertexCount;
        var currentIndexCount = currentModelCount * IndexCount;

        var vIndex = currentModelCount * VertexCount * VertexAttributeDimension;
        var tIndex = currentIndexCount;
        var tValue = (uint)currentVertexCount;

        var halfTileWidth = MapTileWidth * 0.5f;

        for (int i = 0; i < RectSize.x; i++)
        {
            for (int j = 0; j < RectSize.y; j++)
            {
                var tilePosition = RectPosition + new int2(i, j);
                var tileIndex = GetTileIndexFromTilePosition(tilePosition);

                var tileData = MapTiles[tileIndex];

                var tileHeight = tileData.terrainLevel + tileData.terrainHeight;

                if (tileData.terrainLevel < MaxDepth)
                {
                    continue;
                }

                var modelWorldPosition = new float3(
                    tilePosition.x * MapTileWidth + halfTileWidth,
                    tileHeight * MapTileWidth,
                    tilePosition.y * MapTileWidth + halfTileWidth);

                if (tileData.modelType != ModelId)
                {
                    if (tileData.modelType != 0)
                    {
                        ModelArrayOnTilesToRemove[tileData.modelType - 1].Add(tileIndex);
                    }

                    tileData.modelType = ModelId;
                    MapTiles[tileIndex] = tileData;

                    if (Mapping.ContainsKey(tileIndex))
                    {
                        continue;
                    }

                    CreateModel(tileIndex, currentModelCount, modelWorldPosition, modelVerticeArray, modelIndexArray,
                        ref verticeArray, ref indexArray, ref vIndex, ref tIndex, ref tValue);

                    currentModelCount++;
                }
            }
        }
    }

    [BurstCompile]
    private void CreateModel (int modelIndex, int modelCount, float3 modelWorldPosition, NativeArray<float> modelVertexArray,
        NativeArray<uint> modelIndexArray, ref NativeArray<float> verticeArray, ref NativeArray<uint> indexArray, 
        ref int vIndex, ref int tIndex, ref uint tValue)
    {
        for (int v = 0, vIdx = 0; v < VertexCount; v++)
        {
            // Position
            verticeArray[vIndex++] = modelVertexArray[vIdx++] + modelWorldPosition.x;
            verticeArray[vIndex++] = modelVertexArray[vIdx++] + modelWorldPosition.y;
            verticeArray[vIndex++] = modelVertexArray[vIdx++] + modelWorldPosition.z;

            // Normal
            verticeArray[vIndex++] = modelVertexArray[vIdx++];
            verticeArray[vIndex++] = modelVertexArray[vIdx++];
            verticeArray[vIndex++] = modelVertexArray[vIdx++];

            // Tangent
            verticeArray[vIndex++] = modelVertexArray[vIdx++];
            verticeArray[vIndex++] = modelVertexArray[vIdx++];
            verticeArray[vIndex++] = modelVertexArray[vIdx++];
            verticeArray[vIndex++] = modelVertexArray[vIdx++];

            // UV
            verticeArray[vIndex++] = modelVertexArray[vIdx++];
            verticeArray[vIndex++] = modelVertexArray[vIdx++];
        }

        for (int t = 0; t < IndexCount; t++)
        {
            indexArray[tIndex++] = modelIndexArray[t] + tValue;
        }

        tValue += (uint)VertexCount;

        Mapping.Add(modelIndex, modelCount);
        InvMapping.Add(modelCount, modelIndex);
    }

    [BurstCompile]
    private int GetTileIndexFromTilePosition (int2 tilePosition)
    {
        return tilePosition.x * MapTileDimension.y + tilePosition.y;
    }

    [BurstCompile]
    public int2 GetTilePositionFromTileIndex (int tileIndex)
    {
        return new int2(tileIndex / MapTileDimension.y, tileIndex % MapTileDimension.y);
    }
}

[BurstCompile]
public partial struct RemoveModelInChunkJob : IJob
{
    public NativeHashMap<int, int> Mapping;
    public NativeHashMap<int, int> InvMapping;

    [ReadOnly]
    public NativeArray<int> ModelTilesToRemove;

    [ReadOnly]
    public int VertexCount;

    [ReadOnly]
    public int VertexAttributeDimension;

    [WriteOnly]
    [NativeDisableContainerSafetyRestriction]
    public Mesh.MeshData MeshChunkData;


    [BurstCompile]
    public void Execute ()
    {
        var verticeArray = MeshChunkData.GetVertexData<float>();

        for (int i = 0; i < ModelTilesToRemove.Length; i++)
        {
            var modelIndex = ModelTilesToRemove[i];

            if (Mapping.TryGetValue(modelIndex, out var modelId))
            {
                var vIndex = modelId * VertexCount * VertexAttributeDimension;

                var lastModelId = Mapping.Count - 1;

                if (!InvMapping.TryGetValue(lastModelId, out var lastModelIndex))
                {
                    //Debug.LogError("Consistency failed logic");
                }

                var vLastIndex = lastModelId * VertexCount * VertexAttributeDimension;

                Mapping.Remove(modelIndex);
                InvMapping.Remove(modelId);

                if (modelId != lastModelId)
                {
                    for (int v = 0; v < VertexCount; v++)
                    {
                        // Position
                        verticeArray[vIndex++] = verticeArray[vLastIndex++];
                        verticeArray[vIndex++] = verticeArray[vLastIndex++];
                        verticeArray[vIndex++] = verticeArray[vLastIndex++];

                        // Normal
                        verticeArray[vIndex++] = verticeArray[vLastIndex++];
                        verticeArray[vIndex++] = verticeArray[vLastIndex++];
                        verticeArray[vIndex++] = verticeArray[vLastIndex++];

                        // Tangent
                        verticeArray[vIndex++] = verticeArray[vLastIndex++];
                        verticeArray[vIndex++] = verticeArray[vLastIndex++];
                        verticeArray[vIndex++] = verticeArray[vLastIndex++];
                        verticeArray[vIndex++] = verticeArray[vLastIndex++];

                        // UV
                        verticeArray[vIndex++] = verticeArray[vLastIndex++];
                        verticeArray[vIndex++] = verticeArray[vLastIndex++];
                    }

                    Mapping[lastModelIndex] = modelId;

                    InvMapping.Remove(lastModelId);
                    InvMapping.Add(modelId, lastModelIndex);
                }
            }
        }
    }
}