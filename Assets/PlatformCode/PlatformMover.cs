using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlatformMover : MonoBehaviour, IPlatformVelocityProvider
{
    [Header("Waypoints")]
    public Transform pointA;
    public Transform pointB;

    [Header("Movement")]
    public float speed = 3f;
    public bool startFromA = true;
    public bool loop = true;

    private Rigidbody rb;
    private Vector3 targetPosition;
    private bool movingToB;
    private Vector3 currentVelocity;

    // IPlatformVelocityProvider: the ball rides along with this platform
    public Vector3 GetVelocityAtPoint(Vector3 worldPoint) => currentVelocity;
    public Vector3 GetAngularVelocity() => Vector3.zero;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    void Start()
    {
        if (pointA == null || pointB == null)
        {
            Debug.LogError("PlatformMover: точки A и B не назначены!");
            enabled = false;
            return;
        }

        if (startFromA)
        {
            transform.position = pointA.position;
            movingToB = true;
            targetPosition = pointB.position;
        }
        else
        {
            transform.position = pointB.position;
            movingToB = false;
            targetPosition = pointA.position;
        }
    }

    void FixedUpdate()
    {
        Vector3 newPos = Vector3.MoveTowards(transform.position, targetPosition, speed * Time.fixedDeltaTime);
        currentVelocity = (newPos - transform.position) / Time.fixedDeltaTime;
        rb.MovePosition(newPos);

        if (Vector3.Distance(transform.position, targetPosition) < 0.01f)
        {
            if (loop)
            {
                movingToB = !movingToB;
                targetPosition = movingToB ? pointB.position : pointA.position;
            }
            else
            {
                currentVelocity = Vector3.zero;
                enabled = false;
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        if (pointA != null && pointB != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(pointA.position, pointB.position);
            Gizmos.DrawWireSphere(pointA.position, 0.2f);
            Gizmos.DrawWireSphere(pointB.position, 0.2f);
        }
    }
}