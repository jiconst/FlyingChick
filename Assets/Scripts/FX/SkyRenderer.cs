using UnityEngine;

namespace FlyingChick
{
    // Reference: draw()'s sky linear gradient (per-island palette top/bottom,
    // darkening toward night, plus a warm dusk band past dayTime 0.55).
    // Replaces the flat camera.backgroundColor lerp from M4 (FX/SkyTint.cs)
    // now that per-island palettes exist (IslandPalette.cs).
    //
    // A single large static quad with 2 vertex colors (top/bottom) -- sized
    // generously so it covers the screen at any zoom CameraZoom might reach,
    // so it never needs resizing, only its 2 colors change per frame.
    public class SkyRenderer : MonoBehaviour
    {
        [SerializeField] private float quadHalfSize = 2000f;

        private Camera cam;
        private DayCycle dayCycle;
        private GameManager gm;
        private Mesh mesh;

        private static readonly Color NightTop = ColorUtil.Hex("#20204a");
        private static readonly Color NightBottom = ColorUtil.Hex("#3a2a5a");
        private static readonly Color DuskBottom = ColorUtil.Hex("#ff8a5a");

        public void Configure(Camera camera, DayCycle dayCycleRef, GameManager gameManagerRef)
        {
            cam = camera;
            dayCycle = dayCycleRef;
            gm = gameManagerRef;
        }

        private void Awake()
        {
            var meshFilter = gameObject.AddComponent<MeshFilter>();
            var meshRenderer = gameObject.AddComponent<MeshRenderer>();
            meshRenderer.material = new Material(Shader.Find("Sprites/Default"));
            meshRenderer.sortingOrder = -20; // behind everything else in the scene

            mesh = new Mesh();
            mesh.vertices = new[]
            {
                new Vector3(-quadHalfSize, quadHalfSize, 0f),
                new Vector3(quadHalfSize, quadHalfSize, 0f),
                new Vector3(-quadHalfSize, -quadHalfSize, 0f),
                new Vector3(quadHalfSize, -quadHalfSize, 0f),
            };
            mesh.triangles = new[] { 0, 1, 2, 2, 1, 3 };
            mesh.colors = new Color[4];
            mesh.RecalculateBounds();
            meshFilter.mesh = mesh;
        }

        private void LateUpdate()
        {
            if (dayCycle == null || gm == null) return;

            var pal = IslandPalettes.ForIsland(gm.Island);
            float dayTime = dayCycle.DayTime;
            float night = Mathf.Pow(dayTime, 1.4f);

            Color top = Color.Lerp(pal.SkyTop, NightTop, night * 0.85f);
            Color bottom = Color.Lerp(pal.SkyBottom, NightBottom, night * 0.7f);
            if (dayTime > 0.55f)
            {
                float d = (dayTime - 0.55f) / 0.45f;
                bottom = Color.Lerp(bottom, DuskBottom, Mathf.Min(0.5f, d * 0.6f));
            }

            var colors = mesh.colors;
            colors[0] = top;
            colors[1] = top;
            colors[2] = bottom;
            colors[3] = bottom;
            mesh.colors = colors;
        }
    }
}
