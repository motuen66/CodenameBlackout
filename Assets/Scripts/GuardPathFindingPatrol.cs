using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using KwaaktjePathfinder2D;
using UnityEngine.Rendering.Universal; // RẤT QUAN TRỌNG: Thêm dòng này để sử dụng Light2D

public class GuardPathfindingPatrol : MonoBehaviour
{
    // --- Public Configuration (Set in Inspector) ---
    public List<Transform> waypoints; // Danh sách các waypoint tuần tra
    public float moveSpeed = 1.5f;   // Tốc độ di chuyển
    public float waitTime = 1f;      // Thời gian chờ ở mỗi waypoint
    public float cellReachThreshold = 0.1f; // Ngưỡng để xác định đã đến giữa ô hay waypoint

    [Header("Detection")]
    public float detectionRadius = 0.5f; // Bán kính phát hiện tổng quát (vẫn giữ lại nếu bạn muốn dùng cho mục đích khác)
    public LayerMask playerLayer;        // Layer của Player

    [Header("Vision Cone")]
    public float fieldOfViewAngle = 90f; // Góc nhìn hình nón
    public float viewDistance = 5f;      // Tầm nhìn xa của nón
    public LayerMask viewObstacleLayer;  // Layer của các vật thể cản tầm nhìn (tường, thùng)

    [Header("Vision Cone Visual")]
    public UnityEngine.Rendering.Universal.Light2D guardLight; // Kéo component Light2D của Guard vào đây

    // --- Private Variables ---
    private Rigidbody2D rb;
    private Animator animator;
    private int currentWaypointIndex = 0;
    private bool isWaiting = false;
    private bool noReachableWaypoints = false; // Cờ theo dõi nếu không có waypoint nào đến được

    private List<Vector2Int> currentPath = new List<Vector2Int>(); // Đường đi hiện tại theo ô lưới
    private int currentPathIndex = 0;                             // Chỉ số ô hiện tại trên đường đi

    private Pathfinder2D pathfinder; // Tham chiếu đến đối tượng tìm đường
    private PathfindingGridManager gridManager; // Tham chiếu đến quản lý lưới

    private Vector2 lastFacingDirection = Vector2.down; // Hướng nhìn cuối cùng của Guard, mặc định là xuống

    void Awake()
    {
        if (waypoints == null)
            waypoints = new List<Transform>();

        rb = GetComponent<Rigidbody2D>();
        if (rb == null) Debug.LogError("Guard Rigidbody2D component missing!", this);

        animator = GetComponent<Animator>();
        if (animator == null) Debug.LogError("Guard Animator component missing!", this);

        // Lấy Light2D tự động nếu chưa được gán trong Inspector (nếu nó là child)
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
        // Đăng ký lắng nghe sự kiện khi lưới tìm đường được làm mới
        PathfindingGridManager.OnGridRefreshed += HandleGridRefreshed;
    }

    void OnDisable()
    {
        // Hủy đăng ký sự kiện để tránh rò rỉ bộ nhớ
        PathfindingGridManager.OnGridRefreshed -= HandleGridRefreshed;
    }

    void Start()
    {
        // Kiểm tra tính hợp lệ của danh sách waypoints
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

        // Lấy tham chiếu đến PathfindingGridManager trong Scene
        gridManager = Object.FindFirstObjectByType<PathfindingGridManager>();
        if (gridManager == null)
        {
            Debug.LogError("PathfindingGridManager not found in scene! Make sure it exists.", this);
            enabled = false;
            return;
        }

        // Lấy tham chiếu đến Pathfinder2D từ GridManager
        if (PathfindingGridManager.InstancePathfinder == null)
        {
            Debug.LogError("PathfindingGridManager.InstancePathfinder is null. Make sure PathfindingGridManager is initialized first!", this);
            enabled = false;
            return;
        }
        pathfinder = PathfindingGridManager.InstancePathfinder;

        // Bắt đầu tuần tra từ waypoint đầu tiên có thể đến được
        FindAndStartNextReachableWaypointPath(false);
    }

    void Update()
    {
        // Cập nhật animation và hướng nhìn dựa trên vận tốc hiện tại của Guard
        Vector2 currentVelocity = rb.linearVelocity;
        if (currentVelocity.magnitude > 0.05f) // Nếu Guard đang di chuyển đủ nhanh
        {
            animator.SetBool("IsMoving", true);
            animator.SetFloat("MoveX", currentVelocity.x);
            animator.SetFloat("MoveY", currentVelocity.y);

            // Cập nhật hướng nhìn cuối cùng chỉ khi Guard đang di chuyển
            lastFacingDirection = currentVelocity.normalized;
        }
        else
        {
            animator.SetBool("IsMoving", false);
            // Giữ nguyên lastFacingDirection khi đứng yên để hướng nhìn không bị reset
        }

        // QUAN TRỌNG: Cập nhật hướng của Light2D của Guard
        if (guardLight != null)
        {
            float angle = Mathf.Atan2(lastFacingDirection.y, lastFacingDirection.x) * Mathf.Rad2Deg - 90f;
            guardLight.transform.rotation = Quaternion.Euler(0, 0, angle);
        }

        // Thực hiện logic phát hiện người chơi
        DetectPlayer();
    }

    void FixedUpdate()
    {
        // Nếu Guard đang chờ hoặc không có waypoint nào đến được, dừng di chuyển
        if (isWaiting || noReachableWaypoints)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        // Nếu đường đi hiện tại đã kết thúc hoặc không tồn tại
        if (currentPath == null || currentPath.Count == 0 || currentPathIndex >= currentPath.Count)
        {
            // Kiểm tra xem Guard đã đến waypoint đích cuối cùng chưa
            if (waypoints != null && waypoints.Count > currentWaypointIndex && waypoints[currentWaypointIndex] != null &&
                Vector2.Distance(rb.position, waypoints[currentWaypointIndex].position) < cellReachThreshold)
            {
                Debug.Log($"[Guard {gameObject.name}] Reached final waypoint {currentWaypointIndex}. Starting wait.");
                StartCoroutine(WaitAtWaypoint()); // Bắt đầu chờ
            }
            else
            {
                // Nếu đường đi hết nhưng chưa đến waypoint (ví dụ: bị chặn giữa chừng), thử tìm đường lại
                rb.linearVelocity = Vector2.zero;
                animator.SetBool("IsMoving", false);
                Debug.LogWarning($"[Guard {gameObject.name}] Path exhausted or blocked before reaching final waypoint. Re-evaluating path.", this);
                FindAndStartNextReachableWaypointPath(true); // Tìm lại đường
            }
            return;
        }

        // Di chuyển Guard đến ô tiếp theo trên đường đi
        Vector2Int targetCell = currentPath[currentPathIndex];
        Vector3 targetWorldPosition = PathfindingGridManager.GridCellToWorldCenter(targetCell, gridManager.managerCellSize, gridManager.gridOrigin);

        Vector2 moveDirection = ((Vector2)targetWorldPosition - rb.position).normalized;
        rb.linearVelocity = moveDirection * moveSpeed;

        // Nếu Guard đã đến gần ô đích hiện tại
        if (Vector2.Distance(rb.position, targetWorldPosition) < cellReachThreshold)
        {
            rb.position = targetWorldPosition; // Đặt chính xác vào giữa ô
            currentPathIndex++; // Chuyển sang ô tiếp theo

            // Nếu đã đi hết các ô trong đường đi hiện tại
            if (currentPathIndex >= currentPath.Count)
            {
                rb.linearVelocity = Vector2.zero;
                // Kiểm tra lại xem đã đến waypoint chính xác chưa
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
    /// Cố gắng tìm đường đến một vị trí đích.
    /// </summary>
    /// <param name="targetWorldPos">Vị trí thế giới của đích.</param>
    /// <returns>True nếu tìm được đường, False nếu không.</returns>
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
            currentPath.Reverse(); // Đảo ngược để có đường từ vị trí hiện tại đến đích

            Vector2Int currentGuardCell = PathfindingGridManager.WorldToGridCell(rb.position, gridManager.gridSize, gridManager.managerCellSize, gridManager.gridOrigin);
            if (currentPath.Count > 0 && currentPath[0] == currentGuardCell)
            {
                currentPath.RemoveAt(0); // Loại bỏ ô hiện tại nếu đường dẫn bắt đầu bằng chính ô đó
            }

            currentPathIndex = 0;
            return true;
        }
        else
        {
            currentPath.Clear();
            rb.linearVelocity = Vector2.zero; // Dừng lại nếu không tìm thấy đường
            return false;
        }
    }

    /// <summary>
    /// Tìm waypoint tiếp theo có thể đi tới trong danh sách tuần tra và bắt đầu đường đi đến đó.
    /// </summary>
    /// <param name="advanceIndexFirst">Nếu là true, sẽ tăng currentWaypointIndex lên trước khi tìm kiếm.</param>
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
                Debug.Log($"[Guard {gameObject.name}] Waypoint {nextWaypointCheckIndex} is currently unreachable. Trying next.", this);
            }
        }

        if (!pathFoundAndStarted)
        {
            Debug.LogWarning($"[Guard {gameObject.name}] No reachable waypoints found. Guard is staying put.", this);
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

        Debug.Log("Guard " + gameObject.name + " reached waypoint " + currentWaypointIndex + ". Waiting.", this);
        yield return new WaitForSeconds(waitTime);

        isWaiting = false;
        FindAndStartNextReachableWaypointPath(true);
    }

    // Hàm này được gọi khi sự kiện OnGridRefreshed từ PathfindingGridManager được kích hoạt
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
            Debug.LogError($"[Guard {gameObject.name}] PathfindingGridManager.InstancePathfinder is null after refresh! Guard cannot re-evaluate path.", this);
            noReachableWaypoints = true;
            rb.linearVelocity = Vector2.zero;
            return;
        }

        currentPath.Clear();
        currentPathIndex = 0;
        currentWaypointIndex = 0;
        isWaiting = false;
        noReachableWaypoints = false;

        FindAndStartNextReachableWaypointPath(false);
    }

    // Hàm phát hiện người chơi sử dụng tầm nhìn hình nón
    void DetectPlayer()
    {
        // Sử dụng OverlapCircleAll để lấy tất cả collider trong tầm nhìn xa nhất (viewDistance)
        Collider2D[] potentialTargets = Physics2D.OverlapCircleAll(transform.position, viewDistance, playerLayer);

        foreach (var targetCollider in potentialTargets)
        {
            if (targetCollider.CompareTag("Player")) // Đảm bảo đối tượng là Player
            {
                Vector2 directionToTarget = ((Vector2)targetCollider.transform.position - (Vector2)transform.position).normalized;

                // Tính góc giữa hướng nhìn của Guard và hướng đến Player
                float angleToPlayer = Vector2.SignedAngle(lastFacingDirection, directionToTarget);

                // Kiểm tra xem Player có nằm trong góc nhìn hay không
                if (Mathf.Abs(angleToPlayer) < fieldOfViewAngle / 2f)
                {
                    // Kiểm tra Line of Sight (đường nhìn có bị vật cản che không)
                    RaycastHit2D hit = Physics2D.Raycast(transform.position, directionToTarget, viewDistance, viewObstacleLayer);

                    // Nếu Raycast không chạm vào vật cản nào HOẶC nếu nó chạm vào chính Player
                    if (hit.collider == null || hit.collider.CompareTag("Player"))
                    {
                        Debug.Log("Guard " + gameObject.name + " detected player: " + targetCollider.name + " via vision cone!", this);
                        // TODO: Kích hoạt logic phát hiện Player ở đây (ví dụ: chuyển sang trạng thái truy đuổi, báo động)
                        return; // Phát hiện được player, không cần kiểm tra collider khác
                    }
                }
            }
        }
    }

    // Các hàm xử lý va chạm, hiện tại không được sử dụng cho logic chính
    void OnCollisionEnter2D(Collision2D collision) { /* Debug.Log($"Collided with {collision.gameObject.name}"); */ }
    void OnTriggerEnter2D(Collider2D other) { /* Debug.Log($"Triggered by {other.gameObject.name}"); */ }

    // --- Gizmos for Debugging in Scene View ---
    void OnDrawGizmosSelected()
    {
        // Debug bán kính phát hiện (DetectionRadius)
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

        // Debug đường tuần tra (waypoints)
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

        // Debug đường đi hiện tại từ Pathfinder2D
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

        // Debug tầm nhìn hình nón (Vision Cone)
        if (rb != null)
        {
            Vector3 guardPos = transform.position;
            Vector2 forwardDir = lastFacingDirection;

            // Vẽ tầm nhìn xa tổng quát (vòng tròn màu cam mờ)
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.2f); // Màu cam trong suốt
            Gizmos.DrawSphere(guardPos, viewDistance);

            // Vẽ nón nhìn (màu vàng)
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

            // Vẽ tia chính giữa của nón nhìn
            Gizmos.DrawLine(guardPos, guardPos + (Vector3)forwardDir * viewDistance);

            // Debug Line of Sight (tùy chọn) - Hiển thị tia Raycast bị chặn bởi vật cản
            // Chỉ chạy trong Play Mode để tránh Raycast liên tục trong Editor
            if (Application.isPlaying)
            {
                Vector2 raycastDir = forwardDir; // Hướng Raycast
                RaycastHit2D hit = Physics2D.Raycast(guardPos, raycastDir, viewDistance, viewObstacleLayer);
                Gizmos.color = hit.collider != null ? Color.red : Color.blue;
                Gizmos.DrawRay(guardPos, raycastDir * viewDistance);
            }
        }
    }
}
