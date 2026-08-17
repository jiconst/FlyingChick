using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FlyingChick
{
    // 시작 화면. GameState.Start일 때만 보임. 메인 메뉴(타이틀 + 4개 버튼) +
    // 4개 하위 화면(게임플레이/설정/게임 방법/기록)으로 구성 — currentPanel
    // 하나로 어떤 화면이 떠 있는지 관리하고, SwitchTo()로만 전환함.
    //
    // "빈 곳 탭하면 시작" 동작은 게임플레이 화면에서만 필요하므로, tap
    // catcher를 캔버스 루트가 아니라 playGroup의 자식으로 만듦 — playGroup이
    // 비활성화되면 tap catcher도 같이 없어지니 별도 상태 체크가 필요 없음
    // (버튼이 tap catcher보다 나중 형제로 그려져서 위에 있는 걸 먼저 받는
    // 원리는 기존과 동일). 마우스/터치는 이 방식으로 자동 처리되고,
    // 스페이스바는 UGUI 클릭 경로가 없어서 Update()에서 currentPanel ==
    // Panel.Play일 때만 직접 폴링함.
    //
    // 로그인/회원가입 폼, 기록(리더보드) 패널은 기존 구조를 그대로 재사용 —
    // 다만 여닫히는 지점만 메인 메뉴 기준으로 바뀜.
    public class StartScreen : MonoBehaviour
    {
        private const int MaxDailyMissionLines = 5;
        private const int MaxLeaderboardLines = 12;

        private enum Panel { MainMenu, Play, Settings, HowToPlay, Leaderboard }

        private CoinWallet wallet;
        private DailyMissions dailyMissions;
        private BirdCollection collection;
        private Leaderboard leaderboard;
        private AudioManager audio;
        private PlayerProfile profile;
        private AuthService auth;

        private Panel currentPanel = Panel.MainMenu;
        private string hatchMessage;
        private float hatchMessageTimeLeft;

        private GameObject root;
        private GameObject mainMenuGroup;
        private GameObject playGroup;
        private GameObject settingsGroup;
        private GameObject howToPlayGroup;
        private GameObject leaderboardGroup;
        private GameObject authFormGroup;

        // 메인 메뉴
        private RectTransform titleRect;
        private TextMeshProUGUI bestText;
        private Button playButton, settingsButton, howToPlayButton, leaderboardMenuButton;
        private TextMeshProUGUI playButtonText, settingsButtonText, howToPlayButtonText, leaderboardMenuButtonText;

        // 게임플레이 화면
        private RectTransform hintRect;
        private TextMeshProUGUI hintText;
        private TextMeshProUGUI coinsText;
        private Button eggButton;
        private TextMeshProUGUI eggButtonText;
        private TextMeshProUGUI hatchText;
        private TextMeshProUGUI birdNameText;
        private TMP_InputField nicknameField;
        private Button rerollButton;
        private TextMeshProUGUI rerollButtonText;
        private Button playBackButton;
        private TextMeshProUGUI playBackButtonText;

        private Button[] birdButtons;
        private Image[] birdSelectionBorders;
        private TextMeshProUGUI[] birdLockTexts;
        private RectTransform[] birdIconRects;

        private GameObject dailyPanel;
        private TextMeshProUGUI dailyHeaderText;
        private TextMeshProUGUI[] dailyLines;

        // 설정 화면
        private TextMeshProUGUI musicLabelText, sfxLabelText;
        private Slider musicSlider, sfxSlider;
        private Button languageToggleButton;
        private TextMeshProUGUI languageToggleText;
        private Button authStatusButton;
        private TextMeshProUGUI authStatusButtonText;
        private Button settingsBackButton;
        private TextMeshProUGUI settingsBackButtonText;

        // 게임 방법 화면
        private TextMeshProUGUI howToPlayTitleText;
        private TextMeshProUGUI howToPlayBodyText;
        private Button howToPlayBackButton;
        private TextMeshProUGUI howToPlayBackButtonText;

        // 기록(리더보드) — 기존 그대로
        private TextMeshProUGUI leaderboardHeaderText;
        private TextMeshProUGUI[] leaderboardLines;
        private Button leaderboardCloseButton;
        private TextMeshProUGUI leaderboardCloseText;

        // 로그인/회원가입 폼 — 기존 그대로, 설정 화면에서 열림
        private TextMeshProUGUI authLoginIdLabelText, authPasswordLabelText;
        private TMP_InputField authLoginIdField, authPasswordField;
        private TextMeshProUGUI authErrorText;
        private Button authLoginButton, authSignupButton, authCloseButton;
        private TextMeshProUGUI authLoginButtonText, authSignupButtonText, authCloseButtonText;

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

            mainMenuGroup = UIFactory.CreateChild(t, "MainMenuGroup").gameObject;
            BuildMainMenu(mainMenuGroup.transform);

            playGroup = UIFactory.CreateChild(t, "PlayGroup").gameObject;
            BuildPlayPanel(playGroup.transform);

            settingsGroup = UIFactory.CreateChild(t, "SettingsGroup").gameObject;
            BuildSettingsPanel(settingsGroup.transform);

            howToPlayGroup = UIFactory.CreateChild(t, "HowToPlayGroup").gameObject;
            BuildHowToPlayPanel(howToPlayGroup.transform);

            leaderboardGroup = UIFactory.CreateChild(t, "LeaderboardGroup").gameObject;
            BuildLeaderboardGroup(leaderboardGroup.transform);

            authFormGroup = UIFactory.CreateChild(t, "AuthFormGroup").gameObject;
            BuildAuthFormGroup(authFormGroup.transform);
            authFormGroup.SetActive(false);

            lastWidth = Screen.width;
            lastHeight = Screen.height;
            ReflowLayout();
            RefreshStaticLabels();
            SwitchTo(Panel.MainMenu);
        }

        // 다섯 그룹(메인메뉴/게임플레이/설정/게임방법/기록) 중 하나만 활성화 —
        // authFormGroup은 이 상태 머신과 별개로 설정 화면 위에 뜨는 모달.
        private void SwitchTo(Panel panel)
        {
            currentPanel = panel;
            mainMenuGroup.SetActive(panel == Panel.MainMenu);
            playGroup.SetActive(panel == Panel.Play);
            settingsGroup.SetActive(panel == Panel.Settings);
            howToPlayGroup.SetActive(panel == Panel.HowToPlay);
            leaderboardGroup.SetActive(panel == Panel.Leaderboard);
        }

        private void BuildMainMenu(Transform parent)
        {
            var brown = new Color(0.42f, 0.29f, 0.12f);

            var title = UIFactory.CreateText(parent, "Title", 48, new Color(0.36f, 0.24f, 0.1f), TextAlignmentOptions.Center, FontStyles.Bold);
            titleRect = (RectTransform)title.transform;
            title.text = "Flying Chick";

            bestText = UIFactory.CreateText(parent, "Best", 18, brown, TextAlignmentOptions.Center);

            playButton = UIFactory.CreateButton(parent, "PlayButton", "", 20, brown, out playButtonText);
            playButton.onClick.AddListener(() => { audio?.PlayClick(); SwitchTo(Panel.Play); });

            settingsButton = UIFactory.CreateButton(parent, "SettingsButton", "", 20, brown, out settingsButtonText);
            settingsButton.onClick.AddListener(() => { audio?.PlayClick(); SwitchTo(Panel.Settings); });

            howToPlayButton = UIFactory.CreateButton(parent, "HowToPlayButton", "", 20, brown, out howToPlayButtonText);
            howToPlayButton.onClick.AddListener(() => { audio?.PlayClick(); SwitchTo(Panel.HowToPlay); });

            leaderboardMenuButton = UIFactory.CreateButton(parent, "LeaderboardMenuButton", "", 20, brown, out leaderboardMenuButtonText);
            leaderboardMenuButton.onClick.AddListener(() => { audio?.PlayClick(); SwitchTo(Panel.Leaderboard); });
        }

        private void BuildPlayPanel(Transform parent)
        {
            var brown = new Color(0.42f, 0.29f, 0.12f);

            var tapCatcher = UIFactory.CreateFullScreenTapCatcher(parent, "TapCatcher");
            tapCatcher.onClick.AddListener(() => GameManager.Instance.BeginRun());

            hintText = UIFactory.CreateText(parent, "Hint", 18, brown, TextAlignmentOptions.Center);
            hintRect = (RectTransform)hintText.transform;

            playBackButton = UIFactory.CreateButton(parent, "PlayBackButton", "", 15, brown, out playBackButtonText);
            playBackButton.onClick.AddListener(() => { audio?.PlayClick(); SwitchTo(Panel.MainMenu); });

            coinsText = UIFactory.CreateText(parent, "Coins", 18, new Color(0.85f, 0.6f, 0.1f), TextAlignmentOptions.TopLeft, FontStyles.Bold);

            BuildNicknameRow(parent, brown);

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

        private void BuildSettingsPanel(Transform parent)
        {
            var brown = new Color(0.42f, 0.29f, 0.12f);

            musicLabelText = UIFactory.CreateText(parent, "MusicLabel", 16, brown);
            musicSlider = UIFactory.CreateSlider(parent, "MusicSlider", audio != null ? audio.MusicVolume : 0.16f);
            musicSlider.onValueChanged.AddListener(v => audio?.SetMusicVolume(v));

            sfxLabelText = UIFactory.CreateText(parent, "SfxLabel", 16, brown);
            sfxSlider = UIFactory.CreateSlider(parent, "SfxSlider", audio != null ? audio.SfxVolume : 0.6f);
            sfxSlider.onValueChanged.AddListener(v => audio?.SetSfxVolume(v));

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

            settingsBackButton = UIFactory.CreateButton(parent, "SettingsBackButton", "", 15, brown, out settingsBackButtonText);
            settingsBackButton.onClick.AddListener(() => { audio?.PlayClick(); SwitchTo(Panel.MainMenu); });
        }

        private void BuildHowToPlayPanel(Transform parent)
        {
            var brown = new Color(0.42f, 0.29f, 0.12f);

            howToPlayTitleText = UIFactory.CreateText(parent, "Title", 32, new Color(0.36f, 0.24f, 0.1f), TextAlignmentOptions.Center, FontStyles.Bold);

            howToPlayBodyText = UIFactory.CreateText(parent, "Body", 16, brown, TextAlignmentOptions.TopLeft);

            howToPlayBackButton = UIFactory.CreateButton(parent, "HowToPlayBackButton", "", 15, brown, out howToPlayBackButtonText);
            howToPlayBackButton.onClick.AddListener(() => { audio?.PlayClick(); SwitchTo(Panel.MainMenu); });
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
                audio?.PlayClick();
                SwitchTo(Panel.MainMenu);
            });

            UIFactory.SetTopLeftCentered(panelRect, 0f, 0f, 440f, 480f); // ReflowLayout에서 화면 중앙으로 재배치됨
        }

        // 로그인/회원가입 폼 — 필드 하나(아이디+비밀번호)를 두 버튼이 같이 씀.
        // 자식 위치는 전부 이 패널 자신의 rect 기준(패널만 ReflowLayout에서
        // 화면 중앙으로 재배치되고, 자식들은 안 움직임) — BuildLeaderboardGroup과
        // 같은 관례.
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

            UIFactory.SetTopLeftCentered(panelRect, 0f, 0f, 360f, 324f); // ReflowLayout에서 화면 중앙으로 재배치됨
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
            // null이면 코인이 부족했다는 뜻 — 버튼은 그대로 눌러볼 수 있게 둠.
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
            if (currentPanel == Panel.Play && !typing && InputService.IsSpaceDownThisFrame())
                gm.BeginRun();
        }

        // 화면 크기가 실제로 바뀔 때만 호출됨(리사이즈/화면 회전) — 매 프레임
        // 아님.
        private void ReflowLayout()
        {
            float cx = Screen.width * 0.5f;
            float cy = Screen.height * 0.5f;

            ReflowMainMenu(cx, cy);
            ReflowPlayPanel(cx, cy);
            ReflowSettingsPanel(cx, cy);
            ReflowHowToPlayPanel(cx, cy);

            var leaderboardPanel = leaderboardGroup.transform.Find("Panel");
            if (leaderboardPanel != null)
                UIFactory.SetTopLeftCentered((RectTransform)leaderboardPanel, cx - 220f, cy - 240f, 440f, 480f);

            var authPanel = authFormGroup.transform.Find("Panel");
            if (authPanel != null)
                UIFactory.SetTopLeftCentered((RectTransform)authPanel, cx - 180f, cy - 162f, 360f, 324f);
        }

        private void ReflowMainMenu(float cx, float cy)
        {
            UIFactory.SetTopLeftCentered(titleRect, cx - 300f, cy - 220f, 600f, 60f);
            UIFactory.SetTopLeftCentered((RectTransform)bestText.transform, cx - 300f, cy - 150f, 600f, 26f);

            const float btnW = 260f, btnH = 52f, gap = 16f;
            float y = cy - 90f;
            UIFactory.SetTopLeftCentered((RectTransform)playButton.transform, cx - btnW * 0.5f, y, btnW, btnH);
            y += btnH + gap;
            UIFactory.SetTopLeftCentered((RectTransform)settingsButton.transform, cx - btnW * 0.5f, y, btnW, btnH);
            y += btnH + gap;
            UIFactory.SetTopLeftCentered((RectTransform)howToPlayButton.transform, cx - btnW * 0.5f, y, btnW, btnH);
            y += btnH + gap;
            UIFactory.SetTopLeftCentered((RectTransform)leaderboardMenuButton.transform, cx - btnW * 0.5f, y, btnW, btnH);
        }

        private void ReflowPlayPanel(float cx, float cy)
        {
            UIFactory.SetTopLeftCentered(hintRect, cx - 300f, 60f, 600f, 30f);

            UIFactory.SetTopLeft((RectTransform)nicknameField.transform, 16f, 16f, 180f, 32f);
            UIFactory.SetTopLeft((RectTransform)rerollButton.transform, 204f, 16f, 64f, 32f);

            UIFactory.SetTopLeftCentered((RectTransform)coinsText.transform, Screen.width - 160f, 16f, 140f, 26f);
            UIFactory.SetTopLeft((RectTransform)playBackButton.transform, Screen.width - 160f, 48f, 140f, 26f);

            UIFactory.SetTopLeftCentered((RectTransform)eggButton.transform, cx - 100f, Screen.height - 56f, 200f, 40f);
            UIFactory.SetTopLeftCentered((RectTransform)hatchText.transform, cx - 200f, Screen.height - 82f, 400f, 22f);

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
        }

        private void ReflowSettingsPanel(float cx, float cy)
        {
            const float rowW = 340f, labelW = 90f;
            float sliderW = rowW - labelW - 10f;
            float x = cx - rowW * 0.5f;

            float y = cy - 160f;
            UIFactory.SetTopLeft((RectTransform)musicLabelText.transform, x, y, labelW, 26f);
            UIFactory.SetTopLeft((RectTransform)musicSlider.transform, x + labelW + 10f, y + 3f, sliderW, 20f);

            y += 50f;
            UIFactory.SetTopLeft((RectTransform)sfxLabelText.transform, x, y, labelW, 26f);
            UIFactory.SetTopLeft((RectTransform)sfxSlider.transform, x + labelW + 10f, y + 3f, sliderW, 20f);

            const float btnW = 260f, btnH = 48f, gap = 14f;
            y += 66f;
            UIFactory.SetTopLeftCentered((RectTransform)languageToggleButton.transform, cx - btnW * 0.5f, y, btnW, btnH);
            y += btnH + gap;
            UIFactory.SetTopLeftCentered((RectTransform)authStatusButton.transform, cx - btnW * 0.5f, y, btnW, btnH);
            y += btnH + gap;
            UIFactory.SetTopLeftCentered((RectTransform)settingsBackButton.transform, cx - btnW * 0.5f, y, btnW, btnH);
        }

        private void ReflowHowToPlayPanel(float cx, float cy)
        {
            UIFactory.SetTopLeftCentered((RectTransform)howToPlayTitleText.transform, cx - 300f, cy - 220f, 600f, 50f);
            UIFactory.SetTopLeft((RectTransform)howToPlayBodyText.transform, cx - 320f, cy - 150f, 640f, 320f);

            const float btnW = 200f, btnH = 46f;
            UIFactory.SetTopLeftCentered((RectTransform)howToPlayBackButton.transform, cx - btnW * 0.5f, cy + 200f, btnW, btnH);
        }

        // Build*()에서 한 번만 설정되고 그 뒤로 안 건드리는 라벨들 — 언어가
        // 바뀌면 여기서 한꺼번에 다시 씀. 매 프레임 갱신되는 값(코인/점수/
        // 미션 진행률 등)은 RefreshContent() 쪽에서 이미 매 프레임 다시
        // 쓰이므로 언어 전환에 저절로 반응함.
        private void RefreshStaticLabels()
        {
            playButtonText.text = Localization.Get("menu.play");
            settingsButtonText.text = Localization.Get("menu.settings");
            howToPlayButtonText.text = Localization.Get("menu.howToPlay");
            leaderboardMenuButtonText.text = Localization.Get("start.leaderboardButton");

            hintText.text = Localization.Get("start.subtitle2");
            playBackButtonText.text = Localization.Get("menu.back");
            dailyHeaderText.text = Localization.Get("start.dailyMissionsHeader");
            rerollButtonText.text = Localization.Get("start.nicknameReroll");

            musicLabelText.text = Localization.Get("settings.music");
            sfxLabelText.text = Localization.Get("settings.sfx");
            languageToggleText.text = Localization.Current == Language.Korean ? "English" : "한국어";
            settingsBackButtonText.text = Localization.Get("menu.back");

            howToPlayTitleText.text = Localization.Get("menu.howToPlay");
            howToPlayBodyText.text = Localization.Get("howtoplay.body");
            howToPlayBackButtonText.text = Localization.Get("menu.back");

            leaderboardHeaderText.text = Localization.Get("leaderboard.header");
            leaderboardCloseText.text = Localization.Get("leaderboard.close");

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

            switch (currentPanel)
            {
                case Panel.Play:
                    if (wallet != null)
                    {
                        coinsText.gameObject.SetActive(true);
                        coinsText.text = $"Coins: {wallet.Coins:N0}";
                    }
                    RefreshBirdRow();
                    RefreshDailyMissions();
                    break;

                case Panel.Settings:
                    authStatusButtonText.text = (auth != null && auth.IsLoggedIn)
                        ? $"{auth.ServerNickname} · {Localization.Get("auth.logoutButton")}"
                        : Localization.Get("auth.loginButton");
                    break;

                case Panel.Leaderboard:
                    RefreshLeaderboard();
                    break;
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
