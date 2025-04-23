using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using System.IO.Compression;

public class PhotoStitcher : MonoBehaviour
{
    public float positionScale = 2000f;
    public float directionBias = 10f;

    public TextAsset jsonFile;
    public Texture2D[] photoTextures;

    private Texture2D[] photos;
    private List<PhotoMetadata> photoDataList = new List<PhotoMetadata>();
    private ARCameraCapture aRCameraCapture;

    public void RunStitchExternally(Texture2D[] inputPhotos, PhotoMetadata[] inputMeta)
    {
        photos = inputPhotos;
        photoDataList = new List<PhotoMetadata>(inputMeta);
        StitchPhotos();
    }

    public void SetLogger(ARCameraCapture ref_)
    {
        aRCameraCapture = ref_;
    }

#if UNITY_EDITOR
    [ContextMenu("Test Stitch From Inspector")]
    public void TestStitchFromInspector()
    {
        if (jsonFile == null || photoTextures == null || photoTextures.Length == 0)
        {
            PopupLogger.Log("[PhotoStitcher] Assign both jsonFile and photoTextures[] in inspector.");
            return;
        }

        var wrapper = JsonConvert.DeserializeObject<PhotoMetadataWrapper>(jsonFile.text);
        photoDataList = new List<PhotoMetadata>(wrapper.Items);
        photos = photoTextures;
        StitchPhotos();
    }
#endif

void StitchPhotos()
{
    PopupLogger.Log($"[PhotoStitcher] Starting stitch, photos: {photos?.Length}, meta: {photoDataList?.Count}");

    if (photos == null || photoDataList == null || photos.Length == 0 || photoDataList.Count == 0)
    {
        PopupLogger.Log("[PhotoStitcher] Nothing to stitch");
        return;
    }

    int photoWidth = photos[0].width;
    int photoHeight = photos[0].height;

    List<Vector2> projectedPositions = new List<Vector2>();
    float minX = float.MaxValue, maxX = float.MinValue;
    float minY = float.MaxValue, maxY = float.MinValue;

    foreach (var meta in photoDataList)
    {
        Vector2 pos = new Vector2(
            meta.relativePosition.x * positionScale,
            meta.relativePosition.z * positionScale
        );
        projectedPositions.Add(pos);
        minX = Mathf.Min(minX, pos.x);
        maxX = Mathf.Max(maxX, pos.x);
        minY = Mathf.Min(minY, pos.y);
        maxY = Mathf.Max(maxY, pos.y);
    }

    int canvasWidth = Mathf.CeilToInt(maxX - minX) + photoWidth;
    int canvasHeight = Mathf.CeilToInt(maxY - minY) + photoHeight;

    Texture2D canvas = new Texture2D(canvasWidth, canvasHeight, TextureFormat.RGBA32, false);
    Color[] canvasPixels = new Color[canvasWidth * canvasHeight];
    for (int i = 0; i < canvasPixels.Length; i++) canvasPixels[i] = Color.black;

    for (int i = 0; i < photos.Length; i++)
    {
        Texture2D photo = photos[i];
        Quaternion rot = Quaternion.Euler(photoDataList[i].relativeEulerAngles);
        Texture2D rotated = Apply3DRotation(photo, rot);

        Vector2 projected = projectedPositions[i];
        int baseX = Mathf.RoundToInt(projected.x - minX);
        int baseY = Mathf.RoundToInt(projected.y - minY);

        CopyPhotoToGrid(rotated, canvas, canvasPixels, baseX, baseY);
    }

    canvas.SetPixels(canvasPixels);
    canvas.Apply();

    string filePath = Path.Combine(Application.persistentDataPath, "StitchedByPosition_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png");
    byte[] pngData = canvas.EncodeToPNG();
    File.WriteAllBytes(filePath, pngData);
    PopupLogger.Log($"File written: {filePath}");
#if UNITY_ANDROID && !UNITY_EDITOR
    string galleryDir = Path.Combine("/storage/emulated/0/Pictures/ARCameraDemo/");
    if (!Directory.Exists(galleryDir)) Directory.CreateDirectory(galleryDir);

    string finalName = Path.GetFileName(filePath);
    string finalPath = Path.Combine(galleryDir, finalName);

    File.Copy(filePath, finalPath, true);
    NativeGallery.SaveImageToGallery(finalPath, "ARCameraDemo", Path.GetFileName(finalPath));
    PopupLogger.Log("📷 Stitched image saved to gallery: " + finalPath);
#endif
}


    Texture2D RotateTexture(Texture2D original, float angleDegrees)
    {
        int width = original.width;
        int height = original.height;
        Texture2D rotated = new Texture2D(width, height);

        float angleRad = angleDegrees * Mathf.Deg2Rad;
        float cos = Mathf.Cos(angleRad);
        float sin = Mathf.Sin(angleRad);

        int x0 = width / 2;
        int y0 = height / 2;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                int dx = x - x0;
                int dy = y - y0;

                int srcX = Mathf.RoundToInt(cos * dx + sin * dy) + x0;
                int srcY = Mathf.RoundToInt(-sin * dx + cos * dy) + y0;

                Color color = Color.black;
                if (srcX >= 0 && srcX < width && srcY >= 0 && srcY < height)
                    color = original.GetPixel(srcX, srcY);

                rotated.SetPixel(x, y, color);
            }
        }

        rotated.Apply();
        return rotated;
    }
    
    Texture2D Apply3DRotation(Texture2D source, Quaternion rotation)
    {
        int width = source.width;
        int height = source.height;

        RenderTexture rt = RenderTexture.GetTemporary(width, height, 24);
        RenderTexture prevRT = RenderTexture.active;

        GameObject renderCamObj = new GameObject("RenderCam");
        Camera renderCam = renderCamObj.AddComponent<Camera>();
        renderCam.orthographic = true;
        renderCam.orthographicSize = 1;
        renderCam.clearFlags = CameraClearFlags.SolidColor;
        renderCam.backgroundColor = Color.black;
        renderCam.targetTexture = rt;
        renderCam.aspect = (float)width / height;

        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.transform.localScale = new Vector3(2, 2, 1);
        Material quadMat = new Material(Shader.Find("Unlit/Texture"));
        if (quadMat == null)
            PopupLogger.Log("Shader 'Unlit/Texture' not found!");
        quadMat.mainTexture = source;
        quad.GetComponent<MeshRenderer>().material = quadMat;

        quad.transform.rotation = rotation;
        quad.transform.position = Vector3.zero;
        renderCam.transform.position = new Vector3(0, 0, -3);
        renderCam.transform.LookAt(Vector3.zero);

        renderCam.Render();

        RenderTexture.active = rt;
        Texture2D result = new Texture2D(width, height, TextureFormat.RGBA32, false);
        result.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        result.Apply();

        RenderTexture.active = prevRT;

        UnityEngine.Object.DestroyImmediate(renderCamObj);
        UnityEngine.Object.DestroyImmediate(quad);
        RenderTexture.ReleaseTemporary(rt);

        return result;
    }





    Texture2D AlignPhotoPerspective(Texture2D source, float angle)
    {
        RenderTexture rt = RenderTexture.GetTemporary(source.width, source.height);
        Material rotMat = new Material(Shader.Find("Hidden/Internal-GUITextureClip"));

        Matrix4x4 m = Matrix4x4.identity;
        float rad = angle * Mathf.Deg2Rad;
        m.SetTRS(Vector3.zero, Quaternion.Euler(0, 0, -angle), Vector3.one);
        rotMat.SetMatrix("_GuiMatrix", m);

        Graphics.Blit(source, rt, rotMat);

        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;

        Texture2D result = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
        result.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        result.Apply();

        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);

        return result;
    }

    [Serializable]
    public class PhotoMetadataWrapper
    {
        public PhotoMetadata[] Items;
    }

    Vector2 FindBestOverlap(Texture2D current, Texture2D previous, Vector3 currentOffset, Vector3 prevOffset)
    {
        int searchRange = 10;
        float minDiff = float.MaxValue;
        Vector2 bestOffset = Vector2.zero;

        Vector3 delta = currentOffset - prevOffset;
        bool horizontalPriority = Mathf.Abs(delta.x) > Mathf.Abs(delta.z);

        for (int dx = -searchRange; dx <= searchRange; dx++)
        {
            for (int dy = -searchRange; dy <= searchRange; dy++)
            {
                float totalDiff =
                    CompareRightLeftEdges(current, previous, dx, dy) +
                    CompareLeftRightEdges(current, previous, dx, dy) +
                    CompareBottomTopEdges(current, previous, dx, dy) +
                    CompareTopBottomEdges(current, previous, dx, dy);

                if (totalDiff < minDiff)
                {
                    minDiff = totalDiff;
                    bestOffset = new Vector2(dx, dy);
                }
            }
        }
        
        return bestOffset;
    }

    float CompareRightLeftEdges(Texture2D current, Texture2D previous, int offsetX, int offsetY)
    {
        int height = Mathf.Min(current.height, previous.height);
        int width = 5;

        float totalDiff = 0f;

        for (int y = 0; y < height; y++)
        {
            int y1 = y + offsetY;
            if (y1 < 0 || y1 >= height) continue;

            for (int x = 0; x < width; x++)
            {
                Color c1 = current.GetPixel(current.width - width + x, y1);
                Color c2 = previous.GetPixel(x, y1);
                totalDiff += Mathf.Abs(c1.r - c2.r) + Mathf.Abs(c1.g - c2.g) + Mathf.Abs(c1.b - c2.b);
            }
        }

        return totalDiff;
    }

    float CompareBottomTopEdges(Texture2D current, Texture2D previous, int offsetX, int offsetY)
    {
        int width = Mathf.Min(current.width, previous.width);
        int height = 5;

        float totalDiff = 0f;

        for (int x = 0; x < width; x++)
        {
            int x1 = x + offsetX;
            if (x1 < 0 || x1 >= width) continue;

            for (int y = 0; y < height; y++)
            {
                Color c1 = current.GetPixel(x1, y);
                Color c2 = previous.GetPixel(x1, previous.height - height + y);
                totalDiff += Mathf.Abs(c1.r - c2.r) + Mathf.Abs(c1.g - c2.g) + Mathf.Abs(c1.b - c2.b);
            }
        }

        return totalDiff;
    }
    
    float CompareLeftRightEdges(Texture2D current, Texture2D previous, int offsetX, int offsetY)
    {
        int height = Mathf.Min(current.height, previous.height);
        int width = 5;

        float totalDiff = 0f;

        for (int y = 0; y < height; y++)
        {
            int y1 = y + offsetY;
            if (y1 < 0 || y1 >= height) continue;

            for (int x = 0; x < width; x++)
            {
                Color c1 = current.GetPixel(x, y1);
                Color c2 = previous.GetPixel(previous.width - width + x, y1);
                totalDiff += Mathf.Abs(c1.r - c2.r) + Mathf.Abs(c1.g - c2.g) + Mathf.Abs(c1.b - c2.b);
            }
        }

        return totalDiff;
    }

    float CompareTopBottomEdges(Texture2D current, Texture2D previous, int offsetX, int offsetY)
    {
        int width = Mathf.Min(current.width, previous.width);
        int height = 5;

        float totalDiff = 0f;

        for (int x = 0; x < width; x++)
        {
            int x1 = x + offsetX;
            if (x1 < 0 || x1 >= width) continue;

            for (int y = 0; y < height; y++)
            {
                Color c1 = current.GetPixel(x1, current.height - height + y);
                Color c2 = previous.GetPixel(x1, y);
                totalDiff += Mathf.Abs(c1.r - c2.r) + Mathf.Abs(c1.g - c2.g) + Mathf.Abs(c1.b - c2.b);
            }
        }

        return totalDiff;
    }


    void CopyPhotoToGrid(Texture2D photo, Texture2D canvas, Color[] canvasPixels, int baseX, int baseY)
    {
        Color[] photoPixels = photo.GetPixels();

        for (int y = 0; y < photo.height; y++)
        {
            for (int x = 0; x < photo.width; x++)
            {
                Color color = photoPixels[y * photo.width + x];

                int destX = x + baseX;
                int destY = y + baseY;

                if (destX >= 0 && destX < canvas.width &&
                    destY >= 0 && destY < canvas.height)
                {
                    canvasPixels[destY * canvas.width + destX] = color;
                }
            }
        }
    }
}