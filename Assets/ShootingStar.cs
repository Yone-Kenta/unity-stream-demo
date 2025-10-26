using UnityEngine;

public class ShootingStar : MonoBehaviour
{
    Vector3 velocity;
    float lifetime;
    float elapsed;

    public void Initialize(Vector3 initialVelocity, float lifeTimeSeconds)
    {
        velocity = initialVelocity;
        lifetime = Mathf.Max(0.1f, lifeTimeSeconds);
    }

    void Update()
    {
        transform.position += velocity * Time.deltaTime;
        elapsed += Time.deltaTime;

        if (elapsed >= lifetime)
        {
            Destroy(gameObject);
        }
    }
}
