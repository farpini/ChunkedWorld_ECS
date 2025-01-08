using System.Collections;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using static UnityEngine.Mesh;

public partial struct CreateModelSystem : ISystem, ISystemStartStop
{
    private MapComponent mapComponent;
    private NativeHashMap<int, Mesh.MeshDataArray> meshInfoMapping;

    public void OnCreate (ref SystemState state)
    {
        state.RequireForUpdate<MapComponent>();
        state.RequireForUpdate<ControllerComponent>();
        state.RequireForUpdate<ModelDataEntityBuffer>();
    }

    public void OnDestroy (ref SystemState state)
    {

    }

    public void OnStartRunning (ref SystemState state)
    {
        mapComponent = SystemAPI.GetSingleton<MapComponent>();

        meshInfoMapping = new NativeHashMap<int, Mesh.MeshDataArray>(
            mapComponent.ChunkDimension.x * mapComponent.ChunkDimension.y, Allocator.Persistent);
    }

    public void OnStopRunning (ref SystemState state)
    {
        meshInfoMapping.Dispose();
    }

    public void OnUpdate (ref SystemState state)
    {
        var controllerData = SystemAPI.GetSingletonRW<ControllerComponent>();

        if (controllerData.ValueRO.State != ControllerState.CreateModel)
        {
            return;
        }

        controllerData.ValueRW.State = ControllerState.None;

        var modelDataEntityBuffer = SystemAPI.GetSingletonBuffer<ModelDataEntityBuffer>();

        var modelId = controllerData.ValueRO.ModelSelectedId;

        if (modelId >= modelDataEntityBuffer.Length)
        {
            return;
        }

        var entitiesGraphicsSystem = state.World.GetExistingSystemManaged<EntitiesGraphicsSystem>();

        var ecb = new EntityCommandBuffer(Allocator.Temp);

        var modelEntity = modelDataEntityBuffer[modelId].Value;

        var meshDataComponent = SystemAPI.GetComponent<MeshDataComponent>(modelEntity);

        var rectPosition = new int2(controllerData.ValueRO.Rect.x, controllerData.ValueRO.Rect.y);
        var rectSize = new int2(controllerData.ValueRO.Rect.z, controllerData.ValueRO.Rect.w);

        MeshChunkData meshChunkData = null;
        BatchMeshID chunkMeshId = BatchMeshID.Null;
        var chunkRendererBufferEntity = SystemAPI.GetBuffer<ModelChunkEntityBuffer>(modelEntity);
        var lastChunkIndex = -1;

        meshInfoMapping.Clear();

        for (int i = 0; i < rectSize.x; i++)
        {
            for (int j = 0; j < rectSize.y; j++)
            {
                var tilePosition = rectPosition + new int2(i, j);
                var chunkPosition = mapComponent.GetChunkFromTilePosition(tilePosition);
                var chunkIndex = mapComponent.GetChunkIndexFromChunkPosition(chunkPosition);

                if (chunkIndex != lastChunkIndex)
                {
                    meshChunkData = state.EntityManager.GetComponentObject<MeshChunkData>(chunkRendererBufferEntity[chunkIndex]);
                    chunkMeshId = SystemAPI.GetComponent<MaterialMeshInfo>(chunkRendererBufferEntity[chunkIndex]).MeshID;
                    //m_FloorEntitiesToRefresh.Add(chunkRendererBufferEntity[chunkIndex]);
                    lastChunkIndex = chunkIndex;
                }

                CreateModel(ref state, chunkRendererBufferEntity[chunkIndex], tilePosition, chunkIndex, meshChunkData, 
                    meshDataComponent, chunkMeshId, entitiesGraphicsSystem);
            }
        }

        foreach (var meshDataArray in meshInfoMapping)
        {
            var chunkRendererEntity = chunkRendererBufferEntity[meshDataArray.Key];

            var materialMeshInfo = SystemAPI.GetComponentRW<MaterialMeshInfo>(chunkRendererEntity);

            if (materialMeshInfo.ValueRO.Mesh > 0)
            {
                entitiesGraphicsSystem.UnregisterMesh(materialMeshInfo.ValueRO.MeshID);
            }
           
            meshChunkData = state.EntityManager.GetComponentObject<MeshChunkData>(chunkRendererEntity);
            var modelCount = meshChunkData.mapping.Count;

            var vertexCount = modelCount * meshDataComponent.vertexCount;
            var indexCount = modelCount * meshDataComponent.indexCount;

            var meshData = meshDataArray.Value[0];

            meshData.SetVertexBufferParams(vertexCount, meshDataComponent.meshDataBlob.Value.attributes.ToArray());
            meshData.SetIndexBufferParams(indexCount, IndexFormat.UInt32);

            meshData.subMeshCount = 1;
            meshData.SetSubMesh(0, new SubMeshDescriptor(0, indexCount)); // check here the value

            Mesh mesh = new Mesh();

            Mesh.ApplyAndDisposeWritableMeshData(
                meshDataArray.Value,
                mesh,
                MeshUpdateFlags.Default);

            mesh.RecalculateBounds();

            SystemAPI.GetComponentRW<RenderBounds>(chunkRendererEntity).ValueRW.Value = mesh.bounds.ToAABB();

            materialMeshInfo.ValueRW.MeshID = entitiesGraphicsSystem.RegisterMesh(mesh);

            if (SystemAPI.HasComponent<DisableRendering>(chunkRendererEntity))
            {
                ecb.RemoveComponent<DisableRendering>(chunkRendererEntity);
            }
        }

        ecb.Playback(state.EntityManager);
    }

    private void CreateModel (ref SystemState state, Entity chunkRendererEntity, int2 tilePosition, int chunkIndex, MeshChunkData meshChunkData, 
        MeshDataComponent meshDataComponent, BatchMeshID chunkMeshId, EntitiesGraphicsSystem entitiesGraphicsSystem)
    {
        var modelIndex = mapComponent.GetTileIndexFromTilePosition(tilePosition);

        var modelWorldPosition = new float3(
            tilePosition.x * mapComponent.TileWidth + mapComponent.HalfTileWidth, 
            0f, 
            tilePosition.y * mapComponent.TileWidth + mapComponent.HalfTileWidth);

        if (meshChunkData.mapping.ContainsKey(modelIndex))
        {
            return;
        }

        if (!meshInfoMapping.TryGetValue(chunkIndex, out var meshDataArray))
        {
            //var mesh = entitiesGraphicsSystem.GetMesh(chunkMeshId);
            //var mesh = renderMeshArray.GetMesh(SystemAPI.GetComponent<MaterialMeshInfo>(chunkRendererEntity));
            //if (mesh == null)
            //{
            //    mesh = new Mesh();
            //    mesh.indexFormat = IndexFormat.UInt32;
            //}

            meshDataArray = Mesh.AllocateWritableMeshData(chunkMeshId.value > 0 ? entitiesGraphicsSystem.GetMesh(chunkMeshId) : new Mesh());
            var meshDataInit = meshDataArray[0];

            var possibleMaxModelCount = mapComponent.ChunkWidth * mapComponent.ChunkWidth;

            meshDataInit.SetVertexBufferParams(possibleMaxModelCount * meshDataComponent.vertexCount, 
                meshDataComponent.meshDataBlob.Value.attributes.ToArray());
            meshDataInit.SetIndexBufferParams(possibleMaxModelCount * meshDataComponent.indexCount, IndexFormat.UInt32);

            meshInfoMapping.Add(chunkIndex, meshDataArray);
        }

        var meshData = meshDataArray[0];

        var verticeArray = meshData.GetVertexData<float>();
        var indexArray = meshData.GetIndexData<uint>();

        var currentModelCount = meshChunkData.mapping.Count;
        var currentVertexCount = currentModelCount * meshDataComponent.vertexCount;
        var currentIndexCount = currentModelCount * meshDataComponent.indexCount;

        var vIndex = currentModelCount * meshDataComponent.vertexCount * meshDataComponent.vertexAttributeDimension;
        var tIndex = currentIndexCount;
        var tValue = (uint)currentVertexCount;

        ref var vertexBlobArray = ref meshDataComponent.meshDataBlob.Value.vertexes;
        ref var indexBlobArray = ref meshDataComponent.meshDataBlob.Value.indexes;

        for (int v = 0, vIdx = 0; v < meshDataComponent.vertexCount; v++)
        {
            // Position
            verticeArray[vIndex++] = vertexBlobArray[vIdx++] + modelWorldPosition.x;
            verticeArray[vIndex++] = vertexBlobArray[vIdx++] + modelWorldPosition.y;
            verticeArray[vIndex++] = vertexBlobArray[vIdx++] + modelWorldPosition.z;

            // Normal
            verticeArray[vIndex++] = vertexBlobArray[vIdx++];
            verticeArray[vIndex++] = vertexBlobArray[vIdx++];
            verticeArray[vIndex++] = vertexBlobArray[vIdx++];

            // Tangent
            verticeArray[vIndex++] = vertexBlobArray[vIdx++];
            verticeArray[vIndex++] = vertexBlobArray[vIdx++];
            verticeArray[vIndex++] = vertexBlobArray[vIdx++];
            verticeArray[vIndex++] = vertexBlobArray[vIdx++];

            // UV
            verticeArray[vIndex++] = vertexBlobArray[vIdx++];
            verticeArray[vIndex++] = vertexBlobArray[vIdx++];
        }

        //Debug.LogWarning(vIndex);

        for (int t = 0; t < meshDataComponent.indexCount; t++)
        {
            indexArray[tIndex++] = indexBlobArray[t] + tValue;
        }

        //Debug.LogWarning(tIndex);

        //tValue += (uint)meshDataComponent.vertexCount;

        meshChunkData.mapping.Add(modelIndex, currentModelCount);
        meshChunkData.invMapping.Add(currentModelCount, modelIndex);



        //modelsCreated++;
    }
}