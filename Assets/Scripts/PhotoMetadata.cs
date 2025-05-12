using System;
using UnityEngine;

[Serializable]
public class PhotoMetadata
{
    public string photoId;
    public string sessionId;
    public string timestamp;
    public string path;
    public int width;
    public int height;
    public int quality;

    public Vector3 gyroRotationRate;
    public Quaternion gyroAttitude;
    public Quaternion relativeGyroAttitude;
    public Vector3 relativeEulerAngles;
    public Vector3 relativePosition;
    public Vector3 gyroEulerAngles;

    public float latitude;
    public float longitude;
    public Vector2 focalLength;
    public Vector2 sensorSize;
    public Vector2Int resolution;
    public Vector2 principalPoint;
}