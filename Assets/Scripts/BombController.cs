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
    public GameObject explosionPrefab; // Add this line under Bomb Settings
    public GameObject explosionExtraRangePrefab;
    public GameObject bombPrefab; // Prefab of the bomb object
    public float bombFuseTime = 3f; // Time until the bomb "explodes" (used for despawning for now)
    public int bombAmount = 2; // Maximum number of bombs the player can place simultaneously
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
        Debug.Log($"Placed bomb ");
        AudioManager.Instance.PlayFuseSound();
        Vector2 currentPlayerPosition = transform.position;
        Vector2 bombPlacementPosition = new Vector2(
            Mathf.Floor(currentPlayerPosition.x) + 0.5f,
            Mathf.Floor(currentPlayerPosition.y) + 0.5f
        );

        GameObject bomb = Instantiate(bombPrefab, bombPlacementPosition, Quaternion.identity);
        //bomb.tag = "Bomb";
        bombsRemaining--; // Decrease the count of bombs available to place.
        //Debug.Log($"Bom đã đặt. bombsRemaining: {bombsRemaining}");
        bombsRemaining--;
        bomb.tag = $"Bomb-{2 - bombsRemaining}";
        Debug.Log($"Placed bomb, Bombs remaining: {bombsRemaining}");

        yield return new WaitForSeconds(bombFuseTime);

        // Instantiate the explosion at the bomb's position (not the player's position)
        if (explosionPrefab != null)
        AudioManager.Instance.PlayExplosionSound();

        // Gọi explosion tại vị trí bomb
        if (currentExplosionPrefab != null)
        {
            Instantiate(explosionPrefab, bomb.transform.position, Quaternion.identity);
            // Tạo vụ nổ trung tâm
            Instantiate(currentExplosionPrefab, bomb.transform.position, Quaternion.identity);

            // Gọi các hướng nổ
            int explosionRange = 2; // hoặc lấy từ player/item
            ExplosionPart.Instance.SpawnExplosionLine((Vector2)bomb.transform.position, Vector2.up, explosionRange, currentExplosionPrefab);
            ExplosionPart.Instance.SpawnExplosionLine((Vector2)bomb.transform.position, Vector2.down, explosionRange, currentExplosionPrefab);
            ExplosionPart.Instance.SpawnExplosionLine((Vector2)bomb.transform.position, Vector2.left, explosionRange, currentExplosionPrefab);
            ExplosionPart.Instance.SpawnExplosionLine((Vector2)bomb.transform.position, Vector2.right, explosionRange, currentExplosionPrefab);
            Destroy(bomb);
        }

        Destroy(bomb);
        bombsRemaining++; // Increase the bomb count, allowing the player to place another bomb.
        bombsRemaining++;
        Debug.Log($"Bomb explosion, Bombs remaining: {bombsRemaining}");
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        ExplosionPart explosionPart = ExplosionPart.Instance;
        string touchObjectName = collision.gameObject.name.Split("(Clone)")[0];

        //Debug.Log(touchObjectName + "111");
        //Debug.Log(explosionPart.ItemExtraBombPrefap.name + "111");
        if (touchObjectName == explosionPart.ItemExtraBombPrefap.name)
        {
            bombAmount++;
            Destroy(collision.gameObject);
        }
        else if (touchObjectName == explosionPart.ItemExtraRangePrefap.name)
        {
            //explosionPrefab = explosionPart.explosionExtraRangePrefab1;
            Destroy(collision.gameObject);
        }
        else if (touchObjectName == explosionPart.ItemSpiritPrefab.name)
        {
            MovementController.Instance.speed += 3f;
            Destroy(collision.gameObject);
        }
    }
}