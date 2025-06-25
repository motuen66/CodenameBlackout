using UnityEngine;
using System.Collections.Generic;
using KwaaktjePathfinder2D;
using System;

public class PathfindingGridManager : MonoBehaviour
{
    // Configure your virtual grid in the Inspector
    public Vector2Int gridSize = new Vector2Int(20, 20); // Grid size (number of cells)
    public Vector2 managerCellSize = Vector2.one; // Size of each cell (e.g., 1x1 Unity units)
    public Vector2 gridOrigin = Vector2.zero; // Origin point (bottom-left corner) of the grid in World Space

    // Layer of obstacles
    public LayerMask obstacleLayer;

    public float defaultWalkCost = 1.0f; // Default movement cost for empty cells
    public float difficultTerrainWeight = 100.0f; // Weight for difficult terrain

    // Variable for other scripts to access Pathfinder2D
    public static Pathfinder2D InstancePathfinder;

    // Event fired when the grid is refreshed
    public static event Action OnGridRefreshed;

    // --- SINGLETON IMPLEMENTATION ---
    private static PathfindingGridManager _instance;
    public static PathfindingGridManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<PathfindingGridManager>();
                if (_instance == null)
                {
                    GameObject singletonObject = new GameObject(typeof(PathfindingGridManager).Name);
                    _instance = singletonObject.AddComponent<PathfindingGridManager>();
                    Debug.LogWarning($"No PathfindingGridManager found in scene. Creating a new one named {singletonObject.name}.", _instance);
                }
            }
            return _instance;
        }
    }
    // --- END SINGLETON IMPLEMENTATION ---

    // Stores the map and blockers for Gizmo visualization
    private Dictionary<Vector2Int, float> _currentWeightedMap;
    private List<Vector2Int> _currentMovementBlockers;

    // Ensures only one instance exists and initializes the grid.
    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            Debug.LogWarning($"Duplicate PathfindingGridManager found. Destroying {gameObject.name}.");
            return;
        }
        _instance = this;

        InitializeGrid();
        Debug.Log("Pathfinder2D initialized by PathfindingGridManager in Awake.");
    }

    // Initializes the grid data and Pathfinder2D instance.
    private void InitializeGrid()
    {
        GetWeightedAndBlockedMap(out _currentWeightedMap, out _currentMovementBlockers);
        NodeConnectionType connectionType = NodeConnectionType.RectangleNoDiagonals;
        InstancePathfinder = new Pathfinder2D(_currentWeightedMap, _currentMovementBlockers, connectionType);
    }

    // Refreshes the grid data and notifies subscribers.
    public void RefreshGridData()
    {
        Debug.Log("PathfindingGridManager: Refreshing grid data and pathfinder...");
        InitializeGrid();
        OnGridRefreshed?.Invoke();
        Debug.Log("PathfindingGridManager: Grid data refreshed and event invoked.");
    }

    // Scans the game space to find obstacles and creates the weighted map.
    private void GetWeightedAndBlockedMap(out Dictionary<Vector2Int, float> weightedMap, out List<Vector2Int> movementBlockers)
    {
        weightedMap = new Dictionary<Vector2Int, float>();
        movementBlockers = new List<Vector2Int>();

        for (int x = 0; x < gridSize.x; x++)
        {
            for (int y = 0; y < gridSize.y; y++)
            {
                Vector2Int gridCell = new Vector2Int(x, y);
                Vector2 worldCellCenter = GridCellToWorldCenter(gridCell, managerCellSize, gridOrigin);

                // Check for obstacles in this cell
                Collider2D hitCollider = Physics2D.OverlapBox(worldCellCenter, managerCellSize * 0.9f, 0f, obstacleLayer);

                if (hitCollider != null)
                {
                    movementBlockers.Add(gridCell); // Mark cell as blocked
                }
                else
                {
                    weightedMap.Add(gridCell, defaultWalkCost); // Mark cell as walkable
                }
            }
        }
        Debug.Log($"PathfindingGridManager: Weighted Map Size: {weightedMap.Count}, Movement Blockers Size: {movementBlockers.Count} after scan.");
    }

    // Converts a World Position to a Grid Cell coordinate.
    public static Vector2Int WorldToGridCell(Vector3 worldPosition, Vector2Int _gridSize, Vector2 _cellSize, Vector2 _gridOrigin)
    {
        Vector3 localPos = worldPosition - (Vector3)_gridOrigin;
        int x = Mathf.FloorToInt(localPos.x / _cellSize.x);
        int y = Mathf.FloorToInt(localPos.y / _cellSize.y);

        x = Mathf.Clamp(x, 0, _gridSize.x - 1);
        y = Mathf.Clamp(y, 0, _gridSize.y - 1);
        return new Vector2Int(x, y);
    }

    // Converts a Grid Cell coordinate to its World Space center position.
    public static Vector3 GridCellToWorldCenter(Vector2Int gridCell, Vector2 _cellSize, Vector2 _gridOrigin)
    {
        float worldX = _gridOrigin.x + gridCell.x * _cellSize.x;
        float worldY = _gridOrigin.y + gridCell.y * _cellSize.y;
        return new Vector3(worldX + _cellSize.x / 2, worldY + _cellSize.y / 2, 0);
    }

    // Non-static version of GridCellToWorldCenter for internal use.
    private Vector2 GridCellToWorldCenter(Vector2Int gridCell)
    {
        return GridCellToWorldCenter(gridCell, managerCellSize, gridOrigin);
    }

    // Draws the grid and obstacle visualization in the Scene View for debugging.
    void OnDrawGizmos()
    {
        Gizmos.color = Color.grey;
        Vector3 topLeft = gridOrigin + new Vector2(0, gridSize.y * managerCellSize.y);
        Vector3 topRight = gridOrigin + new Vector2(gridSize.x * managerCellSize.x, gridSize.y * managerCellSize.y);
        Vector3 bottomRight = gridOrigin + new Vector2(gridSize.x * managerCellSize.x, 0);
        Gizmos.DrawLine(gridOrigin, topLeft);
        Gizmos.DrawLine(topLeft, topRight);
        Gizmos.DrawLine(topRight, bottomRight);
        Gizmos.DrawLine(bottomRight, gridOrigin);

        if (_currentWeightedMap != null)
        {
            Gizmos.color = Color.green;
            foreach (var entry in _currentWeightedMap)
            {
                Gizmos.DrawWireCube(GridCellToWorldCenter(entry.Key), managerCellSize * 0.9f);
            }
        }

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
