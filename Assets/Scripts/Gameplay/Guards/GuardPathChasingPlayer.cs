using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using KwaaktjePathfinder2D;

[RequireComponent(typeof(Collider2D))]
public class GuardPathChasingPlayer : MonoBehaviour
{
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
    private bool lostPlayer = false;
    private bool recentlyLostPlayer = false;
    public float lostPlayerCooldown = 3f;

    private List<Vector2Int> currentPath = new List<Vector2Int>();
    private int currentPathIndex = 0;

    private Pathfinder2D pathfinder;
    private PathfindingGridManager gridManager;

    private Vector2 lastFacingDirection = Vector2.down;
    private Vector3 lastKnownPlayerPosition;

    public UnityEngine.Rendering.Universal.Light2D guardLight; // Updated guardLight reference

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        guardCollider = GetComponent<Collider2D>();
    }

    void OnEnable()
    {
        PathfindingGridManager.OnGridRefreshed += HandleGridRefreshed;
    }

    void OnDisable()
    {
        PathfindingGridManager.OnGridRefreshed -= HandleGridRefreshed;
    }

    void Start()
    {
        gridManager = PathfindingGridManager.Instance;
        pathfinder = PathfindingGridManager.InstancePathfinder;
        if (waypoints == null || waypoints.Count == 0)
        {
            enabled = false;
            return;
        }
        StartPatrol();
    }

    void Update()
    {
        // Luôn kiểm tra tầm nhìn player, kể cả khi lostPlayer
        DetectPlayer();

        if (isChasing && !lostPlayer)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                AttemptToFindPath(player.transform.position);
            }
        }
        UpdateAnimation();

        if (guardLight != null)
        {
            float angle = Mathf.Atan2(lastFacingDirection.y, lastFacingDirection.x) * Mathf.Rad2Deg - 90f;
            guardLight.transform.rotation = Quaternion.Euler(0, 0, angle);
        }
    }

    void FixedUpdate()
    {
        if (isWaiting) { rb.linearVelocity = Vector2.zero; return; }

        if (currentPath == null || currentPath.Count == 0 || currentPathIndex >= currentPath.Count)
        {
            if (isChasing && lostPlayer)
            {
                StartCoroutine(WaitAtLostPlayerPosition());
            }
            else if (!isChasing)
            {
                StartCoroutine(WaitAtWaypoint());
            }
            rb.linearVelocity = Vector2.zero;
            return;
        }

        Vector2Int targetCell = currentPath[currentPathIndex];
        Vector3 targetWorldPosition = PathfindingGridManager.GridCellToWorldCenter(targetCell, gridManager.managerCellSize, gridManager.gridOrigin);
        Vector2 moveDirection = ((Vector2)targetWorldPosition - rb.position).normalized;
        rb.linearVelocity = moveDirection * (isChasing ? chaseSpeed : moveSpeed);

        if (moveDirection.sqrMagnitude > 0.01f)
        {
            lastFacingDirection = moveDirection;
        }

        if (Vector2.Distance(rb.position, targetWorldPosition) < cellReachThreshold)
        {
            rb.position = targetWorldPosition;
            currentPathIndex++;
        }
    }

    void StartPatrol()
    {
        isChasing = false;
        lostPlayer = false;
        isWaiting = false;
        FindAndStartNextReachableWaypointPath(false);
    }

    void StartChase(Vector3 playerPosition)
    {
        isChasing = true;
        lostPlayer = false;
        lastKnownPlayerPosition = playerPosition;
        StopAllCoroutines();
        AttemptToFindPath(playerPosition);
    }

    void LosePlayer()
    {
        lostPlayer = true;
        AttemptToFindPath(lastKnownPlayerPosition);
    }

    void DetectPlayer()
    {
        if (recentlyLostPlayer) return;

        Vector2 visionOrigin = (guardCollider != null)
            ? new Vector2(guardCollider.bounds.center.x, guardCollider.bounds.max.y + visionVerticalOffset)
            : (Vector2)rb.position;

        Collider2D[] potentialTargets = Physics2D.OverlapCircleAll(visionOrigin, viewDistance, playerLayer);
        bool playerDetected = false;

        foreach (var targetCollider in potentialTargets)
        {
            if (targetCollider == null || !targetCollider.CompareTag("Player")) continue;

            Vector2 playerPos = targetCollider.transform.position;
            Vector2 directionToPlayer = (playerPos - visionOrigin).normalized;
            float angleToPlayer = Vector2.SignedAngle(lastFacingDirection, directionToPlayer);

            if (Mathf.Abs(angleToPlayer) <= fieldOfViewAngle / 2f)
            {
                // Vẽ các tia kiểm tra tầm nhìn (multi-ray)
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
                            debugColor = Color.green;
                        else
                            debugColor = Color.red;
                    }
                    Debug.DrawRay(rayStart, rayDirection * viewDistance, debugColor, 0.1f);
                }

                if (CheckLineOfSight(visionOrigin, playerPos))
                {
                    playerDetected = true;
                    // Nếu đang lostPlayer hoặc patrol, chuyển sang chase ngay lập tức
                    if (!isChasing || lostPlayer)
                    {
                        StartChase(playerPos);
                    }
                    else
                    {
                        lastKnownPlayerPosition = playerPos;
                    }
                    break;
                }
            }
        }

        // Nếu đang chase mà không còn thấy player nữa, thì mất dấu
        if (isChasing && !playerDetected && !lostPlayer)
        {
            LosePlayer();
        }
    }

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
                return true;
        }
        return false;
    }

    void UpdateAnimation()
    {
        Vector2 currentVelocity = rb.linearVelocity;
        if (animator == null) return;
        if (currentVelocity.magnitude > 0.05f)
        {
            animator.SetBool("IsMoving", true);
            animator.SetFloat("MoveX", currentVelocity.x);
            animator.SetFloat("MoveY", currentVelocity.y);
            // Do NOT update lastFacingDirection here anymore!
        }
        else
        {
            animator.SetBool("IsMoving", false);
        }   
    }

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
                currentPath.RemoveAt(0);
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

    IEnumerator WaitAtWaypoint()
    {
        isWaiting = true;
        rb.linearVelocity = Vector2.zero;
        yield return new WaitForSeconds(waitTime);
        isWaiting = false;
        FindAndStartNextReachableWaypointPath(true);
    }

    IEnumerator WaitAtLostPlayerPosition()
    {
        isWaiting = true;
        rb.linearVelocity = Vector2.zero;
        yield return new WaitForSeconds(lostPlayerWaitTime);
        isWaiting = false;
        recentlyLostPlayer = true;
        StartPatrol();
        StartCoroutine(LostPlayerCooldownCoroutine());
    }

    IEnumerator LostPlayerCooldownCoroutine()
    {
        yield return new WaitForSeconds(lostPlayerCooldown);
        recentlyLostPlayer = false;
    }

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
        if (isChasing && !lostPlayer)
        {
            // Đang chase thì tìm lại đường đến player
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                AttemptToFindPath(player.transform.position);
        }
        else if (isChasing && lostPlayer)
        {
            // Đang mất dấu thì tìm lại đường về vị trí cuối cùng thấy player
            AttemptToFindPath(lastKnownPlayerPosition);
        }
        else
        {
            // Patrol bình thường
            FindAndStartNextReachableWaypointPath(false);
        }
    }

    void OnDrawGizmosSelected()
    {
        if (guardCollider == null) guardCollider = GetComponent<Collider2D>();
        Vector3 guardPos = (guardCollider != null)
            ? new Vector3(guardCollider.bounds.center.x, guardCollider.bounds.max.y + visionVerticalOffset, transform.position.z)
            : transform.position;

        // Vẽ bán kính phát hiện (DetectionRadius)
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(guardPos, viewDistance);

        // Vẽ hình nón tầm nhìn (màu vàng)
        Gizmos.color = Color.yellow;
        float halfFOV = fieldOfViewAngle / 2f;
        Vector2 forwardDir = lastFacingDirection.sqrMagnitude > 0.01f ? lastFacingDirection : Vector2.down;

        Quaternion leftRayRotation = Quaternion.AngleAxis(-halfFOV, Vector3.forward);
        Quaternion rightRayRotation = Quaternion.AngleAxis(halfFOV, Vector3.forward);

        Vector3 leftRayDirection = leftRayRotation * forwardDir;
        Vector3 rightRayDirection = rightRayRotation * forwardDir;

        Gizmos.DrawRay(guardPos, leftRayDirection * viewDistance);
        Gizmos.DrawRay(guardPos, rightRayDirection * viewDistance);

        // Vẽ cung tròn hình nón
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
