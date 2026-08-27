using UnityEngine;

namespace HillyWings
{
    // Cute-chick look, built procedurally (no imported art -- spec explicitly
    // allows this for M1: circle/ellipse/triangle-beak drawing baked into a
    // runtime texture). Body/belly/crest/beak/eye are one static sprite;
    // only a separate wing sprite moves, matching the reference's per-frame
    // wing-only flap redraw.
    //
    // M6: body/belly/wing colors come from BirdCollection.SelectedBird
    // (ApplyBird), so switching birds on the Start screen actually changes
    // how the bird looks. Beak/crest/eye stay fixed -- shared chick features
    // across every bird in the pool, not part of BirdDefinition.
    public class BirdVisual : MonoBehaviour
    {
        [SerializeField] private int textureSize = 64;
        [SerializeField] private Color bodyColor = new Color(1f, 0.86f, 0.25f);
        [SerializeField] private Color bellyColor = new Color(1f, 0.97f, 0.82f);
        [SerializeField] private Color beakColor = new Color(1f, 0.55f, 0.15f);
        [SerializeField] private Color crestColor = new Color(0.95f, 0.35f, 0.2f);
        [SerializeField] private Color wingColor = new Color(0.93f, 0.72f, 0.15f);

        private BirdController controller;
        private BirdCollection collection;
        private Transform wingTransform;
        private SpriteRenderer bodyRenderer;
        private SpriteRenderer wingRenderer;

        private void Awake()
        {
            controller = GetComponent<BirdController>();

            bodyRenderer = gameObject.AddComponent<SpriteRenderer>();
            bodyRenderer.sprite = BuildBodySprite();
            bodyRenderer.sortingOrder = 10;

            var wingGO = new GameObject("Wing");
            wingGO.transform.SetParent(transform, false);
            wingTransform = wingGO.transform;
            wingTransform.localPosition = new Vector3(-3f, -1f, 0f);

            wingRenderer = wingGO.AddComponent<SpriteRenderer>();
            wingRenderer.sprite = ProceduralSprite.CreateEllipse(18, 12, wingColor);
            wingRenderer.sortingOrder = 9;
        }

        public void SetCollection(BirdCollection collectionRef)
        {
            collection = collectionRef;
            collection.OnSelectionChanged += HandleSelectionChanged;
            ApplyBird(collection.SelectedBird);
        }

        private void OnDestroy()
        {
            if (collection != null) collection.OnSelectionChanged -= HandleSelectionChanged;
        }

        private void HandleSelectionChanged() => ApplyBird(collection.SelectedBird);

        private void ApplyBird(BirdDefinition bird)
        {
            bodyColor = bird.BodyColor;
            bellyColor = bird.BellyColor;
            wingColor = bird.WingColor;

            bodyRenderer.sprite = BuildBodySprite();
            wingRenderer.sprite = ProceduralSprite.CreateEllipse(18, 12, wingColor);
        }

        private void Update()
        {
            bool onGround = controller != null && controller.OnGround;
            float amplitude = onGround ? 1f : 3f;
            float flap = Mathf.Sin(Time.time * 22f) * amplitude;
            wingTransform.localPosition = new Vector3(-3f, -1f + flap * 0.15f, 0f);
            wingTransform.localRotation = Quaternion.Euler(0f, 0f, -20f + flap * 4f);
        }

        private Sprite BuildBodySprite()
        {
            int size = textureSize;
            var pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.clear;

            float cx = size * 0.5f;
            float cy = size * 0.5f;
            float bodyRx = size * 0.34f, bodyRy = size * 0.30f;

            FillEllipse(pixels, size, cx, cy, bodyRx, bodyRy, bodyColor);

            float bellyCx = cx + size * 0.06f, bellyCy = cy - size * 0.10f;
            FillEllipse(pixels, size, bellyCx, bellyCy, size * 0.20f, size * 0.16f, bellyColor);

            for (int k = -1; k <= 1; k++)
            {
                float dotX = cx + k * size * 0.09f;
                float dotY = cy + bodyRy * 0.92f;
                FillEllipse(pixels, size, dotX, dotY, size * 0.045f, size * 0.045f, crestColor);
            }

            float beakBaseX = cx + bodyRx * 0.85f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float lx = x - beakBaseX;
                    float ly = y - cy;
                    float halfWidthAtLx = (size * 0.08f) * (1f - lx / (size * 0.16f));
                    if (lx >= 0f && lx <= size * 0.16f && Mathf.Abs(ly) <= halfWidthAtLx)
                        pixels[y * size + x] = beakColor;
                }
            }

            FillEllipse(pixels, size, cx + bodyRx * 0.35f, cy + bodyRy * 0.25f, size * 0.06f, size * 0.06f, new Color(0.13f, 0.13f, 0.15f));
            FillEllipse(pixels, size, cx + bodyRx * 0.30f, cy + bodyRy * 0.32f, size * 0.02f, size * 0.02f, Color.white);

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.SetPixels(pixels);
            tex.Apply();
            // pixelsPerUnit = 1: 1 texture px == 1 world unit (see ProceduralSprite).
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 1f);
        }

        private static void FillEllipse(Color[] pixels, int size, float cx, float cy, float rx, float ry, Color color)
        {
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f - cx) / rx;
                    float dy = (y + 0.5f - cy) / ry;
                    if (dx * dx + dy * dy <= 1f)
                        pixels[y * size + x] = color;
                }
            }
        }
    }
}
