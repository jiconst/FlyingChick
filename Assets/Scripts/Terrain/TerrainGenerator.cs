using UnityEngine;

namespace FlyingChick
{
    // Rebuilds a single reusable ground mesh every frame by sampling
    // GroundSampler across the current visible width (reference: 6px steps,
    // redrawn every canvas frame). No chunk spawning/destroying.
    //
    // M7: colors now come from IslandPalette.ForIsland(GameManager.Island)
    // (10-palette swap, reference PALETTES) darkened toward night using the
    // same 4-stop gradient and lerp targets as the reference's drawHills().
    // Parallax background hill and grass tufts are still skipped.
    public class TerrainGenerator : MonoBehaviour
    {
        [SerializeField] private float sampleStep = 6f;
        [SerializeField] private float fillDepth = 200f;

        private Mesh mesh;
        private Camera cam;
        private DayCycle dayCycle;

        // Reference: night-darkening targets in drawHills()'s gradient.
        private static readonly Color NightTop = ColorUtil.Hex("#1a1a3a");
        private static readonly Color NightHill0 = ColorUtil.Hex("#1a1a3a");
        private static readonly Color NightHill1 = ColorUtil.Hex("#141430");
        private static readonly Color NightHill2 = ColorUtil.Hex("#0e0e28");

        private void Awake()
        {
            cam = Camera.main;

            var meshFilter = gameObject.AddComponent<MeshFilter>();
            var meshRenderer = gameObject.AddComponent<MeshRenderer>();
            var shader = Shader.Find("Sprites/Default");
            meshRenderer.material = new Material(shader);

            mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            meshFilter.mesh = mesh;
        }

        // Wired once DayCycle exists; night-darkening needs dayTime.
        public void SetDayCycle(DayCycle dayCycleRef) => dayCycle = dayCycleRef;

        private void LateUpdate()
        {
            RebuildMesh();
        }

        private void RebuildMesh()
        {
            var gm = GameManager.Instance;
            var ground = gm.Ground;
            float viewHeight = gm.ViewHeight;

            var pal = IslandPalettes.ForIsland(gm.Island);
            float night = dayCycle != null ? Mathf.Pow(dayCycle.DayTime, 1.4f) : 0f;

            Color top = Color.Lerp(pal.HillTop, NightTop, night * 0.75f);
            Color band0 = Color.Lerp(pal.Hill0, NightHill0, night * 0.75f);
            Color band1 = Color.Lerp(pal.Hill1, NightHill1, night * 0.78f);
            Color band2 = Color.Lerp(pal.Hill2, NightHill2, night * 0.8f);

            // Sample the actual on-screen range at the CURRENT zoom, not the
            // baseline [0, ViewWidth] -- otherwise CameraZoom's zoom-out
            // leaves blank gaps at the edges (see ScreenSpace comment).
            float leftEdge = ScreenSpace.LeftEdgeCanvasX(viewHeight, cam.aspect, cam.orthographicSize);
            float rightEdge = ScreenSpace.RightEdgeCanvasX(viewHeight, cam.aspect, cam.orthographicSize);
            int steps = Mathf.Max(2, Mathf.CeilToInt((rightEdge - leftEdge) / sampleStep) + 1);

            var vertices = new Vector3[steps * 2];
            var colors = new Color[steps * 2];

            float bandTop = ground.BaseY - ground.MaxAmplitude;
            float bandRange = ground.MaxAmplitude * 2f;

            for (int i = 0; i < steps; i++)
            {
                float canvasX = leftEdge + i * sampleStep;
                float worldX = gm.ScrollX + canvasX;
                float canvasY = ground.GroundY(worldX);

                float localX = ScreenSpace.ToWorldX(canvasX, viewHeight, cam.aspect);
                float localY = ScreenSpace.ToWorldY(canvasY, viewHeight);

                vertices[i * 2] = new Vector3(localX, localY, 0f);
                vertices[i * 2 + 1] = new Vector3(localX, -viewHeight * 0.5f - fillDepth, 0f);

                float t = Mathf.Clamp01((canvasY - bandTop) / bandRange);
                colors[i * 2] = SampleBand(t, top, band0, band1, band2);
                colors[i * 2 + 1] = band2;
            }

            var triangles = new int[(steps - 1) * 6];
            int t2 = 0;
            for (int i = 0; i < steps - 1; i++)
            {
                int topLeft = i * 2, bottomLeft = i * 2 + 1, topRight = (i + 1) * 2, bottomRight = (i + 1) * 2 + 1;
                triangles[t2++] = topLeft; triangles[t2++] = topRight; triangles[t2++] = bottomLeft;
                triangles[t2++] = bottomLeft; triangles[t2++] = topRight; triangles[t2++] = bottomRight;
            }

            mesh.Clear();
            mesh.vertices = vertices;
            mesh.colors = colors;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
        }

        // Reference: 4-stop CSS gradient (0, 0.25, 0.55, 1) inside drawHills().
        private Color SampleBand(float t, Color top, Color band0, Color band1, Color band2)
        {
            if (t < 0.25f) return Color.Lerp(top, band0, t / 0.25f);
            if (t < 0.55f) return Color.Lerp(band0, band1, (t - 0.25f) / 0.30f);
            return Color.Lerp(band1, band2, (t - 0.55f) / 0.45f);
        }
    }
}
