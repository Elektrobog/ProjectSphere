using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class OrbitCamera : MonoBehaviour
{
    [Header("Цель и дистанция")]
    public Transform target;
    public float distance = 5f;
    public float minDistance = 1.2f;
    public float heightOffset = 2f;

    [Header("Чувствительность (шкала 0.1 – 10, норма = 5)")]
    [Tooltip("Чувствительность по горизонтали в пользовательской шкале (5 = стандарт).")]
    [Range(0.1f, 10f)] public float xSpeed = 5f;

    [Tooltip("Чувствительность по вертикали в пользовательской шкале (5 = стандарт).")]
    [Range(0.1f, 10f)] public float ySpeed = 5f;

    [Tooltip("Инвертировать вертикальную ось (ось Y).")]
    public bool invertY = false;

    public float yMinLimit = -20f;
    public float yMaxLimit = 80f;

    [Header("Калибровка базовой скорости")]
    [Tooltip("Градусов на 1 пиксель движения мыши при сенсе = 1. При 0.035 значение сенсы 5 будет комфортным.")]
    public float mouseBaseScale = 0.035f;

    [Tooltip("Скорость поворота (°/сек) для стика геймпада при сенсе = 1.")]
    public float gamepadBaseSpeed = 35f;

    [Header("Плавность камеры")]
    [Tooltip("Плавность поворота камеры (0 = мгновенно, 0.04–0.07 = мягко без рывков).")]
    [Range(0f, 0.25f)] public float rotationSmoothTime = 0.045f;

    [Tooltip("Плавность следования за позицией шара (0.03–0.06 убирает микро-ступеньки физики).")]
    [Range(0f, 0.25f)] public float followSmoothTime = 0.04f;

    [Tooltip("Плавность изменения дистанции при обходе препятствий.")]
    [Range(0f, 0.3f)] public float distanceSmoothTime = 0.08f;

    [Header("Слои коллизий")]
    [Tooltip("Слои, сквозь которые камера ВООБЩЕ не может летать (пол и внешние стены).")]
    public LayerMask hardWallLayers;

    [Tooltip("Слои объектов перекрытия (колонна, платформы).")]
    public LayerMask cutoutObjectLayers;

    public float collisionBuffer = 0.25f;
    public float sphereRadius = 0.35f;

    [Header("Поведение у объектов перекрытия")]
    [Tooltip("Насколько близко камера может подлетать к поверхности объекта (в метрах).")]
    [Min(0.05f)] public float minObjectProximity = 0.6f;

    [Tooltip("Разрешить камере уходить ЗА объект, если позади есть место.")]
    public bool allowPassBehindObject = true;

    [Tooltip("Насколько дальше distance камера может уйти, чтобы оказаться ЗА толстым объектом.")]
    [Min(0f)] public float behindObjectExtraDistance = 6f;

    [Tooltip("Мгновенный перенос через объект без плавного пролёта.")]
    public bool instantPassThrough = false;

    private const int MaxCrossings = 16;
    private const float NoEnd = 1e9f;

    private BallInputActions inputActions;
    private CameraObstacleFade obstacleFade;

    private readonly RaycastHit[] hitBuffer = new RaycastHit[16];
    private readonly float[] entryDist = new float[MaxCrossings];
    private readonly float[] exitDist = new float[MaxCrossings];
    private readonly float[] intStart = new float[MaxCrossings];
    private readonly float[] intEnd = new float[MaxCrossings];

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

    void Awake()
    {
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
        currentDistance = distance;

        if (target != null)
            smoothedPivot = target.position;
    }

    // --- Публичные методы для привязки к UI-слайдерам в меню настроек ---

    public void SetSensitivity(float uniformSens)
    {
        float clamped = Mathf.Clamp(uniformSens, 0.1f, 10f);
        xSpeed = clamped;
        ySpeed = clamped;
    }

    public void SetSensitivityX(float sensX)
    {
        xSpeed = Mathf.Clamp(sensX, 0.1f, 10f);
    }

    public void SetSensitivityY(float sensY)
    {
        ySpeed = Mathf.Clamp(sensY, 0.1f, 10f);
    }

    public void SetInvertY(bool invert)
    {
        invertY = invert;
    }

    // -------------------------------------------------------------------

    void LateUpdate()
    {
        if (!target) return;

        float dt = Time.deltaTime;
        // Если игра на паузе (например, открыто меню настроек) — не крутим камеру
        if (dt <= 0f) return;

        if (obstacleFade != null && obstacleFade.playerTarget != target)
            obstacleFade.playerTarget = target;

        // 1. Чтение ввода (отдельно для мыши и для стика геймпада)
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
                targetYAngle += ySign * lookInput.y * ySpeed * mouseBaseScale;
            }

            targetYAngle = Mathf.Clamp(targetYAngle, yMinLimit, yMaxLimit);
        }

        // 2. Плавное вращение камеры
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

        // 3. Плавное слежение за точкой привязки шара
        Vector3 rawPivot = target.position;
        if (followSmoothTime > 0.001f && (smoothedPivot - rawPivot).sqrMagnitude < 25f)
        {
            smoothedPivot = Vector3.SmoothDamp(smoothedPivot, rawPivot, ref pivotVelocity, followSmoothTime, Mathf.Infinity, dt);
        }
        else
        {
            smoothedPivot = rawPivot;
            pivotVelocity = Vector3.zero;
        }

        Quaternion rotation = Quaternion.Euler(currentYAngle, currentXAngle, 0f);
        Vector3 elevatedTarget = smoothedPivot + Vector3.up * heightOffset;

        if (heightOffset > 0.05f && CastHardWall(smoothedPivot, Vector3.up, heightOffset, sphereRadius * 0.6f, out RaycastHit upHit))
        {
            elevatedTarget = smoothedPivot + Vector3.up * Mathf.Max(0.1f, upHit.distance - collisionBuffer);
        }

        Vector3 dir = rotation * Vector3.back;

        // 4. Проверка твёрдых стен
        float maxReach = distance + (allowPassBehindObject ? behindObjectExtraDistance : 0f);
        float wallFree = maxReach;
        if (CastHardWall(elevatedTarget, dir, maxReach, sphereRadius, out RaycastHit backHit))
        {
            wallFree = Mathf.Max(minDistance, backHit.distance - collisionBuffer);
        }

        float desiredDistance = Mathf.Min(distance, wallFree);
        bool snap = false;

        // 5. Проверка объектов перекрытия (колонна, платформы)
        if (cutoutObjectLayers.value != 0)
        {
            float clearance = Mathf.Max(minObjectProximity, sphereRadius + 0.05f);
            int n = BuildSolidIntervals(elevatedTarget, dir, wallFree + clearance, clearance);
            desiredDistance = ResolveAgainstIntervals(desiredDistance, wallFree, n);
            snap = instantPassThrough && RangeCrossesSolid(currentDistance, desiredDistance, n);
        }

        // 6. Плавная интерполяция дистанции
        if (snap)
        {
            currentDistance = desiredDistance;
            distanceVelocity = 0f;
        }
        else
        {
            float smooth = desiredDistance < currentDistance ? distanceSmoothTime * 0.5f : distanceSmoothTime;
            currentDistance = smooth > 0.001f
                ? Mathf.SmoothDamp(currentDistance, desiredDistance, ref distanceVelocity, smooth, Mathf.Infinity, dt)
                : desiredDistance;
        }

        transform.SetPositionAndRotation(elevatedTarget + dir * currentDistance, rotation);
    }

    private int BuildSolidIntervals(Vector3 origin, Vector3 dir, float length, float clearance)
    {
        int nEntry = CollectCrossingsNonAlloc(origin, dir, length, entryDist, false);

        Vector3 end = origin + dir * length;
        int nExit = CollectCrossingsNonAlloc(end, -dir, length, exitDist, true);

        Array.Sort(entryDist, 0, nEntry);
        Array.Sort(exitDist, 0, nExit);

        int count = 0;
        int ei = 0, xi = 0, depth = 0;
        float start = 0f;

        if (nExit > 0 && (nEntry == 0 || exitDist[0] < entryDist[0]))
        {
            depth = 1;
            start = 0f;
        }

        while (ei < nEntry || xi < nExit)
        {
            bool takeEntry = xi >= nExit || (ei < nEntry && entryDist[ei] <= exitDist[xi]);
            if (takeEntry)
            {
                if (depth == 0) start = entryDist[ei];
                depth++;
                ei++;
            }
            else
            {
                if (depth > 0)
                {
                    depth--;
                    if (depth == 0 && count < MaxCrossings)
                    {
                        intStart[count] = start;
                        intEnd[count] = exitDist[xi];
                        count++;
                    }
                }
                xi++;
            }
        }

        if (depth > 0 && count < MaxCrossings)
        {
            intStart[count] = start;
            intEnd[count] = NoEnd;
            count++;
        }

        int merged = 0;
        for (int i = 0; i < count; i++)
        {
            float s = Mathf.Max(0f, intStart[i] - clearance);
            float e = intEnd[i] >= NoEnd ? NoEnd : intEnd[i] + clearance;

            if (merged > 0 && s <= intEnd[merged - 1])
            {
                intEnd[merged - 1] = Mathf.Max(intEnd[merged - 1], e);
            }
            else
            {
                intStart[merged] = s;
                intEnd[merged] = e;
                merged++;
            }
        }

        return merged;
    }

    private int CollectCrossingsNonAlloc(Vector3 from, Vector3 dir, float length, float[] result, bool invertDistance)
    {
        int rawHits = Physics.RaycastNonAlloc(from, dir, hitBuffer, length, cutoutObjectLayers, QueryTriggerInteraction.Ignore);
        int n = 0;

        for (int i = 0; i < rawHits && n < result.Length; i++)
        {
            Collider col = hitBuffer[i].collider;
            if (col == null || IsTarget(col)) continue;

            float d = hitBuffer[i].distance;
            result[n++] = invertDistance ? (length - d) : d;
        }

        return n;
    }

    private float ResolveAgainstIntervals(float dist, float wallFree, int n)
    {
        for (int i = 0; i < n; i++)
        {
            if (dist > intStart[i] && dist < intEnd[i])
            {
                float behind = intEnd[i];
                float front = intStart[i];

                dist = (allowPassBehindObject && behind <= wallFree)
                    ? behind
                    : Mathf.Max(minDistance, front);
            }
        }

        return dist;
    }

    private bool RangeCrossesSolid(float a, float b, int n)
    {
        float lo = Mathf.Min(a, b);
        float hi = Mathf.Max(a, b);

        for (int i = 0; i < n; i++)
        {
            if (intStart[i] < hi && intEnd[i] > lo) return true;
        }

        return false;
    }

    private bool IsTarget(Collider col)
    {
        return col.transform == target || col.transform.IsChildOf(target);
    }

    private bool CastHardWall(Vector3 origin, Vector3 direction, float maxDist, float radius, out RaycastHit bestHit)
    {
        bestHit = default;
        if (hardWallLayers.value == 0) return false;

        // Обычно hardWallLayers не содержит игрока: в таком случае достаточно
        // обычного SphereCast, который возвращает только ближайшее пересечение.
        if (Physics.SphereCast(
            origin,
            radius,
            direction,
            out bestHit,
            maxDist,
            hardWallLayers,
            QueryTriggerInteraction.Ignore))
        {
            if (bestHit.collider != null && !IsTarget(bestHit.collider))
                return true;

            // Совместимость с конфигурацией, где коллайдер цели тоже входит
            // в hardWallLayers: ищем ближайшее пересечение, не принадлежащее цели.
        }
        else
        {
            return false;
        }

        int count = Physics.SphereCastNonAlloc(
            origin,
            radius,
            direction,
            hitBuffer,
            maxDist,
            hardWallLayers,
            QueryTriggerInteraction.Ignore);

        float closestDist = float.MaxValue;
        bool found = false;
        for (int i = 0; i < count; i++)
        {
            RaycastHit h = hitBuffer[i];
            if (h.collider == null || IsTarget(h.collider)) continue;

            if (h.distance < closestDist)
            {
                closestDist = h.distance;
                bestHit = h;
                found = true;
            }
        }

        return found;
    }
}