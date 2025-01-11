using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using MeshGenerator;

public class MeshDataAuthoring : MonoBehaviour
{
    public GameObject chunkModelRendererPrefab;
    public GameObject chunkTileRendererPrefab;
    public GameObject tilePrefab;
    public Model[] modelArray;

    private class DataMesh : Baker<MeshDataAuthoring>
    {
        public override void Bake (MeshDataAuthoring authoring)
        {
            var meshDataMappingEntity = GetEntity(TransformUsageFlags.None);

            AddComponent(meshDataMappingEntity, new RendererPrefabEntities
            {
                chunkModelRenderer = GetEntity(authoring.chunkModelRendererPrefab, TransformUsageFlags.Dynamic),
                chunkTileRenderer = GetEntity(authoring.chunkTileRendererPrefab, TransformUsageFlags.Dynamic),
                tilePrefab = GetEntity(authoring.tilePrefab, TransformUsageFlags.Dynamic)
            });

            var modelDataEntityBuffer = AddBuffer<ModelDataEntityBuffer>(meshDataMappingEntity);

            if (authoring.modelArray != null)
            {
                var modelCount = authoring.modelArray.Length;

                for (int i = 0; i < modelCount; i++)
                {
                    modelDataEntityBuffer.Add(new ModelDataEntityBuffer
                    {
                        Value = GetEntity(authoring.modelArray[i], TransformUsageFlags.Dynamic)
                    });
                }
            }
        }
    }
}

public class ModelBaker : Baker<Model>
{
    public override void Bake (Model authoring)
    {
        var entity = GetEntity(authoring, TransformUsageFlags.None);

        var mesh = Mesh.AcquireReadOnlyMeshData(authoring.Mesh);
        var meshData = mesh[0];

        var blobBuilder = new BlobBuilder(Allocator.Temp);
        ref var blobData = ref blobBuilder.ConstructRoot<MeshBlobInfo>();

        var modelNameArray = blobBuilder.Allocate<char>(ref blobData.meshName, authoring.gameObject.name.Length);
        for (int j = 0; j < modelNameArray.Length; j++)
        {
            modelNameArray[j] = authoring.gameObject.name[j];
        }

        var vertexAttributes = authoring.Mesh.GetVertexAttributes();
        var vertexAttributeDimension = 0;

        var vertexAttributeArray = blobBuilder.Allocate<VertexAttributeDescriptor>(ref blobData.attributes, vertexAttributes.Length);
        for (int j = 0; j < vertexAttributeArray.Length; j++)
        {
            vertexAttributeArray[j] = new VertexAttributeDescriptor
            {
                attribute = vertexAttributes[j].attribute,
                dimension = vertexAttributes[j].dimension,
                format = vertexAttributes[j].format,
                stream = vertexAttributes[j].stream,
            };

            vertexAttributeDimension += vertexAttributes[j].dimension;
        }

        var blobMeshDataRef = blobBuilder.CreateBlobAssetReference<MeshBlobInfo>(Allocator.Persistent);
        AddBlobAsset<MeshBlobInfo>(ref blobMeshDataRef, out _);

        AddSharedComponent(entity, new MeshBlobInfoComponent
        {
            meshInfoBlob = blobMeshDataRef,
            vertexCount = meshData.vertexCount,
            indexCount = meshData.GetSubMesh(0).indexCount,
            vertexAttributeDimension = vertexAttributeDimension
        });

        AddComponent(entity, new ModelDataComponent { modelId = -1 });

        AddBuffer<ChunkRendererEntityBuffer>(entity);

        mesh.Dispose();
        blobBuilder.Dispose();
    }
}

public class MapTileBaker : Baker<MapTile>
{
    public override void Bake (MapTile authoring)
    {
        var entity = GetEntity(authoring, TransformUsageFlags.None);

        AddComponent<TerrainComponent>(entity);

        var blobBuilder = new BlobBuilder(Allocator.Temp);
        ref var blobData = ref blobBuilder.ConstructRoot<MeshBlobData>();

        var tileTypeCount = 19;
        var tileVertexCount = 4;
        var tileIndexCount = 6;
        var vertexAttributeCount = 3;
        var vertexesCount = tileTypeCount * tileVertexCount * vertexAttributeCount;
        var indexesCount = tileTypeCount * tileIndexCount;

        var vertexesArray = blobBuilder.Allocate<float3>(ref blobData.vertexes, vertexesCount);
        var indexesArray = blobBuilder.Allocate<uint>(ref blobData.indexes, indexesCount);

        var vertexStart = 0;
        var indexStart = 0;
        var indexValue = 0u;

        for (int i = 0; i < tileTypeCount; i++)
        {
            if (TileMeshGenerator.InsertTileTerrainType((TileTerrainType)i, vertexStart, indexStart, ref vertexesArray, ref indexesArray))
            {
                vertexStart += (tileVertexCount * vertexAttributeCount);
                indexStart += tileIndexCount;
            }
        }

        var blobMeshDataRef = blobBuilder.CreateBlobAssetReference<MeshBlobData>(Allocator.Persistent);
        AddBlobAsset<MeshBlobData>(ref blobMeshDataRef, out _);

        AddSharedComponent(entity, new MeshBlobDataComponent
        {
            meshDataBlob = blobMeshDataRef
        });

        blobBuilder.Dispose();

        blobBuilder = new BlobBuilder(Allocator.Temp);
        ref var blobInfo = ref blobBuilder.ConstructRoot<MeshBlobInfo>();

        var tileString = "Tile";
        var meshNameArray = blobBuilder.Allocate<char>(ref blobInfo.meshName, tileString.Length);
        for (int j = 0; j < meshNameArray.Length; j++)
        {
            meshNameArray[j] = tileString[j];
        }

        var vertexAttributes = new VertexAttributeDescriptor[]
        {
            new VertexAttributeDescriptor
            {
                attribute = VertexAttribute.Position, format = VertexAttributeFormat.Float32, dimension = 3
            },
            new VertexAttributeDescriptor
            {
                attribute = VertexAttribute.Normal, format = VertexAttributeFormat.Float32, dimension = 3
            },
            new VertexAttributeDescriptor
            {
                attribute = VertexAttribute.TexCoord0, format = VertexAttributeFormat.Float32, dimension = 3
            }
        };

        var vertexAttributeDimension = 0;

        var vertexAttributeArray = blobBuilder.Allocate<VertexAttributeDescriptor>(ref blobInfo.attributes, vertexAttributes.Length);
        for (int j = 0; j < vertexAttributeArray.Length; j++)
        {
            vertexAttributeArray[j] = new VertexAttributeDescriptor
            {
                attribute = vertexAttributes[j].attribute,
                dimension = vertexAttributes[j].dimension,
                format = vertexAttributes[j].format,
                stream = vertexAttributes[j].stream,
            };

            vertexAttributeDimension += vertexAttributes[j].dimension;
        }

        var blobMeshInfoRef = blobBuilder.CreateBlobAssetReference<MeshBlobInfo>(Allocator.Persistent);
        AddBlobAsset<MeshBlobInfo>(ref blobMeshInfoRef, out _);

        AddSharedComponent(entity, new MeshBlobInfoComponent
        {
            meshInfoBlob = blobMeshInfoRef,
            vertexCount = tileVertexCount,
            indexCount = tileIndexCount,
            vertexAttributeDimension = vertexAttributeDimension
        });

        blobBuilder.Dispose();

        AddBuffer<ChunkRendererEntityBuffer>(entity);
    }
}