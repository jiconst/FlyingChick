using UnityEngine;

namespace FlyingChick
{
    // Wires the current milestone slice together at runtime so Play works
    // with zero manual scene setup. The camera stays completely fixed for
    // the whole run -- the reference scrolls the world via ScrollX, not the
    // bird/camera through world space.
    //
    // M1: terrain + bird physics + input.
    // M2 (added): Great Slide streak judging, Fever, island progression
    // bonus/speed-kick (island logic itself lives in GameManager), score HUD.
    // M3 (added): coins, speed coins, clouds, pickup particle bursts + popups.
    public class GameBootstrapper : MonoBehaviour
    {
        [SerializeField] private float viewHeight = 720f;
        [SerializeField] private int terrainSeed = 0; // 0 = random each run

        private void Start()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var camGO = new GameObject("Main Camera");
                camGO.tag = "MainCamera";
                cam = camGO.AddComponent<Camera>();
            }
            cam.orthographic = true;
            cam.orthographicSize = viewHeight * 0.5f;
            cam.transform.position = new Vector3(0f, 0f, -10f);
            cam.backgroundColor = new Color(0.98f, 0.97f, 0.85f);
            cam.clearFlags = CameraClearFlags.SolidColor;

            int seed = terrainSeed != 0 ? terrainSeed : UnityEngine.Random.Range(1, int.MaxValue);
            var gmGO = new GameObject("GameManager");
            var gm = gmGO.AddComponent<GameManager>();
            gm.Configure(viewHeight, seed);

            var terrainGO = new GameObject("Terrain");
            terrainGO.AddComponent<TerrainGenerator>();

            var birdGO = new GameObject("Bird");
            var bird = birdGO.AddComponent<BirdController>();
            birdGO.AddComponent<BirdVisual>();
            bird.Configure(cam);

            var feverGO = new GameObject("FeverSystem");
            var fever = feverGO.AddComponent<FeverSystem>();

            var scoreGO = new GameObject("ScoreManager");
            var score = scoreGO.AddComponent<ScoreManager>();
            score.Configure(fever);

            var judgeGO = new GameObject("SlideJudge");
            var slideJudge = judgeGO.AddComponent<SlideJudge>();
            slideJudge.Configure(bird, fever);

            var burstGO = new GameObject("PickupBurst");
            var burst = burstGO.AddComponent<PickupBurst>();

            var coinGO = new GameObject("CoinSpawner");
            var coinSpawner = coinGO.AddComponent<CoinSpawner>();
            coinSpawner.Configure(bird, cam, burst, seed + 1);

            var cloudGO = new GameObject("CloudSpawner");
            var cloudSpawner = cloudGO.AddComponent<CloudSpawner>();
            cloudSpawner.Configure(bird, cam, burst, seed + 2);

            var hud = gameObject.AddComponent<HUD>();
            hud.Bind(bird, score, slideJudge, fever, gm);
            hud.BindCollectibles(coinSpawner, cloudSpawner, cam);
        }
    }
}
