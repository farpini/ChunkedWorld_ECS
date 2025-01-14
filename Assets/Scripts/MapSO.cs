using Unity.Mathematics;
using UnityEngine;

[CreateAssetMenu(fileName = "NewMap", menuName = "Map/Map")]
public class MapSO : ScriptableObject
{
    [SerializeField] private int2 mapDimension;
    [SerializeField] private int tileWidth;
    [SerializeField] private float borderingWidth;
    [SerializeField] private int chunkWidth;
    [SerializeField] private int maxHeight;

    private float2 unitDimension;

    public int2 MapDimension => mapDimension;
    public int TileWidth => tileWidth;
    public float2 MapUnitDimension => unitDimension;
    public float BorderingWidth => borderingWidth;
    public int ChunkWidth => chunkWidth;
    public bool Validate => (chunkWidth != 0 && (mapDimension.x % chunkWidth == 0) && (mapDimension.y % chunkWidth == 0));
    public int2 ChunkDimension => (mapDimension / chunkWidth);
    public int MaxHeight => maxHeight;

    private void OnEnable ()
    {
        unitDimension = new float2(mapDimension.x, mapDimension.y) * tileWidth;
    }
}