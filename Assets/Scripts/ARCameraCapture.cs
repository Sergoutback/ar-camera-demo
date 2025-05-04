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
    [SerializeField] private Button stitchButton;
    [SerializeField] private Button stitchFromJsonButton;
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private CameraStatusUI cameraStatusUI;
    [SerializeField] private float autoStitchDelay = 0.5f;
    public EdgeOverlayUI edgeOverlayUI;

    private List<Texture2D> capturedPhotos = new List<Texture2D>();
    private List<GameObject> previewImages = new List<GameObject>();
    private string lastSavedImagePath;
    private string currentSessionId;

    private Texture2D delayedCombined;
    private string delayedCombinedPath;

    private Quaternion baseGyroRotation;
    private Vector3 basePosition;
    private bool baseRotationSet = false;

    private bool sessionStarted = false;
    private bool readyToFinalize = false;

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
        //exportZipButton.onClick.AddListener(OnExportSessionZipButton);
        fotoButton.onClick.AddListener(CapturePhoto);
        stitchButton.onClick.AddListener(OnStitchPhotosManually);
        stitchFromJsonButton.onClick.AddListener(OnStitchFromLastJson);


        Input.gyro.enabled = true;
        StartCoroutine(UpdateLocation());
    }

    public void OnStitchPhotosManually()
    {
        if (capturedPhotos.Count == 0 || sessionPhotos.Count == 0)
        {
            PopupLogger.Log("No photos to stitch.");
            return;
        }

        PhotoStitcher stitcher = FindObjectOfType<PhotoStitcher>();
        if (stitcher != null)
        {
            PopupLogger.Log("Stitching photos...");
            stitcher.RunStitchExternally(capturedPhotos.ToArray(), sessionPhotos.ToArray());
        }
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
            PopupLogger.Log("[ARCamera] ARCameraManager not assigned.");
            yield break;
        }

        if (!arCameraManager.TryAcquireLatestCpuImage(out XRCpuImage image))
        {
            PopupLogger.Log("[ARCamera] Could not acquire AR camera image.");
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

            photo = new Texture2D(conversionParams.outputDimensions.x, conversionParams.outputDimensions.y,
                conversionParams.outputFormat, false);
            photo.LoadRawTextureData(rawData);
            photo.Apply();

            rawData.Dispose();
        }

        photo = RotateTexture90CW(photo);

        string filename = $"Photo_{DateTime.Now:yyyyMMdd_HHmmss}.png";
        string path = Path.Combine(Application.persistentDataPath, filename);
        File.WriteAllBytes(path, photo.EncodeToPNG());

        AddPhotoToPreview(photo, path);
        PopupLogger.Log($"Saved photo #{capturedPhotos.Count}");

        int n = capturedPhotos.Count;
        float edgePercent = 0.1f;

        Texture2D left = null;
        Texture2D up = null;

        if (n >= 1 && n <= 3)
        {
            left = capturedPhotos[n - 1];
        }
        else if (n == 4)
        {
            left = capturedPhotos[3];
            up = capturedPhotos[0];
        }
        else if (n == 5)
        {
            left = capturedPhotos[4];
            up = capturedPhotos[1];
        }
        else if (n == 6)
        {
            left = capturedPhotos[5];
            up = capturedPhotos[2];
        }
        else if (n == 7)
        {
            left = capturedPhotos[6];
            up = capturedPhotos[3];
        }
        else if (n == 8)
        {
            left = capturedPhotos[7];
            up = null;
        }

        if (left != null || up != null)
            edgeOverlayUI.ShowEdges(leftEdge: left, upEdge: up, edgePercent: edgePercent);
        else
            edgeOverlayUI.HideAll();

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
            path = path,
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

        if (capturedPhotos.Count == 8 && sessionPhotos.Count == 8 && !readyToFinalize)
        {
            readyToFinalize = true;

            Texture2D combined = new Texture2D(photo.width * 4, photo.height * 2);
            for (int i = 0; i < 8; i++)
            {
                int col = i % 4;
                int row = 1 - (i / 4);
                combined.SetPixels(col * photo.width, row * photo.height, photo.width, photo.height,
                    capturedPhotos[i].GetPixels());
            }

            combined.Apply();

            galleryButton.image.sprite = Sprite.Create(combined, new Rect(0, 0, combined.width, combined.height),
                new Vector2(0.5f, 0.5f));
            string combinedName = $"Combined_{DateTime.Now:yyyyMMdd_HHmmss}.png";
            string combinedPath = Path.Combine(Application.persistentDataPath, combinedName);
            File.WriteAllBytes(combinedPath, combined.EncodeToPNG());
            NativeGallery.SaveImageToGallery(combinedPath, "ARCameraDemo", combinedName);

            PopupLogger.Log("Collage preview saved to Gallery");

            delayedCombined = combined;
            delayedCombinedPath = combinedPath;
            Invoke(nameof(DelayedFinalize), 0.3f);
        }
    }


    private void DelayedFinalize()
    {
        StartCoroutine(FinalizeMiniSessionWithDelay(delayedCombined, delayedCombinedPath));
    }

    private IEnumerator FinalizeMiniSessionWithDelay(Texture2D combined, string combinedPath)
    {
        yield return null;
        yield return null;

        if (capturedPhotos.Count < 8 || sessionPhotos.Count < 8)
        {
            PopupLogger.Log(
                $"FinalizeMiniSession called too early. captured: {capturedPhotos.Count}, session: {sessionPhotos.Count}");
            yield break;
        }

        if (!readyToFinalize)
        {
            PopupLogger.Log("FinalizeMiniSessionWithDelay called, but not ready.");
            yield break;
        }

        FinalizeMiniSession(combined, combinedPath);
    }

    private void FinalizeMiniSession(Texture2D combined, string combinedPath)
    {
        if (!readyToFinalize || capturedPhotos.Count < 8 || sessionPhotos.Count < 8)
        {
            PopupLogger.Log(
                $"FinalizeMiniSession aborted. captured: {capturedPhotos.Count}, session: {sessionPhotos.Count}");
            return;
        }

        string sessionJson = JsonHelper.ToJson(sessionPhotos.ToArray(), true);
        string sessionFile = Path.Combine(Application.persistentDataPath, $"Session_{currentSessionId}.json");
        File.WriteAllText(sessionFile, sessionJson);

        string exportDir = Path.Combine(Application.persistentDataPath, $"Session_{currentSessionId}_Export");
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
        PopupLogger.Log("Mini-session exported to ZIP: " + zipPath);

        foreach (var obj in previewImages)
            Destroy(obj);
        previewImages.Clear();

        foreach (Transform child in previewContainer)
            DestroyImmediate(child.gameObject);

        PhotoStitcher stitcher = FindObjectOfType<PhotoStitcher>();
        if (stitcher != null)
        {
            stitcher.SetLogger(this);

            if (capturedPhotos.Count == 8 && sessionPhotos.Count == 8)
            {
                var photosCopy = capturedPhotos.ToArray();
                var metaCopy = sessionPhotos.ToArray();

                PopupLogger.Log("Stitching photos...");
                stitcher.RunStitchExternally(photosCopy, metaCopy);
            }
            else
            {
                PopupLogger.Log("Skipping stitch: photos or metadata not ready");
            }
        }

        capturedPhotos.Clear();
        sessionPhotos.Clear();
        readyToFinalize = false;
        currentSessionId = Guid.NewGuid().ToString();
    }


    public void OnStitchFromLastJson()
    {
        capturedPhotos.Clear();
        sessionPhotos.Clear();

        string[] jsonFiles = Directory.GetFiles(Application.persistentDataPath, "Session_*.json");
        if (jsonFiles.Length == 0)
        {
            PopupLogger.Log("No previous session JSON found.");
            return;
        }

        string latestJson = jsonFiles[jsonFiles.Length - 1];
        string jsonText = File.ReadAllText(latestJson);
        PhotoStitcher.PhotoMetadataWrapper wrapper = JsonUtility.FromJson<PhotoStitcher.PhotoMetadataWrapper>(jsonText);

        if (wrapper.Items == null || wrapper.Items.Length == 0)
        {
            PopupLogger.Log("Failed to parse JSON.");
            return;
        }

        int loaded = 0;

        foreach (var meta in wrapper.Items)
        {
            if (!File.Exists(meta.path)) continue;

            byte[] data = File.ReadAllBytes(meta.path);
            Texture2D tex = new Texture2D(2, 2);
            if (!tex.LoadImage(data)) continue;

            capturedPhotos.Add(tex);
            sessionPhotos.Add(meta);
            loaded++;
        }

        if (loaded == 0)
        {
            PopupLogger.Log("No valid photos found in session.");
        }
        else
        {
            PopupLogger.Log($"Loaded {loaded} photo(s) from session. Ready to stitch.");
        }
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


    public void OnExportSessionZipButton()
    {
        string exportDir = Path.Combine(Application.persistentDataPath, $"Session_{currentSessionId}_Export");
        Directory.CreateDirectory(exportDir);

        int copiedCount = 0;

        foreach (var meta in sessionPhotos)
        {
            if (File.Exists(meta.path))
            {
                string fileName = Path.GetFileName(meta.path);
                File.Copy(meta.path, Path.Combine(exportDir, fileName), true);
                copiedCount++;
            }
            else
            {
                PopupLogger.Log($"Skipped missing photo: {meta.path}");
            }
        }

        string sessionJson = JsonHelper.ToJson(sessionPhotos.ToArray(), true);
        string jsonFile = Path.Combine(exportDir, $"Session_{currentSessionId}.json");
        File.WriteAllText(jsonFile, sessionJson);

        string zipPath = Path.Combine(Application.persistentDataPath, $"Session_{currentSessionId}.zip");
        if (File.Exists(zipPath)) File.Delete(zipPath);
        ZipFile.CreateFromDirectory(exportDir, zipPath);
        Directory.Delete(exportDir, true);

#if UNITY_ANDROID && !UNITY_EDITOR
    AndroidMediaScanner.ScanFile(zipPath);
#endif

        PopupLogger.Log($"Exported ZIP with {copiedCount} photos to: {zipPath}");
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
            PopupLogger.Log("Opening system gallery...");

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
            PopupLogger.Log("[ARCamera] Failed to open gallery: " + e.Message);
        }
#else
        PopupLogger.Log("[ARCamera] Gallery open only works on Android device.");
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

    private void AddPhotoToPreview(Texture2D photo, string imagePath)
    {
        GameObject newPreview = Instantiate(previewPrefab, previewContainer);
        newPreview.GetComponent<RawImage>().texture = photo;
        previewImages.Add(newPreview);
        capturedPhotos.Add(photo);

        // Save each photo to gallery with a unique filename
        string debugGalleryName = $"Single_{DateTime.Now:yyyyMMdd_HHmmss_fff}.png";
        NativeGallery.SaveImageToGallery(imagePath, "ARCameraDemo", debugGalleryName);

        // --- Overlay update logic ---
        int n = capturedPhotos.Count;
        float edgePercent = 0.1f;

        // Always show the full photo for debugging
        if (edgeOverlayUI.previewLeft != null)
            edgeOverlayUI.previewLeft.uvRect = new Rect(0, 0, 1, 1);
        if (edgeOverlayUI.previewUp != null)
            edgeOverlayUI.previewUp.uvRect = new Rect(0, 0, 1, 1);

        Texture2D left = null;
        Texture2D up = null;

        if (n >= 1 && n <= 3)
        {
            left = capturedPhotos[n - 1];
        }
        else if (n == 4)
        {
            left = capturedPhotos[3];
            up = capturedPhotos[0];
        }
        else if (n == 5)
        {
            left = capturedPhotos[4];
            up = capturedPhotos[1];
        }
        else if (n == 6)
        {
            left = capturedPhotos[5];
            up = capturedPhotos[2];
        }
        else if (n == 7)
        {
            left = capturedPhotos[6];
            up = capturedPhotos[3];
        }
        else if (n == 8)
        {
            left = capturedPhotos[7];
            up = null;
        }

        if (left != null || up != null)
            edgeOverlayUI.ShowEdges(leftEdge: left, upEdge: up, edgePercent: edgePercent);
        else
            edgeOverlayUI.HideAll();
        // --- End overlay logic ---

        Debug.Log($"ShowEdges: leftEdge={(left != null ? left.width + "x" + left.height : "null")}, upEdge={(up != null ? up.width + "x" + up.height : "null")}");

        Debug.Log("PreviewUp set active");
    }
}