using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{

    void Start()
    {
        // Kiểm tra xem AudioManager có tồn tại không
        if (AudioManager.Instance != null)
        {
            // Phát nhạc nền
            AudioManager.Instance.PlayBackgroundMusic();

            // Phát âm thanh chào mừng (chỉ phát một lần)
            AudioManager.Instance.PlayWelcomeSound();
        }
        else
        {
            Debug.LogError("AudioManager Instance is not found! Make sure AudioManager GameObject is in the scene and the script is attached.");
        }
    }

    private void Awake()
    {
        GameManager.OnGameStateChanged += HandleGameStateChanged;
    }

    private void OnDestroy()
    {
        GameManager.OnGameStateChanged -= HandleGameStateChanged;
    }

    private void HandleGameStateChanged(GameState state)
    {
    }

}
