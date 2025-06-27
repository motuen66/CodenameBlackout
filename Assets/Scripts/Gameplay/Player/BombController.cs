using Assets.Scripts;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class BombController : MonoBehaviour
{
    public static BombController Instance { get; private set; }

    // Reference to your Input Actions Asset.
    public PlayerInputActions inputActions;

    // Prefab for default explosion.
    public GameObject explosionDefaultPrefab;
    // Prefab for explosion with extra range.
    public GameObject explosionExtraRangePrefab;
    // Currently active explosion prefab.
    public GameObject currentExplosionPrefab;
    // Prefab of the bomb object to place.
    public GameObject bombPrefab;
    // Time until the bomb explodes.
    public float bombFuseTime = 3f;
    // Maximum number of bombs the player can place simultaneously.
    public int bombAmount = 1;
    // Current count of bombs the player can still place.
    public int bombsRemaining;

    // Sets up the singleton instance.
    private void Start()
    {
        if (Instance == null)
        {
            Instance = this;
        }
    }

    // Initializes input actions and subscribes to the bomb placement event.
    private void Awake()
    {
        if (inputActions == null)
        {
            inputActions = new PlayerInputActions();
        }

        inputActions.Player.PlaceBomb.performed += OnPlaceBomb;
        if (explosionDefaultPrefab != null)
        {
            currentExplosionPrefab = explosionDefaultPrefab;
        }
        else
        {
            Debug.LogError("Explosion Default Prefab is not assigned!");
        }
    }

    // Resets bomb count and enables input actions when the script is enabled.
    private void OnEnable()
    {
        bombsRemaining = bombAmount;
        if (inputActions != null)
        {
            inputActions.Player.Enable();
        }
    }

    // Unsubscribes from input actions and disables them when the script is disabled.
    private void OnDisable()
    {
        inputActions.Player.PlaceBomb.performed -= OnPlaceBomb;
        if (inputActions != null)
        {
            inputActions.Player.Disable();
        }
    }

    // Called when the "PlaceBomb" action is triggered, places a bomb if available.
    private void OnPlaceBomb(InputAction.CallbackContext context)
    {
        if (bombsRemaining > 0)
        {
            StartCoroutine(PlaceBombRoutine());
            // Refresh the pathfinding grid after a bomb is placed (assuming bombs create obstacles).
            PathfindingGridManager.Instance.RefreshGridData();
        }
    }

    // Coroutine to handle bomb placement, fuse time, and explosion.
    private IEnumerator PlaceBombRoutine()
    {
        AudioManager.Instance.PlayFuseSound();
        // Get the player's current position.
        Vector2 currentPlayerPosition = transform.position;

        // Calculate the center position of the grid cell for bomb placement.
        Vector2 bombPlacementPosition = new Vector2(
            Mathf.Floor(currentPlayerPosition.x) + 0.5f,
            Mathf.Floor(currentPlayerPosition.y) + 0.5f
        );

        // Instantiate a bomb prefab at the calculated position.
        GameObject bomb = Instantiate(bombPrefab, bombPlacementPosition, Quaternion.identity);
        bombsRemaining--; // Decrease the count of bombs available to place.

        // Wait for the bomb's fuse time.
        yield return new WaitForSeconds(bombFuseTime);
        // Play the explosion sound effect.
        AudioManager.Instance.PlayExplosionSound();
        // Instantiate the explosion prefab at the bomb's position.
        if (currentExplosionPrefab != null)
        {
            Instantiate(currentExplosionPrefab, bomb.transform.position, Quaternion.identity);
        }

        // Destroy the bomb GameObject.
        Destroy(bomb);
        bombsRemaining++; // Increase the bomb count, allowing the player to place another bomb.
        PathfindingGridManager.Instance.RefreshGridData();
    }

    // Handles collisions with other 2D colliders (e.g., picking up items, hitting enemies).
    private void OnCollisionEnter2D(Collision2D collision)
    {
        ExplosionPart explosionPart = ExplosionPart.Instance;
        ItemController itemController = ItemController.Instance;

        // Get the name of the touched object, removing "(Clone)" suffix.
        string touchObjectName = collision.gameObject.name.Split("(Clone)")[0];

        // Check for specific item pickups and activate their effects.
        //if (touchObjectName == explosionPart.ItemExtraBombPrefap.name)
        //{
        //    itemController.StartBombPlusTemporary();
        //    Destroy(collision.gameObject);
        //    AudioManager.Instance.PlayPickItemSound();
        //}
        //else if (touchObjectName == explosionPart.ItemExtraRangePrefap.name)
        //{
        //    itemController.StartBombExtraRangeTemporary();
        //    Destroy(collision.gameObject);
        //    AudioManager.Instance.PlayPickItemSound();
        //}
        //else if (touchObjectName == explosionPart.ItemSpiritPrefab.name)
        //{
        //    itemController.StartSpeedUpTemporary();
        //    Destroy(collision.gameObject);
        //    AudioManager.Instance.PlayPickItemSound();
        //}
        // Check if the player collides with an enemy, triggering game over.
        if (collision.gameObject.CompareTag("Enemy"))
        {
            GameManager.Instance.UpdateGameState(GameState.GameOver);
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        ExplosionPart explosionPart = ExplosionPart.Instance;
        ItemController itemController = ItemController.Instance;
        string touchObjectName = collision.gameObject.name.Split("(Clone)")[0];
        if (touchObjectName == explosionPart.ItemExtraBombPrefap.name)
        {
            itemController.StartBombPlusTemporary();
            Destroy(collision.gameObject);
            AudioManager.Instance.PlayPickItemSound();
        }
        else if (touchObjectName == explosionPart.ItemExtraRangePrefap.name)
        {
            itemController.StartBombExtraRangeTemporary();
            Destroy(collision.gameObject);
            AudioManager.Instance.PlayPickItemSound();
        }
        else if (touchObjectName == explosionPart.ItemSpiritPrefab.name)
        {
            itemController.StartSpeedUpTemporary();
            Destroy(collision.gameObject);
            AudioManager.Instance.PlayPickItemSound();
        }
    }
}
