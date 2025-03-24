using UnityEngine;

public static class AndroidMediaScanner
{
    public static void ScanFile(string path)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (AndroidJavaObject context = currentActivity.Call<AndroidJavaObject>("getApplicationContext"))
            using (AndroidJavaClass mediaScanner = new AndroidJavaClass("android.media.MediaScannerConnection"))
            {
                mediaScanner.CallStatic("scanFile", context, new string[] { path }, null, null);
                Debug.Log($"[MediaScanner] Scanned file: {path}");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[MediaScanner] Failed: {e.Message}");
        }
#endif
    }
}