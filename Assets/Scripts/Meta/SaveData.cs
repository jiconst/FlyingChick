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

        // Localization/nickname feature.
        public int language; // Language enum cast to int; 0 = Korean (default)
        public string nickname = "";

        // Online account (FlyingChick-Server) -- JWT access token from a
        // previous login/signup, re-validated against GET /auth/me on
        // startup (see AuthService.ValidateStoredToken). Empty means
        // logged out. Plain JSON on disk is NOT secure storage; acceptable
        // for this hobby project's threat model, flagged as a future
        // hardening item (Keychain/Keystore) if it ever matters.
        public string authToken = "";

        // 사운드 설정 (설정 화면). 기본값은 AudioManager의 기존 SerializeField
        // 기본값(sfxVolume=0.6f, bgmVolume=0.16f)과 동일하게 맞춤 — 새 세이브
        // 파일에는 이 키가 없으므로 JsonUtility가 이 필드 초기값을 그대로
        // 씀(기존 language/nickname 필드와 같은 패턴).
        public float musicVolume = 0.16f;
        public float sfxVolume = 0.6f;
    }
}
