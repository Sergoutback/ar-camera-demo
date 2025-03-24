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
    public Button galleryButton;
    public GameObject popup;

    private WebCamTexture webcamTexture;
    private List<Texture2D> capturedPhotos = new List<Texture2D>();
    private List<GameObject> previewImages = new List<GameObject>();
    private string savedPhotosPath;
    private int photoCounter = 0;

    void Start()
    {
        savedPhotosPath = Application.persistentDataPath;
        RequestPermissions();
        StartCoroutine(InitializeAfterPermissions());
        StartCoroutine(MonitorCamera());
        Screen.sleepTimeout = SleepTimeout.NeverSleep;

        galleryButton.onClick.AddListener(OpenGallery);
    }

    void RequestPermissions()
    {
        if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
        {
            Permission.RequestUserPermission(Permission.Camera);
        }
        if (!Permission.HasUserAuthorizedPermission(Permission.ExternalStorageWrite))
        {
            Permission.RequestUserPermission(Permission.ExternalStorageWrite);
        }
    }

    IEnumerator InitializeAfterPermissions()
    {
        yield return new WaitForSeconds(2);
        if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
        {
            Debug.LogError("Camera permission denied!");
            yield break;
        }
        StartCoroutine(InitializeCameraWithDelay());
    }

    IEnumerator InitializeCameraWithDelay()
    {
        WebCamDevice[] devices = WebCamTexture.devices;
        if (devices.Length == 0) yield break;

        string cameraName = devices[0].name;
        foreach (var device in devices)
        {
            if (!device.isFrontFacing)
            {
                cameraName = device.name;
                break;
            }
        }

        webcamTexture = new WebCamTexture(cameraName, 1280, 720); // Set high resolution
        webcamTexture.Play();

        yield return new WaitUntil(() => webcamTexture.width > 16);
        cameraPreview.texture = webcamTexture;
        cameraPreview.enabled = true;

        Debug.Log($"Camera started: {cameraName}, Resolution: {webcamTexture.width}x{webcamTexture.height}");
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

    public void CapturePhoto()
    {
        StartCoroutine(TakeScreenshot());
    }

    private IEnumerator TakeScreenshot()
    {
        yield return new WaitForEndOfFrame();
        if (webcamTexture == null || !webcamTexture.isPlaying) yield break;

        Texture2D screenshot = new Texture2D(webcamTexture.width, webcamTexture.height, TextureFormat.RGB24, false);
        screenshot.SetPixels32(webcamTexture.GetPixels32());
        screenshot.Apply();

        if (Screen.orientation == ScreenOrientation.Portrait || Screen.orientation == ScreenOrientation.PortraitUpsideDown)
        {
            screenshot = RotateTexture90(screenshot);
        }

        string filename = $"Photo_{photoCounter:D2}.png";
        string path = Path.Combine(savedPhotosPath, filename);
        File.WriteAllBytes(path, screenshot.EncodeToPNG());

        photoCounter++;
        AddPhotoToPreview(screenshot);

        if (capturedPhotos.Count >= 8)
        {
            StartCoroutine(HandleFullPreview());
        }
    }


    private void AddPhotoToPreview(Texture2D photo)
    {
        GameObject newPreview = Instantiate(previewPrefab, previewContainer);
        newPreview.GetComponent<RawImage>().texture = photo;
        previewImages.Add(newPreview);
        capturedPhotos.Add(photo);
    }

    private IEnumerator HandleFullPreview()
    {
        yield return new WaitForSeconds(1);
        CombinePhotos();
        ClearPreviews();
    }

    private void CombinePhotos()
    {
        int w = capturedPhotos[0].width, h = capturedPhotos[0].height;
        Texture2D finalTexture = new Texture2D(w * 4, h * 2, TextureFormat.RGB24, false);

        for (int i = 0; i < 8; i++)
        {
            int row = i / 4;
            int col = i % 4;
            int flippedRow = 1 - row; // flip vertically
            finalTexture.SetPixels(col * w, flippedRow * h, w, h, capturedPhotos[i].GetPixels());
        }
        finalTexture.Apply();

        string filename = "Combined_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png";
        string path = Path.Combine(savedPhotosPath, filename);
        File.WriteAllBytes(path, finalTexture.EncodeToPNG());

        galleryButton.image.sprite = Sprite.Create(finalTexture, new Rect(0, 0, finalTexture.width, finalTexture.height), new Vector2(0.5f, 0.5f));
    }


    private void ClearPreviews()
    {
        foreach (GameObject preview in previewImages) Destroy(preview);
        previewImages.Clear();
        capturedPhotos.Clear();
    }

    private void OpenGallery()
    {
#if UNITY_EDITOR
        Application.OpenURL("file://" + savedPhotosPath);
#elif UNITY_ANDROID
        popup.SetActive(true);
        StartCoroutine(DisablePopupAfterDelay(3f));
#endif
    }

    private IEnumerator DisablePopupAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        popup.SetActive(false);
    }
    private Texture2D RotateTexture90(Texture2D original)
    {
        int width = original.width;
        int height = original.height;
        Texture2D rotated = new Texture2D(height, width);

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                rotated.SetPixel(y, width - x - 1, original.GetPixel(x, y));
            }
        }

        rotated.Apply();
        return rotated;
    }

}
