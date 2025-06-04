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

    //private IEnumerator PlaceBombRoutine()
    //{
    //    // Round the player's current position to ensure the bomb is placed precisely
    //    // in the center of the nearest grid cell.
    //    Vector2 position = transform.position;
    //    position.x = Mathf.Round(position.x);
    //    position.y = Mathf.Round(position.y);

    //    // Instantiate the bomb prefab at the calculated grid position.
    //    GameObject bomb = Instantiate(bombPrefab, position, Quaternion.identity);
    //    bombsRemaining--; // Decrease the count of bombs available to place.

    //    // Wait for the specified bomb fuse time.
    //    yield return new WaitForSeconds(bombFuseTime);

    //    // Debug.Log("Bomb at " + bomb.transform.position + " would explode now.");

    //    // Destroy the bomb after its fuse time.
    //    // (Currently, this is where the explosion logic would go in a full implementation).
    //    Destroy(bomb);
    //    bombsRemaining++; // Increase the bomb count, allowing the player to place another bomb.
    //}

    private IEnumerator PlaceBombRoutine()
    {
        // Lấy vị trí hiện tại của nhân vật.
        Vector2 currentPlayerPosition = transform.position;

        // Tính toán vị trí trung tâm của ô lưới mà nhân vật đang đứng.
        // Mathf.Floor làm tròn xuống số nguyên gần nhất.
        // Sau đó cộng 0.5f để dịch chuyển đến tâm của ô.
        Vector2 bombPlacementPosition = new Vector2(
            Mathf.Floor(currentPlayerPosition.x) + 0.5f,
            Mathf.Floor(currentPlayerPosition.y) + 0.5f
        );

        // Tạo một quả bom tại vị trí tâm của ô lưới
        GameObject bomb = Instantiate(bombPrefab, bombPlacementPosition, Quaternion.identity);
        bombsRemaining--; // Giảm số bom còn lại

        yield return new WaitForSeconds(bombFuseTime);

        Destroy(bomb);
        bombsRemaining++;
    }
}