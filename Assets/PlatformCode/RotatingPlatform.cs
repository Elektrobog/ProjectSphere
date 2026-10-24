using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class RotatingPlatform : MonoBehaviour, IPlatformVelocityProvider
{
    [Header("Rotation Settings")]
    public Transform rotationCenter;
    public float angularSpeed = 30f;
    public bool clockwise = true;

    private Rigidbody rb;
    private Vector3 initialLocalOffset;
    private Quaternion initialRotation;
    private float accumulatedAngle = 0f;
    private float angularSpeedRad;
    private bool isInitialized;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        angularSpeedRad = angularSpeed * Mathf.Deg2Rad;

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

        Vector3 rotatedOffset = Quaternion.Euler(0f, accumulatedAngle, 0f) * initialLocalOffset;
        rb.MovePosition(rotationCenter.position + rotatedOffset);
        rb.MoveRotation(Quaternion.Euler(0f, accumulatedAngle, 0f) * initialRotation);
    }

    public Vector3 GetVelocityAtPoint(Vector3 worldPoint)
    {
        if (rotationCenter == null) return Vector3.zero;
        Vector3 radius = worldPoint - rotationCenter.position;
        float omega = angularSpeedRad * (clockwise ? -1f : 1f);
        return Vector3.Cross(new Vector3(0f, omega, 0f), radius);
    }

    public Vector3 GetAngularVelocity()
    {
        float omega = angularSpeedRad * (clockwise ? -1f : 1f);
        return new Vector3(0f, omega, 0f);
    }
}