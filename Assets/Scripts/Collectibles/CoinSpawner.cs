using System;
using UnityEngine;

namespace FlyingChick
{
    // Pooled coin visuals + pickup detection. No per-coin Instantiate/Destroy
    // -- a fixed pool of SpriteRenderers is repositioned onto whichever
    // CoinField entries are currently visible and untaken.
    public class CoinSpawner : MonoBehaviour
    {
        [SerializeField] private int poolSize = 48;
        [SerializeField] private float pickupRadius = 31f; // reference: bird.radius(15) + 16
        [SerializeField] private float speedBoostAmount = 260f;
        [SerializeField] private float coinScore = 3f;

        public event Action<Vector3, string, Color> OnPickupPopup;

        private CoinField field;
        private BirdController bird;
        private Camera cam;
        private PickupBurst burst;

        private SpriteRenderer[] pool;
        private Sprite coinSprite;
        private Sprite speedSprite;

        public void Configure(BirdController birdRef, Camera camera, PickupBurst burstRef, int seed)
        {
            bird = birdRef;
            cam = camera;
            burst = burstRef;
            field = new CoinField(seed, GameManager.Instance.ScrollX);
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
            field = new CoinField(UnityEngine.Random.Range(1, int.MaxValue), GameManager.Instance.ScrollX);
            if (pool != null)
                foreach (var sr in pool) sr.enabled = false;
        }

        private void Awake()
        {
            coinSprite = ProceduralSprite.CreateCircle(20, new Color(1f, 0.82f, 0.25f));
            speedSprite = ProceduralSprite.CreateCircle(24, new Color(0.25f, 0.65f, 1f));

            pool = new SpriteRenderer[poolSize];
            for (int i = 0; i < poolSize; i++)
            {
                var go = new GameObject($"Coin_{i}");
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sortingOrder = 5;
                sr.enabled = false;
                pool[i] = sr;
            }
        }

        private void LateUpdate()
        {
            var gm = GameManager.Instance;
            float width = ScreenSpace.ViewWidth(gm.ViewHeight, cam.aspect);
            field.EnsureCoverage(gm.ScrollX + width * 1.2f);

            CheckPickups(gm);
            RenderVisible(gm, width);
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

                if (e.Type == CoinField.CoinType.Speed)
                {
                    bird.AddSpeedBoost(speedBoostAmount);
                    burst.Burst(worldPos, new Color(0.25f, 0.65f, 1f), 12);
                    OnPickupPopup?.Invoke(worldPos, "SPEED!", new Color(0.25f, 0.65f, 1f));
                }
                else
                {
                    ScoreManager.Instance.AddScore(coinScore);
                    burst.Burst(worldPos, new Color(1f, 0.82f, 0.25f), 6);
                    OnPickupPopup?.Invoke(worldPos, "+3", new Color(1f, 0.82f, 0.25f));
                }
            }
        }

        private void RenderVisible(GameManager gm, float width)
        {
            int used = 0;
            var entries = field.Entries;

            for (int i = 0; i < entries.Count && used < pool.Length; i++)
            {
                var e = entries[i];
                if (e.Taken) continue;

                float canvasX = e.WorldX - gm.ScrollX;
                if (canvasX < -60f || canvasX > width + 60f) continue;

                float canvasY = gm.Ground.GroundY(e.WorldX) - e.Offset;
                var sr = pool[used++];
                sr.enabled = true;
                sr.sprite = e.Type == CoinField.CoinType.Speed ? speedSprite : coinSprite;
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
