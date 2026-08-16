using System;
using UnityEngine;

namespace FlyingChick
{
    // Reference: addScore(n) { let gain = n; if (feverActive) gain *= 2;
    // score += Math.round(gain); } -- fever doubling happens here, at the
    // single point of entry, not at each call site. Also collects the
    // island-jump bonus (50 * mult * 0.1). Singleton per project convention.
    public class ScoreManager : MonoBehaviour
    {
        public static ScoreManager Instance { get; private set; }

        public int Score { get; private set; }
        public event Action<int> OnScoreChanged;

        private FeverSystem fever;

        private void Awake()
        {
            Instance = this;
        }

        public void Configure(FeverSystem feverRef)
        {
            fever = feverRef;
        }

        private void Start()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.OnIslandAdvanced += HandleIslandAdvanced;
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.OnIslandAdvanced -= HandleIslandAdvanced;
        }

        public void AddScore(float baseAmount)
        {
            float gain = baseAmount * (fever != null ? fever.Multiplier : 1f);
            Score += Mathf.RoundToInt(gain);
            OnScoreChanged?.Invoke(Score);
        }

        private void HandleIslandAdvanced(int island)
        {
            AddScore(50f * GameManager.Instance.Multiplier * 0.1f);
        }
    }
}
