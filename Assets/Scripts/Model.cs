using System.Collections;
using UnityEngine;

public class Model : MonoBehaviour
{
    [SerializeField] private MeshFilter meshFilter;
    [SerializeField] private int modelId;

    public Mesh Mesh => meshFilter.sharedMesh;
    public int ModelId => modelId;
}