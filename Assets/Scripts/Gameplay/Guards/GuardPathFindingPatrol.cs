using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using KwaaktjePathfinder2D;
using UnityEngine.Rendering.Universal; // IMPORTANT: Add this line to use Light2D

[RequireComponent(typeof(Collider2D))] // Ensure the Guard has a Collider2D for vision origin
public class GuardPathfindingPatrol : MonoBehaviour
{// --- Public Configuration (Set in Inspector) ---
    public List<Transform> waypoints; // List of patrol waypoints
    public float moveSpeed = 1.5f;     // Movement speed
    public float waitTime = 1f;        // Time to wait at each waypoint
    public float cellReachThreshold = 0.1f; // Threshold to determine if the center of a cell or waypoint has been reached

    [Header("Detection")]
    public float detectionRadius = 0.5f; // General detection radius (kept if you want to use it for other purposes)
    public LayerMask playerLayer;        // Layer of the Player

    [Header("Vision Cone")]
    public float fieldOfViewAngle = 220f; // NEW: Increased default vision cone angle for wider front view
    public float viewDistance = 5f;      // Vision cone distance
    [Tooltip("Layer(s) of objects that block vision (walls, crates). Make sure this layer DOES NOT include the Player layer.")]
    public LayerMask viewObstacleLayer;  // Layer of objects that block vision (walls, crates)
    public float visionVerticalOffset = 0f; // Offset to adjust vision origin vertically (from top of guard's collider)
    public float multiRayAngularSpread = 30f; // NEW: Angular spread for multiple LOS rays (e.g., 30 degrees)
    public int numberOfRays = 7;         // NEW: Number of rays to cast for line of sight check (odd number is best)
    [Tooltip("Small offset to push raycast origin slightly outside Guard's collider to prevent self-intersection.")]
    public float raycastOriginOffset = 0.1f; // NEW: Offset for raycast start point

    [Header("Vision Cone Visual")]
    public UnityEngine.Rendering.Universal.Light2D guardLight; // Drag the Guard's Light2D component here

    // --- Private Variables ---
    private Rigidbody2D rb;
    private Animator animator;
    private Collider2D guardCollider; // Reference to Guard's main Collider2D
    private int currentWaypointIndex = 0;
    private bool isWaiting = false;
    private bool noReachableWaypoints = false; // Flag to track if no waypoints are reachable

    private List<Vector2Int> currentPath = new List<Vector2Int>(); // Current path in grid cells
    private int currentPathIndex = 0;                              // Current cell index on the path

    private Pathfinder2D pathfinder; // Reference to the pathfinding object
    private PathfindingGridManager gridManager; // Reference to the grid manager

    private Vector2 lastFacingDirection = Vector2.down;
    
    [SerializeField] private LayerMask visionBlockingMask; // ✅ Layer: Player + Obstacl

    [SerializeField] private float alertSpeed = 3f;
    [SerializeField] private float speedBoostDuration = 5f;

    private float currentSpeed;
    private Coroutine speedBoostCoroutine;
    

    void Awake()
    {
        if (waypoints == null)
            waypoints = new List<Transform>();

        rb = GetComponent<Rigidbody2D>();
        if (rb == null) Debug.LogError("Guard Rigidbody2D component missing!", this);

        animator = GetComponent<Animator>();
        if (animator == null) Debug.LogError("Guard Animator component missing!", this);

        // Get Guard's Collider2D for accurate vision origin
        guardCollider = GetComponent<Collider2D>();
        if (guardCollider == null) Debug.LogError("Guard Collider2D component missing! Vision calculations might be off. Add a Collider2D to Guard.", this);

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
        currentSpeed = moveSpeed;
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
                //Debug.LogWarning($"[Guard {gameObject.name}] Path exhausted or blocked before reaching final waypoint. Re-evaluating path.", this);
                FindAndStartNextReachableWaypointPath(true); // Find path again
            }
            return;
        }

        // Move the Guard to the next cell on the path
        Vector2Int targetCell = currentPath[currentPathIndex];
        Vector3 targetWorldPosition = PathfindingGridManager.GridCellToWorldCenter(targetCell, gridManager.managerCellSize, gridManager.gridOrigin);

        Vector2 moveDirection = ((Vector2)targetWorldPosition - rb.position).normalized;
        rb.linearVelocity = moveDirection * currentSpeed;

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
                    //Debug.Log($"[Guard {gameObject.name}] Reached waypoint {currentWaypointIndex}. Starting wait.");
                    StartCoroutine(WaitAtWaypoint());
                }
                else
                {
                    //Debug.LogWarning($"[Guard {gameObject.name}] Path completed, but not exactly at waypoint {currentWaypointIndex}. Retrying pathfinding.");
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
            //Debug.LogError("[Guard] Missing pathfinder or gridManager for pathfinding attempt!", this);
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
            //Debug.LogError("[Guard] Waypoints list is null or empty. Cannot find next waypoint.", this);
            rb.linearVelocity = Vector2.zero;
            animator.SetBool("IsMoving", false);
            enabled = false;
            return;
        }

        if (pathfinder == null)
        {
            //Debug.LogError("[Guard] Pathfinder is null when trying to find next reachable waypoint! Aborting.", this);
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
                //Debug.LogWarning($"[Guard] Waypoint at index {nextWaypointCheckIndex} is NULL. Skipping.", this);
                continue;
            }

            if (AttemptToFindPath(waypoints[nextWaypointCheckIndex].position))
            {
                currentWaypointIndex = nextWaypointCheckIndex;
                pathFoundAndStarted = true;
                noReachableWaypoints = false;
                //Debug.Log($"[Guard {gameObject.name}] Proceeding to reachable waypoint {currentWaypointIndex}.");
                break;
            }
            else
            {
                // Only log this if it's the currently targeted waypoint and it becomes unreachable.
                // Avoids spamming for every other waypoint in the list being unreachable during the initial scan.
                if (nextWaypointCheckIndex == currentWaypointIndex)
                {
                     Debug.LogWarning($"[Guard {gameObject.name}] Current waypoint {nextWaypointCheckIndex} is currently unreachable. Trying next available waypoint.");
                }
            }
        }

        if (!pathFoundAndStarted)
        {
            //Debug.LogWarning($"[Guard {gameObject.name}] No reachable waypoints found. Guard is staying put.");
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

        yield return new WaitForSeconds(waitTime);

        isWaiting = false;
        FindAndStartNextReachableWaypointPath(true);
    }

    // This method is called when the OnGridRefreshed event from PathfindingGridManager is triggered
    private void HandleGridRefreshed()
    {
        //Debug.Log($"[Guard {gameObject.name}] Grid refreshed event received. Re-evaluating patrol path.");

        if (PathfindingGridManager.InstancePathfinder != null)
        {
            pathfinder = PathfindingGridManager.InstancePathfinder;
            // Debug.Log($"[Guard {gameObject.name}] Updated pathfinder reference to the new grid data."); // Commented out to reduce log spam
        }
        else
        {
            //Debug.LogError($"[Guard {gameObject.name}] PathfindingGridManager.InstancePathfinder is null after refresh! Guard cannot re-evaluate path. Disabling guard.", this);
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
        Vector2 visionOrigin = (guardCollider != null)
            ? new Vector2(guardCollider.bounds.center.x, guardCollider.bounds.max.y + visionVerticalOffset)
            : (Vector2)rb.position;

        // Tìm các Player trong tầm nhìn
        Collider2D[] potentialTargets = Physics2D.OverlapCircleAll(visionOrigin, viewDistance, playerLayer);
        if (potentialTargets.Length == 0) return;

        foreach (var targetCollider in potentialTargets)
        {
            if (targetCollider == null || !targetCollider.CompareTag("Player")) continue;

            Collider2D playerCollider = targetCollider.GetComponent<Collider2D>();
            List<Vector2> playerTargetPoints = new List<Vector2>();

            if (playerCollider != null)
            {
                // Các điểm mục tiêu để raycast: trên, giữa, dưới collider
                playerTargetPoints.Add(new Vector2(playerCollider.bounds.center.x, playerCollider.bounds.max.y)); // Top
                playerTargetPoints.Add(playerCollider.bounds.center); // Center
                playerTargetPoints.Add(new Vector2(playerCollider.bounds.center.x, playerCollider.bounds.min.y)); // Bottom
            }
            else
            {
                playerTargetPoints.Add(targetCollider.transform.position); // fallback
            }

            foreach (Vector2 playerTargetPoint in playerTargetPoints)
            {
                Vector2 directionToTarget = (playerTargetPoint - visionOrigin).normalized;
                float angleToPlayer = Vector2.SignedAngle(lastFacingDirection, directionToTarget);

                // Kiểm tra trong góc nhìn
                if (Mathf.Abs(angleToPlayer) <= fieldOfViewAngle / 2f)
                {
                    // Check LOS: nếu phát hiện player không bị cản
                    bool losClear = CheckLineOfSightMultiRay(
                        visionOrigin,
                        playerTargetPoint,
                        viewDistance,
                        visionBlockingMask, // 👈 bao gồm Player + Obstacle layer
                        multiRayAngularSpread,
                        numberOfRays,
                        raycastOriginOffset
                    );

                    if (losClear)
                    {
                        //Debug.Log($"<color=lime>[Guard {gameObject.name}] Player {targetCollider.name} detected!</color>", this);
                        TriggerSpeedBoost();

                        // Vẽ tia trắng đơn để thấy hướng tổng quát
                        //Debug.DrawRay(visionOrigin, (playerTargetPoint - visionOrigin).normalized * viewDistance, Color.white, 0.1f);

                        return; // Phát hiện → dừng kiểm tra
                    }
                }
            }

            //Debug.Log($"<color=orange>[Guard {gameObject.name}] Player in cone but blocked.</color>", this);
        }
    }



    void TriggerSpeedBoost()
    {
        if (speedBoostCoroutine != null)    
        {
            StopCoroutine(speedBoostCoroutine);
        }

        speedBoostCoroutine = StartCoroutine(SpeedBoostCoroutine());
    }
    IEnumerator SpeedBoostCoroutine()
    {
        currentSpeed = alertSpeed;
        yield return new WaitForSeconds(speedBoostDuration);
        currentSpeed = moveSpeed;
        speedBoostCoroutine = null;
    }

    /// <summary>
    /// Checks Line of Sight using multiple rays that detect obstacles blocking view to the player.
    /// </summary>
    /// <param name="origin">The starting point of the rays (e.g., Guard's eye level).</param>
    /// <param name="target">The target point (e.g., Player's top collider point).</param>
    /// <param name="distance">Maximum distance for rays.</param>
    /// <param name="visionBlockingMask">LayerMask that includes Player + Obstacles.</param>
    /// <param name="angularSpread">Total angular spread for the rays (e.g., 30 degrees).</param>
    /// <param name="numRays">Total number of rays to cast (should be odd for a central ray).</param>
    /// <param name="originOffset">Offset to push ray start slightly outside the guard collider.</param>
    /// <returns>True if any ray reaches player without being blocked.</returns>
    private bool CheckLineOfSightMultiRay(Vector2 origin, Vector2 target, float distance, LayerMask visionBlockingMask, float angularSpread, int numRays, float originOffset)
    {
        Vector2 mainDirection = (target - origin).normalized;
        bool anyRayHitsPlayer = false;

        float angleStep = numRays > 1 ? angularSpread / (numRays - 1) : 0f;
        float startAngle = -angularSpread / 2f;

        for (int i = 0; i < numRays; i++)
        {
            float currentAngle = startAngle + i * angleStep;
            Vector2 rayDirection = Quaternion.Euler(0, 0, currentAngle) * mainDirection;
            Vector2 rayStart = origin + rayDirection * originOffset;

            RaycastHit2D hit = Physics2D.Raycast(rayStart, rayDirection, distance, visionBlockingMask);

            Color drawColor = Color.blue;

            if (hit.collider != null)
            {
                if (hit.collider.CompareTag("Player"))
                {
                    drawColor = Color.green; // 👀 Có thể thấy player
                    anyRayHitsPlayer = true;
                    break; // Phát hiện thành công, không cần kiểm tra tiếp
                }
                else
                {
                    drawColor = Color.red; // 🚫 Bị cản
                }
            }

            Debug.DrawRay(rayStart, rayDirection * distance, drawColor, Time.deltaTime);
        }

        return anyRayHitsPlayer;
    }



    // Collision handling functions, currently not used for main logic
    void OnCollisionEnter2D(Collision2D collision) { /* Debug.Log($"Collided with {collision.gameObject.name}"); */ }
    void OnTriggerEnter2D(Collider2D other) { /* Debug.Log($"Triggered by {other.gameObject.name}"); */ }

    // --- Gizmos for Debugging in Scene View ---
    void OnDrawGizmosSelected()
    {
        // Get the accurate point for Gizmos drawing from the top of the collider + vertical offset for Guard.
        Vector3 guardPos = (guardCollider != null) 
                           ? new Vector3(guardCollider.bounds.center.x, guardCollider.bounds.max.y + visionVerticalOffset, transform.position.z) 
                           : transform.position;

        // Debug detection radius (DetectionRadius)
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(guardPos, detectionRadius); 

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
        // This still uses rb.position as pathfinding usually works on grid cells relative to Rigidbody.
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

         // Debug Vision Cone - Ensure guardCollider is not null before drawing vision gizmos
        if (rb != null && guardCollider != null) 
        {
            // guardPos already defined above from the top of the collider + vertical offset
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

            // Draw the two outer rays of the cone
            Gizmos.DrawRay(guardPos, leftRayDirection * viewDistance); 
            Gizmos.DrawRay(guardPos, rightRayDirection * viewDistance); 

            // Draw the arc of the cone
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
        }
    }
}
