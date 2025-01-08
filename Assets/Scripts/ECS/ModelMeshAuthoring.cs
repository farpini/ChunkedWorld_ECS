using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Rendering;

public class ModelMeshAuthoring : MonoBehaviour
{
    public GameObject modelRendererPrefab;
    public Model[] modelArray;

    private class ModelMesh : Baker<ModelMeshAuthoring>
    {
        public override void Bake (ModelMeshAuthoring authoring)
        {
            if (authoring.modelArray == null)
            {
                return;
            }

            var modelCount = authoring.modelArray.Length;

            var modelMappingEntity = GetEntity(TransformUsageFlags.None);

            AddComponent(modelMappingEntity, new RendererPrefabEntities
            {
                modelRenderer = GetEntity(authoring.modelRendererPrefab, TransformUsageFlags.Dynamic)
            });

            var modelDataEntityBuffer = AddBuffer<ModelDataEntityBuffer>(modelMappingEntity);

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

public class ModelBaker : Baker<Model>
{
    public override void Bake (Model authoring)
    {
        var entity = GetEntity(authoring, TransformUsageFlags.None);

        var mesh = Mesh.AcquireReadOnlyMeshData(authoring.Mesh);
        var meshData = mesh[0];

        var blobBuilder = new BlobBuilder(Allocator.Temp);
        ref var blobData = ref blobBuilder.ConstructRoot<MeshBlobData>();

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

        var vertexes = meshData.GetVertexData<float>();
        var vertexesArray = blobBuilder.Allocate<float>(ref blobData.vertexes, vertexes.Length);
        for (int j = 0; j < vertexesArray.Length; j++)
        {
            vertexesArray[j] = vertexes[j];
        }

        var indexes = meshData.GetIndexData<uint>();
        var indexesArray = blobBuilder.Allocate<uint>(ref blobData.indexes, indexes.Length);
        for (int j = 0; j < indexesArray.Length; j++)
        {
            indexesArray[j] = indexes[j];
        }

        var blobMeshDataRef = blobBuilder.CreateBlobAssetReference<MeshBlobData>(Allocator.Persistent);
        AddBlobAsset<MeshBlobData>(ref blobMeshDataRef, out _);

        AddComponent(entity, new MeshDataComponent
        {
            meshDataBlob = blobMeshDataRef,
            vertexCount = meshData.vertexCount,
            indexCount = indexesArray.Length,
            vertexAttributeDimension = vertexAttributeDimension
        });

        AddComponent(entity, new ModelDataComponent { modelId = -1 });
        AddBuffer<ModelChunkEntityBuffer>(entity);

        mesh.Dispose();
        blobBuilder.Dispose();
    }
}