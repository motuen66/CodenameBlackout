using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem; // Required for the new Input System

public class BombController : MonoBehaviour
{
    [Header("Input System")]
    // Reference to your Input Actions Asset.
    // Drag and drop the PlayerInputActions asset (created in previous steps) here in the Inspector.
    public PlayerInputActions inputActions;

    [Header("Bomb Settings")]
    public GameObject bombPrefab; // Prefab of the bomb object
    public float bombFuseTime = 3f; // Time until the bomb "explodes" (used for despawning for now)
    public int bombAmount = 1; // Maximum number of bombs the player can place simultaneously
    private int bombsRemaining; // Current count of bombs the player can still place

    private void Awake()
    {
        // Initialize Input Actions if not already assigned via the Inspector.
        // This provides a fallback if the asset isn't dragged in.
        if (inputActions == null)
        {
            inputActions = new PlayerInputActions(); 
        }

        // Subscribe to the "PlaceBomb" action.
        // When the "PlaceBomb" action is performed (e.g., button pressed and released),
        // the OnPlaceBomb method will be called.
        inputActions.Player.PlaceBomb.performed += OnPlaceBomb;
    }

    private void OnEnable()
    {
        // Reset bombs remaining when the script/GameObject is enabled.
        bombsRemaining = bombAmount;
        // Enable the 'Player' Action Map when the script is enabled.
        // This allows the input actions to be processed.
        if (inputActions != null)
        {
            inputActions.Player.Enable();
        }
    }

    private void OnDisable()
    {
        // Unsubscribe from the action to prevent memory leaks when the script is disabled.
        inputActions.Player.PlaceBomb.performed -= OnPlaceBomb;
        // Disable the 'Player' Action Map when the script is disabled.
        // This stops processing input for this map.
        if (inputActions != null)
        {
            inputActions.Player.Disable();
        }
    }

    // This method is called when the "PlaceBomb" action is triggered.
    private void OnPlaceBomb(InputAction.CallbackContext context)
    {
        // Check if the player has any bombs left to place.
        if (bombsRemaining > 0)
        {
            // Start the coroutine to place the bomb and handle its "fuse".
            StartCoroutine(PlaceBombRoutine());
        } 
    }

    private IEnumerator PlaceBombRoutine()
    {
        // Get the player's current position.
        Vector2 currentPlayerPosition = transform.position;

        // Calculate the center position of the grid cell the player is currently on.
        // Mathf.Floor rounds down to the nearest integer, giving the bottom-left corner of the grid cell.
        // Adding 0.5f then shifts the position to the center of that grid cell (assuming 1x1 unit grid cells).
        Vector2 bombPlacementPosition = new Vector2(
            Mathf.Floor(currentPlayerPosition.x) + 0.5f,
            Mathf.Floor(currentPlayerPosition.y) + 0.5f
        );

        // Instantiate a bomb prefab at the calculated center of the grid cell.   
        GameObject bomb = Instantiate(bombPrefab, bombPlacementPosition, Quaternion.identity);
        bombsRemaining--; // Decrease the count of bombs available to place.

        // Wait for the bomb's fuse time.
        yield return new WaitForSeconds(bombFuseTime);

        // Destroy the bomb GameObject after the fuse time expires.
        // (In a full game, explosion logic would be triggered here).
        Destroy(bomb);
        bombsRemaining++; // Increase the bomb count, allowing the player to place another bomb.
    }
}