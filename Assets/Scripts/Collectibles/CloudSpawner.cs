using System;
using UnityEngine;

namespace FlyingChick
{
    // Pooled cloud visuals + cloud-touch detection. Reference: touch only
    // counts while airborne, worth 20*(mult/10), and each cloud can only be
    // touched once. Each pooled cloud is a small parent GameObject with a
    // few overlapping circle-blob children (built once, reused).
    public class CloudSpawner : MonoBehaviour
    {
        [SerializeField] private int poolSize = 24;
        [SerializeField] private float touchRadiusBase = 42f; // multiplied by cloud scale
        [SerializeField] private float baseCloudScore = 20f;

        public event Action<Vector3, string, Color> OnPickupPopup;

        private CloudField field;
        private BirdController bird;
        private Camera cam;
        private PickupBurst burst;

        private Transform[] pool;
        private Sprite blobSprite;

        public void Configure(BirdController birdRef, Camera camera, PickupBurst burstRef, int seed)
        {
            bird = birdRef;
            cam = camera;
            burst = burstRef;
            var gm = GameManager.Instance;
            field = new CloudField(seed, gm.ViewHeight, gm.ScrollX);
        }

        private void Awake()
        {
            blobSprite = ProceduralSprite.CreateCircle(40, new Color(1f, 1f, 1f, 0.92f));

            pool = new Transform[poolSize];
            for (int i = 0; i < poolSize; i++)
            {
                var go = new GameObject($"Cloud_{i}");
                go.transform.SetParent(transform, false);
                AddBlob(go.transform, new Vector2(-14f, 0f), 0.9f);
                AddBlob(go.transform, new Vector2(0f, 6f), 1.1f);
                AddBlob(go.transform, new Vector2(14f, 0f), 0.85f);
                AddBlob(go.transform, new Vector2(0f, -3f), 1.0f);
                go.SetActive(false);
                pool[i] = go.transform;
            }
        }

        private void AddBlob(Transform parent, Vector2 localOffset, float scale)
        {
            var go = new GameObject("Blob");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(localOffset.x, localOffset.y, 0f);
            go.transform.localScale = Vector3.one * scale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = blobSprite;
            sr.sortingOrder = 3;
        }

        private void LateUpdate()
        {
            var gm = GameManager.Instance;
            float width = ScreenSpace.ViewWidth(gm.ViewHeight, cam.aspect);
            field.EnsureCoverage(gm.ScrollX + width * 1.5f);

            CheckTouches(gm);
            RenderVisible(gm, width);
        }

        private void CheckTouches(GameManager gm)
        {
            if (!bird.Airborne) return;

            var entries = field.Entries;
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e.Touched) continue;

                float canvasX = e.WorldX - gm.ScrollX;
                float dx = canvasX - bird.CanvasX;
                float dy = e.CanvasY - bird.CanvasY;
                float r = bird.Radius + touchRadiusBase * e.Scale;
                if (dx * dx + dy * dy >= r * r) continue;

                field.MarkTouched(i);
                float gain = baseCloudScore * (GameManager.Instance.Multiplier / 10f);
                ScoreManager.Instance.AddScore(gain);

                Vector3 worldPos = ToWorldPos(gm, canvasX, e.CanvasY);
                burst.Burst(worldPos, Color.white, 20);
                OnPickupPopup?.Invoke(worldPos, $"CLOUD TOUCH! +{Mathf.RoundToInt(gain):0}", new Color(0.6f, 0.4f, 1f));
            }
        }

        private void RenderVisible(GameManager gm, float width)
        {
            int used = 0;
            var entries = field.Entries;

            for (int i = 0; i < entries.Count && used < pool.Length; i++)
            {
                var e = entries[i];
                float canvasX = e.WorldX - gm.ScrollX;
                if (canvasX < -140f || canvasX > width + 140f) continue;

                var t = pool[used++];
                t.gameObject.SetActive(true);
                t.position = ToWorldPos(gm, canvasX, e.CanvasY);
                t.localScale = Vector3.one * e.Scale;
            }

            for (int i = used; i < pool.Length; i++) pool[i].gameObject.SetActive(false);
        }

        private Vector3 ToWorldPos(GameManager gm, float canvasX, float canvasY)
        {
            float localX = ScreenSpace.ToWorldX(canvasX, gm.ViewHeight, cam.aspect);
            float localY = ScreenSpace.ToWorldY(canvasY, gm.ViewHeight);
            return new Vector3(localX, localY, 0f);
        }
    }
}
