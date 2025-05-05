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

        float width = cameraPreview.rect.width;
        float height = cameraPreview.rect.height;
        float edgePercent = 0.2f;

        // PreviewLeft: 20% ширины, вся высота
        previewLeft.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width * edgePercent);
        previewLeft.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        previewLeft.anchoredPosition = Vector2.zero;

        // PreviewUp: вся ширина, 20% высоты
        previewUp.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        previewUp.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height * edgePercent);
        previewUp.anchoredPosition = Vector2.zero;
    }
}