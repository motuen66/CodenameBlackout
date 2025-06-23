using UnityEngine;
using System.Collections; // Kept in case you want to use Coroutines for other logic later.

public class DestructibleBlock : MonoBehaviour
{
    // This method will be called when the block is destroyed (e.g., by a bomb explosion).
    // You can call this method directly from the bomb script or its explosion area.
    public void DestroyBlock()
    {
        Debug.Log(gameObject.name + " is destroyed!");

        // Call RefreshGridData on the PathfindingGridManager to update the grid.
        // Always check if Instance != null to avoid errors if the GridManager was destroyed earlier.
        // Also check Application.isPlaying to prevent execution when exiting the editor.
        if (Application.isPlaying && PathfindingGridManager.Instance != null)
        {
            // --- FIX APPLIED HERE: Changed from RefreshGrid() to RefreshGridData() ---
            PathfindingGridManager.Instance.RefreshGridData();
            // --- END OF FIX ---
        }
        else if (Application.isPlaying) // Log a warning if the Instance is null during Play Mode
        {
            Debug.LogWarning("PathfindingGridManager.Instance is null. Cannot refresh grid on DestroyBlock call.");
        }

        // Destroy this GameObject immediately.
        // You can uncomment the line below to actually destroy the block.
        Destroy(gameObject); // Uncommented to ensure the block is actually destroyed
    }

    // Optional: Use OnTriggerEnter2D if the bomb explosion area uses a Trigger Collider
    // and is tagged with something like "BombExplosion".
    void OnTriggerEnter2D(Collider2D other)
    {
        // Make sure the Collider of the explosion area is tagged as "BombExplosion".
        //if (other.CompareTag("BombExplosion"))
        //{
        //    DestroyBlock(); // Call the method to destroy this block
        //}
    }

    // OnDestroy() can be a backup if DestroyBlock() is not guaranteed to be called.
    // However, if you always call DestroyBlock(), this might be unnecessary.
    // If you choose to keep it, ensure that it doesn’t result in RefreshGrid() being called twice.
    /*
    void OnDestroy()
    {
        // Always check Application.isPlaying to avoid issues when exiting the Editor
        if (Application.isPlaying)
        {
            if (PathfindingGridManager.Instance != null)
            {
                // Ensure to call RefreshGridData() here as well if you uncomment this
                // PathfindingGridManager.Instance.RefreshGridData();
            }
        }
    }
    */
}
