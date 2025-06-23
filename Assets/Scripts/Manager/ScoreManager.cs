using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
        private int InitScore { get; set; } = 300;

        private readonly int RedGuardKilledScore = 50;

        private readonly int YellowGuardKilledScore = 30;

        private int totalSecondToCompleteLevel { get; set; } = 0;

        public int TotalScore { get; private set; } = 0;


        private void Start()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            GameManager.OnGameStateChanged += OnGameStateChanged;
            StartCoroutine(CountSeconds());
        }

        private void OnDestroy()
        {
            GameManager.OnGameStateChanged -= OnGameStateChanged;
        }

        private void OnGameStateChanged(GameState state)
        {
            if (state == GameState.Win)
            {
                totalSecondToCompleteLevel = 0;
            }
        }

        private IEnumerator CountSeconds()
        {
            while (GameManager.Instance.CurrentGameState == GameState.Playing)
            {
                yield return new WaitForSeconds(1f);
                totalSecondToCompleteLevel++;
            }
        }

        public int CalculateTotalScore() => InitScore - totalSecondToCompleteLevel;

        public void KillRedGuard() => TotalScore += RedGuardKilledScore;
        public void KillYelloGuard() => TotalScore += YellowGuardKilledScore;
    }
}
