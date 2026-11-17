using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody), typeof(GroundCheck), typeof(BallSlopeSlide))]
public class BallController : MonoBehaviour
{
    [Header("References")]
    public Transform cameraTransform;

    [Header("Ground Movement")]
    [Min(0f)] public float maxSpeed = 10f;
    [Tooltip("Максимальное ускорение при движении по земле (м/с²).")]
    [Min(0f)] public float groundAcceleration = 32f;
    [Tooltip("Ускорение торможения при отпускании клавиш (м/с²).")]
    [Min(0f)] public float groundDeceleration = 12f;
    [Tooltip("Дополнительное торможение при нажатии в противоположную сторону (м/с²).")]
    [Min(0f)] public float counterSteerAcceleration = 42f;
    [Tooltip("Скорость на движущейся платформе относительно её поверхности.")]
    [Range(0.1f, 1f)] public float platformSpeedScale = 0.6f;

    [Header("Rolling")]
    [Tooltip("Насколько быстро угловая скорость приводится к скорости чистого качения.")]
    [Min(0f)] public float rollingResponse = 28f;
    [Tooltip("Допустимый запас угловой скорости относительно чистого качения.")]
    [Range(1f, 2f)] public float rollingSpeedMargin = 1.2f;
    [Min(0.01f)] public float ballRadius = 0.5f;
    [Min(1f)] public float maxAngularVelocity = 25f;

    [Header("Air Control")]
    [Range(0f, 1f)] public float airControlFactor = 0.2f;
    [Min(0f)] public float airControlForce = 20f;

    [Header("Moving Platforms")]
    public bool platformRelativeControl = true;
    [Tooltip("Ускорение, с которым шар удерживает скорость платформы, когда игрок не нажимает клавиши.")]
    [Min(0f)] public float platformGripAcceleration = 18f;
    [Tooltip("Множитель центробежного сноса. 0 отключает снос.")]
    [Range(0f, 1f)] public float centrifugalDriftFactor = 0.7f;
    [Tooltip("Предел сцепления платформы (м/с²), после которого начинается центробежный снос.")]
    [Min(0f)] public float centrifugalGripLimit = 5f;

    /// <summary>Скорость платформы в точке шара. Используется, в частности, BallJump.</summary>
    public Vector3 PlatformVelocity { get; private set; }

    private const float InputDeadZone = 0.01f;

    private Rigidbody rb;
    private GroundCheck ground;
    private BallSlopeSlide slopeSlide;
    private BallInputActions inputActions;

    private Vector2 moveInput;
    private bool isBraking;
    private float defaultAngularDamping;
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
        defaultAngularDamping = rb.angularDamping;

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
    }

    private void OnMovePerformed(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
    }

    private void OnMoveCanceled(InputAction.CallbackContext context)
    {
        moveInput = Vector2.zero;
    }

    private void OnBrakePerformed(InputAction.CallbackContext context)
    {
        isBraking = true;
    }

    private void OnBrakeCanceled(InputAction.CallbackContext context)
    {
        isBraking = false;
    }

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

        Vector3 moveDirection = GetCameraRelativeDirection();
        if (ground.IsGrounded)
            MoveOnGround(moveDirection, platformVelocity, platformAngularVelocity);
        else
            MoveInAir(moveDirection);

        rb.maxAngularVelocity = maxAngularVelocity;
    }

    private Vector3 GetCameraRelativeDirection()
    {
        Vector3 forward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
        Vector3 right = Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized;
        Vector3 direction = forward * moveInput.y + right * moveInput.x;

        if (direction.sqrMagnitude > 1f)
            direction.Normalize();

        return direction;
    }

    private void MoveOnGround(Vector3 inputDirection, Vector3 platformVelocity,
        Vector3 platformAngularVelocity)
    {
        Vector3 groundNormal = ground.GroundNormal.sqrMagnitude > 0.001f
            ? ground.GroundNormal.normalized
            : Vector3.up;

        Vector3 desiredDirection = Vector3.ProjectOnPlane(inputDirection, groundNormal);
        if (desiredDirection.sqrMagnitude > 0.001f)
            desiredDirection.Normalize();

        bool hasInput = desiredDirection.sqrMagnitude > InputDeadZone * InputDeadZone;
        bool onMovingPlatform = platformRelativeControl && activePlatform != null;

        Vector3 relativeVelocity = rb.linearVelocity - platformVelocity;
        Vector3 currentRelativeTangent = Vector3.ProjectOnPlane(relativeVelocity, groundNormal);

        float speedScale = onMovingPlatform ? platformSpeedScale : 1f;
        Vector3 targetRelativeVelocity = hasInput
            ? desiredDirection * (maxSpeed * speedScale)
            : Vector3.zero;

        if (isBraking)
            targetRelativeVelocity = Vector3.zero;

        Vector3 velocityError = targetRelativeVelocity - currentRelativeTangent;
        float acceleration = GetMovementAcceleration(hasInput, velocityError, isBraking);

        // Управление скоростью, а не бесконечное добавление крутящего момента.
        // При нажатии назад ошибка скорости сразу меняет знак и активно гасит старое движение.
        Vector3 accelerationCommand = Vector3.ClampMagnitude(velocityError / Time.fixedDeltaTime,
            acceleration);
        if (onMovingPlatform && !hasInput && !isBraking)
        {
            accelerationCommand = Vector3.ClampMagnitude(
                -currentRelativeTangent / Time.fixedDeltaTime, platformGripAcceleration);
        }

        accelerationCommand *= slopeSlide.SlopeControlFactor;
        rb.AddForce(accelerationCommand, ForceMode.Acceleration);

        if (centrifugalDriftFactor > 0f && onMovingPlatform)
            ApplyCentrifugalDrift(platformVelocity, platformAngularVelocity);

        ApplyRollingControl(targetRelativeVelocity, groundNormal, platformAngularVelocity,
            hasInput || isBraking);
    }

    private float GetMovementAcceleration(bool hasInput, Vector3 velocityError, bool braking)
    {
        if (braking)
            return counterSteerAcceleration;

        if (!hasInput)
            return groundDeceleration;

        Vector3 currentVelocity = Vector3.ProjectOnPlane(rb.linearVelocity - PlatformVelocity,
            ground.GroundNormal);
        bool reversing = currentVelocity.sqrMagnitude > 0.04f &&
                         Vector3.Dot(currentVelocity, velocityError) < 0f;

        return reversing ? counterSteerAcceleration : groundAcceleration;
    }

    private void ApplyRollingControl(Vector3 targetRelativeVelocity, Vector3 groundNormal,
        Vector3 platformAngularVelocity, bool activelyControlled)
    {
        float radius = Mathf.Max(0.01f, ballRadius);
        Vector3 targetRollingAngularVelocity =
            Vector3.Cross(targetRelativeVelocity, groundNormal) / radius;

        Vector3 targetAngularVelocity = platformAngularVelocity + targetRollingAngularVelocity;
        Vector3 angularVelocityError = targetAngularVelocity - rb.angularVelocity;

        float response = activelyControlled ? rollingResponse : rollingResponse * 0.65f;
        rb.AddTorque(angularVelocityError * response, ForceMode.Acceleration);

        float maxRollingSpin = (targetRelativeVelocity.magnitude / radius) * rollingSpeedMargin;
        Vector3 relativeSpin = rb.angularVelocity - platformAngularVelocity;
        if (relativeSpin.magnitude > maxRollingSpin + 0.5f)
        {
            Vector3 clampedSpin = relativeSpin.normalized * (maxRollingSpin + 0.5f);
            Vector3 excessSpin = relativeSpin - clampedSpin;
            rb.AddTorque(-excessSpin * response, ForceMode.Acceleration);
        }
    }

    private void ApplyCentrifugalDrift(Vector3 platformVelocity, Vector3 platformAngularVelocity)
    {
        if (platformAngularVelocity.sqrMagnitude < 0.0001f)
            return;

        Vector3 radial = Vector3.Cross(platformVelocity, platformAngularVelocity) /
                         platformAngularVelocity.sqrMagnitude;
        float radius = radial.magnitude;
        if (radius < 0.05f)
            return;

        float centrifugalAcceleration = platformAngularVelocity.sqrMagnitude * radius;
        if (centrifugalAcceleration * centrifugalDriftFactor <= centrifugalGripLimit)
            return;

        rb.AddForce(radial / radius *
                    (centrifugalAcceleration * centrifugalDriftFactor), ForceMode.Acceleration);
    }

    private void MoveInAir(Vector3 moveDirection)
    {
        rb.angularDamping = defaultAngularDamping;

        if (moveDirection.sqrMagnitude < InputDeadZone * InputDeadZone)
            return;

        float slopeFactor = slopeSlide != null ? slopeSlide.SlopeControlFactor : 1f;
        rb.AddForce(moveDirection * airControlForce * airControlFactor * slopeFactor,
            ForceMode.Acceleration);
    }
}
