using System;
using UnityEngine;

namespace FlyingChick
{
    // Pooled green "Sky Flight" orb visuals + pickup detection, same
    // pooled-SpriteRenderer pattern as CoinSpawner/CloudSpawner. On pickup,
    // sends the bird into a real, physically-simulated soar for a few
    // seconds (BirdController.StartSkyFlight -> BirdPhysics.StartSkyFlight)
    // so the run genuinely flies high above the clouds -- CameraZoom needs
    // no special-casing for this, it just zooms out for the bird's actual
    // altitude the same way it would for any other high flight.
    //
    // Placement (SkyOrbField) is a simple random test build for now -- per
    // feedback, spacing/height/rarity get a real rebalancing pass later
    // once the buff itself is confirmed fun; this spawner doesn't encode
    // any tuning assumptions beyond "read whatever SkyOrbField hands back".
    // Height above ground is recomputed from the CURRENT GroundY(worldX)
    // every frame here (not stored in the field), same as CoinSpawner does
    // for the blue speed coin -- see CheckPickups/RenderVisible.
    public class SkyOrbSpawner : MonoBehaviour
    {
        [SerializeField] private int poolSize = 8;
        [SerializeField] private float pickupRadius = 34f;
        [SerializeField] private float flightDuration = 5.5f; // "5,6초 동안 날으는 느낌" 피드백 -- BirdPhysics.StartSkyFlight's arc length

        public event Action<Vector3, string, Color> OnPickupPopup;

        private static readonly Color OrbColor = new Color(0.3f, 0.9f, 0.45f);

        private SkyOrbField field;
        private BirdController bird;
        private Camera cam;
        private PickupBurst burst;

        private SpriteRenderer[] pool;
        private Sprite orbSprite;

        public void Configure(BirdController birdRef, Camera camera, PickupBurst burstRef, int seed)
        {
            bird = birdRef;
            cam = camera;
            burst = burstRef;
            field = new SkyOrbField(seed, GameManager.Instance.ScrollX);
        }

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
            field = new SkyOrbField(UnityEngine.Random.Range(1, int.MaxValue), GameManager.Instance.ScrollX);
            if (pool != null)
                foreach (var sr in pool) sr.enabled = false;
        }

        private void Awake()
        {
            orbSprite = ProceduralSprite.CreateCircle(22, OrbColor);

            pool = new SpriteRenderer[poolSize];
            for (int i = 0; i < poolSize; i++)
            {
                var go = new GameObject($"SkyOrb_{i}");
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = orbSprite;
                sr.sortingOrder = 5;
                sr.enabled = false;
                pool[i] = sr;
            }
        }

        private void LateUpdate()
        {
            var gm = GameManager.Instance;
            float leftEdge = ScreenSpace.LeftEdgeCanvasX(gm.ViewHeight, cam.aspect, cam.orthographicSize);
            float rightEdge = ScreenSpace.RightEdgeCanvasX(gm.ViewHeight, cam.aspect, cam.orthographicSize);
            field.EnsureCoverage(gm.ScrollX + rightEdge + (rightEdge - leftEdge) * 0.2f);

            CheckPickups(gm);
            RenderVisible(gm, leftEdge, rightEdge);
        }

        private void CheckPickups(GameManager gm)
        {
            var entries = field.Entries;
            float rr = pickupRadius * pickupRadius;

            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e.Taken) continue;

                float canvasX = e.WorldX - gm.ScrollX;
                float canvasY = gm.Ground.GroundY(e.WorldX) - e.Offset;
                float dx = canvasX - bird.CanvasX;
                float dy = canvasY - bird.CanvasY;
                if (dx * dx + dy * dy >= rr) continue;

                field.MarkTaken(i);
                Vector3 worldPos = ToWorldPos(gm, canvasX, canvasY);

                bird.StartSkyFlight(flightDuration);
                burst.Burst(worldPos, OrbColor, 14);
                OnPickupPopup?.Invoke(worldPos, Localization.Get("skyorb.popup"), OrbColor);
            }
        }

        private void RenderVisible(GameManager gm, float leftEdge, float rightEdge)
        {
            int used = 0;
            var entries = field.Entries;

            for (int i = 0; i < entries.Count && used < pool.Length; i++)
            {
                var e = entries[i];
                if (e.Taken) continue;

                float canvasX = e.WorldX - gm.ScrollX;
                if (canvasX < leftEdge - 60f || canvasX > rightEdge + 60f) continue;

                float canvasY = gm.Ground.GroundY(e.WorldX) - e.Offset;
                var sr = pool[used++];
                sr.enabled = true;
                sr.transform.position = ToWorldPos(gm, canvasX, canvasY);
            }

            for (int i = used; i < pool.Length; i++) pool[i].enabled = false;
        }

        private Vector3 ToWorldPos(GameManager gm, float canvasX, float canvasY)
        {
            float localX = ScreenSpace.ToWorldX(canvasX, gm.ViewHeight, cam.aspect);
            float localY = ScreenSpace.ToWorldY(canvasY, gm.ViewHeight);
            return new Vector3(localX, localY, 0f);
        }
    }
}
