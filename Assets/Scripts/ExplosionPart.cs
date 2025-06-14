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
    private SpriteRenderer spriteRenderer;
    private Animator animator;

    [Header("Items")]
    public GameObject ItemExtraBombPrefap;
    public GameObject ItemSpiritPrefab;
    public GameObject ItemExtraRangePrefap;
    private string[] items = new string[] { "ItemExtraBomb", "ItemSpirit", "ItemExtraRange" };

    // Dùng để lưu các vị trí đã spawn item
    private static HashSet<Vector2> spawnedPositions = new HashSet<Vector2>();

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Indestructible"))
        {
            Destroy(gameObject);
        }
        else if (other.CompareTag("Destructible"))
        {
            if (other.gameObject != null)
            {
                Vector2 destroyPosition = other.transform.position;
                Destroy(other.gameObject);
                SpawnItemsRandom(destroyPosition);
            }
        }
    }

    private void SpawnItemsRandom(Vector2 spawnPosition)
    {
        // Làm tròn vị trí để tránh lệch
        spawnPosition = new Vector2(Mathf.Round(spawnPosition.x), Mathf.Round(spawnPosition.y));

        // Nếu vị trí này đã spawn item thì bỏ qua
        if (spawnedPositions.Contains(spawnPosition)) return;

        // Tỷ lệ spawn item
        float spawnChance = 0.5f;
        if (Random.value > spawnChance) return;

        // Đánh dấu đã spawn ở vị trí này
        spawnedPositions.Add(spawnPosition);

        // Offset để điều chỉnh vị trí hiển thị item
        float offsetY = 0.75f;
        float offsetX = -0.5f;
        Vector2 adjustedPos = new Vector2(spawnPosition.x + offsetX, spawnPosition.y + offsetY);

        // Chọn item
        string itemStr = items[Random.Range(0, items.Length)];
        GameObject itemPrefab = GetItems(itemStr);

        if (itemPrefab != null)
        {
            Instantiate(itemPrefab, adjustedPos, Quaternion.identity);
            Debug.Log("Spawned item: " + itemStr + " at " + adjustedPos);
        }
    }

    private GameObject GetItems(string type)
    {
        switch (type)
        {
            case "ItemExtraBomb": return ItemExtraBombPrefap;
            case "ItemSpirit": return ItemSpiritPrefab;
            case "ItemExtraRange": return ItemExtraRangePrefap;
            default: return null;
        }
    }
}
