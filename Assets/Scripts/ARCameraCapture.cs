using System;
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
    private string lastSavedImagePath;


    void Start()
    {
        savedPhotosPath = Application.persistentDataPath;
        RequestPermissions();
        StartCoroutine(InitializeAfterPermissions());
        StartCoroutine(MonitorCamera());
        Screen.sleepTimeout = SleepTimeout.NeverSleep;

        galleryButton.onClick.AddListener(() =>
        {
            popup.SetActive(true);
            StartCoroutine(DisablePopupAfterDelay(3f));
            OpenSystemGallery();
        });

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

        byte[] imageBytes = screenshot.EncodeToJPG(95);
        string filename = $"Photo_{DateTime.Now:yyyyMMdd_HHmmss}.jpg";
        string tempPath = Path.Combine(Application.temporaryCachePath, filename);
        File.WriteAllBytes(tempPath, imageBytes);

        NativeGallery.SaveImageToGallery(tempPath, "ARCameraDemo", filename, (success, path) =>
        {
            Debug.Log($"[NativeGallery] Saved: {success}, Path: {path}");
            if (success) lastSavedImagePath = path;
        });


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

        string filename = "Combined_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".jpg";
        string tempPath = Path.Combine(Application.temporaryCachePath, filename);
        File.WriteAllBytes(tempPath, finalTexture.EncodeToJPG(95));

        NativeGallery.SaveImageToGallery(tempPath, "ARCameraDemo", filename, (success, path) =>
        {
            Debug.Log($"[NativeGallery] Combined image saved: {success}, Path: {path}");
            if (success) lastSavedImagePath = path;
        });


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
    
    public void OpenLastPhoto()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
    if (string.IsNullOrEmpty(lastSavedImagePath))
    {
        Debug.LogWarning("[ARCamera] No saved photo to open.");
        return;
    }

    try
    {
        using (AndroidJavaClass intentClass = new AndroidJavaClass("android.content.Intent"))
        using (AndroidJavaObject intent = new AndroidJavaObject("android.content.Intent"))
        using (AndroidJavaClass uriClass = new AndroidJavaClass("android.net.Uri"))
        using (AndroidJavaObject fileObj = new AndroidJavaObject("java.io.File", lastSavedImagePath))
        {
            AndroidJavaObject uri = uriClass.CallStatic<AndroidJavaObject>("fromFile", fileObj);

            intent.Call<AndroidJavaObject>("setAction", intentClass.GetStatic<string>("ACTION_VIEW"));
            intent.Call<AndroidJavaObject>("setDataAndType", uri, "image/*");
            intent.Call<AndroidJavaObject>("addFlags", intentClass.GetStatic<int>("FLAG_ACTIVITY_NEW_TASK"));

            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            {
                currentActivity.Call("startActivity", intent);
            }
        }
    }
    catch (System.Exception e)
    {
        Debug.LogError("[ARCamera] Failed to open photo: " + e.Message);
    }
#else
        Debug.Log("[ARCamera] Open photo is only available on Android device.");
#endif
    }
    
    public void OpenSystemGallery()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
    try
    {
        using (AndroidJavaClass intentClass = new AndroidJavaClass("android.content.Intent"))
        using (AndroidJavaObject intent = new AndroidJavaObject("android.content.Intent"))
        using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        using (AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
        {
            intent.Call<AndroidJavaObject>("setAction", intentClass.GetStatic<string>("ACTION_VIEW"));
            intent.Call<AndroidJavaObject>("setType", "image/*");
            intent.Call<AndroidJavaObject>("addFlags", intentClass.GetStatic<int>("FLAG_ACTIVITY_NEW_TASK"));

            currentActivity.Call("startActivity", intent);
        }
    }
    catch (Exception e)
    {
        Debug.LogError("[ARCamera] Failed to open gallery: " + e.Message);
    }
#else
        Debug.Log("[ARCamera] Gallery open only works on Android device.");
#endif
    }




}
