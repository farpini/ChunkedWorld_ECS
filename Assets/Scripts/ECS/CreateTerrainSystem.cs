using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

public partial struct CreateTerrainSystem : ISystem
{
    public void OnCreate (ref SystemState state)
    {
        state.RequireForUpdate<MapComponent>();
        state.RequireForUpdate<ControllerComponent>();
        state.RequireForUpdate<ModelDataEntityBuffer>();
    }

    public void OnDestroy (ref SystemState state)
    {
        foreach (var meshData in SystemAPI.Query<MeshChunkData>())
        {
            meshData.Dispose();
        }

        SystemAPI.GetSingleton<MapTileComponent>().Dispose();
    }

    public void OnUpdate (ref SystemState state)
    {
        var mapComponent = SystemAPI.GetSingleton<MapComponent>();

        var chunkDimension = mapComponent.ChunkDimension;
        //var chunkDimension = new int2(1,1);

        var rendererPrefabEntities = SystemAPI.GetSingleton<RendererPrefabEntities>();

        var chunkRendererEntities = state.EntityManager.Instantiate(rendererPrefabEntities.chunkTileRenderer, 
            chunkDimension.x * chunkDimension.y, Allocator.Temp);

        //var terrainEntity = state.EntityManager.Instantiate(rendererPrefabEntities.tilePrefab);

        var terrainChunkEntityBuffer = SystemAPI.GetBuffer<ChunkRendererEntityBuffer>(rendererPrefabEntities.tilePrefab);

        var chunkIndex = 0;

        for (int i = 0; i < chunkDimension.x; i++)
        {
            for (int j = 0; j < chunkDimension.y; j++)
            {
                state.EntityManager.SetName(chunkRendererEntities[chunkIndex], "Terrain[" + i + "][" + j + "]");
                terrainChunkEntityBuffer.Add(new ChunkRendererEntityBuffer { Value = chunkRendererEntities[chunkIndex++] });
            }
        }

        state.Enabled = false;
    }
}