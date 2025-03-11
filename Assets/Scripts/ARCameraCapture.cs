using System.IO;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Android;

public class ARCameraCapture : MonoBehaviour
{
    public RawImage cameraPreview;
    public GameObject previewPrefab;
    public Transform previewContainer;
    private WebCamTexture webcamTexture;
    private List<Texture2D> capturedPhotos = new List<Texture2D>();
    private List<GameObject> previewImages = new List<GameObject>();

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

        if (!Permission.HasUserAuthorizedPermission(Permission.ExternalStorageWrite))
        {
            Debug.LogWarning("Write storage permission missing! Requesting...");
            Permission.RequestUserPermission(Permission.ExternalStorageWrite);
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

        int attempts = 0;
        while (webcamTexture.width <= 16 && attempts < 10)
        {
            yield return new WaitForSeconds(0.5f);
            attempts++;
        }

        if (webcamTexture.width <= 16)
        {
            Debug.LogError("Failed to start camera.");
            yield break;
        }

        if (cameraPreview != null)
        {
            cameraPreview.texture = webcamTexture;
            cameraPreview.enabled = true;
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
            Debug.LogError("Camera is not available!");
            yield break;
        }

        Texture2D screenshot = new Texture2D(webcamTexture.width, webcamTexture.height, TextureFormat.RGB24, false);
        screenshot.SetPixels32(RotateTexture(webcamTexture.GetPixels32(), webcamTexture.width, webcamTexture.height, webcamTexture.videoRotationAngle));
        screenshot.Apply();

        AddPhotoToPreview(screenshot);

        if (capturedPhotos.Count >= 8)
        {
            CombinePhotos();
        }
    }

    private void AddPhotoToPreview(Texture2D photo)
    {
        if (previewImages.Count >= 8)
        {
            Debug.Log("Maximum of 8 photos reached. Combining images...");
            return;
        }

        GameObject newPreview = Instantiate(previewPrefab, previewContainer);
        RawImage rawImage = newPreview.GetComponent<RawImage>();
        rawImage.texture = photo;

        previewImages.Add(newPreview);
        capturedPhotos.Add(photo);
    }

    private void CombinePhotos()
    {
        int photoWidth = capturedPhotos[0].width;
        int photoHeight = capturedPhotos[0].height;
        int finalWidth = photoWidth * 4;
        int finalHeight = photoHeight * 2;

        Texture2D finalTexture = new Texture2D(finalWidth, finalHeight, TextureFormat.RGB24, false);

        for (int i = 0; i < 8; i++)
        {
            int x = (i % 4) * photoWidth;
            int y = (i / 4) * photoHeight;
            finalTexture.SetPixels(x, finalHeight - y - photoHeight, photoWidth, photoHeight, capturedPhotos[i].GetPixels());
        }

        finalTexture.Apply();
        SaveCombinedPhoto(finalTexture);

        ClearPreviews();
    }

    private void SaveCombinedPhoto(Texture2D finalTexture)
    {
        string filename = "Combined_Photo_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png";
        string path = Path.Combine(Application.persistentDataPath, filename);

        byte[] imageBytes = finalTexture.EncodeToPNG();
        File.WriteAllBytes(path, imageBytes);
        Debug.Log("Combined photo saved at: " + path);
    }

    private void ClearPreviews()
    {
        foreach (GameObject preview in previewImages)
        {
            Destroy(preview);
        }
        previewImages.Clear();
        capturedPhotos.Clear();
    }

    IEnumerator MonitorCamera()
    {
        while (true)
        {
            if (webcamTexture != null && !webcamTexture.isPlaying)
            {
                Debug.LogWarning("Camera stopped! Restarting...");
                StartCoroutine(InitializeCameraWithDelay());
            }
            yield return new WaitForSeconds(2);
        }
    }

    private Color32[] RotateTexture(Color32[] pixels, int width, int height, int angle)
    {
        Color32[] rotatedPixels = new Color32[pixels.Length];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int newX = x, newY = y;
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
