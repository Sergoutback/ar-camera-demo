using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using System;
using Unity.Collections;

public class ARCameraToRawImage : MonoBehaviour
{
    public ARCameraManager cameraManager;
    public RawImage cameraRawImage;

    private Texture2D cameraTexture;

    void OnEnable()
    {
        cameraManager.frameReceived += OnCameraFrameReceived;
    }

    void OnDisable()
    {
        cameraManager.frameReceived -= OnCameraFrameReceived;
    }

    void OnCameraFrameReceived(ARCameraFrameEventArgs eventArgs)
    {
        if (!cameraManager.TryAcquireLatestCpuImage(out XRCpuImage image))
            return;

        var conversionParams = new XRCpuImage.ConversionParams
        {
            inputRect = new RectInt(0, 0, image.width, image.height),
            outputDimensions = new Vector2Int(image.width, image.height),
            outputFormat = TextureFormat.RGBA32,
            transformation = XRCpuImage.Transformation.MirrorY
        };

        // Allocate a buffer to store the image
        int size = image.GetConvertedDataSize(conversionParams);
        var buffer = new NativeArray<byte>(size, Allocator.Temp);

        image.Convert(conversionParams, buffer);

        if (cameraTexture == null || cameraTexture.width != image.width || cameraTexture.height != image.height)
        {
            cameraTexture = new Texture2D(image.width, image.height, TextureFormat.RGBA32, false);
        }

        cameraTexture.LoadRawTextureData(buffer);
        cameraTexture.Apply();

        // Dispose
        buffer.Dispose();
        image.Dispose();

        cameraRawImage.texture = RotateTexture90CCW(cameraTexture);
    }

    // Helper method to rotate a Texture2D 90 degrees clockwise
    private Texture2D RotateTexture90(Texture2D original)
    {
        int width = original.width;
        int height = original.height;
        Texture2D rotated = new Texture2D(height, width, original.format, false);
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

    // Helper method to rotate a Texture2D 90 degrees counterclockwise
    private Texture2D RotateTexture90CCW(Texture2D original)
    {
        int width = original.width;
        int height = original.height;
        Texture2D rotated = new Texture2D(height, width, original.format, false);
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
}