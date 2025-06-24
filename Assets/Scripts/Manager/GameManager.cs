using Assets.Scripts;
using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    public GameState CurrentGameState { get; private set; } = GameState.MainMenu;

    public static event Action<GameState> OnGameStateChanged;

    [SerializeField]
    private GameObject PauseMenu;
    [SerializeField]
    private GameObject GameOverMenu;
    [SerializeField]
    private GameObject WinMenu;
    [SerializeField]
    private GameObject MissionBriefMenu;

    private void Start()
    {
        Debug.Log("GameManager Start called");
        if (Instance == null)
        {
            Instance = this;
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

        Debug.Log("Current Game State: " + CurrentGameState);
        Debug.Log("Current Time Scale: " + Time.timeScale);

        OnGameStateChanged?.Invoke(newState);
    }

    public void ShowWinScore()
    {
        //int score = ScoreManager.Instance.CalculateTotalScore();
        //Debug.Log("Total Score: " + score);
        WinMenu.SetActive(true);
        Time.timeScale = 0f;
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

        SceneManager.LoadSceneAsync(level);
        if (Time.timeScale == 0f)
        {
            Time.timeScale = 1f;
        }
        if (CurrentGameState == GameState.Paused || CurrentGameState == GameState.GameOver || CurrentGameState == GameState.Win)
        {
            //ResumeGame();
            UpdateGameState(GameState.Playing);
        } else if (CurrentGameState == GameState.Playing)
        {
            PauseGame();
        }
        //if (level > 0 && MissionBriefMenu != null && !MissionBriefMenu.activeSelf)
        //{
        //    MissionBriefMenu.SetActive(true);
        //}
    }

    public void GameOver()
    {
        Debug.Log("GameOver triggered");

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayLoseSound();

        Time.timeScale = 0f;
        GameOverMenu.SetActive(true);

        // ❌ BỎ dòng dưới
        // UpdateGameState(GameState.GameOver);
    }


    public void HideMissionBriefMenu()
    {
        if (MissionBriefMenu.activeSelf)
        {
            MissionBriefMenu.SetActive(false);
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
