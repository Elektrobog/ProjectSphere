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

    private float ungroundUntil;
    private readonly RaycastHit[] hitBuffer = new RaycastHit[8];

    void FixedUpdate()
    {
        bool wasGrounded = IsGrounded;

        if (Time.time < ungroundUntil)
        {
            // Блокировка после прыжка: пока шар ещё в пределах длины лучей, не считаем его заземлённым
            ForceUnground();
            IsCenterOverGround = false;
        }
        else
        {
            IsGrounded = CheckGround();
        }

        JustLanded = IsGrounded && !wasGrounded;
        if (JustLanded) LastGroundedTime = Time.time;
    }

    /// <summary>Принудительно «отрывает» шар от земли на заданное время (вызывается при прыжке).</summary>
    public void LockUnground(float seconds)
    {
        ungroundUntil = Time.time + Mathf.Max(0f, seconds);
        ForceUnground();
        IsCenterOverGround = false;
    }

    // Ближайшая поверхность под лучом. Триггеры игнорируются (FallZone, кнопки, стартовые зоны),
    // собственные коллайдеры шара (включая дочерние пластины) — тоже.
    private bool RaycastGround(Vector3 origin, float length, out RaycastHit best)
    {
        best = default;
        int count = Physics.RaycastNonAlloc(origin, Vector3.down, hitBuffer, length, groundLayer,
            QueryTriggerInteraction.Ignore);

        float bestDist = float.MaxValue;
        bool found = false;
        for (int i = 0; i < count; i++)
        {
            RaycastHit h = hitBuffer[i];
            if (h.collider == null || h.collider.transform.IsChildOf(transform)) continue;
            if (h.distance < bestDist)
            {
                bestDist = h.distance;
                best = h;
                found = true;
            }
        }
        return found;
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
        if (RaycastGround(transform.position, rayLength, out RaycastHit centerHit))
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
            if (RaycastGround(rayOrigin, rayLength, out RaycastHit hit))
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