using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public class StarFieldManager : MonoBehaviour
{
    [Header("Star Field")]
    [SerializeField, Min(1)] int starCount = 75;
    [SerializeField] bool randomizeStarCount = true;
    [SerializeField] Vector2Int randomStarCountRange = new Vector2Int(60, 120);
    [SerializeField] bool regenerateOnStart = true;
    [SerializeField] Vector3 fieldCenter = Vector3.zero;
    [SerializeField] Vector3 fieldSize = new Vector3(80f, 0f, 80f);
    [SerializeField] Vector2 starScaleRange = new Vector2(1.1f, 2f);
    [SerializeField] Color starColor = new Color(1f, 1f, 1f, 0.95f);
    [SerializeField] Color selectedStarColor = new Color(0.4f, 0.8f, 1f, 1f);

    [Header("Boundary Stars")]
    [SerializeField] bool enableBoundaryStars = true;
    [SerializeField, Min(0f)] float boundaryPadding = 20f;
    [SerializeField, Min(0f)] float boundaryVerticalPadding = 0f;
    [SerializeField, Range(0f, 10f)] float boundaryStarDensity = 0.6f;

    public Vector3 FieldCenter => fieldCenter;
    public Vector3 FieldSize => fieldSize;

    [Header("Selection")]
    [SerializeField] float selectionColliderMultiplier = 1.4f;
    [SerializeField] float minimumColliderRadius = 0.65f;
    [SerializeField] float selectionColliderPadding = 0.4f;

    [Header("Selection Visuals")]
    [SerializeField, Min(0f)] float baseEmissionIntensity = 1.6f;
    [SerializeField, Min(0f)] float selectedEmissionIntensity = 4.2f;
    [SerializeField] bool enableSelectionPulse = true;
    [SerializeField, Range(0f, 1.5f)] float selectionPulseScale = 0.3f;
    [SerializeField, Min(0.1f)] float selectionPulseSpeed = 3.2f;

    [Header("Constellation Lines")]
    [SerializeField] float lineWidth = 0.05f;
    [SerializeField] Color lineColor = new Color(0.7f, 0.85f, 1f, 1f);
    [SerializeField] float lineSelectionRadius = 0.2f;
    [SerializeField, Min(0f)] float lineSelectionPadding = 0.3f;
    [SerializeField] KeyCode undoLineKey = KeyCode.Backspace;
    [SerializeField] KeyCode clearAllLinesKey = KeyCode.Delete;
    [SerializeField, Tooltip("If enabled, newly connected stars stay selected so you can chain connections without reselecting. Disable for discrete pairs.")] bool keepLastStarSelectedAfterConnection;

    [Header("Constellation Naming")]
    [SerializeField] ConstellationNamingUI namingUI;
    [SerializeField] Camera namingCamera;
    [SerializeField, Min(0f)] float labelVerticalOffset = 0.6f;
    [SerializeField, Min(0.1f)] float labelFontSize = 1.2f;
    [SerializeField] Color labelColor = new Color(0.85f, 0.95f, 1f, 1f);
    [SerializeField] TMP_FontAsset labelFontAsset;
    [SerializeField] Vector2 labelScreenOffset = new Vector2(0f, 0.35f);

    [Header("Shooting Stars")]
    [SerializeField] bool enableShootingStars = true;
    [SerializeField] Vector2 shootingStarIntervalRange = new Vector2(6f, 12f);
    [SerializeField] Vector2 shootingStarSpeedRange = new Vector2(16f, 26f);
    [SerializeField] Vector2 shootingStarLifetimeRange = new Vector2(0.8f, 1.2f);
    [SerializeField] float shootingStarAltitudeOffset = 6f;
    [SerializeField] Gradient shootingStarGradient;

    readonly List<StarNode> stars = new();
    readonly List<GameObject> constellationLines = new();
    readonly Dictionary<GameObject, ConstellationLabel> lineLabels = new();
    readonly Dictionary<StarPair, GameObject> starConnections = new();
    readonly Dictionary<GameObject, StarPair> lineToConnection = new();
    readonly List<StarNode> boundaryStars = new();

    Material lineMaterial;
    StarNode pendingConnection;
    Vector3 cachedHalfFieldSize;
    float shootingStarTimer;
    ConstellationLineHandle activeNamingHandle;
    string pendingOriginalLabelText;
    bool selectionCameraWarningLogged;

    void Awake()
    {
        EnsureGradient();
    }

    void Start()
    {
        InitializeLineMaterial();
        if (regenerateOnStart)
        {
            GenerateStarField();
        }
        ResetShootingStarTimer();
    }

    void Update()
    {
        if (constellationLines.Count > 0 && Input.GetKeyDown(undoLineKey))
        {
            RemoveLastLine();
        }

        if (constellationLines.Count > 0 && Input.GetKeyDown(clearAllLinesKey))
        {
            ClearAllLines();
        }

        if (Input.GetMouseButtonDown(0))
        {
            TrySelectStarUnderCursor();
        }

        if (enableShootingStars)
        {
            shootingStarTimer -= Time.deltaTime;
            if (shootingStarTimer <= 0f)
            {
                SpawnShootingStar();
                ResetShootingStarTimer();
            }
        }
    }

    [ContextMenu("Regenerate Star Field")]
    void RegenerateStarFieldContext()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            InitializeLineMaterial();
        }
#endif
        GenerateStarField();
    }

    public void GenerateStarField()
    {
        ClearExistingStars();

        cachedHalfFieldSize = ComputeHalfFieldSize();
        Vector3 halfSize = cachedHalfFieldSize;

        float minScale = Mathf.Max(0.01f, Mathf.Min(starScaleRange.x, starScaleRange.y));
        float maxScale = Mathf.Max(minScale, Mathf.Max(starScaleRange.x, starScaleRange.y));

        int minCount = Mathf.Max(1, Mathf.Min(randomStarCountRange.x, randomStarCountRange.y));
        int maxCount = Mathf.Max(minCount, Mathf.Max(randomStarCountRange.x, randomStarCountRange.y));
        int targetCount = randomizeStarCount ? Random.Range(minCount, maxCount + 1) : Mathf.Max(1, starCount);

        for (int i = 0; i < targetCount; i++)
        {
            Vector3 position = fieldCenter + new Vector3(
                Random.Range(-halfSize.x, halfSize.x),
                Random.Range(-halfSize.y, halfSize.y),
                Random.Range(-halfSize.z, halfSize.z));

            float scale = Random.Range(minScale, maxScale);
            CreateStar(position, scale, false);
        }

        int coreStars = stars.Count;
        int boundaryCreated = GenerateBoundaryStars(targetCount, minScale, maxScale);

        Debug.Log($"[StarFieldManager] Generated {coreStars} core stars and {boundaryCreated} boundary stars (total: {stars.Count}).");
    }

    Color SampleStarColor()
    {
        float brightness = Random.Range(0.7f, 1.2f);
        return new Color(
            Mathf.Clamp01(starColor.r * brightness),
            Mathf.Clamp01(starColor.g * brightness),
            Mathf.Clamp01(starColor.b * brightness),
            starColor.a);
    }

    StarNode CreateStar(Vector3 position, float scale, bool isBoundary)
    {
        GameObject star = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        star.name = isBoundary ? $"BoundaryStar_{boundaryStars.Count:D3}" : $"Star_{stars.Count:D3}";
        star.transform.SetParent(transform, false);
        star.transform.position = position;
        star.transform.localScale = Vector3.one * scale;

        Color tintedColor = SampleStarColor();

        StarNode node = star.AddComponent<StarNode>();
        node.Initialize(
            this,
            tintedColor,
            selectedStarColor,
            scale,
            selectionColliderMultiplier,
            minimumColliderRadius,
            selectionColliderPadding,
            baseEmissionIntensity,
            selectedEmissionIntensity,
            enableSelectionPulse,
            selectionPulseScale,
            selectionPulseSpeed);

        stars.Add(node);
        if (isBoundary)
        {
            boundaryStars.Add(node);
        }

        return node;
    }

    int GenerateBoundaryStars(int referenceCount, float minScale, float maxScale)
    {
        if (!enableBoundaryStars)
        {
            return 0;
        }

        float clampedDensity = Mathf.Max(0f, boundaryStarDensity);
        int targetCount = Mathf.RoundToInt(referenceCount * clampedDensity);
        if (targetCount <= 0)
        {
            return 0;
        }

        Vector3 halfMain = cachedHalfFieldSize;
        Vector3 halfExpanded = new Vector3(
            Mathf.Max(halfMain.x + boundaryPadding, halfMain.x),
            Mathf.Max(halfMain.y + boundaryVerticalPadding, halfMain.y),
            Mathf.Max(halfMain.z + boundaryPadding, halfMain.z));

        bool hasHorizontalExpansion = halfExpanded.x > halfMain.x || halfExpanded.z > halfMain.z;
        bool hasVerticalExpansion = halfExpanded.y > halfMain.y;
        if (!hasHorizontalExpansion && !hasVerticalExpansion)
        {
            return 0;
        }

        int created = 0;
        for (int i = 0; i < targetCount; i++)
        {
            if (!TrySampleBoundaryPosition(halfMain, halfExpanded, out Vector3 position))
            {
                continue;
            }

            float scale = Random.Range(minScale, maxScale);
            CreateStar(position, scale, true);
            created++;
        }

        return created;
    }

    bool TrySampleBoundaryPosition(Vector3 halfMain, Vector3 halfExpanded, out Vector3 worldPosition)
    {
        const int maxAttempts = 24;
        const float epsilon = 0.05f;

        bool allowX = halfExpanded.x > halfMain.x + epsilon;
        bool allowY = halfExpanded.y > halfMain.y + epsilon;
        bool allowZ = halfExpanded.z > halfMain.z + epsilon;

        if (!allowX && !allowY && !allowZ)
        {
            worldPosition = Vector3.zero;
            return false;
        }

        var axes = new List<int>(3);
        if (allowX) axes.Add(0);
        if (allowY) axes.Add(1);
        if (allowZ) axes.Add(2);

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            int axisSelection = axes[Random.Range(0, axes.Count)];

            float x = Random.Range(-halfExpanded.x, halfExpanded.x);
            float y = allowY
                ? Random.Range(-halfExpanded.y, halfExpanded.y)
                : 0f;
            float z = Random.Range(-halfExpanded.z, halfExpanded.z);

            if (axisSelection == 0)
            {
                float minDistance = halfMain.x + epsilon;
                float maxDistance = Mathf.Max(minDistance, halfExpanded.x);
                float magnitude = Random.Range(minDistance, maxDistance);
                float sign = Random.value < 0.5f ? -1f : 1f;
                x = sign * magnitude;
            }

            if (axisSelection == 1)
            {
                float minDistance = halfMain.y + epsilon;
                float maxDistance = Mathf.Max(minDistance, halfExpanded.y);
                float magnitude = Random.Range(minDistance, maxDistance);
                float sign = Random.value < 0.5f ? -1f : 1f;
                y = sign * magnitude;
            }

            if (axisSelection == 2)
            {
                float minDistance = halfMain.z + epsilon;
                float maxDistance = Mathf.Max(minDistance, halfExpanded.z);
                float magnitude = Random.Range(minDistance, maxDistance);
                float sign = Random.value < 0.5f ? -1f : 1f;
                z = sign * magnitude;
            }

            bool outsideX = Mathf.Abs(x) > halfMain.x + epsilon * 0.5f;
            bool outsideY = Mathf.Abs(y) > halfMain.y + epsilon * 0.5f;
            bool outsideZ = Mathf.Abs(z) > halfMain.z + epsilon * 0.5f;

            if (!outsideX && !outsideY && !outsideZ)
            {
                continue;
            }

            worldPosition = fieldCenter + new Vector3(x, y, z);
            return true;
        }

        worldPosition = Vector3.zero;
        return false;
    }

    void ClearExistingStars()
    {
        foreach (StarNode node in stars)
        {
            if (node != null)
            {
                Destroy(node.gameObject);
            }
        }

        stars.Clear();
        boundaryStars.Clear();

        ClearAllLines();
        RemoveShootingStars();
        pendingConnection = null;
    }

    void RemoveShootingStars()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child != null && child.GetComponent<ShootingStar>() != null)
            {
                Destroy(child.gameObject);
            }
        }
    }

    public void HandleStarSelected(StarNode node)
    {
        if (node == null)
        {
            return;
        }

        if (pendingConnection == null)
        {
            pendingConnection = node;
            node.SetSelected(true);
            return;
        }

        if (pendingConnection == node)
        {
            pendingConnection.SetSelected(false);
            pendingConnection = null;
            return;
        }

        StarPair connection = new StarPair(pendingConnection, node);
        if (TryRemoveExistingLine(connection))
        {
            pendingConnection.SetSelected(false);
            node.SetSelected(false);
            pendingConnection = null;
            return;
        }

        StarNode previous = pendingConnection;
        CreateLineBetween(previous, node);
        previous.SetSelected(false);

        if (keepLastStarSelectedAfterConnection)
        {
            pendingConnection = node;
            pendingConnection.SetSelected(true);
        }
        else
        {
            node.SetSelected(false);
            pendingConnection = null;
        }
    }

    void TrySelectStarUnderCursor()
    {
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        Camera camera = ResolveSelectionCamera();
        if (camera == null)
        {
            if (!selectionCameraWarningLogged)
            {
                Debug.LogWarning("[StarFieldManager] No camera available for star selection. Assign a camera or tag one as MainCamera.");
                selectionCameraWarningLogged = true;
            }
            return;
        }
        selectionCameraWarningLogged = false;

        Ray ray = camera.ScreenPointToRay(Input.mousePosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, Mathf.Infinity, ~0, QueryTriggerInteraction.Collide);
        if (hits == null || hits.Length == 0)
        {
            return;
        }

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            StarNode node = hit.collider != null
                ? hit.collider.GetComponent<StarNode>()
                : null;
            if (node != null)
            {
                HandleStarSelected(node);
                return;
            }
        }
    }

    Camera ResolveSelectionCamera()
    {
        if (namingCamera != null && namingCamera.enabled)
        {
            return namingCamera;
        }

        if (Camera.main != null && Camera.main.enabled)
        {
            return Camera.main;
        }

        if (Camera.current != null && Camera.current.enabled)
        {
            return Camera.current;
        }

        if (Camera.allCamerasCount > 0)
        {
            Camera[] cameras = Camera.allCameras;
            for (int i = 0; i < cameras.Length; i++)
            {
                Camera candidate = cameras[i];
                if (candidate != null && candidate.enabled && candidate.cameraType == CameraType.Game)
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    bool TryRemoveExistingLine(in StarPair pair)
    {
        if (!pair.IsValid)
        {
            return false;
        }

        if (starConnections.TryGetValue(pair, out GameObject lineObject))
        {
            if (lineObject != null)
            {
                RemoveLine(lineObject);
                return true;
            }

            starConnections.Remove(pair);
        }

        return false;
    }

    void CreateLineBetween(StarNode first, StarNode second)
    {
        if (first == null || second == null)
        {
            return;
        }

        StarPair pair = new StarPair(first, second);
        if (!pair.IsValid || starConnections.ContainsKey(pair))
        {
            return;
        }

        GameObject lineObject = new GameObject($"ConstellationLine_{constellationLines.Count:D3}");
        lineObject.transform.SetParent(transform, false);

        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.positionCount = 2;
        line.useWorldSpace = true;
        line.startWidth = lineWidth;
        line.endWidth = lineWidth;
        line.startColor = lineColor;
        line.endColor = lineColor;
        line.numCapVertices = 4;
        line.numCornerVertices = 2;
        line.alignment = LineAlignment.View;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.generateLightingData = false;

        if (lineMaterial != null)
        {
            line.material = lineMaterial;
        }
        else
        {
            Shader fallback = Shader.Find("Sprites/Default");
            if (fallback != null)
            {
                Material materialInstance = new Material(fallback);
                ApplyLineMaterialColor(materialInstance);
                line.material = materialInstance;
            }
        }

        line.SetPosition(0, first.Position);
        line.SetPosition(1, second.Position);

        GameObject colliderGO = new GameObject("LineCollider");
        colliderGO.transform.SetParent(lineObject.transform, false);
        ConstellationLineHandle handle = colliderGO.AddComponent<ConstellationLineHandle>();
        float effectiveRadius = Mathf.Max(0.01f, lineSelectionRadius + lineSelectionPadding);
        handle.Initialize(this, line, effectiveRadius);

        constellationLines.Add(lineObject);
        starConnections[pair] = lineObject;
        lineToConnection[lineObject] = pair;
    }

    Camera ResolveNamingCamera()
    {
        if (namingCamera != null)
        {
            return namingCamera;
        }

        return Camera.main;
    }

    public void RequestLineNaming(ConstellationLineHandle handle)
    {
        if (handle == null || namingUI == null)
        {
            return;
        }

        Camera camera = ResolveNamingCamera();
        if (camera == null)
        {
            Debug.LogWarning("[StarFieldManager] Naming camera is not assigned and no main camera was found.");
            return;
        }

        Vector3 screenPoint = camera.WorldToScreenPoint(handle.transform.position);
        string currentName = string.Empty;

        if (handle.LineObject != null &&
            lineLabels.TryGetValue(handle.LineObject, out ConstellationLabel existingLabel) &&
            existingLabel != null)
        {
            currentName = existingLabel.CurrentText;
        }

        Vector2 uiPosition = new Vector2(screenPoint.x, screenPoint.y);
        activeNamingHandle = handle;
        pendingOriginalLabelText = currentName;
        namingUI.Show(
            uiPosition,
            currentName,
            result => ApplyLineName(handle, result),
            preview => PreviewLineName(handle, preview),
            () => RestoreLineName(handle, pendingOriginalLabelText));
    }

    void ApplyLineName(ConstellationLineHandle handle, string input)
    {
        if (handle == null)
        {
            return;
        }

        GameObject lineObject = handle.LineObject;
        if (lineObject == null)
        {
            return;
        }

        string trimmed = string.IsNullOrWhiteSpace(input) ? string.Empty : input.Trim();

        if (string.IsNullOrEmpty(trimmed))
        {
            RemoveLabel(lineObject);
            pendingOriginalLabelText = null;
            if (activeNamingHandle == handle)
            {
                activeNamingHandle = null;
            }
            return;
        }

        ConstellationLabel label = GetOrCreateLabel(handle);
        if (label != null)
        {
            label.Configure(new Vector3(0f, labelVerticalOffset, 0f), labelScreenOffset, labelFontSize, labelColor, labelFontAsset);
            label.SetText(trimmed);
        }
        if (activeNamingHandle == handle)
        {
            activeNamingHandle = null;
        }
        pendingOriginalLabelText = null;
    }
    void PreviewLineName(ConstellationLineHandle handle, string input)
    {
        if (handle == null || handle.LineObject == null)
        {
            return;
        }

        if (string.IsNullOrEmpty(input))
        {
            if (lineLabels.TryGetValue(handle.LineObject, out ConstellationLabel existing) && existing != null)
            {
                existing.SetText(string.Empty);
            }
            return;
        }

        ConstellationLabel label = GetOrCreateLabel(handle);
        if (label != null)
        {
            label.Configure(new Vector3(0f, labelVerticalOffset, 0f), labelScreenOffset, labelFontSize, labelColor, labelFontAsset);
            label.SetText(input);
        }
    }

    ConstellationLabel GetOrCreateLabel(ConstellationLineHandle handle)
    {
        if (handle == null)
        {
            return null;
        }

        GameObject lineObject = handle.LineObject;
        if (lineObject == null)
        {
            return null;
        }

        if (lineLabels.TryGetValue(lineObject, out ConstellationLabel existingLabel) && existingLabel != null)
        {
            return existingLabel;
        }

        GameObject labelObject = new GameObject("ConstellationLabel");
        ConstellationLabel label = labelObject.AddComponent<ConstellationLabel>();
        label.Initialize(handle.transform, new Vector3(0f, labelVerticalOffset, 0f), labelScreenOffset, labelFontSize, labelColor, labelFontAsset);
        lineLabels[lineObject] = label;
        return label;
    }

    void RemoveLabel(GameObject lineObject)
    {
        if (lineObject == null)
        {
            return;
        }

        if (lineLabels.TryGetValue(lineObject, out ConstellationLabel existingLabel) && existingLabel != null)
        {
            Destroy(existingLabel.gameObject);
        }

        lineLabels.Remove(lineObject);

        if (activeNamingHandle != null && activeNamingHandle.LineObject == lineObject)
        {
            activeNamingHandle = null;
        }
    }

    void RestoreLineName(ConstellationLineHandle handle, string originalText)
    {
        if (handle == null)
        {
            pendingOriginalLabelText = null;
            return;
        }

        if (string.IsNullOrEmpty(originalText))
        {
            RemoveLabel(handle.LineObject);
        }
        else
        {
            ConstellationLabel label = GetOrCreateLabel(handle);
            if (label != null)
            {
                label.Configure(new Vector3(0f, labelVerticalOffset, 0f), labelScreenOffset, labelFontSize, labelColor, labelFontAsset);
                label.SetText(originalText);
            }
        }

        if (activeNamingHandle == handle)
        {
            activeNamingHandle = null;
        }

        pendingOriginalLabelText = null;
    }

    void RemoveLastLine()
    {
        if (constellationLines.Count == 0)
        {
            return;
        }

        GameObject line = constellationLines[constellationLines.Count - 1];
        RemoveLine(line);
    }

    public void RemoveLine(GameObject lineObject)
    {
        if (lineObject == null)
        {
            return;
        }

        if (lineToConnection.TryGetValue(lineObject, out StarPair pair))
        {
            lineToConnection.Remove(lineObject);
            starConnections.Remove(pair);
        }

        RemoveLabel(lineObject);
        if (activeNamingHandle != null && activeNamingHandle.LineObject == lineObject)
        {
            activeNamingHandle = null;
        }
        constellationLines.Remove(lineObject);
        Destroy(lineObject);
    }

    void ClearAllLines()
    {
        if (constellationLines.Count == 0)
        {
            starConnections.Clear();
            lineToConnection.Clear();
            return;
        }

        var snapshot = new List<GameObject>(constellationLines);
        foreach (GameObject line in snapshot)
        {
            RemoveLine(line);
        }

        constellationLines.Clear();
        starConnections.Clear();
        lineToConnection.Clear();
    }

    readonly struct StarPair : System.IEquatable<StarPair>
    {
        public StarNode First { get; }
        public StarNode Second { get; }
        public bool IsValid => First != null && Second != null;

        readonly int firstId;
        readonly int secondId;

        public StarPair(StarNode a, StarNode b)
        {
            if (a == null || b == null)
            {
                First = null;
                Second = null;
                firstId = 0;
                secondId = 0;
                return;
            }

            int aId = a.GetInstanceID();
            int bId = b.GetInstanceID();

            if (aId <= bId)
            {
                First = a;
                Second = b;
                firstId = aId;
                secondId = bId;
            }
            else
            {
                First = b;
                Second = a;
                firstId = bId;
                secondId = aId;
            }
        }

        public bool Equals(StarPair other)
        {
            return firstId == other.firstId && secondId == other.secondId;
        }

        public override bool Equals(object obj)
        {
            return obj is StarPair other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (firstId * 397) ^ secondId;
            }
        }
    }

    void InitializeLineMaterial()
    {
        if (lineMaterial != null)
        {
            return;
        }

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        }
        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        if (shader != null)
        {
            lineMaterial = new Material(shader);
            ApplyLineMaterialColor(lineMaterial);
        }
        else
        {
            Debug.LogWarning("StarFieldManager: No shader found for constellation lines. Default material will be used.");
        }
    }

    void ApplyLineMaterialColor(Material material)
    {
        if (material == null)
        {
            return;
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", lineColor);
        }

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", lineColor);
        }

        if (material.HasProperty("_TintColor"))
        {
            material.SetColor("_TintColor", lineColor);
        }
    }

    Vector3 ComputeHalfFieldSize()
    {
        return new Vector3(
            Mathf.Max(0.01f, Mathf.Abs(fieldSize.x)) * 0.5f,
            Mathf.Max(0.01f, Mathf.Abs(fieldSize.y)) * 0.5f,
            Mathf.Max(0.01f, Mathf.Abs(fieldSize.z)) * 0.5f);
    }

    void SpawnShootingStar()
    {
        Vector3 half = cachedHalfFieldSize.sqrMagnitude > 0f ? cachedHalfFieldSize : ComputeHalfFieldSize();
        Vector3 start = fieldCenter + new Vector3(
            Random.Range(-half.x * 1.2f, half.x * 1.2f),
            Mathf.Max(half.y, 3f) + shootingStarAltitudeOffset,
            -half.z - 10f);

        Vector3 end = fieldCenter + new Vector3(
            Random.Range(-half.x * 1.2f, half.x * 1.2f),
            fieldCenter.y + Random.Range(-half.y, half.y),
            half.z + 10f);

        Vector3 direction = (end - start).normalized;
        float speed = Random.Range(shootingStarSpeedRange.x, shootingStarSpeedRange.y);
        float travelDistance = Vector3.Distance(start, end);
        float travelTime = travelDistance / Mathf.Max(0.1f, speed);
        float lifetimeMultiplier = Random.Range(shootingStarLifetimeRange.x, shootingStarLifetimeRange.y);
        float lifetime = Mathf.Max(0.1f, travelTime * lifetimeMultiplier);

        GameObject shootingStar = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        shootingStar.name = "ShootingStar";
        shootingStar.transform.SetParent(transform, false);
        shootingStar.transform.position = start;
        float size = Mathf.Lerp(0.1f, 0.25f, Random.value);
        shootingStar.transform.localScale = Vector3.one * size;

        Renderer renderer = shootingStar.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            Material material = new Material(renderer.sharedMaterial)
            {
                color = Color.white
            };
            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", Color.white * 2.5f);
            }
            renderer.material = material;
        }

        Collider collider = shootingStar.GetComponent<Collider>();
        if (collider != null)
        {
            Destroy(collider);
        }

        TrailRenderer trail = shootingStar.AddComponent<TrailRenderer>();
        trail.time = lifetime;
        trail.startWidth = size;
        trail.endWidth = 0f;
        trail.colorGradient = shootingStarGradient;
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        trail.receiveShadows = false;

        Shader trailShader = Shader.Find("Sprites/Default");
        if (trailShader == null)
        {
            trailShader = Shader.Find("Unlit/Color");
        }
        if (trailShader != null)
        {
            trail.material = new Material(trailShader)
            {
                color = Color.white
            };
        }

        ShootingStar behaviour = shootingStar.AddComponent<ShootingStar>();
        behaviour.Initialize(direction * speed, lifetime);
    }

    void ResetShootingStarTimer()
    {
        if (!enableShootingStars)
        {
            shootingStarTimer = 0f;
            return;
        }

        float minInterval = Mathf.Max(0.5f, Mathf.Min(shootingStarIntervalRange.x, shootingStarIntervalRange.y));
        float maxInterval = Mathf.Max(minInterval, Mathf.Max(shootingStarIntervalRange.x, shootingStarIntervalRange.y));
        shootingStarTimer = Random.Range(minInterval, maxInterval);
    }

    void EnsureGradient()
    {
        if (shootingStarGradient == null || shootingStarGradient.colorKeys.Length == 0)
        {
            shootingStarGradient = new Gradient();
            shootingStarGradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(new Color(0.6f, 0.8f, 1f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0f, 1f)
                });
        }
    }

    void OnValidate()
    {
        int minRangeValue = Mathf.Max(1, randomStarCountRange.x);
        int maxRangeValue = Mathf.Max(1, randomStarCountRange.y);

        if (maxRangeValue < minRangeValue)
        {
            int temp = minRangeValue;
            minRangeValue = maxRangeValue;
            maxRangeValue = temp;
        }

        randomStarCountRange = new Vector2Int(minRangeValue, maxRangeValue);
        starCount = Mathf.Max(1, starCount);

        shootingStarSpeedRange.x = Mathf.Max(0.1f, Mathf.Min(shootingStarSpeedRange.x, shootingStarSpeedRange.y));
        shootingStarSpeedRange.y = Mathf.Max(shootingStarSpeedRange.x, shootingStarSpeedRange.y);
        shootingStarLifetimeRange.x = Mathf.Max(0.1f, Mathf.Min(shootingStarLifetimeRange.x, shootingStarLifetimeRange.y));
        shootingStarLifetimeRange.y = Mathf.Max(shootingStarLifetimeRange.x, shootingStarLifetimeRange.y);
        selectionColliderPadding = Mathf.Max(0f, selectionColliderPadding);
        lineSelectionRadius = Mathf.Max(0.01f, lineSelectionRadius);
        lineSelectionPadding = Mathf.Max(0f, lineSelectionPadding);
        boundaryPadding = Mathf.Max(0f, boundaryPadding);
        boundaryVerticalPadding = Mathf.Max(0f, boundaryVerticalPadding);
        boundaryStarDensity = Mathf.Clamp(boundaryStarDensity, 0f, 10f);
        baseEmissionIntensity = Mathf.Max(0f, baseEmissionIntensity);
        selectedEmissionIntensity = Mathf.Max(baseEmissionIntensity, selectedEmissionIntensity);
        selectionPulseScale = Mathf.Clamp(selectionPulseScale, 0f, 1.5f);
        selectionPulseSpeed = Mathf.Max(0.1f, selectionPulseSpeed);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.4f, 0.6f, 0.4f);
        Vector3 half = ComputeHalfFieldSize();
        Gizmos.DrawWireCube(fieldCenter, half * 2f);
    }
#endif
}

