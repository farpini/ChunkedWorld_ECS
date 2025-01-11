using Unity.Entities;

public partial struct LoadModelSystem : ISystem
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
    }

    public void OnUpdate (ref SystemState state)
    {
        var mapComponent = SystemAPI.GetSingleton<MapComponent>();

        SetupRenderingModels(ref state, mapComponent);

        state.Enabled = false;
    }

    private void SetupRenderingModels (ref SystemState state, MapComponent mapComponent)
    {
        var chunkDimension = mapComponent.ChunkDimension;

        var modelDataEntityBuffer = SystemAPI.GetSingletonBuffer<ModelDataEntityBuffer>();

        for (int i = 0; i < modelDataEntityBuffer.Length; i++)
        {
            var modelEntity = modelDataEntityBuffer[i].Value;

            var modelChunkEntityBuffer = SystemAPI.GetBuffer<ChunkRendererEntityBuffer>(modelEntity);

            for (int x = 0; x < chunkDimension.x; x++)
            {
                for (int y = 0; y < chunkDimension.y; y++)
                {
                    modelChunkEntityBuffer.Add(new ChunkRendererEntityBuffer { Value = Entity.Null });
                }
            }

            state.EntityManager.SetComponentData(modelEntity, new ModelDataComponent { modelId = i });
        }
    }
}