using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class Map : MonoBehaviour
{
    [SerializeField] private MapSO mapData;
    [SerializeField] private Transform groundTransform;
    [SerializeField] private MeshFilter gridTileMesh;
    [SerializeField] private CameraController cameraController;

    private void Awake ()
    {
        float mapExtend = mapData.borderingWidth * mapData.tileWidth;

        float2 unitDimension = new float2(mapData.tileWidth * mapData.mapDimension.x, mapData.tileWidth * mapData.mapDimension.y);

        Vector2 mapSize = new float2(unitDimension.x + (mapExtend * 2f), unitDimension.y + (mapExtend * 2f));

        transform.position = new Vector3(mapSize.x * 0.5f - mapExtend, -0.001f, mapSize.y * 0.5f - mapExtend);
        transform.localScale = new Vector3(mapSize.x * 0.1f, 1f, mapSize.y * 0.1f);

        groundTransform.localScale = new Vector3(10f, 5f, 10f);
        groundTransform.localPosition = new Vector3(0f, -2.51f, 0f);

        CreateTileGrid(int3.zero, unitDimension, mapData.tileWidth);
    }

    private void Start ()
    {
        cameraController.SetMapData(mapData);
    }

    private void CreateTileGrid (int3 gridPos, float2 gridDim, float gridSize)
    {
        var mesh = new Mesh();
        var vertices = new List<Vector3>();
        var indices = new List<int>();

        Vector3 pos = new(gridPos.x * gridDim.x, 0, gridPos.y * gridDim.y);

        int gridCount = Mathf.RoundToInt(gridDim.x / gridSize);

        for (int i = 0; i <= gridCount; i++)
        {
            vertices.Add(new Vector3(i * gridSize, 0, 0) + pos);
            vertices.Add(new Vector3(i * gridSize, 0, gridDim.x) + pos);

            indices.Add(4 * i + 0);
            indices.Add(4 * i + 1);

            vertices.Add(new Vector3(0, 0, i * gridSize) + pos);
            vertices.Add(new Vector3(gridDim.x, 0, i * gridSize) + pos);

            indices.Add(4 * i + 2);
            indices.Add(4 * i + 3);
        }

        mesh.vertices = vertices.ToArray();
        mesh.SetIndices(indices.ToArray(), MeshTopology.Lines, 0);
        gridTileMesh.mesh = mesh;
    }
}