using Assets.Scripts;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    public GameState CurrentGameState { get; private set; } = GameState.MainMenu;

    public static event Action<GameState> OnGameStateChanged;

    public int CurrentLevel
    {
        get
        {
            return SceneManager.GetActiveScene().buildIndex;
        }
    }

    [SerializeField]
    private GameObject PauseMenu;
    [SerializeField]
    private GameObject GameOverMenu;
    [SerializeField]
    private GameObject WinMenu;
    [SerializeField]
    private GameObject MissionBriefMenu;
    [SerializeField]
    private Animator transitionAnim;

    private void Start()
    {
        Debug.Log("GameManager Start called");
        if (Instance == null)
        {
            Instance = this;
        }
        if (SceneManager.GetActiveScene().buildIndex == 0) // hoặc tên: .name == "MainMenu"
        {
            AudioManager.Instance.PlayWelcomeSound();
        }
        else
        {
            AudioManager.Instance.PlayBackgroundMusic();
        }
    
}

    public void UpdateGameState(GameState newState)
    {
        CurrentGameState = newState;

        switch (newState)
        {
            case GameState.MainMenu:
                Debug.Log("Switched to Main Menu");
                break;
            case GameState.Playing:
                Debug.Log("Game is now Playing");
                break;
            case GameState.Paused:
                Debug.Log("Game is Paused");
                break;
            case GameState.Win:
                ShowWinScore();
                break;
            case GameState.GameOver:
                GameOver();
                Debug.Log("Game Over");
                break;
            default:
                Debug.LogWarning("Unknown game state: " + newState);
                break;
        }

        Debug.Log("State change to: " + CurrentGameState);

        OnGameStateChanged?.Invoke(newState);
    }

    public void ShowWinScore()
    {
        //int score = ScoreManager.Instance.CalculateTotalScore();
        //Debug.Log("Total Score: " + score);
        WinMenu.SetActive(true);
        Time.timeScale = 0f;
        ScoreManager.Instance.ShowTotalScore();
        AudioManager.Instance.PlayWinSound();
    }

    public void QuitGame()
    {
        Application.Quit();
    }

    public void PauseGame()
    {
        PauseMenu.SetActive(true);
        UpdateGameState(GameState.Paused);
        Time.timeScale = 0f;
    }

    public void ResumeGame()
    {
        PauseMenu.SetActive(false);
        UpdateGameState(GameState.Playing);
        Time.timeScale = 1f;
    }

    public void LoadScene(int level)
    {
        if (level < 0 || level > 3)
        {
            Debug.LogError("Invalid level number: " + level);
            return;
        }

        if (GameOverMenu != null && GameOverMenu.activeSelf)
        {
            GameOverMenu.SetActive(false);
        }

        if (PauseMenu != null && PauseMenu.activeSelf)
        {
            PauseMenu.SetActive(false);
        }

        if (WinMenu != null && WinMenu.activeSelf)
        {
            WinMenu.SetActive(false);
            ScoreManager.Instance.ResetScore();
        }

        if (Time.timeScale == 0f)
        {
            Time.timeScale = 1f;
        }

        if (CurrentGameState == GameState.Playing)
        {
            PauseGame();
        }

        // ✅ CHỈ gọi coroutine này thôi
        StartCoroutine(LoadSceneWithTransition(level));
    }



    public void GameOver()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayLoseSound();

        Time.timeScale = 0f;
        if (GameOverMenu == null)
        {
            GameOverMenu = GameObject.Find("MissionFailPanel");
        }
        GameOverMenu.SetActive(true);
    }


    public void HideMissionBriefMenu()
    {
        if (MissionBriefMenu.activeSelf)
        {
            MissionBriefMenu.SetActive(false);
            UpdateGameState(GameState.Playing);
            ScoreManager.Instance.StartCountDown();
            ScoreManager.Instance.ShowHighScore(CurrentLevel);
        }
    }
    private IEnumerator LoadSceneWithTransition(int level)
    {
        if (transitionAnim != null)
        {
            transitionAnim.SetTrigger("End");
            yield return new WaitForSeconds(1f);  // ⏳ Đợi animation chạy xong
        }

        // ✅ Load scene sau khi đợi
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(level);
        yield return asyncLoad;  // 🔄 Đợi scene load xong

        // ✅ Sau khi scene load xong → chạy hiệu ứng mở sáng (fade in)
        if (transitionAnim != null)
        {
            transitionAnim.SetTrigger("Start");
        }
    }


}

public enum GameState
{
    MainMenu,
    Playing,
    Paused,
    Win,
    //WinningLevel1,
    //WinningLevel2,
    //WinningLevel3,
    GameOver
}
