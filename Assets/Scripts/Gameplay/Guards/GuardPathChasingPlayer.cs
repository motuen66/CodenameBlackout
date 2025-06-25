using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using KwaaktjePathfinder2D;
using UnityEngine.Rendering.Universal; // IMPORTANT: Add this line to use Light2D

[RequireComponent(typeof(Collider2D))]
public class GuardPathChasingPlayer : MonoBehaviour
{
    // --- Public Configuration (Set in Inspector) ---
    public List<Transform> waypoints;
    public float moveSpeed = 2f;
    public float chaseSpeed = 3.5f;
    public float waitTime = 1f;
    public float lostPlayerWaitTime = 3f;
    public float cellReachThreshold = 0.1f;

    [Header("Detection")]
    public float viewDistance = 5f;
    public float fieldOfViewAngle = 220f;
    public LayerMask playerLayer;
    public LayerMask viewObstacleLayer;
    public float visionVerticalOffset = 0f;
    public float multiRayAngularSpread = 30f;
    public int numberOfRays = 7;
    public float raycastOriginOffset = 0.1f;

    private Rigidbody2D rb;
    private Animator animator;
    private Collider2D guardCollider;
    private int currentWaypointIndex = 0;
    private bool isWaiting = false;
    private bool isChasing = false;
    private bool lostPlayer = false; // Flag for when player is lost but still at last known position
    private bool recentlyLostPlayer = false; // Flag to prevent immediate re-detection
    public float lostPlayerCooldown = 3f; // Time before guard can detect player again after losing them

    private List<Vector2Int> currentPath = new List<Vector2Int>();
    private int currentPathIndex = 0;

    private Pathfinder2D pathfinder;
    private PathfindingGridManager gridManager;

    private Vector2 lastFacingDirection = Vector2.down;
    private Vector3 lastKnownPlayerPosition;

    public UnityEngine.Rendering.Universal.Light2D guardLight; // Reference to the Guard's Light2D component


    // Initializes component references when the script instance is being loaded.
    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        guardCollider = GetComponent<Collider2D>();

        // Get Light2D automatically if not assigned in Inspector (if it's a child)
        if (guardLight == null)
        {
            guardLight = GetComponentInChildren<UnityEngine.Rendering.Universal.Light2D>();
            if (guardLight == null)
            {
                Debug.LogWarning("Guard Light2D not assigned and not found as child of " + gameObject.name + ". Vision cone visual will not work.", this);
            }
        }
    }

    // Subscribes to the grid refresh event when the GameObject is enabled.
    void OnEnable()
    {
        PathfindingGridManager.OnGridRefreshed += HandleGridRefreshed;
    }

    // Unsubscribes from the grid refresh event when the GameObject is disabled to prevent memory leaks.
    void OnDisable()
    {
        PathfindingGridManager.OnGridRefreshed -= HandleGridRefreshed;
    }

    // Initializes manager references, checks waypoints, and starts the patrol state.
    void Start()
    {
        gridManager = PathfindingGridManager.Instance;
        pathfinder = PathfindingGridManager.InstancePathfinder;
        if (waypoints == null || waypoints.Count == 0)
        {
            Debug.LogError("No waypoints assigned for guard " + gameObject.name + ". Please assign waypoints in the Inspector.", this);
            enabled = false;
            return;
        }
        StartPatrol(); // Start in patrol mode
    }

    // Called once per frame. Handles player detection, path recalculation during chase, and visual updates.
    void Update()
    {
        // Always check player vision, even if recently lost (but cooldown prevents re-detection)
        DetectPlayer();

        // If chasing and player is not lost, continuously update path to player's position
        if (isChasing && !lostPlayer)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                lastKnownPlayerPosition = player.transform.position; // Continuously update last known position
                AttemptToFindPath(player.transform.position); // Recalculate path to player
            }
        }
        UpdateAnimation(); // Update animator parameters based on movement

        // Update the direction of the Guard's Light2D to match facing direction
        if (guardLight != null)
        {
            float angle = Mathf.Atan2(lastFacingDirection.y, lastFacingDirection.x) * Mathf.Rad2Deg - 90f;
            guardLight.transform.rotation = Quaternion.Euler(0, 0, angle);
        }
    }

    // Called at fixed time intervals. Handles guard movement along the current path.
    void FixedUpdate()
    {
        if (isWaiting) { rb.linearVelocity = Vector2.zero; return; } // If waiting, stop movement

        // Check if the current path has ended or doesn't exist
        if (currentPath == null || currentPath.Count == 0 || currentPathIndex >= currentPath.Count)
        {
            if (isChasing && lostPlayer)
            {
                // If lost player, wait at last known position
                StartCoroutine(WaitAtLostPlayerPosition());
            }
            else if (!isChasing)
            {
                // If patrolling, wait at waypoint
                StartCoroutine(WaitAtWaypoint());
            }
            rb.linearVelocity = Vector2.zero; // Stop movement after path exhaustion
            return;
        }

        // Move the Guard to the next cell on the path
        Vector2Int targetCell = currentPath[currentPathIndex];
        Vector3 targetWorldPosition = PathfindingGridManager.GridCellToWorldCenter(targetCell, gridManager.managerCellSize, gridManager.gridOrigin);
        Vector2 moveDirection = ((Vector2)targetWorldPosition - rb.position).normalized;
        rb.linearVelocity = moveDirection * (isChasing ? chaseSpeed : moveSpeed); // Use chaseSpeed or moveSpeed

        // Update facing direction based on movement, only if there's significant movement
        if (moveDirection.sqrMagnitude > 0.01f) // Use sqrMagnitude for performance
        {
            lastFacingDirection = moveDirection;
        }

        // If the Guard is close to the current target cell, move to the next cell
        if (Vector2.Distance(rb.position, targetWorldPosition) < cellReachThreshold)
        {
            rb.position = targetWorldPosition; // Snap to the center of the cell
            currentPathIndex++; // Move to the next cell in the path
        }
    }

    // Transitions the guard to patrol mode.
    void StartPatrol()
    {
        isChasing = false;
        lostPlayer = false;
        isWaiting = false;
        StopAllCoroutines(); // Stop any pending wait or cooldown coroutines
        FindAndStartNextReachableWaypointPath(false); // Find path to current/next waypoint
    }

    // Transitions the guard to chase mode towards the player's position.
    void StartChase(Vector3 playerPosition)
    {
        if (isChasing && !lostPlayer) return; // Already chasing and not lost, no need to restart

        isChasing = true;
        lostPlayer = false;
        recentlyLostPlayer = false; // Reset cooldown when player is re-detected
        lastKnownPlayerPosition = playerPosition; // Store player's position
        StopAllCoroutines(); // Stop any patrol-related waiting
        AttemptToFindPath(playerPosition); // Find path to player
    }

    // Transitions the guard to a "lost player" state, moving to the last known position.
    void LosePlayer()
    {
        if (lostPlayer) return; // Already in lost state

        lostPlayer = true;
        // The current path is already to the lastKnownPlayerPosition if it was actively chasing
        // Just need to ensure it finishes that path and then waits/resumes patrol
        Debug.Log($"Guard {gameObject.name} lost player. Heading to last known position.");
    }

    // Detects the player using a vision cone by casting multiple rays.
    void DetectPlayer()
    {
        if (recentlyLostPlayer) return; // Prevent detection during cooldown after losing player

        Vector2 visionOrigin = (guardCollider != null)
            ? new Vector2(guardCollider.bounds.center.x, guardCollider.bounds.max.y + visionVerticalOffset)
            : (Vector2)rb.position;

        Collider2D[] potentialTargets = Physics2D.OverlapCircleAll(visionOrigin, viewDistance, playerLayer);
        bool playerDetectedInSight = false;

        foreach (var targetCollider in potentialTargets)
        {
            if (targetCollider == null || !targetCollider.CompareTag("Player")) continue;

            Vector2 playerPos = targetCollider.transform.position;
            Vector2 directionToPlayer = (playerPos - visionOrigin).normalized;
            float angleToPlayer = Vector2.SignedAngle(lastFacingDirection, directionToPlayer);

            if (Mathf.Abs(angleToPlayer) <= fieldOfViewAngle / 2f) // Check if player is within the view angle
            {
                // This block is for drawing debug rays only, not the actual LOS check
                // The actual LOS check is done by CheckLineOfSight below
                float angleStep = numberOfRays > 1 ? multiRayAngularSpread / (numberOfRays - 1) : 0f;
                float startAngle = -multiRayAngularSpread / 2f;
                for (int i = 0; i < numberOfRays; i++)
                {
                    float currentAngle = startAngle + i * angleStep;
                    Vector2 rayDirection = Quaternion.Euler(0, 0, currentAngle) * directionToPlayer;
                    Vector2 rayStart = visionOrigin + rayDirection * raycastOriginOffset;

                    RaycastHit2D hit = Physics2D.Raycast(rayStart, rayDirection, viewDistance, playerLayer | viewObstacleLayer);

                    Color debugColor = Color.yellow;
                    if (hit.collider != null)
                    {
                        if (hit.collider.CompareTag("Player"))
                            debugColor = Color.green; // Player found by this ray
                        else
                            debugColor = Color.red; // Obstacle blocking this ray
                    }
                    Debug.DrawRay(rayStart, rayDirection * viewDistance, debugColor, 0.1f);
                }

                // Perform the actual Line of Sight check
                if (CheckLineOfSight(visionOrigin, playerPos))
                {
                    playerDetectedInSight = true;
                    // If not currently chasing OR was chasing but lost, start chasing
                    if (!isChasing || lostPlayer)
                    {
                        StartChase(playerPos);
                    }
                    else // If already chasing and not lost, just update last known position
                    {
                        lastKnownPlayerPosition = playerPos;
                    }
                    break; // Player detected, no need to check other potential targets
                }
            }
        }

        // If currently chasing but player is no longer detected, transition to lost state
        if (isChasing && !playerDetectedInSight && !lostPlayer)
        {
            LosePlayer();
        }
    }

    // Checks for a clear line of sight to a target using multiple rays.
    bool CheckLineOfSight(Vector2 origin, Vector2 target)
    {
        Vector2 mainDirection = (target - origin).normalized;
        float angleStep = numberOfRays > 1 ? multiRayAngularSpread / (numberOfRays - 1) : 0f;
        float startAngle = -multiRayAngularSpread / 2f;

        for (int i = 0; i < numberOfRays; i++)
        {
            float currentAngle = startAngle + i * angleStep;
            Vector2 rayDirection = Quaternion.Euler(0, 0, currentAngle) * mainDirection;
            Vector2 rayStart = origin + rayDirection * raycastOriginOffset;

            RaycastHit2D hit = Physics2D.Raycast(rayStart, rayDirection, viewDistance, playerLayer | viewObstacleLayer);
            if (hit.collider != null && hit.collider.CompareTag("Player"))
                return true; // Found player with a clear line of sight
        }
        return false; // Player not found or blocked
    }

    // Updates the animator parameters based on the guard's movement velocity.
    void UpdateAnimation()
    {
        Vector2 currentVelocity = rb.linearVelocity;
        if (animator == null) return;
        if (currentVelocity.magnitude > 0.05f)
        {
            animator.SetBool("IsMoving", true);
            animator.SetFloat("MoveX", currentVelocity.x);
            animator.SetFloat("MoveY", currentVelocity.y);
            // lastFacingDirection is now updated in FixedUpdate for movement direction
        }
        else
        {
            animator.SetBool("IsMoving", false);
        }
    }

    // Attempts to find a path to a target world position using the pathfinding engine.
    bool AttemptToFindPath(Vector3 targetWorldPos)
    {
        if (pathfinder == null || gridManager == null) return false;
        Vector2Int startCell = PathfindingGridManager.WorldToGridCell(rb.position, gridManager.gridSize, gridManager.managerCellSize, gridManager.gridOrigin);
        Vector2Int endCell = PathfindingGridManager.WorldToGridCell(targetWorldPos, gridManager.gridSize, gridManager.managerCellSize, gridManager.gridOrigin);

        Pathfinder2DResult pathResult = pathfinder.FindPath(startCell, endCell);
        if (pathResult.Status == Pathfinder2DResultStatus.SUCCESS && pathResult.Path.Count > 0)
        {
            currentPath = new List<Vector2Int>(pathResult.Path);
            currentPath.Reverse();
            if (currentPath.Count > 0 && currentPath[0] == startCell)
                currentPath.RemoveAt(0); // Remove the current cell if path starts with it
            currentPathIndex = 0;
            return true;
        }
        else
        {
            currentPath.Clear();
            rb.linearVelocity = Vector2.zero;
            return false;
        }
    }

    // Finds the next reachable waypoint in the patrol list and starts a path to it.
    void FindAndStartNextReachableWaypointPath(bool advanceIndexFirst)
    {
        if (waypoints == null || waypoints.Count == 0) return;
        int startIndex = advanceIndexFirst ? (currentWaypointIndex + 1) % waypoints.Count : currentWaypointIndex;
        for (int i = 0; i < waypoints.Count; i++)
        {
            int idx = (startIndex + i) % waypoints.Count;
            if (waypoints[idx] == null) continue;
            if (AttemptToFindPath(waypoints[idx].position))
            {
                currentWaypointIndex = idx;
                return;
            }
        }
        currentPath.Clear();
        rb.linearVelocity = Vector2.zero;
    }

    // Coroutine for the guard to wait at a patrol waypoint for a specified duration.
    IEnumerator WaitAtWaypoint()
    {
        isWaiting = true;
        rb.linearVelocity = Vector2.zero;
        yield return new WaitForSeconds(waitTime);
        isWaiting = false;
        FindAndStartNextReachableWaypointPath(true);
    }

    // Coroutine for the guard to wait at the last known player position after losing sight.
    IEnumerator WaitAtLostPlayerPosition()
    {
        isWaiting = true;
        rb.linearVelocity = Vector2.zero;
        yield return new WaitForSeconds(lostPlayerWaitTime);
        isWaiting = false;
        recentlyLostPlayer = true; // Activate cooldown to prevent immediate re-detection
        StartPatrol(); // Revert to patrol mode
        StartCoroutine(LostPlayerCooldownCoroutine()); // Start cooldown timer
    }

    // Coroutine that manages the cooldown period after the guard loses the player.
    IEnumerator LostPlayerCooldownCoroutine()
    {
        yield return new WaitForSeconds(lostPlayerCooldown);
        recentlyLostPlayer = false; // Allow re-detection after cooldown
    }

    // Handles grid refresh event, re-evaluating the guard's current path or initiating patrol.
    private void HandleGridRefreshed()
    {
        if (PathfindingGridManager.InstancePathfinder != null)
        {
            pathfinder = PathfindingGridManager.InstancePathfinder;
        }
        else
        {
            enabled = false;
            return;
        }
        currentPath.Clear();
        currentPathIndex = 0;
        isWaiting = false;

        // Re-evaluate path based on current state (chasing, lost, or patrolling)
        if (isChasing && !lostPlayer)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                AttemptToFindPath(player.transform.position);
        }
        else if (isChasing && lostPlayer)
        {
            AttemptToFindPath(lastKnownPlayerPosition);
        }
        else
        {
            FindAndStartNextReachableWaypointPath(false);
        }
    }

    // Draws visual debugging aids in the Scene view, such as patrol paths and view cone.
    void OnDrawGizmosSelected()
    {
        if (guardCollider == null) guardCollider = GetComponent<Collider2D>();
        Vector3 guardPos = (guardCollider != null)
            ? new Vector3(guardCollider.bounds.center.x, guardCollider.bounds.max.y + visionVerticalOffset, transform.position.z)
            : transform.position;

        // Draw view distance (red wire sphere)
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(guardPos, viewDistance);

        // Draw view cone (yellow)
        Gizmos.color = Color.yellow;
        float halfFOV = fieldOfViewAngle / 2f;
        Vector2 forwardDir = lastFacingDirection.sqrMagnitude > 0.01f ? lastFacingDirection : Vector2.down; // Default if not moving

        Quaternion leftRayRotation = Quaternion.AngleAxis(-halfFOV, Vector3.forward);
        Quaternion rightRayRotation = Quaternion.AngleAxis(halfFOV, Vector3.forward);

        Vector3 leftRayDirection = leftRayRotation * forwardDir;
        Vector3 rightRayDirection = rightRayRotation * forwardDir;

        // Draw the two outer rays of the cone
        Gizmos.DrawRay(guardPos, leftRayDirection * viewDistance);
        Gizmos.DrawRay(guardPos, rightRayDirection * viewDistance);

        // Draw the arc of the cone
        int segments = 20;
        Vector3 previousPoint = guardPos + leftRayDirection * viewDistance;
        for (int i = 1; i <= segments; i++)
        {
            float angle = -halfFOV + (fieldOfViewAngle / segments) * i;
            Quaternion rot = Quaternion.AngleAxis(angle, Vector3.forward);
            Vector3 point = guardPos + (rot * forwardDir) * viewDistance;
            Gizmos.DrawLine(previousPoint, point);
            previousPoint = point;
        }
    }
}
