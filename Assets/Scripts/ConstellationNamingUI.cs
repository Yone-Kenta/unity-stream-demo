using System;
using UnityEngine;
using UnityEngine.UI;

public class ConstellationNamingUI : MonoBehaviour
{
    [SerializeField]
    RectTransform panel;

    [SerializeField]
    InputField inputField;

    Canvas rootCanvas;
    Action<string> submitCallback;
    Action<string> changeCallback;
    Action cancelCallback;
    bool isActive;
    bool cancelRequested;
    bool suppressChangeEvent;
    string lastPreviewText = string.Empty;

    void Awake()
    {
        rootCanvas = GetComponentInParent<Canvas>();
        if (panel == null && inputField != null)
        {
            panel = inputField.GetComponent<RectTransform>();
        }

        if (panel == null)
        {
            panel = GetComponent<RectTransform>();
        }

        if (inputField != null)
        {
            inputField.text = string.Empty;
            inputField.onEndEdit.AddListener(HandleEndEdit);
            inputField.onValueChanged.AddListener(HandleValueChanged);
        }

        HideImmediate();
    }

    void OnDestroy()
    {
        if (inputField != null)
        {
            inputField.onEndEdit.RemoveListener(HandleEndEdit);
            inputField.onValueChanged.RemoveListener(HandleValueChanged);
        }
    }

    void Update()
    {
        if (!isActive)
        {
            return;
        }

        string livePreview = BuildPreviewText();
        if (!string.Equals(livePreview, lastPreviewText, StringComparison.Ordinal))
        {
            SendPreview(livePreview);
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            cancelRequested = true;
            Hide();
            return;
        }
    }

    public void Show(Vector2 screenPosition, string initialText, Action<string> onSubmit, Action<string> onChange, Action onCancel)
    {
        if (panel == null || inputField == null)
        {
            Debug.LogWarning("[ConstellationNamingUI] Panel or InputField is not assigned.");
            return;
        }

        submitCallback = onSubmit;
        changeCallback = onChange;
        cancelCallback = onCancel;
        panel.gameObject.SetActive(true);
        isActive = true;
        cancelRequested = false;

        MoveToScreenPosition(screenPosition);

        lastPreviewText = string.Empty;
        suppressChangeEvent = true;
        inputField.text = initialText ?? string.Empty;
        inputField.caretPosition = inputField.text.Length;
        suppressChangeEvent = false;
        SendPreview(BuildPreviewText());
        inputField.Select();
        inputField.ActivateInputField();
    }

    public void Hide()
    {
        if (panel != null)
        {
            panel.gameObject.SetActive(false);
        }

        if (cancelRequested)
        {
            cancelCallback?.Invoke();
        }

        isActive = false;
        lastPreviewText = string.Empty;
        submitCallback = null;
        changeCallback = null;
        cancelCallback = null;
        cancelRequested = false;
    }

    public void HideImmediate()
    {
        if (panel != null)
        {
            panel.gameObject.SetActive(false);
        }

        isActive = false;
        lastPreviewText = string.Empty;
        submitCallback = null;
        changeCallback = null;
        cancelCallback = null;
        cancelRequested = false;
    }

    void Submit()
    {
        if (!isActive)
        {
            return;
        }

        submitCallback?.Invoke(inputField != null ? inputField.text : string.Empty);
        Hide();
    }

    void HandleEndEdit(string _)
    {
        if (!isActive)
        {
            cancelRequested = false;
            return;
        }

        if (cancelRequested)
        {
            cancelRequested = false;
            return;
        }

        SendPreview(BuildPreviewText());
        Submit();
    }

    void HandleValueChanged(string _)
    {
        if (suppressChangeEvent)
        {
            return;
        }

        if (!isActive)
        {
            return;
        }

        SendPreview(BuildPreviewText());
    }

    string BuildPreviewText()
    {
        if (inputField == null)
        {
            return string.Empty;
        }

        string baseText = inputField.text ?? string.Empty;
        string composition = Input.compositionString;
        if (string.IsNullOrEmpty(composition))
        {
            return baseText;
        }

        int insertionIndex = Mathf.Clamp(inputField.caretPosition, 0, baseText.Length);
        return baseText.Insert(insertionIndex, composition);
    }

    void SendPreview(string preview)
    {
        lastPreviewText = preview ?? string.Empty;
        changeCallback?.Invoke(lastPreviewText);
    }

    void MoveToScreenPosition(Vector2 screenPosition)
    {
        if (panel == null || rootCanvas == null)
        {
            return;
        }

        RectTransform canvasRect = rootCanvas.transform as RectTransform;
        Camera camera = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, camera, out Vector2 localPoint))
        {
            panel.anchoredPosition = localPoint;
        }
    }

}
