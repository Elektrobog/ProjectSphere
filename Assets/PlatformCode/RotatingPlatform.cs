using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class RotatingPlatform : MonoBehaviour, IPlatformVelocityProvider
{
    [Header("Rotation Settings")]
    public Transform rotationCenter;
    public float angularSpeed = 30f;
    public bool clockwise = true;

    [Header("Orientation")]
    [Tooltip("Включено: платформа облетает центр, НЕ вращаясь вокруг своей оси (гондола). Выключено: платформа ещё и крутится сама (старое поведение).")]
    public bool keepOrientation = true;

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

        Quaternion orbit = Quaternion.Euler(0f, accumulatedAngle, 0f);
        Vector3 rotatedOffset = orbit * initialLocalOffset;
        rb.MovePosition(rotationCenter.position + rotatedOffset);

        // Гондола: ориентация платформы не меняется, только позиция по орбите.
        // Старое поведение (самовращение) — если keepOrientation выключен.
        Quaternion targetRotation = keepOrientation ? initialRotation : orbit * initialRotation;
        rb.MoveRotation(targetRotation);
    }

    public Vector3 GetVelocityAtPoint(Vector3 worldPoint)
    {
        if (rotationCenter == null) return Vector3.zero;
        Vector3 radius = worldPoint - rotationCenter.position;
        float omega = AngularSpeedRad * (clockwise ? -1f : 1f);
        return Vector3.Cross(new Vector3(0f, omega, 0f), radius);
    }

    public Vector3 GetAngularVelocity()
    {
        if (keepOrientation) return Vector3.zero;
        float omega = AngularSpeedRad * (clockwise ? -1f : 1f);
        return new Vector3(0f, omega, 0f);
    }
}
