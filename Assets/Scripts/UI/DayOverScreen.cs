using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HillyWings
{
    // Reference: endScreen -- final stats (score/island/slides), submits to
    // SaveSystem, shows Best line, "다시하기"(restart)/"홈"(back to start)
    // buttons. Coin count-up animation is a later visual pass.
    //
    // M5 additions: coins earned this run (CoinWallet), and pass/fail for
    // this run's 3 Nest Multiplier objectives (NestMultiplier).
    //
    // M7: converted from OnGUI to a runtime-built UGUI/TextMeshPro
    // hierarchy. This screen is only shown while idle between runs (never
    // during active gameplay), so unlike HUD/StartScreen it just
    // recomputes its whole layout every frame it's visible rather than
    // gating on a resize check -- simpler and not worth the extra
    // bookkeeping here.
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
        private readonly TextMeshProUGUI[] statLines = new TextMeshProUGUI[StatLineCount];
        private TextMeshProUGUI nestHeaderText;
        private readonly TextMeshProUGUI[] nestLines = new TextMeshProUGUI[MaxNestLines];
        private Button restartButton;
        private TextMeshProUGUI restartButtonText;
        private Button homeButton;
        private TextMeshProUGUI homeButtonText;

        public void Bind(ScoreManager scoreRef, SlideJudge slideJudgeRef, CloudSpawner cloudSpawnerRef, FeverSystem feverRef, GameManager gameManagerRef, CoinWallet walletRef, NestMultiplier nestRef, AudioManager audioRef, AuthService authRef = null)
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

            var overlay = UIFactory.CreatePanel(t, "Overlay", new Color(0.1f, 0.05f, 0.15f, 0.6f));
            UIFactory.StretchFull((RectTransform)overlay.transform);

            titleText = UIFactory.CreateText(t, "Title", 36, new Color(0.32f, 0.2f, 0.36f), TextAlignmentOptions.Center, FontStyles.Bold);

            bestBadgeText = UIFactory.CreateText(t, "BestBadge", 16, new Color(1f, 0.55f, 0.15f), TextAlignmentOptions.Center, FontStyles.Bold);
            bestBadgeText.text = "NEW HIGHSCORE!";

            var statColor = new Color(0.32f, 0.2f, 0.2f);
            for (int i = 0; i < StatLineCount; i++)
                statLines[i] = UIFactory.CreateText(t, $"Stat{i}", 18, statColor, TextAlignmentOptions.Center);

            nestHeaderText = UIFactory.CreateText(t, "NestHeader", 16, new Color(0.42f, 0.29f, 0.12f), TextAlignmentOptions.Center, FontStyles.Bold);

            for (int i = 0; i < MaxNestLines; i++)
                nestLines[i] = UIFactory.CreateText(t, $"NestLine{i}", 15, Color.white, TextAlignmentOptions.Center);

            restartButton = UIFactory.CreateButton(t, "RestartButton", "", 20, new Color(0.3f, 0.2f, 0.1f), out restartButtonText);
            restartButton.onClick.AddListener(() =>
            {
                audio?.PlayClick();
                gameManager.BeginRun();
            });

            homeButton = UIFactory.CreateButton(t, "HomeButton", "", 20, new Color(0.3f, 0.2f, 0.1f), out homeButtonText);
            homeButton.onClick.AddListener(() =>
            {
                audio?.PlayClick();
                gameManager.ReturnToStart();
            });

            RefreshStaticLabels();
        }

        // Only titleText/restartButtonText/homeButtonText are set once here
        // and never touched again -- everything else in this screen's
        // Layout() already reassigns .text every frame it's visible, so it
        // picks up a language change on its own (see the M7 comment on
        // Layout() -- this screen isn't perf-sensitive enough to bother
        // gating that).
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
                // 로그인 상태면 서버에 이 런의 결과를 제출하고 로컬 통계도 즉시 반영.
                auth?.SubmitScore(score.Score, gameManager.Island, slides);
            }

            Layout();
        }

        private void Layout()
        {
            float cx = Screen.width * 0.5f;
            float cy = Screen.height * 0.5f;

            UIFactory.SetTopLeftCentered((RectTransform)titleText.transform, cx - 300f, cy - 210f, 600f, 46f);

            bestBadgeText.gameObject.SetActive(isNewBest);
            if (isNewBest)
                UIFactory.SetTopLeftCentered((RectTransform)bestBadgeText.transform, cx - 300f, cy - 170f, 600f, 24f);

            string[] lines =
            {
                $"Score: {score.Score:N0}",
                $"Island: {gameManager.Island}",
                $"Great Slides: {slideJudge.TotalSlides}",
                $"Cloud Touches: {(cloudSpawner != null ? cloudSpawner.TouchCount : 0)}",
                $"Longest Fever: {(fever != null ? fever.LongestDuration : 0f):0.0}s",
                $"Best: {(SaveSystem.Instance != null ? SaveSystem.Instance.BestScore : score.Score):N0}",
                $"Coins earned: +{(wallet != null ? wallet.LastRunCoinsAwarded : 0)}  (total {(wallet != null ? wallet.Coins : 0):N0})"
            };

            float y = cy - 140f;
            for (int i = 0; i < StatLineCount; i++)
            {
                statLines[i].text = lines[i];
                UIFactory.SetTopLeftCentered((RectTransform)statLines[i].transform, cx - 300f, y, 600f, 24f);
                y += 24f;
            }

            if (nest != null) y = LayoutNestObjectives(cx, y + 8f);
            else
            {
                nestHeaderText.gameObject.SetActive(false);
                for (int i = 0; i < MaxNestLines; i++) nestLines[i].gameObject.SetActive(false);
            }

            float btnY = Mathf.Max(y + 16f, cy + 130f);
            UIFactory.SetTopLeftCentered((RectTransform)restartButton.transform, cx - 170f, btnY, 150f, 46f);
            UIFactory.SetTopLeftCentered((RectTransform)homeButton.transform, cx + 20f, btnY, 150f, 46f);
        }

        private float LayoutNestObjectives(float cx, float y)
        {
            nestHeaderText.gameObject.SetActive(true);
            nestHeaderText.text = $"Nest Multiplier (+{nest.Bonus})";
            UIFactory.SetTopLeftCentered((RectTransform)nestHeaderText.transform, cx - 300f, y, 600f, 22f);
            y += 24f;

            var missions = nest.ActiveMissions;
            for (int i = 0; i < MaxNestLines; i++)
            {
                if (i >= missions.Length) { nestLines[i].gameObject.SetActive(false); continue; }

                nestLines[i].gameObject.SetActive(true);
                var mission = missions[i];
                bool passed = nest.GetProgress(mission) >= mission.Target;
                nestLines[i].color = passed ? new Color(0.2f, 0.55f, 0.2f) : new Color(0.55f, 0.2f, 0.2f);
                string mark = passed ? "O" : "X";
                nestLines[i].text = $"{mark} {mission.Description}";
                UIFactory.SetTopLeftCentered((RectTransform)nestLines[i].transform, cx - 300f, y, 600f, 22f);
                y += 22f;
            }

            return y;
        }
    }
}
