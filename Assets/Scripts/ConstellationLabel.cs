using TMPro;
using UnityEngine;

[RequireComponent(typeof(TextMeshPro))]
public class ConstellationLabel : MonoBehaviour
{
    TextMeshPro textMesh;
    Transform anchor;
    Vector3 localOffset;
    Vector2 cameraFacingOffset;
    float currentFontSize = 1f;
    Color currentColor = Color.white;
    TMP_FontAsset currentFont;

    public string CurrentText => textMesh != null ? textMesh.text : string.Empty;

    void Awake()
    {
        textMesh = GetComponent<TextMeshPro>();
        if (textMesh == null)
        {
            textMesh = gameObject.AddComponent<TextMeshPro>();
        }

        textMesh.text = string.Empty;
        textMesh.alignment = TextAlignmentOptions.Center;
        textMesh.enableWordWrapping = false;
    }

    public void Initialize(Transform anchorTransform, Vector3 offset, Vector2 facingOffset, float fontSize, Color color, TMP_FontAsset fontAsset)
    {
        anchor = anchorTransform;
        if (anchor != null)
        {
            transform.SetParent(anchor, false);
        }

        transform.localScale = Vector3.one;
        Configure(offset, facingOffset, fontSize, color, fontAsset);
    }

    public void Configure(Vector3 offset, Vector2 facingOffset, float fontSize, Color color, TMP_FontAsset fontAsset)
    {
        localOffset = offset;
        cameraFacingOffset = facingOffset;
        currentFontSize = fontSize;
        currentColor = color;
        if (fontAsset != null)
        {
            currentFont = fontAsset;
        }
        ApplyStyle();
        UpdateTransform();
    }

    public void SetText(string value)
    {
        if (textMesh == null)
        {
            Awake();
        }

        textMesh.text = value;
    }

    void ApplyStyle()
    {
        if (textMesh == null)
        {
            return;
        }

        if (currentFont != null)
        {
            textMesh.font = currentFont;
        }

        textMesh.fontSize = currentFontSize;
        textMesh.color = currentColor;
    }

    void LateUpdate()
    {
        if (anchor == null)
        {
            Destroy(gameObject);
            return;
        }

        UpdateTransform();
    }

    void UpdateTransform()
    {
        if (anchor == null)
        {
            return;
        }

        Vector3 baseWorldPosition = anchor.TransformPoint(localOffset);
        transform.position = baseWorldPosition;

        Camera camera = Camera.main;
        if (camera != null)
        {
            Vector3 toCamera = baseWorldPosition - camera.transform.position;
            if (toCamera.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(toCamera, camera.transform.up);
            }

            transform.position = baseWorldPosition
                                 + camera.transform.right * cameraFacingOffset.x
                                 + camera.transform.up * cameraFacingOffset.y;
        }
    }
}
