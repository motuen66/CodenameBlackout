using UnityEngine;
using System.Collections; // Required for Coroutines
using System.Collections.Generic; // Required for List<T>

public class GuardPatrol : MonoBehaviour
{
    // --- Public Variables (Inspector Configuration) ---
    public List<Transform> waypoints; // Danh sách các điểm tuần tra của lính canh
    public float moveSpeed = 1.5f;       // Tốc độ di chuyển của lính canh
    public float waitTime = 1f;          // Thời gian lính canh dừng lại ở mỗi điểm
    public float waypointThreshold = 0.05f; // Khoảng cách đủ gần để coi là đã đến waypoint

    [Header("Detection")]
    public float detectionRadius = 0.5f; // Phạm vi phát hiện người chơi (hình tròn)
    public LayerMask playerLayer;        // Layer của người chơi (ví dụ: "Player" layer)

    // --- Private Variables ---
    private Rigidbody2D rb;          // Tham chiếu đến Rigidbody2D component
    private Animator animator;        // Tham chiếu đến Animator component
    private int currentWaypointIndex = 0; // Chỉ số của waypoint mục tiêu hiện tại
    private bool isWaiting = false; // Cờ báo hiệu lính canh đang chờ tại waypoint

    // --- Unity Lifecycle Methods ---
    void Awake()
    {
        // Lấy tham chiếu đến các component cần thiết
        rb = GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            Debug.LogError("Error: Rigidbody2D component not found on guard " + gameObject.name + ". Please add a Rigidbody2D.", this);
        }

        animator = GetComponent<Animator>();
        if (animator == null)
        {
            Debug.LogError("Error: Animator component not found on guard " + gameObject.name + ". Please add an Animator and assign a Controller.", this);
        }
    }

    void Start()
    {
        // Basic validation for waypoints
        if (waypoints == null || waypoints.Count == 0)
        {
            Debug.LogError("Error: No waypoints assigned to guard " + gameObject.name + ". Please assign waypoints in the Inspector.", this);
            enabled = false; // Disable script if no waypoints
            return;
        }

        // Basic validation for components
        if (rb == null || animator == null)
        {
            enabled = false; // Disable script if essential components are missing
            return;
        }

        // Ensure Rigidbody2D is configured correctly for kinematic movement.
        // These settings are crucial for physics interaction, even when movement is manually controlled.
        if (rb.bodyType != RigidbodyType2D.Kinematic)
        {
            Debug.LogWarning("Rigidbody2D Body Type for " + gameObject.name + " was not Kinematic. Setting to Kinematic.", this);
            rb.bodyType = RigidbodyType2D.Kinematic;
        }
        rb.gravityScale = 0; // Ensure no gravity affects the 2D guard
        rb.constraints = RigidbodyConstraints2D.FreezeRotation; // Freeze Z rotation to prevent accidental rotation
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous; // Essential for reliable collision detection (prevents tunneling)
        rb.interpolation = RigidbodyInterpolation2D.Interpolate; // Improves visual smoothness of movement

        // Initialize Animator parameters for initial idle state
        animator.SetBool("IsMoving", false);
        animator.SetFloat("MoveX", 0);
        animator.SetFloat("MoveY", -1); // Default idle direction (e.g., looking down)
        animator.SetFloat("LastMoveX", 0);
        animator.SetFloat("LastMoveY", -1); // For the Idle Blend Tree

        Debug.Log("Guard " + gameObject.name + " initialized. Move Speed: " + moveSpeed, this);
    }

    void Update()
    {
        // Animator update logic: Keep in Update for smoother animations if not tied to FixedUpdate frequency.
        // If animation appears jittery, move this part to FixedUpdate.
        if (!isWaiting)
        {
            // Calculate direction towards next waypoint for animation
            // Use transform.position here as animator usually works well with visual position.
            Vector3 targetPosition = waypoints[currentWaypointIndex].position;
            Vector3 currentMoveDirection = (targetPosition - transform.position).normalized;

            animator.SetBool("IsMoving", true);
            animator.SetFloat("MoveX", currentMoveDirection.x);
            animator.SetFloat("MoveY", currentMoveDirection.y);

            if (currentMoveDirection.magnitude > 0.01f) // Only update last move if actually moving
            {
                animator.SetFloat("LastMoveX", currentMoveDirection.x);
                animator.SetFloat("LastMoveY", currentMoveDirection.y);
            }
        }
        else
        {
            animator.SetBool("IsMoving", false); // Ensure animator is in idle state when waiting
        }

        // Perform player detection logic (non-physics related, so can stay in Update)
        DetectPlayer();
    }

    void FixedUpdate()
    {
        if (isWaiting)
        {
            // Ensure guard is completely stopped when waiting
            rb.linearVelocity = Vector2.zero;
            return; // Do nothing else while waiting
        }

        // Get the current target waypoint's position
        Vector2 targetPosition2D = waypoints[currentWaypointIndex].position;
        // Calculate the normalized direction towards the target using rb.position for physics consistency
        Vector2 currentMoveDirection = (targetPosition2D - rb.position).normalized;

        // Move Rigidbody2D to the new calculated position.
        // With BodyType Kinematic and Collision Detection Continuous, Unity will automatically prevent the Guard from tunneling through non-trigger colliders.
        Vector2 newPosition = Vector2.MoveTowards(rb.position, targetPosition2D, moveSpeed * Time.fixedDeltaTime);
        rb.MovePosition(newPosition);

        // Check if the guard has reached the current waypoint (use rb.position for consistency)
        if (Vector2.Distance(rb.position, targetPosition2D) < waypointThreshold)
        {
            StartCoroutine(WaitAtWaypoint()); // Start the coroutine to wait
        }
    }

    // Coroutine to handle the waiting period at each waypoint
    IEnumerator WaitAtWaypoint()
    {
        isWaiting = true;
        animator.SetBool("IsMoving", false); // Ensure idle animation during wait
        rb.linearVelocity = Vector2.zero; // Make sure it stops immediately when reaching waypoint

        Debug.Log("Guard " + gameObject.name + " reached waypoint " + currentWaypointIndex + ". Waiting.", this);
        yield return new WaitForSeconds(waitTime); // Wait for the specified time

        isWaiting = false;
        // Move to the next waypoint in the list (loop back to start if at the end)
        currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Count;

        // After waiting, set the animator's direction for the next move
        // This is done here to ensure the animation direction is correct when starting the next patrol segment
        Vector3 nextMoveTarget = waypoints[currentWaypointIndex].position;
        Vector2 nextMoveDirection = ((Vector2)nextMoveTarget - rb.position).normalized; // Use rb.position for direction
        animator.SetFloat("MoveX", nextMoveDirection.x);
        animator.SetFloat("MoveY", nextMoveDirection.y);
        animator.SetFloat("LastMoveX", nextMoveDirection.x); // Update for idle when stopping
        animator.SetFloat("LastMoveY", nextMoveDirection.y);
        Debug.Log("Guard " + gameObject.name + " proceeding to waypoint " + currentWaypointIndex + ".", this);
    }

    // Basic player detection logic using OverlapCircle
    void DetectPlayer()
    {
        // Find all colliders within the detectionRadius on the 'playerLayer'
        Collider2D[] hitColliders = Physics2D.OverlapCircleAll(transform.position, detectionRadius, playerLayer);
        foreach (var hitCollider in hitColliders)
        {
            if (hitCollider.CompareTag("Player")) // Check if the detected object has the "Player" tag
            {
                Debug.Log("Guard " + gameObject.name + " detected player: " + hitCollider.name + "!", this);
                // --- INSERT YOUR ALARM/CHASE LOGIC HERE ---
            }
        }
    }

    // --- Collision/Trigger Debug Callbacks ---
    // These methods are called by Unity's physics system when collisions/triggers occur.
    // Useful for debugging physics interactions.
    void OnCollisionEnter2D(Collision2D collision)
    {
        // Logs a message when a physical collision starts
        Debug.Log("Guard " + gameObject.name + " PHYSICAL COLLISION with: " + collision.gameObject.name +
            " (Layer: " + LayerMask.LayerToName(collision.gameObject.layer) + ")", this);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // Logs a message when a trigger collision starts
        Debug.Log("Guard " + gameObject.name + " TRIGGER COLLISION with: " + other.gameObject.name +
            " (Layer: " + LayerMask.LayerToName(other.gameObject.layer) + ")", this);
    }

    // --- Gizmos for Debugging in Scene View ---
    void OnDrawGizmosSelected()
    {
        // Draw the detection radius (red wire sphere)
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

        // Draw the patrol path (green lines and spheres)
        Gizmos.color = Color.green;
        if (waypoints != null)
        {
            for (int i = 0; i < waypoints.Count; i++)
            {
                if (waypoints[i] != null)
                {
                    Gizmos.DrawSphere(waypoints[i].position, 0.2f); // Draw waypoint sphere
                    if (i < waypoints.Count - 1)
                    {
                        Gizmos.DrawLine(waypoints[i].position, waypoints[i + 1].position); // Draw line to next waypoint
                    }
                    else if (waypoints.Count > 1) // Connect the last waypoint to the first
                    {
                        Gizmos.DrawLine(waypoints[i].position, waypoints[0].position);
                    }
                }
            }
        }
    }
}
