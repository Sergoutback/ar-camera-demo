using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using System.IO.Compression;
using System.Collections;

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
        PopupLogger.Log($"RunStitchExternally → photos: {inputPhotos.Length}, meta: {inputMeta.Length}");
        photos = inputPhotos;
        photoDataList = new List<PhotoMetadata>(inputMeta);
        StartCoroutine(StitchPhotosCoroutine());
    }

    public void SetLogger(ARCameraCapture refARCameraCapture)
    {
        aRCameraCapture = refARCameraCapture;
    }

    private IEnumerator StitchPhotosCoroutine()
    {
        if (!ValidateInput()) yield break;

        int photoWidth = photos[0].width;
        int photoHeight = photos[0].height;
        int cols = 4;
        int rows = 2;
        int canvasWidth = photoWidth * cols;
        int canvasHeight = photoHeight * rows;

        Texture2D canvas = CreateCanvas(canvasWidth, canvasHeight);
        Color[] canvasPixels = new Color[canvasWidth * canvasHeight];
        for (int i = 0; i < canvasPixels.Length; i++) canvasPixels[i] = Color.black;

        yield return StartCoroutine(PlacePhotosOnCanvasCoroutine(photos, photoDataList, canvas, canvasPixels));

        canvas.SetPixels(canvasPixels);
        canvas.Apply();

        string filePath = SaveCanvasToFile(canvas, "StitchedGrid_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png");
        PopupLogger.Log("Stitched grid saved to gallery.");

        Texture2D yCanvas = null;
        string yFilePath = null;
        yield return StartCoroutine(CreateYRelativeStitchCoroutine(photos, photoDataList, canvasWidth, photoHeight, (result, path) => {
            yCanvas = result;
            yFilePath = path;
        }));
        PopupLogger.Log("Y_Relative_Stitch saved to gallery.");

#if UNITY_ANDROID && !UNITY_EDITOR
        SaveToGalleryAndroid(filePath, yFilePath);
#endif
    }

    private IEnumerator PlacePhotosOnCanvasCoroutine(Texture2D[] photos, List<PhotoMetadata> metaList, Texture2D canvas, Color[] canvasPixels)
    {
        int photoWidth = photos[0].width;
        int photoHeight = photos[0].height;
        int cols = 4;
        int rows = 2;
        for (int i = 0; i < photos.Length; i++)
        {
            if (i >= metaList.Count) continue;
            int baseX = (i % cols) * photoWidth;
            int baseY = (rows - 1 - i/cols) * photoHeight;
            Texture2D photo = photos[i];
            var meta = metaList[i];
            Quaternion rot = Quaternion.Euler(meta.relativeEulerAngles);
            Texture2D rotated = Apply3DRotation(photo, rot);
            CopyPhotoToGrid(rotated, canvas, canvasPixels, baseX, baseY);
            if (i % 2 == 0) yield return null;
        }
    }

    private IEnumerator CreateYRelativeStitchCoroutine(Texture2D[] photos, List<PhotoMetadata> metaList, int canvasWidth, int photoHeight, Action<Texture2D, string> onComplete)
    {
        float worldMinY = float.MaxValue, worldMaxY = float.MinValue;
        for (int j = 0; j < metaList.Count; j++)
        {
            float yVal = metaList[j].relativePosition.y;
            worldMinY = Mathf.Min(worldMinY, yVal);
            worldMaxY = Mathf.Max(worldMaxY, yVal);
        }
        float positionScale = this.positionScale;
        float canvasHeightF = (worldMaxY - worldMinY) * positionScale + photoHeight;
        int yCanvasWidth = canvasWidth;
        int yCanvasHeight = Mathf.CeilToInt(canvasHeightF);
        Texture2D yCanvas = new Texture2D(yCanvasWidth, yCanvasHeight, TextureFormat.RGBA32, false);
        Color[] yCanvasPixels = new Color[yCanvasWidth * yCanvasHeight];
        for (int k = 0; k < yCanvasPixels.Length; k++) yCanvasPixels[k] = Color.black;

        float zScene = 0f;
        for (int j = 0; j < metaList.Count; j++) zScene += Mathf.Abs(metaList[j].relativePosition.z);
        zScene /= metaList.Count;

        for (int i = 0; i < photos.Length; i++)
        {
            if (i >= metaList.Count) continue;
            var meta = metaList[i];
            Vector2 pp = meta.principalPoint;
            float px = meta.focalLength.x * meta.relativePosition.x / zScene;
            int baseX = Mathf.RoundToInt(pp.x + px);
            int baseY = Mathf.RoundToInt((meta.relativePosition.y - worldMinY) * positionScale);
            Texture2D photo = photos[i];
            Quaternion rot = Quaternion.Euler(meta.relativeEulerAngles);
            Texture2D rotated = Apply3DRotation(photo, rot);
            CopyPhotoToGrid(rotated, yCanvas, yCanvasPixels, baseX, baseY);
            if (i % 2 == 0) yield return null;
        }
        yCanvas.SetPixels(yCanvasPixels);
        yCanvas.Apply();
        string yFilePath = Path.Combine(Application.persistentDataPath, "Y_Relative_Stitch_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png");
        File.WriteAllBytes(yFilePath, yCanvas.EncodeToPNG());
        onComplete?.Invoke(yCanvas, yFilePath);
    }

    private bool ValidateInput()
    {
        if (photos == null || photoDataList == null || photos.Length == 0 || photoDataList.Count == 0)
        {
            PopupLogger.Log("[PhotoStitcher] Nothing to stitch");
            return false;
        }
        return true;
    }

    private Texture2D CreateCanvas(int width, int height)
    {
        return new Texture2D(width, height, TextureFormat.RGBA32, false);
    }

    private string SaveCanvasToFile(Texture2D canvas, string filename)
    {
        string filePath = Path.Combine(Application.persistentDataPath, filename);
        byte[] pngData = canvas.EncodeToPNG();
        File.WriteAllBytes(filePath, pngData);
        return filePath;
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

    /// <summary>
    /// Aligns the edges of two photos with a specified step (for example, every 50th pixel).
    /// edgeA и edgeB: "left", "right", "top", "bottom"
    /// </summary>
    public static float CompareEdgesWithStep(Texture2D photoA, Texture2D photoB, string edgeA, string edgeB,
        int step = 50)
    {
        int width = photoA.width;
        int height = photoA.height;
        float totalDiff = 0f;
        int count = 0;

        if ((edgeA == "right" || edgeA == "left") && (edgeB == "right" || edgeB == "left"))
        {
            // Vertical edges
            for (int y = 0; y < height; y += step)
            {
                Color colorA = (edgeA == "right") ? photoA.GetPixel(width - 1, y) : photoA.GetPixel(0, y);
                Color colorB = (edgeB == "right") ? photoB.GetPixel(width - 1, y) : photoB.GetPixel(0, y);
                totalDiff += Mathf.Abs(colorA.r - colorB.r) + Mathf.Abs(colorA.g - colorB.g) +
                             Mathf.Abs(colorA.b - colorB.b);
                count++;
            }
        }
        else if ((edgeA == "top" || edgeA == "bottom") && (edgeB == "top" || edgeB == "bottom"))
        {
            // Horizontal edges
            for (int x = 0; x < width; x += step)
            {
                Color colorA = (edgeA == "top") ? photoA.GetPixel(x, height - 1) : photoA.GetPixel(x, 0);
                Color colorB = (edgeB == "top") ? photoB.GetPixel(x, height - 1) : photoB.GetPixel(x, 0);
                totalDiff += Mathf.Abs(colorA.r - colorB.r) + Mathf.Abs(colorA.g - colorB.g) +
                             Mathf.Abs(colorA.b - colorB.b);
                count++;
            }
        }
        else
        {
            Debug.LogWarning($"Edge combination {edgeA}-{edgeB} is not supported for comparison.");
            return -1f;
        }

        return (count > 0) ? totalDiff / count : -1f;
    }

    public static Texture2D GetEdge(Texture2D source, string edge, int thickness = 5)
    {
        int width = source.width;
        int height = source.height;
        Texture2D edgeTex = null;

        if (edge == "left")
        {
            edgeTex = new Texture2D(thickness, height, source.format, false);
            for (int x = 0; x < thickness; x++)
            for (int y = 0; y < height; y++)
                edgeTex.SetPixel(x, y, source.GetPixel(x, y));
        }
        else if (edge == "right")
        {
            edgeTex = new Texture2D(thickness, height, source.format, false);
            for (int x = 0; x < thickness; x++)
            for (int y = 0; y < height; y++)
                edgeTex.SetPixel(x, y, source.GetPixel(width - thickness + x, y));
        }
        else if (edge == "top")
        {
            edgeTex = new Texture2D(width, thickness, source.format, false);
            for (int x = 0; x < width; x++)
            for (int y = 0; y < thickness; y++)
                edgeTex.SetPixel(x, y, source.GetPixel(x, height - thickness + y));
        }
        else if (edge == "bottom")
        {
            edgeTex = new Texture2D(width, thickness, source.format, false);
            for (int x = 0; x < width; x++)
            for (int y = 0; y < thickness; y++)
                edgeTex.SetPixel(x, y, source.GetPixel(x, y));
        }

        edgeTex.Apply();
        return edgeTex;
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private void SaveToGalleryAndroid(string filePath, string yFilePath)
    {
        string galleryDir = Path.Combine("/storage/emulated/0/Pictures/ARCameraDemo/");
        if (!Directory.Exists(galleryDir)) Directory.CreateDirectory(galleryDir);
        string finalName = Path.GetFileName(filePath);
        string finalPath = Path.Combine(galleryDir, finalName);
        File.Copy(filePath, finalPath, true);
        AndroidMediaScanner.ScanFile(finalPath);
        NativeGallery.SaveImageToGallery(finalPath, "ARCameraDemo", finalName);
        string yFinalName = Path.GetFileName(yFilePath);
        string yFinalPath = Path.Combine(galleryDir, yFinalName);
        File.Copy(yFilePath, yFinalPath, true);
        AndroidMediaScanner.ScanFile(yFinalPath);
        NativeGallery.SaveImageToGallery(yFinalPath, "ARCameraDemo", yFinalName);
    }
#endif
}