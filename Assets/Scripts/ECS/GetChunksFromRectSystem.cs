/*
 * Written by Fernando Arpini Ferretto
 * https://github.com/farpini/ProceduralTileChunkedMap_ECS
 */

using System.Drawing;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;

public partial struct GetChunksFromRectSystem : ISystem, ISystemStartStop
{
    private MapComponent mapComponent;

    private NativeList<int2> chunkRendererIndexesToInstantiateChunkedModelRenderers;

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

        chunkRendererIndexesToInstantiateChunkedModelRenderers = new NativeList<int2>(mapComponent.ChunkDimension.x * mapComponent.ChunkDimension.y, Allocator.Persistent);
    }

    public void OnStopRunning (ref SystemState state)
    {
        chunkRendererIndexesToInstantiateChunkedModelRenderers.Dispose();
    }

    public void OnUpdate (ref SystemState state)
    {
        var controllerData = SystemAPI.GetSingletonRW<ControllerComponent>();

        if (controllerData.ValueRO.OnRectSelecting)
        {
            return;
        }

        if (controllerData.ValueRO.State != ControllerState.ChunkedModelSelectPlacement &&
            controllerData.ValueRO.State != ControllerState.ChunkedModelRemove)
        {
            return;
        }

        var rectBuffer = SystemAPI.GetSingletonBuffer<RectChunkEntityBuffer>();
        rectBuffer.Clear();

        var modelId = controllerData.ValueRO.ModelSelectedId;

        if (modelId < controllerData.ValueRO.ModelCount)
        {
            mapComponent = SystemAPI.GetSingleton<MapComponent>();

            //var modelEntity = chunkedModelDataEntityBuffer[modelId].Value;
            //var chunkRendererEntityBuffer = SystemAPI.GetBuffer<ChunkRendererEntityBuffer>(modelEntity);

            var terrainChunkedRenderersBuffer = SystemAPI.GetBuffer<ChunkRendererEntityBuffer>(SystemAPI.GetSingletonEntity<MapComponent>());

            var rect = controllerData.ValueRO.Rect;

            chunkRendererIndexesToInstantiateChunkedModelRenderers.Clear();

            var selectedRectangle = new Rectangle(rect.x, rect.y, rect.z, rect.w);

                //(controllerData.ValueRO.State == ControllerState.ChunkedModelSelectPlacement || controllerData.ValueRO.State == ControllerState.ChunkedModelRemove) ?
                //new Rectangle(rect.x, rect.y, rect.z, rect.w) : new Rectangle(rect.x - 1, rect.y - 1, rect.z + 2, rect.w + 2);

            for (int i = 0; i < mapComponent.ChunkDimension.x; i++)
            {
                for (int j = 0; j < mapComponent.ChunkDimension.y; j++)
                {
                    var chunkPosition = new int2(i, j);
                    var chunkRectanglePosition = chunkPosition * mapComponent.ChunkWidth;
                    var chunkRectangle = new Rectangle(
                        chunkRectanglePosition.x, chunkRectanglePosition.y, mapComponent.ChunkWidth, mapComponent.ChunkWidth);
                    var resultRectangle = Rectangle.Intersect(selectedRectangle, chunkRectangle);

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

                            var terrainChunkedModelRenderersBuffer = SystemAPI.GetBuffer<ChunkRendererEntityBuffer>(terrainChunkedRenderersBuffer[chunkIndex].Value);

                            if (terrainChunkedModelRenderersBuffer[modelId].Value == Entity.Null)
                            {
                                chunkRendererIndexesToInstantiateChunkedModelRenderers.Add(chunkPosition);
                            }
                        }
                    }
                }
            }

            if (chunkRendererIndexesToInstantiateChunkedModelRenderers.Length > 0)
            {
                var chunkTiles = mapComponent.ChunkWidth * mapComponent.ChunkWidth;

                var chunkedModelDataEntityBuffer = SystemAPI.GetSingletonBuffer<ModelDataEntityBuffer>();
                var chunkedModelRendererPrefab = chunkedModelDataEntityBuffer[modelId].Value;
                var meshBlobInfoComponent = state.EntityManager.GetSharedComponentManaged<MeshBlobInfoComponent>(chunkedModelRendererPrefab);
                var modelName = meshBlobInfoComponent.meshInfoBlob.Value.meshName.BlobCharToString();

                var chunkedModelRendererEntities = state.EntityManager.Instantiate(
                    chunkedModelRendererPrefab, chunkRendererIndexesToInstantiateChunkedModelRenderers.Length, Allocator.Temp);

                var ecb = new EntityCommandBuffer(Allocator.Temp);

                for (int i = 0; i < chunkedModelRendererEntities.Length; i++)
                {
                    var chunkPosition = chunkRendererIndexesToInstantiateChunkedModelRenderers[i];
                    var chunkIndex = mapComponent.GetChunkIndexFromChunkPosition(chunkPosition);
                    var terrainChunkedModelRenderersBuffer = SystemAPI.GetBuffer<ChunkRendererEntityBuffer>(terrainChunkedRenderersBuffer[chunkIndex].Value);

                    ecb.AddComponent(chunkedModelRendererEntities[i], new MeshChunkData
                    {
                        entity = chunkedModelRendererEntities[i],
                        meshModelId = modelId,
                        chunkPosition = chunkPosition,
                        mapping = new NativeHashMap<int, int>(chunkTiles, Allocator.Persistent),
                        invMapping = new NativeHashMap<int, int>(chunkTiles, Allocator.Persistent)
                    });

                    var materialMeshInfo = SystemAPI.GetComponentRW<MaterialMeshInfo>(chunkedModelRendererEntities[i]);
                    materialMeshInfo.ValueRW.Mesh = 0;

                    state.EntityManager.SetName(chunkedModelRendererEntities[i], modelName + "_[" + chunkPosition.x + "][" + chunkPosition.y + "]");

                    terrainChunkedModelRenderersBuffer[modelId] = new ChunkRendererEntityBuffer { Value = chunkedModelRendererEntities[i] };
                }

                ecb.Playback(state.EntityManager);

                chunkRendererIndexesToInstantiateChunkedModelRenderers.Clear();
            }
        }
    }
}