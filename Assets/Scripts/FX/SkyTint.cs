using UnityEngine;

namespace FlyingChick
{
    // Reference: sky gradient darkens toward night with a warm dusk band
    // past dayTime 0.55. Simplified to a single solid camera background
    // color lerp (day -> dusk -> night) -- full gradient/sun/moon/stars
    // rendering is a later visual pass (M6/M7).
    public class SkyTint : MonoBehaviour
    {
        [SerializeField] private Color dayColor = new Color(0.98f, 0.97f, 0.85f);
        [SerializeField] private Color duskColor = new Color(1f, 0.62f, 0.4f);
        [SerializeField] private Color nightColor = new Color(0.12f, 0.12f, 0.28f);

        private Camera cam;
        private DayCycle dayCycle;

        public void Configure(Camera camera, DayCycle dayCycleRef)
        {
            cam = camera;
            dayCycle = dayCycleRef;
        }

        private void Update()
        {
            if (cam == null || dayCycle == null) return;

            float t = dayCycle.DayTime;
            Color target = t < 0.55f
                ? Color.Lerp(dayColor, duskColor, t / 0.55f)
                : Color.Lerp(duskColor, nightColor, (t - 0.55f) / 0.45f);

            cam.backgroundColor = target;
        }
    }
}
