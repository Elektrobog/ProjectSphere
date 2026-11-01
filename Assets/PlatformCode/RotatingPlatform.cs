using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class RotatingPlatform : MonoBehaviour, IPlatformVelocityProvider
{
    [Header("Rotation Settings")]
    public Transform rotationCenter;
    [Tooltip("Ось орбиты вокруг центра. Для плоскости XZ — (0,1,0), для вертикальной XY — (0,0,1). Спавнер выставляет автоматически по выбранной плоскости.")]
    public Vector3 rotationAxis = Vector3.up;
    [Tooltip("Платформа НЕ вращается вместе с орбитой — всегда держит свой поворот, как кабинка колеса обозрения. Выключено — платформа поворачивается вместе с орбитой (носом от центра, как лопасть). Спавнер выставляет автоматически.")]
    public bool keepOrientation = false;
    public float angularSpeed = 30f;
    public bool clockwise = true;

    private Rigidbody rb;
    private Vector3 initialLocalOffset;
    private Quaternion initialRotation;
    private float accumulatedAngle = 0f;
    private bool isInitialized;

    private float AngularSpeedRad => angularSpeed * Mathf.Deg2Rad;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;

        if (rotationCenter != null)
            Initialize(rotationCenter);
    }

    public void Initialize(Transform center)
    {
        if (center == null) return;
        rotationCenter = center;
        initialLocalOffset = transform.position - rotationCenter.position;
        initialRotation = transform.rotation;
        accumulatedAngle = 0f;
        isInitialized = true;
    }

    private void FixedUpdate()
    {
        if (!isInitialized || rotationCenter == null) return;

        float direction = clockwise ? -1f : 1f;
        float deltaAngle = angularSpeed * direction * Time.fixedDeltaTime;
        accumulatedAngle += deltaAngle;

        Quaternion orbit = Quaternion.AngleAxis(accumulatedAngle, rotationAxis.normalized);
        Vector3 rotatedOffset = orbit * initialLocalOffset;
        rb.MovePosition(rotationCenter.position + rotatedOffset);
        rb.MoveRotation(keepOrientation ? initialRotation : orbit * initialRotation);
    }

    public Vector3 GetVelocityAtPoint(Vector3 worldPoint)
    {
        if (rotationCenter == null) return Vector3.zero;
        Vector3 radius = worldPoint - rotationCenter.position;
        float omega = AngularSpeedRad * (clockwise ? -1f : 1f);
        return Vector3.Cross(rotationAxis.normalized * omega, radius);
    }

    public Vector3 GetAngularVelocity()
    {
        float omega = AngularSpeedRad * (clockwise ? -1f : 1f);
        return rotationAxis.normalized * omega;
    }
}
