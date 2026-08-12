using UnityEngine;

namespace FlyingChick
{
    // Generates simple placeholder art at runtime so the prototype can be
    // played without any imported assets. Replace with real sprites later.
    public static class ProceduralSprite
    {
        public static Sprite CreateCircle(int diameter, Color color)
        {
            var texture = new Texture2D(diameter, diameter, TextureFormat.RGBA32, false);
            float radius = diameter / 2f;
            Vector2 center = new Vector2(radius, radius);

            for (int y = 0; y < diameter; y++)
            {
                for (int x = 0; x < diameter; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                    texture.SetPixel(x, y, dist <= radius ? color : Color.clear);
                }
            }
            texture.Apply();

            return Sprite.Create(texture, new Rect(0, 0, diameter, diameter), new Vector2(0.5f, 0.5f), diameter);
        }
    }
}
