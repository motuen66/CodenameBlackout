using UnityEngine;

public class Explosion : MonoBehaviour
{
    public float duration = 1f; // Thời gian tồn tại của vụ nổ

    void Start()
    {
        Animator animator = GetComponent<Animator>();

        Destroy(gameObject, duration);
    }

   
}