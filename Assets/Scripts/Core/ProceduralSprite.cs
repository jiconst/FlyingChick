using UnityEngine;

namespace HillyWings
{
    // Generic runtime shape-to-texture helpers for placeholder art (no
    // imported assets). Convention: pixelsPerUnit = 1, i.e. 1 texture pixel
    // == 1 world unit, so texture sizes can be reasoned about directly in
    // world-unit terms (matches the canvas-space radii used elsewhere).
    public static class ProceduralSprite
    {
        public static Sprite CreateCircle(int diameter, Color color) => CreateEllipse(diameter, diameter, color);

        public static Sprite CreateEllipse(int width, int height, Color color)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            float rx = width / 2f, ry = height / 2f;
            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float dx = (x + 0.5f - rx) / rx;
                    float dy = (y + 0.5f - ry) / ry;
                    pixels[y * width + x] = (dx * dx + dy * dy <= 1f) ? color : Color.clear;
                }
            }
            texture.SetPixels(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 1f);
        }
    }
}
