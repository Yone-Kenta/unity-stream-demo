using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class CameraGridNavigator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] StarFieldManager starFieldManager;
    [SerializeField] bool autoResolveManager = true;

    [Header("Grid Settings")]
    [SerializeField, Min(1)] int gridDivisionsX = 3;
    [SerializeField, Min(1)] int gridDivisionsY = 1;
    [SerializeField, Min(1)] int gridDivisionsZ = 3;
    [SerializeField] bool loopGridPositions = true;

    [Header("Input")]
    [SerializeField] KeyCode advanceKey = KeyCode.M;

    [Header("Placement")]
    [SerializeField] bool preserveOriginalY = true;
    [SerializeField] float yOffset = 0f;
    [SerializeField] Vector3 additionalOffset = Vector3.zero;

    readonly List<Vector3> gridCenters = new List<Vector3>();
    Vector3 cachedCenter = Vector3.zero;
    Vector3 cachedSize = Vector3.zero;
    Vector3Int cachedDivisions = Vector3Int.zero;
    int nextIndex;
    float initialY;

    void Awake()
    {
        initialY = transform.position.y;
    }

    void OnEnable()
    {
        initialY = transform.position.y;
    }

    void OnValidate()
    {
        gridDivisionsX = Mathf.Max(1, gridDivisionsX);
        gridDivisionsY = Mathf.Max(1, gridDivisionsY);
        gridDivisionsZ = Mathf.Max(1, gridDivisionsZ);
    }

    void Update()
    {
        if (Input.GetKeyDown(advanceKey))
        {
            AdvanceToNextPosition();
        }
    }

    public void AdvanceToNextPosition()
    {
        if (autoResolveManager)
        {
            if (starFieldManager == null)
            {
                starFieldManager = FindObjectOfType<StarFieldManager>();
            }
        }

        if (starFieldManager == null)
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

        Vector3 target = gridCenters[currentIndex] + additionalOffset;
        float finalY = preserveOriginalY ? initialY : target.y;
        finalY += yOffset;
        target.y = finalY;

        transform.position = target;

        nextIndex = loopGridPositions
            ? (currentIndex + 1) % gridCenters.Count
            : Mathf.Min(currentIndex + 1, gridCenters.Count);
    }

    public void ResetTraversal()
    {
        nextIndex = 0;
    }

    void EnsureGridCenters()
    {
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
}
