using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;

public partial struct LoadModelSystem : ISystem, ISystemStartStop
{
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
    }

    public void OnStartRunning (ref SystemState state)
    {
        


    }

    public void OnStopRunning (ref SystemState state)
    {
    }

    public void OnUpdate (ref SystemState state)
    {
        var entityCommandBuffer = new EntityCommandBuffer(Allocator.Temp);

        var mapComponent = SystemAPI.GetSingleton<MapComponent>();

        var rendererModePrefabEntity = SystemAPI.GetSingleton<RendererPrefabEntities>().modelRenderer;

        LoadModels(ref state, entityCommandBuffer, mapComponent, rendererModePrefabEntity);

        entityCommandBuffer.Playback(state.EntityManager);

        state.Enabled = false;
    }

    private void LoadModels (ref SystemState state, EntityCommandBuffer ecb, MapComponent mapComponent, Entity rendererPrefabEntity)
    {
        var chunkDimension = mapComponent.ChunkDimension;
        var chunkTiles = mapComponent.ChunkWidth * mapComponent.ChunkWidth;

        var modelDataEntityBuffer = SystemAPI.GetSingletonBuffer<ModelDataEntityBuffer>();

        /*
        var verticesEmpty = new NativeArray<float3>(4, Allocator.Temp);
        verticesEmpty[0] = new float3(0f, 0f, 0f);
        verticesEmpty[1] = new float3(10f, 0f, 0f);
        verticesEmpty[2] = new float3(10f, 0f, 10f);
        verticesEmpty[2] = new float3(0f, 0f, 10f);
        var indexesEmpty = new NativeArray<int>(6, Allocator.Temp);
        indexesEmpty[0] = 0;
        indexesEmpty[1] = 2;
        indexesEmpty[2] = 1;
        indexesEmpty[3] = 0;
        indexesEmpty[4] = 3;
        indexesEmpty[5] = 2;
        */

        for (int i = 0; i < modelDataEntityBuffer.Length; i++)
        {
            var modelEntity = modelDataEntityBuffer[i].Value;

            var modelBlobDataComponent = SystemAPI.GetComponentRO<MeshDataComponent>(modelEntity);

            var modelName = modelBlobDataComponent.ValueRO.meshDataBlob.Value.meshName.BlobCharToString();

            var modelChunkEntityBuffer = SystemAPI.GetBuffer<ModelChunkEntityBuffer>(modelEntity);

            var chunkEntities = state.EntityManager.Instantiate(rendererPrefabEntity, chunkDimension.x * chunkDimension.y, Allocator.Temp);
            var chunkIdx = 0;

            for (int x = 0; x < chunkDimension.x; x++)
            {
                for (int y = 0; y < chunkDimension.y; y++)
                {
                    var chunkPosition = new int2(x, y);

                    ecb.AddComponent(chunkEntities[chunkIdx], new MeshChunkData
                    {
                        entity = chunkEntities[chunkIdx],
                        meshModelId = i,
                        chunkPosition = chunkPosition,
                        mapping = new NativeHashMap<int, int>(chunkTiles, Allocator.Persistent),
                        invMapping = new NativeHashMap<int, int>(chunkTiles, Allocator.Persistent)
                    });

                    var materialMeshInfo = SystemAPI.GetComponentRW<MaterialMeshInfo>(chunkEntities[chunkIdx]);
                    materialMeshInfo.ValueRW.Mesh = 0;

                    /*
                    var materialMeshInfo = SystemAPI.GetComponentRW<MaterialMeshInfo>(chunkEntities[chunkIdx]);
                    var newMesh = new UnityEngine.Mesh();
                    newMesh.SetVertices(verticesEmpty);
                    newMesh.SetIndices(indexesEmpty, UnityEngine.MeshTopology.Triangles, 0);
                    newMesh.RecalculateNormals();
                    newMesh.RecalculateBounds();
                    materialMeshInfo.ValueRW.Mesh = (int)entitiesGraphicsSystem.RegisterMesh(newMesh).value;
                    */

                    //materialMeshInfo.ValueRW.MeshID = entitiesGraphicsSystem.RegisterMesh(new UnityEngine.Mesh());

                    //entityManager.SetComponentEnabled<MeshChunkRenderer>(chunkEntities[chunkIdx], false);

                    state.EntityManager.SetName(chunkEntities[chunkIdx], modelName + "_[" + chunkPosition.x + "][" + chunkPosition.y + "]");

                    modelChunkEntityBuffer.Add(new ModelChunkEntityBuffer { Value = chunkEntities[chunkIdx++] });
                }
            }

            state.EntityManager.SetComponentData(modelEntity, new ModelDataComponent { modelId = i });

            chunkEntities.Dispose();

            
        }
    }
}