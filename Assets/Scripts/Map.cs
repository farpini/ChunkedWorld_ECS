using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class Map : MonoBehaviour
{
    [SerializeField] private Transform waterTransform;
    [SerializeField] private MeshFilter groundRenderer;
    [SerializeField] private CameraController cameraController;

    public void Initialize (MapComponent mapData)
    {
        float2 unitDimension = mapData.UnitDimension;

        Vector2 mapSize = new float2(unitDimension.x, unitDimension.y);

        transform.position = new Vector3(mapSize.x * 0.5f, -0.001f, mapSize.y * 0.5f);
        transform.localScale = new Vector3(mapSize.x * 0.1f, 1f, mapSize.y * 0.1f);

        waterTransform.gameObject.SetActive(mapData.MaxDepth > 0);
        waterTransform.localScale = new Vector3(9.9999f, mapData.TileWidth * mapData.MaxDepth, 9.9999f);
        waterTransform.localPosition = new Vector3(0f, -(mapData.MaxDepth + mapData.TileWidth * 0.5f), 0f);

        cameraController.SetMapData(mapData);
    }

    public void SetMeshGround (Mesh mesh)
    {
        groundRenderer.mesh = mesh;
    }
}