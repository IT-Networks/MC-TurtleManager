using UnityEngine;

/// <summary>
/// Tracks camera movement direction and velocity for chunk loading prioritization
/// </summary>
public class CameraMovementTracker : MonoBehaviour
{
    [Header("Movement Tracking")]
    [Tooltip("Minimum movement speed to be considered 'moving' (units/second)")]
    public float movementThreshold = 0.1f;

    [Tooltip("How long to average movement over (seconds)")]
    public float movementSmoothTime = 0.3f;

    [Header("Debug")]
    public bool showDebugInfo = false;

    // Movement data
    private Vector3 lastPosition;
    private Vector3 currentVelocity;
    private Vector3 smoothedDirection;
    private float currentSpeed;
    private bool isMoving;

    // Smoothing
    private Vector3 velocitySmooth;

    public Vector3 MovementDirection => smoothedDirection;
    public float Speed => currentSpeed;
    public bool IsMoving => isMoving;
    public Vector3 Velocity => currentVelocity;

    void Start()
    {
        lastPosition = transform.position;
    }

    void LateUpdate()
    {
        UpdateMovement();
    }

    void UpdateMovement()
    {
        // Calculate current velocity
        Vector3 currentPosition = transform.position;
        Vector3 delta = currentPosition - lastPosition;
        currentVelocity = delta / Time.deltaTime;

        // Smooth velocity
        Vector3 targetVelocity = currentVelocity;
        smoothedDirection = Vector3.SmoothDamp(smoothedDirection, targetVelocity.normalized, ref velocitySmooth, movementSmoothTime);

        // Calculate speed
        currentSpeed = currentVelocity.magnitude;

        // Determine if moving
        isMoving = currentSpeed > movementThreshold;

        // Store for next frame
        lastPosition = currentPosition;

        // Debug info
        if (showDebugInfo && isMoving)
        {
            Debug.Log($"Camera moving: {smoothedDirection.normalized * currentSpeed:F2} (speed: {currentSpeed:F2})");
        }
    }

    /// <summary>
    /// Gets the primary movement direction (forward/back/left/right/up/down)
    /// </summary>
    public Vector3 GetPrimaryDirection()
    {
        if (!isMoving) return Vector3.zero;

        Vector3 dir = smoothedDirection;
        Vector3 result = Vector3.zero;

        // Find dominant axis
        float maxAbs = Mathf.Max(Mathf.Abs(dir.x), Mathf.Abs(dir.y), Mathf.Abs(dir.z));

        if (Mathf.Abs(dir.x) == maxAbs)
            result = dir.x > 0 ? Vector3.right : Vector3.left;
        else if (Mathf.Abs(dir.z) == maxAbs)
            result = dir.z > 0 ? Vector3.forward : Vector3.back;
        else if (Mathf.Abs(dir.y) == maxAbs)
            result = dir.y > 0 ? Vector3.up : Vector3.down;

        return result;
    }

    /// <summary>
    /// Gets angle between movement direction and a given direction (0-180 degrees)
    /// </summary>
    public float GetAngleToDirection(Vector3 direction)
    {
        if (!isMoving) return 0f;

        // Ignore Y component for horizontal angle
        Vector3 movementFlat = new Vector3(smoothedDirection.x, 0, smoothedDirection.z).normalized;
        Vector3 directionFlat = new Vector3(direction.x, 0, direction.z).normalized;

        if (movementFlat == Vector3.zero || directionFlat == Vector3.zero)
            return 90f; // Perpendicular if no horizontal movement

        return Vector3.Angle(movementFlat, directionFlat);
    }

    void OnDrawGizmos()
    {
        if (!showDebugInfo || !Application.isPlaying) return;

        if (isMoving)
        {
            // Draw movement direction
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(transform.position, smoothedDirection.normalized * 5f);

            // Draw velocity magnitude as sphere size
            Gizmos.color = new Color(0, 1, 1, 0.3f);
            Gizmos.DrawSphere(transform.position + smoothedDirection.normalized * 5f, currentSpeed * 0.5f);
        }
    }
}
