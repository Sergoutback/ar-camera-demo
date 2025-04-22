using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class StartupLoader : MonoBehaviour
{
    [SerializeField] private string mainSceneName = "MainScene";

    void Start()
    {
        StartCoroutine(RequestPermissionsAndLoad());
    }

    private IEnumerator RequestPermissionsAndLoad()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera))
        {
            UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Camera);
            yield return new WaitUntil(() =>
                UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera));
        }

        if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.FineLocation))
        {
            UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.FineLocation);
            yield return new WaitUntil(() =>
                UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.FineLocation));
        }
#endif

        yield return new WaitForSeconds(0.5f);
        SceneManager.LoadScene(mainSceneName);
    }
}