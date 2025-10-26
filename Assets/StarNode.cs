using UnityEngine;

[RequireComponent(typeof(Renderer))]
[RequireComponent(typeof(Collider))]
public class StarNode : MonoBehaviour
{
    StarFieldManager manager;
    Renderer meshRenderer;
    SphereCollider selectionCollider;
    Material materialInstance;

    Color baseColor;
    Color highlightColor;
    float baseScale;
    float baseEmissionMultiplier = 1.6f;
    float highlightedEmissionMultiplier = 2.4f;
    float currentEmissionMultiplier;
    bool isSelected;
    bool enableSelectionPulse;
    float selectionPulseScaleAmount;
    float selectionPulseSpeed = 3f;
    float pulseTimer;
    float selectionColliderWorldRadius;

    public Vector3 Position => transform.position;

    public void Initialize(
        StarFieldManager owner,
        Color defaultColor,
        Color selectedColor,
        float visualScale,
        float selectionMultiplier,
        float minColliderRadius,
        float padding,
        float baseEmission,
        float highlightedEmission,
        bool enablePulse,
        float pulseScale,
        float pulseSpeed)
    {
        manager = owner;
        meshRenderer = GetComponent<Renderer>();
        selectionCollider = GetComponent<SphereCollider>();
        baseColor = defaultColor;
        highlightColor = selectedColor;
        baseScale = Mathf.Max(0.01f, visualScale);

        if (meshRenderer != null)
        {
            materialInstance = meshRenderer.material;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
        }

        if (selectionCollider != null)
        {
            selectionCollider.isTrigger = false;
            float clampedMultiplier = Mathf.Max(0.01f, selectionMultiplier);
            float clampedMinRadius = Mathf.Max(0.01f, minColliderRadius);
            float clampedPadding = Mathf.Max(0f, padding);
            float desiredWorldRadius = Mathf.Max(clampedMinRadius, baseScale * 0.5f * clampedMultiplier) + clampedPadding;
            selectionColliderWorldRadius = desiredWorldRadius;
            selectionCollider.radius = selectionColliderWorldRadius / baseScale;
        }

        baseEmissionMultiplier = Mathf.Max(0f, baseEmission);
        highlightedEmissionMultiplier = Mathf.Max(baseEmissionMultiplier, highlightedEmission);
        enableSelectionPulse = enablePulse;
        selectionPulseScaleAmount = Mathf.Max(0f, pulseScale);
        selectionPulseSpeed = Mathf.Max(0.1f, pulseSpeed);
        pulseTimer = 0f;

        SetSelected(false);
    }

    public void SetSelected(bool selected)
    {
        isSelected = selected;
        pulseTimer = 0f;

        currentEmissionMultiplier = selected ? highlightedEmissionMultiplier : baseEmissionMultiplier;

        if (materialInstance != null)
        {
            materialInstance.color = selected ? highlightColor : baseColor;
            ApplyEmission();
        }

        if (!selected)
        {
            ResetScaleInstant();
        }
    }

    void Update()
    {
        UpdateSelectionEffects(Time.deltaTime);
    }

    void UpdateSelectionEffects(float deltaTime)
    {
        if (baseScale <= 0f)
        {
            return;
        }

        float targetScale = baseScale;
        float emissionMultiplier = baseEmissionMultiplier;

        if (isSelected)
        {
            float intensityLerp = 1f;

            if (enableSelectionPulse && selectionPulseScaleAmount > 0.001f)
            {
                pulseTimer += deltaTime * selectionPulseSpeed;
                float pulse = 0.5f + 0.5f * Mathf.Sin(pulseTimer);
                targetScale = baseScale * (1f + selectionPulseScaleAmount * pulse);
                intensityLerp = pulse;
            }
            else
            {
                pulseTimer = 0f;
                if (enableSelectionPulse && selectionPulseScaleAmount > 0f)
                {
                    targetScale = baseScale * (1f + selectionPulseScaleAmount);
                }
            }

            float minIntensity = highlightedEmissionMultiplier * 0.85f;
            float maxIntensity = highlightedEmissionMultiplier * 1.25f;
            emissionMultiplier = enableSelectionPulse
                ? Mathf.Lerp(minIntensity, maxIntensity, intensityLerp)
                : highlightedEmissionMultiplier;
        }
        else
        {
            pulseTimer = 0f;
        }

        float currentScale = transform.localScale.x;
        float desiredScale = Mathf.Lerp(currentScale, targetScale, deltaTime * 8f);
        transform.localScale = Vector3.one * desiredScale;

        if (selectionCollider != null && selectionColliderWorldRadius > 0f)
        {
            float safeScale = Mathf.Max(0.001f, desiredScale);
            selectionCollider.radius = selectionColliderWorldRadius / safeScale;
        }

        if (materialInstance != null && !Mathf.Approximately(emissionMultiplier, currentEmissionMultiplier))
        {
            currentEmissionMultiplier = emissionMultiplier;
            ApplyEmission();
        }
    }

    void ResetScaleInstant()
    {
        transform.localScale = Vector3.one * baseScale;
        if (selectionCollider != null && selectionColliderWorldRadius > 0f)
        {
            selectionCollider.radius = selectionColliderWorldRadius / Mathf.Max(0.001f, baseScale);
        }
    }

    void ApplyEmission()
    {
        if (materialInstance.HasProperty("_EmissionColor"))
        {
            materialInstance.EnableKeyword("_EMISSION");
            materialInstance.SetColor("_EmissionColor", materialInstance.color * currentEmissionMultiplier);
        }
    }

    void OnDestroy()
    {
        if (materialInstance != null && Application.isPlaying)
        {
            Destroy(materialInstance);
        }
    }
}
