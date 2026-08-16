using System;
using UnityEngine;

namespace FlyingChick
{
    // Owns fever's timer/multiplier state only -- it doesn't know WHEN fever
    // should start (that's SlideJudge's call, based on streak). Reference:
    // 5s base, +2.5s per great slide while active, capped at 20s, x2 score.
    public class FeverSystem : MonoBehaviour
    {
        [SerializeField] private float baseDuration = 5f;
        [SerializeField] private float extendPerSlide = 2.5f;
        [SerializeField] private float maxDuration = 20f;
        [SerializeField] private float multiplierWhenActive = 2f;

        public bool IsActive { get; private set; }
        public float TimeRemaining { get; private set; }
        public float Multiplier => IsActive ? multiplierWhenActive : 1f;

        public event Action OnFeverStart;
        public event Action OnFeverEnd;

        public void TriggerOrExtend()
        {
            if (!IsActive)
            {
                IsActive = true;
                TimeRemaining = baseDuration;
                OnFeverStart?.Invoke();
            }
            else
            {
                TimeRemaining = Mathf.Min(maxDuration, TimeRemaining + extendPerSlide);
            }
        }

        public void EndImmediately()
        {
            if (!IsActive) return;
            IsActive = false;
            TimeRemaining = 0f;
            OnFeverEnd?.Invoke();
        }

        private void Update()
        {
            if (!IsActive) return;
            TimeRemaining -= Time.deltaTime;
            if (TimeRemaining <= 0f) EndImmediately();
        }
    }
}
