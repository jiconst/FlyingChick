using System.Collections.Generic;
using UnityEngine;

namespace HillyWings
{
    // Reference: drawHills()'s "grass tufts along crest" pass -- a short
    // diagonal stroke every 26 canvas units along the hill crest, skipped
    // wherever the slope is too steep for a tuft to read as "standing on
    // flat ground" (reference: abs(slope) < 0.5). Each tuft is a small
    // quad (2 triangles) built directly into a single reusable mesh rather
    // than a LineRenderer/GameObject per tuft, matching this project's
    // no-per-frame-Instantiate convention -- see TerrainGenerator's perf
    // comment for the same reasoning applied there.
    public class GrassTuftGenerator : MonoBehaviour
    {
        [SerializeField] private float spacing = 26f;
        [SerializeField] private float tuftHeight = 12f;
        [SerializeField] private float tuftWidth = 2.5f;
        [SerializeField] private float flatSlopeThreshold = 0.5f;

        private Mesh mesh;
        private Camera cam;
        private DayCycle dayCycle;

        private readonly List<Vector3> vertexBuffer = new List<Vector3>(256);
        private readonly List<Color> colorBuffer = new List<Color>(256);
        private readonly List<int> triangleBuffer = new List<int>(384);

        // Reference: lerpColor(pal.grass, '#2a2a4a', night*0.7).
        private static readonly Color NightGrass = ColorUtil.Hex("#2a2a4a");

        private void Awake()
        {
            cam = Camera.main;

            var meshFilter = gameObject.AddComponent<MeshFilter>();
            var meshRenderer = gameObject.AddComponent<MeshRenderer>();
            meshRenderer.material = new Material(Shader.Find("Sprites/Default"));
            meshRenderer.sortingOrder = 1; // just above the main terrain fill (default 0) -- tufts sit ON the crest

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
            Color grassColor = Color.Lerp(pal.Grass, NightGrass, night * 0.7f);

            float leftEdge = ScreenSpace.LeftEdgeCanvasX(viewHeight, cam.aspect, cam.orthographicSize);
            float rightEdge = ScreenSpace.RightEdgeCanvasX(viewHeight, cam.aspect, cam.orthographicSize);

            vertexBuffer.Clear();
            colorBuffer.Clear();
            triangleBuffer.Clear();

            float halfWidth = tuftWidth * 0.5f;
            for (float canvasX = leftEdge; canvasX <= rightEdge; canvasX += spacing)
            {
                float worldX = gm.ScrollX + canvasX;
                float slope = ground.GroundSlope(worldX);
                if (Mathf.Abs(slope) >= flatSlopeThreshold) continue;

                float canvasY = ground.GroundY(worldX);
                float tipCanvasX = canvasX + 2f;
                float tipCanvasY = canvasY - tuftHeight - Mathf.Sin(worldX) * 2f;

                Vector2 baseLocal = new Vector2(ScreenSpace.ToWorldX(canvasX, viewHeight, cam.aspect), ScreenSpace.ToWorldY(canvasY, viewHeight));
                Vector2 tipLocal = new Vector2(ScreenSpace.ToWorldX(tipCanvasX, viewHeight, cam.aspect), ScreenSpace.ToWorldY(tipCanvasY, viewHeight));

                Vector2 dir = (tipLocal - baseLocal).normalized;
                Vector2 perp = new Vector2(-dir.y, dir.x) * halfWidth;

                int baseIndex = vertexBuffer.Count;
                vertexBuffer.Add(new Vector3(baseLocal.x - perp.x, baseLocal.y - perp.y, 0f));
                vertexBuffer.Add(new Vector3(tipLocal.x - perp.x, tipLocal.y - perp.y, 0f));
                vertexBuffer.Add(new Vector3(baseLocal.x + perp.x, baseLocal.y + perp.y, 0f));
                vertexBuffer.Add(new Vector3(tipLocal.x + perp.x, tipLocal.y + perp.y, 0f));

                colorBuffer.Add(grassColor); colorBuffer.Add(grassColor); colorBuffer.Add(grassColor); colorBuffer.Add(grassColor);

                triangleBuffer.Add(baseIndex); triangleBuffer.Add(baseIndex + 1); triangleBuffer.Add(baseIndex + 2);
                triangleBuffer.Add(baseIndex + 2); triangleBuffer.Add(baseIndex + 1); triangleBuffer.Add(baseIndex + 3);
            }

            mesh.Clear();
            mesh.SetVertices(vertexBuffer);
            mesh.SetColors(colorBuffer);
            mesh.SetTriangles(triangleBuffer, 0);
            mesh.RecalculateBounds();
        }
    }
}
