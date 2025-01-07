using UnityEditor;
using UnityEngine;

// assetdatabase commented

public class TextureArrayGen : MonoBehaviour
{
    [SerializeField]
    private string textureArrayName = "textureArrayUndefined";

    [SerializeField]
    private TextureFormat textureFormat = TextureFormat.RGBA32;

    [SerializeField]
    private FilterMode textureFilterMode;

    [SerializeField]
    private TextureWrapMode textureWrapMode;

    [SerializeField]
    private int anisioLevel = 1;

    [SerializeField]
    private Texture2D[] textureList;


    public void Awake ()
    {
        if (textureList == null)
        {
            Debug.LogError("class TextureArrayGen Awake : texture list is null.");
            return;
        }

        if (textureList.Length == 0)
        {
            Debug.LogError("class TextureArrayGen Awake : texture list is empty.");
            return;
        }

        Vector2Int textureSize = new Vector2Int(textureList[0].width, textureList[0].height);

        for (int i = 1; i < textureList.Length; i++)
        {
            if (textureList[i].width != textureSize.x || textureList[i].height != textureSize.y)
            {
                Debug.LogError("class TextureArrayGen Awake : texture index " + i + " not match the first texture size.");
                return;
            }
        }

        textureFormat = TextureFormat.RGBA32;

        Texture2DArray textureArray = new Texture2DArray(textureSize.x, textureSize.y, textureList.Length, textureFormat, true);

        for (int i = 0; i < textureList.Length; i++)
        {
            textureArray.SetPixels(textureList[i].GetPixels(0), i);
        }

        textureArray.filterMode = textureFilterMode;
        textureArray.wrapMode = textureWrapMode;
        textureArray.anisoLevel = anisioLevel;
        textureArray.Apply();
        

        //AssetDatabase.CreateAsset(textureArray, "Assets/Resources/Textures/TextureArray_" + textureArrayName + ".asset");
    }
}