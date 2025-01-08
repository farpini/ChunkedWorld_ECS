using UnityEngine;

public class RectView : MonoBehaviour
{
    [SerializeField] private MeshRenderer meshRenderer;
    [SerializeField] private Color validCreatingColor;
    [SerializeField] private Color validRemovingColor;
    [SerializeField] private Color invalidColor;

    public void SetValidation (bool isCreating, bool isValid)
    {
        meshRenderer.material.color = isValid ? (isCreating ? validCreatingColor : validRemovingColor) : invalidColor;
    }

    public void Show (bool toShow)
    {
        gameObject.SetActive(toShow);
    }

    public void SetRect (RectInt rect, int tileWidth)
    {
        var halfTileWidth = tileWidth * 0.5f;
        var halfSize = new Vector2(rect.size.x, rect.size.y) * halfTileWidth;
        var tileScale = 0.1f * tileWidth;
        transform.position = new Vector3((rect.position.x * tileWidth) + halfSize.x, 0.01f, (rect.position.y * tileWidth) + halfSize.y);
        transform.localScale = new Vector3(rect.size.x * tileScale, 1f, rect.size.y * tileScale);
    }
}