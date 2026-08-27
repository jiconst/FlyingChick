using UnityEngine;

namespace HillyWings
{
    // GroundSampler/BirdPhysics are written in the same coordinate convention
    // as the validated hilly-wings.html reference: origin top-left, x grows
    // right, y grows DOWN ("canvas space"). Keeping that convention lets the
    // ported formulas stay byte-for-byte comparable to the reference. Unity's
    // world space is center-origin and y-up, so the conversion happens only
    // here, at the presentation boundary (mesh vertices, transform.position).
    public static class ScreenSpace
    {
        public static float ViewWidth(float viewHeight, float aspect) => viewHeight * aspect;

        public static float ToWorldX(float canvasX, float viewHeight, float aspect) =>
            canvasX - ViewWidth(viewHeight, aspect) * 0.5f;

        public static float ToWorldY(float canvasY, float viewHeight) =>
            viewHeight * 0.5f - canvasY;

        // The camera's X position is always fixed (CameraZoom, M7, never
        // moves transform.position.x), so zooming out reveals extra world
        // space SYMMETRICALLY around that fixed horizontal center -- not
        // just extra width to the right. These give the actual left/right
        // canvas-space bounds currently on screen at the camera's current
        // orthographicSize, vs. ToWorldX/ViewWidth above which always
        // assume the baseline (orthographicSize == viewHeight/2) zoom
        // level. Terrain/coin/cloud generation must sample this range, not
        // [0, ViewWidth], or the edges go blank while zoomed out.
        // (Vertically the camera's Y position is NOT fixed anymore -- see
        // CameraZoom's skyBias -- but nothing here needs a
        // TopEdge/BottomEdge equivalent since ToWorldY-based content is
        // placed once in world space and just becomes visible/hidden as the
        // camera's view moves, not resampled like terrain/coins/clouds are.)
        public static float LeftEdgeCanvasX(float viewHeight, float aspect, float orthographicSize)
        {
            float baseline = ViewWidth(viewHeight, aspect);
            float visible = orthographicSize * 2f * aspect;
            return (baseline - visible) * 0.5f;
        }

        public static float RightEdgeCanvasX(float viewHeight, float aspect, float orthographicSize)
        {
            float baseline = ViewWidth(viewHeight, aspect);
            float visible = orthographicSize * 2f * aspect;
            return (baseline + visible) * 0.5f;
        }
    }
}
