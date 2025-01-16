using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;

[UpdateBefore(typeof(GetChunksFromRectSystem))]
public partial struct GetRectInputSystem : ISystem, ISystemStartStop
{
    private MapComponent2 mapComponent;
    private MeshBlobDataComponent meshBlobDataComponent;
    private MeshBlobInfoComponent meshBlobInfoComponent;
    private MeshBlobTileTerrainMappingComponent meshBlobTileTerrainMappingComponent;
    private float2 worldPosition;
    private int2 currentTilePosition;
    private int2 startDragTilePosition;
    private int4 rect;
    public bool isRectValid;
    private Ray currentRay;
    private Plane mapPlane;
    private bool isDragging;
    private float2 INVALID_FLOAT2;
    private int2 INVALID_INT2;

    public void OnCreate (ref SystemState state)
    {
        state.RequireForUpdate<MapComponent2>();
        state.RequireForUpdate<ControllerComponent>();
        //state.RequireForUpdate<ModelDataEntityBuffer>();
        //state.RequireForUpdate<RectChunkEntityBuffer>();
        state.RequireForUpdate<RendererPrefabEntities>();
    }

    public void OnDestroy (ref SystemState state)
    {
    }

    public void OnStartRunning (ref SystemState state)
    {
        mapComponent = SystemAPI.GetSingleton<MapComponent2>();

        worldPosition = float2.zero;
        currentTilePosition = int2.zero;
        startDragTilePosition = int2.zero;
        rect = new int4(int2.zero, int2.zero);
        isDragging = false;
        isRectValid = false;
        mapPlane = new Plane(Vector3.up, Vector3.zero);
        INVALID_FLOAT2 = new float2(-1f, -1f);
        INVALID_INT2 = new int2(-1, -1);

        var terrainEntity = SystemAPI.GetSingleton<RendererPrefabEntities>().tilePrefab;
        meshBlobDataComponent = state.EntityManager.GetSharedComponentManaged<MeshBlobDataComponent>(terrainEntity);
        meshBlobInfoComponent = state.EntityManager.GetSharedComponentManaged<MeshBlobInfoComponent>(terrainEntity);
        meshBlobTileTerrainMappingComponent = state.EntityManager.GetSharedComponentManaged<MeshBlobTileTerrainMappingComponent>(terrainEntity);

        //chunkRendererIndexesToInstantiate = new NativeList<int2>(mapComponent.ChunkDimension.x * mapComponent.ChunkDimension.y, Allocator.Persistent);
    }

    public void OnStopRunning (ref SystemState state)
    {
        //chunkRendererIndexesToInstantiate.Dispose();
    }

    public void OnUpdate (ref SystemState state)
    {
        var controllerData = SystemAPI.GetSingletonRW<ControllerComponent>();

        if (!controllerData.ValueRO.OnRectSelecting)
        {
            return;
        }

        GetWorldPositions(ref state);

        if (Input.GetMouseButtonDown(0))
        {
            if (!EventSystem.current.IsPointerOverGameObject())
            {
                isDragging = true;

                var mapTiles = SystemAPI.GetSingletonRW<MapTileComponent>().ValueRW.TileData;

                if (GetTilePositionIntersection(ref mapTiles, out var tilePositionIntersected, out var tileIndexIntersected))
                {
                    startDragTilePosition = tilePositionIntersected;
                }
                else
                {
                    startDragTilePosition = currentTilePosition;
                }
            }
        }
        else if (Input.GetMouseButtonUp(0))
        {
            isDragging = false;

            if (!EventSystem.current.IsPointerOverGameObject())
            {
                if (isRectValid)
                {
                    controllerData.ValueRW.Rect = rect;
                    controllerData.ValueRW.OnRectSelecting = false;

                    SystemAPI.ManagedAPI.GetSingleton<RefGameObject>().RectView.SetMesh(new Mesh());
                }

                /*
                if (state == ControllerState.CreateModel)
                {
                    entityManager.SetComponentData(controllerEntity, new ControllerComponent
                    {
                        State = state,
                        OnRectSelecting = true,
                        Rect = new int4(rect.position.x, rect.position.y, rect.size.x, rect.size.y),
                        ModelCount = modelCount,
                        ModelSelectedId = currentModelSelectedId,
                        FloorSelectedTextureId = 0,
                        StartTile = new int2(startDragTilePosition.x, startDragTilePosition.y)
                    });
                }
                else if (state == ControllerState.RemoveModel)
                {
                    entityManager.SetComponentData(controllerEntity, new ControllerComponent
                    {
                        State = state,
                        OnRectSelecting = true,
                        Rect = new int4(rect.position.x, rect.position.y, rect.size.x, rect.size.y),
                        ModelCount = modelCount,
                        ModelSelectedId = currentModelSelectedId,
                        FloorSelectedTextureId = 0,
                        StartTile = new int2(startDragTilePosition.x, startDragTilePosition.y)
                    });
                }
                else if (state == ControllerState.LowerTerrain)
                {
                    entityManager.SetComponentData(controllerEntity, new ControllerComponent
                    {
                        State = state,
                        OnRectSelecting = true,
                        Rect = new int4(rect.position.x, rect.position.y, rect.size.x, rect.size.y),
                        ModelCount = modelCount,
                        ModelSelectedId = currentModelSelectedId,
                        FloorSelectedTextureId = 0,
                        StartTile = new int2(startDragTilePosition.x, startDragTilePosition.y)
                    });
                }
                */
            }
        }


    }

    private void GetWorldPositions (ref SystemState state)
    {
        currentRay = Camera.main.ScreenPointToRay(Input.mousePosition);

        var intersectPoint = math.up();

        if (mapPlane.Raycast(currentRay, out float distance))
        {
            intersectPoint = currentRay.GetPoint(distance);
        }

        var lastTilePosition = currentTilePosition;

        isRectValid = false;

        worldPosition = new float2(intersectPoint.x, intersectPoint.z);

        if (intersectPoint.y < 1f && IsWorldPositionValid(worldPosition))
        {
            currentTilePosition = new int2((int)(worldPosition.x / mapComponent.TileWidth), (int)(worldPosition.y / mapComponent.TileWidth));

            if (lastTilePosition.Equals(currentTilePosition))
            {
                OnTilePositionChanged(ref state, true);
            }
        }
        else
        {
            worldPosition = INVALID_FLOAT2;
            currentTilePosition = INVALID_INT2;

            if (lastTilePosition.Equals(currentTilePosition))
            {
                OnTilePositionChanged(ref state, false);
            }
        }
    }

    private void OnTilePositionChanged (ref SystemState state, bool positionValid)
    {
        if (EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        var rectPosition = int2.zero;
        var rectSize = int2.zero;

        var mapTileComponent = SystemAPI.GetSingletonRW<MapTileComponent>();

        var mapTiles = mapTileComponent.ValueRW.TileData;
        var mapHeights = mapTileComponent.ValueRW.TileHeightMap;

        if (GetTilePositionIntersection(ref mapTiles, out var tilePositionIntersected, out var tileIndexIntersected))
        {
            if (tileIndexIntersected >= 0)
            {
                var endDragTilePosition = tilePositionIntersected;

                if (isDragging)
                {
                    GetRectFromTwoPoints(startDragTilePosition, endDragTilePosition, out rectPosition, out rectSize);
                    //rect = new RectInt(rectPosition, rectSize);
                    //rectView.SetRect(rect, mapData.TileWidth);
                    //isModelEditValid = tilePositionValid && IsWorldPositionValid(startDragTilePosition);
                    //rectView.Show(isModelEditValid);
                }
                else
                {
                    rectPosition = endDragTilePosition;
                    rectSize = new int2(1, 1);
                    //isModelEditValid = tilePositionValid;
                    //rect = new RectInt(currentTilePosition, Vector2Int.one);
                    //rectView.SetRect(rect, mapData.TileWidth);
                    //rectView.Show(isModelEditValid);
                }

                //Debug.Log(rectPosition + " " + rectSize);

                SetRectView(ref state, rectPosition, rectSize, ref mapTiles, ref mapHeights);

                rect = new int4(rectPosition, rectSize);
                isRectValid = true;
            }
        }
    }

    private bool GetTilePositionIntersection (ref NativeArray<TileData> mapTiles, out int2 tilePositionIntersected,
        out int tileIndexIntersected)
    {
        var rectOffset = new int2(2, 2);
        var rectPosition = currentTilePosition - rectOffset;
        var rectSize = rectPosition + (rectOffset * 2);

        var ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        tileIndexIntersected = -1;
        tilePositionIntersected = int2.zero;

        ref var vertexes = ref meshBlobDataComponent.meshDataBlob.Value.vertexes;
        ref var indexes = ref meshBlobDataComponent.meshDataBlob.Value.indexes;

        for (int i = 0; i < rectSize.x; i++)
        {
            for (int j = 0; j < rectSize.y; j++)
            {
                var tilePosition = rectPosition + new int2(i, j);
                var tileIndex = mapComponent.GetTileIndexFromTilePosition(tilePosition);

                if (tileIndex < 0 || tileIndex >= mapTiles.Length)
                {
                    continue;
                }

                var tileData = mapTiles[tileIndex];
                var tileWorldPosition = new float3(tilePosition.x, tileData.terrainLevel, tilePosition.y);

                if (IntersectTile(ray, tileData.terrainType, tileWorldPosition, ref vertexes, ref indexes))
                {
                    tileIndexIntersected = tileIndex;
                    tilePositionIntersected = tilePosition;
                    return true;
                }
            }
        }

        return false;
    }

    private void SetRectView (ref SystemState state, int2 rectPosition, int2 rectSize, ref NativeArray<TileData> mapTiles, ref NativeArray<int> mapHeights)
    {
        var rectView = SystemAPI.ManagedAPI.GetSingleton<RefGameObject>().RectView;
        var mesh = new Mesh();
        var rectViewMeshArray = Mesh.AllocateWritableMeshData(mesh);
        var rectViewMesh = rectViewMeshArray[0];

        var vertexTileCount = meshBlobInfoComponent.vertexCount;
        var vertexAttributeCount = meshBlobInfoComponent.meshInfoBlob.Value.attributes.Length;
        var indexTileCount = meshBlobInfoComponent.indexCount;
        var tileCount = rectSize.x * rectSize.y;

        rectViewMesh.SetVertexBufferParams(tileCount * vertexTileCount,
                meshBlobInfoComponent.meshInfoBlob.Value.attributes.ToArray()); // use same attributes from the tile
        rectViewMesh.SetIndexBufferParams(tileCount * indexTileCount, IndexFormat.UInt32);

        var vertexesArray = rectViewMesh.GetVertexData<float3>();
        var indexArray = rectViewMesh.GetIndexData<uint>();

        ref var vertexBlobArray = ref meshBlobDataComponent.meshDataBlob.Value.vertexes;
        ref var indexBlobArray = ref meshBlobDataComponent.meshDataBlob.Value.indexes;

        var vertexIndex = 0;

        for (int i = 0; i < rectSize.x; i++)
        {
            for (int j = 0; j < rectSize.y; j++)
            {
                var tilePosition = rectPosition + new int2(i, j);
                var tileIndex = mapComponent.GetTileIndexFromTilePosition(tilePosition);

                var heights = int4.zero;
                heights.x = mapHeights[mapComponent.GetHeigthMapIndexFromHeightMapPosition(tilePosition)];
                heights.y = mapHeights[mapComponent.GetHeigthMapIndexFromHeightMapPosition(tilePosition.GetEast())];
                heights.z = mapHeights[mapComponent.GetHeigthMapIndexFromHeightMapPosition(tilePosition.GetNorthEast())];
                heights.w = mapHeights[mapComponent.GetHeigthMapIndexFromHeightMapPosition(tilePosition.GetNorth())];
                var heightMin = math.min(heights.x, math.min(heights.y, math.min(heights.z, heights.w)));

                var tileData = mapTiles[tileIndex];
                tileData.terrainLevel = heightMin;
                mapTiles[tileIndex] = tileData;

                heights.x -= heightMin;
                heights.y -= heightMin;
                heights.z -= heightMin;
                heights.w -= heightMin;

                if (!meshBlobTileTerrainMappingComponent.mapping.Value.TryGetValue(heights, out var tileTerrainType))
                {
                    tileTerrainType = 0;
                }
                var tileWorldPosition = new float3(tilePosition.x, tileData.terrainLevel, tilePosition.y);

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

                vertexIndex++;
            }
        }

        /*

        var tileTerrainType = mapTiles[tileIndexIntersected].terrainType;

        ref var vertexBlobArray = ref meshBlobDataComponent.meshDataBlob.Value.vertexes;
        ref var indexBlobArray = ref meshBlobDataComponent.meshDataBlob.Value.indexes;

        var tileWorldPosition = new float3(currentTilePosition.x, mapTiles[tileIndexIntersected].tileHeight, currentTilePosition.y);

        var vertexTileCount = meshBlobInfoComponent.vertexCount;
        var vStart = 0;
        var vRead = vertexTileCount * meshBlobInfoComponent.vertexAttributeDimension * tileTerrainType;

        for (int v = 0; v < vertexTileCount; v++)
        {
            // Position
            vertexesArray[vStart++] = (vertexBlobArray[vRead++] + tileWorldPosition) * mapComponent.TileWidth;
            // Normal
            vertexesArray[vStart++] = vertexBlobArray[vRead++];
            // UV
            vertexesArray[vStart++] = vertexBlobArray[vRead++];
        }

        var indexTileCount = meshBlobInfoComponent.indexCount;
        var tRead = indexTileCount * tileTerrainType;
        var tStart = 0 * indexTileCount;
        var tValue = 0 * vertexTileCount;

        for (int t = 0; t < indexTileCount; t++)
        {
            indexArray[tStart++] = indexBlobArray[tRead++] + (uint)tValue;
        }
        */

        rectViewMesh.subMeshCount = 1;
        rectViewMesh.SetSubMesh(0, new SubMeshDescriptor(0, tileCount * meshBlobInfoComponent.indexCount));

        Mesh.ApplyAndDisposeWritableMeshData(rectViewMeshArray, mesh, MeshUpdateFlags.Default);

        rectView.SetMesh(mesh);
    }

    [BurstCompile]
    private bool IsWorldPositionValid (float2 worldPosition)
    {
        return worldPosition.x >= 0f && worldPosition.x < (mapComponent.TileDimension.x * mapComponent.TileWidth) &&
            worldPosition.y >= 0f && worldPosition.y < (mapComponent.TileDimension.y * mapComponent.TileWidth);
    }

    [BurstCompile]
    private bool IntersectTile (Ray ray, int terrainType, float3 tileWorldPosition, ref BlobArray<float3> vertexes, ref BlobArray<uint> indexes)
    {
        var vertexIndex = terrainType;
        var tIndex = 6 * vertexIndex;
        var vIndex = vertexIndex * 12;

        var v0 = (vertexes[(int)indexes[tIndex++] * 3 + vIndex] + tileWorldPosition) * mapComponent.TileWidth;
        var v1 = (vertexes[(int)indexes[tIndex++] * 3 + vIndex] + tileWorldPosition) * mapComponent.TileWidth;
        var v2 = (vertexes[(int)indexes[tIndex++] * 3 + vIndex] + tileWorldPosition) * mapComponent.TileWidth;

        if (IntersectTriangle(ray, v0, v1, v2, false))
        {
            return true;
        }

        v0 = (vertexes[(int)indexes[tIndex++] * 3 + vIndex] + tileWorldPosition) * mapComponent.TileWidth;
        v1 = (vertexes[(int)indexes[tIndex++] * 3 + vIndex] + tileWorldPosition) * mapComponent.TileWidth;
        v2 = (vertexes[(int)indexes[tIndex++] * 3 + vIndex] + tileWorldPosition) * mapComponent.TileWidth;

        if (IntersectTriangle(ray, v0, v1, v2, false))
        {
            return true;
        }

        return false;
    }

    [BurstCompile]
    private bool IntersectTriangle (Ray ray, float3 v0, float3 v1, float3 v2, bool bidirectional)
    {
        float3 lhs = v1 - v0;
        float3 vector = v2 - v0;
        float3 vector2 = math.cross(lhs, vector);
        float num = math.dot(-ray.direction, vector2);
        if (num <= 0f)
        {
            return false;
        }

        float3 origin = ray.origin;
        float3 vector3 = origin - v0;
        float num2 = math.dot(vector3, vector2);
        if (num2 < 0f && !bidirectional)
        {
            return false;
        }

        float3 rhs = math.cross(-ray.direction, vector3);
        float num3 = Vector3.Dot(vector, rhs);
        if (num3 < 0f || num3 > num)
        {
            return false;
        }

        float num4 = 0f - Vector3.Dot(lhs, rhs);
        if (num4 < 0f || num3 + num4 > num)
        {
            return false;
        }

        return true;
    }

    [BurstCompile]
    public void GetRectFromTwoPoints (int2 p1, int2 p2, out int2 init, out int2 size)
    {
        if (p1.x <= p2.x && p1.y <= p2.y)
        {
            init = p1;
            size = p2 - p1;
        }
        else if (p1.x <= p2.x && p1.y >= p2.y)
        {
            init = new int2(p1.x, p2.y);
            size = new int2(p2.x - p1.x, p1.y - p2.y);
        }
        else if (p1.x >= p2.x && p1.y <= p2.y)
        {
            init = new int2(p2.x, p1.y);
            size = new int2(p1.x - p2.x, p2.y - p1.y);
        }
        else
        {
            init = p2;
            size = p1 - p2;
        }

        size += new int2(1, 1);
    }
}