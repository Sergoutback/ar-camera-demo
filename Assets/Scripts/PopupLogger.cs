using UnityEngine;
using TMPro;
using System.Collections;
using UnityEngine.UI;

public class PopupLogger : MonoBehaviour
{
    [SerializeField] private GameObject popup;
    [SerializeField] private TextMeshProUGUI popupText;
    [SerializeField] private Button closeButton;

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

            if (closeButton != null)
            {
                closeButton.gameObject.SetActive(true);
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(HidePopup);
            }
        }
    }

    private void HidePopup()
    {
        if (popup != null)
            popup.SetActive(false);
    }


    private IEnumerator HidePopupAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        popup.SetActive(false);
    }
}