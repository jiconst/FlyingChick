using System.IO;
using UnityEngine;

namespace HillyWings
{
    // Best score: PlayerPrefs (M4, spec: "PlayerPrefs는 최고점수만").
    // Everything else (coins, Nest Multiplier bonus, daily mission
    // date/progress): JsonUtility + Application.persistentDataPath (M5).
    // Singleton per project convention (GameManager/ScoreManager/SaveSystem
    // only). CoinWallet/DailyMissions/NestMultiplier read/write through
    // Data and call Save() -- SaveSystem itself has no game-logic opinions,
    // it's just the (de)serialization + file I/O boundary.
    public class SaveSystem : MonoBehaviour
    {
        public static SaveSystem Instance { get; private set; }

        private const string BestScoreKey = "HillyWings.BestScore";
        private const string SaveFileName = "hillywings_save.json";

        public int BestScore { get; private set; }
        public SaveData Data { get; private set; }

        private string SavePath => Path.Combine(Application.persistentDataPath, SaveFileName);

        private void Awake()
        {
            Instance = this;
            BestScore = PlayerPrefs.GetInt(BestScoreKey, 0);
            Data = LoadData();
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

        public void Save()
        {
            try
            {
                File.WriteAllText(SavePath, JsonUtility.ToJson(Data, prettyPrint: true));
            }
            catch (IOException e)
            {
                Debug.LogWarning($"HillyWings: save failed ({e.Message})");
            }
        }

        private SaveData LoadData()
        {
            try
            {
                if (File.Exists(SavePath))
                    return JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath));
            }
            catch (IOException e)
            {
                Debug.LogWarning($"HillyWings: load failed, starting fresh ({e.Message})");
            }
            return new SaveData();
        }
    }
}
