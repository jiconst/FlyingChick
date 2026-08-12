using UnityEngine;

namespace FlyingChick
{
    // Procedurally builds Tiny-Wings-style undulating hill terrain.
    // The shape is a pure function of x (sum of sine waves), so it doubles as
    // both the visual mesh and the analytic ground-height query used by
    // BirdController. There is no physics collider involved -- the function
    // itself is the single source of truth for where the ground is.
    public class TerrainGenerator : MonoBehaviour
    {
        [Header("Shape")]
        [SerializeField] private float baseHeight = 0f;
        [SerializeField] private float[] amplitudes = { 2.5f, 1.1f, 0.4f };
        [SerializeField] private float[] frequencies = { 0.12f, 0.32f, 0.9f };
        [SerializeField] private int seed = 12345;

        [Header("Build range")]
        [SerializeField] private float totalLength = 2000f;
        [SerializeField] private float sampleSpacing = 0.2f;
        [SerializeField] private float fillDepth = 12f;

        [Header("Look")]
        [SerializeField] private Color groundColor = new Color(0.35f, 0.75f, 0.35f);
        [SerializeField] private string shaderName = "Sprites/Default";

        private float[] phases;

        public float TotalLength => totalLength;

        // Lets code (GameBootstrapper) override the Inspector defaults before
        // Generate() builds the mesh. Amplitudes/frequencies are left as
        // Inspector-only tuning knobs to keep this method's surface small.
        public void Configure(int newSeed, float newTotalLength)
        {
            seed = newSeed;
            totalLength = newTotalLength;
        }

        public void Generate()
        {
            var rng = new System.Random(seed);
            phases = new float[amplitudes.Length];
            for (int i = 0; i < phases.Length; i++)
                phases[i] = (float)(rng.NextDouble() * Mathf.PI * 2);

            BuildMesh();
        }

        public float HeightAt(float x)
        {
            x = Mathf.Clamp(x, 0f, totalLength);
            float h = baseHeight;
            for (int i = 0; i < amplitudes.Length; i++)
                h += amplitudes[i] * Mathf.Sin(x * frequencies[i] + phases[i]);
            return h;
        }

        // Normalized tangent direction of the slope at x, facing +x (forward travel).
        public Vector2 TangentAt(float x)
        {
            x = Mathf.Clamp(x, 0f, totalLength);
            float slope = 0f;
            for (int i = 0; i < amplitudes.Length; i++)
                slope += amplitudes[i] * frequencies[i] * Mathf.Cos(x * frequencies[i] + phases[i]);
            return new Vector2(1f, slope).normalized;
        }

        private void BuildMesh()
        {
            int sampleCount = Mathf.Max(2, Mathf.CeilToInt(totalLength / sampleSpacing) + 1);
            var vertices = new Vector3[sampleCount * 2];
            var triangles = new int[(sampleCount - 1) * 6];

            for (int i = 0; i < sampleCount; i++)
            {
                float x = i * sampleSpacing;
                float y = HeightAt(x);
                vertices[i * 2] = new Vector3(x, y, 0f);
                vertices[i * 2 + 1] = new Vector3(x, baseHeight - fillDepth, 0f);
            }

            int t = 0;
            for (int i = 0; i < sampleCount - 1; i++)
            {
                int topLeft = i * 2;
                int bottomLeft = i * 2 + 1;
                int topRight = (i + 1) * 2;
                int bottomRight = (i + 1) * 2 + 1;

                triangles[t++] = topLeft;
                triangles[t++] = topRight;
                triangles[t++] = bottomLeft;

                triangles[t++] = bottomLeft;
                triangles[t++] = topRight;
                triangles[t++] = bottomRight;
            }

            var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();

            var filter = gameObject.AddComponent<MeshFilter>();
            filter.mesh = mesh;

            var meshRenderer = gameObject.AddComponent<MeshRenderer>();
            var shader = Shader.Find(shaderName) != null ? Shader.Find(shaderName) : Shader.Find("Unlit/Color");
            var material = new Material(shader) { color = groundColor };
            meshRenderer.material = material;
        }
    }
}
