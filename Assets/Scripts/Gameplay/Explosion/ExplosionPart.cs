using System.Collections.Generic;
using UnityEngine;

public class ExplosionPart : MonoBehaviour
{
    // Singleton pattern cho ExplosionPart - Cần xem xét kỹ nếu có nhiều đối tượng ExplosionPart tồn tại cùng lúc.
    // Nếu mỗi vụ nổ tạo ra một ExplosionPart riêng, thì không nên dùng Singleton ở đây.
    // Nếu bạn chỉ muốn có một ExplosionPart duy nhất quản lý mọi thứ, thì nó đúng.
    // Tuy nhiên, đối với một "phần của vụ nổ", nó thường là transient (tồn tại tạm thời) và không phải Singleton.
    // Tạm thời giữ lại theo code của bạn nhưng cần lưu ý.
    public static ExplosionPart Instance { get; private set; }
    private SpriteRenderer spriteRenderer;
    private Animator animator;
    //private static bool playerDead = false; // Biến static để kiểm soát log chết của người chơi

    [Header("Items")]
    public GameObject ItemExtraBombPrefap;
    public GameObject ItemSpiritPrefab;
    public GameObject ItemExtraRangePrefap;
    private string[] items = new string[] { "ItemExtraBomb", "ItemSpirit", "ItemExtraRange" };

    // Dùng để lưu các vị trí đã spawn item (static để không spawn nhiều item ở cùng 1 ô trong suốt game)
    private static HashSet<Vector2> spawnedPositions = new HashSet<Vector2>();

    private void Start()
    {
        // Kiểm tra Singleton (nếu có nhiều ExplosionPart, Instance sẽ bị ghi đè)
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            // Nếu đã có Instance khác, hủy đối tượng này.
            // Điều này chỉ đúng nếu bạn muốn ExplosionPart là một Singleton thực sự.
            // Nếu không, bạn có thể loại bỏ khối if/else này.
            // Destroy(gameObject); 
        }
    }

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
    }

    void OnEnable()
    {
        CheckOverlapAndHandle();
    }

    private void CheckOverlapAndHandle()
    {
        BoxCollider2D box = GetComponent<BoxCollider2D>();
        if (box == null)
        {
            Debug.LogError("Không tìm thấy BoxCollider2D trên " + gameObject.name);
            return;
        }

        Vector2 boxSize = box.size;
        Vector2 boxOffset = box.offset;
        Vector2 boxCenter = (Vector2)transform.position + boxOffset;

        // Lấy tất cả các Collider trong vùng overlap
        Collider2D[] hits = Physics2D.OverlapBoxAll(boxCenter, boxSize, 0f);

        foreach (Collider2D other in hits)
        {
            if (other.CompareTag("Indestructible"))
            {
                Debug.Log(this.name + " bị ngăn bởi " + other.name);
                Destroy(gameObject);
                return;
            }
        }

        foreach (Collider2D other in hits)
        {
            if (other.CompareTag("Destructible"))
            {
                DestructibleBlock destructibleBlock = other.GetComponent<DestructibleBlock>();
                if (destructibleBlock != null)
                {
                    Vector2 destroyPosition = other.transform.position;
                    destructibleBlock.DestroyBlock();
                    Debug.Log(this.name + " phá hủy  " + other.name);
                    SpawnItemsRandom(destroyPosition);
                }
            }
            else if (other.CompareTag("Player"))
            {
                Debug.Log("da chet");
                GameManager.Instance.UpdateGameState(GameState.GameOver);
            }
            else if (other.CompareTag("Enemy"))
            {
                Debug.Log("Enemy destroyed by explosion!");
                Destroy(other.gameObject);
            }
            else if (other.CompareTag("Target"))
            {
                Destroy(other.gameObject);
                GameManager.Instance.UpdateGameState(GameState.Win);
            }
        }
    }

    // Hàm spawn item ngẫu nhiên tại vị trí block bị phá hủy
    private void SpawnItemsRandom(Vector2 spawnPosition)
    {
        // Làm tròn vị trí để đảm bảo chúng khớp với lưới hoặc vị trí mong muốn
        spawnPosition = new Vector2(Mathf.Round(spawnPosition.x), Mathf.Round(spawnPosition.y));

        // Nếu vị trí này đã spawn item (do `spawnedPositions` là static) thì bỏ qua
        if (spawnedPositions.Contains(spawnPosition)) return;

        // Tỷ lệ spawn item (ví dụ: 50% cơ hội)
        float spawnChance = 0.5f;
        if (Random.value > spawnChance) return; // Không spawn nếu không đạt tỷ lệ

        // Đánh dấu đã spawn ở vị trí này để tránh spawn lại
        spawnedPositions.Add(spawnPosition);

        // Offset để điều chỉnh vị trí hiển thị item (tùy thuộc vào pivot của prefab item)
        float offsetY = 0.75f;
        float offsetX = -0.5f;
        Vector2 adjustedPos = new Vector2(spawnPosition.x + offsetX, spawnPosition.y + offsetY);

        // Chọn một loại item ngẫu nhiên từ danh sách
        string itemStr = items[Random.Range(0, items.Length)];
        GameObject itemPrefab = GetItems(itemStr); // Lấy prefab tương ứng

        if (itemPrefab != null)
        {
            Instantiate(itemPrefab, adjustedPos, Quaternion.identity); // Tạo item trong thế giới
        }
    }

    // Hàm trả về prefab item dựa trên tên loại
    private GameObject GetItems(string type)
    {
        switch (type)
        {
            case "ItemExtraBomb": return ItemExtraBombPrefap;
            case "ItemSpirit": return ItemSpiritPrefab;
            case "ItemExtraRange": return ItemExtraRangePrefap;
            default: return null; // Trả về null nếu không tìm thấy loại item
        }
    }
}
