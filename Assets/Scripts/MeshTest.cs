using System;
using TreeEditor;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using static UnityEngine.Rendering.DebugUI;

public class MeshTest : MonoBehaviour
{
    [SerializeField] private MeshFilter meshFilter;
    [SerializeField] private MeshRenderer meshRenderer;
    [SerializeField] private MapSO mapSettings;

    [SerializeField] private MeshFilter treeMeshFilter;

    [SerializeField] private RectInt rect;

    [SerializeField] private Mesh treeMesh;

    private NativeHashMap<int, int> mapping;
    private NativeHashMap<int, int> invMapping;
    private int maxChunkWidth = 64;
    private int floorTexture = 2;
    private int formVerticeCount = 4;
    private int formTriangleCount = 6;
    private int2 mapDimension;

    private float3 float3Up;

    private float3[] formVertices;
    private float3[] formUVS;
    private VertexAttributeDescriptor[] vertexLayout;
    private VertexAttributeDescriptor[] modelsVertexLayout;

    //private float3[] treeVertices;
    private ushort[] treeTriangles;
    //private float3[] treeNormals;
    //private float4[] treeTangents;
    //private float2[] treeUVS;

    private float[] treeVertex;
    private int treeVertexAttributesDimension;
    private int modelVertexCount;
    private int modelIndexCount;



    public void Start ()
    {
        mapping = new NativeHashMap<int, int>(maxChunkWidth * maxChunkWidth, Allocator.Persistent);
        invMapping = new NativeHashMap<int, int>(maxChunkWidth * maxChunkWidth, Allocator.Persistent);

        mapDimension = mapSettings.mapDimension;

        formVertices = new float3[]
        {
            float3.zero, new float3(1f, 0f, 0f), new float3(1f, 0f, 1f), new float3(0f, 0f, 1f)
        };

        formUVS = new float3[]
        {
            new float3(0f, 0f, floorTexture), new float3(1f, 0f, floorTexture), new float3(1f, 1f, floorTexture), new float3(0f, 1f, floorTexture)
        };

        vertexLayout = new[]
        {
            new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 3)
        };

        float3Up = new float3(0f, 1f, 0f);

        modelsVertexLayout = treeMesh.GetVertexAttributes();
        treeVertexAttributesDimension = 0;
        for (int i = 0; i < modelsVertexLayout.Length; i++)
        {
            treeVertexAttributesDimension += modelsVertexLayout[i].dimension;
        }
        var mesh = Mesh.AcquireReadOnlyMeshData(treeMesh);
        var meshData = mesh[0];
        modelVertexCount = meshData.vertexCount;
        var vertexData = meshData.GetVertexData<float>();
        treeVertex = new float[vertexData.Length];
        for (int i = 0; i < treeVertex.Length; i++)
        {
            treeVertex[i] = vertexData[i];
        }

        var indexData = meshData.GetIndexData<ushort>();
        modelIndexCount = indexData.Length;
        treeTriangles = new ushort[modelIndexCount];
        for (int i = 0; i < treeTriangles.Length; i++)
        {
            treeTriangles[i] = indexData[i];
        }

        mesh.Dispose();
    }

    private void OnDestroy ()
    {
        mapping.Dispose();
        invMapping.Dispose();
    }

    private int GetTileIndexFromTilePosition (int2 tilePosition)
    {
        return tilePosition.x * mapDimension.y + tilePosition.y;
    }

    private void PrintMapping ()
    {
        var str = "";

        foreach (var map in mapping)
        {
            str += map.Key + "-" + map.Value + " , ";
        }

        Debug.Log(str);
    }

    private void PrintInvMapping ()
    {
        var str = "";

        foreach (var map in invMapping)
        {
            str += map.Key + "-" + map.Value + " , ";
        }

        Debug.LogWarning(str);
    }

    private void CreateTrees ()
    {
        if (Input.GetKeyDown(KeyCode.H))
        {
            var newModelAmount = rect.size.x * rect.size.y;

            var currentModelCount = mapping.Count;

            var currentVertexCount = currentModelCount * modelVertexCount;
            var currentIndexCount = currentModelCount * modelIndexCount;

            var newVertexCount = currentVertexCount + (newModelAmount * modelVertexCount);
            var newIndexCount = currentIndexCount + (newModelAmount * modelIndexCount);

            if (newIndexCount >= 65535)
            {
                Debug.LogWarning("Index count exceeds 65535");
                return;
            }

            var mesh = Mesh.AllocateWritableMeshData(treeMeshFilter.mesh);
            var meshData = mesh[0];

            meshData.SetVertexBufferParams(newVertexCount, modelsVertexLayout);
            meshData.SetIndexBufferParams(newIndexCount, IndexFormat.UInt16);

            var verticeArray = meshData.GetVertexData<float>();
            var indexArray = meshData.GetIndexData<ushort>();

            var vIndex = currentModelCount * modelVertexCount;
            var tIndex = currentIndexCount;
            var tValue = (ushort)currentVertexCount;

            var modelsCreated = 0;

            for (int i = 0; i < rect.size.x; i++)
            {
                for (int j = 0; j < rect.size.y; j++)
                {
                    var modelPosition = new int2(rect.position.x + i, rect.position.y + j);
                    var modelIndex = GetTileIndexFromTilePosition(modelPosition);

                    var modelWorldPosition = new float3(modelPosition.x, 0f, modelPosition.y);

                    if (!mapping.TryGetValue(modelIndex, out var modelId))
                    {
                        for (int v = 0; v < modelVertexCount; v++)
                        {
                            // Position
                            verticeArray[vIndex] = treeVertex[vIndex++] + modelWorldPosition.x;
                            verticeArray[vIndex] = treeVertex[vIndex++] + modelWorldPosition.y;
                            verticeArray[vIndex] = treeVertex[vIndex++] + modelWorldPosition.z;

                            // Normal
                            verticeArray[vIndex] = treeVertex[vIndex++];
                            verticeArray[vIndex] = treeVertex[vIndex++];
                            verticeArray[vIndex] = treeVertex[vIndex++];

                            // Tangent
                            verticeArray[vIndex] = treeVertex[vIndex++];
                            verticeArray[vIndex] = treeVertex[vIndex++];
                            verticeArray[vIndex] = treeVertex[vIndex++];
                            verticeArray[vIndex] = treeVertex[vIndex++];

                            // UV
                            verticeArray[vIndex] = treeVertex[vIndex++];
                            verticeArray[vIndex] = treeVertex[vIndex++];
                        }

                        indexArray[tIndex++] = tValue;
                        indexArray[tIndex++] = (ushort)(tValue + 2);
                        indexArray[tIndex++] = (ushort)(tValue + 1);

                        tValue += 3;

                        mapping.Add(modelIndex, currentModelCount + modelsCreated);
                        invMapping.Add(currentModelCount + modelsCreated, modelIndex);

                        modelsCreated++;
                    }
                }
            }

            newVertexCount = currentVertexCount + (newModelAmount * modelVertexCount);
            newIndexCount = currentIndexCount + (newModelAmount * modelIndexCount);


            /*
            int modelCount = mapping.Count;
            int indexCount = modelCount * formTriangleCount;

            int newModelVertexAmount = treeVertex.Length * newModelAmount;
            int newModelIndexAmount = treeTriangles.Length * newModelAmount;

            //Debug.LogWarning(newModelVertexAmount);

            meshData.SetVertexBufferParams(newModelVertexAmount, modelsVertexLayout);
            meshData.SetIndexBufferParams(newModelIndexAmount, IndexFormat.UInt32);



            */



            mesh.Dispose();
        }
    }

    private void Update ()
    {
        CreateTrees();

        // ADD FORMS
        if (Input.GetKeyDown(KeyCode.F))
        {
            var newFormAmount = rect.size.x * rect.size.y;

            var mesh = Mesh.AllocateWritableMeshData(meshFilter.mesh);
            var meshData = mesh[0];

            var verticeCount = meshData.vertexCount;

            Debug.LogWarning("vc " + verticeCount);

            int formCount = mapping.Count;
            int triangleCount = formCount * formTriangleCount;

            int newFormVerticeAmount = formVerticeCount * newFormAmount;
            int newFormTriangleAmount = formTriangleCount * newFormAmount;

            Debug.LogWarning("nfv " + newFormVerticeAmount);

            meshData.SetVertexBufferParams(verticeCount + newFormVerticeAmount, vertexLayout);
            meshData.SetIndexBufferParams(triangleCount + newFormTriangleAmount, IndexFormat.UInt32);



            //var verticeArray = meshData.GetVertexData<float3>();

            var verticeArray2 = meshData.GetVertexData<float>();

            var triangleArray = meshData.GetIndexData<int>();

            var vertexDimension = 9;

            //var vIndex = vertexCount * vertexLayout.Length;
            var vIndex2 = verticeCount * vertexDimension;
            var tIndex = triangleCount;

            //Debug.Log("Init vIndex: " + vIndex + " Init tIndex: " + tIndex);

            var tValue = formCount * 4;

            var formsCreated = 0;

            for (int i = 0; i < rect.size.x; i++)
            {
                for (int j = 0; j < rect.size.y; j++)
                {
                    var formPosition = new int2(rect.position.x + i, rect.position.y + j);
                    var formIndex = GetTileIndexFromTilePosition(formPosition);

                    var formWorldPosition = new float3(formPosition.x, 0f, formPosition.y);

                    if (!mapping.TryGetValue(formIndex, out var formId))
                    {
                        for (int v = 0; v < formVertices.Length; v++)
                        {
                            // vertice
                            //verticeArray[vIndex++] = formVertices[v] + modelWorldPosition;

                            verticeArray2[vIndex2++] = formVertices[v].x + formWorldPosition.x;
                            verticeArray2[vIndex2++] = formVertices[v].y + formWorldPosition.y;
                            verticeArray2[vIndex2++] = formVertices[v].z + formWorldPosition.z;

                            // normal
                            //verticeArray[vIndex++] = float3Up;

                            verticeArray2[vIndex2++] = 0f;
                            verticeArray2[vIndex2++] = 1f;
                            verticeArray2[vIndex2++] = 0f;

                            // uv
                            //verticeArray[vIndex++] = formUVS[v];

                            verticeArray2[vIndex2++] = formUVS[v].x;
                            verticeArray2[vIndex2++] = formUVS[v].y;
                            verticeArray2[vIndex2++] = formUVS[v].z;
                        }

                        triangleArray[tIndex++] = tValue;
                        triangleArray[tIndex++] = tValue + 2;
                        triangleArray[tIndex++] = tValue + 1;
                        triangleArray[tIndex++] = tValue;
                        triangleArray[tIndex++] = tValue + 3;
                        triangleArray[tIndex++] = tValue + 2;

                        tValue += 4;

                        mapping.Add(formIndex, formCount + formsCreated);
                        invMapping.Add(formCount + formsCreated, formIndex);

                        formsCreated++;
                    }
                }
            }

            newFormVerticeAmount = formVerticeCount * formsCreated;
            newFormTriangleAmount = formTriangleCount * formsCreated;

            meshData.SetVertexBufferParams(verticeCount + newFormVerticeAmount, vertexLayout);
            meshData.SetIndexBufferParams(triangleCount + newFormTriangleAmount, IndexFormat.UInt32);

            meshData.subMeshCount = 1;
            meshData.SetSubMesh(0, new SubMeshDescriptor(0, triangleCount + newFormTriangleAmount)); // check here the value

            Mesh.ApplyAndDisposeWritableMeshData(mesh, meshFilter.mesh, MeshUpdateFlags.Default);

            meshFilter.mesh.RecalculateNormals();
            meshFilter.mesh.RecalculateBounds();

            Debug.Log("Created: " + formsCreated);
            //Debug.Log("vIndexFinal: " + vIndex + " tIndexFinal: " + tIndex);

            //PrintMapping(); PrintInvMapping();

            return;
        }

        // REMOVE FORMS
        if (Input.GetKeyDown(KeyCode.G))
        {
            //var newModelAmount = rect.size.x * rect.size.y;

            var mesh = Mesh.AllocateWritableMeshData(meshFilter.mesh);
            var meshData = mesh[0];

            var verticeCount = meshData.vertexCount;
            int formCount = mapping.Count;
            int triangleCount = formCount * formTriangleCount;

            //int newModelVertexAmount = formVerticeCount * newModelAmount;
            //int newModelIndexAmount = formTriangleCount * newModelAmount;

            var verticeArray = meshData.GetVertexData<float3>();
            //var triangleArray = meshData.GetIndexData<int>();

            //var vIndex = vertexCount * vertexLayout.Length;
            //var tIndex = indexCount;

            var formsRemoved = 0;

            for (int i = 0; i < rect.size.x; i++)
            {
                for (int j = 0; j < rect.size.y; j++)
                {
                    var formPosition = new int2(rect.position.x + i, rect.position.y + j);
                    var formIndex = GetTileIndexFromTilePosition(formPosition);

                    //var modelWorldPosition = new float3(modelPosition.x, 0f, modelPosition.y);

                    var lastFormId = mapping.Count - 1;

                    if (mapping.TryGetValue(formIndex, out var formId))
                    {
                        var vIndex = formId * vertexLayout.Length * formVerticeCount;

                        if (!invMapping.TryGetValue(lastFormId, out var lastFormIdx))
                        {
                            Debug.Log("NOOOOOOOOOO");
                        }

                        var vLastIndex = lastFormId * vertexLayout.Length * formVerticeCount;

                        mapping.Remove(formIndex);
                        invMapping.Remove(formId);
                        
                        if (formId != lastFormId)
                        {
                            for (int v = 0; v < formVertices.Length; v++)
                            {
                                // vertice
                                verticeArray[vIndex++] = verticeArray[vLastIndex++];
                                // normal
                                verticeArray[vIndex++] = verticeArray[vLastIndex++];
                                // uv
                                verticeArray[vIndex++] = verticeArray[vLastIndex++];
                            }

                            mapping[lastFormIdx] = formId;

                            invMapping.Remove(lastFormId);
                            invMapping.Add(formId, lastFormIdx);
                        }

                        formsRemoved++;
                    }
                    else
                    {
                        Debug.Log("NOOOOOOOOOO");
                    }
                }
            }

            var newFormVerticeAmount = formVerticeCount * formsRemoved;
            var newFormTriangleAmount = formTriangleCount * formsRemoved;

            meshData.SetVertexBufferParams(verticeCount - newFormVerticeAmount, vertexLayout);
            meshData.SetIndexBufferParams(triangleCount - newFormTriangleAmount, IndexFormat.UInt32);

            meshData.subMeshCount = 1;
            meshData.SetSubMesh(0, new SubMeshDescriptor(0, triangleCount - newFormTriangleAmount)); // check here the value

            Mesh.ApplyAndDisposeWritableMeshData(mesh, meshFilter.mesh, MeshUpdateFlags.Default);

            meshFilter.mesh.RecalculateNormals();
            meshFilter.mesh.RecalculateBounds();

            //Debug.Log("Created: " + modelsCreated);
            //Debug.Log("vIndexFinal: " + vIndex + " tIndexFinal: " + tIndex);

            //Debug.Log("Map: " + mapping.Count + " Inv: " + invMapping.Count);

            PrintMapping(); PrintInvMapping();

            return;
        }
    }
}