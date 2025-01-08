using System.Collections;
using UnityEngine;

public class Model : MonoBehaviour
{
    [SerializeField] private MeshFilter meshFilter;

    public Mesh Mesh => meshFilter.sharedMesh;
}