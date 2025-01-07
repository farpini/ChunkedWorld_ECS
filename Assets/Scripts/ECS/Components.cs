using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine.Rendering;
using BlobHashMaps;

public struct ModelMapping : IComponentData, IDisposable
{
    public BlobAssetReference<BlobHashMap<int, Entity>> mapping;

    public void Dispose ()
    {
        if (mapping.IsCreated) mapping.Dispose();
    }
}

[InternalBufferCapacity(64)]
public struct ModelChunkEntityBuffer : IBufferElementData
{
    public static implicit operator Entity (ModelChunkEntityBuffer e) { return e.Value; }
    public static implicit operator ModelChunkEntityBuffer (Entity e) { return new ModelChunkEntityBuffer { Value = e }; }

    public Entity Value;
}

public struct MeshChunkRenderer : IComponentData, IEnableableComponent
{
}

public class MeshChunkData : IComponentData, IDisposable 
{
    public Entity entity;
    public int2 chunkPosition;
    public NativeHashMap<int, int> mapping; // modelIndex -> modelId
    public NativeHashMap<int, int> invMapping; // modelId -> modelIndex

    public void Dispose ()
    {
        if (mapping.IsCreated) mapping.Dispose();
    }
}

public struct MeshDataComponent : IComponentData
{
    public BlobAssetReference<MeshBlobData> meshDataBlob;
    public int modelId;
    public int vertexCount;
    public int indexCount;
    public int vertexAttributeDimension;
}

public struct MeshBlobData
{
    public BlobArray<char> meshName;
    public BlobArray<VertexAttributeDescriptor> attributes;
    public BlobArray<float> vertexes;
    public BlobArray<uint> indexes;
}