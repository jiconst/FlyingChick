using System;
using UnityEngine;

namespace FlyingChick
{
    // Spec: 3 missions/day, 100 coins each, reset when DateTime.Today
    // changes. Progress persists across runs AND app restarts within the
    // same day (SaveSystem.Data), unlike NestMultiplier which is scoped to
    // a single run. Missions are picked once per day (seeded by the date
    // string so re-launching the app the same day doesn't reroll them) and
    // tracked by subscribing to the same gameplay events other systems fire.
    public class DailyMissions : MonoBehaviour
    {
        private const int MissionCount = 3;
        [SerializeField] private int coinReward = 100;

        public MissionDefinition[] ActiveMissions { get; private set; } = new MissionDefinition[0];
        public int[] Progress { get; private set; } = new int[0];
        public bool[] Completed { get; private set; } = new bool[0];

        public event Action<int> OnMissionCompleted; // index into ActiveMissions

        private CoinWallet wallet;

        public void Configure(CoinWallet walletRef, SlideJudge slideJudge, FeverSystem fever, CoinSpawner coinSpawner, CloudSpawner cloudSpawner, GameManager gameManager)
        {
            wallet = walletRef;

            LoadOrRollForToday();

            slideJudge.OnGreatSlide += (streak, gained) => AddProgress(MissionType.GreatSlideCount, 1);
            fever.OnFeverStart += () => AddProgress(MissionType.FeverTriggerCount, 1);
            coinSpawner.OnCoinCollected += () => AddProgress(MissionType.CollectCoins, 1);
            cloudSpawner.OnCloudTouched += () => AddProgress(MissionType.CloudTouchCount, 1);
            gameManager.OnIslandAdvanced += island => SetBest(MissionType.ReachIsland, island);
        }

        private void LoadOrRollForToday()
        {
            string today = DateTime.Today.ToString("yyyy-MM-dd");
            var data = SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;

            if (data != null && data.dailyMissionDate == today && data.dailyMissionTypes.Length == MissionCount)
            {
                ActiveMissions = new MissionDefinition[MissionCount];
                Progress = (int[])data.dailyMissionProgress.Clone();
                Completed = (bool[])data.dailyMissionCompleted.Clone();
                for (int i = 0; i < MissionCount; i++)
                {
                    var type = (MissionType)data.dailyMissionTypes[i];
                    ActiveMissions[i] = FindDefinition(type);
                }
                return;
            }

            // New day (or first run ever): reroll, seeded by the date so
            // re-launching the app the same day gives the same 3 missions.
            var rng = new System.Random(today.GetHashCode());
            var pool = (MissionDefinition[])MissionPool.Daily.Clone();
            for (int i = pool.Length - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (pool[i], pool[j]) = (pool[j], pool[i]);
            }

            int count = Mathf.Min(MissionCount, pool.Length);
            ActiveMissions = new MissionDefinition[count];
            Array.Copy(pool, ActiveMissions, count);
            Progress = new int[count];
            Completed = new bool[count];

            Persist(today);
        }

        private MissionDefinition FindDefinition(MissionType type)
        {
            foreach (var m in MissionPool.Daily)
                if (m.Type == type) return m;
            return default;
        }

        private void AddProgress(MissionType type, int amount)
        {
            bool changed = false;
            for (int i = 0; i < ActiveMissions.Length; i++)
            {
                if (Completed[i] || ActiveMissions[i].Type != type) continue;
                Progress[i] += amount;
                changed = true;
                CheckCompletion(i);
            }
            if (changed) Persist(DateTime.Today.ToString("yyyy-MM-dd"));
        }

        private void SetBest(MissionType type, int value)
        {
            bool changed = false;
            for (int i = 0; i < ActiveMissions.Length; i++)
            {
                if (Completed[i] || ActiveMissions[i].Type != type) continue;
                if (value > Progress[i])
                {
                    Progress[i] = value;
                    changed = true;
                }
                CheckCompletion(i);
            }
            if (changed) Persist(DateTime.Today.ToString("yyyy-MM-dd"));
        }

        private void CheckCompletion(int index)
        {
            if (Completed[index] || Progress[index] < ActiveMissions[index].Target) return;
            Completed[index] = true;
            wallet.AddCoins(coinReward);
            OnMissionCompleted?.Invoke(index);
        }

        private void Persist(string today)
        {
            if (SaveSystem.Instance == null) return;
            var data = SaveSystem.Instance.Data;
            data.dailyMissionDate = today;
            data.dailyMissionTypes = new int[ActiveMissions.Length];
            for (int i = 0; i < ActiveMissions.Length; i++) data.dailyMissionTypes[i] = (int)ActiveMissions[i].Type;
            data.dailyMissionProgress = (int[])Progress.Clone();
            data.dailyMissionCompleted = (bool[])Completed.Clone();
            SaveSystem.Instance.Save();
        }
    }
}
