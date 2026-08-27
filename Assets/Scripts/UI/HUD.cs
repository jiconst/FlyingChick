using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HillyWings
{
    // M2 scope was a functional score/island/fever/streak readout via
    // OnGUI. M7: converted to a runtime-built UGUI/TextMeshPro hierarchy
    // (Canvas + TMP_Text + Image), per explicit choice among OnGUI
    // replacement options (see CLAUDE.md). Still fully code-driven -- built
    // once in Bind(), never touched in the Editor. Toasts and pickup toasts
    // use small fixed-size pools of pre-built UI elements (enabled/disabled
    // + text/color/position updated per frame) instead of
    // Instantiate/Destroy, matching this project's existing pooling
    // convention (see Collectibles/CoinSpawner etc).
    //
    // Korean text ("STREAK"/미션 설명 등) needs a font asset with Hangul
    // coverage -- see UIFontProvider for how that's built at runtime
    // without a manual Editor import step. If Korean glyphs show as tofu
    // boxes on first playtest, that file is the place to look.
    public class HUD : MonoBehaviour
    {
        private struct Toast
        {
            public string Text;
            public Color Color;
            public float Duration;
            public float TimeLeft;
        }

        private struct PositionedToast
        {
            public string Text;
            public Color Color;
            public Vector3 WorldPos;
            public float Duration;
            public float TimeLeft;
        }

        private const int ToastPoolSize = 6;
        private const int PickupToastPoolSize = 10;
        private const int NestLinePoolSize = 5;
        private const int StreakDotCount = 3;

        [SerializeField] private float toastDuration = 1.1f;
        [SerializeField] private float feverToastDuration = 1.6f;
        [SerializeField] private float pickupToastDuration = 0.8f;

        private BirdController bird;
        private ScoreManager score;
        private SlideJudge slideJudge;
        private FeverSystem fever;
        private GameManager gameManager;
        private DayCycle dayCycle;
        private Camera cam;
        private CoinSpawner coinSpawner;
        private CloudSpawner cloudSpawner;
        private SkyOrbSpawner skyOrbSpawner;
        private NestMultiplier nest;
        private PlayerProfile profile;
        private AuthService auth;
        private PlayerLevel playerLevel;

        private readonly List<Toast> toasts = new List<Toast>();
        private readonly List<PositionedToast> pickupToasts = new List<PositionedToast>();

        private GameObject root;

        private RectTransform scorePanelRect;
        private TextMeshProUGUI nicknameText;
        private TextMeshProUGUI scoreLabelText;
        private TextMeshProUGUI scoreText;

        // "레벨 값" 요청 -- 기존엔 우측 상단에 "Island N · Mx" 한 줄짜리
        // 텍스트만 있었음. Score 박스와 짝을 이루도록 라벨+큰 숫자+배수를
        // 세로로 쌓은 전용 박스로 승격.
        private RectTransform levelPanelRect;
        private TextMeshProUGUI levelLabelText;
        private TextMeshProUGUI levelText;
        private TextMeshProUGUI levelMultText;

        // 우측 상단 픽업 개수 박스 -- 골드 코인/스피드(파랑) 코인/Sky Flight(초록)
        // 오브 각각 몇 개 먹었는지. 런 시작 시 0으로 리셋(HandleRunStart).
        private RectTransform pickupCountsPanelRect;
        private TextMeshProUGUI goldCountText;
        private TextMeshProUGUI blueCountText;
        private TextMeshProUGUI greenCountText;
        private int goldPickupCount;
        private int bluePickupCount;
        private int greenPickupCount;

        // "다음 섬까지 얼마나 남았는지 알 수가 없다" 피드백으로 신설 -- LEVEL
        // 박스(levelText) 바로 아래, GameManager.IslandProgress(0~1)를 채움으로
        // 보여주는 진행 바. 해/밤 바(dayTrackFill)와 같은 트랙+필 패턴.
        private RectTransform islandProgressTrackRect;
        private Image islandProgressFill;
        // "레벨업 기준"(다음 섬까지 남은 거리)을 숫자로도 보여줌 -- 진행 바
        // 바로 아래. GameManager.IslandRemainingDistance가 0이 되는 순간이
        // 곧 레벨업(Island 증가) 조건이라, 이 숫자가 그 기준 자체.
        private TextMeshProUGUI islandRemainingText;

        // 화면 중앙 하단에 표시하는 "DIST: ...  AIRTIME: ..." -- Score(좌측
        // 상단)와는 별개의 값으로, GameManager.ScrollX(월드 스크롤 거리)와
        // BirdController.TotalAirborneTime(누적 체공 시간)을 한 줄로 보여줌.
        private TextMeshProUGUI distanceText;
        private RectTransform distanceRect;

        private RectTransform dayTrackRect;
        private Image dayTrackFill;
        private RectTransform sunIconRect;

        private RectTransform streakPanelRect;
        private TextMeshProUGUI streakLabel;
        private readonly Image[] streakDots = new Image[StreakDotCount];

        private RectTransform nestPanelRect;
        private Image nestPanelBg;
        private TextMeshProUGUI nestHeaderText;
        private readonly TextMeshProUGUI[] nestLines = new TextMeshProUGUI[NestLinePoolSize];

        private RectTransform feverRect;
        private TextMeshProUGUI feverText;

        private readonly TextMeshProUGUI[] toastPool = new TextMeshProUGUI[ToastPoolSize];
        private readonly TextMeshProUGUI[] pickupToastPool = new TextMeshProUGUI[PickupToastPoolSize];

        private RectTransform debugPanelRect;
        private TextMeshProUGUI dbgStateText;
        private TextMeshProUGUI dbgHeightText;

        public void Bind(BirdController birdRef, ScoreManager scoreRef, SlideJudge slideJudgeRef, FeverSystem feverRef, GameManager gameManagerRef)
        {
            bird = birdRef;
            score = scoreRef;
            slideJudge = slideJudgeRef;
            fever = feverRef;
            gameManager = gameManagerRef;

            slideJudge.OnGreatSlide += HandleGreatSlide;
            slideJudge.OnStreakBroken += HandleStreakBroken;
            fever.OnFeverStart += HandleFeverStart;
            gameManager.OnRunStart += HandleRunStart;
            gameManager.OnIslandAdvanced += HandleIslandAdvanced;

            BuildUI();
        }

        public void BindDayCycle(DayCycle dayCycleRef) => dayCycle = dayCycleRef;
        public void BindMeta(NestMultiplier nestRef) => nest = nestRef;

        // "나의 레벨 값과 닉네임 표시도 같이 추가해줬으면" 요청 -- 로그인
        // 상태면 서버 닉네임, 아니면 로컬 PlayerProfile 닉네임을 씀(StartScreen의
        // 기록 탭에서 이미 쓰던 것과 같은 우선순위).
        // "레벨업은 누적 기록의 합산" -- HUD LEVEL 박스가 런마다 리셋되는 Island가
        // 아니라 평생 누적 거리 기반 PlayerLevel을 보여주도록 연결.
        public void BindLevel(PlayerLevel playerLevelRef) => playerLevel = playerLevelRef;

        public void BindIdentity(PlayerProfile profileRef, AuthService authRef)
        {
            profile = profileRef;
            auth = authRef;
        }

        private string DisplayNickname()
        {
            if (auth != null && !string.IsNullOrEmpty(auth.ServerNickname)) return auth.ServerNickname;
            return profile != null ? profile.Nickname : "";
        }

        public void BindCollectibles(CoinSpawner coinSpawnerRef, CloudSpawner cloudSpawnerRef, Camera camera)
        {
            cam = camera;
            coinSpawner = coinSpawnerRef;
            cloudSpawner = cloudSpawnerRef;
            coinSpawner.OnPickupPopup += HandlePickupPopup;
            cloudSpawner.OnPickupPopup += HandlePickupPopup;
            coinSpawner.OnCoinCollected += HandleGoldPickup;
            coinSpawner.OnSpeedCoinCollected += HandleBluePickup;
        }

        public void BindSkyOrb(SkyOrbSpawner skyOrbSpawnerRef)
        {
            skyOrbSpawner = skyOrbSpawnerRef;
            skyOrbSpawner.OnPickupPopup += HandlePickupPopup;
            skyOrbSpawner.OnOrbCollected += HandleGreenPickup;
        }

        private void HandlePickupPopup(Vector3 worldPos, string text, Color color)
        {
            pickupToasts.Add(new PositionedToast { Text = text, Color = color, WorldPos = worldPos, Duration = pickupToastDuration, TimeLeft = pickupToastDuration });
        }

        // 우측 상단 픽업 개수 박스("골드/파랑/초록 몇 개 먹었는지 박스 안에" 요청) --
        // 시각 효과 없는 순수 카운트만. CoinSpawner/SkyOrbSpawner의 기존
        // "clean domain event" 패턴(OnCoinCollected 등)을 그대로 구독.
        private void HandleGoldPickup() => goldPickupCount++;
        private void HandleBluePickup() => bluePickupCount++;
        private void HandleGreenPickup() => greenPickupCount++;

        private void HandleRunStart()
        {
            goldPickupCount = 0;
            bluePickupCount = 0;
            greenPickupCount = 0;
        }

        // "도착하면 기록" 요청 -- 다음 섬(스테이지) 목표(레벨업 기준 -- 다음 섬까지
        // IslandRemainingDistance가 0이 되는 것)에 도달한 순간을 토스트로 눈에
        // 보이게 남김. 그 다음 섬의 남은 거리 카운트다운은 별도 처리 없이 그대로
        // 이어짐(GameManager.AdvanceScroll이 islandDistance를 알아서 리셋).
        private void HandleIslandAdvanced(int island) => AddToast($"ISLAND {island} REACHED! LEVEL UP", new Color(0.4f, 0.85f, 0.5f), toastDuration);

        private void OnDestroy()
        {
            if (slideJudge != null)
            {
                slideJudge.OnGreatSlide -= HandleGreatSlide;
                slideJudge.OnStreakBroken -= HandleStreakBroken;
            }
            if (fever != null) fever.OnFeverStart -= HandleFeverStart;
            if (gameManager != null)
            {
                gameManager.OnRunStart -= HandleRunStart;
                gameManager.OnIslandAdvanced -= HandleIslandAdvanced;
            }
            if (coinSpawner != null)
            {
                coinSpawner.OnPickupPopup -= HandlePickupPopup;
                coinSpawner.OnCoinCollected -= HandleGoldPickup;
                coinSpawner.OnSpeedCoinCollected -= HandleBluePickup;
            }
            if (cloudSpawner != null) cloudSpawner.OnPickupPopup -= HandlePickupPopup;
            if (skyOrbSpawner != null)
            {
                skyOrbSpawner.OnPickupPopup -= HandlePickupPopup;
                skyOrbSpawner.OnOrbCollected -= HandleGreenPickup;
            }
        }

        private void HandleGreatSlide(int streak, int gained)
        {
            string text = streak >= 3 ? $"GREAT SLIDE x{streak}! +{gained}" : $"SLIDE! +{gained}";
            AddToast(text, new Color(1f, 0.6f, 0.2f), toastDuration);
        }

        private void HandleStreakBroken() => AddToast("STREAK RESET", new Color(0.85f, 0.35f, 0.35f), toastDuration);
        private void HandleFeverStart() => AddToast($"FEVER TRIGGERED!  SCORE x{fever.Multiplier:0}", new Color(1f, 0.3f, 0.55f), feverToastDuration);

        private void AddToast(string text, Color color, float duration)
        {
            toasts.Add(new Toast { Text = text, Color = color, Duration = duration, TimeLeft = duration });
        }

        private void BuildUI()
        {
            var canvas = UIFactory.CreateCanvas("HUD Canvas", 0);
            root = canvas.gameObject;
            var t = canvas.transform;

            var brown = new Color(0.42f, 0.29f, 0.12f);

            // 요청: "게임 플레이 화면 내 텍스트와 박스를 좀더 키워주고" -- 이전
            // 패스(96 높이)에서 한 단계 더 키움(150), "닉네임 표시도 같이"
            // 요청으로 라벨 위에 닉네임 한 줄을 더 얹음.
            var scorePanel = UIFactory.CreatePanel(t, "ScorePanel", new Color(0f, 0f, 0f, 0.4f));
            scorePanelRect = (RectTransform)scorePanel.transform;
            UIFactory.SetTopLeft(scorePanelRect, 12, 8, 340, 150);

            nicknameText = UIFactory.CreateText(t, "Nickname", 20, new Color(1f, 0.9f, 0.65f), TextAlignmentOptions.TopLeft, FontStyles.Bold);
            UIFactory.SetTopLeft((RectTransform)nicknameText.transform, 24, 8, 300, 26);

            scoreLabelText = UIFactory.CreateText(t, "ScoreLabel", 18, new Color(1f, 0.85f, 0.55f), TextAlignmentOptions.TopLeft, FontStyles.Bold);
            UIFactory.SetTopLeft((RectTransform)scoreLabelText.transform, 24, 36, 200, 24);
            scoreLabelText.text = "SCORE"; // 정적 텍스트라 매 프레임 다시 설정할 필요 없음

            scoreText = UIFactory.CreateText(t, "Score", 60, new Color(1f, 0.95f, 0.75f), TextAlignmentOptions.TopLeft, FontStyles.Bold);
            UIFactory.SetTopLeft((RectTransform)scoreText.transform, 24, 62, 360, 78);

            // "레벨 값" 요청 -- Score 박스와 짝을 이루는 우측 상단 LEVEL 박스로
            // 승격(이전엔 "Island N · Mx" 한 줄짜리 평문 텍스트였음).
            var levelPanel = UIFactory.CreatePanel(t, "LevelPanel", new Color(0f, 0f, 0f, 0.4f));
            levelPanelRect = (RectTransform)levelPanel.transform;

            levelLabelText = UIFactory.CreateText(t, "LevelLabel", 18, new Color(1f, 0.85f, 0.55f), TextAlignmentOptions.TopLeft, FontStyles.Bold);
            levelLabelText.text = "LEVEL";

            levelText = UIFactory.CreateText(t, "LevelValue", 56, new Color(1f, 0.95f, 0.75f), TextAlignmentOptions.TopLeft, FontStyles.Bold);
            levelMultText = UIFactory.CreateText(t, "LevelMult", 20, new Color(1f, 0.85f, 0.55f), TextAlignmentOptions.TopLeft, FontStyles.Bold);

            // "다음 섬(스테이지)까지 얼마나 남았는지 알 수가 없다" 피드백 -- Island
            // 텍스트 바로 아래에 진행 바. 해/밤 바(주황~보라 계열)와 헷갈리지 않게
            // 초록 계열로 색을 다르게 둠.
            var islandProgressTrack = UIFactory.CreatePanel(t, "IslandProgressTrack", new Color(0f, 0f, 0f, 0.35f));
            islandProgressTrackRect = (RectTransform)islandProgressTrack.transform;

            islandProgressFill = UIFactory.CreatePanel(islandProgressTrack.transform, "IslandProgressFill", new Color(0.35f, 0.8f, 0.4f));
            var islandFillRect = (RectTransform)islandProgressFill.transform;
            islandFillRect.anchorMin = new Vector2(0f, 0f);
            islandFillRect.anchorMax = new Vector2(0f, 1f);
            islandFillRect.pivot = new Vector2(0f, 0.5f);
            islandFillRect.anchoredPosition = Vector2.zero;
            islandFillRect.sizeDelta = Vector2.zero;

            // 요청: "남은 거리 표시" -- 진행 바 아래 숫자로도 보여줌(레벨업 기준을
            // 눈에 보이는 값으로). brown은 카드 배경 없이 게임 월드 위에 바로
            // 뜨는 텍스트라 배경에 묻히기 쉬워서, 다른 HUD 텍스트와 같은 밝은
            // 크림색 + Bold로 바꿈(가독성 요청 반영).
            islandRemainingText = UIFactory.CreateText(t, "IslandRemaining", 18, new Color(1f, 0.95f, 0.75f), TextAlignmentOptions.TopLeft, FontStyles.Bold);

            // 요청: "거리 표시는 화면 중앙 하단에" + "dist: 뒤에 표시, 그 옆에
            // airtime:" -- 화면 하단 가로 중앙에 "DIST: ...  AIRTIME: ..."를 한
            // 줄로 표시. TextAlignmentOptions.Bottom이 상자 안에서 가로 중앙 +
            // 세로 하단 정렬해주므로, 상자 자체를 화면 하단 중앙에 두면 됨
            // (Update에서 Screen.width/height 기준으로 매 프레임 재배치).
            distanceText = UIFactory.CreateText(t, "Distance", 34, new Color(1f, 0.95f, 0.75f), TextAlignmentOptions.Bottom, FontStyles.Bold);
            distanceRect = (RectTransform)distanceText.transform;

            BuildDayClock(t);
            BuildPickupCountsPanel(t);
            BuildStreakPanel(t);
            BuildNestPanel(t);
            BuildFeverBadge(t);
            BuildToastPool(t);
            BuildPickupToastPool(t);
            BuildDebugText(t);
        }

        // 우측 상단 픽업 개수 박스("골드/파랑/초록 몇 개 먹었는지 박스 안에" 요청) --
        // 원 아이콘(각 픽업과 같은 색) + 숫자를 가로로 3쌍 나열.
        private void BuildPickupCountsPanel(Transform parent)
        {
            var panel = UIFactory.CreatePanel(parent, "PickupCountsPanel", new Color(0f, 0f, 0f, 0.35f));
            pickupCountsPanelRect = (RectTransform)panel.transform;

            // 요청: "텍스트와 박스를 좀더 키워주고" -- 아이콘 20->28, 폰트 16->22.
            var goldSprite = ProceduralSprite.CreateCircle(28, new Color(1f, 0.82f, 0.25f));
            var blueSprite = ProceduralSprite.CreateCircle(28, new Color(0.25f, 0.65f, 1f));
            var greenSprite = ProceduralSprite.CreateCircle(28, new Color(0.3f, 0.9f, 0.45f));

            var goldIcon = UIFactory.CreateImage(panel.transform, "GoldIcon", goldSprite);
            UIFactory.SetTopLeft((RectTransform)goldIcon.transform, 12f, 16f, 28f, 28f);
            goldCountText = UIFactory.CreateText(panel.transform, "GoldCount", 22, Color.white, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            UIFactory.SetTopLeft((RectTransform)goldCountText.transform, 44f, 14f, 44f, 32f);

            var blueIcon = UIFactory.CreateImage(panel.transform, "BlueIcon", blueSprite);
            UIFactory.SetTopLeft((RectTransform)blueIcon.transform, 102f, 16f, 28f, 28f);
            blueCountText = UIFactory.CreateText(panel.transform, "BlueCount", 22, Color.white, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            UIFactory.SetTopLeft((RectTransform)blueCountText.transform, 134f, 14f, 44f, 32f);

            var greenIcon = UIFactory.CreateImage(panel.transform, "GreenIcon", greenSprite);
            UIFactory.SetTopLeft((RectTransform)greenIcon.transform, 192f, 16f, 28f, 28f);
            greenCountText = UIFactory.CreateText(panel.transform, "GreenCount", 22, Color.white, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            UIFactory.SetTopLeft((RectTransform)greenCountText.transform, 224f, 14f, 44f, 32f);
        }

        private void BuildDayClock(Transform parent)
        {
            var track = UIFactory.CreatePanel(parent, "DayTrack", new Color(0f, 0f, 0f, 0.45f));
            dayTrackRect = (RectTransform)track.transform;

            dayTrackFill = UIFactory.CreatePanel(track.transform, "DayFill", new Color(1f, 0.6f, 0.15f));
            var fillRect = (RectTransform)dayTrackFill.transform;
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(0f, 1f);
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.anchoredPosition = Vector2.zero;
            fillRect.sizeDelta = Vector2.zero;

            var sunSprite = BuildSunSprite(32);
            var sun = UIFactory.CreateImage(parent, "SunIcon", sunSprite);
            sunIconRect = (RectTransform)sun.transform;
        }

        private void BuildStreakPanel(Transform parent)
        {
            var panel = UIFactory.CreatePanel(parent, "StreakPanel", new Color(0f, 0f, 0f, 0.35f));
            streakPanelRect = (RectTransform)panel.transform;

            streakLabel = UIFactory.CreateText(panel.transform, "StreakLabel", 18, Color.white, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            UIFactory.SetTopLeft((RectTransform)streakLabel.transform, 10f, 4f, 190f, 24f);

            for (int i = 0; i < StreakDotCount; i++)
            {
                var dot = UIFactory.CreatePanel(panel.transform, $"Dot{i}", Color.white);
                streakDots[i] = dot;
                UIFactory.SetTopLeft((RectTransform)dot.transform, 10f + i * 34f, 28f, 26f, 26f);
            }
        }

        private void BuildNestPanel(Transform parent)
        {
            var panel = UIFactory.CreatePanel(parent, "NestPanel", new Color(0f, 0f, 0f, 0.3f));
            nestPanelRect = (RectTransform)panel.transform;
            nestPanelBg = panel;

            nestHeaderText = UIFactory.CreateText(panel.transform, "NestHeader", 13, new Color(1f, 0.85f, 0.4f), TextAlignmentOptions.TopLeft, FontStyles.Bold);
            UIFactory.SetTopLeft((RectTransform)nestHeaderText.transform, 8f, 2f, 214f, 18f);

            for (int i = 0; i < NestLinePoolSize; i++)
            {
                var line = UIFactory.CreateText(panel.transform, $"NestLine{i}", 12, Color.white);
                nestLines[i] = line;
                UIFactory.SetTopLeft((RectTransform)line.transform, 8f, 20f + i * 20f, 214f, 18f);
            }
        }

        private void BuildFeverBadge(Transform parent)
        {
            var panel = UIFactory.CreatePanel(parent, "FeverBadge", new Color(1f, 0.25f, 0.5f));
            feverRect = (RectTransform)panel.transform;

            feverText = UIFactory.CreateText(panel.transform, "FeverText", 22, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            UIFactory.StretchFull((RectTransform)feverText.transform);

            panel.gameObject.SetActive(false);
        }

        private void BuildToastPool(Transform parent)
        {
            for (int i = 0; i < ToastPoolSize; i++)
            {
                var text = UIFactory.CreateText(parent, $"Toast{i}", 26, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
                toastPool[i] = text;
                text.gameObject.SetActive(false);
            }
        }

        private void BuildPickupToastPool(Transform parent)
        {
            for (int i = 0; i < PickupToastPoolSize; i++)
            {
                var text = UIFactory.CreateText(parent, $"PickupToast{i}", 16, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
                pickupToastPool[i] = text;
                text.gameObject.SetActive(false);
            }
        }

        private void BuildDebugText(Transform parent)
        {
            var panel = UIFactory.CreatePanel(parent, "DebugPanel", new Color(0f, 0f, 0f, 0.45f));
            debugPanelRect = (RectTransform)panel.transform;

            var brightWhite = new Color(1f, 0.95f, 0.75f);
            dbgStateText = UIFactory.CreateText(parent, "DbgState", 16, brightWhite, TextAlignmentOptions.TopRight, FontStyles.Bold);
            dbgHeightText = UIFactory.CreateText(parent, "DbgHeight", 16, brightWhite, TextAlignmentOptions.TopRight, FontStyles.Bold);
        }

        private void Update()
        {
            for (int i = toasts.Count - 1; i >= 0; i--)
            {
                var tst = toasts[i];
                tst.TimeLeft -= Time.deltaTime;
                if (tst.TimeLeft <= 0f) toasts.RemoveAt(i);
                else toasts[i] = tst;
            }

            for (int i = pickupToasts.Count - 1; i >= 0; i--)
            {
                var tst = pickupToasts[i];
                tst.TimeLeft -= Time.deltaTime;
                if (tst.TimeLeft <= 0f) pickupToasts.RemoveAt(i);
                else pickupToasts[i] = tst;
            }

            if (score == null) return;

            bool playing = gameManager.State == GameState.Playing;
            if (root.activeSelf != playing) root.SetActive(playing);
            if (!playing) return;

            scoreText.text = score.Score.ToString("N0");
            nicknameText.text = DisplayNickname();

            // 요청: 화면 하단 중앙에 "DIST: ...  AIRTIME: ..." 한 줄로. 폰트가
            // 26->34로 커진 만큼 상자도 같이 키움.
            distanceText.text = $"DIST: {gameManager.ScrollX:0.00}M   AIRTIME: {bird.TotalAirborneTime:0.00}S";
            const float distanceW = 620f, distanceH = 50f;
            UIFactory.SetTopLeft(distanceRect, (Screen.width - distanceW) * 0.5f, Screen.height - distanceH - 16f, distanceW, distanceH);

            // "레벨 값" 요청 -- Score 박스와 짝을 이루는 LEVEL 박스. 그 아래
            // (섬 진행 바/남은 거리/픽업 개수 박스)는 전부 이 박스의 x/폭 기준으로
            // 세로로 줄줄이 이어짐 -- 박스 하나 커지면 전부 다시 계산해야 하는
            // 이 화면의 고질적인 패턴(이전 HUD 작업들과 동일).
            const float levelPanelW = 180f, levelPanelH = 150f;
            float levelX = Screen.width - levelPanelW - 20f;
            UIFactory.SetTopLeft(levelPanelRect, levelX, 8f, levelPanelW, levelPanelH);
            UIFactory.SetTopLeft((RectTransform)levelLabelText.transform, levelX + 16f, 16f, 140f, 24f);
            // PlayerLevel.Level = 누적 이동 거리 기반 계정 레벨(런 종료 후 저장됨).
            // gameManager.Island는 Multiplier 서브텍스트와 진행 바에서 계속 사용.
            levelText.text = playerLevel != null ? playerLevel.Level.ToString() : gameManager.Island.ToString();
            UIFactory.SetTopLeft((RectTransform)levelText.transform, levelX + 16f, 44f, 140f, 68f);
            levelMultText.text = $"{gameManager.Multiplier}x";
            UIFactory.SetTopLeft((RectTransform)levelMultText.transform, levelX + 16f, 114f, 140f, 26f);

            const float islandTrackH = 10f;
            UIFactory.SetTopLeft(islandProgressTrackRect, levelX, 168f, levelPanelW, islandTrackH);
            ((RectTransform)islandProgressFill.transform).sizeDelta =
                new Vector2(levelPanelW * Mathf.Clamp01(gameManager.IslandProgress), 0f);

            islandRemainingText.text = $"{Mathf.Max(0f, gameManager.IslandRemainingDistance):0}m to Island {gameManager.Island + 1}";
            UIFactory.SetTopLeft((RectTransform)islandRemainingText.transform, levelX, 184f, levelPanelW, 24f);

            // 요청: "골드/파랑/초록 몇 개 먹었는지 박스 안에" -- LEVEL 박스와
            // 오른쪽 끝을 맞춰서 정렬.
            const float pickupPanelW = 280f, pickupPanelH = 60f;
            UIFactory.SetTopLeft(pickupCountsPanelRect, levelX + levelPanelW - pickupPanelW, 216f, pickupPanelW, pickupPanelH);
            goldCountText.text = goldPickupCount.ToString("N0");
            blueCountText.text = bluePickupCount.ToString("N0");
            greenCountText.text = greenPickupCount.ToString("N0");

            if (dayCycle != null) UpdateDayClock();
            UpdateStreakPanel();

            bool feverActive = fever.IsActive;
            if (feverRect.gameObject.activeSelf != feverActive) feverRect.gameObject.SetActive(feverActive);
            if (feverActive) UpdateFeverBadge();

            if (nest != null) UpdateNestPanel();
            else nestPanelBg.gameObject.SetActive(false);

            UpdateToasts();
            UpdatePickupToasts();

            bool showHeight = cam != null;
            UIFactory.SetTopLeft(debugPanelRect, Screen.width - 260, Screen.height - (showHeight ? 66 : 44), 248, showHeight ? 60 : 38);

            string state = bird.OnGround ? "Grounded" : bird.Airborne ? "Airborne" : "Falling";
            dbgStateText.text = $"{state}  spd {bird.Speed:0}{(bird.IsDiving ? "  DIVE" : "")}";
            UIFactory.SetTopLeft((RectTransform)dbgStateText.transform, Screen.width - 250, Screen.height - 58, 230, 24);

            if (showHeight)
            {
                dbgHeightText.gameObject.SetActive(true);
                dbgHeightText.text = $"height {bird.HeightAboveGround:0}  zoom {cam.orthographicSize:0}";
                UIFactory.SetTopLeft((RectTransform)dbgHeightText.transform, Screen.width - 250, Screen.height - 34, 230, 24);
            }
            else
            {
                dbgHeightText.gameObject.SetActive(false);
            }
        }

        private void UpdateDayClock()
        {
            // 요청: "텍스트와 박스를 좀더 키워주고" -- 스코어 패널이 닉네임
            // 줄 추가로 (12, 8, 340, 150)까지 커져서, 그 오른쪽 여백/trackY도
            // 다시 맞춤(패널 세로 중앙 8+150/2=83, trackH=18 -> trackY=74).
            // 바 자체도 135x14 -> 160x18, 태양 32 -> 38로 같이 키움.
            const float trackX = 372f, trackY = 74f, trackW = 160f, trackH = 18f;
            UIFactory.SetTopLeft(dayTrackRect, trackX, trackY, trackW, trackH);

            float t = dayCycle.DayTime;
            ((RectTransform)dayTrackFill.transform).sizeDelta = new Vector2(trackW * t, 0f);
            dayTrackFill.color = Color.Lerp(new Color(1f, 0.6f, 0.15f), new Color(0.55f, 0.3f, 0.85f), t);

            const float sunSize = 38f;
            float sunX = trackX + trackW * t - sunSize * 0.5f;
            float sunY = trackY + trackH * 0.5f - sunSize * 0.5f;
            UIFactory.SetTopLeft(sunIconRect, sunX, sunY, sunSize, sunSize);
        }

        private void UpdateStreakPanel()
        {
            const float panelW = 210f, panelH = 56f;
            UIFactory.SetTopLeft(streakPanelRect, 16f, Screen.height - panelH - 14f, panelW, panelH);

            streakLabel.text = $"STREAK {slideJudge.SlideStreak}/3";

            for (int i = 0; i < StreakDotCount; i++)
            {
                bool lit = slideJudge.SlideStreak > i || fever.IsActive;
                streakDots[i].color = lit ? new Color(1f, 0.85f, 0.25f) : new Color(1f, 1f, 1f, 0.25f);
            }
        }

        private void UpdateNestPanel()
        {
            var missions = nest.ActiveMissions;
            if (missions.Length == 0)
            {
                nestPanelBg.gameObject.SetActive(false);
                return;
            }

            nestPanelBg.gameObject.SetActive(true);
            const float panelW = 230f;
            float panelH = 22f + missions.Length * 20f;
            // 점수 패널이 닉네임 줄 추가로 (8, 150) -> 아래쪽 끝이 y=158로 다시
            // 커짐 -- 그 아래로 여유 있게 내림(116 -> 168).
            UIFactory.SetTopLeft(nestPanelRect, 20f, 168f, panelW, panelH);

            nestHeaderText.text = string.Format(Localization.Get("hud.nestHeader"), nest.Bonus);

            for (int i = 0; i < NestLinePoolSize; i++)
            {
                if (i >= missions.Length) { nestLines[i].gameObject.SetActive(false); continue; }

                nestLines[i].gameObject.SetActive(true);
                var mission = missions[i];
                float progress = nest.GetProgress(mission);
                bool done = progress >= mission.Target;
                nestLines[i].color = done ? new Color(0.6f, 1f, 0.6f) : new Color(1f, 1f, 1f, 0.85f);
                string mark = done ? "O" : $"{Mathf.Min(progress, mission.Target):0}/{mission.Target}";
                nestLines[i].text = $"{mission.Description} ({mark})";
            }
        }

        private void UpdateFeverBadge()
        {
            UIFactory.SetTopLeftCentered(feverRect, Screen.width * 0.5f - 130f, 16f, 260f, 44f);
            float pulse = 1f + Mathf.Sin(Time.time * 8f) * 0.06f;
            feverRect.localScale = new Vector3(pulse, pulse, 1f);
            feverText.text = $"FEVER x{fever.Multiplier:0}  {fever.TimeRemaining:0.0}s";
        }

        private void UpdateToasts()
        {
            float y = Screen.height * 0.32f;
            for (int i = 0; i < ToastPoolSize; i++)
            {
                if (i >= toasts.Count) { toastPool[i].gameObject.SetActive(false); continue; }

                var tst = toasts[i];
                toastPool[i].gameObject.SetActive(true);
                float alpha = Mathf.Clamp01(tst.TimeLeft / (tst.Duration * 0.4f));
                float rise = (1f - tst.TimeLeft / tst.Duration) * 30f;
                var c = tst.Color;
                c.a = alpha;
                toastPool[i].color = c;
                toastPool[i].text = tst.Text;
                UIFactory.SetTopLeftCentered((RectTransform)toastPool[i].transform, Screen.width * 0.5f - 260f, y - rise, 520f, 40f);
                y += 34f;
            }
        }

        private void UpdatePickupToasts()
        {
            if (cam == null)
            {
                for (int i = 0; i < PickupToastPoolSize; i++) pickupToastPool[i].gameObject.SetActive(false);
                return;
            }

            for (int i = 0; i < PickupToastPoolSize; i++)
            {
                if (i >= pickupToasts.Count) { pickupToastPool[i].gameObject.SetActive(false); continue; }

                var tst = pickupToasts[i];
                Vector3 screenPos = cam.WorldToScreenPoint(tst.WorldPos);
                if (screenPos.z < 0f) { pickupToastPool[i].gameObject.SetActive(false); continue; }

                pickupToastPool[i].gameObject.SetActive(true);
                float alpha = Mathf.Clamp01(tst.TimeLeft / (tst.Duration * 0.5f));
                float rise = (1f - tst.TimeLeft / tst.Duration) * 24f;
                var c = tst.Color;
                c.a = alpha;
                pickupToastPool[i].color = c;
                pickupToastPool[i].text = tst.Text;

                float guiY = Screen.height - screenPos.y;
                UIFactory.SetTopLeftCentered((RectTransform)pickupToastPool[i].transform, screenPos.x - 80f, guiY - rise - 20f, 160f, 26f);
            }
        }

        // Cute sun icon that rides the day-clock progress -- same
        // procedural pixel-drawing technique as BirdVisual's chick, now
        // baked into a Sprite once (instead of a raw Texture2D handed to
        // GUI.DrawTexture every OnGUI call).
        private Sprite BuildSunSprite(int size)
        {
            var pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.clear;

            float cx = size * 0.5f, cy = size * 0.5f;
            float bodyR = size * 0.30f;
            var core = new Color(1f, 0.82f, 0.25f);
            var rayColor = new Color(1f, 0.62f, 0.15f);
            var faceColor = new Color(0.35f, 0.22f, 0.05f);

            const int rayCount = 8;
            for (int r = 0; r < rayCount; r++)
            {
                float angle = r * Mathf.PI * 2f / rayCount + Mathf.PI / 8f;
                float dirX = Mathf.Cos(angle), dirY = Mathf.Sin(angle);
                for (int k = 0; k < 3; k++)
                {
                    float dist = bodyR * 0.95f + k * size * 0.085f;
                    float px = cx + dirX * dist;
                    float py = cy + dirY * dist;
                    float rr = size * (0.05f - k * 0.009f);
                    FillDot(pixels, size, px, py, rr, rayColor);
                }
            }

            FillDot(pixels, size, cx, cy, bodyR, core);
            FillDot(pixels, size, cx - bodyR * 0.4f, cy - bodyR * 0.1f, size * 0.035f, faceColor);
            FillDot(pixels, size, cx + bodyR * 0.4f, cy - bodyR * 0.1f, size * 0.035f, faceColor);
            for (int i = -2; i <= 2; i++)
            {
                float sx = cx + i * size * 0.035f;
                float sy = cy + bodyR * 0.25f + (2 - Mathf.Abs(i)) * size * 0.02f;
                FillDot(pixels, size, sx, sy, size * 0.025f, faceColor);
            }

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        private static void FillDot(Color[] pixels, int size, float cx, float cy, float r, Color color)
        {
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                    if (dx * dx + dy * dy <= r * r)
                        pixels[y * size + x] = color;
                }
            }
        }
    }
}
