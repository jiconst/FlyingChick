using UnityEngine;

namespace FlyingChick
{
    // Rebuilds a single reusable ground mesh every frame by sampling
    // GroundSampler across the current visible width (reference: 6px steps,
    // redrawn every canvas frame). No chunk spawning/destroying.
    //
    // M1 scope: flat 3-band fill color only. Sky gradient, sun/moon, stars,
    // parallax background hill, grass tufts, and the full 10-palette
    // per-island swap are visual polish for a later milestone (spec stage 6).
    public class TerrainGenerator : MonoBehaviour
    {
        [SerializeField] private float sampleStep = 6f;
        [SerializeField] private float fillDepth = 200f;
        [SerializeField] private Color hillTopColor = new Color(0.95f, 0.76f, 0.29f);
        [SerializeField] private Color hillMidColor = new Color(0.91f, 0.55f, 0.16f);
        [SerializeField] private Color hillBottomColor = new Color(0.72f, 0.36f, 0.13f);

        private Mesh mesh;
        private Camera cam;

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

        private void LateUpdate()
        {
            RebuildMesh();
        }

        private void RebuildMesh()
        {
            var gm = GameManager.Instance;
            var ground = gm.Ground;
            float viewHeight = gm.ViewHeight;

            // Sample the actual on-screen range at the CURRENT zoom, not the
            // baseline [0, ViewWidth] -- otherwise CameraZoom's zoom-out
            // leaves blank gaps at the edges (see ScreenSpace comment).
            float leftEdge = ScreenSpace.LeftEdgeCanvasX(viewHeight, cam.aspect, cam.orthographicSize);
            float rightEdge = ScreenSpace.RightEdgeCanvasX(viewHeight, cam.aspect, cam.orthographicSize);
            int steps = Mathf.Max(2, Mathf.CeilToInt((rightEdge - leftEdge) / sampleStep) + 1);

            var vertices = new Vector3[steps * 2];
            var colors = new Color[steps * 2];

            float top = ground.BaseY - ground.MaxAmplitude;
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

                float t = Mathf.Clamp01((canvasY - top) / bandRange);
                colors[i * 2] = SampleBand(t);
                colors[i * 2 + 1] = hillBottomColor;
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

        private Color SampleBand(float t)
        {
            return t < 0.5f
                ? Color.Lerp(hillTopColor, hillMidColor, t / 0.5f)
                : Color.Lerp(hillMidColor, hillBottomColor, (t - 0.5f) / 0.5f);
        }
    }
}
