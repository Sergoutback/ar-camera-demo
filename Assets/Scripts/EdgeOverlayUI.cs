using UnityEngine;
using UnityEngine.UI;

public class EdgeOverlayUI : MonoBehaviour
{
    [Header("Overlay Images")]
    public RawImage previewLeft;
    public RawImage previewUp;

    [Header("Colors")]
    public Color matchColor = Color.green;
    public Color noMatchColor = Color.red;
    public Color defaultColor = new Color(1, 1, 1, 0.5f);

    private void Awake()
    {
        HideAll();
    }

    /// <summary>
    /// Shows the required overlays with edge textures.
    /// </summary>
    public void ShowEdges(Texture2D leftEdge = null, Texture2D upEdge = null, float edgePercent = 0.1f)
    {
        Debug.Log($"ShowEdges called: leftEdge={(leftEdge != null)}, upEdge={(upEdge != null)}");
        Debug.Log($"[ARCamera] Calling ShowEdges: left={(leftEdge != null)}, up={(upEdge != null)}, n={edgePercent}");

        if (leftEdge != null) Debug.Log("[EdgeOverlayUI] Showing PreviewLeft");
        if (upEdge != null) Debug.Log("[EdgeOverlayUI] Showing PreviewUp");

        if (previewLeft != null)
        {
            if (leftEdge != null)
            {
                previewLeft.texture = leftEdge;
                previewLeft.color = defaultColor;
                previewLeft.uvRect = new Rect(1f - edgePercent, 0, edgePercent, 1);
                previewLeft.gameObject.SetActive(true);
            }
            else
            {
                previewLeft.gameObject.SetActive(false);
            }
        }

        if (previewUp != null)
        {
            if (upEdge != null)
            {
                previewUp.texture = upEdge;
                previewUp.color = defaultColor;
                previewUp.uvRect = new Rect(0, 0, 1, edgePercent);
                previewUp.gameObject.SetActive(true);
            }
            else
            {
                previewUp.gameObject.SetActive(false);
            }
        }
    }

    /// <summary>
    /// Changes the frame/overlay color depending on the match.
    /// </summary>
    public void SetEdgeMatch(bool leftMatch, bool upMatch)
    {
        if (previewLeft != null && previewLeft.gameObject.activeSelf)
            previewLeft.color = leftMatch ? matchColor : noMatchColor;

        if (previewUp != null && previewUp.gameObject.activeSelf)
            previewUp.color = upMatch ? matchColor : noMatchColor;
    }

    /// <summary>
    /// Disables all overlays.
    /// </summary>
    public void HideAll()
    {
        if (previewLeft != null) previewLeft.gameObject.SetActive(false);
        if (previewUp != null) previewUp.gameObject.SetActive(false);
    }
}