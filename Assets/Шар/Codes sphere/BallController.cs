using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody), typeof(GroundCheck))]
public class BallController : MonoBehaviour
{
    [Header("References")]
    public Transform cameraTransform;

    [Header("Rolling Drive")]
    [Tooltip("Максимальная скорость качения (м/с).")]
    [Min(0f)] public float maxSpeed = 10f;
    [Tooltip("Желаемое линейное ускорение от привода (м/с²).")]
    [Min(0f)] public float driveAcceleration = 6f;
    [Tooltip("Допустимое проскальзывание (м/с). Если выше - шар буксует.")]
    [Min(0.05f)] public float maxSlipSpeed = 0.8f;
    [Tooltip("Ускорение гашения скорости при реверсе (м/с²).")]
    [Min(0f)] public float reverseAcceleration = 14f;
    [Tooltip("Боковое сцепление (1/с).")]
    [Min(0f)] public float lateralGrip = 3f;
    [Tooltip("Ручное торможение по качению (м/с²).")]
    [Min(0f)] public float brakeAcceleration = 20f;
    [Tooltip("Сопротивление качению при свободном выбеге на обычной поверхности.")]
    [Min(0f)] public float rollingResistance = 1.5f;
    [Tooltip("Минимальное замедление при финальной остановке на обычной поверхности (м/с²).")]
    [Min(0.05f)] public float minStopDeceleration = 0.8f;

    [Header("Platform Hold")]
    [Tooltip("Плавное замедление выбега на платформе (м/с²), чтобы шар катился по инерции, но успевал остановиться до края.")]
    [Min(0.5f)] public float platformCoastDeceleration = 5f;
    [Tooltip("Относительная скорость (м/с), ниже которой выбег заканчивается и включается фиксация на платформе.")]
    [Min(0.05f)] public float settleSpeed = 0.25f;
    [Tooltip("Время плавного входа в фиксацию (сек).")]
    [Min(0f)] public float holdEngageTime = 0.15f;
    [Tooltip("Смещение от точки якоря (м), при котором фиксация срывается (например, от внешнего удара).")]
    [Min(0.05f)] public float holdBreakawayDistance = 0.45f;
    [Tooltip("Жёсткость удержания точки на платформе (1/с).")]
    [Min(0f)] public float anchorStiffness = 25f;
    [Tooltip("Максимальная скорость доводки до точки якоря (м/с).")]
    [Min(0f)] public float maxAnchorCorrectionSpeed = 8f;

    [Header("Ball")]
    [Min(0.01f)] public float ballRadius = 0.5f;
    [Min(1f)] public float maxAngularVelocity = 25f;
    [Tooltip("1 + I/(m*r^2). Для сплошного шара = 1.4.")]
    [Min(1f)] public float rollingInertiaFactor = 1.4f;

    [Header("Air Control")]
    [Range(0f, 1f)] public float airControlFactor = 0.2f;
    [Min(0f)] public float airControlForce = 20f;

    /// <summary>Скорость платформы в точке шара. Используется, в частности, BallJump.</summary>
    public Vector3 PlatformVelocity { get; private set; }

    private const float InputDeadZone = 0.01f;

    private Rigidbody rb;
    private GroundCheck ground;
    private BallInputActions inputActions;

    private Vector2 moveInput;
    private bool isBraking;

    private IPlatformVelocityProvider collisionPlatform;
    private IPlatformVelocityProvider activePlatform;
    private Transform activePlatformTransform;

    private bool hasPrevPlatformFrame;
    private Quaternion prevPlatformRotation;
    private Vector3 prevPlatformVelocity;
    private Vector3 prevPlatformAngularVelocity;

    private Vector3 anchorLocalPos;
    private bool hasAnchor;
    private float holdBlend;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        ground = GetComponent<GroundCheck>();
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
        ResetPlatformState();
    }

    private void OnMovePerformed(InputAction.CallbackContext context) => moveInput = context.ReadValue<Vector2>();
    private void OnMoveCanceled(InputAction.CallbackContext context) => moveInput = Vector2.zero;
    private void OnBrakePerformed(InputAction.CallbackContext context) => isBraking = true;
    private void OnBrakeCanceled(InputAction.CallbackContext context) => isBraking = false;

    private void OnCollisionStay(Collision collision)
    {
        var platform = collision.gameObject.GetComponentInParent<IPlatformVelocityProvider>();
        if (platform != null)
            collisionPlatform = platform;
    }

    private void OnCollisionExit(Collision collision)
    {
        if (collisionPlatform == null)
            return;

        var exited = collision.gameObject.GetComponentInParent<IPlatformVelocityProvider>();
        if (ReferenceEquals(exited, collisionPlatform))
            collisionPlatform = null;
    }

    private void FixedUpdate()
    {
        if (cameraTransform == null)
            return;

        IPlatformVelocityProvider currentPlatform = ResolveActivePlatform();
        if (!ReferenceEquals(currentPlatform, activePlatform))
        {
            activePlatform = currentPlatform;
            activePlatformTransform = (activePlatform as Component)?.transform;
            hasPrevPlatformFrame = false;
            ReleaseAnchor();
        }

        Vector3 platformVelocity = Vector3.zero;
        Vector3 platformAngularVelocity = Vector3.zero;

        if (ground.IsGrounded && activePlatform != null)
        {
            platformVelocity = activePlatform.GetVelocityAtPoint(rb.position);
            platformAngularVelocity = activePlatform.GetAngularVelocity();

            CarryWithPlatformFrame(platformVelocity, platformAngularVelocity);
        }
        else
        {
            hasPrevPlatformFrame = false;
        }

        PlatformVelocity = platformVelocity;
        Vector3 inputDirection = GetCameraRelativeInput();

        if (ground.IsGrounded)
        {
            RollOnGround(inputDirection, platformVelocity, platformAngularVelocity);
        }
        else
        {
            ReleaseAnchor();
            MoveInAir(inputDirection);
        }

        if (ground.IsGrounded && activePlatform != null)
        {
            prevPlatformVelocity = activePlatform.GetVelocityAtPoint(rb.position);
            prevPlatformAngularVelocity = activePlatform.GetAngularVelocity();
            if (activePlatformTransform != null)
                prevPlatformRotation = activePlatformTransform.rotation;
            hasPrevPlatformFrame = true;
        }
    }

    private void CarryWithPlatformFrame(Vector3 platformVelocity, Vector3 platformAngularVelocity)
    {
        if (!hasPrevPlatformFrame)
            return;

        Quaternion deltaRot = Quaternion.identity;
        if (activePlatformTransform != null)
        {
            deltaRot = activePlatformTransform.rotation * Quaternion.Inverse(prevPlatformRotation);
        }
        else if (prevPlatformAngularVelocity.sqrMagnitude > 0.000001f)
        {
            float angleDeg = prevPlatformAngularVelocity.magnitude * Mathf.Rad2Deg * Time.fixedDeltaTime;
            deltaRot = Quaternion.AngleAxis(angleDeg, prevPlatformAngularVelocity.normalized);
        }

        Vector3 prevRelVel = rb.linearVelocity - prevPlatformVelocity;
        rb.linearVelocity = platformVelocity + (deltaRot * prevRelVel);

        Vector3 prevRelSpin = rb.angularVelocity - prevPlatformAngularVelocity;
        rb.angularVelocity = platformAngularVelocity + (deltaRot * prevRelSpin);
    }

    private IPlatformVelocityProvider ResolveActivePlatform()
    {
        if (!ground.IsGrounded)
            return null;

        if (ground.GroundCollider != null)
        {
            var provider = ground.GroundCollider.GetComponentInParent<IPlatformVelocityProvider>();
            if (provider != null)
                return provider;
        }

        return collisionPlatform;
    }

    private void ReleaseAnchor()
    {
        hasAnchor = false;
        holdBlend = 0f;
    }

    private void ResetPlatformState()
    {
        collisionPlatform = null;
        activePlatform = null;
        activePlatformTransform = null;
        hasPrevPlatformFrame = false;
        ReleaseAnchor();
        PlatformVelocity = Vector3.zero;
    }

    private Vector3 GetCameraRelativeInput()
    {
        Vector3 forward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up);
        if (forward.sqrMagnitude < 0.0001f)
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

        float inputMagnitude = inputDirection.magnitude;
        Vector3 direction = Vector3.ProjectOnPlane(inputDirection, normal);
        bool hasInput = inputMagnitude > InputDeadZone && direction.sqrMagnitude > InputDeadZone * InputDeadZone;

        float radius = Mathf.Max(0.01f, ballRadius);
        float toAngular = rollingInertiaFactor / radius;

        Vector3 relativeVelocity = Vector3.ProjectOnPlane(rb.linearVelocity - platformVelocity, normal);
        Vector3 relativeSpin = rb.angularVelocity - platformAngularVelocity;

        // Если шар уже свесился центром за край платформы (!IsCenterOverGround), якорь не держит его от падения
        bool safelyOnSurface = ground.IsCenterOverGround && !ground.IsSteepSlope();

        if (hasInput || !safelyOnSurface || activePlatform == null)
        {
            ReleaseAnchor();
        }
        else
        {
            if (TryHoldAtRestOnPlatform(normal, platformVelocity, platformAngularVelocity, relativeVelocity, relativeSpin, radius))
                return;
        }

        Vector3 angularAcceleration;

        if (isBraking && safelyOnSurface)
        {
            float speed = relativeVelocity.magnitude;
            float spinSpeed = relativeSpin.magnitude * radius;
            if (activePlatform == null && speed <= 0.01f && spinSpeed <= 0.01f)
            {
                rb.linearVelocity = Vector3.Project(rb.linearVelocity, normal);
                rb.angularVelocity = Vector3.zero;
                return;
            }

            float absFactor = GetBrakingTraction(relativeVelocity, relativeSpin, normal, radius);
            angularAcceleration = OpposeSpin(relativeSpin, brakeAcceleration * toAngular * absFactor);
        }
        else if (hasInput)
        {
            direction.Normalize();
            Vector3 rollAxis = Vector3.Cross(normal, direction);

            float targetSpeed = maxSpeed * inputMagnitude;
            float speedAlong = Vector3.Dot(relativeVelocity, direction);
            float spinSpeed = Vector3.Dot(relativeSpin, rollAxis) * radius;
            float rollSpeed = Mathf.Max(speedAlong, spinSpeed);

            float headroom = Mathf.Clamp01((targetSpeed - rollSpeed) / Mathf.Max(0.5f, targetSpeed * 0.15f));
            float acceleration = speedAlong < -0.25f ? reverseAcceleration : driveAcceleration;

            float slip = spinSpeed - speedAlong;
            float tractionFactor = 1f - Mathf.Clamp01(slip / maxSlipSpeed);

            Vector3 drive = rollAxis * (acceleration * headroom * tractionFactor * toAngular);

            if (slip > maxSlipSpeed)
                drive -= rollAxis * ((slip - maxSlipSpeed) / radius * 10f);

            Vector3 lateral = relativeVelocity - direction * speedAlong;
            Vector3 lateralCorrection = Vector3.zero;
            if (lateralGrip > 0f && lateral.sqrMagnitude > 0.0025f)
            {
                lateralCorrection = -Vector3.Cross(normal, lateral) * (lateralGrip * rollingInertiaFactor / radius);
                lateralCorrection = Vector3.ClampMagnitude(lateralCorrection, reverseAcceleration * toAngular);
            }

            angularAcceleration = drive + lateralCorrection;
        }
        else if (activePlatform != null && safelyOnSurface)
        {
            Vector3 normalVel = Vector3.Project(rb.linearVelocity, normal);
            Vector3 platformPlanarVel = Vector3.ProjectOnPlane(platformVelocity, normal);

            Vector3 nextRelVel = Vector3.MoveTowards(
                relativeVelocity,
                Vector3.zero,
                platformCoastDeceleration * Time.fixedDeltaTime
            );

            rb.linearVelocity = normalVel + platformPlanarVel + nextRelVel;

            Vector3 targetRelSpin = Vector3.Cross(normal, nextRelVel) / radius;
            rb.angularVelocity = platformAngularVelocity + Vector3.MoveTowards(
                relativeSpin,
                targetRelSpin,
                platformCoastDeceleration * toAngular * Time.fixedDeltaTime
            );
            return;
        }
        else if (safelyOnSurface)
        {
            float speed = relativeVelocity.magnitude;
            float spinSpeed = relativeSpin.magnitude * radius;
            float maxCurrentSpeed = Mathf.Max(speed, spinSpeed);
            Vector3 normalVel = Vector3.Project(rb.linearVelocity, normal);

            if (maxCurrentSpeed <= 0.005f)
            {
                rb.linearVelocity = normalVel;
                rb.angularVelocity = Vector3.zero;
                return;
            }

            float transitionSpeed = Mathf.Max(0.3f, minStopDeceleration / Mathf.Max(0.01f, rollingResistance));
            if (maxCurrentSpeed <= transitionSpeed)
            {
                float decel = Mathf.Max(minStopDeceleration, speed * rollingResistance);
                Vector3 nextVel = Vector3.MoveTowards(relativeVelocity, Vector3.zero, decel * Time.fixedDeltaTime);
                rb.linearVelocity = normalVel + nextVel;

                Vector3 targetSpin = Vector3.Cross(normal, nextVel) / radius;
                rb.angularVelocity = Vector3.MoveTowards(
                    rb.angularVelocity,
                    targetSpin,
                    decel * toAngular * Time.fixedDeltaTime
                );
                return;
            }

            float absFactor = GetBrakingTraction(relativeVelocity, relativeSpin, normal, radius);
            angularAcceleration = -relativeSpin * (rollingResistance * absFactor);
        }
        else
        {
            return;
        }

        rb.AddTorque(angularAcceleration, ForceMode.Acceleration);
    }

    private bool TryHoldAtRestOnPlatform(
        Vector3 normal,
        Vector3 platformVelocity,
        Vector3 platformAngularVelocity,
        Vector3 relativeVelocity,
        Vector3 relativeSpin,
        float radius)
    {
        if (activePlatformTransform == null)
            return false;

        if (!hasAnchor)
        {
            float linearRelSpeed = relativeVelocity.magnitude;
            float spinRelSpeed = relativeSpin.magnitude * radius;

            if (linearRelSpeed > settleSpeed || spinRelSpeed > settleSpeed)
                return false;

            hasAnchor = true;
            holdBlend = 0f;
            anchorLocalPos = activePlatformTransform.InverseTransformPoint(rb.position);
        }

        holdBlend = holdEngageTime > 0.001f
            ? Mathf.MoveTowards(holdBlend, 1f, Time.fixedDeltaTime / holdEngageTime)
            : 1f;

        Vector3 targetWorldPos = activePlatformTransform.TransformPoint(anchorLocalPos);
        Vector3 planarError = Vector3.ProjectOnPlane(targetWorldPos - rb.position, normal);

        if (planarError.sqrMagnitude > holdBreakawayDistance * holdBreakawayDistance)
        {
            ReleaseAnchor();
            return false;
        }

        Vector3 normalVel = Vector3.Project(rb.linearVelocity, normal);
        Vector3 platformPlanarVel = Vector3.ProjectOnPlane(platformVelocity, normal);
        Vector3 correctionVel = Vector3.ClampMagnitude(planarError * anchorStiffness, maxAnchorCorrectionSpeed);

        Vector3 targetPlanarVel = platformPlanarVel + correctionVel;
        Vector3 currentPlanarVel = Vector3.ProjectOnPlane(rb.linearVelocity, normal);

        rb.linearVelocity = normalVel + Vector3.Lerp(currentPlanarVel, targetPlanarVel, holdBlend);
        rb.angularVelocity = Vector3.Lerp(rb.angularVelocity, platformAngularVelocity, holdBlend);

        return true;
    }

    private float GetBrakingTraction(Vector3 relativeVelocity, Vector3 relativeSpin, Vector3 normal, float radius)
    {
        float speed = relativeVelocity.magnitude;
        if (speed < 0.3f)
            return 1f;

        Vector3 velocityDirection = relativeVelocity / speed;
        Vector3 rollAxis = Vector3.Cross(normal, velocityDirection);
        float spinSpeed = Vector3.Dot(relativeSpin, rollAxis) * radius;

        float slip = speed - spinSpeed;
        return 1f - Mathf.Clamp01(slip / maxSlipSpeed);
    }

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

        rb.AddForce(inputDirection * airControlForce * airControlFactor,
            ForceMode.Acceleration);
    }
}