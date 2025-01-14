using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class Map : MonoBehaviour
{
    [SerializeField] private Transform groundTransform;
    [SerializeField] private CameraController cameraController;

    private MapSO mapData;

    public void Initialize (MapSO map)
    {
        if (!map.Validate)
        {
            Debug.LogWarning("MapSO is not a valid map settings. Use other map input data.");
            return;
        }

        mapData = map;

        float mapExtend = mapData.BorderingWidth * mapData.TileWidth;

        float2 unitDimension = mapData.MapUnitDimension;

        Vector2 mapSize = new float2(unitDimension.x + (mapExtend * 2f), unitDimension.y + (mapExtend * 2f));

        transform.position = new Vector3(mapSize.x * 0.5f - mapExtend, -0.001f, mapSize.y * 0.5f - mapExtend);
        transform.localScale = new Vector3(mapSize.x * 0.1f, 1f, mapSize.y * 0.1f);

        groundTransform.localScale = new Vector3(10f, 5f, 10f);
        groundTransform.localPosition = new Vector3(0f, -1f, 0f);

        cameraController.SetMapData(mapData);
    }
}