using System.Collections;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

[BurstCompile]
public partial struct CreateWaterJob2 : IJob
{
    public NativeArray<int3> MapTiles;

    [ReadOnly]
    public int2 MapDimension;

    [ReadOnly]
    public int TileWidth;

    [ReadOnly]
    public int2 RectPosition;

    [ReadOnly]
    public int2 RectSize;

    [ReadOnly]
    public NativeArray<float4> TileTerrainMapping;

    [WriteOnly]
    [NativeDisableContainerSafetyRestriction]
    public Mesh.MeshData MeshData;

    [BurstCompile]
    public void Execute ()
    {
        var verticeArray = MeshData.GetVertexData<float3>();

        var tileVerticeCount = 4;
        var vVerticeDimension = 3;

        int2 tilePosition;
        int3 tileData;
        int3 tileDataAux;
        int tileIndex;
        int tileIndexAux;
        int vIndex;

        var tileDataId = 1;

        for (int i = 0; i < RectSize.x; i++)
        {
            for (int j = 0; j < RectSize.y; j++)
            {
                tilePosition = RectPosition + new int2(i, j);
                tileIndex = GetTileIndexFromTilePosition(tilePosition);

                tileData = MapTiles[tileIndex];
                tileData.x = 0;
                tileData.z = -1;
                MapTiles[tileIndex] = tileData;

                vIndex = tileIndex * vVerticeDimension * tileVerticeCount;
                SetTileQuad(vIndex, tileDataId, vVerticeDimension, ref verticeArray);
            }
        }

        for (int i = 0; i < RectSize.x; i++)
        {
            tilePosition = RectPosition + new int2(i, -1);
            tileIndex = GetTileIndexFromTilePosition(tilePosition);

            tileData = MapTiles[tileIndex];






            tilePosition = RectPosition + new int2(i, RectSize.y);
            tileIndex = GetTileIndexFromTilePosition(tilePosition);

            tileData = MapTiles[tileIndex];




        }

        for (int j = 0; j < RectSize.y; j++)
        {
            tilePosition = RectPosition + new int2(-1, j);
            tileIndex = GetTileIndexFromTilePosition(tilePosition);

            tileData = MapTiles[tileIndex];
            if (tileData.x != 1)
            {
                tileDataId = GetTileQuadIdFromTile(tilePosition);

                tileData.x = tileDataId;
                MapTiles[tileIndex] = tileData;

                vIndex = tileIndex * vVerticeDimension * tileVerticeCount;
                SetTileQuad(vIndex, tileDataId, vVerticeDimension, ref verticeArray);
            }

            tilePosition = RectPosition + new int2(RectSize.x, j);
            tileIndex = GetTileIndexFromTilePosition(tilePosition);

            tileData = MapTiles[tileIndex];
            if (tileData.x != 1)
            {
                tileDataId = GetTileQuadIdFromTile(tilePosition);

                tileData.x = tileDataId;
                MapTiles[tileIndex] = tileData;

                vIndex = tileIndex * vVerticeDimension * tileVerticeCount;
                SetTileQuad(vIndex, tileDataId, vVerticeDimension, ref verticeArray);
            }
        }

        tilePosition = RectPosition + new int2(-1, -1);
        tileIndex = GetTileIndexFromTilePosition(tilePosition);

        tileData = MapTiles[tileIndex];
        if (tileData.x != 1)
        {
            tileDataId = GetTileQuadIdFromTile(tilePosition);

            tileData.x = tileDataId;
            MapTiles[tileIndex] = tileData;

            vIndex = tileIndex * vVerticeDimension * tileVerticeCount;
            SetTileQuad(vIndex, tileDataId, vVerticeDimension, ref verticeArray);
        }

        tilePosition = RectPosition + new int2(-1, RectSize.y);
        tileIndex = GetTileIndexFromTilePosition(tilePosition);

        tileData = MapTiles[tileIndex];
        if (tileData.x != 1)
        {
            tileDataId = GetTileQuadIdFromTile(tilePosition);

            tileData.x = tileDataId;
            MapTiles[tileIndex] = tileData;

            vIndex = tileIndex * vVerticeDimension * tileVerticeCount;
            SetTileQuad(vIndex, tileDataId, vVerticeDimension, ref verticeArray);
        }

        tilePosition = RectPosition + new int2(RectSize.x, RectSize.y);
        tileIndex = GetTileIndexFromTilePosition(tilePosition);

        tileData = MapTiles[tileIndex];
        if (tileData.x != 1)
        {
            tileDataId = GetTileQuadIdFromTile(tilePosition);

            tileData.x = tileDataId;
            MapTiles[tileIndex] = tileData;

            vIndex = tileIndex * vVerticeDimension * tileVerticeCount;
            SetTileQuad(vIndex, tileDataId, vVerticeDimension, ref verticeArray);
        }

        tilePosition = RectPosition + new int2(RectSize.x, -1);
        tileIndex = GetTileIndexFromTilePosition(tilePosition);

        tileData = MapTiles[tileIndex];
        if (tileData.x != 1)
        {
            tileDataId = GetTileQuadIdFromTile(tilePosition);

            tileData.x = tileDataId;
            MapTiles[tileIndex] = tileData;

            vIndex = tileIndex * vVerticeDimension * tileVerticeCount;
            SetTileQuad(vIndex, tileDataId, vVerticeDimension, ref verticeArray);
        }
    }

    [BurstCompile]
    private void Teste ()
    {






    }



    [BurstCompile]
    private int GetTileQuadIdFromTile (int2 tilePosition)
    {
        var tileIndex = GetTileIndexFromTilePosition(tilePosition.GetSouth());
        var tileSouthId = MapTiles[tileIndex].x;
        tileIndex = GetTileIndexFromTilePosition(tilePosition.GetNorth());
        var tileNorthId = MapTiles[tileIndex].x;
        tileIndex = GetTileIndexFromTilePosition(tilePosition.GetEast());
        var tileEastId = MapTiles[tileIndex].x;
        tileIndex = GetTileIndexFromTilePosition(tilePosition.GetWest());
        var tileWestId = MapTiles[tileIndex].x;

        var countAux = 0;
        if (tileSouthId == 1)
            countAux += 1;
        if (tileEastId == 1)
            countAux += 2;
        if (tileNorthId == 1)
            countAux += 4;
        if (tileWestId == 1)
            countAux += 8;

        switch (countAux)
        {
            case 1: return 6;
            case 2: return 3;
            case 4: return 5;
            case 8: return 4;
            case 3: return 12;
            case 6: return 13;
            case 12: return 14;
            case 9: return 11;
            default:
            {
                tileIndex = GetTileIndexFromTilePosition(tilePosition.GetSouthWest());
                var tileSouthWestId = MapTiles[tileIndex].x;
                tileIndex = GetTileIndexFromTilePosition(tilePosition.GetSouthEast());
                var tileSouthEastId = MapTiles[tileIndex].x;
                tileIndex = GetTileIndexFromTilePosition(tilePosition.GetNorthEast());
                var tileNorthEastId = MapTiles[tileIndex].x;
                tileIndex = GetTileIndexFromTilePosition(tilePosition.GetNorthWest());
                var tileNorthWestId = MapTiles[tileIndex].x;

                countAux = 0;
                if (tileSouthWestId == 1)
                    countAux += 1;
                if (tileSouthEastId == 1)
                    countAux += 2;
                if (tileNorthEastId == 1)
                    countAux += 4;
                if (tileNorthWestId == 1)
                    countAux += 8;

                switch (countAux)
                {
                    case 1: return 9;
                    case 2: return 8;
                    case 4: return 7;
                    case 8: return 10;
                    default: return 1;
                }
            }
        }
    }

    [BurstCompile]
    private void SetTileQuad (int vIndex, int tileDataId, int vVerticeDimension, ref NativeArray<float3> verticeArray)
    {
        var vQuadIndex = vIndex;
        verticeArray[vQuadIndex] = verticeArray[vQuadIndex].WithY(TileTerrainMapping[tileDataId].x);
        vQuadIndex = vIndex + (1 * vVerticeDimension);
        verticeArray[vQuadIndex] = verticeArray[vQuadIndex].WithY(TileTerrainMapping[tileDataId].y);
        vQuadIndex = vIndex + (2 * vVerticeDimension);
        verticeArray[vQuadIndex] = verticeArray[vQuadIndex].WithY(TileTerrainMapping[tileDataId].z);
        vQuadIndex = vIndex + (3 * vVerticeDimension);
        verticeArray[vQuadIndex] = verticeArray[vQuadIndex].WithY(TileTerrainMapping[tileDataId].w);
    }

    [BurstCompile]
    private int GetTileIndexFromTilePosition (int2 tilePosition)
    {
        return tilePosition.x * MapDimension.y + tilePosition.y;
    }
}