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

    private const int GRID_ROWS = 2;
    private const int GRID_COLS = 4;
    private const float BLUE_THRESHOLD = 0.6f;

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

    bool IsBlue(Color c)
    {
        return c.b > BLUE_THRESHOLD && c.b > c.r * 2 && c.b > c.g * 2;
    }

    void StitchPhotos()
    {
        int singleWidth = photos[0].width;
        int singleHeight = photos[0].height;
        
        // Create canvas with exact grid size
        int canvasWidth = singleWidth * GRID_COLS;
        int canvasHeight = singleHeight * GRID_ROWS;

        Texture2D canvas = new Texture2D(canvasWidth, canvasHeight, TextureFormat.RGBA32, false);
        Color[] canvasPixels = new Color[canvasWidth * canvasHeight];

        // Fill with white
        for (int i = 0; i < canvasPixels.Length; i++)
        {
            canvasPixels[i] = Color.white;
        }

        // First align and copy photos
        for (int row = 0; row < GRID_ROWS; row++)
        {
            for (int col = 0; col < GRID_COLS; col++)
            {
                int index = row * GRID_COLS + col;
                if (index >= photos.Length) continue;

                Texture2D aligned = AlignPhoto(photos[index], photoDataList[index].relativeEulerAngles.z);
                
                CopyPhotoToGrid(
                    aligned,
                    canvas,
                    canvasPixels,
                    col * singleWidth,
                    (GRID_ROWS - 1 - row) * singleHeight
                );
            }
        }

        // Draw perfect grid lines
        DrawGridLines(canvasPixels, canvasWidth, canvasHeight, singleWidth, singleHeight);

        canvas.SetPixels(canvasPixels);
        canvas.Apply();

        byte[] jpgData = ImageConversion.EncodeToJPG(canvas, 95);
        string filePath = Application.dataPath + "/StitchedGrid.jpg";
        System.IO.File.WriteAllBytes(filePath, jpgData);
        Debug.Log("Saved stitched image: " + filePath);
    }

    Texture2D AlignPhoto(Texture2D photo, float angle)
    {
        // Create new texture for aligned photo
        Texture2D aligned = new Texture2D(photo.width, photo.height, TextureFormat.RGBA32, false);
        Color[] alignedPixels = new Color[photo.width * photo.height];
        Color[] photoPixels = photo.GetPixels();

        // Calculate rotation
        float angleRad = -angle * Mathf.Deg2Rad; // Negative because we're correcting the rotation
        float cos = Mathf.Cos(angleRad);
        float sin = Mathf.Sin(angleRad);
        Vector2 center = new Vector2(photo.width/2, photo.height/2);

        // Rotate each pixel
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

    void DrawGridLines(Color[] pixels, int width, int height, int cellWidth, int cellHeight)
    {
        Color blue = new Color(0, 0, 1, 1);
        int lineWidth = 2;

        // Draw vertical lines
        for (int col = 0; col <= GRID_COLS; col++)
        {
            int x = col * cellWidth;
            if (col > 0) x -= 1;

            for (int y = 0; y < height; y++)
            {
                for (int i = 0; i < lineWidth; i++)
                {
                    if (x + i >= 0 && x + i < width)
                    {
                        pixels[y * width + (x + i)] = blue;
                    }
                }
            }
        }

        // Draw horizontal lines
        for (int row = 0; row <= GRID_ROWS; row++)
        {
            int y = row * cellHeight;
            if (row > 0) y -= 1;

            for (int x = 0; x < width; x++)
            {
                for (int i = 0; i < lineWidth; i++)
                {
                    if (y + i >= 0 && y + i < height)
                    {
                        pixels[(y + i) * width + x] = blue;
                    }
                }
            }
        }
    }
}
