/*
 * Written by Fernando Arpini Ferretto
 * https://github.com/farpini/ProceduralTileChunkedMap_ECS
 */

using System.Collections;
using Unity.Mathematics;
using UnityEngine;

public class RectSelection : MonoBehaviour
{
    [SerializeField] private Material material;

    // To easily and fast implementation it's being used the material set value method.
    // Other option: create a componentdata to the rendering entity instead.
    public void SetRect (int2 rectPosition, int2 rectSize)
    {
        material.SetVector("_RectSelection", new Vector4(rectPosition.x, rectPosition.y, rectSize.x, rectSize.y));
    }

    public void Hide ()
    {
        material.SetVector("_RectSelection", new Vector4(0f, 0f, 0f, 0f));
    }
}