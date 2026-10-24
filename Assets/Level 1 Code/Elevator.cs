using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class ElevatorPingPong : MonoBehaviour
{
    [Header("Elevator Settings")]
    public Transform topPoint;
    public Transform bottomPoint;
    public float moveSpeed = 2f;     // скорость движени€
    public float waitTime = 1f;      // пауза на крайних точках

    [Header("Obstacle detection")]
    public Transform sensorAnchor;   // €корь сенсора под лифтом
    public Vector3 sensorSize = new Vector3(0.9f, 0.9f, 0.9f);
    public float stopOffset = 0.06f;
    public string ballTag = "Ball";

    private Rigidbody rb;
    private float timer;
    private float waitTimer;
    private bool movingUp = true; // направление движени€

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    void FixedUpdate()
    {
        // если шар под лифтом Ч стоим
        if (IsBallAhead()) return;

        // если ждЄм на точке
        if (waitTimer > 0f)
        {
            waitTimer -= Time.fixedDeltaTime;
            return;
        }

        // движение вверх-вниз
        float direction = movingUp ? 1f : -1f;
        timer += Time.fixedDeltaTime * moveSpeed * direction;

        // ограничиваем t от 0 до 1
        float t = Mathf.Clamp01(timer);
        Vector3 newPos = Vector3.Lerp(bottomPoint.position, topPoint.position, t);
        rb.MovePosition(newPos);

        // если достигли кра€ Ч пауза и смена направлени€
        if (t <= 0f || t >= 1f)
        {
            waitTimer = waitTime;
            movingUp = !movingUp;
        }
    }

    private bool IsBallAhead()
    {
        if (sensorAnchor == null) return false;

        Vector3 sensorCenter = sensorAnchor.position + Vector3.down * stopOffset;

        Collider[] hits = Physics.OverlapBox(
            sensorCenter,
            sensorSize * 0.5f,
            Quaternion.identity
        );

        foreach (var hit in hits)
        {
            if (hit.CompareTag(ballTag))
                return true;
        }

        return false;
    }

    void OnDrawGizmosSelected()
    {
        if (sensorAnchor != null)
        {
            Gizmos.color = Color.yellow;
            Vector3 sensorCenter = sensorAnchor.position + Vector3.down * stopOffset;
            Gizmos.DrawWireCube(sensorCenter, sensorSize);
        }
    }
}

