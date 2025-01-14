using BlobHashMaps;
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

    private Color GetColor (int h)
    {
        switch (h)
        {
            case 0: return Color.blue;
            case 1: return Color.green;
            case 2: return Color.yellow;
            case 3: return Color.grey;
            case 4: return Color.white;
            default: return Color.red;
        }
    }

    public void OnStartRunning (ref SystemState state)
    {
        mapComponent = SystemAPI.GetSingleton<MapComponent>();

        var mapTileComponent = SystemAPI.GetSingletonRW<MapTileComponent>();
        var mapTiles = mapTileComponent.ValueRW.TileData;
        var heigthMap = mapTileComponent.ValueRW.TileHeightMap;

        terrainEntity = SystemAPI.GetSingleton<RendererPrefabEntities>().tilePrefab;

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

        if (controllerData.ValueRO.State != ControllerState.LowerTerrain && controllerData.ValueRO.State != ControllerState.RaiseTerrain &&
            controllerData.ValueRO.State != ControllerState.LevelTerrain)
        {
            return;
        }

        var rectBuffer = SystemAPI.GetSingletonBuffer<RectChunkEntityBuffer>();

        if (rectBuffer.Length == 0)
        {
            return;
        }

        var mapTileComponent = SystemAPI.GetSingletonRW<MapTileComponent>();
        var mapTiles = mapTileComponent.ValueRW.TileData;
        var mapHeights = mapTileComponent.ValueRW.TileHeightMap;

        if (controllerData.ValueRO.State == ControllerState.LowerTerrain)
        {
            //var editRectPosition = controllerData.ValueRO.Rect.xy;
            //var editRectSize = controllerData.ValueRO.Rect.zw;

            //var height = -1;

            /*
            var setTerrainHeightJob = new SetTerrainHeightJob
            {
                MapTiles = mapTiles,
                MapTileDimension = mapComponent.TileDimension,
                TileWidth = mapComponent.TileWidth,
                RectPosition = editRectPosition,
                RectSize = editRectSize,
                Height = height
            };

            setTerrainHeightJob.Schedule(state.Dependency).Complete();
            */


            

        }

        var meshBlobInfoComponent = state.EntityManager.GetSharedComponentManaged<MeshBlobInfoComponent>(terrainEntity);
        var meshBlobDataComponent = state.EntityManager.GetSharedComponentManaged<MeshBlobDataComponent>(terrainEntity);
        var meshBlobTerrainTileMappingComponent = state.EntityManager.GetSharedComponentManaged<MeshBlobTileTerrainMappingComponent>(terrainEntity);
        var rendererChunkBuffer = SystemAPI.GetBuffer<ChunkRendererEntityBuffer>(terrainEntity);
        var entitiesGraphicsSystem = state.World.GetExistingSystemManaged<EntitiesGraphicsSystem>();
        var chunkTileCount = mapComponent.ChunkWidth * mapComponent.ChunkWidth;

        for (int i = 0; i < rectBuffer.Length; i++)
        {
            var chunkRect = rectBuffer[i].Value;
            var chunkPosition = chunkRect.chunkPosition;
            var chunkIndex = mapComponent.GetChunkIndexFromChunkPosition(chunkPosition);

            var chunkRendererEntity = rendererChunkBuffer[chunkIndex].Value;
            var materialMeshInfoComponent = SystemAPI.GetComponentRW<MaterialMeshInfo>(chunkRendererEntity);
            var chunkMeshId = materialMeshInfoComponent.ValueRO.MeshID;

            var chunkMesh = chunkMeshId.value > 0 ? entitiesGraphicsSystem.GetMesh(chunkMeshId) : new Mesh();
            var chunkDataArray = Mesh.AllocateWritableMeshData(chunkMesh);
            var chunkData = chunkDataArray[0];

            //Debug.Log(chunkRect.chunkRect.xy + " " + chunkRect.chunkRect.zw);

            var setTerrainInChunkRectJob = new SetTerrainInChunkRectJob
            {
                MapTiles = mapTiles,
                MapHeights = mapHeights,
                MapHeightTileTypeMapping = meshBlobTerrainTileMappingComponent,
                MapTileDimension = mapComponent.TileDimension,
                TileWidth = mapComponent.TileWidth,
                ChunkWidth = mapComponent.ChunkWidth,
                RectPosition = chunkRect.chunkRect.xy,
                RectSize = chunkRect.chunkRect.zw,
                MeshData = meshBlobDataComponent,
                MeshChunkData = chunkData
            };

            setTerrainInChunkRectJob.Schedule(state.Dependency).Complete();

            /*
            var vertexesArray = chunkData.GetVertexData<float3>();
            var indexArray = chunkData.GetIndexData<uint>();

            ref var vertexBlobArray = ref meshBlobDataComponent.meshDataBlob.Value.vertexes;
            ref var indexBlobArray = ref meshBlobDataComponent.meshDataBlob.Value.indexes;

            var vertexTileCount = 4;
            var vertexAttributeCount = 3;
            var indexTileCount = 6;

            var rPosition = chunkRect.chunkRect.xy;
            var rSize = chunkRect.chunkRect.zw;

            for (int m = 0; m < rSize.x; m++)
            {
                for (int n = 0; n < rSize.y; n++)
                {
                    var currentTilePosition = rPosition + new int2(m, n);
                    var tileIndex = GetTileIndexFromTilePosition(currentTilePosition);
                    if (!tileTypeHeightMapping.TryGetValue(mapTiles[tileIndex].heights, out var tileTerrainType))
                    {
                        tileTerrainType = 0;
                    }
                    //Debug.Log("tileTerrainType: " + tileTerrainType);
                    //Debug.Log("heghts: " + mapTiles[tileIndex].heights);
                    var tileWorldPosition = new float3(currentTilePosition.x, mapTiles[tileIndex].tileHeight, currentTilePosition.y);

                    var vertexIndex = GetVertexIndexFromTilePosition(currentTilePosition);

                    var vRead = vertexTileCount * vertexAttributeCount * tileTerrainType;
                    var vStart = vertexIndex * vertexAttributeCount * vertexTileCount;

                    for (int v = 0; v < vertexTileCount; v++)
                    {
                        // Position
                        vertexesArray[vStart++] = (vertexBlobArray[vRead++] + tileWorldPosition) * mapComponent.TileWidth;
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
            */

            UpdateMesh(ref state, entitiesGraphicsSystem, chunkRendererEntity, materialMeshInfoComponent, meshBlobInfoComponent,
                chunkDataArray, chunkMesh, chunkTileCount);
        }

        rectBuffer.Clear();

        controllerData.ValueRW.State = ControllerState.None;
    }

    //[BurstCompile]
    private int GetTileIndexFromTilePosition (int2 tilePosition)
    {
        return tilePosition.x * mapComponent.TileDimension.y + tilePosition.y;
    }

    //[BurstCompile]
    private int GetVertexIndexFromTilePosition (int2 tilePosition)
    {
        var vertexPosition = new int2(tilePosition.x % mapComponent.ChunkWidth, tilePosition.y % mapComponent.ChunkWidth);
        return vertexPosition.x * mapComponent.ChunkWidth + vertexPosition.y;
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

    /*
    private int GetMidHeightFromTile (TileData tileData)
    {
        return math.max(tileData.heights.x, math.max(tileData.heights.y, math.max(tileData.heights.z, tileData.heights.w)));
    }
    */
}

/*
[BurstCompile]
public partial struct SetTerrainHeightJob : IJob
{
    public NativeArray<TileData> MapTiles;

    [ReadOnly]
    public int2 MapTileDimension;

    [ReadOnly]
    public int TileWidth;

    [ReadOnly]
    public int2 RectPosition;

    [ReadOnly]
    public int2 RectSize;

    [ReadOnly]
    public int Height;

    [BurstCompile]
    public void Execute ()
    {
        // corners
        var tilePosition = RectPosition + new int2(-1, -1);
        var tileIndex = GetTileIndexFromTilePosition(tilePosition);
        var tileData = MapTiles[tileIndex];
        tileData.tileHeight += Height;
        tileData.heights.x = 1;
        tileData.heights.y = 1;
        tileData.heights.w = 1;
        MapTiles[tileIndex] = tileData;

        tilePosition = RectPosition + new int2(-1, RectSize.y);
        tileIndex = GetTileIndexFromTilePosition(tilePosition);
        tileData = MapTiles[tileIndex];
        tileData.tileHeight += Height;
        tileData.heights.x = 1;
        tileData.heights.z = 1;
        tileData.heights.w = 1;
        MapTiles[tileIndex] = tileData;

        tilePosition = RectPosition + new int2(RectSize.x, RectSize.y);
        tileIndex = GetTileIndexFromTilePosition(tilePosition);
        tileData = MapTiles[tileIndex];
        tileData.tileHeight += Height;
        tileData.heights.y = 1;
        tileData.heights.z = 1;
        tileData.heights.w = 1;
        MapTiles[tileIndex] = tileData;

        tilePosition = RectPosition + new int2(RectSize.x, -1);
        tileIndex = GetTileIndexFromTilePosition(tilePosition);
        tileData = MapTiles[tileIndex];
        tileData.tileHeight += Height;
        tileData.heights.x = 1;
        tileData.heights.y = 1;
        tileData.heights.z = 1;
        MapTiles[tileIndex] = tileData;

        for (int i = 0; i < RectSize.x; i++)
        {
            // horizontal borders
            tilePosition = RectPosition + new int2(i, -1);
            tileIndex = GetTileIndexFromTilePosition(tilePosition);
            tileData = MapTiles[tileIndex];
            tileData.tileHeight += Height;
            tileData.heights.x = 1;
            tileData.heights.y = 1;
            MapTiles[tileIndex] = tileData;

            tilePosition = RectPosition + new int2(i, RectSize.y);
            tileIndex = GetTileIndexFromTilePosition(tilePosition);
            tileData = MapTiles[tileIndex];
            tileData.tileHeight += Height;
            tileData.heights.z = 1;
            tileData.heights.w = 1;
            MapTiles[tileIndex] = tileData;

            for (int j = 0; j < RectSize.y; j++)
            {
                // center fill
                tilePosition = RectPosition + new int2(i, j);
                tileIndex = GetTileIndexFromTilePosition(tilePosition);
                tileData = MapTiles[tileIndex];
                tileData.tileHeight += Height;
                tileData.heights = int4.zero;
                MapTiles[tileIndex] = tileData;
            }
        }

        // vertical borders
        for (int j = 0; j < RectSize.y; j++)
        {
            tilePosition = RectPosition + new int2(-1, j);
            tileIndex = GetTileIndexFromTilePosition(tilePosition);
            tileData = MapTiles[tileIndex];
            tileData.tileHeight += Height;
            tileData.heights.x = 1;
            tileData.heights.w = 1;
            MapTiles[tileIndex] = tileData;

            tilePosition = RectPosition + new int2(RectSize.x, j);
            tileIndex = GetTileIndexFromTilePosition(tilePosition);
            tileData = MapTiles[tileIndex];
            tileData.tileHeight += Height;
            tileData.heights.y = 1;
            tileData.heights.z = 1;
            MapTiles[tileIndex] = tileData;
        }
    }

    [BurstCompile]
    private int GetTileIndexFromTilePosition (int2 tilePosition)
    {
        return tilePosition.x * MapTileDimension.y + tilePosition.y;
    }

    [BurstCompile]
    private int GetMinHeightFromTile (TileData tileData)
    {
        return math.min(tileData.heights.x, math.min(tileData.heights.y, math.min(tileData.heights.z, tileData.heights.w)));
    }
}
*/



[BurstCompile]
public partial struct SetTerrainInChunkRectJob : IJob
{
    public NativeArray<TileData> MapTiles;
    public NativeArray<int> MapHeights;

    [ReadOnly]
    public int2 MapTileDimension;

    [ReadOnly]
    public int TileWidth;

    [ReadOnly]
    public int2 RectPosition;

    [ReadOnly]
    public int2 RectSize;

    [ReadOnly]
    public int ChunkWidth;

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
                heights.x = MapHeights[GetHeigthMapIndexFromHeightMapPosition(tilePosition)];
                heights.y = MapHeights[GetHeigthMapIndexFromHeightMapPosition(tilePosition.GetEast())];
                heights.z = MapHeights[GetHeigthMapIndexFromHeightMapPosition(tilePosition.GetNorthEast())];
                heights.w = MapHeights[GetHeigthMapIndexFromHeightMapPosition(tilePosition.GetNorth())];
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