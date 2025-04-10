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

        // Create a canvas with room for transformations
        float margin = 0.2f; // 20% reserve for transformations
        int canvasWidth = Mathf.RoundToInt(singleWidth * 4 * (1 + margin));
        int canvasHeight = Mathf.RoundToInt(singleHeight * 2 * (1 + margin));
        
        Texture2D canvas = new Texture2D(canvasWidth, canvasHeight);
        // Fill with transparent color
        Color[] clearColors = new Color[canvasWidth * canvasHeight];
        for (int i = 0; i < clearColors.Length; i++)
            clearColors[i] = new Color(0, 0, 0, 0);
        canvas.SetPixels(clearColors);

        // We place photos taking into account the angles of rotation
        for (int row = 0; row < 2; row++)
        {
            for (int col = 0; col < 4; col++)
            {
                int photoIndex = row * 4 + col;
                if (photoIndex >= count) continue;

                // Basic position of the photo without transformation
                int baseX = col * singleWidth;
                int baseY = (1 - row) * singleHeight; // Top row with row=0

                // Getting rotation angles from JSON
                Vector3 angles = photoDataList[photoIndex].relativeEulerAngles;
                
                // Applying Angle Based Transformation
                Matrix4x4 rotationMatrix = Matrix4x4.TRS(
                    Vector3.zero,
                    Quaternion.Euler(angles),
                    Vector3.one
                );

                // Copy and transform photos
                CopyTransformedPhotoToCanvas(
                    photos[photoIndex],
                    canvas,
                    baseX,
                    baseY,
                    singleWidth,
                    singleHeight,
                    rotationMatrix
                );
            }
        }

        canvas.Apply();

        byte[] jpgData = ImageConversion.EncodeToJPG(canvas);
        string filePath = Application.dataPath + "/StitchedGrid.jpg";
        System.IO.File.WriteAllBytes(filePath, jpgData);
        Debug.Log("Сохранено JPG полотно: " + filePath);
    }

    void CopyTransformedPhotoToCanvas(
        Texture2D photo,
        Texture2D canvas,
        int baseX,
        int baseY,
        int width,
        int height,
        Matrix4x4 transform
    )
    {
        // Finding the corners of a photograph
        Vector3[] corners = new Vector3[]
        {
            new Vector3(0, 0, 0),            // Bottom left
            new Vector3(width, 0, 0),        // Bottom right
            new Vector3(width, height, 0),   // Top right
            new Vector3(0, height, 0)        // Top left
        };

        // Transforming corners
        for (int i = 0; i < corners.Length; i++)
        {
            corners[i] = transform.MultiplyPoint(corners[i]);
        }

        // Finding the boundaries of the transformed image
        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;
        
        foreach (Vector3 corner in corners)
        {
            minX = Mathf.Min(minX, corner.x);
            maxX = Mathf.Max(maxX, corner.x);
            minY = Mathf.Min(minY, corner.y);
            maxY = Mathf.Max(maxY, corner.y);
        }

        // Copy pixels with transformation
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                // Apply transformation to the current point
                Vector3 sourcePoint = new Vector3(x, y, 0);
                Vector3 transformedPoint = transform.MultiplyPoint(sourcePoint);

                // Calculate the position on the canvas
                int targetX = baseX + Mathf.RoundToInt(transformedPoint.x);
                int targetY = baseY + Mathf.RoundToInt(transformedPoint.y);

                // Checking the canvas boundaries
                if (targetX >= 0 && targetX < canvas.width && 
                    targetY >= 0 && targetY < canvas.height)
                {
                    Color pixel = photo.GetPixel(x, y);
                    if (pixel.a > 0.01f)
                    {
                        canvas.SetPixel(targetX, targetY, pixel);
                    }
                }
            }
        }
    }
}
