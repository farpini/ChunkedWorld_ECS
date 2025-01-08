using Unity.Collections;
using Unity.Entities;

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
}