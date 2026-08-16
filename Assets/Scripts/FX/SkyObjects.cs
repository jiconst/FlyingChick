using UnityEngine;

namespace FlyingChick
{
    // Reference: sun/moon descending disc (sunY = H*0.14 + dayTime*H*0.62,
    // fixed sunX = W*0.16, swaps to moon past dayTime 0.8) and 40 twinkling
    // stars that fade in past night > 0.2. Both are positioned in canvas
    // space WITHOUT adding ScrollX -- the reference draws them at a fixed
    // canvas position every frame regardless of world scroll, so on our
    // fixed camera they read as screen-fixed background decoration.
    public class SkyObjects : MonoBehaviour
    {
        private const int StarCount = 40;

        private Camera cam;
        private DayCycle dayCycle;
        private float viewHeight;

        private Transform sun;
        private Transform moon;
        private SpriteRenderer[] stars;

        public void Configure(Camera camera, DayCycle dayCycleRef, float viewHeightValue)
        {
            cam = camera;
            dayCycle = dayCycleRef;
            viewHeight = viewHeightValue;
        }

        private void Awake()
        {
            var sunGO = new GameObject("Sun");
            sunGO.transform.SetParent(transform, false);
            var sunRenderer = sunGO.AddComponent<SpriteRenderer>();
            sunRenderer.sprite = ProceduralSprite.CreateCircle(56, new Color(1f, 0.94f, 0.7f));
            sunRenderer.sortingOrder = -15;
            sun = sunGO.transform;

            var moonGO = new GameObject("Moon");
            moonGO.transform.SetParent(transform, false);
            var moonRenderer = moonGO.AddComponent<SpriteRenderer>();
            moonRenderer.sprite = ProceduralSprite.CreateCircle(46, new Color(0.94f, 0.94f, 0.85f));
            moonRenderer.sortingOrder = -15;
            moon = moonGO.transform;

            var starSprite = ProceduralSprite.CreateCircle(6, Color.white);
            stars = new SpriteRenderer[StarCount];
            for (int i = 0; i < StarCount; i++)
            {
                var go = new GameObject($"Star_{i}");
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = starSprite;
                sr.sortingOrder = -16;
                stars[i] = sr;
            }
        }

        private void LateUpdate()
        {
            if (cam == null || dayCycle == null) return;

            float dayTime = dayCycle.DayTime;
            float night = Mathf.Pow(dayTime, 1.4f);
            float aspect = cam.aspect;
            float width = ScreenSpace.ViewWidth(viewHeight, aspect);

            float canvasY = viewHeight * 0.14f + dayTime * (viewHeight * 0.62f);
            float canvasX = width * 0.16f;
            Vector3 pos = ToWorld(canvasX, canvasY, aspect);

            bool isMoon = dayTime > 0.8f;
            sun.gameObject.SetActive(!isMoon);
            moon.gameObject.SetActive(isMoon);
            (isMoon ? moon : sun).position = pos;

            float starAlpha = Mathf.Clamp01((night - 0.2f) * 1.2f);
            bool starsVisible = starAlpha > 0.01f;
            for (int i = 0; i < stars.Length; i++)
            {
                stars[i].gameObject.SetActive(starsVisible);
                if (!starsVisible) continue;

                float sx = (i * 97.3f) % width;
                float sy = (i * 53.7f) % (viewHeight * 0.5f);
                stars[i].transform.position = ToWorld(sx, sy, aspect);

                float twinkle = 0.5f + 0.5f * Mathf.Sin(Time.time * 3f + i);
                var c = Color.white;
                c.a = starAlpha * twinkle;
                stars[i].color = c;
            }
        }

        private Vector3 ToWorld(float canvasX, float canvasY, float aspect)
        {
            float localX = ScreenSpace.ToWorldX(canvasX, viewHeight, aspect);
            float localY = ScreenSpace.ToWorldY(canvasY, viewHeight);
            return new Vector3(localX, localY, 0f);
        }
    }
}
