using System;

namespace FlyingChick
{
    // JsonUtility-serializable save blob (Application.persistentDataPath).
    // Best score is deliberately NOT here -- it stays in PlayerPrefs (M4,
    // SaveSystem.BestScore) per spec ("PlayerPrefs는 최고점수만").
    [Serializable]
    public class SaveData
    {
        public int coins;
        public int nestMultiplierBonus;

        // "yyyy-MM-dd"; missions re-roll and progress resets when this
        // no longer matches DateTime.Today.
        public string dailyMissionDate = "";
        public int[] dailyMissionTypes = new int[0]; // MissionType cast to int
        public int[] dailyMissionProgress = new int[0];
        public bool[] dailyMissionCompleted = new bool[0];

        // M6: bird collection.
        public string[] ownedBirdIds = new string[0];
        public string selectedBirdId = "";

        // M6: local leaderboard/stats.
        public int[] topScores = new int[0]; // sorted desc, capped at 10
        public int totalSlidesAllTime;
        public int totalRuns; // "총 비행일 수" -- one completed run = one day flown
    }
}
