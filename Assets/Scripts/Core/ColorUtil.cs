using UnityEngine;

namespace FlyingChick
{
    // Tiny wrapper so palette tables (IslandPalette, sky/night targets) can
    // be written as the same hex strings as the flying-chick.html reference
    // instead of hand-converted floats -- easier to eyeball-verify against
    // the source and to tweak later.
    public static class ColorUtil
    {
        public static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var color);
            return color;
        }
    }
}
