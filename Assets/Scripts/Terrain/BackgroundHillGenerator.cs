using System.Collections.Generic;
using UnityEngine;

namespace FlyingChick
{
    // Reference: drawHills()'s "background echo hills (parallax, lighter)"
    // pass -- a single flat-color silhouette sampled from the SAME
    // GroundSampler but scrolled at parallaxFactor (0.55x) of the real
    // speed and pushed down by verticalOffset, giving a cheap sense of
    // depth behind the real, fully-scrolling foreground hill
    // (TerrainGenerator). Screen POSITION still uses the real (unscaled)
    // canvasX -- only the world X fed into GroundY() is slowed down, exactly
    // like the reference's `wx = scrollX*0.55 + x; lineTo(x, y)`.
    //
    // Same reusable-mesh, reusable-buffer, zoom-aware-bounds pattern as
    // TerrainGenerator (see that file's comments for why); kept as a
    // separate script/GameObject rather than folded into TerrainGenerator
    // since it's a distinct visual layer with its own sorting order
    // (one script = one responsibility).
    public class BackgroundHillGenerator : MonoBehaviour
    {
        [SerializeField] private float sampleStep = 12f; // coarser than the foreground -- it's meant to read as a soft, out-of-focus backdrop
        [SerializeField] private float verticalOffset = 70f; // reference: groundY(wx) + 70
        [SerializeField] private float fillDepth = 200f;
        [SerializeField] private float parallaxFactor = 0.55f;
        [SerializeField, Range(0f, 1f)] private float alpha = 0.5f; // reference: ctx.globalAlpha = 0.5

        private Mesh mesh;
        private Camera cam;
        private DayCycle dayCycle;

        private readonly List<Vector3> vertexBuffer = new List<Vector3>(128);
        private readonly List<Color> colorBuffer = new List<Color>(128);
        private readonly List<int> triangleBuffer = new List<int>(384);

        // Reference: lerpColor(pal.hillTop, '#20204a', night*0.7).
        private static readonly Color NightBackground = ColorUtil.Hex("#20204a");

        private void Awake()
        {
            cam = Camera.main;

            var meshFilter = gameObject.AddComponent<MeshFilter>();
            var meshRenderer = gameObject.AddComponent<MeshRenderer>();
            meshRenderer.material = new Material(Shader.Find("Sprites/Default"));
            meshRenderer.sortingOrder = -5; // behind the main terrain (default 0), in front of sky/sun/moon/stars (-15..-20, see SkyRenderer/SkyObjects)

            mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            meshFilter.mesh = mesh;
        }

        public void SetDayCycle(DayCycle dayCycleRef) => dayCycle = dayCycleRef;

        private void LateUpdate() => RebuildMesh();

        private void RebuildMesh()
        {
            var gm = GameManager.Instance;
            var ground = gm.Ground;
            float viewHeight = gm.ViewHeight;

            var pal = IslandPalettes.ForIsland(gm.Island);
            float night = dayCycle != null ? Mathf.Pow(dayCycle.DayTime, 1.4f) : 0f;
            Color fillColor = Color.Lerp(pal.HillTop, NightBackground, night * 0.7f);
            fillColor.a = alpha;

            float leftEdge = ScreenSpace.LeftEdgeCanvasX(viewHeight, cam.aspect, cam.orthographicSize);
            float rightEdge = ScreenSpace.RightEdgeCanvasX(viewHeight, cam.aspect, cam.orthographicSize);
            int steps = Mathf.Max(2, Mathf.CeilToInt((rightEdge - leftEdge) / sampleStep) + 1);

            // Same zoom-aware bottom edge as TerrainGenerator (see that
            // file's comment on the fixed-vs-current-orthographicSize bug).
            float bottomY = -cam.orthographicSize - fillDepth;

            vertexBuffer.Clear();
            colorBuffer.Clear();
            triangleBuffer.Clear();

            for (int i = 0; i < steps; i++)
            {
                float canvasX = leftEdge + i * sampleStep;
                float worldX = gm.ScrollX * parallaxFactor + canvasX;
                float canvasY = ground.GroundY(worldX) + verticalOffset;

                float localX = ScreenSpace.ToWorldX(canvasX, viewHeight, cam.aspect);
                float localY = ScreenSpace.ToWorldY(canvasY, viewHeight);

                vertexBuffer.Add(new Vector3(localX, localY, 0f));
                vertexBuffer.Add(new Vector3(localX, bottomY, 0f));
                colorBuffer.Add(fillColor);
                colorBuffer.Add(fillColor);
            }

            for (int i = 0; i < steps - 1; i++)
            {
                int topLeft = i * 2, bottomLeft = i * 2 + 1, topRight = (i + 1) * 2, bottomRight = (i + 1) * 2 + 1;
                triangleBuffer.Add(topLeft); triangleBuffer.Add(topRight); triangleBuffer.Add(bottomLeft);
                triangleBuffer.Add(bottomLeft); triangleBuffer.Add(topRight); triangleBuffer.Add(bottomRight);
            }

            mesh.Clear();
            mesh.SetVertices(vertexBuffer);
            mesh.SetColors(colorBuffer);
            mesh.SetTriangles(triangleBuffer, 0);
            mesh.RecalculateBounds();
        }
    }
}
