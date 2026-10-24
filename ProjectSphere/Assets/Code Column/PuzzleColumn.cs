using UnityEngine;

public class PuzzleColumn : MonoBehaviour
{
    [Header("Rotation Settings")]
    public float rotationAngle = 90f;       // угол поворота при событии
    public float rotationSpeed = 90f;       // скорость вращения (град/сек)

    [Header("Reset Settings")]
    public Vector3 resetEulerAngles;        // целевое вращение для сброса (в градусах)

    private Quaternion targetRotation;
    private bool isRotating = false;

    void Start()
    {
        targetRotation = transform.rotation;
    }

    void Update()
    {
        if (isRotating)
        {
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                rotationSpeed * Time.deltaTime
            );

            if (Quaternion.Angle(transform.rotation, targetRotation) < 0.1f)
            {
                transform.rotation = targetRotation;
                isRotating = false;
            }
        }
    }

    // Событие: повернуть колонну на rotationAngle
    public void RotateColumn()
    {
        if (!isRotating)
        {
            targetRotation = transform.rotation * Quaternion.Euler(0f, rotationAngle, 0f);
            isRotating = true;
        }
    }

    // Событие: вернуть колонну в заданное вращение
    public void ResetColumn()
    {
        if (!isRotating)
        {
            targetRotation = Quaternion.Euler(resetEulerAngles);
            isRotating = true;
        }
    }
}