using UnityEngine;

public class Bomb : MonoBehaviour
{
    private bool playerInside = false;

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
        if (other.CompareTag("Player"))
        {
            playerInside = true;
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            playerInside = false;
            // Tắt trigger để bomb trở thành vật cản
            Collider2D col = GetComponent<Collider2D>();
            if (col != null)
                col.isTrigger = false;
        }
    }
}
