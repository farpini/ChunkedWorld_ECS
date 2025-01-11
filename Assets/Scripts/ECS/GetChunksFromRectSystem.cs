using System.Drawing;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;

[UpdateBefore(typeof(EditModelSystem))]
public partial struct GetChunksFromRectSystem : ISystem, ISystemStartStop
{
    private MapComponent mapComponent;

    private NativeList<int2> chunkRendererIndexesToInstantiate;

    public void OnCreate (ref SystemState state)
    {
        state.RequireForUpdate<MapComponent>();
        state.RequireForUpdate<ControllerComponent>();
        state.RequireForUpdate<ModelDataEntityBuffer>();
        state.RequireForUpdate<RectChunkEntityBuffer>();
        state.RequireForUpdate<RendererPrefabEntities>();
    }

    public void OnDestroy (ref SystemState state)
    {
    }

    public void OnStartRunning (ref SystemState state)
    {
        mapComponent = SystemAPI.GetSingleton<MapComponent>();

        chunkRendererIndexesToInstantiate = new NativeList<int2>(mapComponent.ChunkDimension.x * mapComponent.ChunkDimension.y, Allocator.Persistent);
    }

    public void OnStopRunning (ref SystemState state)
    {
        chunkRendererIndexesToInstantiate.Dispose();
    }

    public void OnUpdate (ref SystemState state)
    {
        var controllerData = SystemAPI.GetSingletonRW<ControllerComponent>();

        if (controllerData.ValueRO.State != ControllerState.CreateModel && controllerData.ValueRO.State != ControllerState.RemoveModel)
        {
            return;
        }

        var rectBuffer = SystemAPI.GetSingletonBuffer<RectChunkEntityBuffer>();
        rectBuffer.Clear();

        var modelDataEntityBuffer = SystemAPI.GetSingletonBuffer<ModelDataEntityBuffer>();

        var modelId = controllerData.ValueRO.ModelSelectedId;

        if (modelId < modelDataEntityBuffer.Length)
        {
            var modelEntity = modelDataEntityBuffer[modelId].Value;
            var chunkRendererEntityBuffer = SystemAPI.GetBuffer<ChunkRendererEntityBuffer>(modelEntity);

            var rect = controllerData.ValueRO.Rect;

            chunkRendererIndexesToInstantiate.Clear();

            var createRectangle = new Rectangle(rect.x, rect.y, rect.z, rect.w);
            for (int i = 0; i < mapComponent.ChunkDimension.x; i++)
            {
                for (int j = 0; j < mapComponent.ChunkDimension.y; j++)
                {
                    var chunkPosition = new int2(i, j);
                    var chunkRectanglePosition = chunkPosition * mapComponent.ChunkWidth;
                    var chunkRectangle = new Rectangle(
                        chunkRectanglePosition.x, chunkRectanglePosition.y, mapComponent.ChunkWidth, mapComponent.ChunkWidth);
                    var resultRectangle = Rectangle.Intersect(createRectangle, chunkRectangle);

                    if (!resultRectangle.IsEmpty)
                    {
                        if (resultRectangle.Width > 0 && resultRectangle.Height > 0)
                        {
                            //controllerData.ValueRW.HasRect = true;

                            rectBuffer.Add(new RectChunkEntityBuffer
                            {
                                Value = new RectChunkData
                                {
                                    chunkPosition = chunkPosition,
                                    chunkRect = new int4(resultRectangle.X, resultRectangle.Y, resultRectangle.Width, resultRectangle.Height)
                                }
                            });

                            var chunkIndex = mapComponent.GetChunkIndexFromChunkPosition(chunkPosition);

                            if (chunkRendererEntityBuffer[chunkIndex] == Entity.Null)
                            {
                                chunkRendererIndexesToInstantiate.Add(chunkPosition);
                            }
                        }
                    }
                }
            }

            InstantiateChunkRenderers(ref state, modelEntity, modelId);
        }
    }

    private void InstantiateChunkRenderers (ref SystemState state, Entity modelEntity, int modelId)
    {
        if (chunkRendererIndexesToInstantiate.Length == 0)
        {
            return;
        }

        var ecb = new EntityCommandBuffer(Allocator.Temp);

        var chunkRendererEntityPrefab = SystemAPI.GetSingleton<RendererPrefabEntities>().chunkModelRenderer;

        var chunkTiles = mapComponent.ChunkWidth * mapComponent.ChunkWidth;

        var chunkRendererEntityBuffer = SystemAPI.GetBuffer<ChunkRendererEntityBuffer>(modelEntity);
        var meshBlobInfoComponent = state.EntityManager.GetSharedComponentManaged<MeshBlobInfoComponent>(modelEntity);
        //var meshBlobInfoComponent = SystemAPI.GetComponent<MeshBlobInfoComponent>(modelEntity);
        var modelName = meshBlobInfoComponent.meshInfoBlob.Value.meshName.BlobCharToString();

        var chunkRendererEntities = state.EntityManager.Instantiate(
            chunkRendererEntityPrefab, chunkRendererIndexesToInstantiate.Length, Allocator.Temp);

        for (int i = 0; i < chunkRendererIndexesToInstantiate.Length; i++)
        {
            var chunkPosition = chunkRendererIndexesToInstantiate[i];
            var chunkIndex = mapComponent.GetChunkIndexFromChunkPosition(chunkPosition);

            ecb.AddComponent(chunkRendererEntities[i], new MeshChunkData
            {
                entity = chunkRendererEntities[i],
                meshModelId = modelId,
                chunkPosition = chunkPosition,
                mapping = new NativeHashMap<int, int>(chunkTiles, Allocator.Persistent),
                invMapping = new NativeHashMap<int, int>(chunkTiles, Allocator.Persistent)
            });

            var materialMeshInfo = SystemAPI.GetComponentRW<MaterialMeshInfo>(chunkRendererEntities[i]);
            materialMeshInfo.ValueRW.Mesh = 0;

            state.EntityManager.SetName(chunkRendererEntities[i], modelName + "_[" + chunkPosition.x + "][" + chunkPosition.y + "]");

            chunkRendererEntityBuffer[chunkIndex] = new ChunkRendererEntityBuffer { Value = chunkRendererEntities[i] };
        }

        ecb.Playback(state.EntityManager);

        chunkRendererIndexesToInstantiate.Clear();
    }
}