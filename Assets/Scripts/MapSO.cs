using Unity.Mathematics;
using UnityEngine;

[CreateAssetMenu(fileName = "NewMap", menuName = "Map/Map")]
public class MapSO : ScriptableObject
{
    public int2 mapDimension;
    public float tileWidth;
    public float borderingWidth;
}