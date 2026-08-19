using UnityEngine;

namespace FlyingChick
{
    // Keeps the bird inside the camera's view at all times, zooming out
    // exactly as much as its current height requires and smoothly zooming
    // back in as it descends. Camera transform.position.x never moves
    // (fixed per the project's scrolling-world architecture) -- but Y now
    // rises with the zoom-out (see skyBias below), and orthographicSize
    // changes as before. See ScreenSpace.LeftEdgeCanvasX/RightEdgeCanvasX
    // for how terrain/coin/cloud generation stays correct at any zoom
    // level; TerrainGenerator/BackgroundHillGenerator's fill-bottom edge
    // also reads the camera's current Y (see their bottomY comments).
    //
    // Replaces an earlier version capped at a fixed "max 15% zoom-out" (the
    // original spec's number). That cap could let a big enough launch carry
    // the bird off the top of the screen -- worse than just zooming out
    // further, so the cap is gone; only a generous safety ceiling remains
    // in case a physics bug ever produces a pathological height.
    //
    // Deliberately has NO special-case code for the Sky Flight orb buff
    // (Collectibles/SkyOrbSpawner.cs) -- an earlier version forced
    // orthographicSize to a fixed multiple while the buff was active, which
    // read as an artificial camera trick disconnected from the bird
    // ("줌 인/줌 아웃 동작이 이상해졌어" feedback: jarring snaps on
    // start/end). BirdPhysics.StartSkyFlight now sends the bird on an
    // actual high, floaty, physically-simulated arc instead, so this
    // class's normal height-based formula below zooms out for it exactly
    // the same way it would for any other very high flight -- one formula,
    // always driven by the bird's real position, so zoom in/out stays
    // smooth and "natural" (feedback's word) in every case, not just this
    // one pickup.
    public class CameraZoom : MonoBehaviour
    {
        [SerializeField] private float margin = 60f; // world-unit padding so the bird isn't glued to the edge, applied once past zoomStartFraction
        [SerializeField] private float heightMultiplier = 1.6f; // extra zoom-out headroom, scales with bird height beyond the start threshold (tuned up from "just barely fits" per feedback)
        [SerializeField, Range(0f, 1f)] private float zoomStartFraction = 0.85f; // bird height (as a fraction of baseOrthoSize) below which the camera stays flat at baseline -- feedback: zoom-out was triggering on nearly every hop, not just real high-altitude flight; raising this gate makes it kick in only once the bird is close to what used to be the view's own edge
        [SerializeField] private float maxOrthoSizeMultiplier = 4f; // safety ceiling, not a normal-gameplay limit -- also comfortably covers Sky Flight's peak height (~3x baseOrthoSize with the current tuning), see BirdPhysics.SkyFlightGravity
        [SerializeField, Range(0f, 1f)] private float skyBias = 0.7f; // fraction of the extra zoomed-out space that goes upward (sky) instead of symmetrically above+below (see LateUpdate comment) -- feedback: symmetric zoom read as "unnatural", hills should shrink and sky should open up like actually flying higher
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

            // At baseline the camera's vertical view spans
            // [-orthographicSize, +orthographicSize] around Y=0. Below
            // zoomStartFraction * baseOrthoSize the camera stays flat at
            // baseline -- no zoom at all -- so ordinary hops/launches don't
            // trigger it; only once the bird gets close to (and then past)
            // that threshold does the camera start pulling back, scaled by
            // heightMultiplier plus a flat margin once it does.
            float birdY = Mathf.Abs(bird.transform.position.y);
            float zoomStartHeight = baseOrthoSize * zoomStartFraction;
            float excess = birdY - zoomStartHeight;
            float neededHalfHeight = excess <= 0f ? baseOrthoSize : baseOrthoSize + excess * heightMultiplier + margin;
            float targetSize = Mathf.Min(neededHalfHeight, baseOrthoSize * maxOrthoSizeMultiplier);

            float smoothTime = targetSize > cam.orthographicSize ? zoomOutSmoothTime : zoomInSmoothTime;
            cam.orthographicSize = Mathf.SmoothDamp(cam.orthographicSize, targetSize, ref velocity, smoothTime);

            // A purely symmetric zoom-out (camera Y fixed at 0) splits every
            // extra unit of reveal 50/50 between sky above and ground/fill
            // below -- half of the "more zoomed out" feeling is spent on
            // empty space beneath the hills instead of open sky, which read
            // as unnatural. Shifting the camera upward by skyBias * (how far
            // past baseline we've zoomed) biases that reveal toward the sky
            // instead: hills end up occupying a visibly smaller strip near
            // the bottom of the frame rather than staying centered, which is
            // what actually flying higher looks like. skyBias=0 reproduces
            // the old centered behavior; skyBias=1 keeps the bottom edge
            // pinned at baseline and sends every extra unit upward.
            float extra = Mathf.Max(0f, cam.orthographicSize - baseOrthoSize);
            var pos = cam.transform.position;
            pos.y = extra * skyBias;
            cam.transform.position = pos;
        }
    }
}
