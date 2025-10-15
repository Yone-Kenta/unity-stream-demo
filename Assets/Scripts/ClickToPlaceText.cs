using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Instantiates a UI prefab (Text, TMP_Text, InputField など) at the mouse click position.
/// Attach this component to the Canvas that should hold the spawned elements.
/// </summary>
public class ClickToPlaceText : MonoBehaviour
{
    [SerializeField]
    private GameObject textPrefab;

    [SerializeField]
    private Canvas targetCanvas;

    [SerializeField]
    private bool ignoreClicksOverUI = true;

    private void Reset()
    {
        targetCanvas = GetComponent<Canvas>();
    }

    private void Update()
    {
        if (!Input.GetMouseButtonDown(0))
        {
            return;
        }

        if (ignoreClicksOverUI && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        if (textPrefab == null || targetCanvas == null)
        {
            Debug.LogWarning("[ClickToPlaceText] Prefab or Canvas is not set.");
            return;
        }

        var canvasRect = targetCanvas.transform as RectTransform;

        if (canvasRect == null)
        {
            Debug.LogWarning("[ClickToPlaceText] Target canvas must be a UI canvas.");
            return;
        }

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                Input.mousePosition,
                targetCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : targetCanvas.worldCamera,
                out var localPoint))
        {
            return;
        }

        var instance = Instantiate(textPrefab, targetCanvas.transform);
        if (instance.TryGetComponent(out RectTransform rect))
        {
            rect.anchoredPosition = localPoint;
        }
    }
}
