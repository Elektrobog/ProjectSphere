using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody), typeof(GroundCheck), typeof(BallSlopeSlide))]
public class BallController : MonoBehaviour
{
    [Header("References")]
    public Transform cameraTransform;

    [Header("Movement Settings")]
    [Tooltip("Сила крутящего момента для движения шара")]
    public float torqueStrength = 15f;
    [Tooltip("Максимальная скорость движения шара")]
    public float maxSpeed = 10f;
    [Tooltip("Максимальная угловая скорость")]
    public float maxAngularVelocity = 25f;
    
    [Tooltip("Множитель ускорения при начале движения (0.0-1.0 для плавного старта)")]
    [Range(0f, 1f)]
    public float accelerationMultiplier = 0.8f;
    
    [Tooltip("Множитель замедления при отпускании ввода (0.0-1.0)")]
    [Range(0f, 1f)]
    public float decelerationMultiplier = 0.6f;
    
    [Tooltip("Время плавного ускорения до полной силы (в секундах)")]
    public float accelerationRampTime = 0.3f;

    [Header("Brake Settings")]
    [Tooltip("Сила торможения")]
    public float brakeStrength = 4f;
    [Tooltip("Угловое демпфирование при торможении")]
    public float brakeAngularDamping = 10f;

    [Header("Grip & Inertia (Плавное сцепление с платформой)")]
    [Tooltip("Время сглаживания скорости сразу после отпускания ввода (мягкое начало сцепления)")]
    public float gripSmoothTimeStart = 0.8f;
    [Tooltip("Время сглаживания скорости после полного 'созревания' (без задержки, не сразу после ввода)")]
    public float gripSmoothTimeMax = 0.15f;
    [Tooltip("Аналогично для угловой скорости сглаживания в начале")]
    public float rotationSmoothTimeStart = 0.6f;
    [Tooltip("Аналогично для угловой скорости сглаживания в максимуме")]
    public float rotationSmoothTimeMax = 0.12f;

    [Tooltip("Через сколько секунд скорость после отпускания ввода достигает максимума (gripSmoothTimeMax)")]
    public float gripRampDuration = 0.6f;
    [Tooltip("Ещё один параметр: если скорость ниже этого порога, то сцепление срабатывает сразу без задержки (не ждём gripRampDuration)")]
    public float gripVelocityThreshold = 0.5f;

    [Header("Position Anchor (Устраняет сползание при вращении платформы)")]
    [Tooltip("Насколько сильно шар подтягивается к 'якорю' на платформе, если не движется")]
    public float anchorCorrectionStrength = 6f;
    [Tooltip("Максимальная скорость коррекции позиции — ограничивает рывки")]
    public float anchorMaxCorrectionSpeed = 5f;

    [Header("Air Control")]
    [Tooltip("Множитель силы управления в воздухе от torqueStrength")]
    [Range(0f, 1f)] public float airControlFactor = 0.2f;
    [Tooltip("Сила (м/с²) для управления в воздухе через AddForce")]
    public float airControlForce = 20f;
    
    [Tooltip("Дополнительный множитель для воздуха при движении по склонам")]
    [Range(0f, 1f)]
    public float airSlopeControlFactor = 0.5f;

    [Header("Advanced Physics")]
    [Tooltip("Дополнительная гравитация при подъеме (для более отзывчивого прыжка)")]
    public float extraGravityRise = 0f;
    [Tooltip("Дополнительная гравитация при падении (для более быстрого спуска)")]
    public float extraGravityFall = 0f;
    
    [Tooltip("Максимальный угол наклона, при котором шар может катиться вверх")]
    [Range(0f, 90f)]
    public float maxUphillAngle = 30f;
    
    [Tooltip("Множитель силы при движении вверх по склону")]
    [Range(0f, 1f)]
    public float uphillTorqueMultiplier = 0.5f;

    private Rigidbody rb;
    private GroundCheck ground;
    private BallSlopeSlide slopeSlide;
    private BallInputActions inputActions;

    private Vector2 moveInput;
    private Vector2 lastMoveInput;
    private bool isBraking;
    private float defaultAngularDamping;
    private float lastMoveTime;
    private IPlatformVelocityProvider activePlatform;
    private int lastContactCount;

    // Для SmoothDamp
    private Vector3 velocityRef;
    private Vector3 angularVelocityRef;

    // Для позиционного якоря
    private Transform activePlatformTransform;
    private Vector3 anchorLocalPos;
    private bool hasAnchor;
    
    // Для плавного ускорения
    private float currentTorqueMultiplier = 0f;
    private float inputMagnitudeHistory = 0f;
    private float accelerationRampVelocity = 0f;

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
        lastMoveInput = moveInput;
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
                    // Сменилаcь платформа - сбросим якорь
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

        // Вычисляем текущий множитель управления
        float control = (ground.IsGrounded ? 1f : airControlFactor * airSlopeControlFactor) * slopeSlide.SlopeControlFactor;

        // 2. Данные платформы (если есть)
        Vector3 pVel = Vector3.zero;
        Vector3 pAngVel = Vector3.zero;
        if (ground.IsGrounded && activePlatform != null)
        {
            pVel = activePlatform.GetVelocityAtPoint(transform.position);
            pAngVel = activePlatform.GetAngularVelocity();
        }

        // 3. Обновляем таймер последнего движения
        if (moveInput.sqrMagnitude > 0.01f) lastMoveTime = Time.time;

        // ===== ПЛАВНОЕ УСКОРЕНИЕ =====
        // Вычисляем изменение величины ввода
        float currentInputMagnitude = moveInput.magnitude;
        float inputChange = currentInputMagnitude - inputMagnitudeHistory;
        inputMagnitudeHistory = currentInputMagnitude;
        
        // Плавно меняем множитель крутящего момента при ускорении
        if (currentInputMagnitude > 0.01f)
        {
            // При нажатии - плавно увеличиваем силу
            currentTorqueMultiplier = Mathf.SmoothDamp(
                currentTorqueMultiplier, 
                1f, 
                ref accelerationRampVelocity,
                accelerationRampTime
            );
        }
        else if (currentTorqueMultiplier > 0.01f)
        {
            // При отпускании - плавно уменьшаем силу
            currentTorqueMultiplier = Mathf.Lerp(
                currentTorqueMultiplier, 
                0f, 
                decelerationMultiplier * Time.fixedDeltaTime * 10f
            );
        }
        else
        {
            currentTorqueMultiplier = 0f;
        }
        
        // Учитываем наклон поверхности
        float slopeFactor = 1f;
        if (ground.IsGrounded)
        {
            float slopeAngle = Vector3.Angle(ground.GroundNormal, Vector3.up);
            
            // Если движемся вверх по склону, уменьшаем силу
            if (moveDir != Vector3.zero && slopeAngle > 5f)
            {
                Vector3 moveDirHorizontal = new Vector3(moveDir.x, 0, moveDir.z).normalized;
                Vector3 slopeDownDir = Vector3.ProjectOnPlane(Vector3.down, ground.GroundNormal).normalized;
                
                // Проверяем движемся ли вверх по склону
                float dot = Vector3.Dot(moveDirHorizontal, slopeDownDir);
                if (dot < 0) // Движение вверх по склону
                {
                    float uphillAngle = slopeAngle;
                    // Чем круче склон и чем выше угол движения против склона, тем сильнее уменьшаем силу
                    float uphillMultiplier = Mathf.Clamp01(
                        1f - (uphillAngle / maxUphillAngle) * (1f - uphillTorqueMultiplier)
                    );
                    slopeFactor = uphillMultiplier;
                }
            }
        }
        
        // Конечный множитель torque
        float finalTorqueMultiplier = currentTorqueMultiplier * slopeFactor * accelerationMultiplier;

        // 4. Логика торможения или обычного движения
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
                    // На земле - качение через крутящий момент
                    Vector3 torqueAxis = Vector3.Cross(Vector3.up, moveDir);
                    rb.AddTorque(torqueAxis * torqueStrength * control * finalTorqueMultiplier, ForceMode.Acceleration);
                }
                else
                {
                    // В воздухе - применение силы напрямую
                    // Это позволяет лучше контролировать шар в полете
                    rb.AddForce(moveDir * airControlForce * airControlFactor * slopeSlide.SlopeControlFactor, ForceMode.Acceleration);
                }

                // Сбрасываем "память" SmoothDamp и якорь
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

                // 0 = только что отпустили ввод, 1 = сцепление полностью "созрело"
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

                // Корректируем скорость через AddForce (VelocityChange),
                // оставляя Y-составляющую нетронутой и позволяя PhysX контролировать движение по вертикали.
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

                // Изменение угловой скорости
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

        // 5. Ограничение максимальной горизонтальной скорости
        LimitVelocity(pVel);
        
        // 6. Дополнительная гравитация
        ApplyExtraGravity();
    }

    private void ApplyBraking(Vector3 pVel, Vector3 pAngVel)
    {
        rb.angularDamping = Mathf.Lerp(rb.angularDamping, brakeAngularDamping, Time.fixedDeltaTime * 10f);

        // Тормозим только горизонтальную скорость через AddForce
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
    
    private void ApplyExtraGravity()
    {
        if (extraGravityRise > 0f || extraGravityFall > 0f)
        {
            float g = rb.linearVelocity.y > 0f ? extraGravityRise : extraGravityFall;
            rb.AddForce(Vector3.down * g, ForceMode.Acceleration);
        }
    }
}
