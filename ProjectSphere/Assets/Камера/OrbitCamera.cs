using UnityEngine;
using UnityEngine.InputSystem;

public class OrbitCamera : MonoBehaviour
{
    [Header("Target")]
    public Transform target;
    public float distance = 5f;
    public float minDistance = 1.5f;
    public float heightOffset = 2f;

    [Header("Orbit Settings")]
    public float xSpeed = 180f;
    public float ySpeed = 180f;
    public float yMinLimit = -20f;
    public float yMaxLimit = 80f;

    [Header("Collision Settings")]
    public LayerMask collisionLayers;
    public float collisionBuffer = 0.2f;
    public float sphereRadius = 0.3f;

    private BallInputActions inputActions;
    private float xAngle;
    private float yAngle;

    void Awake()
    {
        inputActions = new BallInputActions();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void OnEnable()
    {
        inputActions.Player.Look.performed += OnLook;
        inputActions.Player.Look.canceled += OnLook;
        inputActions.Player.Enable();
    }

    void OnDisable()
    {
        inputActions.Player.Disable();
    }

    void Start()
    {
        Vector3 angles = transform.eulerAngles;
        xAngle = angles.y;
        yAngle = angles.x;
    }

    private void OnLook(InputAction.CallbackContext ctx)
    {
        Vector2 input = ctx.ReadValue<Vector2>();
        xAngle += input.x * xSpeed * Time.deltaTime;
        yAngle -= input.y * ySpeed * Time.deltaTime;
        yAngle = Mathf.Clamp(yAngle, yMinLimit, yMaxLimit);
    }

    void LateUpdate()
    {
        if (!target) return;

        Quaternion rotation = Quaternion.Euler(yAngle, xAngle, 0f);
        Vector3 elevatedTarget = target.position + Vector3.up * heightOffset;
        Vector3 dir = rotation * Vector3.back;

        float targetDistance = distance;

        // --- Коллизии камеры ---
        if (Physics.SphereCast(
            elevatedTarget,
            sphereRadius,
            dir,
            out RaycastHit hit,
            distance,
            collisionLayers))
        {
            targetDistance = Mathf.Max(minDistance, hit.distance - collisionBuffer);
        }

        Vector3 finalPos = elevatedTarget + dir.normalized * targetDistance;

        transform.rotation = rotation;
        transform.position = finalPos;
    }
}
