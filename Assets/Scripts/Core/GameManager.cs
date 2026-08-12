using System;
using UnityEngine;

namespace FlyingChick
{
    // Prototype-scope game state: a single day/night timer and a running
    // score driven by the bird's great-slide/fever events. Coins, nests,
    // islands, and localization are deliberately out of scope for this pass.
    public class GameManager : MonoBehaviour
    {
        [SerializeField] private BirdController bird;
        [SerializeField] private float dayDurationSeconds = 90f;
        [SerializeField] private int greatSlideScore = 10;

        public event Action OnDayOver;

        public float TimeRemaining { get; private set; }
        public int Score { get; private set; }
        public bool IsDayOver { get; private set; }

        private float scoreMultiplier = 1f;

        public void Configure(BirdController birdRef, float dayDurationSecondsValue)
        {
            bird = birdRef;
            dayDurationSeconds = dayDurationSecondsValue;
        }

        private void Start()
        {
            TimeRemaining = dayDurationSeconds;
            if (bird != null)
            {
                bird.OnGreatSlide += HandleGreatSlide;
                bird.OnFeverStart += HandleFeverStart;
                bird.OnFeverEnd += HandleFeverEnd;
            }
        }

        private void Update()
        {
            if (IsDayOver) return;

            TimeRemaining -= Time.deltaTime;
            if (TimeRemaining <= 0f)
            {
                TimeRemaining = 0f;
                IsDayOver = true;
                if (bird != null) bird.ControlEnabled = false;
                OnDayOver?.Invoke();
            }
        }

        private void HandleGreatSlide()
        {
            Score += Mathf.RoundToInt(greatSlideScore * scoreMultiplier);
        }

        private void HandleFeverStart() => scoreMultiplier = 2f;
        private void HandleFeverEnd() => scoreMultiplier = 1f;
    }
}
