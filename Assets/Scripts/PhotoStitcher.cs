using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using System.IO.Compression;

[Serializable]
public class PhotoMetadataWrapper
{
    public PhotoMetadata[] Items;
}

public class PhotoStitcher : MonoBehaviour
{
    public float positionScale = 2000f;
    public float directionBias = 10f;

    public TextAsset jsonFile;
    public Texture2D[] photoTextures;

    private Texture2D[] photos;
    private List<PhotoMetadata> photoDataList = new List<PhotoMetadata>();

    public void RunStitchExternally(Texture2D[] inputPhotos, PhotoMetadata[] inputMeta)
    {
        photos = inputPhotos;
        photoDataList = new List<PhotoMetadata>(inputMeta);
        StitchPhotos();
    }

#if UNITY_EDITOR
    [ContextMenu("🔧 Test Stitch From Inspector")]
    public void TestStitchFromInspector()
    {
        if (jsonFile == null || photoTextures == null || photoTextures.Length == 0)
        {
            Debug.LogError("[PhotoStitcher] Assign both jsonFile and photoTextures[] in inspector.");
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
        if (photos == null || photoDataList == null || photos.Length == 0 || photoDataList.Count == 0)
        {
            Debug.LogWarning("[PhotoStitcher] Nothing to stitch");
            return;
        }

        int singleWidth = photos[0].width;
        int singleHeight = photos[0].height;

        int cols = 4;
        int rows = 2;
        int canvasWidth = singleWidth * cols;
        int canvasHeight = singleHeight * rows;

        Texture2D canvas = new Texture2D(canvasWidth, canvasHeight, TextureFormat.RGBA32, false);
        Color[] canvasPixels = new Color[canvasWidth * canvasHeight];
        for (int i = 0; i < canvasPixels.Length; i++) canvasPixels[i] = Color.white;

        for (int i = 0; i < photos.Length; i++)
        {
            if (i >= photoDataList.Count) continue;

            Texture2D photo = photos[i];
            float angle = photoDataList[i].relativeEulerAngles.z + photoDataList[i].relativeEulerAngles.y;
            Texture2D aligned = AlignPhoto(photo, angle);

            int col = i % cols;
            int row = i / cols;

            int baseX = col * singleWidth;
            int baseY = (rows - 1 - row) * singleHeight;

            CopyPhotoToGrid(
                aligned,
                canvas,
                canvasPixels,
                baseX,
                baseY
            );
        }

        canvas.SetPixels(canvasPixels);
        canvas.Apply();

        string filePath = Path.Combine(Application.persistentDataPath, "StitchedGrid_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png");
        byte[] pngData = canvas.EncodeToPNG();
        File.WriteAllBytes(filePath, pngData);
        Debug.Log("✅ Auto-stitched image saved: " + filePath);

        string exportDir = Path.Combine(Application.temporaryCachePath, "AutoStitchExport");
        if (Directory.Exists(exportDir)) Directory.Delete(exportDir, true);
        Directory.CreateDirectory(exportDir);

        string exportedPngPath = Path.Combine(exportDir, Path.GetFileName(filePath));
        File.Copy(filePath, exportedPngPath, true);

        for (int i = 0; i < photoDataList.Count; i++)
        {
            string jsonOut = JsonConvert.SerializeObject(
                photoDataList[i],
                Formatting.Indented,
                new JsonSerializerSettings
                {
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                });

            string jsonPath = Path.Combine(exportDir, $"meta_{i}.json");
            File.WriteAllText(jsonPath, jsonOut);
        }

        string zipName = "Stitched_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".zip";
        string zipPath = Path.Combine(Application.persistentDataPath, zipName);
        if (File.Exists(zipPath)) File.Delete(zipPath);
        ZipFile.CreateFromDirectory(exportDir, zipPath);

        Debug.Log("📦 Auto ZIP created: " + zipPath);
    }

    Texture2D AlignPhoto(Texture2D photo, float angle)
    {
        Texture2D aligned = new Texture2D(photo.width, photo.height, TextureFormat.RGBA32, false);
        Color[] alignedPixels = new Color[photo.width * photo.height];
        Color[] photoPixels = photo.GetPixels();

        float angleRad = -angle * Mathf.Deg2Rad;
        float cos = Mathf.Cos(angleRad);
        float sin = Mathf.Sin(angleRad);
        Vector2 center = new Vector2(photo.width / 2, photo.height / 2);

        for (int y = 0; y < photo.height; y++)
        {
            for (int x = 0; x < photo.width; x++)
            {
                Vector2 pos = new Vector2(x, y);
                Vector2 centered = pos - center;
                Vector2 rotated = new Vector2(
                    centered.x * cos - centered.y * sin,
                    centered.x * sin + centered.y * cos
                ) + center;

                int srcX = Mathf.RoundToInt(rotated.x);
                int srcY = Mathf.RoundToInt(rotated.y);

                if (srcX >= 0 && srcX < photo.width && srcY >= 0 && srcY < photo.height)
                {
                    alignedPixels[y * photo.width + x] = photoPixels[srcY * photo.width + srcX];
                }
                else
                {
                    alignedPixels[y * photo.width + x] = Color.white;
                }
            }
        }

        aligned.SetPixels(alignedPixels);
        aligned.Apply();
        return aligned;
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