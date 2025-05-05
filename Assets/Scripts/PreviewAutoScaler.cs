using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;

public class PreviewAutoScaler : MonoBehaviour
{
    public RectTransform cameraPreview;
    public RectTransform previewLeft;
    public RectTransform previewUp;
    public ARCameraManager cameraManager;

    public float cameraAspect = 1.0f;

    // Thickness of the comparison strip (as a percentage of CameraPreview width/height)
    [Range(0.01f, 0.5f)]
    public float edgePercent = 0.1f;

    void LateUpdate()
    {
        if (cameraManager == null)
            return;

        if (cameraPreview == null || previewLeft == null || previewUp == null)
            return;

        RawImage leftRaw = previewLeft.GetComponent<RawImage>();
        RawImage upRaw = previewUp.GetComponent<RawImage>();

        if (leftRaw.texture != null)
        {
            leftRaw.uvRect = new Rect(0, 0, 1, 1);
            float edgeWidth = leftRaw.texture.width;
            float edgeHeight = leftRaw.texture.height;
            previewLeft.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, edgeWidth);
            previewLeft.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, edgeHeight);
            // Place the cameraPreview to the LEFT edge
            previewLeft.position = cameraPreview.position;
        }
        if (upRaw.texture != null)
        {
            upRaw.uvRect = new Rect(0, 0, 1, 1);
            float edgeWidth = upRaw.texture.width;
            float edgeHeight = upRaw.texture.height;
            previewUp.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, edgeWidth);
            previewUp.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, edgeHeight);
            // Place the cameraPreview to the TOP edge
            previewUp.position = cameraPreview.position;
        }
    }
}