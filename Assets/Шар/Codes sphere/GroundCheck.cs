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

    [Tooltip("Доля радиуса шара, на которой пускаются боковые лучи (0.5–0.6 не даёт шару зависать на самом краю обрыва).")]
    [SerializeField, Range(0.2f, 0.9f)] private float perimeterRayScale = 0.55f;

    [Header("Отладка")]
    [SerializeField] private bool drawDebugRays = false;

    public bool IsGrounded { get; private set; }
    public bool IsCenterOverGround { get; private set; }
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

        if (drawDebugRays)
            Debug.DrawRay(origin, Vector3.down * length, found ? Color.green : Color.red);

        return found;
    }

    private bool CheckGround()
    {
        GroundNormal = Vector3.up;
        GroundAngle = 0f;
        GroundCollider = null;
        IsCenterOverGround = false;

        float rayLength = checkRadius + rayLengthExtra;
        int hitCount = 0;
        Vector3 averageNormal = Vector3.zero;
        Vector3 averageHitPoint = Vector3.zero;

        // 1. Центральный луч (строго вниз под центром масс шара)
        if (RaycastGround(transform.position, rayLength, out RaycastHit centerHit))
        {
            if (IsValidGround(centerHit.normal))
            {
                IsCenterOverGround = true;
                hitCount++;
                averageNormal += centerHit.normal;
                averageHitPoint += centerHit.point;
                GroundCollider = centerHit.collider;
            }
        }

        // 2. Лучи по кругу (уменьшенный радиус, чтобы шар не цеплялся за пустоту)
        float ringRadius = checkRadius * perimeterRayScale;
        for (int i = 0; i < rayCount; i++)
        {
            float angle = i * (360f / rayCount);
            Vector3 direction = Quaternion.Euler(0, angle, 0) * Vector3.forward;
            Vector3 rayOrigin = transform.position + direction * ringRadius;
            if (RaycastGround(rayOrigin, rayLength, out RaycastHit hit))
            {
                if (IsValidGround(hit.normal))
                {
                    hitCount++;
                    averageNormal += hit.normal;
                    averageHitPoint += hit.point;
                    if (GroundCollider == null) GroundCollider = hit.collider;
                }
            }
        }

        if (hitCount > 0)
        {
            Vector3 surfaceNormal = (averageNormal / hitCount).normalized;

            // Если центр шара уже вышел за обрыв, смещаем нормаль в сторону падения с края
            if (!IsCenterOverGround)
            {
                Vector3 meanPoint = averageHitPoint / hitCount;
                Vector3 edgeOutDir = Vector3.ProjectOnPlane(transform.position - meanPoint, Vector3.up);
                if (edgeOutDir.sqrMagnitude > 0.0001f)
                {
                    surfaceNormal = (surfaceNormal + edgeOutDir.normalized * 1.1f).normalized;
                }
            }

            GroundNormal = surfaceNormal;
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