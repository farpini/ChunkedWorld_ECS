/*
 * Written by Fernando Arpini Ferretto
 * https://github.com/farpini/ProceduralTileChunkedMap_ECS
 */

using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
public class Model : MonoBehaviour
{
    [SerializeField] private MeshFilter meshFilter;

    public Mesh Mesh => meshFilter.sharedMesh;
}