/*
 * Written by Fernando Arpini Ferretto
 * https://github.com/farpini/ProceduralTileChunkedMap_ECS
 */

using Unity.Mathematics;
using UnityEngine;

public class Map : MonoBehaviour
{
    [SerializeField] private Transform waterTransform;
    [SerializeField] private MeshFilter groundRenderer;
    [SerializeField] private CameraController cameraController;

    private static float scalePrecision = 9.999f;

    public void Initialize (MapComponent mapData)
    {
        SetMapTransform(mapData);
    }

    private void SetMapTransform (MapComponent mapData)
    {
        float2 unitDimension = mapData.UnitDimension;

        Vector2 mapSize = new float2(unitDimension.x, unitDimension.y);

        transform.position = new Vector3(mapSize.x * 0.5f, -0.001f, mapSize.y * 0.5f);
        transform.localScale = new Vector3(mapSize.x * 0.1f, 1f, mapSize.y * 0.1f);

        waterTransform.gameObject.SetActive(mapData.MaxDepth > 0);

        waterTransform.localScale = new Vector3(scalePrecision, mapData.TileWidth * mapData.MaxDepth, scalePrecision);
        waterTransform.localPosition = new Vector3(0f, (mapData.MaxDepth - 1) / 0.5f, 0f);

        cameraController.SetMapData(mapData);
    }

    public void SetMeshMap (Mesh mesh, MapComponent mapData)
    {
        groundRenderer.mesh = mesh;

        SetMapTransform(mapData);
    }
}