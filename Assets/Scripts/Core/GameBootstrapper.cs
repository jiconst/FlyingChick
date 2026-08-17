using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace FlyingChick
{
    // Wires the current milestone slice together at runtime so Play works
    // with zero manual scene setup. The camera's POSITION stays completely
    // fixed for the whole run -- the reference scrolls the world via
    // ScrollX, not the bird/camera through world space. Its orthographicSize
    // (zoom) can change though, see CameraZoom (M7).
    //
    // M1: terrain + bird physics + input.
    // M2 (added): Great Slide streak judging, Fever, island progression
    // bonus/speed-kick (island logic itself lives in GameManager), score HUD.
    // M3 (added): coins, speed coins, clouds, pickup particle bursts + popups.
    // M4 (added): Start/Playing/DayOver state machine, day-length timer +
    // sky tint, Start/DayOver screens, best-score save.
    // M5 (added): coin wallet, Nest Multiplier (per-run objectives ->
    // permanent starting-multiplier bonus), daily missions.
    // M6 (added): bird collection/egg gacha with perks, home-screen bird
    // selection, local Top-10 leaderboard + lifetime stats.
    // M7 (added so far): camera zoom-out while high above the ground; dive
    // dust + Fever star trail particles; per-island palette sky gradient +
    // sun/moon/stars; procedurally-synthesized SFX + ambient BGM; GC-alloc
    // cleanup pass; OnGUI -> UGUI/TextMeshPro UI (HUD/StartScreen/
    // DayOverScreen), which is why an EventSystem now gets created here too.
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

            // M7: one EventSystem for the whole scene so the UGUI canvases
            // (HUD/StartScreen/DayOverScreen) receive clicks/taps -- routed
            // through the New Input System (this project's Active Input
            // Handling has legacy UnityEngine.Input disabled, see
            // InputService), via InputSystemUIInputModule.AssignDefaultActions()
            // so no .inputactions asset needs to exist in the project.
            var eventSystemGO = new GameObject("EventSystem");
            eventSystemGO.AddComponent<EventSystem>();
            var uiInputModule = eventSystemGO.AddComponent<InputSystemUIInputModule>();
            uiInputModule.AssignDefaultActions();

            // SaveSystem first: CoinWallet/BirdCollection/Leaderboard read
            // SaveSystem.Instance in their own Awake/Configure, so it must
            // exist before them.
            var saveGO = new GameObject("SaveSystem");
            saveGO.AddComponent<SaveSystem>();

            int seed = terrainSeed != 0 ? terrainSeed : UnityEngine.Random.Range(1, int.MaxValue);
            var gmGO = new GameObject("GameManager");
            var gm = gmGO.AddComponent<GameManager>();
            gm.Configure(viewHeight, seed);

            var terrainGO = new GameObject("Terrain");
            var terrain = terrainGO.AddComponent<TerrainGenerator>();

            var birdGO = new GameObject("Bird");
            var bird = birdGO.AddComponent<BirdController>();
            var birdVisual = birdGO.AddComponent<BirdVisual>();
            bird.Configure(cam);

            var zoomGO = new GameObject("CameraZoom");
            var cameraZoom = zoomGO.AddComponent<CameraZoom>();
            cameraZoom.Configure(cam, bird);

            var feverGO = new GameObject("FeverSystem");
            var fever = feverGO.AddComponent<FeverSystem>();

            var trailParticles = birdGO.AddComponent<BirdTrailParticles>();
            trailParticles.Configure(bird, fever);

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

            var dayGO = new GameObject("DayCycle");
            var dayCycle = dayGO.AddComponent<DayCycle>();
            terrain.SetDayCycle(dayCycle);

            var skyGO = new GameObject("SkyRenderer");
            var sky = skyGO.AddComponent<SkyRenderer>();
            sky.Configure(cam, dayCycle, gm);

            var skyObjectsGO = new GameObject("SkyObjects");
            var skyObjects = skyObjectsGO.AddComponent<SkyObjects>();
            skyObjects.Configure(cam, dayCycle, viewHeight);

            var walletGO = new GameObject("CoinWallet");
            var wallet = walletGO.AddComponent<CoinWallet>();
            wallet.Configure(gm, score);

            var nestGO = new GameObject("NestMultiplier");
            var nest = nestGO.AddComponent<NestMultiplier>();
            nest.Configure(gm, slideJudge, fever, cloudSpawner, score);

            var dailyGO = new GameObject("DailyMissions");
            var dailyMissions = dailyGO.AddComponent<DailyMissions>();
            dailyMissions.Configure(wallet, slideJudge, fever, coinSpawner, cloudSpawner, gm);

            var collectionGO = new GameObject("BirdCollection");
            var collection = collectionGO.AddComponent<BirdCollection>();
            collection.Configure(wallet);

            bird.SetCollection(collection);
            birdVisual.SetCollection(collection);
            slideJudge.SetCollection(collection);
            fever.SetCollection(collection);
            coinSpawner.SetCollection(collection);

            var leaderboardGO = new GameObject("Leaderboard");
            var leaderboard = leaderboardGO.AddComponent<Leaderboard>();
            leaderboard.Configure(gm, score, slideJudge);

            var audioGO = new GameObject("AudioManager");
            var audio = audioGO.AddComponent<AudioManager>();
            audio.Configure(bird, slideJudge, fever, coinSpawner, cloudSpawner, gm, dayCycle);

            var hud = gameObject.AddComponent<HUD>();
            hud.Bind(bird, score, slideJudge, fever, gm);
            hud.BindCollectibles(coinSpawner, cloudSpawner, cam);
            hud.BindDayCycle(dayCycle);
            hud.BindMeta(nest);

            var startScreen = gameObject.AddComponent<StartScreen>();
            startScreen.Bind(wallet, dailyMissions, collection, leaderboard, audio);

            var dayOverScreen = gameObject.AddComponent<DayOverScreen>();
            dayOverScreen.Bind(score, slideJudge, cloudSpawner, fever, gm, wallet, nest, audio);
        }
    }
}
