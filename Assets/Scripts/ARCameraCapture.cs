// ARCameraCapture.cs — fixed collage orientation and added gyro/location to metadata

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Unity.Collections;
using TMPro;
using UnityEngine.SceneManagement;

public class ARCameraCapture : MonoBehaviour
{
    [SerializeField] private GameObject previewPrefab;
    [SerializeField] private Transform previewContainer;
    [SerializeField] private Button fotoButton;
    [SerializeField] private Button galleryButton;
    [SerializeField] private Button exportZipButton;
    [SerializeField] private GameObject popup;
    [SerializeField] private TextMeshProUGUI popupText;
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private CameraStatusUI cameraStatusUI;

    private List<Texture2D> capturedPhotos = new List<Texture2D>();
    private List<GameObject> previewImages = new List<GameObject>();
    private string lastSavedImagePath;
    private string currentSessionId;

    private Quaternion baseGyroRotation;
    private Vector3 basePosition;
    private bool baseRotationSet = false;
    
    private bool sessionStarted = false;

    private float currentLatitude;
    private float currentLongitude;

    private List<PhotoMetadata> sessionPhotos = new List<PhotoMetadata>();

    private ARCameraManager arCameraManager;

    void Awake()
    {
        arCameraManager = Camera.main.GetComponent<ARCameraManager>();
    }

    void Start()
    {
        RequestPermissions();
        currentSessionId = Guid.NewGuid().ToString();

        galleryButton.onClick.AddListener(OpenSystemGallery);
        exportZipButton.onClick.AddListener(OnExportSessionZipButton);
        fotoButton.onClick.AddListener(CapturePhoto);

        Input.gyro.enabled = true;
        StartCoroutine(UpdateLocation());
    }

    void Update()
    {
        if (cameraTransform == null)
        {
            cameraStatusUI.ShowWaiting();
            return;
        }
        if (!baseRotationSet)
        {
            cameraStatusUI.ShowWaiting();
            return;
        }

        Vector3 currentPosition = cameraTransform.position;
        Quaternion currentGyro = Input.gyro.attitude;
        Quaternion relativeGyro = Quaternion.Inverse(baseGyroRotation) * currentGyro;
        Vector3 relativeEuler = relativeGyro.eulerAngles;
        Vector3 relativePos = currentPosition - basePosition;

#if UNITY_EDITOR
        relativePos = Vector3.zero;
#endif

        if (relativePos.magnitude < 0.01f)
            cameraStatusUI.ShowNoMovement(relativePos, relativeEuler);
        else
            cameraStatusUI.ShowReady(relativePos, relativeEuler);
    }

    public void CapturePhoto()
    {
        if (!sessionStarted)
        {
            StartNewSession();
            sessionStarted = true;
        }

        if (!baseRotationSet)
        {
            baseGyroRotation = Input.gyro.attitude;
            basePosition = cameraTransform.position;
            baseRotationSet = true;
        }

        StartCoroutine(CaptureARPhoto());
    }


    private IEnumerator CaptureARPhoto()
    {
        yield return new WaitForEndOfFrame();

        if (arCameraManager == null)
        {
            Debug.LogError("[ARCamera] ARCameraManager not assigned.");
            yield break;
        }

        if (!arCameraManager.TryAcquireLatestCpuImage(out XRCpuImage image))
        {
            Debug.LogWarning("[ARCamera] Could not acquire AR camera image.");
            yield break;
        }

        Texture2D photo;

        using (image)
        {
            var conversionParams = new XRCpuImage.ConversionParams
            {
                inputRect = new RectInt(0, 0, image.width, image.height),
                outputDimensions = new Vector2Int(image.width, image.height),
                outputFormat = TextureFormat.RGB24,
                transformation = XRCpuImage.Transformation.MirrorY
            };

            var rawData = new NativeArray<byte>(image.GetConvertedDataSize(conversionParams), Allocator.Temp);
            image.Convert(conversionParams, rawData);

            photo = new Texture2D(conversionParams.outputDimensions.x, conversionParams.outputDimensions.y, conversionParams.outputFormat, false);
            photo.LoadRawTextureData(rawData);
            photo.Apply();

            rawData.Dispose();
        }

        photo = RotateTexture90CW(photo);

        string filename = $"Photo_{DateTime.Now:yyyyMMdd_HHmmss}.png";
        string path = Path.Combine(Application.persistentDataPath, filename);
        File.WriteAllBytes(path, photo.EncodeToPNG());

        NativeGallery.SaveImageToGallery(path, "ARCameraDemo", filename, (success, outputPath) =>
        {
            if (success) lastSavedImagePath = outputPath;
        });

        AddPhotoToPreview(photo, path);
        ShowPopup("📸 Photo saved to Gallery");
    }

    private void AddPhotoToPreview(Texture2D photo, string imagePath)
    {
        GameObject newPreview = Instantiate(previewPrefab, previewContainer);
        newPreview.GetComponent<RawImage>().texture = photo;
        previewImages.Add(newPreview);
        capturedPhotos.Add(photo);

        Quaternion currentGyro = Input.gyro.attitude;
        Quaternion relativeGyro = Quaternion.Inverse(baseGyroRotation) * currentGyro;
        Vector3 relativeEuler = relativeGyro.eulerAngles;
        Vector3 currentPosition = cameraTransform.position;
        Vector3 relativePos = currentPosition - basePosition;

        PhotoMetadata meta = new PhotoMetadata
        {
            photoId = Guid.NewGuid().ToString(),
            sessionId = currentSessionId,
            timestamp = DateTime.Now.ToString("o"),
            path = imagePath,
            width = photo.width,
            height = photo.height,
            quality = 95,
            gyroRotationRate = Input.gyro.rotationRateUnbiased,
            gyroAttitude = currentGyro,
            relativeGyroAttitude = relativeGyro,
            relativeEulerAngles = relativeEuler,
            relativePosition = relativePos,
            gyroEulerAngles = currentGyro.eulerAngles,
            latitude = currentLatitude,
            longitude = currentLongitude
        };
        sessionPhotos.Add(meta);

        if (capturedPhotos.Count == 8)
        {
            Texture2D combined = new Texture2D(photo.width * 4, photo.height * 2);
            for (int i = 0; i < 8; i++)
            {
                int col = i % 4;
                int row = 1 - (i / 4);
                combined.SetPixels(col * photo.width, row * photo.height, photo.width, photo.height, capturedPhotos[i].GetPixels());
            }
            combined.Apply();
            galleryButton.image.sprite = Sprite.Create(combined, new Rect(0, 0, combined.width, combined.height), new Vector2(0.5f, 0.5f));

            string combinedName = $"Combined_{DateTime.Now:yyyyMMdd_HHmmss}.png";
            string combinedPath = Path.Combine(Application.persistentDataPath, combinedName);
            File.WriteAllBytes(combinedPath, combined.EncodeToPNG());
            NativeGallery.SaveImageToGallery(combinedPath, "ARCameraDemo", combinedName);

            ShowPopup("🧵 Collage saved to Gallery");

            FinalizeMiniSession(combined, combinedPath);
        }
    }
    private void FinalizeMiniSession(Texture2D combined, string combinedPath)
    {
        sessionPhotos.Add(new PhotoMetadata
        {
            photoId = Guid.NewGuid().ToString(),
            sessionId = currentSessionId,
            timestamp = DateTime.Now.ToString("o"),
            path = combinedPath,
            width = combined.width,
            height = combined.height,
            quality = 95
        });

        string sessionJson = JsonHelper.ToJson(sessionPhotos.ToArray(), true);
        string sessionFile = Path.Combine(Application.persistentDataPath, $"Session_{currentSessionId}.json");
        File.WriteAllText(sessionFile, sessionJson);

        string exportDir = Path.Combine(Application.temporaryCachePath, $"Session_{currentSessionId}_Export");
        Directory.CreateDirectory(exportDir);

        foreach (var meta in sessionPhotos)
        {
            if (File.Exists(meta.path))
                File.Copy(meta.path, Path.Combine(exportDir, Path.GetFileName(meta.path)), true);
        }

        File.Copy(sessionFile, Path.Combine(exportDir, Path.GetFileName(sessionFile)), true);

        string zipPath = Path.Combine(Application.persistentDataPath, $"Session_{currentSessionId}.zip");
        if (File.Exists(zipPath)) File.Delete(zipPath);
        ZipFile.CreateFromDirectory(exportDir, zipPath);

#if UNITY_ANDROID && !UNITY_EDITOR
    AndroidMediaScanner.ScanFile(zipPath);
#endif
        ShowPopup("📦 Mini-session exported to ZIP");

        Debug.Log("📦 Mini-session exported to ZIP: " + zipPath);

        foreach (var obj in previewImages)
            Destroy(obj);
        previewImages.Clear();

        foreach (Transform child in previewContainer)
            DestroyImmediate(child.gameObject);
        
        PhotoStitcher stitcher = FindObjectOfType<PhotoStitcher>();
        if (stitcher != null)
        {
            ShowPopup("🧷 Stitching photos...");
            stitcher.RunStitchExternally(capturedPhotos.ToArray(), sessionPhotos.ToArray());
        }
        
        capturedPhotos.Clear();
        sessionPhotos.Clear();
        currentSessionId = Guid.NewGuid().ToString();
    }



    private void SaveSessionMetadata()
    {
        string sessionJson = JsonHelper.ToJson(sessionPhotos.ToArray(), true);
        string sessionFile = Path.Combine(Application.persistentDataPath, $"Session_{currentSessionId}.json");
        File.WriteAllText(sessionFile, sessionJson);
    }

    private Texture2D RotateTexture90CW(Texture2D original)
    {
        int width = original.width;
        int height = original.height;
        Texture2D rotated = new Texture2D(height, width);

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                rotated.SetPixel(height - y - 1, x, original.GetPixel(x, y));
            }
        }

        rotated.Apply();
        return rotated;
    }

    private void ShowPopup(string message)
    {
        if (popup != null && popupText != null)
        {
            popup.SetActive(true);
            popupText.text = message;
            StartCoroutine(HidePopupAfterDelay(2f));
        }
    }

    private IEnumerator HidePopupAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        popup.SetActive(false);
    }

    public void OnExportSessionZipButton()
    {
        string exportDir = Path.Combine(Application.temporaryCachePath, $"Session_{currentSessionId}_Export");
        Directory.CreateDirectory(exportDir);

        foreach (var meta in sessionPhotos)
        {
            if (File.Exists(meta.path))
                File.Copy(meta.path, Path.Combine(exportDir, Path.GetFileName(meta.path)), true);

            string jsonPath = Path.ChangeExtension(meta.path, ".json");
            if (File.Exists(jsonPath))
                File.Copy(jsonPath, Path.Combine(exportDir, Path.GetFileName(jsonPath)), true);
        }

        string sessionJson = JsonHelper.ToJson(sessionPhotos.ToArray(), true);
        File.WriteAllText(Path.Combine(exportDir, $"Session_{currentSessionId}.json"), sessionJson);

        string zipPath = Path.Combine(Application.persistentDataPath, $"Session_{currentSessionId}.zip");
        if (File.Exists(zipPath)) File.Delete(zipPath);
        ZipFile.CreateFromDirectory(exportDir, zipPath);

        AndroidMediaScanner.ScanFile(zipPath);
        ShowPopup("📦 Session exported to ZIP");
    }
    public void StartNewSession()
    {
        baseRotationSet = false;
        currentSessionId = Guid.NewGuid().ToString();

        foreach (var obj in previewImages)
            Destroy(obj);
        previewImages.Clear();
        capturedPhotos.Clear();
        sessionPhotos.Clear();

        foreach (Transform child in previewContainer)
            DestroyImmediate(child.gameObject);

        galleryButton.image.sprite = null;
    }


    private IEnumerator UpdateLocation()
    {
        if (!Input.location.isEnabledByUser) yield break;
        Input.location.Start();

        int maxWait = 10;
        while (Input.location.status == LocationServiceStatus.Initializing && maxWait > 0)
        {
            yield return new WaitForSeconds(1);
            maxWait--;
        }

        if (Input.location.status == LocationServiceStatus.Running)
        {
            currentLatitude = Input.location.lastData.latitude;
            currentLongitude = Input.location.lastData.longitude;
        }
    }

    public void OpenSystemGallery()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            ShowPopup("📁 Opening system gallery...");

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

    private void RequestPermissions()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera))
            UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Camera);

        if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.FineLocation))
            UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.FineLocation);
#endif
    }
}
