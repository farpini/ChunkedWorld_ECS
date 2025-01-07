using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

public struct MeshFloorRenderer : IComponentData, IEnableableComponent
{
}

public class MeshChunkData : IComponentData, IDisposable 
{
    public Entity entity;
    public int2 chunkPosition;
    public int lastFormIndex;
    public NativeHashMap<int, int> mapping; // posIndex -> formId

    public void Dispose ()
    {
        if (mapping.IsCreated) mapping.Dispose();
    }
}