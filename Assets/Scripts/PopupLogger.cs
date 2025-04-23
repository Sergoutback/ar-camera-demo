using UnityEngine;
using TMPro;
using System.Collections;

public class PopupLogger : MonoBehaviour
{
    [SerializeField] private GameObject popup;
    [SerializeField] private TextMeshProUGUI popupText;

    private static PopupLogger _instance;

    void Awake()
    {
        _instance = this;
    }

    public static void Log(string message, bool isWarning = false)
    {
#if UNITY_EDITOR
        if (isWarning)
            Debug.LogWarning(message);
        else
            Debug.Log(message);
#endif
        if (_instance != null)
            _instance.ShowPopup(message);
    }

    private void ShowPopup(string message)
    {
        if (popup != null && popupText != null)
        {
            popup.SetActive(true);
            popupText.text = message;
            StartCoroutine(HidePopupAfterDelay(2f));
        }
    }

    private IEnumerator HidePopupAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        popup.SetActive(false);
    }
}