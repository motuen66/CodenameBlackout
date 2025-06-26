using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;

namespace Assets.Scripts
{
    /// <summary>
    /// Score = InitScore - TotalSecondToCompleteLevel + a * RedGuardKilledScore + b * YellowGuardKilledScore 
    /// Where a,b = number of Red, Yellow Guards killed
    /// </summary>
    public class ScoreManager : MonoBehaviour
    {
        public static ScoreManager Instance { get; private set; }
        private int InitScore { get; set; } = 1000;

        private readonly int RedGuardKilledScore = 500;

        private readonly int YellowGuardKilledScore = 300;

        private int TotalSecondToCompleteLevel { get; set; } = 0;

        public int EarningScore { get; private set; } = 0;

        [SerializeField]
        private TextMeshProUGUI CountDownText;
        [SerializeField]
        private TextMeshProUGUI ScoreText;
        [SerializeField]
        private TextMeshProUGUI CurrentScoreText;
        [SerializeField]
        private TextMeshProUGUI HighScoreText;

        private void Start()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            GameManager.OnGameStateChanged += OnGameStateChanged;
        }
        private void Update()
        {
            CurrentScoreText.text = $"Score: {CalculateTotalScore()}";
        }

        private void OnDestroy()
        {
            GameManager.OnGameStateChanged -= OnGameStateChanged;
        }

        public void StartCountDown()
        {
            ResetScore();
            Debug.Log("StartCountDown called");
            StartCoroutine(CountSeconds());
        }

        private void OnGameStateChanged(GameState state)
        {
            //if (state == GameState.Win)
            //{
            //    TotalSecondToCompleteLevel = 0;
            //}
        }

        private IEnumerator CountSeconds()
        {
            while (GameManager.Instance.CurrentGameState == GameState.Playing)
            {
                yield return new WaitForSeconds(1f);
                TotalSecondToCompleteLevel++;
                if (CountDownText == null)
                {
                    CountDownText = GameObject.Find("CurrentTime")?.GetComponent<TextMeshProUGUI>();
                }
                CountDownText.text = $"Time: {FormatTime(TotalSecondToCompleteLevel)}";
            }
        }

        public void KillRedGuard() => EarningScore += RedGuardKilledScore;
        public void KillYelloGuard() => EarningScore += YellowGuardKilledScore;

        private string FormatTime(int seconds)
        {
            int minutes = seconds / 60;
            seconds = seconds % 60;
            return $"{minutes:D2}:{seconds:D2}";
        }

        internal void ShowTotalScore()
        {
            int score = CalculateTotalScore();
            ScoreText.text = score.ToString();
            SaveHighScore(GameManager.Instance.CurrentLevel, score);
        }

        internal void ShowHighScore(int level)
        {
            HighScoreText.text = $"High Score: {GetHighScore(level)}";
        }

        public int CalculateTotalScore()
        {
            return InitScore - TotalSecondToCompleteLevel + EarningScore;
        }

        public void SaveHighScore(int level, int score)
        {
            string levelName = $"level{level}HighScore";
            if (PlayerPrefs.HasKey(levelName))
            {
                int currentHighScore = PlayerPrefs.GetInt(levelName);
                if (score > currentHighScore)
                {
                    PlayerPrefs.SetInt(levelName, score);
                }
            }
            else
            {
                PlayerPrefs.SetInt(levelName, score);
            }
        }

        public string GetHighScore(int level)
        {
            string levelName = $"level{level}HighScore";
            if (PlayerPrefs.HasKey(levelName))
            {
                return PlayerPrefs.GetInt(levelName).ToString();
            }
            return "0";
        }

        public void ResetScore()
        {
            EarningScore = 0;
            TotalSecondToCompleteLevel = 0;
        }
    }
}