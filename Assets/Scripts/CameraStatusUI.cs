using UnityEngine;
using TMPro;

/// <summary>
/// Displays camera tracking status, relative position, and rotation using TextMeshPro.
/// </summary>
public class CameraStatusUI : MonoBehaviour
{
    public TextMeshProUGUI statusText;
    public TextMeshProUGUI positionText;
    public TextMeshProUGUI rotationText;

    public Color waitingColor = Color.red;
    public Color stationaryColor = new Color(1f, 0.65f, 0f); // orange
    public Color readyColor = Color.green;

    /// <summary>
    /// Shows red "waiting for tracking" status.
    /// </summary>
    public void ShowWaiting()
    {
        SetColors(waitingColor);
        statusText.text = "🔴 Waiting for tracking...";
        positionText.text = "";
        rotationText.text = "";
    }

    /// <summary>
    /// Shows yellow "no movement" status with position and rotation info.
    /// </summary>
    public void ShowNoMovement(Vector3 relativePos, Vector3 relativeEuler)
    {
        SetColors(stationaryColor);
        statusText.text = "🟡 Camera is stationary";
        positionText.text = $"📍 Offset: {FormatVector(relativePos)}";
        rotationText.text = $"🔄 Rotation: {FormatVector(relativeEuler)}";
    }

    /// <summary>
    /// Shows green "ready to capture" status with position and rotation info.
    /// </summary>
    public void ShowReady(Vector3 relativePos, Vector3 relativeEuler)
    {
        SetColors(readyColor);
        statusText.text = "🟢 Ready to capture";
        positionText.text = $"📍 Offset: {FormatVector(relativePos)}";
        rotationText.text = $"🔄 Rotation: {FormatVector(relativeEuler)}";
    }

    private void SetColors(Color color)
    {
        if (statusText != null) statusText.color = color;
        if (positionText != null) positionText.color = color;
        if (rotationText != null) rotationText.color = color;
    }

    private string FormatVector(Vector3 v)
    {
        return $"({v.x:F2}, {v.y:F2}, {v.z:F2})";
    }
}