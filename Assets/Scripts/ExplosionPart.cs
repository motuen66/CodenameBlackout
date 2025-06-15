using UnityEngine;

public class ExplosionPart : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;
    private Animator animator;
    private static bool playerDead = false; // Biến static để kiểm soát log

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Indestructible"))
        {
            // Phá hủy phần vụ nổ này ngay lập tức (tắt animation tự nhiên)
            Destroy(gameObject);
        }
        else if (other.CompareTag("Destructible"))
        {
            // Phá hủy object destructible
            Destroy(other.gameObject);
        }
        else if (other.CompareTag("Player"))
        {
            if (!playerDead)
            {
                Debug.Log("da chet");
                playerDead = true;
            }
        }
    }
}
