using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using KwaaktjePathfinder2D;
using UnityEngine.Rendering.Universal;
using System;

[RequireComponent(typeof(Collider2D))]
public class GuardPathfindingPatrol : MonoBehaviour
{
    // --- Public Configuration (Set in Inspector) ---
    public List<Transform> waypoints;
    public float moveSpeed = 1.5f;
    public float waitTime = 1f;
    public float cellReachThreshold = 0.1f;

    [Header("Detection")]
    public float detectionRadius = 0.5f;
    public LayerMask playerLayer;

    [Header("View Cone")]
    public float fieldOfViewAngle = 220f;
    public float viewDistance = 5f;
    [Tooltip("Layer(s) of objects that block vision (walls, crates). Make sure this DOES NOT include the Player layer.")]
    public LayerMask viewObstacleLayer;
    public float visionVerticalOffset = 0f;
    public float multiRayAngularSpread = 30f;
    public int numberOfRays = 7;
    [Tooltip("Small offset to push the raycast origin slightly outside the Guard's collider to avoid self-collision.")]
    public float raycastOriginOffset = 0.1f;

    [Header("View Cone Visuals")]
    public UnityEngine.Rendering.Universal.Light2D guardLight;

    // --- Private Variables ---
    private Rigidbody2D rb;
    private Animator animator;
    private Collider2D guardCollider;
    private int currentWaypointIndex = 0;
    private bool isWaiting = false;
    private bool noReachableWaypoints = false;

    private List<Vector2Int> currentPath = new List<Vector2Int>();
    private int currentPathIndex = 0;

    private Pathfinder2D pathfinder;
    private PathfindingGridManager gridManager;

    private Vector2 lastFacingDirection = Vector2.down;

    [SerializeField] private LayerMask visionBlockingMask;

    [SerializeField] private float alertSpeed = 3f;
    [SerializeField] private float speedBoostDuration = 5f;

    [SerializeField] private GameObject exclamationPoint;

    private float currentSpeed;
    private Coroutine speedBoostCoroutine;

    private float distanceGuardCanHearExplosion = 10f;

    // Initializes component references when the script is loaded.
    void Awake()
    {
        if (waypoints == null)
            waypoints = new List<Transform>();

        rb = GetComponent<Rigidbody2D>();
        if (rb == null) Debug.LogError("Guard Rigidbody2D component missing!", this);

        animator = GetComponent<Animator>();
        if (animator == null) Debug.LogError("Guard Animator component missing!", this);

        guardCollider = GetComponent<Collider2D>();
        if (guardCollider == null) Debug.LogError("Guard Collider2D component missing! Vision calculations might be off. Add a Collider2D to Guard.", this);

        if (guardLight == null)
        {
            guardLight = GetComponentInChildren<UnityEngine.Rendering.Universal.Light2D>();
            if (guardLight == null)
            {
                Debug.LogWarning("Guard Light2D not assigned and not found as child of " + gameObject.name + ". Vision cone visual will not work.", this);
            }
        }
    }

    // Registers grid refresh event when the GameObject is enabled.
    void OnEnable()
    {
        PathfindingGridManager.OnGridRefreshed += HandleGridRefreshed;
    }

    // Unregisters grid refresh event when the GameObject is disabled to prevent memory leaks.
    void OnDisable()
    {
        PathfindingGridManager.OnGridRefreshed -= HandleGridRefreshed;
    }

    // Initializes guard speed, checks waypoints, gets manager references, and starts patrolling.
    void Start()
    {
        currentSpeed = moveSpeed;
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

        gridManager = PathfindingGridManager.Instance;
        if (gridManager == null)
        {
            Debug.LogError("PathfindingGridManager not found (or not properly initialized) in scene! Make sure it exists and has the script attached.", this);
            enabled = false;
            return;
        }

        if (PathfindingGridManager.InstancePathfinder == null)
        {
            Debug.LogError("PathfindingGridManager.InstancePathfinder is null. Make sure PathfindingGridManager is initialized first!", this);
            enabled = false;
            return;
        }
        pathfinder = PathfindingGridManager.InstancePathfinder;

        FindAndStartNextReachableWaypointPath(false);
    }

    // Updates animations, facing direction, Light2D direction, and starts player detection each frame.
    void Update()
    {
        if (BombController.Instance.isBombInExplosion)
        {
            StartCoroutine(DetectBombExplosion());
        }
        Vector2 currentVelocity = rb.linearVelocity;
        if (currentVelocity.magnitude > 0.05f)
        {
            animator.SetBool("IsMoving", true);
            animator.SetFloat("MoveX", currentVelocity.x);
            animator.SetFloat("MoveY", currentVelocity.y);
            lastFacingDirection = currentVelocity.normalized;
        }
        else
        {
            animator.SetBool("IsMoving", false);
        }

        if (guardLight != null)
        {
            float angle = Mathf.Atan2(lastFacingDirection.y, lastFacingDirection.x) * Mathf.Rad2Deg - 90f;
            guardLight.transform.rotation = Quaternion.Euler(0, 0, angle);
        }

        DetectPlayer();
    }

    // Guard run faster for 3s if hear bomb's explosion
    private IEnumerator DetectBombExplosion()
    {
        Vector2 currentGuardPosition = rb.transform.position;
        float distanceFromGuardToExplosion = Vector2.Distance(currentGuardPosition, BombController.Instance.bombPlacedPosition);

        if (distanceFromGuardToExplosion < distanceGuardCanHearExplosion)
        {
            moveSpeed = 2.5f;
            exclamationPoint.SetActive(true);
            yield return new WaitForSeconds(3.0f);
            moveSpeed = 2.0f;
            exclamationPoint.SetActive(false);
        }
    }

    // Handles guard movement along the path in fixed time steps.
    void FixedUpdate()
    {
        if (isWaiting || noReachableWaypoints)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        if (currentPath == null || currentPath.Count == 0 || currentPathIndex >= currentPath.Count)
        {
            if (waypoints != null && waypoints.Count > currentWaypointIndex && waypoints[currentWaypointIndex] != null &&
                Vector2.Distance(rb.position, waypoints[currentWaypointIndex].position) < cellReachThreshold)
            {
                Debug.Log($"[Guard {gameObject.name}] Reached final waypoint {currentWaypointIndex}. Starting wait.");
                StartCoroutine(WaitAtWaypoint());
            }
            else
            {
                rb.linearVelocity = Vector2.zero;
                animator.SetBool("IsMoving", false);
                FindAndStartNextReachableWaypointPath(true);
            }
            return;
        }

        Vector2Int targetCell = currentPath[currentPathIndex];
        Vector3 targetWorldPosition = PathfindingGridManager.GridCellToWorldCenter(targetCell, gridManager.managerCellSize, gridManager.gridOrigin);

        Vector2 moveDirection = ((Vector2)targetWorldPosition - rb.position).normalized;
        rb.linearVelocity = moveDirection * currentSpeed;

        if (Vector2.Distance(rb.position, targetWorldPosition) < cellReachThreshold)
        {
            rb.position = targetWorldPosition;
            currentPathIndex++;

            if (currentPathIndex >= currentPath.Count)
            {
                rb.linearVelocity = Vector2.zero;
                if (waypoints != null && waypoints.Count > currentWaypointIndex && waypoints[currentWaypointIndex] != null &&
                    Vector2.Distance(rb.position, waypoints[currentWaypointIndex].position) < cellReachThreshold)
                {
                    StartCoroutine(WaitAtWaypoint());
                }
                else
                {
                    FindAndStartNextReachableWaypointPath(true);
                }
            }
        }
    }

    // Attempts to find a path to a target world position using the pathfinding engine.
    private bool AttemptToFindPath(Vector3 targetWorldPos)
    {
        if (pathfinder == null || gridManager == null)
        {
            return false;
        }

        Vector2Int startCell = PathfindingGridManager.WorldToGridCell(rb.position, gridManager.gridSize, gridManager.managerCellSize, gridManager.gridOrigin);
        Vector2Int endCell = PathfindingGridManager.WorldToGridCell(targetWorldPos, gridManager.gridSize, gridManager.managerCellSize, gridManager.gridOrigin);

        Pathfinder2DResult pathResult = pathfinder.FindPath(startCell, endCell);

        if (pathResult.Status == Pathfinder2DResultStatus.SUCCESS && pathResult.Path.Count > 0)
        {
            currentPath = new List<Vector2Int>(pathResult.Path);
            currentPath.Reverse();

            Vector2Int currentGuardCell = PathfindingGridManager.WorldToGridCell(rb.position, gridManager.gridSize, gridManager.managerCellSize, gridManager.gridOrigin);
            if (currentPath.Count > 0 && currentPath[0] == currentGuardCell)
            {
                currentPath.RemoveAt(0);
            }

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
    private void FindAndStartNextReachableWaypointPath(bool advanceIndexFirst)
    {
        if (waypoints == null || waypoints.Count == 0)
        {
            rb.linearVelocity = Vector2.zero;
            animator.SetBool("IsMoving", false);
            enabled = false;
            return;
        }

        if (pathfinder == null)
        {
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
                continue;
            }

            if (AttemptToFindPath(waypoints[nextWaypointCheckIndex].position))
            {
                currentWaypointIndex = nextWaypointCheckIndex;
                pathFoundAndStarted = true;
                noReachableWaypoints = false;
                break;
            }
            else
            {
                if (nextWaypointCheckIndex == currentWaypointIndex)
                {
                    Debug.LogWarning($"[Guard {gameObject.name}] Current waypoint {nextWaypointCheckIndex} is currently unreachable. Trying next available waypoint.");
                }
            }
        }

        if (!pathFoundAndStarted)
        {
            rb.linearVelocity = Vector2.zero;
            animator.SetBool("IsMoving", false);
            noReachableWaypoints = true;
            isWaiting = true;
        }
    }

    // Coroutine for the guard to wait at a waypoint for a specified duration.
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

    // Handles grid refresh event, re-evaluating the guard's patrol path.
    private void HandleGridRefreshed()
    {
        if (PathfindingGridManager.InstancePathfinder != null)
        {
            pathfinder = PathfindingGridManager.InstancePathfinder;
        }
        else
        {
            noReachableWaypoints = true;
            rb.linearVelocity = Vector2.zero;
            enabled = false;
            return;
        }

        currentPath.Clear();
        currentPathIndex = 0;
        isWaiting = false;
        noReachableWaypoints = false;

        FindAndStartNextReachableWaypointPath(false);
    }

    // Detects the player using a view cone by casting multiple rays.
    void DetectPlayer()
    {
        Vector2 visionOrigin = (guardCollider != null)
            ? new Vector2(guardCollider.bounds.center.x, guardCollider.bounds.max.y + visionVerticalOffset)
            : (Vector2)rb.position;

        Collider2D[] potentialTargets = Physics2D.OverlapCircleAll(visionOrigin, viewDistance, playerLayer);
        if (potentialTargets.Length == 0) return;

        foreach (var targetCollider in potentialTargets)
        {
            if (targetCollider == null || !targetCollider.CompareTag("Player")) continue;

            Collider2D playerCollider = targetCollider.GetComponent<Collider2D>();
            List<Vector2> playerTargetPoints = new List<Vector2>();

            if (playerCollider != null)
            {
                playerTargetPoints.Add(new Vector2(playerCollider.bounds.center.x, playerCollider.bounds.max.y));
                playerTargetPoints.Add(playerCollider.bounds.center);
                playerTargetPoints.Add(new Vector2(playerCollider.bounds.center.x, playerCollider.bounds.min.y));
            }
            else
            {
                playerTargetPoints.Add(targetCollider.transform.position);
            }

            foreach (Vector2 playerTargetPoint in playerTargetPoints)
            {
                Vector2 directionToTarget = (playerTargetPoint - visionOrigin).normalized;
                float angleToPlayer = Vector2.SignedAngle(lastFacingDirection, directionToTarget);

                if (Mathf.Abs(angleToPlayer) <= fieldOfViewAngle / 2f)
                {
                    bool losClear = CheckLineOfSightMultiRay(
                        visionOrigin,
                        playerTargetPoint,
                        viewDistance,
                        visionBlockingMask,
                        multiRayAngularSpread,
                        numberOfRays,
                        raycastOriginOffset
                    );

                    if (losClear)
                    {
                        exclamationPoint.SetActive(true);
                        TriggerSpeedBoost();
                        return;
                    } else
                    {
                        exclamationPoint.SetActive(false);
                    }
                }
            }
        }
    }

    // Triggers a temporary speed boost for the guard when the player is detected.
    void TriggerSpeedBoost()
    {
        if (speedBoostCoroutine != null)
        {
            StopCoroutine(speedBoostCoroutine);
        }

        speedBoostCoroutine = StartCoroutine(SpeedBoostCoroutine());
    }

    // Coroutine that handles the duration of the temporary speed boost.
    IEnumerator SpeedBoostCoroutine()
    {
        currentSpeed = alertSpeed;
        yield return new WaitForSeconds(speedBoostDuration);
        currentSpeed = moveSpeed;
        speedBoostCoroutine = null;
    }

    // Checks line of sight using multiple rays for obstacles blocking view to the player.
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
                    drawColor = Color.green;
                    anyRayHitsPlayer = true;
                    break;
                }
                else
                {
                    drawColor = Color.red;
                }
            }

            Debug.DrawRay(rayStart, rayDirection * distance, drawColor, Time.deltaTime);
        }

        return anyRayHitsPlayer;
    }


    // Handles 2D physics collisions (currently not used for core logic).
    void OnCollisionEnter2D(Collision2D collision) { /* Debug.Log($"Collided with {collision.gameObject.name}"); */ }

    // Handles 2D trigger collisions (currently not used for core logic).
    void OnTriggerEnter2D(Collider2D other) { /* Debug.Log($"Triggered by {other.gameObject.name}"); */ }

    // Draws visual debugging aids in the Scene view, such as patrol paths, detection radius, and view cone.
    void OnDrawGizmosSelected()
    {
        Vector3 guardPos = (guardCollider != null)
                               ? new Vector3(guardCollider.bounds.center.x, guardCollider.bounds.max.y + visionVerticalOffset, transform.position.z)
                               : transform.position;

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(guardPos, detectionRadius);

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

        if (rb != null && guardCollider != null)
        {
            Vector2 forwardDir = lastFacingDirection;

            Gizmos.color = new Color(1f, 0.5f, 0f, 0.2f);
            Gizmos.DrawSphere(guardPos, viewDistance);

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
        }
    }
}
