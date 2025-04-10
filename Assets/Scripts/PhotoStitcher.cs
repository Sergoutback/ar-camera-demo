using UnityEngine;
using System;
using System.Collections.Generic;
using Newtonsoft.Json;

[Serializable]
public class PhotoData
{
    public Vector3 relativeEulerAngles;
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
        int count = Mathf.Min(8, photos.Length);
        int singleWidth = photos[0].width;
        int singleHeight = photos[0].height;

        Texture2D canvas = new Texture2D(singleWidth * 4, singleHeight * 2);

        int[] previousOffset = new int[2];

        for (int i = 0; i < count; i++)
        {
            int row = i < 4 ? 0 : 1;
            int rowStart = row * 4;
            int col = i % 4;

            int overlapArea = singleWidth / 3;
            int bestMatchOffsetX = 0;

            if (col > 0)
            {
                bestMatchOffsetX = FindBestOverlapOffset(photos[i - 1], photos[i], overlapArea);
            }

            int startX = col == 0 ? 0 : previousOffset[row] + singleWidth - bestMatchOffsetX;
            int flippedRow = 1 - row;
            int startY = flippedRow * singleHeight;

            for (int y = 0; y < singleHeight; y++)
            {
                for (int x = 0; x < singleWidth; x++)
                {
                    Color pixel = photos[i].GetPixel(x, y);
                    if (pixel.a > 0.01f)
                        canvas.SetPixel(startX + x, startY + y, pixel);
                }
            }

            previousOffset[row] = startX;
        }

        canvas.Apply();

        // Сохраняем результат в JPG
        byte[] jpgData = ImageConversion.EncodeToJPG(canvas);
        string filePath = Application.dataPath + "/StitchedGrid.jpg";
        System.IO.File.WriteAllBytes(filePath, jpgData);
        Debug.Log("Сохранено JPG полотно: " + filePath);
        Debug.Log("Сохранено полотно: " + filePath);

        
    }

    int FindBestOverlapOffset(Texture2D leftPhoto, Texture2D rightPhoto, int overlapArea)
    {
        int width = leftPhoto.width;
        int height = leftPhoto.height;

        Color[] leftPixels = leftPhoto.GetPixels();
        Color[] rightPixels = rightPhoto.GetPixels();

        int bestOffset = 0;
        float bestMatch = float.MaxValue;

        for (int offset = 0; offset < overlapArea; offset++)
        {
            float difference = 0f;
            int samples = 0;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < overlapArea; x++)
                {
                    int lx = width - overlapArea + x;
                    int rx = x + offset;

                    if (lx >= width || rx >= width) continue;

                    Color lp = leftPixels[y * width + lx];
                    Color rp = rightPixels[y * width + rx];

                    difference += (lp.r - rp.r) * (lp.r - rp.r) +
                                  (lp.g - rp.g) * (lp.g - rp.g) +
                                  (lp.b - rp.b) * (lp.b - rp.b);
                    samples++;
                }
            }

            if (samples > 0)
                difference /= samples;

            if (difference < bestMatch)
            {
                bestMatch = difference;
                bestOffset = offset;
            }
        }

        return bestOffset;
    }
}
