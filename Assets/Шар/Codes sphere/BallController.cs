using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody), typeof(GroundCheck), typeof(BallSlopeSlide))]
public class BallController : MonoBehaviour
{
    [Header("References")]
    public Transform cameraTransform;

    [Header("Movement Settings")]
    public float torqueStrength = 15f;
    public float maxSpeed = 10f;
    public float maxAngularVelocity = 25f;

    [Header("Brake Settings")]
    public float brakeStrength = 4f;
    public float brakeAngularDamping = 10f;

    [Header("Grip & Inertia (Плавное сцепление с платформами)")]
    [Tooltip("Время сглаживания скорости сразу после отпускания ввода (мягкое начало сцепления)")]
    public float gripSmoothTimeStart = 0.8f;
    [Tooltip("Время сглаживания скорости после полного 'созревания' сцепления (более крепкое, но всё ещё плавное)")]
    public float gripSmoothTimeMax = 0.15f;
    [Tooltip("Аналогично для углового сглаживания в начале")]
    public float rotationSmoothTimeStart = 0.6f;
    [Tooltip("Аналогично для углового сглаживания в максимуме")]
    public float rotationSmoothTimeMax = 0.12f;

    [Tooltip("Через сколько секунд после отпускания ввода сцепление достигает максимума (gripSmoothTimeMax)")]
    public float gripRampDuration = 0.6f;
    [Tooltip("Если относительная скорость ниже этого порога — сцепление сразу считается 'созревшим' наполовину (не ждём всю задержку)")]
    public float gripVelocityThreshold = 0.5f;

    [Header("Position Anchor (устраняет сползание при вращении платформы)")]
    [Tooltip("Насколько сильно шар подтягивается обратно к 'своей' точке на платформе, компенсируя снос из-за вращения. 0 = коррекция выключена")]
    public float anchorCorrectionStrength = 6f;
    [Tooltip("Максимальная скорость коррекции позиции — ограничивает рывок, если накопился большой снос")]
    public float anchorMaxCorrectionSpeed = 5f;

    [Header("Air Control")]
    [Tooltip("Множитель силы управления в воздухе относительно наземного torqueStrength")]
    [Range(0f, 1f)] public float airControlFactor = 0.2f;
    [Tooltip("Сила (не момент вращения!), которой шар толкается в воздухе. " +
             "Не крутит шар, а именно смещает его — раскрутки без контакта с землёй не будет.")]
    public float airControlForce = 20f;

    private Rigidbody rb;
    private GroundCheck ground;
    private BallSlopeSlide slopeSlide;
    private BallInputActions inputActions;

    private Vector2 moveInput;
    private bool isBraking;
    private float defaultAngularDamping;
    private float lastMoveTime;
    private IPlatformVelocityProvider activePlatform;
    private int lastContactCount;

    // Для SmoothDamp
    private Vector3 velocityRef;
    private Vector3 angularVelocityRef;

    // Для позиционного якоря на платформе
    private Transform activePlatformTransform;
    private Vector3 anchorLocalPos;
    private bool hasAnchor;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        ground = GetComponent<GroundCheck>();
        slopeSlide = GetComponent<BallSlopeSlide>();
        inputActions = new BallInputActions();

        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.maxAngularVelocity = maxAngularVelocity;
        defaultAngularDamping = rb.angularDamping;

        lastMoveTime = Time.time;
    }

    private void OnEnable()
    {
        inputActions.Player.Move.performed += OnMovePerformed;
        inputActions.Player.Move.canceled += OnMoveCanceled;

        inputActions.Player.Brake.performed += OnBrakePerformed;
        inputActions.Player.Brake.canceled += OnBrakeCanceled;

        inputActions.Player.Enable();
    }

    private void OnDisable()
    {
        inputActions.Player.Move.performed -= OnMovePerformed;
        inputActions.Player.Move.canceled -= OnMoveCanceled;

        inputActions.Player.Brake.performed -= OnBrakePerformed;
        inputActions.Player.Brake.canceled -= OnBrakeCanceled;

        inputActions.Player.Disable();
    }

    private void OnMovePerformed(InputAction.CallbackContext ctx)
    {
        moveInput = ctx.ReadValue<Vector2>();
        lastMoveTime = Time.time;
    }

    private void OnMoveCanceled(InputAction.CallbackContext ctx) => moveInput = Vector2.zero;

    private void OnBrakePerformed(InputAction.CallbackContext ctx) => isBraking = true;

    private void OnBrakeCanceled(InputAction.CallbackContext ctx) => isBraking = false;

    private void OnCollisionStay(Collision collision)
    {
        if (collision.gameObject.TryGetComponent<IPlatformVelocityProvider>(out var platform))
        {
            if (activePlatform == null || collision.contactCount > lastContactCount)
            {
                if (!ReferenceEquals(activePlatform, platform))
                {
                    // Сменилась платформа - старый якорь больше не валиден
                    hasAnchor = false;
                }
                activePlatform = platform;
                activePlatformTransform = (platform as MonoBehaviour)?.transform;
                lastContactCount = collision.contactCount;
            }
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        if (activePlatform != null && collision.gameObject == (activePlatform as MonoBehaviour)?.gameObject)
        {
            activePlatform = null;
            activePlatformTransform = null;
            hasAnchor = false;
        }
    }

    private void FixedUpdate()
    {
        if (cameraTransform == null) return;

        // 1. Направление движения относительно камеры
        Vector3 camForward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
        Vector3 camRight = Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized;
        Vector3 moveDir = (camForward * moveInput.y + camRight * moveInput.x).normalized;

        float control = (ground.IsGrounded ? 1f : airControlFactor) * slopeSlide.SlopeControlFactor;

        // 2. Данные платформы (если есть)
        Vector3 pVel = Vector3.zero;
        Vector3 pAngVel = Vector3.zero;
        if (ground.IsGrounded && activePlatform != null)
        {
            pVel = activePlatform.GetVelocityAtPoint(transform.position);
            pAngVel = activePlatform.GetAngularVelocity();
        }

        // 3. Обновляем таймер последнего ввода
        if (moveInput.sqrMagnitude > 0.01f) lastMoveTime = Time.time;

        // 4. Логика торможения (активное) или обычное управление
        if (ground.IsGrounded && isBraking)
        {
            hasAnchor = false; // во время активного торможения якорь не нужен
            ApplyBraking(pVel, pAngVel);
        }
        else
        {
            // Возвращаем стандартное угловое демпфирование
            rb.angularDamping = Mathf.Lerp(rb.angularDamping, defaultAngularDamping, Time.fixedDeltaTime * 5f);

            if (moveInput.sqrMagnitude > 0.01f)
            {
                if (ground.IsGrounded)
                {
                    // На земле – качение через момент силы (крутит шар, трение о поверхность
                    // превращает вращение в реальное перемещение)
                    Vector3 torqueAxis = Vector3.Cross(Vector3.up, moveDir);
                    rb.AddTorque(torqueAxis * torqueStrength * control, ForceMode.Acceleration);
                }
                else
                {
                    // В воздухе контакта с поверхностью нет, поэтому крутящий момент
                    // никак не гасится и раскручивает шар до предела без всякого смещения.
                    // Вместо этого толкаем шар напрямую силой в сторону ввода —
                    // так в воздухе шар слегка "рулится", но не набирает лишнее вращение.
                    rb.AddForce(moveDir * airControlForce * airControlFactor * slopeSlide.SlopeControlFactor, ForceMode.Acceleration);
                }

                // Сбрасываем "память" SmoothDamp и якорь - при следующем отпускании
                // кнопки сцепление и привязка к платформе стартуют заново, без рывка
                velocityRef = Vector3.zero;
                angularVelocityRef = Vector3.zero;
                hasAnchor = false;
            }
            else if (ground.IsGrounded && activePlatform != null)
            {
                // ---- ПАССИВНОЕ СЦЕПЛЕНИЕ (GRIP) ----
                float timeSinceMove = Time.time - lastMoveTime;
                Vector3 relativeVel = rb.linearVelocity - pVel;
                Vector3 horizRelVel = new Vector3(relativeVel.x, 0f, relativeVel.z);

                // 0 = только отпустили ввод, 1 = сцепление полностью "созрело"
                float rampT = Mathf.Clamp01(timeSinceMove / Mathf.Max(gripRampDuration, 0.0001f));

                if (horizRelVel.magnitude < gripVelocityThreshold)
                {
                    float velT = 1f - Mathf.Clamp01(horizRelVel.magnitude / gripVelocityThreshold);
                    rampT = Mathf.Max(rampT, velT);
                }

                float currentVelSmoothTime = Mathf.Lerp(gripSmoothTimeStart, gripSmoothTimeMax, rampT);
                float currentRotSmoothTime = Mathf.Lerp(rotationSmoothTimeStart, rotationSmoothTimeMax, rampT);

                // --- Позиционный якорь ---
                Vector3 anchorCorrectionVel = Vector3.zero;
                if (rampT >= 1f)
                {
                    if (!hasAnchor && activePlatformTransform != null)
                    {
                        anchorLocalPos = activePlatformTransform.InverseTransformPoint(rb.position);
                        hasAnchor = true;
                    }

                    if (hasAnchor && activePlatformTransform != null)
                    {
                        Vector3 anchorWorldPos = activePlatformTransform.TransformPoint(anchorLocalPos);
                        Vector3 posError = anchorWorldPos - rb.position;
                        Vector3 horizError = new Vector3(posError.x, 0f, posError.z);

                        anchorCorrectionVel = Vector3.ClampMagnitude(
                            horizError * anchorCorrectionStrength,
                            anchorMaxCorrectionSpeed
                        );
                    }
                }
                else
                {
                    hasAnchor = false;
                }

                // Корректируем только ГОРИЗОНТАЛЬНУЮ скорость через AddForce (VelocityChange),
                // оставляя Y-составляющую и реакцию нормалей PhysX под контролем движка.
                Vector3 currentHorizVel = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
                Vector3 targetHorizVel = new Vector3(pVel.x + anchorCorrectionVel.x, 0f, pVel.z + anchorCorrectionVel.z);

                Vector3 smoothHorizVel = Vector3.SmoothDamp(
                    currentHorizVel,
                    targetHorizVel,
                    ref velocityRef,
                    currentVelSmoothTime,
                    Mathf.Infinity,
                    Time.fixedDeltaTime
                );

                Vector3 horizVelChange = smoothHorizVel - currentHorizVel;
                rb.AddForce(horizVelChange, ForceMode.VelocityChange);

                // Изменение угловой скорости через AddTorque
                Vector3 smoothAngVel = Vector3.SmoothDamp(
                    rb.angularVelocity,
                    pAngVel,
                    ref angularVelocityRef,
                    currentRotSmoothTime,
                    Mathf.Infinity,
                    Time.fixedDeltaTime
                );

                Vector3 angVelChange = smoothAngVel - rb.angularVelocity;
                rb.AddTorque(angVelChange, ForceMode.VelocityChange);
            }
            else
            {
                hasAnchor = false;
            }
        }

        // 5. Ограничение максимальной горизонтальной скорости (относительно платформы)
        LimitVelocity(pVel);
    }

    private void ApplyBraking(Vector3 pVel, Vector3 pAngVel)
    {
        rb.angularDamping = Mathf.Lerp(rb.angularDamping, brakeAngularDamping, Time.fixedDeltaTime * 10f);

        // Тормозим только горизонтальную составляющую через AddForce
        Vector3 currentRelVel = rb.linearVelocity - pVel;
        Vector3 horizRelVel = new Vector3(currentRelVel.x, 0f, currentRelVel.z);

        Vector3 targetHorizVel = Vector3.Lerp(horizRelVel, Vector3.zero, Time.fixedDeltaTime * brakeStrength);
        Vector3 velChange = targetHorizVel - horizRelVel;
        rb.AddForce(velChange, ForceMode.VelocityChange);

        Vector3 targetAngVel = Vector3.Lerp(rb.angularVelocity, pAngVel, Time.fixedDeltaTime * brakeStrength);
        Vector3 angVelChange = targetAngVel - rb.angularVelocity;
        rb.AddTorque(angVelChange, ForceMode.VelocityChange);
    }

    private void LimitVelocity(Vector3 pVel)
    {
        Vector3 relVel = rb.linearVelocity - pVel;
        Vector3 horizRel = new Vector3(relVel.x, 0f, relVel.z);

        if (horizRel.magnitude > maxSpeed)
        {
            Vector3 clampedHorizRel = horizRel.normalized * maxSpeed;
            Vector3 velChange = clampedHorizRel - horizRel;
            rb.AddForce(velChange, ForceMode.VelocityChange);
        }
    }
}