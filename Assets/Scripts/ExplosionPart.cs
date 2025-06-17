//using UnityEngine;

//public class ExplosionPart : MonoBehaviour
//{
//    private SpriteRenderer spriteRenderer;
//    private Animator animator;

//    [Header("Items")]
//    public GameObject ItemExtraBombPrefap;
//    public GameObject ItemSpiritPrefab;
//    public GameObject ItemExtraRangePrefap;
//    private string[] items = new string[] { "ItemExtraBomb", "ItemSpirit" , "ItemExtraRange" };

//    void Awake()
//    {
//        spriteRenderer = GetComponent<SpriteRenderer>();
//        animator = GetComponent<Animator>();
//    }

//    private bool hasSpawnedItem = false; // Cờ để đảm bảo chỉ spawn item một lần

//    private void OnTriggerEnter2D(Collider2D other)
//    {
//        if (hasSpawnedItem) return; // Ngăn việc gọi lại logic nếu đã spawn item

//        if (other.CompareTag("Indestructible"))
//        {
//            // Phá hủy phần vụ nổ này ngay lập tức (tắt animation tự nhiên)
//            Destroy(gameObject);
//        }
//        else if (other.CompareTag("Destructible"))
//        {
//            if (other.gameObject != null) // Kiểm tra null trước khi phá hủy
//            {
//                // Phá hủy object destructible
//                Destroy(other.gameObject);

//                // Spawn item tại vị trí của object bị phá hủy
//                SpawnItemsRandom(other.gameObject.transform.position);

//                // Đánh dấu đã spawn item
//                hasSpawnedItem = true;
//            }
//        }
//    }


//    // function to spawn item 
//    private void SpawnItemsRandom(Vector2 spawnPosition)
//    {
//        // Tỷ lệ spawn item (ví dụ: 30% cơ hội spawn item)
//        float spawnChance = 0.5f;

//        // Kiểm tra xác suất
//        if (Random.value > spawnChance)
//        {
//            Debug.Log("No item spawned this time.");
//            return; // Không spawn item nếu không đạt xác suất
//        }

//        // Lấy offset để điều chỉnh vị trí spawn
//        float offsetY = 0.75f; // Điều chỉnh giá trị này dựa trên chiều cao của object bị phá hủy
//        float offsetX = -0.5f;

//        // Điều chỉnh vị trí spawn để khớp với Pivot của item
//        spawnPosition = new Vector2(Mathf.Round(spawnPosition.x) + offsetX, Mathf.Round(spawnPosition.y) + offsetY);

//        // Chọn ngẫu nhiên một item để spawn
//        string itemsToSpawnStr = items[Random.Range(0, items.Length)];
//        Debug.Log("Spawning item: " + itemsToSpawnStr);
//        GameObject itemsToSpawn = GetItems(itemsToSpawnStr);

//        if (itemsToSpawn != null)
//        {
//            Instantiate(itemsToSpawn, spawnPosition, Quaternion.identity);
//        }
//        else
//        {
//            Debug.LogError("Item to spawn is null. Check your Prefab references.");
//        }
//    }




//    private GameObject GetItems(string type)
//    {
//        switch (type)
//        {
//            case "ItemExtraBomb": return ItemExtraBombPrefap;
//            case "ItemSpirit": return ItemSpiritPrefab;
//            case "ItemExtraRange": return ItemExtraRangePrefap;
//            default: return null;
//        }
//    }
//}
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
    private static bool playerDead = false; // Biến static để kiểm soát log chết của người chơi

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

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Kiểm tra va chạm với vật thể không phá hủy được
        if (other.CompareTag("Indestructible"))
        {
            // Vụ nổ bị chặn lại bởi vật thể không phá hủy, hủy phần nổ này.
            Destroy(gameObject);
        }
        // Kiểm tra va chạm với vật thể có thể phá hủy
        else if (other.CompareTag("Destructible"))
        {
            // --- SỬA ĐỔI CHÍNH Ở ĐÂY ---
            // Thay vì tự hủy vật thể, tìm component DestructibleBlock và gọi hàm DestroyBlock() của nó.
            DestructibleBlock destructibleBlock = other.GetComponent<DestructibleBlock>();
            if (destructibleBlock != null)
            {
                // Vị trí của block bị phá hủy để spawn item
                Vector2 destroyPosition = other.transform.position;
                destructibleBlock.DestroyBlock(); // Gọi hàm DestroyBlock của DestructibleBlock
                //SpawnItemsRandom(destroyPosition); // Spawn item tại vị trí block bị phá hủy
            }
            // --- KẾT THÚC SỬA ĐỔI ---
        }
        // Kiểm tra va chạm với Player
        else if (other.CompareTag("Player"))
        {
            // Chỉ log "da chet" một lần duy nhất
            if (!playerDead)
            {
                Debug.Log("da chet");
                playerDead = true; // Đặt cờ playerDead thành true
                // TODO: Xử lý logic Player chết ở đây (ví dụ: gọi hàm Die() của Player, tải lại scene, v.v.)
            }
        }
        // Có thể thêm các tag khác nếu cần (ví dụ: "Enemy", "Bomb", v.v.)
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
