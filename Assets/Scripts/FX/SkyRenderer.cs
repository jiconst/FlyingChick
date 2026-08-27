using UnityEngine;

namespace HillyWings
{
    // Reference: draw()'s sky linear gradient (per-island palette top/bottom,
    // darkening toward night, plus a warm dusk band past dayTime 0.55).
    // Replaces the flat camera.backgroundColor lerp from M4 (FX/SkyTint.cs)
    // now that per-island palettes exist (IslandPalette.cs).
    //
    // A quad with 2 vertex colors (top/bottom) that's resized every frame to
    // match the camera's ACTUAL current bounds (plus a safety margin) rather
    // than a single fixed size picked in advance. An earlier version used a
    // static quadHalfSize=2000 (later 3000) sized by hand for CameraZoom's
    // worst-case vertical reach only -- that still broke on the sides during
    // Sky Flight ("좌측, 우측의 배경 색이 깨져 보였어" feedback): a fixed SQUARE
    // can't also cover the HORIZONTAL extent, which is
    // orthographicSize*aspect and grows independently of the vertical case
    // on any wide-ish aspect ratio. Matching the real camera bounds every
    // frame (like TerrainGenerator already does for terrain width) makes
    // this correct at any zoom, any aspect ratio, and any future CameraZoom
    // tuning, instead of re-guessing a "big enough" number each time
    // something changes.
    public class SkyRenderer : MonoBehaviour
    {
        [SerializeField] private float boundsMargin = 1.15f; // extra size over the camera's exact current bounds -- absorbs the 1-frame lag possible if this LateUpdate runs before CameraZoom's in a given frame (Unity doesn't guarantee cross-script LateUpdate order)

        private Camera cam;
        private DayCycle dayCycle;
        private GameManager gm;
        private Mesh mesh;
        private Vector3[] vertices;

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
            // Placeholder extent until the first LateUpdate (after Configure
            // runs) fits it to the real camera -- never actually rendered at
            // this size since Configure/the first LateUpdate happen before
            // the first frame draws.
            vertices = new[]
            {
                new Vector3(-1f, 1f, 0f),
                new Vector3(1f, 1f, 0f),
                new Vector3(-1f, -1f, 0f),
                new Vector3(1f, -1f, 0f),
            };
            mesh.vertices = vertices;
            mesh.triangles = new[] { 0, 1, 2, 2, 1, 3 };
            mesh.colors = new Color[4];
            mesh.RecalculateBounds();
            meshFilter.mesh = mesh;
        }

        private void LateUpdate()
        {
            if (dayCycle == null || gm == null || cam == null) return;

            UpdateQuadBounds();

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

        // Reads the camera's CURRENT transform.position/orthographicSize
        // directly (not CameraZoom's internal skyBias formula) so this stays
        // correct regardless of how or why the camera moved/zoomed --
        // CameraZoom is just today's only mover.
        private void UpdateQuadBounds()
        {
            float halfWidth = cam.orthographicSize * cam.aspect * boundsMargin;
            float halfHeight = cam.orthographicSize * boundsMargin;
            float centerX = cam.transform.position.x;
            float centerY = cam.transform.position.y;

            vertices[0] = new Vector3(centerX - halfWidth, centerY + halfHeight, 0f);
            vertices[1] = new Vector3(centerX + halfWidth, centerY + halfHeight, 0f);
            vertices[2] = new Vector3(centerX - halfWidth, centerY - halfHeight, 0f);
            vertices[3] = new Vector3(centerX + halfWidth, centerY - halfHeight, 0f);
            mesh.vertices = vertices;
            mesh.RecalculateBounds();
        }
    }
}
