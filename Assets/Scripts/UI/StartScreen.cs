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
    // InputService.IsSpaceDownThisFrame() (skipped while the nickname field
    // has focus, so typing a space into a nickname doesn't also start a run).
    //
    // Post-M7: nickname (top-left, editable TMP_InputField + reroll button,
    // PlayerProfile) and a Korean/English language toggle (top-right, next
    // to the coin/leaderboard buttons). Most of this screen's text was
    // already refreshed every frame in RefreshContent() and friends, so it
    // automatically picks up a language change; the handful of labels that
    // were only ever set once in Build*() are re-applied by
    // RefreshStaticLabels(), called once at build time and again whenever
    // Localization.OnLanguageChanged fires.
    public class StartScreen : MonoBehaviour
    {
        private const int MaxDailyMissionLines = 5;
        private const int MaxLeaderboardLines = 12;

        private CoinWallet wallet;
        private DailyMissions dailyMissions;
        private BirdCollection collection;
        private Leaderboard leaderboard;
        private AudioManager audio;
        private PlayerProfile profile;
        private AuthService auth;

        private bool showLeaderboard;
        private string hatchMessage;
        private float hatchMessageTimeLeft;

        private GameObject root;
        private GameObject baseContent;
        private GameObject leaderboardGroup;

        private RectTransform titleRect, sub1Rect, sub2Rect;
        private TextMeshProUGUI sub1Text, sub2Text;
        private TextMeshProUGUI bestText;
        private TextMeshProUGUI coinsText;
        private Button eggButton;
        private TextMeshProUGUI eggButtonText;
        private TextMeshProUGUI hatchText;
        private TextMeshProUGUI birdNameText;

        private TMP_InputField nicknameField;
        private Button rerollButton;
        private TextMeshProUGUI rerollButtonText;
        private Button languageToggleButton;
        private TextMeshProUGUI languageToggleText;

        private Button leaderboardToggleButton;
        private TextMeshProUGUI leaderboardToggleText;

        // Online account (FlyingChick-Server, optional -- see AuthService).
        // authStatusButton doubles as both the "log in" entry point (logged
        // out) and the "log out" action (logged in, showing the server
        // nickname in its own label) so only one button slot is needed.
        private GameObject authFormGroup;
        private Button authStatusButton;
        private TextMeshProUGUI authStatusButtonText;
        private TextMeshProUGUI authLoginIdLabelText, authPasswordLabelText;
        private TMP_InputField authLoginIdField, authPasswordField;
        private TextMeshProUGUI authErrorText;
        private Button authLoginButton, authSignupButton, authCloseButton;
        private TextMeshProUGUI authLoginButtonText, authSignupButtonText, authCloseButtonText;

        private Button[] birdButtons;
        private Image[] birdSelectionBorders;
        private TextMeshProUGUI[] birdLockTexts;
        private RectTransform[] birdIconRects;

        private GameObject dailyPanel;
        private TextMeshProUGUI dailyHeaderText;
        private TextMeshProUGUI[] dailyLines;

        private TextMeshProUGUI leaderboardHeaderText;
        private TextMeshProUGUI[] leaderboardLines;
        private Button leaderboardCloseButton;
        private TextMeshProUGUI leaderboardCloseText;

        private int lastWidth = -1, lastHeight = -1;

        public void Bind(CoinWallet walletRef, DailyMissions dailyMissionsRef, BirdCollection collectionRef, Leaderboard leaderboardRef, AudioManager audioRef, PlayerProfile profileRef, AuthService authRef)
        {
            wallet = walletRef;
            dailyMissions = dailyMissionsRef;
            collection = collectionRef;
            leaderboard = leaderboardRef;
            audio = audioRef;
            profile = profileRef;
            auth = authRef;

            BuildUI();

            if (profile != null) profile.OnNicknameChanged += HandleNicknameChanged;
            Localization.OnLanguageChanged += RefreshStaticLabels;
            if (auth != null)
            {
                auth.OnLoggedIn += HandleLoggedIn;
                auth.OnAuthError += HandleAuthError;
            }
        }

        private void OnDestroy()
        {
            if (profile != null) profile.OnNicknameChanged -= HandleNicknameChanged;
            Localization.OnLanguageChanged -= RefreshStaticLabels;
            if (auth != null)
            {
                auth.OnLoggedIn -= HandleLoggedIn;
                auth.OnAuthError -= HandleAuthError;
            }
        }

        private void HandleNicknameChanged()
        {
            if (nicknameField != null) nicknameField.text = profile.Nickname;
        }

        private void HandleLoggedIn()
        {
            authFormGroup.SetActive(false);
            authErrorText.gameObject.SetActive(false);
        }

        private void HandleAuthError(string message)
        {
            authErrorText.gameObject.SetActive(true);
            authErrorText.text = message;
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

            authFormGroup = UIFactory.CreateChild(t, "AuthFormGroup").gameObject;
            BuildAuthFormGroup(authFormGroup.transform);
            authFormGroup.SetActive(false);

            lastWidth = Screen.width;
            lastHeight = Screen.height;
            ReflowLayout();
            RefreshStaticLabels();
        }

        private void BuildBaseContent(Transform parent)
        {
            var brown = new Color(0.42f, 0.29f, 0.12f);

            var title = UIFactory.CreateText(parent, "Title", 48, new Color(0.36f, 0.24f, 0.1f), TextAlignmentOptions.Center, FontStyles.Bold);
            titleRect = (RectTransform)title.transform;
            title.text = "Flying Chick";

            sub1Text = UIFactory.CreateText(parent, "Sub1", 18, brown, TextAlignmentOptions.Center);
            sub1Rect = (RectTransform)sub1Text.transform;

            sub2Text = UIFactory.CreateText(parent, "Sub2", 18, brown, TextAlignmentOptions.Center);
            sub2Rect = (RectTransform)sub2Text.transform;

            bestText = UIFactory.CreateText(parent, "Best", 18, brown, TextAlignmentOptions.Center);

            coinsText = UIFactory.CreateText(parent, "Coins", 18, new Color(0.85f, 0.6f, 0.1f), TextAlignmentOptions.TopLeft, FontStyles.Bold);

            BuildNicknameRow(parent, brown);

            leaderboardToggleButton = UIFactory.CreateButton(parent, "LeaderboardToggle", "", 15, brown, out leaderboardToggleText);
            leaderboardToggleButton.onClick.AddListener(() =>
            {
                baseContent.SetActive(false);
                leaderboardGroup.SetActive(true);
                audio?.PlayClick();
            });

            languageToggleButton = UIFactory.CreateButton(parent, "LanguageToggle", "", 15, brown, out languageToggleText);
            languageToggleButton.onClick.AddListener(() =>
            {
                Localization.Current = Localization.Current == Language.Korean ? Language.English : Language.Korean;
                audio?.PlayClick();
            });

            authStatusButton = UIFactory.CreateButton(parent, "AuthStatusButton", "", 15, brown, out authStatusButtonText);
            authStatusButton.onClick.AddListener(() =>
            {
                audio?.PlayClick();
                if (auth != null && auth.IsLoggedIn) auth.Logout();
                else authFormGroup.SetActive(true);
            });

            eggButton = UIFactory.CreateButton(parent, "EggButton", "", 15, brown, out eggButtonText);
            eggButton.onClick.AddListener(OnEggButtonClicked);

            hatchText = UIFactory.CreateText(parent, "HatchMessage", 15, new Color(1f, 0.6f, 0.15f), TextAlignmentOptions.Center, FontStyles.Bold);
            hatchText.gameObject.SetActive(false);

            birdNameText = UIFactory.CreateText(parent, "BirdName", 13, brown, TextAlignmentOptions.Center);

            BuildBirdRow(parent);
            BuildDailyMissionsPanel(parent);
        }

        private void BuildNicknameRow(Transform parent, Color brown)
        {
            nicknameField = UIFactory.CreateInputField(parent, "NicknameField", 15, brown, 16);
            nicknameField.text = profile != null ? profile.Nickname : "";
            nicknameField.onEndEdit.AddListener(value =>
            {
                profile?.SetNickname(value);
                audio?.PlayClick();
            });

            rerollButton = UIFactory.CreateButton(parent, "RerollButton", "", 13, brown, out rerollButtonText);
            rerollButton.onClick.AddListener(() =>
            {
                profile?.Reroll();
                audio?.PlayClick();
            });
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

            dailyHeaderText = UIFactory.CreateText(panel.transform, "DailyHeader", 15, Color.white, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            UIFactory.SetTopLeft((RectTransform)dailyHeaderText.transform, 10f, 4f, 240f, 20f);

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

            leaderboardHeaderText = UIFactory.CreateText(panel.transform, "Header", 24, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            UIFactory.SetTopLeft((RectTransform)leaderboardHeaderText.transform, 0f, 14f, 440f, 34f);

            leaderboardLines = new TextMeshProUGUI[MaxLeaderboardLines];
            for (int i = 0; i < MaxLeaderboardLines; i++)
            {
                var line = UIFactory.CreateText(panel.transform, $"Line{i}", 16, new Color(1f, 1f, 1f, 0.9f));
                UIFactory.SetTopLeft((RectTransform)line.transform, 24f, 56f + i * 24f, 392f, 22f);
                leaderboardLines[i] = line;
            }

            leaderboardCloseButton = UIFactory.CreateButton(panel.transform, "CloseButton", "", 16, new Color(0.3f, 0.2f, 0.1f), out leaderboardCloseText);
            UIFactory.SetTopLeftCentered((RectTransform)leaderboardCloseButton.transform, 220f - 60f, 480f - 50f, 120f, 36f);
            leaderboardCloseButton.onClick.AddListener(() =>
            {
                leaderboardGroup.SetActive(false);
                baseContent.SetActive(true);
                audio?.PlayClick();
            });

            UIFactory.SetTopLeftCentered(panelRect, 0f, 0f, 440f, 480f); // repositioned in Update to stay screen-centered
        }

        // Combined login/signup form -- one pair of fields, two action
        // buttons. All child positions here are relative to the panel's own
        // rect (its top-left corner), same convention as
        // BuildLeaderboardGroup -- only the panel itself gets recentered on
        // resize (see ReflowLayout), its children never move relative to it.
        private void BuildAuthFormGroup(Transform parent)
        {
            var backdrop = UIFactory.CreatePanel(parent, "Backdrop", new Color(0f, 0f, 0f, 0f));
            backdrop.raycastTarget = true;
            UIFactory.StretchFull((RectTransform)backdrop.transform);

            var panel = UIFactory.CreatePanel(parent, "Panel", new Color(0.15f, 0.1f, 0.08f, 0.9f));
            var panelRect = (RectTransform)panel.transform;

            var white = Color.white;
            var dark = new Color(0.15f, 0.15f, 0.15f);

            authLoginIdLabelText = UIFactory.CreateText(panel.transform, "LoginIdLabel", 14, white);
            UIFactory.SetTopLeft((RectTransform)authLoginIdLabelText.transform, 24f, 20f, 200f, 20f);

            authLoginIdField = UIFactory.CreateInputField(panel.transform, "LoginIdField", 15, dark, 255);
            UIFactory.SetTopLeft((RectTransform)authLoginIdField.transform, 24f, 44f, 312f, 36f);

            authPasswordLabelText = UIFactory.CreateText(panel.transform, "PasswordLabel", 14, white);
            UIFactory.SetTopLeft((RectTransform)authPasswordLabelText.transform, 24f, 92f, 200f, 20f);

            authPasswordField = UIFactory.CreateInputField(panel.transform, "PasswordField", 15, dark, 128, password: true);
            UIFactory.SetTopLeft((RectTransform)authPasswordField.transform, 24f, 116f, 312f, 36f);

            authErrorText = UIFactory.CreateText(panel.transform, "ErrorText", 13, new Color(1f, 0.45f, 0.45f), TextAlignmentOptions.Center);
            UIFactory.SetTopLeft((RectTransform)authErrorText.transform, 24f, 158f, 312f, 36f);
            authErrorText.gameObject.SetActive(false);

            authLoginButton = UIFactory.CreateButton(panel.transform, "LoginButton", "", 15, new Color(0.3f, 0.2f, 0.1f), out authLoginButtonText);
            UIFactory.SetTopLeft((RectTransform)authLoginButton.transform, 24f, 210f, 150f, 42f);
            authLoginButton.onClick.AddListener(() =>
            {
                audio?.PlayClick();
                auth?.Login(authLoginIdField.text, authPasswordField.text);
            });

            authSignupButton = UIFactory.CreateButton(panel.transform, "SignupButton", "", 15, new Color(0.3f, 0.2f, 0.1f), out authSignupButtonText);
            UIFactory.SetTopLeft((RectTransform)authSignupButton.transform, 186f, 210f, 150f, 42f);
            authSignupButton.onClick.AddListener(() =>
            {
                audio?.PlayClick();
                auth?.Signup(authLoginIdField.text, authPasswordField.text);
            });

            authCloseButton = UIFactory.CreateButton(panel.transform, "CloseButton", "", 16, new Color(0.3f, 0.2f, 0.1f), out authCloseButtonText);
            UIFactory.SetTopLeft((RectTransform)authCloseButton.transform, 120f, 268f, 120f, 36f);
            authCloseButton.onClick.AddListener(() =>
            {
                authFormGroup.SetActive(false);
                audio?.PlayClick();
            });

            UIFactory.SetTopLeftCentered(panelRect, 0f, 0f, 360f, 324f); // repositioned in ReflowLayout to stay screen-centered
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
                hatchMessage = string.Format(Localization.Get("start.hatchMessage"), hatched.Value.Name);
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

            bool typing = (nicknameField != null && nicknameField.isFocused)
                || (authLoginIdField != null && authLoginIdField.isFocused)
                || (authPasswordField != null && authPasswordField.isFocused);
            if (!typing && InputService.IsSpaceDownThisFrame())
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

            UIFactory.SetTopLeft((RectTransform)nicknameField.transform, 16f, 16f, 180f, 32f);
            UIFactory.SetTopLeft((RectTransform)rerollButton.transform, 204f, 16f, 64f, 32f);

            UIFactory.SetTopLeftCentered((RectTransform)coinsText.transform, Screen.width - 160f, 16f, 140f, 26f);
            UIFactory.SetTopLeft((RectTransform)leaderboardToggleButton.transform, Screen.width - 160f, 48f, 140f, 26f);
            UIFactory.SetTopLeft((RectTransform)languageToggleButton.transform, Screen.width - 160f, 80f, 140f, 26f);
            UIFactory.SetTopLeft((RectTransform)authStatusButton.transform, Screen.width - 160f, 112f, 140f, 26f);

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

            var authPanel = authFormGroup.transform.Find("Panel");
            if (authPanel != null)
                UIFactory.SetTopLeftCentered((RectTransform)authPanel, cx - 180f, cy - 162f, 360f, 324f);
        }

        // Labels that are only ever set here (never touched again by
        // per-frame refresh code) -- re-applied whenever the language
        // changes so they don't stay stuck in the old language. Everything
        // else (bird names, mission descriptions, leaderboard rows, ...)
        // already gets its text reassigned every frame in RefreshContent()
        // and friends, so it picks up a language change on its own.
        private void RefreshStaticLabels()
        {
            sub1Text.text = Localization.Get("start.subtitle1");
            sub2Text.text = Localization.Get("start.subtitle2");
            leaderboardToggleText.text = Localization.Get("start.leaderboardButton");
            dailyHeaderText.text = Localization.Get("start.dailyMissionsHeader");
            leaderboardHeaderText.text = Localization.Get("leaderboard.header");
            leaderboardCloseText.text = Localization.Get("leaderboard.close");
            rerollButtonText.text = Localization.Get("start.nicknameReroll");
            languageToggleText.text = Localization.Current == Language.Korean ? "English" : "한국어";

            authLoginIdLabelText.text = Localization.Get("auth.loginIdLabel");
            authPasswordLabelText.text = Localization.Get("auth.passwordLabel");
            authLoginButtonText.text = Localization.Get("auth.loginButton");
            authSignupButtonText.text = Localization.Get("auth.signupButton");
            authCloseButtonText.text = Localization.Get("leaderboard.close");
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

            authStatusButtonText.text = (auth != null && auth.IsLoggedIn)
                ? $"{auth.ServerNickname} · {Localization.Get("auth.logoutButton")}"
                : Localization.Get("auth.loginButton");

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
            eggButtonText.text = allOwned
                ? Localization.Get("start.eggButtonAllOwned")
                : string.Format(Localization.Get("start.eggButtonBuy"), BirdPool.EggCostCoins);

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
                string mark = done ? "O" : $"{dailyMissions.Progress[i]}/{missions[i].Target}";
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
                leaderboardLines[line].text = Localization.Get("leaderboard.empty");
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
                leaderboardLines[line].text = string.Format(Localization.Get("leaderboard.totalSlides"), leaderboard.TotalSlidesAllTime);
                line++;
            }
            if (line < MaxLeaderboardLines)
            {
                leaderboardLines[line].gameObject.SetActive(true);
                leaderboardLines[line].text = string.Format(Localization.Get("leaderboard.totalRuns"), leaderboard.TotalRuns);
                line++;
            }

            for (; line < MaxLeaderboardLines; line++)
                leaderboardLines[line].gameObject.SetActive(false);
        }
    }
}
