using UnityEngine;

[DefaultExecutionOrder(-100)]
public class GroundCheck : MonoBehaviour
{
    [Header("Настройки проверки")]
    [SerializeField] private LayerMask groundLayer = -1;
    [SerializeField] public float checkRadius = 0.5f;
    [SerializeField] private float maxGroundAngle = 60f;
    [SerializeField] private float rayLengthExtra = 0.2f;
    [SerializeField] private int rayCount = 8;
    [SerializeField] private float steepAngleThreshold = 30f;

    [Header("Отладка")]
    [SerializeField] private bool drawDebugRays = false;

    public bool IsGrounded { get; private set; }
    public bool IsCenterOverGround { get; private set; } // ✅ НОВОЕ
    public bool JustLanded { get; private set; }
    public Vector3 GroundNormal { get; private set; }
    public float GroundAngle { get; private set; }
    public Collider GroundCollider { get; private set; }
    public float LastGroundedTime { get; private set; }
    public float MaxGroundAngle => maxGroundAngle;

    void FixedUpdate()
    {
        bool wasGrounded = IsGrounded;
        IsGrounded = CheckGround();
        JustLanded = IsGrounded && !wasGrounded;
        if (JustLanded) LastGroundedTime = Time.time;
    }

    private bool CheckGround()
    {
        GroundNormal = Vector3.up;
        GroundAngle = 0f;
        GroundCollider = null;
        IsCenterOverGround = false; // Сбрасываем

        float rayLength = checkRadius + rayLengthExtra;
        int hitCount = 0;
        Vector3 averageNormal = Vector3.zero;

        // 1. Центральный луч (строго вниз)
        if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit centerHit, rayLength, groundLayer))
        {
            if (IsValidGround(centerHit.normal))
            {
                IsCenterOverGround = true; // ✅ Если луч попал — центр на земле
                hitCount++;
                averageNormal += centerHit.normal;
                GroundCollider = centerHit.collider;
            }
        }

        // 2. Лучи по кругу
        for (int i = 0; i < rayCount; i++)
        {
            float angle = i * (360f / rayCount);
            Vector3 direction = Quaternion.Euler(0, angle, 0) * Vector3.forward;
            Vector3 rayOrigin = transform.position + direction * checkRadius * 0.9f;
            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, rayLength, groundLayer))
            {
                if (IsValidGround(hit.normal))
                {
                    hitCount++;
                    averageNormal += hit.normal;
                    if (GroundCollider == null) GroundCollider = hit.collider;
                }
            }
        }

        if (hitCount > 0)
        {
            GroundNormal = (averageNormal / hitCount).normalized;
            GroundAngle = Vector3.Angle(Vector3.up, GroundNormal);
            return GroundAngle <= maxGroundAngle;
        }
        return false;
    }

    private bool IsValidGround(Vector3 normal)
    {
        return Vector3.Angle(Vector3.up, normal) <= maxGroundAngle;
    }

    public bool IsSteepSlope() => IsGrounded && GroundAngle > steepAngleThreshold;
    public bool IsOnWall() => !IsGrounded || GroundAngle > maxGroundAngle;

    public void ForceUnground()
    {
        IsGrounded = false;
        JustLanded = false;
        GroundCollider = null;
        GroundNormal = Vector3.up;
        GroundAngle = 0f;
    }
}