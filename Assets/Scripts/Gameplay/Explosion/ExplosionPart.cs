using Assets.Scripts;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ExplosionPart : MonoBehaviour
{
    public static ExplosionPart Instance { get; private set; }

    [Header("Items")]
    public GameObject ItemExtraBombPrefap;
    public GameObject ItemSpiritPrefab;
    public GameObject ItemExtraRangePrefap;
    private string[] items = new string[] { "ItemExtraBomb", "ItemSpirit", "ItemExtraRange" };

    private static HashSet<Vector2> spawnedPositions = new HashSet<Vector2>();
    private Coroutine overlapCoroutine;

    private void Start()
    {
        if (Instance == null)
        {
            Instance = this;
        }
    }

    void OnEnable()
    {
        overlapCoroutine = StartCoroutine(RepeatCheckOverlap());
    }

    void OnDisable()
    {
        if (overlapCoroutine != null)
        {
            StopCoroutine(overlapCoroutine);
        }
    }

    private IEnumerator RepeatCheckOverlap()
    {
        while (true)
        {
            CheckOverlapAndHandle();
            yield return new WaitForSeconds(0.1f);
        }
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
                    SpawnItemsRandom(destroyPosition);
                }
            }
            else if (other.CompareTag("Player"))
            {
                GameManager.Instance.UpdateGameState(GameState.GameOver);
            }
            else if (other.CompareTag("Enemy"))
            {
                string enemyName = other.name.Split(" ")[0];
                if (enemyName == "Guard1")
                {
                    ScoreManager.Instance.KillYelloGuard();
                }
                else if (enemyName == "Guard2")
                {
                    ScoreManager.Instance.KillRedGuard();
                }
                Destroy(other.gameObject);
            }
            else if (other.CompareTag("Target"))
            {
                Destroy(other.gameObject);
                GameManager.Instance.UpdateGameState(GameState.Win);
            }
        }
    }

    private void SpawnItemsRandom(Vector2 spawnPosition)
    {
        spawnPosition = new Vector2(Mathf.Round(spawnPosition.x), Mathf.Round(spawnPosition.y));
        if (spawnedPositions.Contains(spawnPosition)) return;
        float spawnChance = 0.5f;
        if (Random.value > spawnChance) return;
        spawnedPositions.Add(spawnPosition);
        float offsetY = 0.75f;
        float offsetX = -0.5f;
        Vector2 adjustedPos = new Vector2(spawnPosition.x + offsetX, spawnPosition.y + offsetY);
        string itemStr = items[Random.Range(0, items.Length)];
        GameObject itemPrefab = GetItems(itemStr);
        if (itemPrefab != null)
        {
            Instantiate(itemPrefab, adjustedPos, Quaternion.identity);
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
