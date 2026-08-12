using System;
using UnityEngine;

namespace FlyingChick
{
    // Tiny-Wings-style slide/glide physics.
    //
    // Every fixed step we integrate the bird under gravity as if it were
    // airborne, then compare the resulting position against the terrain
    // height at that x. If the free-falling position would end up below the
    // ground, the bird is "grounded": snap it onto the slope and redirect its
    // speed along the tangent. If a hilltop curves away faster than gravity
    // can pull the bird down, that same comparison naturally leaves the
    // candidate position above the ground, and the bird stays airborne. This
    // one check is what produces both landing AND launching without separate
    // state-specific code paths.
    public class BirdController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TerrainGenerator terrain;

        [Header("Physics")]
        [SerializeField] private float gravity = 20f;
        [SerializeField] private float diveAcceleration = 18f;
        [SerializeField] private float minSpeed = 4f;
        [SerializeField] private float maxSpeed = 28f;
        [SerializeField] private float startSpeed = 8f;

        [Header("Great Slide / Fever")]
        [SerializeField] private int greatSlideComboThreshold = 2;
        [SerializeField] private int feverComboThreshold = 3;
        [SerializeField] private float feverBaseDuration = 4f;
        [SerializeField] private float feverExtendPerSlide = 1.5f;
        [SerializeField] private float landingInputToleranceSeconds = 0.12f;

        public event Action OnLanded;
        public event Action OnLaunched;
        public event Action OnGreatSlide;
        public event Action OnFeverStart;
        public event Action OnFeverEnd;

        public bool IsGrounded { get; private set; }
        public bool IsInFever { get; private set; }
        public bool IsDiving { get; private set; }
        public bool IsHoldingInput { get; private set; }
        public float Speed => velocity.magnitude;
        public float HeightAboveGround { get; private set; }
        public bool ControlEnabled { get; set; } = true;

        private Vector2 position;
        private Vector2 velocity;
        private int slideCombo;
        private float feverTimer;
        private float lastInputDownTime = -10f;

        // Called by code (GameBootstrapper) right after AddComponent, before
        // any FixedUpdate has run, so it fully replaces the Awake defaults.
        public void Configure(TerrainGenerator terrainRef, float gravityValue, float diveAccelerationValue, float maxSpeedValue, float startSpeedValue)
        {
            terrain = terrainRef;
            gravity = gravityValue;
            diveAcceleration = diveAccelerationValue;
            maxSpeed = maxSpeedValue;
            startSpeed = startSpeedValue;
            velocity = new Vector2(startSpeed, 0f);
        }

        private void Awake()
        {
            position = transform.position;
            velocity = new Vector2(startSpeed, 0f);
        }

        private void Update()
        {
            if (InputService.IsPointerDownThisFrame())
                lastInputDownTime = Time.time;
        }

        private void FixedUpdate()
        {
            if (!ControlEnabled || terrain == null) return;

            float dt = Time.fixedDeltaTime;
            bool wasGrounded = IsGrounded;
            IsHoldingInput = InputService.IsPointerHeld();

            velocity.y -= gravity * dt;
            Vector2 candidate = position + velocity * dt;
            float terrainY = terrain.HeightAt(candidate.x);

            if (candidate.y <= terrainY)
            {
                LandOn(candidate.x, terrainY, wasGrounded, dt);
            }
            else
            {
                position = candidate;
                IsGrounded = false;
                IsDiving = false;
                HeightAboveGround = candidate.y - terrain.HeightAt(candidate.x);
                if (wasGrounded) OnLaunched?.Invoke();
            }

            TickFever(dt);

            transform.position = position;
            float angle = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        private void LandOn(float x, float terrainY, bool wasGrounded, float dt)
        {
            Vector2 tangent = terrain.TangentAt(x);
            float speed = velocity.magnitude;

            float slopeGravity = Vector2.Dot(new Vector2(0f, -gravity), tangent);
            speed += slopeGravity * dt;

            bool descending = tangent.y < 0f;
            IsDiving = IsHoldingInput && descending;

            if (IsDiving)
                speed += diveAcceleration * dt;

            speed = Mathf.Clamp(speed, minSpeed, maxSpeed);
            velocity = tangent * speed;
            position = new Vector2(x, terrainY);
            HeightAboveGround = 0f;
            IsGrounded = true;

            if (!wasGrounded)
            {
                bool preciseTiming = Time.time - lastInputDownTime <= landingInputToleranceSeconds;
                OnLanded?.Invoke();
                EvaluateSlideTiming(preciseTiming && descending);
            }
        }

        private void EvaluateSlideTiming(bool hit)
        {
            if (hit)
            {
                slideCombo++;
                if (slideCombo >= greatSlideComboThreshold)
                    OnGreatSlide?.Invoke();

                if (slideCombo >= feverComboThreshold)
                {
                    if (!IsInFever)
                    {
                        IsInFever = true;
                        feverTimer = feverBaseDuration;
                        OnFeverStart?.Invoke();
                    }
                    else
                    {
                        feverTimer += feverExtendPerSlide;
                    }
                }
            }
            else
            {
                slideCombo = 0;
                if (IsInFever)
                {
                    IsInFever = false;
                    feverTimer = 0f;
                    OnFeverEnd?.Invoke();
                }
            }
        }

        private void TickFever(float dt)
        {
            if (!IsInFever) return;
            feverTimer -= dt;
            if (feverTimer <= 0f)
            {
                IsInFever = false;
                OnFeverEnd?.Invoke();
            }
        }
    }
}
