using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Instantiates a UI prefab at the mouse position when clicking on the canvas.
/// Attach to the Canvas, assign a prefab (Text/InputField/etc.), and set the target canvas reference.
/// </summary>
public class ClickToPlaceText : MonoBehaviour
{
    [SerializeField]
    private GameObject textPrefab;

    [SerializeField]
    private Canvas targetCanvas;

    [SerializeField]
    private bool ignoreClicksOverUI = true;

    [SerializeField]
    private bool manualPlacementEnabled = false;

    private void Reset()
    {
        targetCanvas = GetComponent<Canvas>();
    }

    private void Update()
    {
        if (!manualPlacementEnabled || !Input.GetMouseButtonDown(1))
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

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                targetCanvas.transform as RectTransform,
                Input.mousePosition,
                targetCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : targetCanvas.worldCamera,
                out var localPoint))
        {
            return;
        }

        var instance = Instantiate(textPrefab, targetCanvas.transform);
        if (!instance.TryGetComponent(out RectTransform rect))
        {
            return;
        }

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(rect);

        Vector2 anchoredPosition = localPoint;
        anchoredPosition.x += rect.rect.width * rect.pivot.x;
        rect.anchoredPosition = anchoredPosition;
    }
}
