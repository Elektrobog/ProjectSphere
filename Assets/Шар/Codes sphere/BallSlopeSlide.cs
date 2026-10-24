using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(GroundCheck))]
public class BallSlopeSlide : MonoBehaviour
{
    [Tooltip("Угол, начиная с которого включается скольжение. ДОЛЖЕН быть меньше " +
             "GroundCheck.MaxGroundAngle — иначе шар перестаёт считаться заземлённым " +
             "раньше, чем должен начать скользить, и это условие никогда не сработает.")]
    public float maxSlopeAngle = 40f;
    public float slideForce = 20f;
    public float controlReduction = 0.5f;
    public float maxSlideSpeed = 15f;

    private Rigidbody rb;
    private GroundCheck ground;

    public float SlopeControlFactor { get; private set; } = 1f;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        ground = GetComponent<GroundCheck>();
        ValidateAgainstGroundCheck();
    }

    void OnValidate()
    {
        if (ground == null) ground = GetComponent<GroundCheck>();
        ValidateAgainstGroundCheck();
    }

    private void ValidateAgainstGroundCheck()
    {
        if (ground == null) return;
        if (maxSlopeAngle >= ground.MaxGroundAngle)
        {
            Debug.LogWarning(
                $"[{name}] BallSlopeSlide.maxSlopeAngle ({maxSlopeAngle}°) >= " +
                $"GroundCheck.MaxGroundAngle ({ground.MaxGroundAngle}°). " +
                "Скольжение никогда не включится, т.к. IsGrounded станет false раньше, " +
                "чем угол достигнет порога скольжения. Уменьшите maxSlopeAngle или " +
                "увеличьте maxGroundAngle в GroundCheck.", this);
        }
    }

    void FixedUpdate()
    {
        if (!ground.IsGrounded)
        {
            SlopeControlFactor = 1f;
            return;
        }

        float angle = Vector3.Angle(ground.GroundNormal, Vector3.up);
        if (angle <= maxSlopeAngle)
        {
            SlopeControlFactor = 1f;
            return;
        }

        SlopeControlFactor = controlReduction;

        Vector3 slopeDir = Vector3.ProjectOnPlane(Vector3.down, ground.GroundNormal);
        if (slopeDir.magnitude < 0.01f) return;
        slopeDir.Normalize();

        // Полная скорость вдоль склона
        float speedAlongSlope = Vector3.Dot(rb.linearVelocity, slopeDir);
        if (speedAlongSlope < maxSlideSpeed)
        {
            rb.AddForce(slopeDir * slideForce, ForceMode.Force);
        }
        else
        {
            // Притормаживаем, если превысили лимит
            Vector3 excessVel = slopeDir * (speedAlongSlope - maxSlideSpeed);
            rb.AddForce(-excessVel * slideForce * 0.5f, ForceMode.Force);
        }
    }
}