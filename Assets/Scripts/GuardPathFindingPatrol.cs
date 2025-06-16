using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using KwaaktjePathfinder2D;
using UnityEngine.Rendering.Universal; // IMPORTANT: Add this line to use Light2D

public class GuardPathfindingPatrol : MonoBehaviour
{
    // --- Public Configuration (Set in Inspector) ---
    public List<Transform> waypoints; // List of patrol waypoints
    public float moveSpeed = 1.5f;   // Movement speed
    public float waitTime = 1f;      // Time to wait at each waypoint
    public float cellReachThreshold = 0.1f; // Threshold to determine if the center of a cell or waypoint has been reached

    [Header("Detection")]
    public float detectionRadius = 0.5f; // General detection radius (kept if you want to use it for other purposes)
    public LayerMask playerLayer;        // Layer of the Player

    [Header("Vision Cone")]
    public float fieldOfViewAngle = 90f; // Vision cone angle
    public float viewDistance = 5f;      // Vision cone distance
    public LayerMask viewObstacleLayer;  // Layer of objects that block vision (walls, crates)

    [Header("Vision Cone Visual")]
    public UnityEngine.Rendering.Universal.Light2D guardLight; // Drag the Guard's Light2D component here

    // --- Private Variables ---
    private Rigidbody2D rb;
    private Animator animator;
    private int currentWaypointIndex = 0;
    private bool isWaiting = false;
    private bool noReachableWaypoints = false; // Flag to track if no waypoints are reachable

    private List<Vector2Int> currentPath = new List<Vector2Int>(); // Current path in grid cells
    private int currentPathIndex = 0;                             // Current cell index on the path

    private Pathfinder2D pathfinder; // Reference to the pathfinding object
    private PathfindingGridManager gridManager; // Reference to the grid manager

    private Vector2 lastFacingDirection = Vector2.down; // Last facing direction of the Guard, defaults to down

    void Awake()
    {
        if (waypoints == null)
            waypoints = new List<Transform>();

        rb = GetComponent<Rigidbody2D>();
        if (rb == null) Debug.LogError("Guard Rigidbody2D component missing!", this);

        animator = GetComponent<Animator>();
        if (animator == null) Debug.LogError("Guard Animator component missing!", this);

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

    void OnEnable()
    {
        // Subscribe to the grid refresh event
        PathfindingGridManager.OnGridRefreshed += HandleGridRefreshed;
    }

    void OnDisable()
    {
        // Unsubscribe from the event to prevent memory leaks
        PathfindingGridManager.OnGridRefreshed -= HandleGridRefreshed;
    }

    void Start()
    {
        // Check validity of the waypoints list
        if (waypoints == null || waypoints.Count == 0)
        {
            Debug.LogError("No waypoints assigned for guard " + gameObject.name + ". Please assign waypoints in the Inspector.", this);
            enabled = false;
            return;
        }
        else
        {
            for (int i = 0; i < waypoints.Count; i++)
            {
                if (waypoints[i] == null)
                {
                    Debug.LogError($"[Guard] Waypoint at index {i} is NULL! Please re-assign in Inspector. Disabling Guard Patrol.", this);
                    enabled = false;
                    return;
                }
            }
        }

        // Get reference to PathfindingGridManager in the Scene
        // USING SINGLETON INSTANCE
        gridManager = PathfindingGridManager.Instance; // Get instance via singleton
        if (gridManager == null)
        {
            Debug.LogError("PathfindingGridManager not found (or not properly initialized) in scene! Make sure it exists and has the script attached.", this);
            enabled = false;
            return;
        }

        // Get reference to Pathfinder2D from GridManager
        if (PathfindingGridManager.InstancePathfinder == null)
        {
            Debug.LogError("PathfindingGridManager.InstancePathfinder is null. Make sure PathfindingGridManager is initialized first!", this);
            enabled = false;
            return;
        }
        pathfinder = PathfindingGridManager.InstancePathfinder;

        // Start patrolling from the first reachable waypoint
        FindAndStartNextReachableWaypointPath(false);
    }

    void Update()
    {
        // Update animation and facing direction based on the Guard's current velocity
        Vector2 currentVelocity = rb.linearVelocity;
        if (currentVelocity.magnitude > 0.05f) // If the Guard is moving fast enough
        {
            animator.SetBool("IsMoving", true);
            animator.SetFloat("MoveX", currentVelocity.x);
            animator.SetFloat("MoveY", currentVelocity.y);

            // Update the last facing direction only when the Guard is moving
            lastFacingDirection = currentVelocity.normalized;
        }
        else
        {
            animator.SetBool("IsMoving", false);
            // Keep lastFacingDirection when stationary so the facing direction isn't reset
        }

        // IMPORTANT: Update the direction of the Guard's Light2D
        if (guardLight != null)
        {
            float angle = Mathf.Atan2(lastFacingDirection.y, lastFacingDirection.x) * Mathf.Rad2Deg - 90f;
            guardLight.transform.rotation = Quaternion.Euler(0, 0, angle);
        }

        // Execute player detection logic
        DetectPlayer();
    }

    void FixedUpdate()
    {
        // If the Guard is waiting or no waypoints are reachable, stop moving
        if (isWaiting || noReachableWaypoints)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        // If the current path has ended or doesn't exist
        if (currentPath == null || currentPath.Count == 0 || currentPathIndex >= currentPath.Count)
        {
            // Check if the Guard has reached the final destination waypoint
            if (waypoints != null && waypoints.Count > currentWaypointIndex && waypoints[currentWaypointIndex] != null &&
                Vector2.Distance(rb.position, waypoints[currentWaypointIndex].position) < cellReachThreshold)
            {
                Debug.Log($"[Guard {gameObject.name}] Reached final waypoint {currentWaypointIndex}. Starting wait.");
                StartCoroutine(WaitAtWaypoint()); // Start waiting
            }
            else
            {
                // If the path is exhausted but the waypoint hasn't been reached (e.g., blocked midway), try to find a path again
                rb.linearVelocity = Vector2.zero;
                animator.SetBool("IsMoving", false);
                Debug.LogWarning($"[Guard {gameObject.name}] Path exhausted or blocked before reaching final waypoint. Re-evaluating path.", this);
                FindAndStartNextReachableWaypointPath(true); // Find path again
            }
            return;
        }

        // Move the Guard to the next cell on the path
        Vector2Int targetCell = currentPath[currentPathIndex];
        Vector3 targetWorldPosition = PathfindingGridManager.GridCellToWorldCenter(targetCell, gridManager.managerCellSize, gridManager.gridOrigin);

        Vector2 moveDirection = ((Vector2)targetWorldPosition - rb.position).normalized;
        rb.linearVelocity = moveDirection * moveSpeed;

        // If the Guard is close to the current target cell
        if (Vector2.Distance(rb.position, targetWorldPosition) < cellReachThreshold)
        {
            rb.position = targetWorldPosition; // Snap to the center of the cell
            currentPathIndex++; // Move to the next cell

            // If all cells in the current path have been traversed
            if (currentPathIndex >= currentPath.Count)
            {
                rb.linearVelocity = Vector2.zero;
                // Re-check if the exact waypoint has been reached
                if (waypoints != null && waypoints.Count > currentWaypointIndex && waypoints[currentWaypointIndex] != null &&
                    Vector2.Distance(rb.position, waypoints[currentWaypointIndex].position) < cellReachThreshold)
                {
                    Debug.Log($"[Guard {gameObject.name}] Reached waypoint {currentWaypointIndex}. Starting wait.");
                    StartCoroutine(WaitAtWaypoint());
                }
                else
                {
                    Debug.LogWarning("Path completed, but not exactly at waypoint. Retrying...");
                    FindAndStartNextReachableWaypointPath(true);
                }
            }
        }
    }

    /// <summary>
    /// Attempts to find a path to a target world position.
    /// </summary>
    /// <param name="targetWorldPos">The world position of the target.</param>
    /// <returns>True if a path is found, False otherwise.</returns>
    private bool AttemptToFindPath(Vector3 targetWorldPos)
    {
        if (pathfinder == null || gridManager == null)
        {
            Debug.LogError("[Guard] Missing pathfinder or gridManager for pathfinding attempt!", this);
            return false;
        }

        Vector2Int startCell = PathfindingGridManager.WorldToGridCell(rb.position, gridManager.gridSize, gridManager.managerCellSize, gridManager.gridOrigin);
        Vector2Int endCell = PathfindingGridManager.WorldToGridCell(targetWorldPos, gridManager.gridSize, gridManager.managerCellSize, gridManager.gridOrigin);

        Pathfinder2DResult pathResult = pathfinder.FindPath(startCell, endCell);

        if (pathResult.Status == Pathfinder2DResultStatus.SUCCESS && pathResult.Path.Count > 0)
        {
            currentPath = new List<Vector2Int>(pathResult.Path);
            currentPath.Reverse(); // Reverse to get path from current position to target

            Vector2Int currentGuardCell = PathfindingGridManager.WorldToGridCell(rb.position, gridManager.gridSize, gridManager.managerCellSize, gridManager.gridOrigin);
            if (currentPath.Count > 0 && currentPath[0] == currentGuardCell)
            {
                currentPath.RemoveAt(0); // Remove the current cell if the path starts with itself
            }

            currentPathIndex = 0;
            return true;
        }
        else
        {
            currentPath.Clear();
            rb.linearVelocity = Vector2.zero; // Stop if no path is found
            return false;
        }
    }

    /// <summary>
    /// Finds the next reachable waypoint in the patrol list and starts the path to it.
    /// </summary>
    /// <param name="advanceIndexFirst">If true, increments currentWaypointIndex before searching.</param>
    private void FindAndStartNextReachableWaypointPath(bool advanceIndexFirst)
    {
        if (waypoints == null || waypoints.Count == 0)
        {
            Debug.LogError("[Guard] Waypoints list is null or empty. Cannot find next waypoint.", this);
            rb.linearVelocity = Vector2.zero;
            animator.SetBool("IsMoving", false);
            enabled = false;
            return;
        }

        if (pathfinder == null)
        {
            Debug.LogError("[Guard] Pathfinder is null when trying to find next reachable waypoint! Aborting.", this);
            rb.linearVelocity = Vector2.zero;
            animator.SetBool("IsMoving", false);
            noReachableWaypoints = true;
            isWaiting = true;
            return;
        }

        int startIndex = currentWaypointIndex;
        if (advanceIndexFirst)
        {
            startIndex = (currentWaypointIndex + 1) % waypoints.Count;
        }

        bool pathFoundAndStarted = false;

        for (int i = 0; i < waypoints.Count; i++)
        {
            int nextWaypointCheckIndex = (startIndex + i) % waypoints.Count;

            if (waypoints[nextWaypointCheckIndex] == null)
            {
                Debug.LogWarning($"[Guard] Waypoint at index {nextWaypointCheckIndex} is NULL. Skipping.", this);
                continue;
            }

            if (AttemptToFindPath(waypoints[nextWaypointCheckIndex].position))
            {
                currentWaypointIndex = nextWaypointCheckIndex;
                pathFoundAndStarted = true;
                noReachableWaypoints = false;
                Debug.Log($"[Guard {gameObject.name}] Proceeding to reachable waypoint {currentWaypointIndex}.");
                break;
            }
            else
            {
                Debug.Log($"[Guard {gameObject.name}] Waypoint {nextWaypointCheckIndex} is currently unreachable. Trying next.");
            }
        }

        if (!pathFoundAndStarted)
        {
            Debug.LogWarning($"[Guard {gameObject.name}] No reachable waypoints found. Guard is staying put.");
            rb.linearVelocity = Vector2.zero;
            animator.SetBool("IsMoving", false);
            noReachableWaypoints = true;
            isWaiting = true;
        }
    }

    IEnumerator WaitAtWaypoint()
    {
        isWaiting = true;
        rb.linearVelocity = Vector2.zero;
        animator.SetBool("IsMoving", false);

        Debug.Log("Guard " + gameObject.name + " reached waypoint " + currentWaypointIndex + ". Waiting.");
        yield return new WaitForSeconds(waitTime);

        isWaiting = false;
        FindAndStartNextReachableWaypointPath(true);
    }

    // This method is called when the OnGridRefreshed event from PathfindingGridManager is triggered
    private void HandleGridRefreshed()
    {
        Debug.Log($"[Guard {gameObject.name}] Grid refreshed event received. Re-evaluating patrol path.");

        if (PathfindingGridManager.InstancePathfinder != null)
        {
            pathfinder = PathfindingGridManager.InstancePathfinder;
            Debug.Log($"[Guard {gameObject.name}] Updated pathfinder reference to the new grid data.");
        }
        else
        {
            Debug.LogError($"[Guard {gameObject.name}] PathfindingGridManager.InstancePathfinder is null after refresh! Guard cannot re-evaluate path. Disabling guard.", this);
            noReachableWaypoints = true;
            rb.linearVelocity = Vector2.zero;
            enabled = false; // Disable guard if pathfinder is truly gone
            return;
        }

        // Clear current path and force a new pathfinding attempt from current position
        currentPath.Clear();
        currentPathIndex = 0;
        isWaiting = false; // Ensure guard is not stuck in waiting state if grid changed around it
        noReachableWaypoints = false; // Reset this flag for new pathfinding attempt

        // Try to find a path to the *current* target waypoint, or the next if needed
        // This ensures the guard re-evaluates its current objective on the new grid
        FindAndStartNextReachableWaypointPath(false); // Do not advance index yet, re-evaluate current target
    }

    // Function to detect the player using a vision cone
    void DetectPlayer()
    {
        // Use rb.position to ensure consistency with physics
        Collider2D[] potentialTargets = Physics2D.OverlapCircleAll(rb.position, viewDistance, playerLayer);

        if (potentialTargets.Length == 0)
        {
            Debug.Log($"[Guard {gameObject.name}] No potential targets found within viewDistance ({viewDistance:F2}) on layer {LayerMask.LayerToName(playerLayer)}.", this);
            return; // Exit early if no targets are found
        }
        else
        {
            Debug.Log($"[Guard {gameObject.name}] Found {potentialTargets.Length} potential targets within viewDistance.", this);
        }

        foreach (var targetCollider in potentialTargets)
        {
            if (targetCollider == null) continue; // Safety check to prevent errors if collider was destroyed

            // Check if the object's Tag is "Player"
            if (targetCollider.CompareTag("Player"))
            {
                Debug.Log($"[Guard {gameObject.name}] Player candidate found: {targetCollider.name} (Tag matches 'Player')", this);

                // Calculate the direction vector from the Guard's position to the Player's position
                Vector2 directionToTarget = ((Vector2)targetCollider.transform.position - (Vector2)rb.position).normalized;

                // Calculate the angle between the Guard's facing direction and the direction to the Player
                // Vector2.SignedAngle returns an angle between -180 and 180 degrees
                float angleToPlayer = Vector2.SignedAngle(lastFacingDirection, directionToTarget);
                Debug.Log($"[Guard {gameObject.name}] Angle to Player {targetCollider.name}: {angleToPlayer:F2}° (Half FOV: {fieldOfViewAngle / 2f:F2}°)", this);

                // Check if the Player is within the Guard's VISION CONE
                // Mathf.Abs(angleToPlayer) takes the absolute value of the angle to compare with half FOV
                if (Mathf.Abs(angleToPlayer) < fieldOfViewAngle / 2f)
                {
                    Debug.Log($"[Guard {gameObject.name}] Player {targetCollider.name} is WITHIN VISION ANGLE.", this);

                    // Check Line of Sight (if the view path is blocked by an obstacle) using Raycast
                    // Raycast starts from Guard's position, goes in `directionToTarget`, with length `viewDistance`,
                    // and only collides with objects on the `viewObstacleLayer`.
                    RaycastHit2D hit = Physics2D.Raycast(rb.position, directionToTarget, viewDistance, viewObstacleLayer);

                    // Debug.DrawRay will draw the Raycast in the Scene view for visualization
                    // Green: clear line of sight (not blocked)
                    // Yellow: Raycast hits the Player itself (clear line of sight)
                    // Red: Raycast hits an obstacle (line of sight blocked)
                    Color rayColor = Color.blue; // Default blue if no collision
                    if (hit.collider != null)
                    {
                        if (hit.collider.CompareTag("Player"))
                        {
                            rayColor = Color.yellow; // Raycast hits Player directly
                        }
                        else
                        {
                            rayColor = Color.red; // Raycast hits an obstacle
                        }
                    }
                    Debug.DrawRay(rb.position, directionToTarget * viewDistance, rayColor, Time.deltaTime);

                    // If Raycast did not hit any obstacle OR if it hit the Player itself
                    // This means the line of sight is clear
                    if (hit.collider == null || hit.collider.CompareTag("Player"))
                    {
                        Debug.Log($"<color=lime>[Guard {gameObject.name}] PLAYER {targetCollider.name} DETECTED! (Clear LOS or Hit Player)</color>", this);
                        // TODO: Activate Player detection logic here (e.g., transition to chase state, alert)
                        moveSpeed = 3f; // Increase movement speed when Player is detected
                        return; // Player detected, no need to check other colliders
                    }
                    else
                    {
                        // Log when Player is in vision cone but blocked by an obstacle
                        Debug.Log($"<color=orange>[Guard {gameObject.name}] Player {targetCollider.name} is in vision cone but BLOCKED by {hit.collider.name} (Layer: {LayerMask.LayerToName(hit.collider.gameObject.layer)})</color>", this);
                    }
                }
                else
                {
                    // Log when Player is outside the vision cone (but still within OverlapCircle radius)
                    Debug.Log($"[Guard {gameObject.name}] Player {targetCollider.name} is within detection radius but OUT OF VISION CONE (Angle: {angleToPlayer:F2}° vs FOV/2: {fieldOfViewAngle / 2f:F2}°)!", this);
                }
            }
            else
            {
                // Log if an object in OverlapCircle is not the Player
                Debug.Log($"[Guard {gameObject.name}] Non-player collider found in OverlapCircle: {targetCollider.name} (Tag: {targetCollider.tag})", this);
            }
        }
    }

    // Collision handling functions, currently not used for main logic
    void OnCollisionEnter2D(Collision2D collision) { /* Debug.Log($"Collided with {collision.gameObject.name}"); */ }
    void OnTriggerEnter2D(Collider2D other) { /* Debug.Log($"Triggered by {other.gameObject.name}"); */ }

    // --- Gizmos for Debugging in Scene View ---
    void OnDrawGizmosSelected()
    {
        // Debug detection radius (DetectionRadius)
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

        // Debug patrol path (waypoints)
        Gizmos.color = Color.green;
        if (waypoints != null)
        {
            for (int i = 0; i < waypoints.Count; i++)
            {
                if (waypoints[i] != null)
                {
                    Gizmos.DrawSphere(waypoints[i].position, 0.2f);
                    if (i < waypoints.Count - 1 && waypoints[i + 1] != null)
                        Gizmos.DrawLine(waypoints[i].position, waypoints[i + 1].position);
                    else if (waypoints.Count > 1 && waypoints[0] != null)
                        Gizmos.DrawLine(waypoints[i].position, waypoints[0].position);
                }
            }
        }

        // Debug current path from Pathfinder2D
        if (currentPath != null && currentPath.Count > 0 && gridManager != null)
        {
            Gizmos.color = Color.cyan;
            Vector3 previous = rb.position;
            for (int i = currentPathIndex; i < currentPath.Count; i++)
            {
                Vector3 center = PathfindingGridManager.GridCellToWorldCenter(currentPath[i], gridManager.managerCellSize, gridManager.gridOrigin);
                Gizmos.DrawLine(previous, center);
                previous = center;
            }
        }

        // Debug Vision Cone
        if (rb != null)
        {
            Vector3 guardPos = transform.position;
            Vector2 forwardDir = lastFacingDirection;

            // Draw general view distance (faint orange circle)
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.2f); // Transparent orange
            Gizmos.DrawSphere(guardPos, viewDistance);

            // Draw vision cone (yellow)
            Gizmos.color = Color.yellow;
            float halfFOV = fieldOfViewAngle / 2f;

            Quaternion leftRayRotation = Quaternion.AngleAxis(-halfFOV, Vector3.forward);
            Quaternion rightRayRotation = Quaternion.AngleAxis(halfFOV, Vector3.forward);

            Vector3 leftRayDirection = leftRayRotation * forwardDir;
            Vector3 rightRayDirection = rightRayRotation * forwardDir;

            Gizmos.DrawRay(guardPos, leftRayDirection * viewDistance);
            Gizmos.DrawRay(guardPos, rightRayDirection * viewDistance);

            int segments = 20;
            Vector3 previousPointInArc = guardPos + leftRayDirection * viewDistance;
            for (int i = 1; i <= segments; i++)
            {
                float angle = -halfFOV + (fieldOfViewAngle / segments) * i;
                Quaternion currentRayRotation = Quaternion.AngleAxis(angle, Vector3.forward);
                Vector3 currentPointInArc = guardPos + (currentRayRotation * forwardDir) * viewDistance;
                Gizmos.DrawLine(previousPointInArc, currentPointInArc);
                previousPointInArc = currentPointInArc;
            }

            // Draw the central ray of the vision cone
            Gizmos.DrawLine(guardPos, guardPos + (Vector3)forwardDir * viewDistance);

            // Debug Line of Sight (optional) - Displays raycast blocked by obstacles
            // Only runs in Play Mode to avoid continuous Raycast in Editor
            if (Application.isPlaying)
            {
                Vector2 raycastDir = forwardDir; // Raycast direction
                RaycastHit2D hit = Physics2D.Raycast(guardPos, raycastDir, viewDistance, viewObstacleLayer);
                Gizmos.color = hit.collider != null ? Color.red : Color.blue;
                Gizmos.DrawRay(guardPos, raycastDir * viewDistance);
            }
        }
    }
}
