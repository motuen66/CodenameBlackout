using UnityEngine;

public class DestructibleBlock : MonoBehaviour
{
    // This method is called when the block should be destroyed.
    public void DestroyBlock()
    {
        Debug.Log(gameObject.name + " is destroyed!");

        // Refreshes the pathfinding grid after the block is destroyed.
        if (Application.isPlaying && PathfindingGridManager.Instance != null)
        {
            PathfindingGridManager.Instance.RefreshGridData();
        }
        else if (Application.isPlaying)
        {
            Debug.LogWarning("PathfindingGridManager.Instance is null. Cannot refresh grid on DestroyBlock call.");
        }

        // Destroys this GameObject.
        Destroy(gameObject);
    }

    // Called when another collider enters this block's trigger collider.
    void OnTriggerEnter2D(Collider2D other)
    {
        // If the colliding object is tagged "BombExplosion", destroy this block.
        if (other.CompareTag("BombExplosion"))
        {
            DestroyBlock();
        }
    }
}
