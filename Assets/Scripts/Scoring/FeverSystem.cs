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

        // Longest single fever activation this run, for the Day Over stats
        // screen (reference: "LONGEST FEVER").
        public float LongestDuration { get; private set; }
        private float sessionElapsed;

        public event Action OnFeverStart;
        public event Action OnFeverEnd;

        private BirdCollection collection;

        // Wired once BirdCollection exists (M6); applies FeverDurationBonus
        // perk to both the base duration and the cap (otherwise the bonus
        // would just be wasted once a long fever chain hits maxDuration).
        public void SetCollection(BirdCollection collectionRef) => collection = collectionRef;

        private float PerkBonus => collection != null && collection.SelectedBird.Perk == PerkType.FeverDurationBonus
            ? collection.SelectedBird.PerkValue
            : 0f;

        private void Start()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.OnRunStart += HandleRunStart;
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.OnRunStart -= HandleRunStart;
        }

        public void TriggerOrExtend()
        {
            if (!IsActive)
            {
                IsActive = true;
                TimeRemaining = baseDuration + PerkBonus;
                sessionElapsed = 0f;
                OnFeverStart?.Invoke();
            }
            else
            {
                TimeRemaining = Mathf.Min(maxDuration + PerkBonus, TimeRemaining + extendPerSlide);
            }
        }

        public void EndImmediately()
        {
            if (!IsActive) return;
            IsActive = false;
            TimeRemaining = 0f;
            LongestDuration = Mathf.Max(LongestDuration, sessionElapsed);
            OnFeverEnd?.Invoke();
        }

        private void Update()
        {
            if (!IsActive) return;
            TimeRemaining -= Time.deltaTime;
            sessionElapsed += Time.deltaTime;
            if (TimeRemaining <= 0f) EndImmediately();
        }

        private void HandleRunStart()
        {
            IsActive = false;
            TimeRemaining = 0f;
            LongestDuration = 0f;
            sessionElapsed = 0f;
        }
    }
}
