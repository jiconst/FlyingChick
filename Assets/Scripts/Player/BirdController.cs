using System;
using UnityEngine;

namespace FlyingChick
{
    // Orchestrates BirdPhysics: feeds it input/dt/scrollX each FixedUpdate,
    // advances the shared world scroll, and applies the canvas-space result
    // to this Transform through ScreenSpace. The bird's screen X position is
    // fixed (reference: bird.startX = W*0.28) -- the WORLD scrolls, not the
    // bird, so the camera never has to move.
    public class BirdController : MonoBehaviour
    {
        [SerializeField] private float radius = 15f;
        [SerializeField] private float startXFraction = 0.28f;

        public event Action OnGreatSlideLanding;
        public event Action OnMissedLanding;

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

        private BirdPhysics physics;
        private Camera cam;

        public void Configure(Camera camera)
        {
            cam = camera;
        }

        private void Start()
        {
            var gm = GameManager.Instance;
            float width = ScreenSpace.ViewWidth(gm.ViewHeight, cam.aspect);
            float canvasStartX = width * startXFraction;

            physics = new BirdPhysics(canvasStartX, radius, gm.Ground);
            physics.Reset(gm.ScrollX);
            ApplyTransform();

            gm.OnIslandAdvanced += HandleIslandAdvanced;
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.OnIslandAdvanced -= HandleIslandAdvanced;
        }

        // Reference: speed += 120 on island jump.
        private void HandleIslandAdvanced(int island) => physics.AddSpeed(120f);

        // Used by speed-coin pickups (M3): reference adds +260.
        public void AddSpeedBoost(float amount) => physics.AddSpeed(amount);

        private void FixedUpdate()
        {
            var gm = GameManager.Instance;
            float dt = Time.fixedDeltaTime;
            bool holding = InputService.IsPointerHeld();

            physics.Step(dt, gm.ScrollX, holding);
            gm.AdvanceScroll(physics.Speed * dt);

            if (physics.JustLandedGreatSlide) OnGreatSlideLanding?.Invoke();
            if (physics.JustLandedMiss) OnMissedLanding?.Invoke();

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
