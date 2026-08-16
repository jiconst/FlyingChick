using System;
using UnityEngine;

namespace FlyingChick
{
    // Listens to BirdController's landing events and turns them into streak/
    // score/fever decisions. Reference: holdWasDownhill landing -> streak++,
    // base score 10*(mult/10), streak>=3 triggers/extends fever; a landing
    // without a dive resets the streak and ends fever immediately.
    public class SlideJudge : MonoBehaviour
    {
        [SerializeField] private int feverTriggerStreak = 3;
        [SerializeField] private float baseSlideScore = 10f;

        public int SlideStreak { get; private set; }
        public int TotalSlides { get; private set; }

        public event Action<int, int> OnGreatSlide; // current streak count, score gained this slide
        public event Action OnStreakBroken; // landed without diving; streak was reset

        private BirdController bird;
        private FeverSystem fever;
        private BirdCollection collection;

        public void Configure(BirdController birdRef, FeverSystem feverRef)
        {
            bird = birdRef;
            fever = feverRef;
            bird.OnGreatSlideLanding += HandleGreatSlide;
            bird.OnMissedLanding += HandleMiss;
        }

        // Wired once BirdCollection exists (M6); applies SlideScoreBonus perk.
        public void SetCollection(BirdCollection collectionRef) => collection = collectionRef;

        private void Start()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.OnRunStart += HandleRunStart;
        }

        private void OnDestroy()
        {
            if (bird != null)
            {
                bird.OnGreatSlideLanding -= HandleGreatSlide;
                bird.OnMissedLanding -= HandleMiss;
            }
            if (GameManager.Instance != null)
                GameManager.Instance.OnRunStart -= HandleRunStart;
        }

        private void HandleRunStart()
        {
            SlideStreak = 0;
            TotalSlides = 0;
        }

        private void HandleGreatSlide()
        {
            SlideStreak++;
            TotalSlides++;

            float mult = GameManager.Instance.Multiplier;
            float baseScore = baseSlideScore * (mult / 10f);
            if (collection != null && collection.SelectedBird.Perk == PerkType.SlideScoreBonus)
                baseScore *= 1f + collection.SelectedBird.PerkValue;
            float multiplierAtAdd = fever.Multiplier; // capture before this slide can trigger fever
            ScoreManager.Instance.AddScore(baseScore);

            if (SlideStreak >= feverTriggerStreak) fever.TriggerOrExtend();

            int gained = Mathf.RoundToInt(baseScore * multiplierAtAdd);
            OnGreatSlide?.Invoke(SlideStreak, gained);
        }

        private void HandleMiss()
        {
            bool hadStreak = SlideStreak > 0 || fever.IsActive;
            SlideStreak = 0;
            fever.EndImmediately();
            if (hadStreak) OnStreakBroken?.Invoke();
        }
    }
}
