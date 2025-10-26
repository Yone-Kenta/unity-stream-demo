using UnityEngine;

public class OrbitingCamera : MonoBehaviour
{
    [Header("Fixed Transform")]
    [SerializeField] Vector3 fixedPosition = new Vector3(0f, -150f, 0f);
    [SerializeField] float fixedXRotation = -90f;
    [SerializeField] float fixedZRotation = 0f;

    [Header("Rotation")]
    [SerializeField] float rotationSpeedDegreesPerSecond = 1f;
    [SerializeField] float initialYRotation = 0f;

    float currentYRotation;

    void Awake()
    {
        currentYRotation = initialYRotation;
        ApplyTransform();
    }

    void LateUpdate()
    {
        currentYRotation = Mathf.Repeat(
            currentYRotation + rotationSpeedDegreesPerSecond * Time.deltaTime,
            360f);
        ApplyTransform();
    }

    void ApplyTransform()
    {
        transform.position = fixedPosition;
        transform.rotation = Quaternion.Euler(fixedXRotation, currentYRotation, fixedZRotation);
    }
}
