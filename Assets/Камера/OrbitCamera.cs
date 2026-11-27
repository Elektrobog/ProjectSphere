using UnityEngine;
using UnityEngine.InputSystem;

public class OrbitCamera : MonoBehaviour
{
    [Header("Цель и дистанция")]
    public Transform target;
    public float distance = 5f;
    public float minDistance = 1.0f;
    public float heightOffset = 2f;

    [Header("Защита от влетания в шар")]
    [Tooltip("Радиус шара (определяется автоматически по SphereCollider, если он есть).")]
    public float ballRadius = 0.5f;
    [Tooltip("Минимальный зазор от поверхности шара до ближней плоскости камеры (м).")]
    [Min(0.05f)] public float minBallClearance = 0.35f;
    [Tooltip("Максимальное отставание плавного слежения от реального центра шара (м).")]
    [Min(0.05f)] public float maxPivotLag = 0.35f;

    [Header("Чувствительность")]
    [Range(0.1f, 10f)] public float xSpeed = 5f;
    [Range(0.1f, 10f)] public float ySpeed = 5f;
    public bool invertY = false;

    public float yMinLimit = -20f;
    public float yMaxLimit = 80f;

    [Header("Скорость")]
    public float mouseBaseScale = 0.035f;
    public float gamepadBaseSpeed = 35f;

    [Header("Плавность")]
    [Range(0f, 0.25f)] public float rotationSmoothTime = 0.045f;
    [Range(0f, 0.25f)] public float followSmoothTime = 0.04f;
    [Tooltip("Плавность отдаления камеры после выхода из-за стены (приближение к стене всегда мгновенное).")]
    [Range(0f, 0.3f)] public float distanceSmoothTime = 0.08f;

    [Header("Коллизии со стенами и прозрачными объектами")]
    [Tooltip("Слои твёрдых стен и пола, сквозь которые камера и края экрана не должны проходить (HardWall).")]
    public LayerMask hardWallLayers;
    [Tooltip("Слои колонн и платформ, за которые камера может заходить, делая их прозрачными (FadeObstacle).")]
    public LayerMask cutoutObjectLayers;
    [Tooltip("Разрешить камере проходить за объекты из Cutout Object Layers.")]
    public bool allowPassBehindObject = true;

    [Header("Защита краёв экрана")]
    [Tooltip("Отступ от стен для ближней плоскости камеры (м).")]
    [Min(0.05f)] public float collisionBuffer = 0.22f;
    [Tooltip("Минимальный радиус сферы проверки камеры (м).")]
    [Min(0.1f)] public float sphereRadius = 0.35f;
    [Tooltip("Автоматически уменьшает Near Clip Plane камеры, чтобы края экрана не цепляли стены и шар.")]
    public bool autoAdjustNearClip = true;
    [Min(0.03f)] public float preferredNearClip = 0.1f;

    private Camera cam;
    private BallInputActions inputActions;
    private CameraObstacleFade obstacleFade;

    private float targetXAngle;
    private float targetYAngle;
    private float currentXAngle;
    private float currentYAngle;
    private float xAngleVelocity;
    private float yAngleVelocity;

    private Vector3 smoothedPivot;
    private Vector3 pivotVelocity;
    private float currentDistance;
    private float distanceVelocity;

    private readonly RaycastHit[] castHits = new RaycastHit[16];
    private readonly Collider[] overlapColliders = new Collider[16];
    private static SphereCollider tempProbeSphere;

    void Awake()
    {
        cam = GetComponent<Camera>();
        if (cam != null && autoAdjustNearClip && cam.nearClipPlane > preferredNearClip)
            cam.nearClipPlane = preferredNearClip;

        inputActions = new BallInputActions();
        obstacleFade = GetComponent<CameraObstacleFade>();
        if (obstacleFade != null && obstacleFade.playerTarget == null && target != null)
            obstacleFade.playerTarget = target;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void OnDestroy()
    {
        inputActions?.Dispose();
    }

    void OnEnable()
    {
        inputActions.Player.Enable();
    }

    void OnDisable()
    {
        inputActions.Player.Disable();
    }

    void Start()
    {
        Vector3 angles = transform.eulerAngles;
        targetXAngle = currentXAngle = angles.y;
        targetYAngle = currentYAngle = angles.x;
        currentDistance = Mathf.Max(distance, minDistance);

        if (target != null)
        {
            smoothedPivot = target.position;
            DetectBallRadius();
        }
    }

    private void DetectBallRadius()
    {
        if (target == null) return;
        if (target.TryGetComponent(out SphereCollider sphere))
        {
            Vector3 s = target.lossyScale;
            float maxScale = Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
            ballRadius = Mathf.Max(0.05f, sphere.radius * maxScale);
        }
    }

    public void SetSensitivity(float uniformSens)
    {
        float clamped = Mathf.Clamp(uniformSens, 0.1f, 10f);
        xSpeed = clamped;
        ySpeed = clamped;
    }

    public void SetSensitivityX(float sensX) => xSpeed = Mathf.Clamp(sensX, 0.1f, 10f);
    public void SetSensitivityY(float sensY) => ySpeed = Mathf.Clamp(sensY, 0.1f, 10f);
    public void SetInvertY(bool invert) => invertY = invert;

    void LateUpdate()
    {
        if (!target) return;

        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        if (obstacleFade != null && obstacleFade.playerTarget != target)
            obstacleFade.playerTarget = target;

        // 1. Ввод вращения камеры
        var lookAction = inputActions.Player.Look;
        Vector2 lookInput = lookAction.ReadValue<Vector2>();
        if (lookInput.sqrMagnitude > 0f)
        {
            bool isGamepad = lookAction.activeControl?.device is Gamepad;
            float ySign = invertY ? 1f : -1f;

            if (isGamepad)
            {
                float mult = gamepadBaseSpeed * dt;
                targetXAngle += lookInput.x * xSpeed * mult;
                targetYAngle += ySign * lookInput.y * ySpeed * mult;
            }
            else
            {
                targetXAngle += lookInput.x * xSpeed * mouseBaseScale;
                targetYAngle += ySign * lookInput.y * mouseBaseScale;
            }

            targetYAngle = Mathf.Clamp(targetYAngle, yMinLimit, yMaxLimit);
        }

        if (rotationSmoothTime > 0.001f)
        {
            currentXAngle = Mathf.SmoothDampAngle(currentXAngle, targetXAngle, ref xAngleVelocity, rotationSmoothTime, Mathf.Infinity, dt);
            currentYAngle = Mathf.SmoothDampAngle(currentYAngle, targetYAngle, ref yAngleVelocity, rotationSmoothTime, Mathf.Infinity, dt);
        }
        else
        {
            currentXAngle = targetXAngle;
            currentYAngle = targetYAngle;
        }

        // 2. Плавное слежение за шаром с жёстким ограничением отставания
        Vector3 rawBallCenter = target.position;
        if (followSmoothTime > 0.001f && (smoothedPivot - rawBallCenter).sqrMagnitude < 25f)
        {
            smoothedPivot = Vector3.SmoothDamp(smoothedPivot, rawBallCenter, ref pivotVelocity, followSmoothTime, Mathf.Infinity, dt);
            Vector3 lag = smoothedPivot - rawBallCenter;
            if (lag.magnitude > maxPivotLag)
                smoothedPivot = rawBallCenter + lag.normalized * maxPivotLag;
        }
        else
        {
            smoothedPivot = rawBallCenter;
            pivotVelocity = Vector3.zero;
        }

        LayerMask wallMask = GetWallCollisionMask();
        float near = cam != null ? cam.nearClipPlane : preferredNearClip;
        float frustumCornerRadius = ComputeNearPlaneCornerRadius(near);
        float edgeSafetyRadius = frustumCornerRadius + collisionBuffer * 0.5f;
        float castRadius = Mathf.Max(sphereRadius, edgeSafetyRadius);

        // 3. Защита точки привязки от пробивания потолка / нависающих стен
        Vector3 elevatedTarget = smoothedPivot + Vector3.up * heightOffset;
        if (wallMask.value != 0 && heightOffset > 0.01f)
        {
            if (CastClosest(rawBallCenter, Vector3.up, heightOffset, castRadius * 0.85f, wallMask, out RaycastHit upHit))
            {
                float safeHeight = Mathf.Max(0f, upHit.distance - collisionBuffer);
                elevatedTarget = rawBallCenter + Vector3.up * safeHeight;
            }
        }

        Quaternion rotation = Quaternion.Euler(currentYAngle, currentXAngle, 0f);
        Vector3 dir = rotation * Vector3.back;
        float desiredDist = Mathf.Max(distance, minDistance);

        // 4. Проверка стен центральным SphereCast и 8 точками периметра ближней плоскости экрана
        float targetDist = desiredDist;
        if (wallMask.value != 0)
        {
            if (CastClosest(elevatedTarget, dir, desiredDist, castRadius, wallMask, out RaycastHit wallHit))
            {
                targetDist = Mathf.Max(0.05f, wallHit.distance - collisionBuffer);
            }

            targetDist = ClampDistanceByNearPlanePerimeter(elevatedTarget, dir, rotation, targetDist, near, wallMask);
        }

        // Приближение к стене — мгновенно (чтобы края экрана не проваливались за стену), отдаление — плавно
        if (targetDist < currentDistance || distanceSmoothTime <= 0.001f)
        {
            currentDistance = targetDist;
            distanceVelocity = 0f;
        }
        else
        {
            currentDistance = Mathf.SmoothDamp(currentDistance, targetDist, ref distanceVelocity, distanceSmoothTime, Mathf.Infinity, dt);
        }

        Vector3 finalPos = elevatedTarget + dir * currentDistance;

        // 5. Защита от влетания камеры в шар (с обходом вверх вдоль стены, если шар прижат в угол)
        float minAllowedFromBall = ballRadius + near + minBallClearance;
        finalPos = EnforceBallClearance(finalPos, rawBallCenter, dir, minAllowedFromBall, edgeSafetyRadius, wallMask);

        // 6. Финальное выталкивание ближней плоскости камеры из твёрдых стен (устраняет просветы по краям экрана)
        if (wallMask.value != 0)
        {
            finalPos = DepenetrateFromWalls(finalPos, edgeSafetyRadius, wallMask, rawBallCenter, minAllowedFromBall);
        }

        transform.SetPositionAndRotation(finalPos, rotation);
    }

    private LayerMask GetWallCollisionMask()
    {
        int mask = hardWallLayers.value;
        if (!allowPassBehindObject)
            mask |= cutoutObjectLayers.value;
        return mask;
    }

    private float ComputeNearPlaneCornerRadius(float near)
    {
        float fov = cam != null ? cam.fieldOfView : 60f;
        float aspect = cam != null ? cam.aspect : (16f / 9f);
        float halfH = near * Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
        float halfW = halfH * aspect;
        return Mathf.Sqrt(near * near + halfH * halfH + halfW * halfW);
    }

    private bool CastClosest(Vector3 origin, Vector3 direction, float maxDist, float radius, LayerMask mask, out RaycastHit bestHit)
    {
        bestHit = default;
        int count = Physics.SphereCastNonAlloc(origin, radius, direction, castHits, maxDist, mask, QueryTriggerInteraction.Ignore);
        float minHitDist = float.MaxValue;
        bool found = false;

        for (int i = 0; i < count; i++)
        {
            Collider col = castHits[i].collider;
            if (col == null || col.transform == target || col.transform.IsChildOf(target))
                continue;

            if (castHits[i].distance < minHitDist)
            {
                minHitDist = castHits[i].distance;
                bestHit = castHits[i];
                found = true;
            }
        }

        return found;
    }

    /// <summary>
    /// Проверяет все 4 угла и 4 середины сторон ближней плоскости камеры (с запасом collisionBuffer),
    /// чтобы края экрана не выходили за стены при взгляде вдоль стены под острым углом.
    /// </summary>
    private float ClampDistanceByNearPlanePerimeter(Vector3 pivot, Vector3 dir, Quaternion rot, float currentDist, float near, LayerMask mask)
    {
        float fov = cam != null ? cam.fieldOfView : 60f;
        float aspect = cam != null ? cam.aspect : (16f / 9f);

        float checkDepth = near + collisionBuffer;
        float halfH = checkDepth * Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad) + collisionBuffer * 0.5f;
        float halfW = halfH * aspect;

        Vector3 candidateCamPos = pivot + dir * currentDist;
        Vector3 forward = rot * Vector3.forward;
        Vector3 right = rot * Vector3.right;
        Vector3 up = rot * Vector3.up;

        Vector3 nearCenter = candidateCamPos + forward * checkDepth;
        float resultDist = currentDist;

        for (int sx = -1; sx <= 1; sx++)
        {
            for (int sy = -1; sy <= 1; sy++)
            {
                if (sx == 0 && sy == 0) continue;

                Vector3 perimeterPoint = nearCenter + right * (sx * halfW) + up * (sy * halfH);
                Vector3 toPoint = perimeterPoint - pivot;
                float rayLen = toPoint.magnitude;
                if (rayLen <= 0.001f) continue;

                if (Physics.Raycast(pivot, toPoint / rayLen, out RaycastHit hit, rayLen, mask, QueryTriggerInteraction.Ignore))
                {
                    if (hit.collider.transform != target && !hit.collider.transform.IsChildOf(target))
                    {
                        float ratio = Mathf.Clamp01(hit.distance / rayLen);
                        float safeDist = Mathf.Max(0.05f, currentDist * ratio - collisionBuffer * 0.35f);
                        if (safeDist < resultDist)
                            resultDist = safeDist;
                    }
                }
            }
        }

        return resultDist;
    }

    private Vector3 EnforceBallClearance(
        Vector3 pos,
        Vector3 ballCenter,
        Vector3 fallbackDir,
        float minBallDist,
        float wallCheckRadius,
        LayerMask wallMask)
    {
        Vector3 fromBall = pos - ballCenter;
        float distFromBall = fromBall.magnitude;

        if (distFromBall >= minBallDist)
            return pos;

        Vector3 pushOutDir = distFromBall > 0.001f ? (fromBall / distFromBall) : fallbackDir;
        Vector3 radialPushed = ballCenter + pushOutDir * minBallDist;

        if (wallMask.value == 0 || !IsBlockedByWall(radialPushed, wallCheckRadius, wallMask))
            return radialPushed;

        // Если сзади упор в стену — поднимаем камеру вверх вдоль стены на безопасную дистанцию от шара
        Vector3 horizOffset = new Vector3(fromBall.x, 0f, fromBall.z);
        float horizSqr = horizOffset.sqrMagnitude;
        float requiredDy = Mathf.Sqrt(Mathf.Max(0f, minBallDist * minBallDist - horizSqr));

        Vector3 liftedUp = new Vector3(pos.x, ballCenter.y + requiredDy, pos.z);
        if (!IsBlockedByWall(liftedUp, wallCheckRadius, wallMask))
            return liftedUp;

        Vector3 liftedDown = new Vector3(pos.x, ballCenter.y - requiredDy, pos.z);
        if (!IsBlockedByWall(liftedDown, wallCheckRadius, wallMask))
            return liftedDown;

        return radialPushed;
    }

    private bool IsBlockedByWall(Vector3 pos, float radius, LayerMask mask)
    {
        int count = Physics.OverlapSphereNonAlloc(pos, radius, overlapColliders, mask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Collider col = overlapColliders[i];
            if (col != null && col.transform != target && !col.transform.IsChildOf(target))
                return true;
        }
        return false;
    }

    private Vector3 DepenetrateFromWalls(Vector3 pos, float checkRadius, LayerMask mask, Vector3 ballCenter, float minBallDist)
    {
        int count = Physics.OverlapSphereNonAlloc(pos, checkRadius, overlapColliders, mask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Collider col = overlapColliders[i];
            if (col == null || col.transform == target || col.transform.IsChildOf(target))
                continue;

            if (Physics.ComputePenetration(
                col, col.transform.position, col.transform.rotation,
                GetTempProbeSphere(checkRadius), pos, Quaternion.identity,
                out Vector3 sepDir, out float sepDist))
            {
                Vector3 wallOutNormal = -sepDir;
                pos += wallOutNormal * (sepDist + 0.01f);

                // Если выталкивание из стены прижало камеру к шару — скользим вдоль стены от центра шара
                Vector3 fromBall = pos - ballCenter;
                float dist = fromBall.magnitude;
                if (dist < minBallDist)
                {
                    Vector3 tangentOut = Vector3.ProjectOnPlane(fromBall, wallOutNormal);
                    if (tangentOut.sqrMagnitude > 0.0001f)
                    {
                        tangentOut.Normalize();
                        float alongNormal = Vector3.Dot(fromBall, wallOutNormal);
                        float neededTangent = Mathf.Sqrt(Mathf.Max(0f, minBallDist * minBallDist - alongNormal * alongNormal));
                        pos = ballCenter + wallOutNormal * alongNormal + tangentOut * neededTangent;
                    }
                }
            }
        }
        return pos;
    }

    private static SphereCollider GetTempProbeSphere(float radius)
    {
        if (tempProbeSphere == null)
        {
            var go = new GameObject("~CamCollisionProbe");
            go.hideFlags = HideFlags.HideAndDontSave;
            go.SetActive(false);
            tempProbeSphere = go.AddComponent<SphereCollider>();
        }
        tempProbeSphere.radius = radius;
        return tempProbeSphere;
    }
}