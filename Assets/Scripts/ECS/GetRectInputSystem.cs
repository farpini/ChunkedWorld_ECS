/*
 * Written by Fernando Arpini Ferretto
 * https://github.com/farpini/ProceduralTileChunkedMap_ECS
 */

using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.EventSystems;

[UpdateBefore(typeof(GetChunksFromRectSystem))]
public partial struct GetRectInputSystem : ISystem, ISystemStartStop
{
    private MapComponent mapComponent;
    private MeshBlobDataComponent meshBlobDataComponent;
    private int2 currentTilePosition;
    private int2 startDragTilePosition;
    private int4 rect;
    private bool isRectValid;
    private Ray currentRay;
    private Plane mapPlaneZero;
    private bool isDragging;
    private float2 INVALID_FLOAT2;
    private int2 INVALID_INT2;

    public void OnCreate (ref SystemState state)
    {
        state.RequireForUpdate<MapComponent>();
        state.RequireForUpdate<ControllerComponent>();
        state.RequireForUpdate<RendererPrefabEntities>();
    }

    public void OnDestroy (ref SystemState state)
    {
    }

    public void OnStartRunning (ref SystemState state)
    {
        mapComponent = SystemAPI.GetSingleton<MapComponent>();

        currentTilePosition = int2.zero;
        startDragTilePosition = int2.zero;
        rect = new int4(int2.zero, int2.zero);
        isDragging = false;
        isRectValid = false;
        mapPlaneZero = new Plane(Vector3.up, Vector3.zero);
        INVALID_FLOAT2 = new float2(-1f, -1f);
        INVALID_INT2 = new int2(-1, -1);

        var terrainEntity = SystemAPI.GetSingleton<RendererPrefabEntities>().tilePrefab;
        meshBlobDataComponent = state.EntityManager.GetSharedComponentManaged<MeshBlobDataComponent>(terrainEntity);
    }

    public void OnStopRunning (ref SystemState state)
    {
    }

    public void OnUpdate (ref SystemState state)
    {
        var controllerData = SystemAPI.GetSingletonRW<ControllerComponent>();

        if (!controllerData.ValueRO.OnRectSelecting)
        {
            return;
        }

        mapComponent = SystemAPI.GetSingleton<MapComponent>();

        var mapTiles = SystemAPI.GetSingletonRW<MapTileComponent>().ValueRW.TileData;

        GetWorldPositions(ref state, ref mapTiles);

        isDragging = Input.GetMouseButton(0);

        if (Input.GetMouseButtonDown(0))
        {
            if (!EventSystem.current.IsPointerOverGameObject())
            {
                //isDragging = true;
                startDragTilePosition = currentTilePosition;
            }
        }

        if (Input.GetMouseButtonUp(0))
        {
            //isDragging = false;

            if (!EventSystem.current.IsPointerOverGameObject())
            {
                if (isRectValid)
                {
                    controllerData.ValueRW.Rect = rect;
                    controllerData.ValueRW.OnRectSelecting = false;

                    SystemAPI.ManagedAPI.GetSingleton<RefGameObject>().RectSelection.Hide();
                }
            }
        }
    }

    [BurstCompile]
    private bool GetTilePositionIntersection (ref NativeArray<TileData> mapTiles, Ray ray, int2 rectPosition, int2 rectSize,
        out int2 tilePositionIntersected, out int tileIndexIntersected)
    {
        tilePositionIntersected = int2.zero;
        tileIndexIntersected = 0;

        ref var vertexes = ref meshBlobDataComponent.meshDataBlob.Value.vertexes;
        ref var indexes = ref meshBlobDataComponent.meshDataBlob.Value.indexes;

        for (int i = 0; i < rectSize.x; i++)
        {
            for (int j = 0; j < rectSize.y; j++)
            {
                var tilePosition = rectPosition + new int2(i, j);

                if (!mapComponent.IsTilePositionValid(tilePosition))
                {
                    continue;
                }

                var tileIndex = mapComponent.GetTileIndexFromTilePosition(tilePosition);

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

    private void GetWorldPositions (ref SystemState state, ref NativeArray<TileData> mapTiles)
    {
        currentRay = Camera.main.ScreenPointToRay(Input.mousePosition);

        var intersectPointOnPlaneZero = float3.zero;
        var intersectPointOnPlaneMaxHeight = float3.zero;

        if (mapPlaneZero.Raycast(currentRay, out var distance))
        {
            intersectPointOnPlaneZero = currentRay.GetPoint(distance);
        }

        var mapPlaneMaxHeight = new Plane(Vector3.up, new Vector3(0f, mapComponent.MaxHeight * mapComponent.TileWidth));

        if (mapPlaneMaxHeight.Raycast(currentRay, out distance))
        {
            intersectPointOnPlaneMaxHeight = currentRay.GetPoint(distance);
        }

        var lastTilePosition = currentTilePosition;

        isRectValid = false;

        var pA = mapComponent.GetTilePositionFromPosition(intersectPointOnPlaneZero);
        var pB = mapComponent.GetTilePositionFromPosition(intersectPointOnPlaneMaxHeight);

        GetRectFromTwoPoints(pA, pB, out var rectPosition, out var rectSize);

        if (GetTilePositionIntersection(ref mapTiles, currentRay, rectPosition, rectSize, out var tilePositionOver, out var tileIndexOver))
        {
            currentTilePosition = tilePositionOver;

            // comment this if for speed up
            //if (lastTilePosition.Equals(currentTilePosition))
            //{
                OnTilePositionChanged(ref state, true);
            //}
        }
        else
        {
            //worldPosition = INVALID_FLOAT2;
            currentTilePosition = INVALID_INT2;

            // comment this if for speed up
            //if (lastTilePosition.Equals(currentTilePosition))
            //{
                OnTilePositionChanged(ref state, false);
            //}
        }
    }

    private void OnTilePositionChanged (ref SystemState state, bool isValid)
    {
        if (EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        var rectPosition = currentTilePosition;
        var rectSize = new int2(1, 1);

        if (isDragging)
        {
            GetRectFromTwoPoints(startDragTilePosition, currentTilePosition, out rectPosition, out rectSize);

            if (!mapComponent.IsTilePositionValid(startDragTilePosition))
            {
                isValid = false;
            }
        }

        rect = new int4(rectPosition, rectSize);
        isRectValid = isValid;

        if (isValid)
        {
            SystemAPI.ManagedAPI.GetSingleton<RefGameObject>().RectSelection.SetRect(
                rectPosition * mapComponent.TileWidth, (rectPosition + rectSize) * mapComponent.TileWidth);
        }
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