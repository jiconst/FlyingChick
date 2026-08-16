using UnityEngine;

namespace FlyingChick
{
    // Keeps the bird inside the camera's view at all times, zooming out
    // exactly as much as its current height requires and smoothly zooming
    // back in as it descends. Camera transform.position never moves (fixed
    // per the project's scrolling-world architecture) -- only
    // orthographicSize changes. See ScreenSpace.LeftEdgeCanvasX/
    // RightEdgeCanvasX for how terrain/coin/cloud generation stays correct
    // at any zoom level.
    //
    // Replaces an earlier version capped at a fixed "max 15% zoom-out" (the
    // original spec's number). That cap could let a big enough launch carry
    // the bird off the top of the screen -- worse than just zooming out
    // further, so the cap is gone; only a generous safety ceiling remains
    // in case a physics bug ever produces a pathological height.
    public class CameraZoom : MonoBehaviour
    {
        [SerializeField] private float margin = 60f; // world-unit padding so the bird isn't glued to the edge
        [SerializeField] private float maxOrthoSizeMultiplier = 4f; // safety ceiling, not a normal-gameplay limit
        [SerializeField] private float zoomOutSmoothTime = 0.12f; // fast enough to keep up with a launch
        [SerializeField] private float zoomInSmoothTime = 0.35f; // slower/gentler when returning to baseline

        private Camera cam;
        private BirdController bird;
        private float baseOrthoSize;
        private float velocity;

        public void Configure(Camera camera, BirdController birdRef)
        {
            cam = camera;
            bird = birdRef;
            baseOrthoSize = camera.orthographicSize;
        }

        private void LateUpdate()
        {
            if (cam == null || bird == null) return;

            // Camera sits at world Y = 0, so its current vertical view spans
            // [-orthographicSize, +orthographicSize]. Figure out how big
            // that half-height needs to be for the bird's actual Y (plus
            // margin) to fit inside it.
            float birdY = bird.transform.position.y;
            float neededHalfHeight = Mathf.Max(baseOrthoSize, Mathf.Abs(birdY) + margin);
            float targetSize = Mathf.Min(neededHalfHeight, baseOrthoSize * maxOrthoSizeMultiplier);

            float smoothTime = targetSize > cam.orthographicSize ? zoomOutSmoothTime : zoomInSmoothTime;
            cam.orthographicSize = Mathf.SmoothDamp(cam.orthographicSize, targetSize, ref velocity, smoothTime);
        }
    }
}
