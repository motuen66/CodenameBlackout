using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(PlayerInput))]
public class MovementController : MonoBehaviour
{
    private Rigidbody2D rb;
    private Vector2 direction = Vector2.down;
    public float speed = 5f;

    [Header("Sprites")]
    public AnimatedSpriteRenderer spriteRendererUp;
    public AnimatedSpriteRenderer spriteRendererDown;
    public AnimatedSpriteRenderer spriteRendererLeft;
    public AnimatedSpriteRenderer spriteRendererRight;
    private AnimatedSpriteRenderer activeSpriteRenderer;

    // Input System
    private PlayerInput playerInput;
    private InputAction moveAction;
    private InputAction dropBombAction;

    // Tham chiếu tới BombController
    public BombController bombController;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        playerInput = GetComponent<PlayerInput>();

        // Lấy đúng action theo tên bạn đã đặt trong InputActions (putBomb)
        moveAction = playerInput.actions.FindAction("Move");
        dropBombAction = playerInput.actions.FindAction("putBomb");

        // Kiểm tra null
        if (dropBombAction == null)
        {
            Debug.LogError("Không tìm thấy InputAction tên 'putBomb'. Vui lòng kiểm tra lại InputActions.");
        }

        activeSpriteRenderer = spriteRendererDown ?? GetComponentInChildren<AnimatedSpriteRenderer>();
    }

    private void OnEnable()
    {
        moveAction?.Enable();
        dropBombAction?.Enable();
    }

    private void OnDisable()
    {
        moveAction?.Disable();
        dropBombAction?.Disable();
    }

    private void Update()
    {
        // Đặt bomb nếu nhấn space
        if (dropBombAction != null && dropBombAction.triggered && bombController != null)
        {
        
            Debug.Log("Đã nhấn Space - Đặt bomb");
        }

        // Di chuyển
        Vector2 input = moveAction.ReadValue<Vector2>();

        if (input.y > 0)
        {
            SetDirection(Vector2.up, spriteRendererUp);
        }
        else if (input.y < 0)
        {
            SetDirection(Vector2.down, spriteRendererDown);
        }
        else if (input.x < 0)
        {
            SetDirection(Vector2.left, spriteRendererLeft);
        }
        else if (input.x > 0)
        {
            SetDirection(Vector2.right, spriteRendererRight);
        }
        else
        {
            SetDirection(Vector2.zero, activeSpriteRenderer);
        }
    }

    private void FixedUpdate()
    {
        Vector2 position = rb.position;
        Vector2 translation = speed * Time.fixedDeltaTime * direction;
        rb.MovePosition(position + translation);
    }

    private void SetDirection(Vector2 newDirection, AnimatedSpriteRenderer spriteRenderer)
    {
        direction = newDirection;

        spriteRendererUp.enabled = spriteRenderer == spriteRendererUp;
        spriteRendererDown.enabled = spriteRenderer == spriteRendererDown;
        spriteRendererLeft.enabled = spriteRenderer == spriteRendererLeft;
        spriteRendererRight.enabled = spriteRenderer == spriteRendererRight;

        activeSpriteRenderer = spriteRenderer;
        activeSpriteRenderer.idle = direction == Vector2.zero;
    }
}
