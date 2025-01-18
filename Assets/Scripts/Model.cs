/*
 * Written by Fernando Arpini Ferretto
 * https://github.com/farpini/ProceduralTileChunkedMap_ECS
 */

using System.Collections;
using UnityEngine;

public class Model : MonoBehaviour
{
    [SerializeField] private MeshFilter meshFilter;

    public Mesh Mesh => meshFilter.sharedMesh;
}