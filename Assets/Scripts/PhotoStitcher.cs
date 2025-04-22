using UnityEngine;
using System;
using System.Collections.Generic;
using Newtonsoft.Json;

[Serializable]
public class PhotoData
{
    public Vector3 relativeEulerAngles;
    public Vector3 relativePosition;
    public string path;
}

public class PhotoStitcher : MonoBehaviour
{
    public Texture2D[] photos;
    public TextAsset[] jsonFiles;
    private List<PhotoData> photoDataList = new List<PhotoData>();

    [ContextMenu("Stitch Photos")]
    void RunFromContextMenu()
    {
        LoadJsonData();
        StitchPhotos();
    }

    void LoadJsonData()
    {
        photoDataList.Clear();
        foreach (var jsonFile in jsonFiles)
        {
            PhotoData data = JsonConvert.DeserializeObject<PhotoData>(jsonFile.text);
            photoDataList.Add(data);
        }
    }

    void StitchPhotos()
    {
        int singleWidth = photos[0].width;
        int singleHeight = photos[0].height;

        int canvasWidth = singleWidth * 6;
        int canvasHeight = singleHeight * 4;

        Texture2D canvas = new Texture2D(canvasWidth, canvasHeight, TextureFormat.RGBA32, false);
        Color[] canvasPixels = new Color[canvasWidth * canvasHeight];
        for (int i = 0; i < canvasPixels.Length; i++) canvasPixels[i] = Color.white;

        Vector2 canvasCenter = new Vector2(canvasWidth / 2, canvasHeight / 2);
        float scale = 40f;

        List<Vector2> placedPositions = new List<Vector2>();

        for (int i = 0; i < photos.Length; i++)
        {
            if (i >= photoDataList.Count) continue;

            Texture2D photo = photos[i];
            float angle = photoDataList[i].relativeEulerAngles.z;
            Vector3 offset = photoDataList[i].relativePosition;

            Texture2D aligned = AlignPhoto(photo, angle);

            Vector2 approxPos = canvasCenter + new Vector2(offset.x, -offset.z) * scale;

            if (i > 0)
            {
                Vector2 previousPos = placedPositions[i - 1];
                Texture2D previous = photos[i - 1];
                Vector3 prevOffset = photoDataList[i - 1].relativePosition;

                Vector2 bestOffset = FindBestOverlap(aligned, previous, offset, prevOffset);
                approxPos += bestOffset;
            }

            placedPositions.Add(approxPos);

            CopyPhotoToGrid(
                aligned,
                canvas,
                canvasPixels,
                Mathf.RoundToInt(approxPos.x - singleWidth / 2),
                Mathf.RoundToInt(approxPos.y - singleHeight / 2)
            );
        }

        canvas.SetPixels(canvasPixels);
        canvas.Apply();

        byte[] jpgData = ImageConversion.EncodeToJPG(canvas, 95);
        string filePath = Application.dataPath + "/StitchedGrid.jpg";
        System.IO.File.WriteAllBytes(filePath, jpgData);
        Debug.Log("Saved stitched image: " + filePath);
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
                float totalDiff = 0f;

                if (horizontalPriority)
                {
                    totalDiff = CompareRightLeftEdges(current, previous, dx, dy);
                }
                else
                {
                    totalDiff = CompareBottomTopEdges(current, previous, dx, dy);
                }

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
