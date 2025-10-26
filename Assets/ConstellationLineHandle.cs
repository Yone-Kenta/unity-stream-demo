using UnityEngine;

[RequireComponent(typeof(CapsuleCollider))]
public class ConstellationLineHandle : MonoBehaviour
{
    StarFieldManager manager;
    LineRenderer lineRenderer;
    CapsuleCollider capsuleCollider;
    float colliderRadius;
    GameObject lineOwner;

    public void Initialize(StarFieldManager owner, LineRenderer line, float radius)
    {
        manager = owner;
        lineRenderer = line;
        lineOwner = line != null ? line.gameObject : null;
        colliderRadius = Mathf.Max(0.01f, radius);
        capsuleCollider = GetComponent<CapsuleCollider>();
        capsuleCollider.isTrigger = true;
        capsuleCollider.direction = 2; // Z axis
        capsuleCollider.radius = colliderRadius;
        capsuleCollider.center = Vector3.zero;
    }

    void LateUpdate()
    {
        if (lineRenderer == null || lineRenderer.positionCount < 2)
        {
            return;
        }

        Vector3 start = lineRenderer.GetPosition(0);
        Vector3 end = lineRenderer.GetPosition(1);
        Vector3 direction = end - start;
        float length = direction.magnitude;

        if (length < 0.001f)
        {
            length = 0.001f;
            direction = Vector3.forward;
        }

        transform.position = (start + end) * 0.5f;
        transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        capsuleCollider.height = length + colliderRadius * 2f;
    }

    void OnMouseOver()
    {
        if (Input.GetMouseButtonDown(1))
        {
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            {
                if (lineOwner != null)
                {
                    manager?.RemoveLine(lineOwner);
                }
            }
            else
            {
                manager?.RequestLineNaming(this);
            }
        }
    }

    public GameObject LineObject => lineOwner;
    public LineRenderer LineRenderer => lineRenderer;
}
