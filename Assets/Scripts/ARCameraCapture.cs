using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class ARCameraCapture : MonoBehaviour
{
    public RawImage cameraPreview;
    public RawImage previewImage;
    private WebCamTexture webcamTexture;
    private Texture2D lastPhoto;

    void Start()
    {
        InitializeCamera();
    }

    private void InitializeCamera()
    {
        WebCamDevice[] devices = WebCamTexture.devices;
        if (devices.Length == 0)
        {
            Debug.LogError("Camera not found!");
            return;
        }

        string cameraName = devices[0].name;
#if UNITY_EDITOR
        cameraName = devices[0].name;
#elif UNITY_ANDROID
        // On phones, we look for the main (rear) camera
        foreach (var device in devices)
        {
            if (!device.isFrontFacing) // Берем первую заднюю камеру
            {
                cameraName = device.name;
                break;
            }
        }
#endif

        webcamTexture = new WebCamTexture(cameraName);
        webcamTexture.Play();

        if (cameraPreview != null)
        {
            cameraPreview.texture = webcamTexture;
            cameraPreview.enabled = true;
        }

        if (previewImage != null)
        {
            previewImage.enabled = false;
        }
    }

    public void CapturePhoto()
    {
        StartCoroutine(TakeScreenshot());
    }

    private IEnumerator TakeScreenshot()
    {
        yield return new WaitForEndOfFrame();

        if (webcamTexture == null || !webcamTexture.isPlaying)
        {
            Debug.LogError("The camera is not working!");
            yield break;
        }

        int width = webcamTexture.width;
        int height = webcamTexture.height;
        Texture2D screenshot = new Texture2D(width, height, TextureFormat.RGB24, false);
        screenshot.SetPixels(webcamTexture.GetPixels());
        screenshot.Apply();

        if (previewImage != null)
        {
            previewImage.texture = screenshot;
            previewImage.enabled = true;
        }

        lastPhoto = screenshot;
    }
}
