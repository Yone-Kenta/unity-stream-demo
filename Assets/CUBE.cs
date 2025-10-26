using UnityEngine;

public class A : MonoBehaviour
{
    [Header("Spawn Settings")]
    [SerializeField] Vector3 spawnAreaCenter = Vector3.zero;
    [SerializeField] Vector3 spawnAreaSize = new Vector3(10f, 5f, 10f);
    [SerializeField] float spawnIntervalSeconds = 1.5f;
    [SerializeField] int maxSphereCount = 50;

    [Header("Sphere Properties")]
    [SerializeField] Vector2 radiusRange = new Vector2(0.4f, 1.2f);
    [SerializeField] Vector2 massRange = new Vector2(0.5f, 3f);
    [SerializeField] bool randomizeColor = true;

    float spawnTimer;
    int spawnedCount;

    void Update()
    {
        if (maxSphereCount > 0 && spawnedCount >= maxSphereCount)
        {
            return;
        }

        spawnTimer += Time.deltaTime;
        float interval = Mathf.Max(0.001f, spawnIntervalSeconds);
        if (spawnTimer < interval)
        {
            return;
        }

        spawnTimer -= interval;
        SpawnRandomSphere();
    }

    void SpawnRandomSphere()
    {
        Vector3 halfSize = new Vector3(
            Mathf.Max(0.01f, Mathf.Abs(spawnAreaSize.x)) * 0.5f,
            Mathf.Max(0.01f, Mathf.Abs(spawnAreaSize.y)) * 0.5f,
            Mathf.Max(0.01f, Mathf.Abs(spawnAreaSize.z)) * 0.5f);

        Vector3 spawnPosition = spawnAreaCenter + new Vector3(
            Random.Range(-halfSize.x, halfSize.x),
            Random.Range(-halfSize.y, halfSize.y),
            Random.Range(-halfSize.z, halfSize.z));

        float minRadius = Mathf.Max(0.01f, Mathf.Min(radiusRange.x, radiusRange.y));
        float maxRadius = Mathf.Max(minRadius, Mathf.Max(radiusRange.x, radiusRange.y));
        float radius = Random.Range(minRadius, maxRadius);

        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.transform.position = spawnPosition;
        sphere.transform.localScale = Vector3.one * (radius * 2f);
        sphere.name = $"RandomSphere_{spawnedCount:D3}";

        Rigidbody body = sphere.GetComponent<Rigidbody>();
        if (body == null)
        {
            body = sphere.AddComponent<Rigidbody>();
        }

        float minMass = Mathf.Max(0.01f, Mathf.Min(massRange.x, massRange.y));
        float maxMass = Mathf.Max(minMass, Mathf.Max(massRange.x, massRange.y));
        body.mass = Random.Range(minMass, maxMass);
        body.useGravity = true;

        if (randomizeColor && sphere.TryGetComponent<Renderer>(out Renderer renderer))
        {
            Material materialInstance = new Material(renderer.sharedMaterial);
            materialInstance.color = Random.ColorHSV(0f, 1f, 0.6f, 1f, 0.5f, 1f);
            renderer.material = materialInstance;
        }

        spawnedCount++;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(spawnAreaCenter, new Vector3(
            Mathf.Max(0.01f, Mathf.Abs(spawnAreaSize.x)),
            Mathf.Max(0.01f, Mathf.Abs(spawnAreaSize.y)),
            Mathf.Max(0.01f, Mathf.Abs(spawnAreaSize.z))));
    }
#endif
}

