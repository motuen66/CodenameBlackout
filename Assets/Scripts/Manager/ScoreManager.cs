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

        private void Start()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            GameManager.OnGameStateChanged += OnGameStateChanged;
        }

        private void OnDestroy()
        {
            GameManager.OnGameStateChanged -= OnGameStateChanged;
        }

        public void StartCountDown()
        {
            Debug.Log("StartCountDown called");
            TotalSecondToCompleteLevel = 0;
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
                CountDownText.text = FormatTime(TotalSecondToCompleteLevel);
                if (CountDownText == null)
                {
                    Debug.LogError("CountDownText chưa được gán!");
                } else if (CountDownText.text == null)
                {
                    Debug.LogError("CountDownText.text chưa được gán!");
                }
                Debug.Log(FormatTime(TotalSecondToCompleteLevel));
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
            Debug.Log($"{TotalSecondToCompleteLevel} {EarningScore}");
            int score = InitScore - TotalSecondToCompleteLevel + EarningScore;
            ScoreText.text = score.ToString();
        }

        public void ResetScore()
        {
            EarningScore = 0;
            TotalSecondToCompleteLevel = 0;
        }
    }
}
