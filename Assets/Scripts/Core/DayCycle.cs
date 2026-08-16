using System;
using UnityEngine;

namespace FlyingChick
{
    // Reference: DAY_LENGTH = 90s, dayTime 0..1, endGame() when it hits 1.
    // Only ticks while GameManager.State == Playing; resets on OnRunStart.
    public class DayCycle : MonoBehaviour
    {
        [SerializeField] private float dayDuration = 90f;

        public float DayTime { get; private set; }
        public float ElapsedTime { get; private set; }

        public event Action OnDayOver;

        private void Start()
        {
            GameManager.Instance.OnRunStart += HandleRunStart;
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.OnRunStart -= HandleRunStart;
        }

        private void HandleRunStart()
        {
            ElapsedTime = 0f;
            DayTime = 0f;
        }

        private void Update()
        {
            var gm = GameManager.Instance;
            if (gm.State != GameState.Playing) return;

            ElapsedTime += Time.deltaTime;
            DayTime = Mathf.Min(1f, ElapsedTime / dayDuration);

            if (DayTime >= 1f)
            {
                gm.EndRun();
                OnDayOver?.Invoke();
            }
        }
    }
}
