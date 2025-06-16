using UnityEngine;
using System.Collections.Generic;
using KwaaktjePathfinder2D; // Ensure the asset's namespace is included

public class PathfindingGridManager : MonoBehaviour
{
    // Configure your virtual grid in the Inspector
    public Vector2Int gridSize = new Vector2Int(20, 20); // Grid dimensions (number of cells)
    public Vector2 managerCellSize = Vector2.one; // Size of each cell (e.g., 1x1 Unity units)
    public Vector2 gridOrigin = Vector2.zero; // Origin point (bottom-left corner) of the grid in World Space

    // Layer for obstacles (your block prefab)
    public LayerMask obstacleLayer;

    public float defaultWalkCost = 1.0f; // Default walk cost for empty cells
    // public float difficultTerrainWeight = 100.0f; // Weight for difficult terrain (if used, define manually)

    // Static reference for other scripts to access Pathfinder2D
    public static Pathfinder2D InstancePathfinder;

    // Store map and blockers for Gizmo visualization
    private Dictionary<Vector2Int, float> _currentWeightedMap;
    private List<Vector2Int> _currentMovementBlockers;

    // Singleton pattern to ensure a single instance
    public static PathfindingGridManager Instance { get; private set; }

    // Optional event triggered after grid refresh
    public static event System.Action OnGridRefreshed;

    void Awake()
    {
        // Initialize Singleton instance
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            // Ensure only one GridManager exists in the scene
            Destroy(gameObject);
            return;
        }

        // Initialize map and blockers for the first time,
        // and create the Pathfinder instance
        InitializeGridAndPathfinder();
        Debug.Log("Pathfinder2D initialized by PathfindingGridManager.");
    }

    // Initializes or reinitializes the grid and Pathfinder
    private void InitializeGridAndPathfinder()
    {
        // Scan the space for obstacles and build the weighted map
        GetWeightedAndBlockedMap(out _currentWeightedMap, out _currentMovementBlockers);

        // Choose node connection type: RectangleNoDiagonals for orthogonal movement
        NodeConnectionType connectionType = NodeConnectionType.RectangleNoDiagonals;

        // Reinitialize Pathfinder2D with new data
        // This is necessary if KwaaktjePathfinder2D doesn’t support dynamic grid updates
        InstancePathfinder = new Pathfinder2D(_currentWeightedMap, _currentMovementBlockers, connectionType);
    }

    // Scans the grid and detects obstacles to build the weighted map
    private void GetWeightedAndBlockedMap(out Dictionary<Vector2Int, float> weightedMap, out List<Vector2Int> movementBlockers)
    {
        weightedMap = new Dictionary<Vector2Int, float>();
        movementBlockers = new List<Vector2Int>();

        // Scan each cell in the virtual grid
        for (int x = 0; x < gridSize.x; x++)
        {
            for (int y = 0; y < gridSize.y; y++)
            {
                Vector2Int gridCell = new Vector2Int(x, y);
                Vector2 worldCellCenter = GridCellToWorldCenter(gridCell, managerCellSize, gridOrigin);

                // Use OverlapBox to check for obstacles in the current cell
                // Adjust OverlapBox size to properly detect obstacles
                Collider2D hitCollider = Physics2D.OverlapBox(worldCellCenter, managerCellSize * 0.9f, 0f, obstacleLayer);

                if (hitCollider != null)
                {
                    movementBlockers.Add(gridCell);
                }
                else
                {
                    weightedMap.Add(gridCell, defaultWalkCost);
                }
            }
        }

        Debug.Log($"Weighted Map Size: {weightedMap.Count}, Movement Blockers Size: {movementBlockers.Count} after scan.");
    }

    // Public method to refresh the entire grid
    // Called by obstacles when they are destroyed
    public void RefreshGrid()
    {
        Debug.Log("[PathfindingGridManager] Refreshing grid...");
        InitializeGridAndPathfinder(); // Reinitialize grid and Pathfinder
        Debug.Log("[PathfindingGridManager] Grid refreshed successfully.");

        // Trigger event to notify listeners (e.g., guards)
        OnGridRefreshed?.Invoke();
    }

    // Converts a world position to a grid cell (static, accessible from other scripts)
    public static Vector2Int WorldToGridCell(Vector3 worldPosition, Vector2Int _gridSize, Vector2 _cellSize, Vector2 _gridOrigin)
    {
        Vector3 localPos = worldPosition - (Vector3)_gridOrigin;
        int x = Mathf.FloorToInt(localPos.x / _cellSize.x);
        int y = Mathf.FloorToInt(localPos.y / _cellSize.y);

        x = Mathf.Clamp(x, 0, _gridSize.x - 1);
        y = Mathf.Clamp(y, 0, _gridSize.y - 1);
        return new Vector2Int(x, y);
    }

    // Converts a grid cell to its center position in World Space (static for use in other scripts)
    public static Vector3 GridCellToWorldCenter(Vector2Int gridCell, Vector2 _cellSize, Vector2 _gridOrigin)
    {
        float worldX = _gridOrigin.x + gridCell.x * _cellSize.x;
        float worldY = _gridOrigin.y + gridCell.y * _cellSize.y;
        return new Vector3(worldX + _cellSize.x / 2, worldY + _cellSize.y / 2, 0);
    }

    // Non-static version used within this class
    private Vector2 GridCellToWorldCenter(Vector2Int gridCell)
    {
        return GridCellToWorldCenter(gridCell, managerCellSize, gridOrigin);
    }

    // Optional: Draw grid in editor for debugging
    void OnDrawGizmos()
    {
        // Draw the grid border
        Gizmos.color = Color.grey;
        Vector3 topLeft = gridOrigin + new Vector2(0, gridSize.y * managerCellSize.y);
        Vector3 topRight = gridOrigin + new Vector2(gridSize.x * managerCellSize.x, gridSize.y * managerCellSize.y);
        Vector3 bottomRight = gridOrigin + new Vector2(gridSize.x * managerCellSize.x, 0);
        Gizmos.DrawLine(gridOrigin, topLeft);
        Gizmos.DrawLine(topLeft, topRight);
        Gizmos.DrawLine(topRight, bottomRight);
        Gizmos.DrawLine(bottomRight, gridOrigin);

        // Draw walkable cells in green (as wireframes)
        if (_currentWeightedMap != null)
        {
            Gizmos.color = Color.green;
            foreach (var entry in _currentWeightedMap)
            {
                Gizmos.DrawWireCube(GridCellToWorldCenter(entry.Key), managerCellSize * 0.9f);
            }
        }

        // Draw blocked cells in red (as wireframes)
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
