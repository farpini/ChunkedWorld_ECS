using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

public static class BlobExtensions
{
    public static string BlobCharToString (this ref BlobArray<char> blobData)
    {
        return new string(blobData.ToArray());
    }

    public static NativeArray<int> BlobIntToNativeArray (this ref BlobArray<int> blobData, Unity.Collections.Allocator allocatorType)
    {
        var array = new NativeArray<int>(blobData.Length, allocatorType);
        for (int i = 0; i < array.Length; i++)
        {
            array[i] = blobData[i];
        }
        return array;
    }

    public static float3 WithY (this float3 vector, float y_Value)
    {
        return new float3(vector.x, y_Value, vector.z);
    }

    public static int2 GetEast (this int2 position)
    {
        return position + new int2(1, 0);
    }

    public static int2 GetWest (this int2 position)
    {
        return position + new int2(-1, 0);
    }

    public static int2 GetNorth (this int2 position)
    {
        return position + new int2(0, 1);
    }

    public static int2 GetSouth (this int2 position)
    {
        return position + new int2(0, -1);
    }

    public static int2 GetSouthEast (this int2 position)
    {
        return position + new int2(1, -1);
    }

    public static int2 GetSouthWest (this int2 position)
    {
        return position + new int2(-1, -1);
    }

    public static int2 GetNorthEast (this int2 position)
    {
        return position + new int2(1, 1);
    }

    public static int2 GetNorthWest (this int2 position)
    {
        return position + new int2(-1, 1);
    }
}