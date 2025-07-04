using UnityEngine;

public class DestructibleBlock : MonoBehaviour
{
    public void DestroyBlock()
    {
        Debug.Log(gameObject.name + " is destroyed!");

        if (Application.isPlaying && PathfindingGridManager.Instance != null)
        {
            PathfindingGridManager.Instance.RefreshGridData();
        }
        else if (Application.isPlaying)
        {
            Debug.LogWarning("PathfindingGridManager.Instance is null. Cannot refresh grid on DestroyBlock call.");
        }

        Destroy(gameObject);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("BombExplosion"))
        {
            DestroyBlock();
        }
    }
}
