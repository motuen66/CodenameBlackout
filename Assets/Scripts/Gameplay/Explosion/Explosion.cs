using UnityEngine;

public class Explosion : MonoBehaviour
{
    public float duration = 1f;

    void Start()
    {
        Animator animator = GetComponent<Animator>();

        Destroy(gameObject, duration);
    }
}