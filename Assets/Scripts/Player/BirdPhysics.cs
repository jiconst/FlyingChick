using UnityEngine;

namespace HillyWings
{
    // Pure physics state + step function, ported 1:1 from hilly-wings.html's
    // update() bird section (same constants, same order of operations).
    // Operates in canvas-space (y grows downward) so it stays directly
    // comparable to the reference. No MonoBehaviour/Transform coupling --
    // BirdController applies the result to a Transform via ScreenSpace.
    //
    // Queries the single shared GroundSampler instance (GameManager.Ground)
    // rather than a static function -- terrain is now stateful/randomized,
    // and physics must read the exact same instance TerrainGenerator draws
    // from, or the ground line and the physics contact point drift apart.
    public class BirdPhysics
    {
        public const float GravAir = 1500f;
        public const float GravHold = 3400f;
        public const float SpeedBase = 360f;
        public const float SpeedMax = 980f;
        public const float SpeedMin = 300f;
        public const float DiveAccel = 520f;

        // Sky Flight orb buff (Collectibles/SkyOrbSpawner.cs): a real,
        // physically-simulated soar instead of a camera trick, so the
        // zoom naturally follows the bird's actual height (see StartSkyFlight).
        // ~15% of GravAir -- floaty enough that a single launch stays up for
        // several seconds instead of the usual sub-second hop.
        public const float SkyFlightGravity = 220f;

        // Loosened from the reference's 0.06 / -0.10 / 0.9 per playtest
        // feedback -- landing a Great Slide (and therefore a 3-streak) felt
        // too dependent on precise timing. Registers a dive on gentler
        // downhills and launches earlier/with less speed, so a slide is
        // more forgiving to land successfully.
        public const float DownhillSlopeThreshold = 0.04f;
        public const float LaunchSlopeThreshold = -0.07f;
        public const float LaunchSpeedFactor = 0.75f;
        // "많이 튕기지 않았으면"(1.25->0.8) + "오르막 힘겹게"(UphillDecelRate)
        // 튜닝을 둘 다 시도했다가 "게임이 너무 쉬워졌다" 피드백으로 되돌림
        // (2026-08-20) -- 원래 값(1.25)으로 복원. 튜닝 자체는 CLAUDE.md에
        // 기록만 남기고 코드에서는 제거.
        public const float LaunchVelocityFactor = 1.25f;

        // A looser launch threshold means near-flat hilltops can trigger a
        // brief, unintended micro-hop (a few frames of Airborne from
        // curvature alone). Judging every such blip as a miss reset the
        // streak constantly. Flights shorter than this are ignored for
        // scoring entirely -- holdWasDownhill carries through untouched, so
        // a genuine ongoing dive isn't punished by terrain jitter.
        public const float MinAirborneTimeForJudging = 0.15f;

        public float CanvasX { get; }
        public float CanvasY { get; private set; }
        public float VerticalVelocity { get; private set; }
        public float Speed { get; private set; } = SpeedBase;
        public bool OnGround { get; private set; }
        public bool Airborne { get; private set; }
        public float Angle { get; private set; }
        public bool IsDiving { get; private set; }

        // True for exactly one Step() call: the frame a flight that began
        // with a proper downhill dive lands (== a Great Slide, reference
        // holdWasDownhill). False-landing (JustLandedMiss) breaks the streak.
        public bool JustLandedGreatSlide { get; private set; }
        public bool JustLandedMiss { get; private set; }
        // True for exactly one Step() call: grounded -> airborne this frame
        // (either an explicit launch off an uphill crest, or the implicit
        // "terrain curved away faster than gravity" case). Drives the jump SFX.
        public bool JustLaunched { get; private set; }

        private readonly float radius;
        private readonly GroundSampler ground;
        private bool holdWasDownhill;
        private float airborneTime;
        private float skyFlightTimeRemaining;

        // Drives BirdController's continuous distance-score trickle while
        // flying (see BirdController.FixedUpdate) -- without this, score
        // only grows from landing/pickup EVENTS, none of which happen while
        // high above the clouds, so it visibly stopped climbing for the
        // whole buff ("녹색 공을 먹었을때도 지속적으로 측정이 되는 버전으로"
        // feedback).
        public bool IsSkyFlightActive => skyFlightTimeRemaining > 0f;

        public BirdPhysics(float canvasStartX, float radiusValue, GroundSampler groundSampler)
        {
            CanvasX = canvasStartX;
            radius = radiusValue;
            ground = groundSampler;
        }

        // Shared by island-jump kicks (+120, M2) and speed-coin pickups (M3).
        public void AddSpeed(float amount)
        {
            Speed = Mathf.Min(SpeedMax, Speed + amount);
        }

        // Sky Flight orb: kicks off a real, physically-simulated soar rather
        // than just forcing the camera to zoom out -- see Step()'s gravity
        // override below. Only applies the launch kick if a flight isn't
        // already in progress (re-catching a second orb mid-flight just
        // extends the timer via Mathf.Max, it doesn't re-kick velocity and
        // jolt the arc).
        //
        // v0 = -g*duration/2 is the launch velocity for a SYMMETRIC
        // ballistic arc under SkyFlightGravity that returns to roughly the
        // same height exactly when duration elapses -- rise for the first
        // half, glide near the top, ease back down for the second half, all
        // through the exact same integrator normal flight uses (Step()
        // below), so it reads as the bird actually flying, not a scripted
        // hover.
        public void StartSkyFlight(float duration)
        {
            if (skyFlightTimeRemaining <= 0f)
                VerticalVelocity = -SkyFlightGravity * duration * 0.5f;

            skyFlightTimeRemaining = Mathf.Max(skyFlightTimeRemaining, duration);
        }

        public void Reset(float scrollX)
        {
            CanvasY = ground.GroundY(scrollX + CanvasX) - radius;
            VerticalVelocity = 0f;
            Speed = SpeedBase;
            OnGround = true;
            Airborne = false;
            Angle = 0f;
            IsDiving = false;
            holdWasDownhill = false;
            airborneTime = 0f;
        }

        public void Step(float dt, float scrollX, bool holding)
        {
            JustLandedGreatSlide = false;
            JustLandedMiss = false;
            JustLaunched = false;
            bool wasOnGround = OnGround;

            float worldBirdX = scrollX + CanvasX;
            float slope = ground.GroundSlope(worldBirdX);

            if (skyFlightTimeRemaining > 0f) skyFlightTimeRemaining -= dt;
            bool skyFlightActive = skyFlightTimeRemaining > 0f;

            if (skyFlightActive)
            {
                // 요청("좀더 속도감있게"): 버프 도중엔 평소의 감속 로직을 끄고
                // 최고 속도로 고정 -- 병아리가 실제로 붕 뜨는 것만으론 느리게
                // 흘러가는 느낌이라, 지형/코인/구름이 아래로 빠르게 스쳐 지나가는
                // 체감 속도가 있어야 "빠르게 하늘을 가른다"는 느낌이 남.
                Speed = SpeedMax;
            }
            else
            {
                if (Speed > SpeedBase) Speed -= 90f * dt;
                if (Speed < SpeedBase) Speed += 60f * dt;
                Speed = Mathf.Clamp(Speed, SpeedMin, SpeedMax);
            }

            // While a Sky Flight buff is active, gravity is overridden
            // regardless of `holding` -- the point is a graceful, mostly
            // hands-off soar above the clouds, not a player-controlled dive.
            float g = skyFlightActive ? SkyFlightGravity : (holding ? GravHold : GravAir);
            VerticalVelocity += g * dt;
            CanvasY += VerticalVelocity * dt;

            float groundY = ground.GroundY(worldBirdX) - radius;
            IsDiving = false;

            if (CanvasY >= groundY)
            {
                if (Airborne)
                {
                    Airborne = false;
                    if (airborneTime >= MinAirborneTimeForJudging)
                    {
                        if (holdWasDownhill) JustLandedGreatSlide = true;
                        else JustLandedMiss = true;
                        holdWasDownhill = false;
                    }
                    airborneTime = 0f;
                }

                CanvasY = groundY;
                OnGround = true;
                VerticalVelocity = slope * Speed;

                if (holding && slope > DownhillSlopeThreshold)
                {
                    holdWasDownhill = true;
                    IsDiving = true;
                    Speed = Mathf.Min(SpeedMax, Speed + DiveAccel * dt);
                }

                if (slope < LaunchSlopeThreshold && Speed > SpeedBase * LaunchSpeedFactor)
                {
                    VerticalVelocity = slope * Speed * LaunchVelocityFactor;
                    OnGround = false;
                    Airborne = true;
                }
            }
            else
            {
                OnGround = false;
                if (!Airborne && VerticalVelocity < 0f) Airborne = true;
            }

            if (Airborne) airborneTime += dt;
            if (wasOnGround && Airborne) JustLaunched = true;

            float targetAngle = Mathf.Atan2(VerticalVelocity, Speed);
            Angle += (targetAngle - Angle) * Mathf.Min(1f, dt * 10f);
        }
    }
}
