using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine.Rendering;

public struct ControllerComponent : IComponentData
{
    public ControllerState State;
    public int4 Rect;
    public int ModelSelectedId;
    public int FloorSelectedTextureId;
}

public struct MapComponent : IComponentData
{
    public int2 TileDimension;
    public int TileWidth;
    public int2 ChunkDimension;
    public int ChunkWidth;

    public float HalfTileWidth => TileWidth * 0.5f;

    public int GetTileIndexFromTilePosition (int2 tilePosition)
    {
        return tilePosition.x * TileDimension.y + tilePosition.y;
    }

    public int2 GetChunkFromTilePosition (int2 tilePosition)
    {
        return new int2(tilePosition.x / ChunkWidth, tilePosition.y / ChunkWidth);
    }

    public int GetChunkIndexFromChunkPosition (int2 chunkPosition)
    {
        return chunkPosition.x * ChunkDimension.y + chunkPosition.y;
    }
}

public enum ControllerState
{
    None, CreateModel, RemoveModel, CreateFloor, RemoveFloor
}

public struct RendererPrefabEntities : IComponentData
{
    public Entity modelRenderer;
}

public struct ModelDataEntityBuffer : IBufferElementData
{
    public static implicit operator Entity (ModelDataEntityBuffer e) { return e.Value; }
    public static implicit operator ModelDataEntityBuffer (Entity e) { return new ModelDataEntityBuffer { Value = e }; }

    public Entity Value;
}

[InternalBufferCapacity(64)]
public struct ModelChunkEntityBuffer : IBufferElementData
{
    public static implicit operator Entity (ModelChunkEntityBuffer e) { return e.Value; }
    public static implicit operator ModelChunkEntityBuffer (Entity e) { return new ModelChunkEntityBuffer { Value = e }; }

    public Entity Value;
}

[Serializable]
public class MeshChunkData : IComponentData, IDisposable 
{
    public Entity entity;
    public int meshModelId;
    public int2 chunkPosition;
    public NativeHashMap<int, int> mapping; // modelIndex -> modelId
    public NativeHashMap<int, int> invMapping; // modelId -> modelIndex

    public void Dispose ()
    {
        if (mapping.IsCreated) mapping.Dispose();
    }
}

public struct ModelDataComponent : IComponentData
{
    public int modelId;
}

public struct MeshDataComponent : IComponentData
{
    public BlobAssetReference<MeshBlobData> meshDataBlob;
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