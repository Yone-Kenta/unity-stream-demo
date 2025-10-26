using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class OrbitingGridCamera : MonoBehaviour
{
    [Header("References")]
    [SerializeField] StarFieldManager starFieldManager;
    [SerializeField] bool autoResolveManager = true;

    [Header("Grid Settings")]
    [SerializeField, Min(1)] int gridDivisionsX = 3;
    [SerializeField, Min(1)] int gridDivisionsY = 1;
    [SerializeField, Min(1)] int gridDivisionsZ = 3;
    [SerializeField] bool loopGridPositions = true;
    [SerializeField] bool snapToFirstGridOnStart = true;

    [Header("Input")]
    [SerializeField] KeyCode advanceKey = KeyCode.M;

    [Header("Placement")]
    [SerializeField] bool preserveOriginalY = true;
    [SerializeField] float yOffset = 0f;
    [SerializeField] Vector3 additionalOffset = Vector3.zero;

    [Header("Linked Camera")]
    [SerializeField] Transform linkedGameCamera;
    [SerializeField] bool autoResolveLinkedCamera = true;
    [SerializeField] Vector3 linkedCameraPositionOffset = Vector3.zero;
    [SerializeField] bool syncLinkedCameraRotation = true;
    [SerializeField] Vector3 linkedCameraEulerOffset = Vector3.zero;

    [Header("Orbiting")]
    [SerializeField] bool enableOrbiting = true;
    [SerializeField] float rotationSpeedDegreesPerSecond = 1f;
    [SerializeField] float initialYRotation = 0f;
    [SerializeField] float fixedXRotation = -90f;
    [SerializeField] float fixedZRotation = 0f;

    readonly List<Vector3> gridCenters = new List<Vector3>();
    Vector3 cachedCenter = Vector3.zero;
    Vector3 cachedSize = Vector3.zero;
    Vector3Int cachedDivisions = Vector3Int.zero;

    int nextIndex;
    float initialY;
    float currentYRotation;
    Vector3 currentTargetPosition;
    bool hasTarget;

    void Awake()
    {
        initialY = transform.position.y;
        currentYRotation = initialYRotation;
        currentTargetPosition = transform.position;
        hasTarget = true;
        ResolveLinkedCamera();
        ApplyTransform();
    }

    void OnEnable()
    {
        initialY = transform.position.y;
        currentYRotation = initialYRotation;
        if (!hasTarget)
        {
            currentTargetPosition = transform.position;
            hasTarget = true;
        }
        ResolveLinkedCamera();
    }

    void Start()
    {
        if (snapToFirstGridOnStart)
        {
            SnapToCurrentGridPosition();
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(advanceKey))
        {
            AdvanceToNextPosition();
        }

        if (enableOrbiting)
        {
            currentYRotation = Mathf.Repeat(
                currentYRotation + rotationSpeedDegreesPerSecond * Time.deltaTime,
                360f);
        }

        ApplyTransform();
    }

    public void AdvanceToNextPosition()
    {
        if (!EnsureStarFieldManager())
        {
            return;
        }

        EnsureGridCenters();

        if (gridCenters.Count == 0)
        {
            return;
        }

        int currentIndex = loopGridPositions
            ? nextIndex % gridCenters.Count
            : Mathf.Min(nextIndex, gridCenters.Count - 1);

        Vector3 rawTarget = gridCenters[currentIndex];
        currentTargetPosition = ApplyPlacement(rawTarget);
        hasTarget = true;

        nextIndex = loopGridPositions
            ? (currentIndex + 1) % gridCenters.Count
            : Mathf.Min(currentIndex + 1, gridCenters.Count);
    }

    public void SnapToCurrentGridPosition()
    {
        if (!EnsureStarFieldManager())
        {
            currentTargetPosition = transform.position;
            hasTarget = true;
            return;
        }

        EnsureGridCenters();

        if (gridCenters.Count == 0)
        {
            currentTargetPosition = transform.position;
            hasTarget = true;
            return;
        }

        int currentIndex = loopGridPositions
            ? nextIndex % gridCenters.Count
            : Mathf.Min(nextIndex, gridCenters.Count - 1);

        Vector3 rawTarget = gridCenters[currentIndex];
        currentTargetPosition = ApplyPlacement(rawTarget);
        hasTarget = true;

        nextIndex = loopGridPositions
            ? (currentIndex + 1) % gridCenters.Count
            : Mathf.Min(currentIndex + 1, gridCenters.Count);

        ApplyTransform();
    }

    public void ResetTraversal()
    {
        nextIndex = 0;
        SnapToCurrentGridPosition();
    }

    Vector3 ApplyPlacement(Vector3 target)
    {
        float finalY = preserveOriginalY ? initialY : target.y;
        finalY += yOffset;
        target.y = finalY;
        return target + additionalOffset;
    }

    bool EnsureStarFieldManager()
    {
        if (starFieldManager != null)
        {
            return true;
        }

        if (!autoResolveManager)
        {
            return false;
        }

        starFieldManager = FindObjectOfType<StarFieldManager>();
        return starFieldManager != null;
    }

    void EnsureGridCenters()
    {
        if (starFieldManager == null)
        {
            return;
        }

        Vector3 managerCenter = starFieldManager.FieldCenter;
        Vector3 managerSize = starFieldManager.FieldSize;
        Vector3Int divisions = new Vector3Int(gridDivisionsX, gridDivisionsY, gridDivisionsZ);

        if (gridCenters.Count == 0 ||
            cachedCenter != managerCenter ||
            cachedSize != managerSize ||
            cachedDivisions != divisions)
        {
            RebuildGridCenters(managerCenter, managerSize, divisions);
        }
    }

    void RebuildGridCenters(Vector3 center, Vector3 size, Vector3Int divisions)
    {
        gridCenters.Clear();

        Vector3 absSize = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
        Vector3 halfSize = absSize * 0.5f;
        Vector3 step = new Vector3(
            divisions.x > 0 ? absSize.x / divisions.x : 0f,
            divisions.y > 0 ? absSize.y / divisions.y : 0f,
            divisions.z > 0 ? absSize.z / divisions.z : 0f);

        Vector3 origin = center - halfSize;

        for (int y = 0; y < divisions.y; y++)
        {
            float yCoord = origin.y + (y + 0.5f) * step.y;
            for (int z = 0; z < divisions.z; z++)
            {
                float zCoord = origin.z + (z + 0.5f) * step.z;
                for (int x = 0; x < divisions.x; x++)
                {
                    float xCoord = origin.x + (x + 0.5f) * step.x;
                    gridCenters.Add(new Vector3(xCoord, yCoord, zCoord));
                }
            }
        }

        if (gridCenters.Count == 0)
        {
            gridCenters.Add(center);
        }

        cachedCenter = center;
        cachedSize = size;
        cachedDivisions = divisions;
    }

    void ApplyTransform()
    {
        if (hasTarget)
        {
            transform.position = currentTargetPosition;
        }

        Quaternion baseRotation = Quaternion.Euler(fixedXRotation, currentYRotation, fixedZRotation);
        transform.rotation = baseRotation;
        ApplyLinkedCameraTransform(baseRotation);
    }

    void OnValidate()
    {
        gridDivisionsX = Mathf.Max(1, gridDivisionsX);
        gridDivisionsY = Mathf.Max(1, gridDivisionsY);
        gridDivisionsZ = Mathf.Max(1, gridDivisionsZ);
    }

    void ResolveLinkedCamera()
    {
        if (linkedGameCamera != null)
        {
            return;
        }

        if (!autoResolveLinkedCamera)
        {
            return;
        }

        Camera mainCamera = Camera.main;
        if (mainCamera != null && mainCamera.transform != transform)
        {
            linkedGameCamera = mainCamera.transform;
        }
    }

    void ApplyLinkedCameraTransform(Quaternion baseRotation)
    {
        if (linkedGameCamera == null)
        {
            return;
        }

        linkedGameCamera.position = currentTargetPosition + linkedCameraPositionOffset;
        if (syncLinkedCameraRotation)
        {
            linkedGameCamera.rotation = baseRotation * Quaternion.Euler(linkedCameraEulerOffset);
        }
    }
}
