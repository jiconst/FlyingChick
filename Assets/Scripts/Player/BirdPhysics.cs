using UnityEngine;

namespace FlyingChick
{
    // Pure physics state + step function, ported 1:1 from flying-chick.html's
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

        // Loosened from the reference's 0.06 / -0.10 / 0.9 per playtest
        // feedback -- landing a Great Slide (and therefore a 3-streak) felt
        // too dependent on precise timing. Registers a dive on gentler
        // downhills and launches earlier/with less speed, so a slide is
        // more forgiving to land successfully.
        public const float DownhillSlopeThreshold = 0.04f;
        public const float LaunchSlopeThreshold = -0.07f;
        public const float LaunchSpeedFactor = 0.75f;
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

            if (Speed > SpeedBase) Speed -= 90f * dt;
            if (Speed < SpeedBase) Speed += 60f * dt;
            Speed = Mathf.Clamp(Speed, SpeedMin, SpeedMax);

            float g = holding ? GravHold : GravAir;
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
