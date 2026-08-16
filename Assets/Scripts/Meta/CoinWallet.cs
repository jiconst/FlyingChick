using System;
using UnityEngine;

namespace FlyingChick
{
    // Meta-currency wallet, persisted through SaveSystem.Data.coins. Not a
    // singleton (only GameManager/ScoreManager/SaveSystem are, per project
    // convention) -- other systems get it via Configure.
    //
    // No reference formula exists for "run score -> coins" (flying-chick.html
    // doesn't have a meta layer at all; this is purely from the screenshot
    // spec). Chose score/50, rounded down, as a simple starting ratio --
    // easy single constant to retune once real balance passes happen.
    public class CoinWallet : MonoBehaviour
    {
        [SerializeField] private float scoreToCoinsRatio = 1f / 50f;

        public int Coins { get; private set; }
        public int LastRunCoinsAwarded { get; private set; }

        public event Action<int> OnCoinsChanged;

        private GameManager gameManager;
        private ScoreManager score;

        public void Configure(GameManager gameManagerRef, ScoreManager scoreRef)
        {
            gameManager = gameManagerRef;
            score = scoreRef;
            gameManager.OnRunEnd += HandleRunEnd;
        }

        private void Awake()
        {
            Coins = SaveSystem.Instance != null ? SaveSystem.Instance.Data.coins : 0;
        }

        private void OnDestroy()
        {
            if (gameManager != null) gameManager.OnRunEnd -= HandleRunEnd;
        }

        public void AddCoins(int amount)
        {
            if (amount == 0) return;
            Coins += amount;
            Persist();
            OnCoinsChanged?.Invoke(Coins);
        }

        // Used by the egg shop (M6). Returns false (no change) if funds are short.
        public bool SpendCoins(int amount)
        {
            if (amount <= 0 || Coins < amount) return false;
            Coins -= amount;
            Persist();
            OnCoinsChanged?.Invoke(Coins);
            return true;
        }

        private void HandleRunEnd()
        {
            LastRunCoinsAwarded = Mathf.FloorToInt(score.Score * scoreToCoinsRatio);
            AddCoins(LastRunCoinsAwarded);
        }

        private void Persist()
        {
            if (SaveSystem.Instance == null) return;
            SaveSystem.Instance.Data.coins = Coins;
            SaveSystem.Instance.Save();
        }
    }
}
