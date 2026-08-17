using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FlyingChick
{
    // Reference: title overlay shown in state 'start'; any click/tap/space
    // begins the run -- EXCEPT when the tap lands on one of this screen's
    // own buttons (bird icons, egg purchase, leaderboard toggle), which
    // exist as of M6.
    //
    // M7: converted from OnGUI to a runtime-built UGUI/TextMeshPro
    // hierarchy. The old "tap anywhere to start" needed a hand-rolled
    // IsBlockingClick(Rect) check to avoid double-firing on top of buttons;
    // with real UGUI buttons that problem disappears structurally -- a
    // full-screen transparent "tap catcher" Button sits behind everything
    // else (built first, so later siblings render on top and intercept
    // clicks first), and it only ever receives a click when nothing else
    // was hit. Mouse/touch reach it through normal UGUI click routing;
    // space bar has no UGUI click path so it's still polled directly via
    // InputService.IsSpaceDownThisFrame().
    public class StartScreen : MonoBehaviour
    {
        private const int MaxDailyMissionLines = 5;
        private const int MaxLeaderboardLines = 12;

        private CoinWallet wallet;
        private DailyMissions dailyMissions;
        private BirdCollection collection;
        private Leaderboard leaderboard;
        private AudioManager audio;

        private bool showLeaderboard;
        private string hatchMessage;
        private float hatchMessageTimeLeft;

        private GameObject root;
        private GameObject baseContent;
        private GameObject leaderboardGroup;

        private RectTransform titleRect, sub1Rect, sub2Rect;
        private TextMeshProUGUI bestText;
        private TextMeshProUGUI coinsText;
        private Button eggButton;
        private TextMeshProUGUI eggButtonText;
        private TextMeshProUGUI hatchText;
        private TextMeshProUGUI birdNameText;

        private Button[] birdButtons;
        private Image[] birdSelectionBorders;
        private TextMeshProUGUI[] birdLockTexts;
        private RectTransform[] birdIconRects;

        private GameObject dailyPanel;
        private TextMeshProUGUI[] dailyLines;

        private TextMeshProUGUI[] leaderboardLines;

        private int lastWidth = -1, lastHeight = -1;

        public void Bind(CoinWallet walletRef, DailyMissions dailyMissionsRef, BirdCollection collectionRef, Leaderboard leaderboardRef, AudioManager audioRef)
        {
            wallet = walletRef;
            dailyMissions = dailyMissionsRef;
            collection = collectionRef;
            leaderboard = leaderboardRef;
            audio = audioRef;

            BuildUI();
        }

        private void BuildUI()
        {
            var canvas = UIFactory.CreateCanvas("StartScreen Canvas", 10);
            root = canvas.gameObject;
            var t = canvas.transform;

            var overlay = UIFactory.CreatePanel(t, "Overlay", new Color(1f, 0.97f, 0.87f, 0.55f));
            UIFactory.StretchFull((RectTransform)overlay.transform);

            var tapCatcher = UIFactory.CreateFullScreenTapCatcher(t, "TapCatcher");
            tapCatcher.onClick.AddListener(() => GameManager.Instance.BeginRun());

            baseContent = UIFactory.CreateChild(t, "BaseContent").gameObject;
            BuildBaseContent(baseContent.transform);

            leaderboardGroup = UIFactory.CreateChild(t, "LeaderboardGroup").gameObject;
            BuildLeaderboardGroup(leaderboardGroup.transform);
            leaderboardGroup.SetActive(false);

            lastWidth = Screen.width;
            lastHeight = Screen.height;
            ReflowLayout();
        }

        private void BuildBaseContent(Transform parent)
        {
            var brown = new Color(0.42f, 0.29f, 0.12f);

            var title = UIFactory.CreateText(parent, "Title", 48, new Color(0.36f, 0.24f, 0.1f), TextAlignmentOptions.Center, FontStyles.Bold);
            titleRect = (RectTransform)title.transform;
            title.text = "Flying Chick";

            var sub1 = UIFactory.CreateText(parent, "Sub1", 18, brown, TextAlignmentOptions.Center);
            sub1Rect = (RectTransform)sub1.transform;
            sub1.text = "내리막에서 눌러 다이빙, 오르막에서 발사!";

            var sub2 = UIFactory.CreateText(parent, "Sub2", 18, brown, TextAlignmentOptions.Center);
            sub2Rect = (RectTransform)sub2.transform;
            sub2.text = "터치 / 클릭 / 스페이스바로 시작";

            bestText = UIFactory.CreateText(parent, "Best", 18, brown, TextAlignmentOptions.Center);

            coinsText = UIFactory.CreateText(parent, "Coins", 18, new Color(0.85f, 0.6f, 0.1f), TextAlignmentOptions.TopLeft, FontStyles.Bold);

            var leaderboardToggle = UIFactory.CreateButton(parent, "LeaderboardToggle", "기록 보기", 15, brown, out _);
            leaderboardToggle.onClick.AddListener(() =>
            {
                baseContent.SetActive(false);
                leaderboardGroup.SetActive(true);
                audio?.PlayClick();
            });

            eggButton = UIFactory.CreateButton(parent, "EggButton", "", 15, brown, out eggButtonText);
            eggButton.onClick.AddListener(OnEggButtonClicked);

            hatchText = UIFactory.CreateText(parent, "HatchMessage", 15, new Color(1f, 0.6f, 0.15f), TextAlignmentOptions.Center, FontStyles.Bold);
            hatchText.gameObject.SetActive(false);

            birdNameText = UIFactory.CreateText(parent, "BirdName", 13, brown, TextAlignmentOptions.Center);

            BuildBirdRow(parent);
            BuildDailyMissionsPanel(parent);
        }

        private void BuildBirdRow(Transform parent)
        {
            var birds = BirdPool.All;
            int n = birds.Length;
            birdButtons = new Button[n];
            birdSelectionBorders = new Image[n];
            birdLockTexts = new TextMeshProUGUI[n];
            birdIconRects = new RectTransform[n];

            for (int i = 0; i < n; i++)
            {
                var border = UIFactory.CreatePanel(parent, $"BirdBorder{i}", Color.white);
                birdSelectionBorders[i] = border;

                var iconSprite = ProceduralSprite.CreateCircle(40, birds[i].BodyColor);
                var iconRt = UIFactory.CreateChild(parent, $"BirdIcon{i}");
                birdIconRects[i] = iconRt;
                var iconImg = iconRt.gameObject.AddComponent<Image>();
                iconImg.sprite = iconSprite;

                var btn = iconRt.gameObject.AddComponent<Button>();
                btn.targetGraphic = iconImg;
                btn.navigation = new Navigation { mode = Navigation.Mode.None };
                birdButtons[i] = btn;

                int captured = i;
                btn.onClick.AddListener(() => OnBirdClicked(captured));

                var lockText = UIFactory.CreateText(iconRt, "Lock", 20, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
                UIFactory.StretchFull((RectTransform)lockText.transform);
                lockText.text = "?";
                lockText.raycastTarget = false;
                birdLockTexts[i] = lockText;
            }
        }

        private void BuildDailyMissionsPanel(Transform parent)
        {
            var panel = UIFactory.CreatePanel(parent, "DailyMissionsPanel", new Color(0f, 0f, 0f, 0.3f));
            dailyPanel = panel.gameObject;

            var header = UIFactory.CreateText(panel.transform, "DailyHeader", 15, Color.white, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            UIFactory.SetTopLeft((RectTransform)header.transform, 10f, 4f, 240f, 20f);
            header.text = "오늘의 미션";

            dailyLines = new TextMeshProUGUI[MaxDailyMissionLines];
            for (int i = 0; i < MaxDailyMissionLines; i++)
            {
                var line = UIFactory.CreateText(panel.transform, $"DailyLine{i}", 13, new Color(1f, 1f, 1f, 0.85f));
                UIFactory.SetTopLeft((RectTransform)line.transform, 10f, 26f + i * 22f, 240f, 20f);
                dailyLines[i] = line;
            }
        }

        private void BuildLeaderboardGroup(Transform parent)
        {
            var backdrop = UIFactory.CreatePanel(parent, "Backdrop", new Color(0f, 0f, 0f, 0f));
            backdrop.raycastTarget = true;
            UIFactory.StretchFull((RectTransform)backdrop.transform);

            var panel = UIFactory.CreatePanel(parent, "Panel", new Color(0.15f, 0.1f, 0.08f, 0.9f));
            var panelRect = (RectTransform)panel.transform;

            var header = UIFactory.CreateText(panel.transform, "Header", 24, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            UIFactory.SetTopLeft((RectTransform)header.transform, 0f, 14f, 440f, 34f);
            header.text = "기록";

            leaderboardLines = new TextMeshProUGUI[MaxLeaderboardLines];
            for (int i = 0; i < MaxLeaderboardLines; i++)
            {
                var line = UIFactory.CreateText(panel.transform, $"Line{i}", 16, new Color(1f, 1f, 1f, 0.9f));
                UIFactory.SetTopLeft((RectTransform)line.transform, 24f, 56f + i * 24f, 392f, 22f);
                leaderboardLines[i] = line;
            }

            var closeButton = UIFactory.CreateButton(panel.transform, "CloseButton", "닫기", 16, new Color(0.3f, 0.2f, 0.1f), out _);
            UIFactory.SetTopLeftCentered((RectTransform)closeButton.transform, 220f - 60f, 480f - 50f, 120f, 36f);
            closeButton.onClick.AddListener(() =>
            {
                leaderboardGroup.SetActive(false);
                baseContent.SetActive(true);
                audio?.PlayClick();
            });

            UIFactory.SetTopLeftCentered(panelRect, 0f, 0f, 440f, 480f); // repositioned in Update to stay screen-centered
        }

        private void OnBirdClicked(int index)
        {
            var birds = BirdPool.All;
            if (!collection.IsOwned(birds[index].Id)) return;
            collection.Select(birds[index].Id);
            audio?.PlayClick();
        }

        private void OnEggButtonClicked()
        {
            bool allOwned = collection.OwnedBirdIds.Count >= BirdPool.All.Length;
            if (allOwned) return;

            audio?.PlayClick();
            var hatched = collection.BuyEgg();
            if (hatched.HasValue)
            {
                hatchMessage = $"부화! {hatched.Value.Name} 획득";
                hatchMessageTimeLeft = 2.5f;
            }
            // null means funds were short -- button just stays available.
        }

        private void Update()
        {
            if (hatchMessageTimeLeft > 0f) hatchMessageTimeLeft -= Time.deltaTime;

            var gm = GameManager.Instance;
            bool active = gm.State == GameState.Start;
            if (root.activeSelf != active) root.SetActive(active);
            if (!active) return;

            if (Screen.width != lastWidth || Screen.height != lastHeight)
            {
                lastWidth = Screen.width;
                lastHeight = Screen.height;
                ReflowLayout();
            }

            RefreshContent();

            if (InputService.IsSpaceDownThisFrame())
                gm.BeginRun();
        }

        // Repositions everything whose placement depends on Screen.width/
        // height -- only runs when the resolution actually changed (window
        // resize / orientation change), not every frame.
        private void ReflowLayout()
        {
            float cx = Screen.width * 0.5f;
            float cy = Screen.height * 0.5f;

            UIFactory.SetTopLeftCentered(titleRect, cx - 300f, cy - 150f, 600f, 60f);
            UIFactory.SetTopLeftCentered(sub1Rect, cx - 300f, cy - 80f, 600f, 30f);
            UIFactory.SetTopLeftCentered(sub2Rect, cx - 300f, cy - 50f, 600f, 30f);

            UIFactory.SetTopLeftCentered((RectTransform)coinsText.transform, Screen.width - 160f, 16f, 140f, 26f);
            var leaderboardToggle = baseContent.transform.Find("LeaderboardToggle");
            if (leaderboardToggle != null) UIFactory.SetTopLeft((RectTransform)leaderboardToggle, Screen.width - 160f, 48f, 140f, 26f);

            UIFactory.SetTopLeftCentered((RectTransform)eggButton.transform, cx - 100f, Screen.height - 56f, 200f, 40f);
            UIFactory.SetTopLeftCentered((RectTransform)hatchText.transform, cx - 200f, Screen.height - 82f, 400f, 22f);
            UIFactory.SetTopLeftCentered((RectTransform)bestText.transform, cx - 300f, cy - 10f, 600f, 26f);

            var birds = BirdPool.All;
            const float iconSize = 46f, spacing = 10f;
            float totalW = birds.Length * iconSize + (birds.Length - 1) * spacing;
            float startX = cx - totalW * 0.5f;
            float y = Screen.height - 116f;

            for (int i = 0; i < birds.Length; i++)
            {
                float x = startX + i * (iconSize + spacing);
                UIFactory.SetTopLeft(birdIconRects[i], x, y, iconSize, iconSize);
                UIFactory.SetTopLeft((RectTransform)birdSelectionBorders[i].transform, x - 3f, y - 3f, iconSize + 6f, iconSize + 6f);
            }
            UIFactory.SetTopLeftCentered((RectTransform)birdNameText.transform, cx - 300f, y - 22f, 600f, 20f);

            const float dailyPanelW = 260f;
            float dailyPanelH = 26f + MaxDailyMissionLines * 24f;
            UIFactory.SetTopLeft((RectTransform)dailyPanel.transform, 16f, Screen.height - dailyPanelH - 16f, dailyPanelW, dailyPanelH);

            var leaderboardPanel = leaderboardGroup.transform.Find("Panel");
            if (leaderboardPanel != null)
                UIFactory.SetTopLeftCentered((RectTransform)leaderboardPanel, cx - 220f, cy - 240f, 440f, 480f);
        }

        private void RefreshContent()
        {
            if (SaveSystem.Instance != null && SaveSystem.Instance.BestScore > 0)
            {
                bestText.gameObject.SetActive(true);
                bestText.text = $"Best: {SaveSystem.Instance.BestScore:N0}";
            }
            else
            {
                bestText.gameObject.SetActive(false);
            }

            if (wallet != null)
            {
                coinsText.gameObject.SetActive(true);
                coinsText.text = $"Coins: {wallet.Coins:N0}";
            }

            showLeaderboard = leaderboardGroup.activeSelf;

            if (!showLeaderboard)
            {
                RefreshBirdRow();
                RefreshDailyMissions();
            }
            else
            {
                RefreshLeaderboard();
            }
        }

        private void RefreshBirdRow()
        {
            if (collection == null) return;

            var birds = BirdPool.All;
            for (int i = 0; i < birds.Length; i++)
            {
                bool owned = collection.IsOwned(birds[i].Id);
                bool selected = collection.SelectedBirdId == birds[i].Id;

                birdSelectionBorders[i].gameObject.SetActive(selected);
                birdButtons[i].image.color = owned ? Color.white : new Color(0.35f, 0.35f, 0.35f, 0.85f);
                birdLockTexts[i].gameObject.SetActive(!owned);
            }

            var selectedBird = collection.SelectedBird;
            birdNameText.text = selectedBird.Perk == PerkType.None ? selectedBird.Name : $"{selectedBird.Name} · {selectedBird.PerkDescription}";

            bool allOwned = collection.OwnedBirdIds.Count >= BirdPool.All.Length;
            eggButton.interactable = !allOwned;
            eggButtonText.text = allOwned ? "새를 모두 모았어요" : $"알 구매 ({BirdPool.EggCostCoins} 코인)";

            if (hatchMessageTimeLeft > 0f)
            {
                hatchText.gameObject.SetActive(true);
                hatchText.text = hatchMessage;
            }
            else
            {
                hatchText.gameObject.SetActive(false);
            }
        }

        private void RefreshDailyMissions()
        {
            if (dailyMissions == null)
            {
                dailyPanel.SetActive(false);
                return;
            }

            dailyPanel.SetActive(true);
            var missions = dailyMissions.ActiveMissions;
            for (int i = 0; i < MaxDailyMissionLines; i++)
            {
                if (i >= missions.Length) { dailyLines[i].gameObject.SetActive(false); continue; }

                dailyLines[i].gameObject.SetActive(true);
                bool done = dailyMissions.Completed[i];
                dailyLines[i].color = done ? new Color(0.5f, 1f, 0.5f) : new Color(1f, 1f, 1f, 0.85f);
                string mark = done ? "✓" : $"{dailyMissions.Progress[i]}/{missions[i].Target}";
                dailyLines[i].text = $"{missions[i].Description} ({mark})";
            }
        }

        private void RefreshLeaderboard()
        {
            if (leaderboard == null) return;

            var scores = leaderboard.TopScores;
            int line = 0;

            if (scores.Count == 0)
            {
                leaderboardLines[line].gameObject.SetActive(true);
                leaderboardLines[line].text = "아직 기록이 없어요";
                line++;
            }
            else
            {
                for (int i = 0; i < scores.Count && line < MaxLeaderboardLines; i++, line++)
                {
                    leaderboardLines[line].gameObject.SetActive(true);
                    leaderboardLines[line].text = $"{i + 1}.  {scores[i]:N0}";
                }
            }

            if (line < MaxLeaderboardLines)
            {
                leaderboardLines[line].gameObject.SetActive(true);
                leaderboardLines[line].text = $"총 슬라이드: {leaderboard.TotalSlidesAllTime:N0}";
                line++;
            }
            if (line < MaxLeaderboardLines)
            {
                leaderboardLines[line].gameObject.SetActive(true);
                leaderboardLines[line].text = $"총 비행일 수: {leaderboard.TotalRuns:N0}";
                line++;
            }

            for (; line < MaxLeaderboardLines; line++)
                leaderboardLines[line].gameObject.SetActive(false);
        }
    }
}
