using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class ARCameraCapture : MonoBehaviour
{
    public RawImage previewImage; // UI element for overlay
    private Texture2D lastPhoto;

    void Start()
    {
        if (previewImage != null)
            previewImage.enabled = false;
    }

    public void CapturePhoto()
    {
        StartCoroutine(TakeScreenshot());
    }

    private IEnumerator TakeScreenshot()
    {
        yield return new WaitForEndOfFrame();

        // Capture the current camera frame
        int width = Screen.width;
        int height = Screen.height;
        Texture2D screenshot = new Texture2D(width, height, TextureFormat.RGB24, false);
        screenshot.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        screenshot.Apply();

        // UI update
        if (previewImage != null)
        {
            previewImage.texture = screenshot;
            previewImage.enabled = true;
        }

        // Save for next frame
        lastPhoto = screenshot;
    }
}