using UnityEngine;

namespace FlyingChick
{
    // M4 scope only: best score via PlayerPrefs (spec: "최고점수만
    // PlayerPrefs"). Coin wallet / bird collection / missions are M5, backed
    // by JsonUtility + persistentDataPath -- not here. Singleton per project
    // convention (GameManager/ScoreManager/SaveSystem only).
    public class SaveSystem : MonoBehaviour
    {
        public static SaveSystem Instance { get; private set; }

        private const string BestScoreKey = "FlyingChick.BestScore";

        public int BestScore { get; private set; }

        private void Awake()
        {
            Instance = this;
            BestScore = PlayerPrefs.GetInt(BestScoreKey, 0);
        }

        // Returns true if this score set a new best.
        public bool SubmitScore(int score)
        {
            if (score <= BestScore) return false;
            BestScore = score;
            PlayerPrefs.SetInt(BestScoreKey, BestScore);
            PlayerPrefs.Save();
            return true;
        }
    }
}
