using System.Collections.Generic;
using UnityEngine;

namespace HillyWings
{
    // Spec: local Top 10 scores + cumulative stats (total slides, total runs
    // -- "총 비행일 수", one completed run/day-cycle = one day flown).
    // Persisted through SaveSystem.Data. Records at GameManager.OnRunEnd,
    // same moment CoinWallet/NestMultiplier do their run-end bookkeeping.
    public class Leaderboard : MonoBehaviour
    {
        private const int MaxEntries = 10;

        public IReadOnlyList<int> TopScores { get; private set; } = new List<int>();
        public int TotalSlidesAllTime { get; private set; }
        public int TotalRuns { get; private set; }

        private GameManager gameManager;
        private ScoreManager score;
        private SlideJudge slideJudge;

        public void Configure(GameManager gameManagerRef, ScoreManager scoreRef, SlideJudge slideJudgeRef)
        {
            gameManager = gameManagerRef;
            score = scoreRef;
            slideJudge = slideJudgeRef;

            var data = SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;
            if (data != null)
            {
                TopScores = new List<int>(data.topScores);
                TotalSlidesAllTime = data.totalSlidesAllTime;
                TotalRuns = data.totalRuns;
            }

            gameManager.OnRunEnd += HandleRunEnd;
        }

        private void OnDestroy()
        {
            if (gameManager != null) gameManager.OnRunEnd -= HandleRunEnd;
        }

        private void HandleRunEnd()
        {
            var scores = new List<int>(TopScores) { score.Score };
            scores.Sort((a, b) => b.CompareTo(a));
            if (scores.Count > MaxEntries) scores.RemoveRange(MaxEntries, scores.Count - MaxEntries);
            TopScores = scores;

            TotalSlidesAllTime += slideJudge.TotalSlides;
            TotalRuns += 1;

            Persist();
        }

        private void Persist()
        {
            if (SaveSystem.Instance == null) return;
            var data = SaveSystem.Instance.Data;
            data.topScores = new List<int>(TopScores).ToArray();
            data.totalSlidesAllTime = TotalSlidesAllTime;
            data.totalRuns = TotalRuns;
            SaveSystem.Instance.Save();
        }
    }
}
