using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody), typeof(PlateController), typeof(GroundCheck))]
public class BallJump : MonoBehaviour
{
    [Header("━━━ ОСНОВНОЕ ━━━")]
    [SerializeField, Min(0.1f)] private float jumpHeight = 4f;
    [SerializeField, Min(0f)] private float maxHorizontalSpeedAtJump = 12f;

    [Header("━━━ УПРАВЛЕНИЕ ━━━")]
    [SerializeField, Range(0f, 0.5f)] private float coyoteTime = 0f;
    [SerializeField, Range(0f, 0.5f)] private float jumpBufferTime = 0.15f;
    [SerializeField, Range(0f, 2f)] private float jumpCooldown = 0.1f;

    [Header("━━━ ПОРШЕНЬ (КОЛОННА) ━━━")]
    [Tooltip("Максимальная длина выдвижения как доля от высоты прыжка.")]
    [SerializeField, Range(0.05f, 0.6f)] private float pistonStrokeRatio = 0.35f;

    [Tooltip("Синхронизация с высотой шара. 1.0 = 1 к 1.")]
    [SerializeField, Range(0.3f, 2.5f)] private float syncFactor = 1.0f;

    [Tooltip("Максимальная скорость выдвижения (м/с).")]
    [SerializeField, Range(0.5f, 5f)] private float maxExtensionSpeed = 2.0f;

    [Tooltip("Доля времени падения, за которую колонна втягивается.\n\n" +
             "0.8 = втянется за 80% времени падения (20% на «уже втянута»)\n" +
             "1.0 = втянется ровно к приземлению\n" +
             "1.3 = ещё втягивается после приземления")]
    [SerializeField, Range(0.3f, 2f)] private float retractDurationFactor = 0.9f;

    [Tooltip("Пауза перед началом втягивания после пика (сек).")]
    [SerializeField, Range(0f, 0.5f)] private float holdAtPeak = 0.1f;

    [Header("━━━ ГРАВИТАЦИЯ ━━━")]
    [SerializeField, Range(0f, 60f)] private float extraGravityRise = 12f;
    [SerializeField, Range(0f, 100f)] private float extraGravityFall = 25f;

    [Header("━━━ ОТЛАДКА ━━━")]
    [SerializeField] private bool debugLogs = false;

    // ============================================================
    public float JumpHeight => jumpHeight;
    public float JumpVelocity => Mathf.Sqrt(2f * (9.81f + extraGravityRise) * jumpHeight);
    public float PistonStroke => jumpHeight * pistonStrokeRatio;
    public bool IsJumping => state != JumpState.Idle;

    private Rigidbody rb;
    private PlateController plates;
    private GroundCheck ground;
    private BallInputActions input;

    public enum JumpState { Idle, Extending, HoldingAtPeak, Retracting }
    private JumpState state = JumpState.Idle;

    private float stateStartTime;
    private float lastJumpEndTime = -999f;
    private float lastGroundedTime = -999f;
    private float lastJumpPressTime = -999f;
    private float jumpStartY = 0f;

    // ============================================================
    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        plates = GetComponent<PlateController>();
        ground = GetComponent<GroundCheck>();
        EnsureInput();

        if (ground == null) Debug.LogError("[BallJump] GroundCheck не найден!", this);
        if (plates == null) Debug.LogError("[BallJump] PlateController не найден!", this);

        plates.Configure(PistonStroke, 4f, 4f);
    }

    private void Start()
    {
        plates.Configure(PistonStroke, 4f, 4f);
    }

    private void OnValidate()
    {
        if (plates == null) plates = GetComponent<PlateController>();
        plates.Configure(PistonStroke, 4f, 4f);
    }

    private void OnEnable()
    {
        EnsureInput();
        if (input == null) return;
        input.Player.Jump.started += OnJumpStarted;
        input.Player.Enable();
    }

    private void OnDisable()
    {
        if (input == null) return;
        input.Player.Jump.started -= OnJumpStarted;
        input.Player.Disable();
    }

    private void EnsureInput()
    {
        if (input == null) input = new BallInputActions();
    }

    private void OnJumpStarted(InputAction.CallbackContext ctx)
    {
        lastJumpPressTime = Time.time;
    }

    // ============================================================
    private void Update()
    {
        if (plates == null || rb == null) return;

        if (ground != null && ground.IsGrounded)
            lastGroundedTime = Time.time;

        bool hasBufferedPress = (Time.time - lastJumpPressTime) <= jumpBufferTime;
        bool canJumpNow = CanJumpNow();

        if (hasBufferedPress && canJumpNow)
        {
            DoJump();
            lastJumpPressTime = -999f;
        }

        switch (state)
        {
            case JumpState.Extending:
                {
                    float heightAbove = rb.position.y - jumpStartY;
                    float rawTarget = Mathf.Clamp(heightAbove * syncFactor, 0f, PistonStroke);

                    float currentExt = plates.CurrentExtension;
                    float maxDelta = maxExtensionSpeed * Time.deltaTime;
                    float limitedTarget = Mathf.MoveTowards(currentExt, rawTarget, maxDelta);

                    plates.SetExtensionDirect(limitedTarget);

                    if (rb.linearVelocity.y <= 0f)
                    {
                        state = JumpState.HoldingAtPeak;
                        stateStartTime = Time.time;

                        if (debugLogs)
                            Debug.Log($"[BallJump] → HoldingAtPeak (ext={limitedTarget:F3})");
                    }
                    break;
                }

            case JumpState.HoldingAtPeak:
                {
                    plates.SetExtensionDirect(PistonStroke);

                    if (Time.time - stateStartTime >= holdAtPeak)
                    {
                        float heightAtPeak = rb.position.y - jumpStartY;
                        float fallGravity = 9.81f + extraGravityFall;
                        float fallTime = Mathf.Sqrt(2f * Mathf.Max(0.1f, heightAtPeak) / fallGravity);

                        float retractDuration = fallTime * retractDurationFactor;
                        plates.SetRetractDuration(retractDuration);
                        plates.StartRetract();

                        state = JumpState.Retracting;
                        stateStartTime = Time.time;

                        if (debugLogs)
                            Debug.Log($"[BallJump] → Retracting " +
                                      $"(высота пика={heightAtPeak:F2} м, " +
                                      $"t_падения={fallTime:F2} с, " +
                                      $"втягивание за {retractDuration:F2} с)");
                    }
                    break;
                }

            case JumpState.Retracting:
                {
                    float retractDuration = PistonStroke / Mathf.Max(0.001f, plates.RetractSpeed);
                    if (Time.time - stateStartTime >= retractDuration)
                    {
                        state = JumpState.Idle;
                        lastJumpEndTime = Time.time;

                        if (debugLogs) Debug.Log("[BallJump] → Idle");
                    }
                    break;
                }
        }
    }

    private bool CanJumpNow()
    {
        if (state != JumpState.Idle) return false;
        if (Time.time - lastJumpEndTime < jumpCooldown) return false;

        if (coyoteTime <= 0f)
            return ground != null && ground.IsGrounded;

        float sinceGrounded = Time.time - lastGroundedTime;
        return (ground != null && ground.IsGrounded) || sinceGrounded <= coyoteTime;
    }

    private void DoJump()
    {
        if (maxHorizontalSpeedAtJump > 0f)
        {
            Vector3 v = rb.linearVelocity;
            Vector3 horiz = new Vector3(v.x, 0f, v.z);
            if (horiz.magnitude > maxHorizontalSpeedAtJump)
            {
                Vector3 clamped = horiz.normalized * maxHorizontalSpeedAtJump;
                rb.linearVelocity = new Vector3(clamped.x, v.y, clamped.z);
            }
        }

        jumpStartY = rb.position.y;

        plates.Configure(PistonStroke, 4f, 4f);
        rb.AddForce(Vector3.up * JumpVelocity, ForceMode.VelocityChange);
        plates.SetExtensionDirect(0f);

        state = JumpState.Extending;
        stateStartTime = Time.time;

        if (debugLogs)
            Debug.Log($"[BallJump] Прыжок: v={JumpVelocity:F2} м/с, " +
                      $"stroke={PistonStroke:F3} м, h={jumpHeight} м");
    }

    private void FixedUpdate()
    {
        if (rb == null) return;
        float g = rb.linearVelocity.y > 0f ? extraGravityRise : extraGravityFall;
        rb.AddForce(Vector3.down * g, ForceMode.Acceleration);
    }
}