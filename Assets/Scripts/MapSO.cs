using Unity.Mathematics;
using UnityEngine;

[CreateAssetMenu(fileName = "NewMap", menuName = "Map/Map")]
public class MapSO : ScriptableObject
{
    [SerializeField] private int2 mapDimension;
    [SerializeField] private int tileWidth;
    [SerializeField] private int chunkWidth;

    public int2 MapDimension => mapDimension;
    public int TileWidth => tileWidth;
    public int ChunkWidth => chunkWidth;
    public int2 ChunkDimension => (mapDimension / chunkWidth);
}