using UnityEngine;
using System.Collections.Generic;
using KwaaktjePathfinder2D;
using System; // Required for Action event

public class PathfindingGridManager : MonoBehaviour
{
    // Configure your virtual grid in the Inspector
    public Vector2Int gridSize = new Vector2Int(20, 20); // Grid size (number of cells)
    public Vector2 managerCellSize = Vector2.one; // Size of each cell (e.g., 1x1 Unity units), renamed to avoid confusion
    public Vector2 gridOrigin = Vector2.zero; // Origin point (bottom-left corner) of the grid in World Space

    // Layer of obstacles (your Prefabs)
    public LayerMask obstacleLayer;

    public float defaultWalkCost = 1.0f; // Default movement cost for empty cells
    public float difficultTerrainWeight = 100.0f; // Weight for difficult terrain (if applicable, you need to define this)

    // Variable for other scripts to access Pathfinder2D
    public static Pathfinder2D InstancePathfinder;

    // NEW EVENT: Fired when the grid is refreshed
    public static event Action OnGridRefreshed;

    // --- SINGLETON IMPLEMENTATION ---
    private static PathfindingGridManager _instance;
    public static PathfindingGridManager Instance
    {
        get
        {
            if (_instance == null)
            {
                // Try to find an existing instance in the scene
                _instance = FindObjectOfType<PathfindingGridManager>();
                if (_instance == null)
                {
                    // If no instance found, create a new GameObject and add the component
                    GameObject singletonObject = new GameObject(typeof(PathfindingGridManager).Name);
                    _instance = singletonObject.AddComponent<PathfindingGridManager>();
                    Debug.LogWarning($"No PathfindingGridManager found in scene. Creating a new one named {singletonObject.name}.", _instance);
                }
            }
            return _instance;
        }
    }
    // --- END SINGLETON IMPLEMENTATION ---

    // Stores the map and blockers here for Gizmo visualization
    private Dictionary<Vector2Int, float> _currentWeightedMap;
    private List<Vector2Int> _currentMovementBlockers;

    void Awake()
    {
        // Ensure only one instance of PathfindingGridManager exists
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject); // Destroy this duplicate instance
            Debug.LogWarning($"Duplicate PathfindingGridManager found. Destroying {gameObject.name}.");
            return;
        }
        _instance = this; // Set this instance as the singleton

        InitializeGrid(); // Initialize the grid on Awake
        Debug.Log("Pathfinder2D initialized by PathfindingGridManager in Awake.");
    }

    /// <summary>
    /// Initializes the grid data and Pathfinder2D instance.
    /// This method can be called from Awake or RefreshGridData.
    /// </summary>
    private void InitializeGrid()
    {
        // Initialize weightedMap and movementBlockers by scanning for obstacles
        GetWeightedAndBlockedMap(out _currentWeightedMap, out _currentMovementBlockers);

        // Select NodeConnectionType: RectangleNoDiagonals for orthogonal movement (no diagonals)
        NodeConnectionType connectionType = NodeConnectionType.RectangleNoDiagonals;

        // Create or update InstancePathfinder with new grid data
        InstancePathfinder = new Pathfinder2D(_currentWeightedMap, _currentMovementBlockers, connectionType);
    }

    /// <summary>
    /// Refreshes the grid data, re-initializes the pathfinder, and notifies subscribers.
    /// Call this method when obstacles are added or removed (e.g., when a destructible wall is destroyed).
    /// </summary>
    public void RefreshGridData()
    {
        Debug.Log("PathfindingGridManager: Refreshing grid data and pathfinder...");
        InitializeGrid(); // Re-initialize the grid and pathfinder with new data
        // Invoke the event to notify all subscribed objects
        OnGridRefreshed?.Invoke();
        Debug.Log("PathfindingGridManager: Grid data refreshed and event invoked.");
    }

    // This function scans the space to find obstacles and create the weightedMap
    private void GetWeightedAndBlockedMap(out Dictionary<Vector2Int, float> weightedMap, out List<Vector2Int> movementBlockers)
    {
        weightedMap = new Dictionary<Vector2Int, float>();
        movementBlockers = new List<Vector2Int>();

        // Scan each cell in your virtual grid (from (0,0) to (gridSize.x-1, gridSize.y-1))
        for (int x = 0; x < gridSize.x; x++)
        {
            for (int y = 0; y < gridSize.y; y++)
            {
                Vector2Int gridCell = new Vector2Int(x, y);
                // Get the world center position of the current grid cell
                Vector2 worldCellCenter = GridCellToWorldCenter(gridCell, managerCellSize, gridOrigin);

                // Use Physics2D.OverlapBox to check if there are any obstacles in this cell
                // managerCellSize * 0.9f: Size of the check box, slightly smaller than the actual cell size
                // This helps to prevent obstacles exactly on the boundary between cells from being detected in both cells.
                // 0f: Rotation angle of the box (0 degrees because it's 2D and no rotation is needed)
                // obstacleLayer: LayerMask specifies which layers are considered obstacles
                Collider2D hitCollider = Physics2D.OverlapBox(worldCellCenter, managerCellSize * 0.9f, 0f, obstacleLayer);

                if (hitCollider != null)
                {
                    movementBlockers.Add(gridCell); // Mark this cell as blocked
                }
                else
                {
                    weightedMap.Add(gridCell, defaultWalkCost); // Mark this cell as walkable with default cost
                }
            }
        }

        Debug.Log($"PathfindingGridManager: Weighted Map Size: {weightedMap.Count}, Movement Blockers Size: {movementBlockers.Count} after scan.");
    }

    // Function to convert World Position to Grid Cell (static so it can be called from other scripts)
    public static Vector2Int WorldToGridCell(Vector3 worldPosition, Vector2Int _gridSize, Vector2 _cellSize, Vector2 _gridOrigin)
    {
        Vector3 localPos = worldPosition - (Vector3)_gridOrigin;
        int x = Mathf.FloorToInt(localPos.x / _cellSize.x);
        int y = Mathf.FloorToInt(localPos.y / _cellSize.y);

        // Ensure cell coordinates are within grid limits
        x = Mathf.Clamp(x, 0, _gridSize.x - 1);
        y = Mathf.Clamp(y, 0, _gridSize.y - 1);
        return new Vector2Int(x, y);
    }

    // Function to convert Grid Cell to World Space Cell Center (static so it can be called from other scripts)
    public static Vector3 GridCellToWorldCenter(Vector2Int gridCell, Vector2 _cellSize, Vector2 _gridOrigin)
    {
        // Calculate the bottom-left corner position of the cell, then add half the cell size to get the center
        float worldX = _gridOrigin.x + gridCell.x * _cellSize.x;
        float worldY = _gridOrigin.y + gridCell.y * _cellSize.y;
        return new Vector3(worldX + _cellSize.x / 2, worldY + _cellSize.y / 2, 0);
    }

    // Non-static version for internal use within this class (easier to call)
    private Vector2 GridCellToWorldCenter(Vector2Int gridCell)
    {
        return GridCellToWorldCenter(gridCell, managerCellSize, gridOrigin);
    }

    // For Debug Visualization (draw grid in Scene View)
    void OnDrawGizmos()
    {
        // Draw the entire grid frame (grey color)
        Gizmos.color = Color.grey;
        Vector3 topLeft = gridOrigin + new Vector2(0, gridSize.y * managerCellSize.y);
        Vector3 topRight = gridOrigin + new Vector2(gridSize.x * managerCellSize.x, gridSize.y * managerCellSize.y);
        Vector3 bottomRight = gridOrigin + new Vector2(gridSize.x * managerCellSize.x, 0);
        Gizmos.DrawLine(gridOrigin, topLeft);
        Gizmos.DrawLine(topLeft, topRight);
        Gizmos.DrawLine(topRight, bottomRight);
        Gizmos.DrawLine(bottomRight, gridOrigin);

        // Draw walkable cells (green wireframes)
        if (_currentWeightedMap != null)
        {
            Gizmos.color = Color.green;
            foreach (var entry in _currentWeightedMap)
            {
                Gizmos.DrawWireCube(GridCellToWorldCenter(entry.Key), managerCellSize * 0.9f);
            }
        }

        // Draw blocked cells (red wireframes)
        if (_currentMovementBlockers != null)
        {
            Gizmos.color = Color.red;
            foreach (var blocker in _currentMovementBlockers)
            {
                Gizmos.DrawWireCube(GridCellToWorldCenter(blocker), managerCellSize * 0.9f);
            }
        }
    }
}
