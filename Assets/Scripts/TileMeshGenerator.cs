using Unity.Entities;
using Unity.Mathematics;

namespace MeshGenerator
{
    public static class TileMeshGenerator
    {
        private static float3 UpNormal = new float3(0f, 1f, 0f);



        public static bool InsertTileTerrainType (TileTerrainType tileType, int vertexStart, int indexStart,
            ref BlobBuilderArray<float3> vertexesArray, ref BlobBuilderArray<uint> indexesArray)
        {
            switch (tileType)
            {
                case TileTerrainType.Flat: { Flat(vertexStart, indexStart, ref vertexesArray, ref indexesArray); return true; }
                case TileTerrainType.Saddle_0: { Saddle(new int4(0, 1, 0, 1), vertexStart, indexStart, ref vertexesArray, ref indexesArray); return true; }
                case TileTerrainType.Saddle_1: { Saddle(new int4(1, 0, 1, 0), vertexStart, indexStart, ref vertexesArray, ref indexesArray); return true; }
                case TileTerrainType.Ramp_0: { Ramp(new int4(0, 0, 1, 1), vertexStart, indexStart, ref vertexesArray, ref indexesArray); return true; }
                case TileTerrainType.Ramp_1: { Ramp(new int4(1, 1, 0, 0), vertexStart, indexStart, ref vertexesArray, ref indexesArray); return true; }
                case TileTerrainType.Ramp_2: { Ramp(new int4(0, 1, 1, 0), vertexStart, indexStart, ref vertexesArray, ref indexesArray); return true; }
                case TileTerrainType.Ramp_3: { Ramp(new int4(1, 0, 0, 1), vertexStart, indexStart, ref vertexesArray, ref indexesArray); return true; }
                case TileTerrainType.H1_0: { High1(new int4(1, 0, 0, 0), vertexStart, indexStart, ref vertexesArray, ref indexesArray); return true; }
                case TileTerrainType.H1_1: { High1(new int4(0, 0, 1, 0), vertexStart, indexStart, ref vertexesArray, ref indexesArray); return true; }
                case TileTerrainType.H1_2: { High1(new int4(0, 1, 0, 0), vertexStart, indexStart, ref vertexesArray, ref indexesArray); return true; }
                case TileTerrainType.H1_3: { High1(new int4(0, 0, 0, 1), vertexStart, indexStart, ref vertexesArray, ref indexesArray); return true; }
                case TileTerrainType.H3_0: { High3(new int4(1, 1, 0, 1), vertexStart, indexStart, ref vertexesArray, ref indexesArray); return true; }
                case TileTerrainType.H3_1: { High3(new int4(0, 1, 1, 1), vertexStart, indexStart, ref vertexesArray, ref indexesArray); return true; }
                case TileTerrainType.H3_2: { High3(new int4(1, 1, 1, 0), vertexStart, indexStart, ref vertexesArray, ref indexesArray); return true; }
                case TileTerrainType.H3_3: { High3(new int4(1, 0, 1, 1), vertexStart, indexStart, ref vertexesArray, ref indexesArray); return true; }
                case TileTerrainType.Steep_0: { Steep(new int4(2, 1, 0, 1), vertexStart, indexStart, ref vertexesArray, ref indexesArray); return true; }
                case TileTerrainType.Steep_1: { Steep(new int4(1, 2, 1, 0), vertexStart, indexStart, ref vertexesArray, ref indexesArray); return true; }
                case TileTerrainType.Steep_2: { Steep(new int4(0, 1, 2, 1), vertexStart, indexStart, ref vertexesArray, ref indexesArray); return true; }
                case TileTerrainType.Steep_3: { Steep(new int4(1, 0, 1, 2), vertexStart, indexStart, ref vertexesArray, ref indexesArray); return true; }
                default: return false;
            }
        }

        private static void Flat (int vertexStart, int indexStart,
            ref BlobBuilderArray<float3> vertexesArray, ref BlobBuilderArray<uint> indexesArray)
        {
            var v = vertexStart;

            vertexesArray[v++] = new float3(0f, 0f, 0f);
            vertexesArray[v++] = UpNormal;
            vertexesArray[v++] = new float3(0f, 0f, 0f);

            vertexesArray[v++] = new float3(1f, 0f, 0f);
            vertexesArray[v++] = UpNormal;
            vertexesArray[v++] = new float3(1f, 0f, 0f);

            vertexesArray[v++] = new float3(1f, 0f, 1f);
            vertexesArray[v++] = UpNormal;
            vertexesArray[v++] = new float3(1f, 1f, 0f);

            vertexesArray[v++] = new float3(0f, 0f, 1f);
            vertexesArray[v++] = UpNormal;
            vertexesArray[v++] = new float3(0f, 1f, 0f);

            var i = indexStart;

            indexesArray[i++] = 0;
            indexesArray[i++] = 2;
            indexesArray[i++] = 1;
            indexesArray[i++] = 0;
            indexesArray[i++] = 3;
            indexesArray[i++] = 2;
        }

        private static void Saddle (int4 saddleFilter, int vertexStart, int indexStart,
            ref BlobBuilderArray<float3> vertexesArray, ref BlobBuilderArray<uint> indexesArray)
        {
            var v = vertexStart;

            vertexesArray[v++] = new float3(0f, 1f * saddleFilter.x, 0f);
            vertexesArray[v++] = UpNormal;
            vertexesArray[v++] = new float3(0f, 0f, 0f);

            vertexesArray[v++] = new float3(1f, 1f * saddleFilter.y, 0f);
            vertexesArray[v++] = UpNormal;
            vertexesArray[v++] = new float3(1f, 0f, 0f);

            vertexesArray[v++] = new float3(1f, 1f * saddleFilter.z, 1f);
            vertexesArray[v++] = UpNormal;
            vertexesArray[v++] = new float3(1f, 1f, 0f);

            vertexesArray[v++] = new float3(0f, 1f * saddleFilter.w, 1f);
            vertexesArray[v++] = UpNormal;
            vertexesArray[v++] = new float3(0f, 1f, 0f);

            var i = indexStart;

            if (saddleFilter.x == 1 || saddleFilter.z == 1)
            {
                indexesArray[i++] = 0;
                indexesArray[i++] = 3;
                indexesArray[i++] = 1;
                indexesArray[i++] = 1;
                indexesArray[i++] = 3;
                indexesArray[i++] = 2;
            }
            else
            {
                indexesArray[i++] = 0;
                indexesArray[i++] = 2;
                indexesArray[i++] = 1;
                indexesArray[i++] = 0;
                indexesArray[i++] = 3;
                indexesArray[i++] = 2;
            }
        }

        private static void Ramp (int4 rampFilter, int vertexStart, int indexStart,
            ref BlobBuilderArray<float3> vertexesArray, ref BlobBuilderArray<uint> indexesArray)
        {
            var v = vertexStart;

            vertexesArray[v++] = new float3(0f, 1f * rampFilter.x, 0f);
            vertexesArray[v++] = UpNormal;
            vertexesArray[v++] = new float3(0f, 0f, 0f);

            vertexesArray[v++] = new float3(1f, 1f * rampFilter.y, 0f);
            vertexesArray[v++] = UpNormal;
            vertexesArray[v++] = new float3(1f, 0f, 0f);

            vertexesArray[v++] = new float3(1f, 1f * rampFilter.z, 1f);
            vertexesArray[v++] = UpNormal;
            vertexesArray[v++] = new float3(1f, 1f, 0f);

            vertexesArray[v++] = new float3(0f, 1f * rampFilter.w, 1f);
            vertexesArray[v++] = UpNormal;
            vertexesArray[v++] = new float3(0f, 1f, 0f);

            var i = indexStart;

            indexesArray[i++] = 0;
            indexesArray[i++] = 2;
            indexesArray[i++] = 1;
            indexesArray[i++] = 0;
            indexesArray[i++] = 3;
            indexesArray[i++] = 2;
        }

        private static void High1 (int4 highFilter, int vertexStart, int indexStart,
            ref BlobBuilderArray<float3> vertexesArray, ref BlobBuilderArray<uint> indexesArray)
        {
            var v = vertexStart;

            vertexesArray[v++] = new float3(0f, 1f * highFilter.x, 0f);
            vertexesArray[v++] = UpNormal;
            vertexesArray[v++] = new float3(0f, 0f, 0f);

            vertexesArray[v++] = new float3(1f, 1f * highFilter.y, 0f);
            vertexesArray[v++] = UpNormal;
            vertexesArray[v++] = new float3(1f, 0f, 0f);

            vertexesArray[v++] = new float3(1f, 1f * highFilter.z, 1f);
            vertexesArray[v++] = UpNormal;
            vertexesArray[v++] = new float3(1f, 1f, 0f);

            vertexesArray[v++] = new float3(0f, 1f * highFilter.w, 1f);
            vertexesArray[v++] = UpNormal;
            vertexesArray[v++] = new float3(0f, 1f, 0f);

            var i = indexStart;

            if (highFilter.x == 1 || highFilter.z == 1)
            {
                indexesArray[i++] = 0;
                indexesArray[i++] = 3;
                indexesArray[i++] = 1;
                indexesArray[i++] = 1;
                indexesArray[i++] = 3;
                indexesArray[i++] = 2;
            }
            else
            {
                indexesArray[i++] = 0;
                indexesArray[i++] = 2;
                indexesArray[i++] = 1;
                indexesArray[i++] = 0;
                indexesArray[i++] = 3;
                indexesArray[i++] = 2;
            }
        }

        private static void High3 (int4 highFilter, int vertexStart, int indexStart,
            ref BlobBuilderArray<float3> vertexesArray, ref BlobBuilderArray<uint> indexesArray)
        {
            var v = vertexStart;

            vertexesArray[v++] = new float3(0f, 1f * highFilter.x, 0f);
            vertexesArray[v++] = UpNormal;
            vertexesArray[v++] = new float3(0f, 0f, 0f);

            vertexesArray[v++] = new float3(1f, 1f * highFilter.y, 0f);
            vertexesArray[v++] = UpNormal;
            vertexesArray[v++] = new float3(1f, 0f, 0f);

            vertexesArray[v++] = new float3(1f, 1f * highFilter.z, 1f);
            vertexesArray[v++] = UpNormal;
            vertexesArray[v++] = new float3(1f, 1f, 0f);

            vertexesArray[v++] = new float3(0f, 1f * highFilter.w, 1f);
            vertexesArray[v++] = UpNormal;
            vertexesArray[v++] = new float3(0f, 1f, 0f);

            var i = indexStart;

            if (highFilter.x == 0 || highFilter.z == 0)
            {
                indexesArray[i++] = 0;
                indexesArray[i++] = 3;
                indexesArray[i++] = 1;
                indexesArray[i++] = 1;
                indexesArray[i++] = 3;
                indexesArray[i++] = 2;
            }
            else
            {
                indexesArray[i++] = 0;
                indexesArray[i++] = 2;
                indexesArray[i++] = 1;
                indexesArray[i++] = 0;
                indexesArray[i++] = 3;
                indexesArray[i++] = 2;
            }
        }

        private static void Steep (int4 steepFilter, int vertexStart, int indexStart,
            ref BlobBuilderArray<float3> vertexesArray, ref BlobBuilderArray<uint> indexesArray)
        {
            var v = vertexStart;

            vertexesArray[v++] = new float3(0f, 1f * steepFilter.x, 0f);
            vertexesArray[v++] = UpNormal;
            vertexesArray[v++] = new float3(0f, 0f, 0f);

            vertexesArray[v++] = new float3(1f, 1f * steepFilter.y, 0f);
            vertexesArray[v++] = UpNormal;
            vertexesArray[v++] = new float3(1f, 0f, 0f);

            vertexesArray[v++] = new float3(1f, 1f * steepFilter.z, 1f);
            vertexesArray[v++] = UpNormal;
            vertexesArray[v++] = new float3(1f, 1f, 0f);

            vertexesArray[v++] = new float3(0f, 1f * steepFilter.w, 1f);
            vertexesArray[v++] = UpNormal;
            vertexesArray[v++] = new float3(0f, 1f, 0f);

            var i = indexStart;

            indexesArray[i++] = 0;
            indexesArray[i++] = 2;
            indexesArray[i++] = 1;
            indexesArray[i++] = 0;
            indexesArray[i++] = 3;
            indexesArray[i++] = 2;
        }
    }
}