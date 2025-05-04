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

        if (cameraManager.TryAcquireLatestCpuImage(out var cpuImage))
        {
            cameraAspect = (float)cpuImage.width / cpuImage.height;
            cpuImage.Dispose();
        }
        if (cameraPreview == null || previewLeft == null || previewUp == null)
            return;

        float width = cameraPreview.rect.width;
        float height = cameraPreview.rect.height;
        
        previewLeft.GetComponent<RawImage>().uvRect = new Rect(0, 0, edgePercent, 1);
        previewUp.GetComponent<RawImage>().uvRect = new Rect(0, 1 - edgePercent, 1, edgePercent);

        previewLeft.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width * edgePercent);
        previewLeft.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);

        previewUp.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        previewUp.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height * edgePercent);

        previewLeft.position = cameraPreview.position
                               + new Vector3(-0.5f * (width - width * edgePercent), 0, 0);

        previewUp.position = cameraPreview.position
                             + new Vector3(0, 0.5f * (height - height * edgePercent), 0);
    }
}