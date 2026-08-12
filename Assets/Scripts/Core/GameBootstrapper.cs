using UnityEngine;

namespace FlyingChick
{
    // Drop this on an empty GameObject in an empty scene and press Play.
    // It builds the terrain, bird, camera rig, and game manager entirely at
    // runtime using generated placeholder art, so the core slide/glide feel
    // can be tested without any manual scene setup or imported art assets.
    public class GameBootstrapper : MonoBehaviour
    {
        [Header("Terrain")]
        [SerializeField] private int terrainSeed = 12345;
        [SerializeField] private float terrainTotalLength = 2000f;

        [Header("Bird")]
        [SerializeField] private float gravity = 20f;
        [SerializeField] private float diveAcceleration = 18f;
        [SerializeField] private float maxSpeed = 28f;
        [SerializeField] private float startSpeed = 8f;

        [Header("Day")]
        [SerializeField] private float dayDurationSeconds = 90f;

        private void Start()
        {
            var terrainGO = new GameObject("Terrain");
            var terrain = terrainGO.AddComponent<TerrainGenerator>();
            terrain.Configure(terrainSeed, terrainTotalLength);
            terrain.Generate();

            var birdGO = new GameObject("Bird");
            var spriteRenderer = birdGO.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = ProceduralSprite.CreateCircle(64, new Color(1f, 0.85f, 0.2f));
            spriteRenderer.sortingOrder = 10;
            birdGO.transform.localScale = Vector3.one * 0.5f;
            birdGO.transform.position = new Vector3(0f, terrain.HeightAt(0f) + 1f, 0f);

            var bird = birdGO.AddComponent<BirdController>();
            bird.Configure(terrain, gravity, diveAcceleration, maxSpeed, startSpeed);

            var cam = Camera.main;
            if (cam == null)
            {
                var camGO = new GameObject("Main Camera");
                camGO.tag = "MainCamera";
                cam = camGO.AddComponent<Camera>();
            }
            cam.backgroundColor = new Color(0.55f, 0.8f, 0.95f);
            cam.clearFlags = CameraClearFlags.SolidColor;

            var cameraRig = cam.gameObject.GetComponent<CameraRig>();
            if (cameraRig == null) cameraRig = cam.gameObject.AddComponent<CameraRig>();
            cameraRig.SetTarget(bird);

            var gameManager = gameObject.AddComponent<GameManager>();
            gameManager.Configure(bird, dayDurationSeconds);

            var hud = gameObject.AddComponent<SimpleHud>();
            hud.Bind(bird, gameManager);
        }
    }
}
