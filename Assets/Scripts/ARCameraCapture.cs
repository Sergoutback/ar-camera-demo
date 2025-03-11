using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using UnityEngine.Android;

public class ARCameraCapture : MonoBehaviour
{
    public RawImage cameraPreview;
    public RawImage previewImage;
    private WebCamTexture webcamTexture;
    private Texture2D lastPhoto;

    void Start()
    {
        RequestPermissions();
        StartCoroutine(InitializeAfterPermissions());
        StartCoroutine(MonitorCamera());
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
    }

    void RequestPermissions()
    {
        Debug.Log("Checking permissions...");

        if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
        {
            Debug.LogWarning("Camera permission missing! Requesting...");
            Permission.RequestUserPermission(Permission.Camera);
        }
        else
        {
            Debug.Log("Camera permission granted.");
        }

        if (!Permission.HasUserAuthorizedPermission(Permission.ExternalStorageRead))
        {
            Debug.LogWarning("Read storage permission missing! Requesting...");
            Permission.RequestUserPermission(Permission.ExternalStorageRead);
        }
        else
        {
            Debug.Log("Read storage permission granted.");
        }

        if (!Permission.HasUserAuthorizedPermission(Permission.ExternalStorageWrite))
        {
            Debug.LogWarning("Write storage permission missing! Requesting...");
            Permission.RequestUserPermission(Permission.ExternalStorageWrite);
        }
        else
        {
            Debug.Log("Write storage permission granted.");
        }
    }

    IEnumerator InitializeAfterPermissions()
    {
        yield return new WaitForSeconds(2);

        if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
        {
            Debug.LogError("Camera permission denied! Please enable it in settings.");
            yield break;
        }

        StartCoroutine(InitializeCameraWithDelay());
    }

    IEnumerator InitializeCameraWithDelay()
    {
        Debug.Log("Initializing camera...");
        WebCamDevice[] devices = WebCamTexture.devices;

        if (devices.Length == 0)
        {
            Debug.LogError("No cameras found!");
            yield break;
        }

        string cameraName = devices[0].name;
        foreach (var device in devices)
        {
            if (!device.isFrontFacing)
            {
                cameraName = device.name;
                break;
            }
        }

        webcamTexture = new WebCamTexture(cameraName);
        webcamTexture.Play();
        AdjustCameraTexture();

        int attempts = 0;
        while (webcamTexture.width <= 16 && attempts < 10)
        {
            Debug.Log($"Waiting for camera to start... Attempt {attempts}");
            yield return new WaitForSeconds(0.5f);
            attempts++;
        }

        if (webcamTexture.width <= 16)
        {
            Debug.LogError("Failed to start camera. Is another app using it?");
            yield break;
        }

        if (cameraPreview != null)
        {
            cameraPreview.texture = webcamTexture;
            cameraPreview.enabled = true;
            Debug.Log("cameraPreview.enabled = " + cameraPreview.enabled);
        }
    }

    public void CapturePhoto()
    {
        Debug.Log("CapturePhoto() started");

        Camera[] cameras = Camera.allCameras;
        foreach (Camera cam in cameras)
        {
            Debug.Log("Active Camera: " + cam.name + " Depth: " + cam.depth);
        }

        Debug.Log("WebCamTexture isPlaying: " + (webcamTexture != null ? webcamTexture.isPlaying.ToString() : "null"));
        Debug.Log("cameraPreview.enabled = " + cameraPreview.enabled);

        StartCoroutine(TakeScreenshot());

        Debug.Log("CapturePhoto() finished");
    }


    private IEnumerator TakeScreenshot()
    {
        Debug.Log("TakeScreenshot() started");
        yield return new WaitForEndOfFrame();

        if (webcamTexture == null)
        {
            Debug.LogError("webcamTexture is null! Camera might not be started.");
            yield break;
        }

        if (!webcamTexture.isPlaying)
        {
            Debug.LogError("webcamTexture is not playing! Camera is stopped.");
            yield break;
        }

        Texture2D screenshot = new Texture2D(webcamTexture.width, webcamTexture.height, TextureFormat.RGB24, false);
        screenshot.SetPixels32(RotateTexture(webcamTexture.GetPixels32(), webcamTexture.width, webcamTexture.height, webcamTexture.videoRotationAngle));

        screenshot.Apply();
        
        if (webcamTexture.videoVerticallyMirrored)
        {
            previewImage.uvRect = new Rect(0, 1, 1, -1);
        }
        else
        {
            previewImage.uvRect = new Rect(0, 0, 1, 1);
        }

        if (previewImage != null)
        {
            previewImage.texture = screenshot;
            previewImage.enabled = true;
            Debug.Log("Preview image updated");
        }

        lastPhoto = screenshot;
        Debug.Log("TakeScreenshot() finished");
    }
    IEnumerator MonitorCamera()
    {
        while (true)
        {
            if (webcamTexture != null && !webcamTexture.isPlaying)
            {
                Debug.LogWarning("Camera stopped! Restarting...");
            
                webcamTexture.Stop();
                yield return new WaitForSeconds(1);

                if (!webcamTexture.isPlaying)
                {
                    Debug.Log("Restarting WebCamTexture...");
                    StartCoroutine(InitializeCameraWithDelay());
                }
            }
            yield return new WaitForSeconds(2);
        }
    }
    void AdjustCameraTexture()
    {
        if (cameraPreview != null && webcamTexture != null)
        {
            cameraPreview.texture = webcamTexture;
        
            cameraPreview.rectTransform.localEulerAngles = new Vector3(0, 0, -webcamTexture.videoRotationAngle);
        
            if (webcamTexture.videoVerticallyMirrored)
            {
                cameraPreview.uvRect = new Rect(0, 1, 1, -1);
            }
            else
            {
                cameraPreview.uvRect = new Rect(0, 0, 1, 1);
            }

            cameraPreview.enabled = true;
        }
    }
    private Color32[] RotateTexture(Color32[] pixels, int width, int height, int angle)
    {
        Color32[] rotatedPixels = new Color32[pixels.Length];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int newX = x;
                int newY = y;
                int index = y * width + x;

                switch (angle)
                {
                    case 90:
                        newX = height - 1 - y;
                        newY = x;
                        break;
                    case 180:
                        newX = width - 1 - x;
                        newY = height - 1 - y;
                        break;
                    case 270:
                        newX = y;
                        newY = width - 1 - x;
                        break;
                    default:
                        rotatedPixels[index] = pixels[index];
                        continue;
                }

                if (newX >= 0 && newX < width && newY >= 0 && newY < height)
                {
                    int newIndex = newY * width + newX;
                    rotatedPixels[newIndex] = pixels[index];
                }
            }
        }

        return rotatedPixels;
    }
}