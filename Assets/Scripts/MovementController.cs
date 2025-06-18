using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(PlayerInput))]
public class MovementController : MonoBehaviour
{
    public static MovementController Instance { get; private set; }
    private Rigidbody2D rb;
    private Vector2 direction = Vector2.down;
    public float speed = 5f;
    public float maxSpeed = 8f;
    public float minSpeed = 5f;

    [Header("Sprites")]
    public AnimatedSpriteRenderer spriteRendererUp;
    public AnimatedSpriteRenderer spriteRendererDown;
    public AnimatedSpriteRenderer spriteRendererLeft;
    public AnimatedSpriteRenderer spriteRendererRight;
    private AnimatedSpriteRenderer activeSpriteRenderer;

    // Input System variables
    private PlayerInput playerInput;
    private InputAction moveAction;

    private void Start()
    {
        if (Instance == null)
        {
            Instance = this;
        }
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            Debug.LogError("Rigidbody2D component missing!", this);
            return;
        }

        playerInput = GetComponent<PlayerInput>();
        if (playerInput == null)
        {
            Debug.LogError("PlayerInput component missing!", this);
            return;
        }

        moveAction = playerInput.actions.FindAction("Move");
        if (moveAction == null)
        {
            Debug.LogError("Move action not found in Input Actions!", this);
            return;
        }

        activeSpriteRenderer = spriteRendererDown ?? GetComponentInChildren<AnimatedSpriteRenderer>();
    }

    private void Update()
    {
        // Read input from Input System
        Vector2 input = moveAction.ReadValue<Vector2>();

        // Determine direction and animation
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

        // Chuyển hướng hiển thị sprite
        spriteRendererUp.enabled = spriteRenderer == spriteRendererUp;
        spriteRendererDown.enabled = spriteRenderer == spriteRendererDown;
        spriteRendererLeft.enabled = spriteRenderer == spriteRendererLeft;
        spriteRendererRight.enabled = spriteRenderer == spriteRendererRight;

        activeSpriteRenderer = spriteRenderer;
        activeSpriteRenderer.idle = direction == Vector2.zero;

        // Thêm xử lý âm thanh bước chân
        if (direction == Vector2.zero)
        {
            AudioManager.Instance.StopFootstep();
        }
        else
        {
            AudioManager.Instance.PlayFootstep();
        }
    }

}