using UnityEngine;

namespace FlyingChick
{
    // GroundSampler/BirdPhysics are written in the same coordinate convention
    // as the validated flying-chick.html reference: origin top-left, x grows
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
    }
}
