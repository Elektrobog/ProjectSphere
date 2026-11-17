using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Физический контроллер шара.
///
/// Игрок НЕ толкает шар. Ввод создаёт только крутящий момент (торк) вокруг горизонтальной оси,
/// перпендикулярной направлению движения. Шар едет вперёд исключительно за счёт трения о поверхность,
/// поэтому он реалистично: буксует при слишком резком разгоне, катится по инерции,
/// разворачивается по дуге, сносится вращающейся платформой и т.д.
///
/// Направления (W/S/A/D) считаются относительно взгляда камеры.
/// Для работы нужно трение на коллайдерах шара и поверхностей (см. комментарий внизу файла).
/// </summary>
[RequireComponent(typeof(Rigidbody), typeof(GroundCheck), typeof(BallSlopeSlide))]
public class BallController : MonoBehaviour
{
    [Header("References")]
    public Transform cameraTransform;

    [Header("Rolling Drive")]
    [Tooltip("Максимальная скорость качения (м/с). Выше неё привод шара отключается.")]
    [Min(0f)] public float maxSpeed = 10f;
    [Tooltip("Желаемое линейное ускорение от привода (м/с²). Реально ограничено трением: " +
             "примерно коэффициент трения × 9.81. Если больше — шар буксует.")]
    [Min(0f)] public float driveAcceleration = 6f;
    [Tooltip("Допустимое проскальзывание (м/с): насколько поверхность шара может «обгонять» его скорость. " +
             "Ограничитель буксования режет привод и торможение, чтобы шар катился, а не скользил. " +
             "Меньше — жёстче сцепление, больше — допускает дрифт.")]
    [Min(0.05f)] public float maxSlipSpeed = 0.8f;
    [Tooltip("Ускорение, когда игрок давит против текущего движения (м/с²).")]
    [Min(0f)] public float reverseAcceleration = 14f;
    [Tooltip("Насколько активно шар гасит боковое скольжение/снос при повороте (1/с). " +
             "0 — чистая инерция, шар поворачивает очень лениво.")]
    [Min(0f)] public float lateralGrip = 3f;
    [Tooltip("Сопротивление качению, когда клавиши отпущены (1/с). Чем больше — тем быстрее шар останавливается.")]
    [Min(0f)] public float rollingResistance = 1.5f;
    [Tooltip("Торможение по кнопке Brake (м/с²). Тоже работает через трение.")]
    [Min(0f)] public float brakeAcceleration = 20f;

    [Header("Ball")]
    [Min(0.01f)] public float ballRadius = 0.5f;
    [Min(1f)] public float maxAngularVelocity = 25f;
    [Tooltip("1 + I/(m·r²). Для сплошного шара = 1.4. Нужен, чтобы перевести желаемое линейное ускорение в угловое.")]
    [Min(1f)] public float rollingInertiaFactor = 1.4f;

    [Header("Air Control")]
    [Range(0f, 1f)] public float airControlFactor = 0.2f;
    [Min(0f)] public float airControlForce = 20f;

    /// <summary>Скорость платформы в точке шара. Используется, в частности, BallJump.</summary>
    public Vector3 PlatformVelocity { get; private set; }

    private const float InputDeadZone = 0.01f;

    private Rigidbody rb;
    private GroundCheck ground;
    private BallSlopeSlide slopeSlide;
    private BallInputActions inputActions;

    private Vector2 moveInput;
    private bool isBraking;
    private IPlatformVelocityProvider activePlatform;
    private int lastContactCount;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        ground = GetComponent<GroundCheck>();
        slopeSlide = GetComponent<BallSlopeSlide>();
        inputActions = new BallInputActions();

        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.maxAngularVelocity = maxAngularVelocity;

        if (TryGetComponent(out SphereCollider sphere))
        {
            Vector3 scale = transform.lossyScale;
            ballRadius = Mathf.Max(0.01f, sphere.radius *
                Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
        }
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

        moveInput = Vector2.zero;
        isBraking = false;
    }

    private void OnMovePerformed(InputAction.CallbackContext context) => moveInput = context.ReadValue<Vector2>();
    private void OnMoveCanceled(InputAction.CallbackContext context) => moveInput = Vector2.zero;
    private void OnBrakePerformed(InputAction.CallbackContext context) => isBraking = true;
    private void OnBrakeCanceled(InputAction.CallbackContext context) => isBraking = false;

    // ---------- Платформы: нужны только чтобы считать скорость ОТНОСИТЕЛЬНО платформы ----------

    private void OnCollisionStay(Collision collision)
    {
        if (!collision.gameObject.TryGetComponent<IPlatformVelocityProvider>(out var platform))
            return;

        if (activePlatform == null || collision.contactCount > lastContactCount)
        {
            activePlatform = platform;
            lastContactCount = collision.contactCount;
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        if (activePlatform == null)
            return;

        if (collision.gameObject == (activePlatform as MonoBehaviour)?.gameObject)
        {
            activePlatform = null;
            PlatformVelocity = Vector3.zero;
        }
    }

    // ---------- Физика ----------

    private void FixedUpdate()
    {
        if (cameraTransform == null)
            return;

        Vector3 platformVelocity = Vector3.zero;
        Vector3 platformAngularVelocity = Vector3.zero;

        if (ground.IsGrounded && activePlatform != null)
        {
            platformVelocity = activePlatform.GetVelocityAtPoint(rb.position);
            platformAngularVelocity = activePlatform.GetAngularVelocity();
        }

        PlatformVelocity = platformVelocity;

        Vector3 inputDirection = GetCameraRelativeInput();

        if (ground.IsGrounded)
            RollOnGround(inputDirection, platformVelocity, platformAngularVelocity);
        else
            MoveInAir(inputDirection);
    }

    /// <summary>Ввод в мировых координатах относительно взгляда камеры. Длина вектора 0..1 — сила нажатия.</summary>
    private Vector3 GetCameraRelativeInput()
    {
        Vector3 forward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up);
        if (forward.sqrMagnitude < 0.0001f) // камера смотрит строго вверх/вниз
            forward = Vector3.ProjectOnPlane(cameraTransform.up, Vector3.up);
        forward.Normalize();

        Vector3 right = Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized;
        Vector3 direction = forward * moveInput.y + right * moveInput.x;

        return Vector3.ClampMagnitude(direction, 1f);
    }

    private void RollOnGround(Vector3 inputDirection, Vector3 platformVelocity, Vector3 platformAngularVelocity)
    {
        Vector3 normal = ground.GroundNormal.sqrMagnitude > 0.001f
            ? ground.GroundNormal.normalized
            : Vector3.up;

        float radius = Mathf.Max(0.01f, ballRadius);
        // Перевод «линейное ускорение центра» -> «угловое ускорение» при качении без проскальзывания.
        float toAngular = rollingInertiaFactor / radius;

        // Всё считаем относительно платформы, чтобы вращающаяся платформа не воспринималась как «движение игрока».
        Vector3 relativeVelocity = Vector3.ProjectOnPlane(rb.linearVelocity - platformVelocity, normal);
        Vector3 relativeSpin = rb.angularVelocity - platformAngularVelocity;

        float inputMagnitude = inputDirection.magnitude;
        Vector3 direction = Vector3.ProjectOnPlane(inputDirection, normal);
        bool hasInput = inputMagnitude > InputDeadZone && direction.sqrMagnitude > InputDeadZone * InputDeadZone;

        Vector3 angularAcceleration = Vector3.zero;

        if (isBraking)
        {
            // Тормоз: крутящий момент против текущего вращения, останавливает трение.
            // «ABS»: не даём колесу застопориться раньше, чем шар остановится (иначе шар скользит юзом).
            float absFactor = GetBrakingTraction(relativeVelocity, relativeSpin, normal, radius);
            angularAcceleration = OpposeSpin(relativeSpin, brakeAcceleration * toAngular * absFactor);
        }
        else if (hasInput)
        {
            direction.Normalize();

            // Ось качения: шар катится в сторону direction, если вращать его вокруг normal × direction.
            // (v = ω × r, контакт с землёй в точке -normal·R  =>  ω = normal × v / R)
            Vector3 rollAxis = Vector3.Cross(normal, direction);

            float targetSpeed = maxSpeed * inputMagnitude;
            float speedAlong = Vector3.Dot(relativeVelocity, direction);

            // Учитываем и вращение: если колесо уже «раскручено» (буксует), дальше не разгоняем.
            float spinSpeed = Vector3.Dot(relativeSpin, rollAxis) * radius;
            float rollSpeed = Mathf.Max(speedAlong, spinSpeed);

            float headroom = Mathf.Clamp01((targetSpeed - rollSpeed) / Mathf.Max(0.5f, targetSpeed * 0.15f));
            float acceleration = speedAlong < -0.25f ? reverseAcceleration : driveAcceleration;

            // Ограничитель буксования: чем сильнее поверхность шара обгоняет скорость, тем меньше привод.
            float slip = spinSpeed - speedAlong;
            float tractionFactor = 1f - Mathf.Clamp01(slip / maxSlipSpeed);

            Vector3 drive = rollAxis * (acceleration * headroom * tractionFactor * toAngular);

            // Если колесо всё же раскрутилось сверх допуска, мягко возвращаем его к скорости качения.
            if (slip > maxSlipSpeed)
                drive -= rollAxis * ((slip - maxSlipSpeed) / radius * 10f);

            // Гашение бокового сноса: тоже только моментом, физика трения сделает остальное.
            Vector3 lateral = relativeVelocity - direction * speedAlong;
            Vector3 lateralCorrection = Vector3.zero;
            if (lateralGrip > 0f && lateral.sqrMagnitude > 0.0025f)
            {
                lateralCorrection = -Vector3.Cross(normal, lateral) * (lateralGrip * rollingInertiaFactor / radius);
                lateralCorrection = Vector3.ClampMagnitude(lateralCorrection, reverseAcceleration * toAngular);
            }

            angularAcceleration = drive + lateralCorrection;
        }
        else
        {
            // Клавиши отпущены: шар катится по инерции, медленно теряя скорость из-за сопротивления качению.
            // Тот же «ABS», чтобы сопротивление не стопорило колесо и шар не скользил как по льду.
            float absFactor = GetBrakingTraction(relativeVelocity, relativeSpin, normal, radius);
            angularAcceleration = -relativeSpin * (rollingResistance * absFactor);
        }

        // На крутом склоне управление слабеет, как и раньше.
        if (hasInput && !isBraking)
            angularAcceleration *= slopeSlide.SlopeControlFactor;

        rb.AddTorque(angularAcceleration, ForceMode.Acceleration);
    }

    /// <summary>
    /// 1 — шар катится без проскальзывания, 0 — вращение отстало от скорости на maxSlipSpeed и более
    /// (колесо «заблокировано»). Используется, чтобы тормозить на пределе сцепления, а не в юз.
    /// </summary>
    private float GetBrakingTraction(Vector3 relativeVelocity, Vector3 relativeSpin, Vector3 normal, float radius)
    {
        float speed = relativeVelocity.magnitude;
        if (speed < 0.3f)
            return 1f;

        Vector3 velocityDirection = relativeVelocity / speed;
        Vector3 rollAxis = Vector3.Cross(normal, velocityDirection);
        float spinSpeed = Vector3.Dot(relativeSpin, rollAxis) * radius;

        float slip = speed - spinSpeed; // >0: вращение отстало от скорости
        return 1f - Mathf.Clamp01(slip / maxSlipSpeed);
    }

    /// <summary>Угловое ускорение против вращения, не больше maxAlpha и без перерегулирования за один шаг.</summary>
    private Vector3 OpposeSpin(Vector3 spin, float maxAlpha)
    {
        float magnitude = spin.magnitude;
        if (magnitude < 0.001f)
            return Vector3.zero;

        float alpha = Mathf.Min(maxAlpha, magnitude / Time.fixedDeltaTime);
        return -spin / magnitude * alpha;
    }

    private void MoveInAir(Vector3 inputDirection)
    {
        if (inputDirection.sqrMagnitude < InputDeadZone * InputDeadZone)
            return;

        float slopeFactor = slopeSlide != null ? slopeSlide.SlopeControlFactor : 1f;
        rb.AddForce(inputDirection * airControlForce * airControlFactor * slopeFactor,
            ForceMode.Acceleration);
    }
}

/*
 * НАСТРОЙКА В UNITY (без этого шар не будет нормально ехать):
 *
 * 1. Physics Material (в Unity 6 — PhysicsMaterial) на коллайдер шара и на пол/платформы:
 *      Dynamic Friction = 0.8..1,  Static Friction = 0.8..1,  Bounciness = 0,
 *      Friction Combine = Maximum.
 * 2. Rigidbody шара: Linear Damping = 0, Angular Damping = 0.05, Use Gravity = on.
 *    Сопротивление качению теперь задаётся полем rollingResistance в контроллере.
 * 3. Если driveAcceleration больше ~ (трение × 9.81), шар будет буксовать на месте — это нормально
 *    для честной физики. Подбирайте driveAcceleration под трение.
 * 4. У дочерних коллайдеров (пластины/поршень) не должно быть лишнего веса — они меняют момент инерции
 *    шара. Если разгон «не тот», подправьте rollingInertiaFactor.
 */