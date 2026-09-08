using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HillyWings
{
    public class DayOverScreen : MonoBehaviour
    {
        private const int StatLineCount = 7;
        private const int MaxNestLines = 5;

        private ScoreManager score;
        private SlideJudge slideJudge;
        private CloudSpawner cloudSpawner;
        private FeverSystem fever;
        private GameManager gameManager;
        private CoinWallet wallet;
        private NestMultiplier nest;
        private AudioManager audio;
        private AuthService auth;

        private bool submittedThisRun;
        private bool isNewBest;

        private GameObject root;
        private TextMeshProUGUI titleText;
        private TextMeshProUGUI bestBadgeText;

        // 스탯 카드
        private RectTransform statCardRT;
        private readonly TextMeshProUGUI[] statLines = new TextMeshProUGUI[StatLineCount];

        // 네스트 카드
        private RectTransform nestCardRT;
        private TextMeshProUGUI nestHeaderText;
        private readonly TextMeshProUGUI[] nestLines = new TextMeshProUGUI[MaxNestLines];

        private Button restartButton;
        private TextMeshProUGUI restartButtonText;
        private Button homeButton;
        private TextMeshProUGUI homeButtonText;

        // 레이아웃 상수
        private const float CardW   = 660f;
        private const float CardPad = 12f;
        private const float StatH   = 42f;   // 스탯 한 줄 높이
        private const float NestH   = 38f;   // 네스트 한 줄 높이
        private const float TitleH  = 66f;
        private const float BadgeH  = 36f;
        private const float BtnH    = 60f;
        private const float BtnW    = 188f;

        public void Bind(ScoreManager scoreRef, SlideJudge slideJudgeRef, CloudSpawner cloudSpawnerRef,
            FeverSystem feverRef, GameManager gameManagerRef, CoinWallet walletRef,
            NestMultiplier nestRef, AudioManager audioRef, AuthService authRef = null)
        {
            score = scoreRef;
            slideJudge = slideJudgeRef;
            cloudSpawner = cloudSpawnerRef;
            fever = feverRef;
            gameManager = gameManagerRef;
            wallet = walletRef;
            nest = nestRef;
            audio = audioRef;
            auth = authRef;

            gameManager.OnRunStart += HandleRunStart;

            BuildUI();

            Localization.OnLanguageChanged += RefreshStaticLabels;
        }

        private void OnDestroy()
        {
            if (gameManager != null)
                gameManager.OnRunStart -= HandleRunStart;
            Localization.OnLanguageChanged -= RefreshStaticLabels;
        }

        private void HandleRunStart() => submittedThisRun = false;

        private void BuildUI()
        {
            var canvas = UIFactory.CreateCanvas("DayOverScreen Canvas", 20);
            root = canvas.gameObject;
            var t = canvas.transform;

            // 반투명 전체 오버레이
            var overlay = UIFactory.CreatePanel(t, "Overlay", new Color(0.08f, 0.04f, 0.12f, 0.7f));
            UIFactory.StretchFull((RectTransform)overlay.transform);

            // 타이틀
            titleText = UIFactory.CreateText(t, "Title", 52, new Color(1f, 0.93f, 0.75f),
                TextAlignmentOptions.Center, FontStyles.Bold);

            // NEW HIGHSCORE 뱃지
            bestBadgeText = UIFactory.CreateText(t, "BestBadge", 24, new Color(1f, 0.6f, 0.1f),
                TextAlignmentOptions.Center, FontStyles.Bold);
            bestBadgeText.text = "★ NEW HIGHSCORE! ★";

            // 스탯 카드 배경 — statLines보다 먼저 생성해야 뒤에 렌더링됨
            var statCard = UIFactory.CreatePanel(t, "StatCard", new Color(0.04f, 0.02f, 0.08f, 0.85f));
            statCardRT = (RectTransform)statCard.transform;

            // 스탯 텍스트 (폰 가독성을 위해 굵고 밝게)
            var statColor = new Color(0.95f, 0.9f, 0.82f);
            for (int i = 0; i < StatLineCount; i++)
                statLines[i] = UIFactory.CreateText(t, $"Stat{i}", 28, statColor,
                    TextAlignmentOptions.Center, FontStyles.Bold);

            // 네스트 카드 배경 — nestLines보다 먼저 생성
            var nestCard = UIFactory.CreatePanel(t, "NestCard", new Color(0.1f, 0.06f, 0.01f, 0.85f));
            nestCardRT = (RectTransform)nestCard.transform;

            nestHeaderText = UIFactory.CreateText(t, "NestHeader", 22, new Color(1f, 0.8f, 0.3f),
                TextAlignmentOptions.Center, FontStyles.Bold);

            for (int i = 0; i < MaxNestLines; i++)
                nestLines[i] = UIFactory.CreateText(t, $"NestLine{i}", 24, Color.white,
                    TextAlignmentOptions.Center, FontStyles.Bold);

            restartButton = UIFactory.CreateButton(t, "RestartButton", "", 26,
                new Color(0.35f, 0.2f, 0.08f), out restartButtonText);
            restartButton.onClick.AddListener(() => { audio?.PlayClick(); gameManager.BeginRun(); });

            homeButton = UIFactory.CreateButton(t, "HomeButton", "", 26,
                new Color(0.18f, 0.18f, 0.28f), out homeButtonText);
            homeButton.onClick.AddListener(() => { audio?.PlayClick(); gameManager.ReturnToStart(); });

            RefreshStaticLabels();
        }

        private void RefreshStaticLabels()
        {
            titleText.text = Localization.Get("dayover.title");
            restartButtonText.text = Localization.Get("dayover.restart");
            homeButtonText.text = Localization.Get("dayover.home");
        }

        private void Update()
        {
            bool active = gameManager.State == GameState.DayOver;
            if (root.activeSelf != active) root.SetActive(active);
            if (!active) return;

            if (!submittedThisRun)
            {
                isNewBest = SaveSystem.Instance != null && SaveSystem.Instance.SubmitScore(score.Score);
                submittedThisRun = true;
                int slides = slideJudge != null ? slideJudge.TotalSlides : 0;
                auth?.SubmitScore(score.Score, gameManager.Island, slides);
            }

            Layout();
        }

        private void Layout()
        {
            float cx = Screen.width * 0.5f;
            float cy = Screen.height * 0.5f;

            // ── 타이틀 ──────────────────────────────────────────────────
            float titleTop = cy - 248f;
            UIFactory.SetTopLeftCentered((RectTransform)titleText.transform,
                cx - CardW * 0.5f, titleTop, CardW, TitleH);

            // ── NEW HIGHSCORE 뱃지 ────────────────────────────────────
            bestBadgeText.gameObject.SetActive(isNewBest);
            float statTop = titleTop + TitleH + 10f;
            if (isNewBest)
            {
                float badgeTop = titleTop + TitleH + 6f;
                UIFactory.SetTopLeftCentered((RectTransform)bestBadgeText.transform,
                    cx - CardW * 0.5f, badgeTop, CardW, BadgeH);
                statTop = badgeTop + BadgeH + 6f;
            }

            // ── 스탯 라인 (카드 안) ───────────────────────────────────
            string[] lines =
            {
                $"Score: {score.Score:N0}",
                $"Island: {gameManager.Island}",
                $"Great Slides: {slideJudge.TotalSlides}",
                $"Cloud Touches: {(cloudSpawner != null ? cloudSpawner.TouchCount : 0)}",
                $"Longest Fever: {(fever != null ? fever.LongestDuration : 0f):0.0}s",
                $"Best: {(SaveSystem.Instance != null ? SaveSystem.Instance.BestScore : score.Score):N0}",
                $"Coins earned: +{(wallet != null ? wallet.LastRunCoinsAwarded : 0)}" +
                $"  (total {(wallet != null ? wallet.Coins : 0):N0})"
            };

            float y = statTop + CardPad;
            for (int i = 0; i < StatLineCount; i++)
            {
                statLines[i].text = lines[i];
                UIFactory.SetTopLeftCentered((RectTransform)statLines[i].transform,
                    cx - CardW * 0.5f, y, CardW, StatH);
                y += StatH;
            }

            // 스탯 카드 박스 위치 (텍스트 전체를 감쌈)
            UIFactory.SetTopLeft(statCardRT,
                cx - CardW * 0.5f - CardPad, statTop,
                CardW + CardPad * 2f, y - statTop + CardPad);

            // ── 네스트 목표 (카드 안) ─────────────────────────────────
            float nestSectionStart = y + CardPad + 14f;
            if (nest != null)
                y = LayoutNestObjectives(cx, nestSectionStart);
            else
            {
                nestHeaderText.gameObject.SetActive(false);
                for (int i = 0; i < MaxNestLines; i++) nestLines[i].gameObject.SetActive(false);
                nestCardRT.gameObject.SetActive(false);
                y = nestSectionStart;
            }

            // ── 버튼 ─────────────────────────────────────────────────
            float btnY = Mathf.Max(y + 16f, cy + 220f);
            UIFactory.SetTopLeftCentered((RectTransform)restartButton.transform,
                cx - BtnW - 10f, btnY, BtnW, BtnH);
            UIFactory.SetTopLeftCentered((RectTransform)homeButton.transform,
                cx + 10f, btnY, BtnW, BtnH);
        }

        private float LayoutNestObjectives(float cx, float nestTop)
        {
            nestCardRT.gameObject.SetActive(true);
            nestHeaderText.gameObject.SetActive(true);

            float y = nestTop + CardPad;

            nestHeaderText.text = $"Nest Multiplier (+{nest.Bonus})";
            UIFactory.SetTopLeftCentered((RectTransform)nestHeaderText.transform,
                cx - CardW * 0.5f, y, CardW, 32f);
            y += 34f;

            var missions = nest.ActiveMissions;
            for (int i = 0; i < MaxNestLines; i++)
            {
                if (i >= missions.Length) { nestLines[i].gameObject.SetActive(false); continue; }

                nestLines[i].gameObject.SetActive(true);
                var mission = missions[i];
                bool passed = nest.GetProgress(mission) >= mission.Target;
                nestLines[i].color = passed ? new Color(0.2f, 0.8f, 0.3f) : new Color(0.9f, 0.35f, 0.3f);
                nestLines[i].text = (passed ? "O " : "X ") + mission.Description;
                UIFactory.SetTopLeftCentered((RectTransform)nestLines[i].transform,
                    cx - CardW * 0.5f, y, CardW, NestH);
                y += NestH;
            }

            // 네스트 카드 박스
            UIFactory.SetTopLeft(nestCardRT,
                cx - CardW * 0.5f - CardPad, nestTop,
                CardW + CardPad * 2f, y - nestTop + CardPad);

            return y;
        }
    }
}
