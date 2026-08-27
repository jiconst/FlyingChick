using System;
using UnityEngine;

namespace HillyWings
{
    // Spec: 3 objectives rolled per run; completing all 3 permanently adds
    // +1 to the STARTING island multiplier for every future run (persisted).
    // Failing just keeps the current bonus -- never subtracts.
    //
    // Simplification from the original spec's third example objective
    // ("1번 섬에서 5000점" -- 5000 points while still on island 1) to a
    // plain "5000점 획득" this run: tracking a score subtotal scoped to a
    // specific island would need new state threaded through ScoreManager
    // for one mission's sake. Documented here rather than silently dropped.
    //
    // Objectives are evaluated once, at Day Over, against that run's live
    // stats (SlideJudge/FeverSystem/CloudSpawner/ScoreManager/GameManager) --
    // no separate progress tracking needed since those systems already hold
    // the numbers for the whole run.
    public class NestMultiplier : MonoBehaviour
    {
        private const int PickCount = 3;

        private GameManager gameManager;
        private SlideJudge slideJudge;
        private FeverSystem fever;
        private CloudSpawner cloudSpawner;
        private ScoreManager score;

        public MissionDefinition[] ActiveMissions { get; private set; } = new MissionDefinition[0];
        public int Bonus { get; private set; }

        public event Action<bool[]> OnRunEvaluated; // per-mission pass/fail, in ActiveMissions order

        public void Configure(GameManager gameManagerRef, SlideJudge slideJudgeRef, FeverSystem feverRef, CloudSpawner cloudSpawnerRef, ScoreManager scoreRef)
        {
            gameManager = gameManagerRef;
            slideJudge = slideJudgeRef;
            fever = feverRef;
            cloudSpawner = cloudSpawnerRef;
            score = scoreRef;

            Bonus = SaveSystem.Instance != null ? SaveSystem.Instance.Data.nestMultiplierBonus : 0;
            gameManager.NestBonus = Bonus;

            gameManager.OnRunStart += RollMissions;
            gameManager.OnRunEnd += EvaluateRunEnd;
            RollMissions();
        }

        private void OnDestroy()
        {
            if (gameManager == null) return;
            gameManager.OnRunStart -= RollMissions;
            gameManager.OnRunEnd -= EvaluateRunEnd;
        }

        // Subscribed to GameManager.OnRunEnd -- fires while this run's stats
        // are still live, right before State becomes DayOver.
        private void EvaluateRunEnd()
        {
            var results = new bool[ActiveMissions.Length];
            bool allPassed = true;
            for (int i = 0; i < ActiveMissions.Length; i++)
            {
                results[i] = GetProgress(ActiveMissions[i]) >= ActiveMissions[i].Target;
                allPassed &= results[i];
            }

            OnRunEvaluated?.Invoke(results);

            if (allPassed && ActiveMissions.Length > 0)
            {
                Bonus++;
                if (SaveSystem.Instance != null)
                {
                    SaveSystem.Instance.Data.nestMultiplierBonus = Bonus;
                    SaveSystem.Instance.Save();
                }
            }
        }

        public float GetProgress(MissionDefinition mission)
        {
            switch (mission.Type)
            {
                case MissionType.CloudTouchCount: return cloudSpawner.TouchCount;
                case MissionType.FeverDuration: return fever.LongestDuration;
                case MissionType.ScoreReached: return score.Score;
                case MissionType.GreatSlideCount: return slideJudge.TotalSlides;
                case MissionType.ReachIsland: return gameManager.Island;
                default: return 0f;
            }
        }

        private void RollMissions()
        {
            // Fisher-Yates over a copy of the pool, take the first PickCount.
            var pool = (MissionDefinition[])MissionPool.Nest.Clone();
            for (int i = pool.Length - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (pool[i], pool[j]) = (pool[j], pool[i]);
            }

            int count = Mathf.Min(PickCount, pool.Length);
            ActiveMissions = new MissionDefinition[count];
            Array.Copy(pool, ActiveMissions, count);

            // GameManager might have applied a new bonus from a previous
            // run's evaluation -- keep it in sync at the start of every run.
            gameManager.NestBonus = Bonus;
        }
    }
}
