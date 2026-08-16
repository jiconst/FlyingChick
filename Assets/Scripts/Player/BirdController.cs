using System;
using UnityEngine;

namespace FlyingChick
{
    // Orchestrates BirdPhysics: feeds it input/dt/scrollX each FixedUpdate,
    // advances the shared world scroll, and applies the canvas-space result
    // to this Transform through ScreenSpace. The bird's screen X position is
    // fixed (reference: bird.startX = W*0.28) -- the WORLD scrolls, not the
    // bird, so the camera never has to move.
    //
    // Only simulates while GameManager.State == Playing (sits still on the
    // Start/DayOver screens, matching the reference's idle render). Rebuilds
    // BirdPhysics against a fresh GroundSampler on every OnRunStart, since
    // GameManager generates a new terrain seed per run.
    public class BirdController : MonoBehaviour
    {
        [SerializeField] private float radius = 15f;
        [SerializeField] private float startXFraction = 0.28f;

        public event Action OnGreatSlideLanding;
        public event Action OnMissedLanding;
        public event Action OnLaunch; // grounded -> airborne this frame; drives the jump SFX

        public bool OnGround => physics.OnGround;
        public bool Airborne => physics.Airborne;
        public bool IsDiving => physics.IsDiving;
        public float Speed => physics.Speed;
        public float Radius => radius;
        // Canvas-space position, for other systems (Collectibles) doing
        // proximity checks against the bird without reversing the
        // ScreenSpace conversion off of Transform.position.
        public float CanvasX => physics.CanvasX;
        public float CanvasY => physics.CanvasY;
        // Positive when airborne above the terrain directly below the bird;
        // 0 (or negative mid-collision-snap) when grounded. Drives CameraZoom.
        public float HeightAboveGround { get; private set; }

        private BirdPhysics physics;
        private Camera cam;
        private BirdCollection collection;

        public void Configure(Camera camera)
        {
            cam = camera;
        }

        // Wired once BirdCollection exists (M6); applies StartSpeedBonus
        // perk on every ResetForNewRun from then on.
        public void SetCollection(BirdCollection collectionRef)
        {
            collection = collectionRef;
        }

        private void Start()
        {
            var gm = GameManager.Instance;
            gm.OnIslandAdvanced += HandleIslandAdvanced;
            gm.OnRunStart += ResetForNewRun;
            ResetForNewRun();
        }

        private void OnDestroy()
        {
            if (GameManager.Instance == null) return;
            GameManager.Instance.OnIslandAdvanced -= HandleIslandAdvanced;
            GameManager.Instance.OnRunStart -= ResetForNewRun;
        }

        private void ResetForNewRun()
        {
            var gm = GameManager.Instance;
            float width = ScreenSpace.ViewWidth(gm.ViewHeight, cam.aspect);
            float canvasStartX = width * startXFraction;

            physics = new BirdPhysics(canvasStartX, radius, gm.Ground);
            physics.Reset(gm.ScrollX);

            if (collection != null && collection.SelectedBird.Perk == PerkType.StartSpeedBonus)
                physics.AddSpeed(collection.SelectedBird.PerkValue);

            ApplyTransform();
        }

        // Reference: speed += 120 on island jump.
        private void HandleIslandAdvanced(int island) => physics.AddSpeed(120f);

        // Used by speed-coin pickups (M3): reference adds +260.
        public void AddSpeedBoost(float amount) => physics.AddSpeed(amount);

        private void FixedUpdate()
        {
            var gm = GameManager.Instance;
            if (gm.State != GameState.Playing) return;

            float dt = Time.fixedDeltaTime;
            bool holding = InputService.IsPointerHeld();
            float scrollXBeforeAdvance = gm.ScrollX;

            physics.Step(dt, scrollXBeforeAdvance, holding);
            gm.AdvanceScroll(physics.Speed * dt);

            if (physics.JustLandedGreatSlide) OnGreatSlideLanding?.Invoke();
            if (physics.JustLandedMiss) OnMissedLanding?.Invoke();
            if (physics.JustLaunched) OnLaunch?.Invoke();

            // Use the SAME scrollX that physics.Step() just used -- physics.CanvasY
            // corresponds to that x, not to gm.ScrollX after AdvanceScroll shifted it.
            float worldBirdX = scrollXBeforeAdvance + physics.CanvasX;
            HeightAboveGround = gm.Ground.GroundY(worldBirdX) - physics.CanvasY;

            ApplyTransform();
        }

        private void ApplyTransform()
        {
            var gm = GameManager.Instance;
            float localX = ScreenSpace.ToWorldX(physics.CanvasX, gm.ViewHeight, cam.aspect);
            float localY = ScreenSpace.ToWorldY(physics.CanvasY, gm.ViewHeight);
            transform.position = new Vector3(localX, localY, 0f);
            // canvas-space rotation is mirrored under the y-flip -> negate.
            transform.rotation = Quaternion.Euler(0f, 0f, -physics.Angle * Mathf.Rad2Deg);
        }
    }
}
