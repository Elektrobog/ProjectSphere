using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class RotatingColumn : MonoBehaviour
{
    [Header("Rotation Settings")]
    public float rotationSpeed = 30f;       // скорость вращения в градусах/сек
    public Vector3 rotationAxis = Vector3.up; // ось вращения
    public bool reverse = false;            // реверс направления

    private Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;   // обязательно кинематик
        rb.useGravity = false;   // колонна не должна падать
    }

    void FixedUpdate()
    {
        float dir = reverse ? -1f : 1f;
        Quaternion delta = Quaternion.AngleAxis(rotationSpeed * dir * Time.fixedDeltaTime, rotationAxis.normalized);
        rb.MoveRotation(rb.rotation * delta);
    }
}
