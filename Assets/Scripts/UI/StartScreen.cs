using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FlyingChick
{
    // 시작 화면. GameState.Start일 때만 보임.
    //
    // 메인 메뉴(타이틀 + 카드 안에 PLAY/설정/기록/게임 방법 4개 버튼, 참고
    // 스크린샷 레이아웃) + 3개 하위 화면(설정/게임 방법/기록)으로 구성.
    // PLAY는 화면 전환 없이 바로 GameManager.BeginRun() 호출 — 예전처럼
    // "탭하면 시작"하는 별도 게임플레이 화면이 없어짐.
    //
    // 예전에 게임플레이 화면에 있던 코인/새 선택+알구매/오늘의 미션은 전부
    // "기록"(Stats) 화면의 탭 3개(기록/새 선택/일일미션)로 재배치됨.
    //
    // 계정(로그인/회원가입)은 설정 화면 안의 하위 흐름:
    //   환경설정(Preferences) → [로그인/회원가입 선택] → [아이디+비번] →
    //   (회원가입이면) [닉네임 확인/수정] → 메인 메뉴로 복귀
    // 로그인이면 아이디+비번 제출 성공 즉시 메인 메뉴로 복귀.
    public class StartScreen : MonoBehaviour
    {
        private const int MaxDailyMissionLines = 5;
        private const int MaxLeaderboardLines = 12;

        private enum Panel { MainMenu, Settings, HowToPlay, Stats }
        private enum StatsTab { Leaderboard, Birds, Missions }
        private enum SettingsView { Preferences, AuthChoice, Credentials, SignupNickname }

        private CoinWallet wallet;
        private DailyMissions dailyMissions;
        private BirdCollection collection;
        private Leaderboard leaderboard;
        private AudioManager audio;
        private PlayerProfile profile;
        private AuthService auth;

        private Panel currentPanel = Panel.MainMenu;
        private StatsTab currentStatsTab = StatsTab.Leaderboard;
        private SettingsView currentSettingsView = SettingsView.Preferences;
        private bool isSignupFlow;

        private string hatchMessage;
        private float hatchMessageTimeLeft;

        private GameObject root;
        private GameObject mainMenuGroup;
        private GameObject settingsGroup;
        private GameObject howToPlayGroup;
        private GameObject statsGroup;

        // 메인 메뉴
        private RectTransform titleRect;
        private TextMeshProUGUI bestText;
        private RectTransform cardRect;
        private Button playButton, settingsButton, statsButton, howToPlayButton;
        private TextMeshProUGUI playButtonText, settingsButtonText, statsButtonText, howToPlayButtonText;

        // 설정 - 환경설정
        private GameObject settingsPreferencesGroup;
        private TextMeshProUGUI musicLabelText, sfxLabelText;
        private Slider musicSlider, sfxSlider;
        private Button languageToggleButton;
        private TextMeshProUGUI languageToggleText;
        private TextMeshProUGUI accountStatusText;
        private Button logoutButton;
        private TextMeshProUGUI logoutButtonText;
        private Button authEntryButton;
        private TextMeshProUGUI authEntryButtonText;
        private Button settingsBackButton;
        private TextMeshProUGUI settingsBackButtonText;

        // 설정 - 로그인/회원가입 선택
        private GameObject settingsAuthChoiceGroup;
        private Button authChoiceLoginButton, authChoiceSignupButton, authChoiceBackButton;
        private TextMeshProUGUI authChoiceLoginButtonText, authChoiceSignupButtonText, authChoiceBackButtonText;

        // 설정 - 아이디/비밀번호 (로그인/회원가입 공용)
        private GameObject settingsCredentialsGroup;
        private TextMeshProUGUI authLoginIdLabelText, authPasswordLabelText;
        private TMP_InputField authLoginIdField, authPasswordField;
        private TextMeshProUGUI authErrorText;
        private Button authSubmitButton;
        private TextMeshProUGUI authSubmitButtonText;
        private Button authCredentialsBackButton;
        private TextMeshProUGUI authCredentialsBackButtonText;

        // 설정 - 회원가입 닉네임 확인
        private GameObject settingsSignupNicknameGroup;
        private TextMeshProUGUI signupNicknameTitleText;
        private TMP_InputField signupNicknameField;
        private Button signupNicknameRerollButton;
        private TextMeshProUGUI signupNicknameRerollButtonText;
        private Button signupNicknameDoneButton;
        private TextMeshProUGUI signupNicknameDoneButtonText;

        // 게임 방법
        private TextMeshProUGUI howToPlayTitleText;
        private TextMeshProUGUI howToPlayBodyText;
        private Button howToPlayBackButton;
        private TextMeshProUGUI howToPlayBackButtonText;

        // 기록 - 탭바
        private Button statsTabLeaderboardButton, statsTabBirdsButton, statsTabMissionsButton;
        private TextMeshProUGUI statsTabLeaderboardText, statsTabBirdsText, statsTabMissionsText;
        private Image statsTabLeaderboardBg, statsTabBirdsBg, statsTabMissionsBg;
        private Button statsBackButton;
        private TextMeshProUGUI statsBackButtonText;

        // 기록 - 기록 탭
        private GameObject statsLeaderboardGroup;
        private TextMeshProUGUI[] leaderboardLines;

        // 기록 - 새 선택 탭
        private GameObject statsBirdsGroup;
        private TMP_InputField nicknameField;
        private Button rerollButton;
        private TextMeshProUGUI rerollButtonText;
        private TextMeshProUGUI coinsText;
        private Button eggButton;
        private TextMeshProUGUI eggButtonText;
        private TextMeshProUGUI hatchText;
        private TextMeshProUGUI birdNameText;
        private Button[] birdButtons;
        private Image[] birdSelectionBorders;
        private TextMeshProUGUI[] birdLockTexts;
        private RectTransform[] birdIconRects;

        // 기록 - 일일미션 탭
        private GameObject statsMissionsGroup;
        private TextMeshProUGUI dailyHeaderText;
        private TextMeshProUGUI[] dailyLines;

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
                auth.OnNicknameChanged += HandleAuthNicknameChanged;
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
                auth.OnNicknameChanged -= HandleAuthNicknameChanged;
            }
        }

        private void HandleNicknameChanged()
        {
            if (nicknameField != null) nicknameField.text = profile.Nickname;
        }

        private void HandleAuthNicknameChanged()
        {
            if (signupNicknameField != null) signupNicknameField.text = auth.ServerNickname;
        }

        // 로그인 성공 시: 회원가입 흐름 중이었으면 닉네임 확인 단계로,
        // 그냥 로그인이었으면(또는 앱 시작 시 저장된 토큰 검증 성공) 바로
        // 메인 메뉴로.
        private void HandleLoggedIn()
        {
            authErrorText.gameObject.SetActive(false);
            if (isSignupFlow)
            {
                signupNicknameField.text = auth.ServerNickname;
                SwitchSettingsView(SettingsView.SignupNickname);
            }
            else
            {
                SwitchTo(Panel.MainMenu);
            }
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

            mainMenuGroup = UIFactory.CreateFullStretchChild(t, "MainMenuGroup").gameObject;
            BuildMainMenu(mainMenuGroup.transform);

            settingsGroup = UIFactory.CreateFullStretchChild(t, "SettingsGroup").gameObject;
            BuildSettings(settingsGroup.transform);

            howToPlayGroup = UIFactory.CreateFullStretchChild(t, "HowToPlayGroup").gameObject;
            BuildHowToPlayPanel(howToPlayGroup.transform);

            statsGroup = UIFactory.CreateFullStretchChild(t, "StatsGroup").gameObject;
            BuildStats(statsGroup.transform);

            lastWidth = Screen.width;
            lastHeight = Screen.height;
            ReflowLayout();
            RefreshStaticLabels();
            SwitchTo(Panel.MainMenu);
        }

        private void SwitchTo(Panel panel)
        {
            currentPanel = panel;
            mainMenuGroup.SetActive(panel == Panel.MainMenu);
            settingsGroup.SetActive(panel == Panel.Settings);
            howToPlayGroup.SetActive(panel == Panel.HowToPlay);
            statsGroup.SetActive(panel == Panel.Stats);

            // 설정/기록 화면을 벗어날 땐 하위 상태를 초기화 — 다음에 다시
            // 들어왔을 때 회원가입 위자드 중간이나 이전 탭에 멈춰 있으면
            // 헷갈림.
            if (panel != Panel.Settings) SwitchSettingsView(SettingsView.Preferences);
            if (panel != Panel.Stats) SwitchStatsTab(StatsTab.Leaderboard);
        }

        private void SwitchSettingsView(SettingsView view)
        {
            currentSettingsView = view;
            settingsPreferencesGroup.SetActive(view == SettingsView.Preferences);
            settingsAuthChoiceGroup.SetActive(view == SettingsView.AuthChoice);
            settingsCredentialsGroup.SetActive(view == SettingsView.Credentials);
            settingsSignupNicknameGroup.SetActive(view == SettingsView.SignupNickname);
        }

        private void SwitchStatsTab(StatsTab tab)
        {
            currentStatsTab = tab;
            statsLeaderboardGroup.SetActive(tab == StatsTab.Leaderboard);
            statsBirdsGroup.SetActive(tab == StatsTab.Birds);
            statsMissionsGroup.SetActive(tab == StatsTab.Missions);
        }

        private void BuildMainMenu(Transform parent)
        {
            var brown = new Color(0.36f, 0.24f, 0.1f);

            var title = UIFactory.CreateText(parent, "Title", 44, brown, TextAlignmentOptions.Center, FontStyles.Bold);
            titleRect = (RectTransform)title.transform;
            title.text = "Flying Chick";

            bestText = UIFactory.CreateText(parent, "Best", 18, brown, TextAlignmentOptions.Center);

            var card = UIFactory.CreatePanel(parent, "Card", new Color(0.98f, 0.9f, 0.75f, 0.95f));
            cardRect = (RectTransform)card.transform;

            playButton = UIFactory.CreateButton(card.transform, "PlayButton", "", 22, Color.white, out playButtonText);
            playButton.image.color = new Color(0.86f, 0.27f, 0.27f); // 참고 스크린샷처럼 PLAY만 강조색
            playButton.onClick.AddListener(() =>
            {
                audio?.PlayClick();
                GameManager.Instance.BeginRun();
            });

            var secondaryColor = new Color(0.93f, 0.66f, 0.35f);

            settingsButton = UIFactory.CreateButton(card.transform, "SettingsButton", "", 17, brown, out settingsButtonText);
            settingsButton.image.color = secondaryColor;
            settingsButton.onClick.AddListener(() => { audio?.PlayClick(); SwitchTo(Panel.Settings); });

            statsButton = UIFactory.CreateButton(card.transform, "StatsButton", "", 17, brown, out statsButtonText);
            statsButton.image.color = secondaryColor;
            statsButton.onClick.AddListener(() => { audio?.PlayClick(); SwitchTo(Panel.Stats); });

            howToPlayButton = UIFactory.CreateButton(card.transform, "HowToPlayButton", "", 17, brown, out howToPlayButtonText);
            howToPlayButton.image.color = secondaryColor;
            howToPlayButton.onClick.AddListener(() => { audio?.PlayClick(); SwitchTo(Panel.HowToPlay); });
        }

        private void BuildSettings(Transform parent)
        {
            settingsPreferencesGroup = UIFactory.CreateFullStretchChild(parent, "Preferences").gameObject;
            BuildSettingsPreferences(settingsPreferencesGroup.transform);

            settingsAuthChoiceGroup = UIFactory.CreateFullStretchChild(parent, "AuthChoice").gameObject;
            BuildSettingsAuthChoice(settingsAuthChoiceGroup.transform);

            settingsCredentialsGroup = UIFactory.CreateFullStretchChild(parent, "Credentials").gameObject;
            BuildSettingsCredentials(settingsCredentialsGroup.transform);

            settingsSignupNicknameGroup = UIFactory.CreateFullStretchChild(parent, "SignupNickname").gameObject;
            BuildSettingsSignupNickname(settingsSignupNicknameGroup.transform);
        }

        private void BuildSettingsPreferences(Transform parent)
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

            accountStatusText = UIFactory.CreateText(parent, "AccountStatus", 15, brown, TextAlignmentOptions.Center, FontStyles.Bold);

            logoutButton = UIFactory.CreateButton(parent, "LogoutButton", "", 15, brown, out logoutButtonText);
            logoutButton.onClick.AddListener(() => { audio?.PlayClick(); auth?.Logout(); });

            authEntryButton = UIFactory.CreateButton(parent, "AuthEntryButton", "", 15, brown, out authEntryButtonText);
            authEntryButton.onClick.AddListener(() => { audio?.PlayClick(); SwitchSettingsView(SettingsView.AuthChoice); });

            settingsBackButton = UIFactory.CreateButton(parent, "SettingsBackButton", "", 15, brown, out settingsBackButtonText);
            settingsBackButton.onClick.AddListener(() => { audio?.PlayClick(); SwitchTo(Panel.MainMenu); });
        }

        private void BuildSettingsAuthChoice(Transform parent)
        {
            var brown = new Color(0.42f, 0.29f, 0.12f);

            authChoiceLoginButton = UIFactory.CreateButton(parent, "AuthChoiceLogin", "", 18, brown, out authChoiceLoginButtonText);
            authChoiceLoginButton.onClick.AddListener(() => BeginAuthCredentials(false));

            authChoiceSignupButton = UIFactory.CreateButton(parent, "AuthChoiceSignup", "", 18, brown, out authChoiceSignupButtonText);
            authChoiceSignupButton.onClick.AddListener(() => BeginAuthCredentials(true));

            authChoiceBackButton = UIFactory.CreateButton(parent, "AuthChoiceBack", "", 15, brown, out authChoiceBackButtonText);
            authChoiceBackButton.onClick.AddListener(() => { audio?.PlayClick(); SwitchSettingsView(SettingsView.Preferences); });
        }

        private void BeginAuthCredentials(bool signup)
        {
            audio?.PlayClick();
            isSignupFlow = signup;
            authErrorText.gameObject.SetActive(false);
            authLoginIdField.text = "";
            authPasswordField.text = "";
            SwitchSettingsView(SettingsView.Credentials);
        }

        private void BuildSettingsCredentials(Transform parent)
        {
            var white = Color.white;
            var dark = new Color(0.15f, 0.15f, 0.15f);
            var brown = new Color(0.42f, 0.29f, 0.12f);

            authLoginIdLabelText = UIFactory.CreateText(parent, "LoginIdLabel", 14, white);
            authLoginIdField = UIFactory.CreateInputField(parent, "LoginIdField", 15, dark, 255);

            authPasswordLabelText = UIFactory.CreateText(parent, "PasswordLabel", 14, white);
            authPasswordField = UIFactory.CreateInputField(parent, "PasswordField", 15, dark, 128, password: true);

            authErrorText = UIFactory.CreateText(parent, "ErrorText", 13, new Color(0.7f, 0.15f, 0.15f), TextAlignmentOptions.Center);
            authErrorText.gameObject.SetActive(false);

            authSubmitButton = UIFactory.CreateButton(parent, "AuthSubmitButton", "", 16, brown, out authSubmitButtonText);
            authSubmitButton.onClick.AddListener(() =>
            {
                audio?.PlayClick();
                if (isSignupFlow) auth?.Signup(authLoginIdField.text, authPasswordField.text);
                else auth?.Login(authLoginIdField.text, authPasswordField.text);
            });

            authCredentialsBackButton = UIFactory.CreateButton(parent, "AuthCredentialsBack", "", 15, brown, out authCredentialsBackButtonText);
            authCredentialsBackButton.onClick.AddListener(() => { audio?.PlayClick(); SwitchSettingsView(SettingsView.AuthChoice); });
        }

        private void BuildSettingsSignupNickname(Transform parent)
        {
            var brown = new Color(0.42f, 0.29f, 0.12f);
            var dark = new Color(0.15f, 0.15f, 0.15f);

            signupNicknameTitleText = UIFactory.CreateText(parent, "Title", 18, brown, TextAlignmentOptions.Center, FontStyles.Bold);

            signupNicknameField = UIFactory.CreateInputField(parent, "NicknameField", 16, dark, 32);
            signupNicknameField.onEndEdit.AddListener(value => auth?.SetNickname(value));

            signupNicknameRerollButton = UIFactory.CreateButton(parent, "RerollButton", "", 14, brown, out signupNicknameRerollButtonText);
            signupNicknameRerollButton.onClick.AddListener(() => { audio?.PlayClick(); auth?.RerollNickname(); });

            signupNicknameDoneButton = UIFactory.CreateButton(parent, "DoneButton", "", 17, brown, out signupNicknameDoneButtonText);
            signupNicknameDoneButton.onClick.AddListener(() => { audio?.PlayClick(); SwitchTo(Panel.MainMenu); });
        }

        private void BuildHowToPlayPanel(Transform parent)
        {
            var brown = new Color(0.42f, 0.29f, 0.12f);

            howToPlayTitleText = UIFactory.CreateText(parent, "Title", 32, new Color(0.36f, 0.24f, 0.1f), TextAlignmentOptions.Center, FontStyles.Bold);
            howToPlayBodyText = UIFactory.CreateText(parent, "Body", 16, brown, TextAlignmentOptions.TopLeft);

            howToPlayBackButton = UIFactory.CreateButton(parent, "HowToPlayBackButton", "", 15, brown, out howToPlayBackButtonText);
            howToPlayBackButton.onClick.AddListener(() => { audio?.PlayClick(); SwitchTo(Panel.MainMenu); });
        }

        private void BuildStats(Transform parent)
        {
            var brown = new Color(0.42f, 0.29f, 0.12f);

            statsTabLeaderboardButton = UIFactory.CreateButton(parent, "TabLeaderboard", "", 14, brown, out statsTabLeaderboardText);
            statsTabLeaderboardBg = statsTabLeaderboardButton.image;
            statsTabLeaderboardButton.onClick.AddListener(() => { audio?.PlayClick(); SwitchStatsTab(StatsTab.Leaderboard); });

            statsTabBirdsButton = UIFactory.CreateButton(parent, "TabBirds", "", 14, brown, out statsTabBirdsText);
            statsTabBirdsBg = statsTabBirdsButton.image;
            statsTabBirdsButton.onClick.AddListener(() => { audio?.PlayClick(); SwitchStatsTab(StatsTab.Birds); });

            statsTabMissionsButton = UIFactory.CreateButton(parent, "TabMissions", "", 14, brown, out statsTabMissionsText);
            statsTabMissionsBg = statsTabMissionsButton.image;
            statsTabMissionsButton.onClick.AddListener(() => { audio?.PlayClick(); SwitchStatsTab(StatsTab.Missions); });

            statsBackButton = UIFactory.CreateButton(parent, "StatsBackButton", "", 15, brown, out statsBackButtonText);
            statsBackButton.onClick.AddListener(() => { audio?.PlayClick(); SwitchTo(Panel.MainMenu); });

            statsLeaderboardGroup = UIFactory.CreateFullStretchChild(parent, "LeaderboardTab").gameObject;
            BuildStatsLeaderboardTab(statsLeaderboardGroup.transform);

            statsBirdsGroup = UIFactory.CreateFullStretchChild(parent, "BirdsTab").gameObject;
            BuildStatsBirdsTab(statsBirdsGroup.transform);

            statsMissionsGroup = UIFactory.CreateFullStretchChild(parent, "MissionsTab").gameObject;
            BuildStatsMissionsTab(statsMissionsGroup.transform);
        }

        private void BuildStatsLeaderboardTab(Transform parent)
        {
            leaderboardLines = new TextMeshProUGUI[MaxLeaderboardLines];
            for (int i = 0; i < MaxLeaderboardLines; i++)
                leaderboardLines[i] = UIFactory.CreateText(parent, $"Line{i}", 16, new Color(0.32f, 0.2f, 0.2f));
        }

        private void BuildStatsBirdsTab(Transform parent)
        {
            var brown = new Color(0.42f, 0.29f, 0.12f);

            BuildNicknameRow(parent, brown);

            coinsText = UIFactory.CreateText(parent, "Coins", 18, new Color(0.85f, 0.6f, 0.1f), TextAlignmentOptions.TopLeft, FontStyles.Bold);

            eggButton = UIFactory.CreateButton(parent, "EggButton", "", 15, brown, out eggButtonText);
            eggButton.onClick.AddListener(OnEggButtonClicked);

            hatchText = UIFactory.CreateText(parent, "HatchMessage", 15, new Color(1f, 0.6f, 0.15f), TextAlignmentOptions.Center, FontStyles.Bold);
            hatchText.gameObject.SetActive(false);

            birdNameText = UIFactory.CreateText(parent, "BirdName", 13, brown, TextAlignmentOptions.Center);

            BuildBirdRow(parent);
        }

        private void BuildStatsMissionsTab(Transform parent)
        {
            dailyHeaderText = UIFactory.CreateText(parent, "DailyHeader", 16, new Color(0.42f, 0.29f, 0.12f), TextAlignmentOptions.Center, FontStyles.Bold);

            dailyLines = new TextMeshProUGUI[MaxDailyMissionLines];
            for (int i = 0; i < MaxDailyMissionLines; i++)
                dailyLines[i] = UIFactory.CreateText(parent, $"DailyLine{i}", 14, new Color(0.32f, 0.2f, 0.2f));
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
            // null이면 코인 부족 — 버튼은 그대로 눌러볼 수 있게 둠.
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
                || (authPasswordField != null && authPasswordField.isFocused)
                || (signupNicknameField != null && signupNicknameField.isFocused);
            if (currentPanel == Panel.MainMenu && !typing && InputService.IsSpaceDownThisFrame())
                gm.BeginRun();
        }

        private void ReflowLayout()
        {
            float cx = Screen.width * 0.5f;
            float cy = Screen.height * 0.5f;

            ReflowMainMenu(cx, cy);
            ReflowSettings(cx, cy);
            ReflowHowToPlay(cx, cy);
            ReflowStats(cx, cy);
        }

        // PLAY 버튼이 있는 카드가 화면 정중앙에 오도록 배치 — cardY는
        // SetTopLeft 기준(카드 윗변)이라 cardH/2를 빼야 카드의 실제 중심이
        // cy(화면 세로 중앙)에 정확히 맞음. 예전엔 cy - 82로 고정해뒀는데
        // 이건 카드 윗변 위치였지 중심이 아니라서 카드가 중앙보다 아래로
        // 쏠려 있었음 — 이 버그 때문에 다시 조정.
        private void ReflowMainMenu(float cx, float cy)
        {
            const float cardW = 300f, cardH = 280f;
            float cardX = cx - cardW * 0.5f;
            float cardY = cy - cardH * 0.5f;

            UIFactory.SetTopLeftCentered(titleRect, cx - 300f, cardY - 158f, 600f, 60f);
            UIFactory.SetTopLeftCentered((RectTransform)bestText.transform, cx - 300f, cardY - 88f, 600f, 26f);
            UIFactory.SetTopLeft(cardRect, cardX, cardY, cardW, cardH);

            // 아래 좌표는 카드 자신의 rect 기준(카드가 부모) — 화면 절대좌표 아님.
            UIFactory.SetTopLeft((RectTransform)playButton.transform, 20f, 20f, cardW - 40f, 60f);
            UIFactory.SetTopLeft((RectTransform)settingsButton.transform, 20f, 96f, cardW - 40f, 46f);
            UIFactory.SetTopLeft((RectTransform)statsButton.transform, 20f, 154f, cardW - 40f, 46f);
            UIFactory.SetTopLeft((RectTransform)howToPlayButton.transform, 20f, 212f, cardW - 40f, 46f);
        }

        private void ReflowSettings(float cx, float cy)
        {
            const float btnW = 260f, btnH = 46f, gap = 14f;

            // 환경설정
            const float rowW = 340f, labelW = 90f;
            float sliderW = rowW - labelW - 10f;
            float x = cx - rowW * 0.5f;

            float y = cy - 200f;
            UIFactory.SetTopLeft((RectTransform)musicLabelText.transform, x, y, labelW, 26f);
            UIFactory.SetTopLeft((RectTransform)musicSlider.transform, x + labelW + 10f, y + 3f, sliderW, 20f);

            y += 50f;
            UIFactory.SetTopLeft((RectTransform)sfxLabelText.transform, x, y, labelW, 26f);
            UIFactory.SetTopLeft((RectTransform)sfxSlider.transform, x + labelW + 10f, y + 3f, sliderW, 20f);

            y += 66f;
            UIFactory.SetTopLeftCentered((RectTransform)languageToggleButton.transform, cx - btnW * 0.5f, y, btnW, btnH);

            y += btnH + gap;
            UIFactory.SetTopLeftCentered((RectTransform)accountStatusText.transform, cx - btnW * 0.5f, y, btnW, 26f);

            y += 30f;
            UIFactory.SetTopLeftCentered((RectTransform)logoutButton.transform, cx - btnW * 0.5f, y, btnW, btnH);
            UIFactory.SetTopLeftCentered((RectTransform)authEntryButton.transform, cx - btnW * 0.5f, y, btnW, btnH); // 같은 자리 - 로그인 여부에 따라 둘 중 하나만 보임

            y += btnH + gap;
            UIFactory.SetTopLeftCentered((RectTransform)settingsBackButton.transform, cx - btnW * 0.5f, y, btnW, btnH);

            // 로그인/회원가입 선택
            float ay = cy - 60f;
            UIFactory.SetTopLeftCentered((RectTransform)authChoiceLoginButton.transform, cx - btnW * 0.5f, ay, btnW, btnH);
            ay += btnH + gap;
            UIFactory.SetTopLeftCentered((RectTransform)authChoiceSignupButton.transform, cx - btnW * 0.5f, ay, btnW, btnH);
            ay += btnH + gap;
            UIFactory.SetTopLeftCentered((RectTransform)authChoiceBackButton.transform, cx - btnW * 0.5f, ay, btnW, btnH);

            // 아이디/비밀번호 (로그인/회원가입 공용)
            const float formW = 340f;
            float fx = cx - formW * 0.5f;
            float fy = cy - 150f;
            UIFactory.SetTopLeft((RectTransform)authLoginIdLabelText.transform, fx, fy, 200f, 20f);
            UIFactory.SetTopLeft((RectTransform)authLoginIdField.transform, fx, fy + 24f, formW, 36f);
            UIFactory.SetTopLeft((RectTransform)authPasswordLabelText.transform, fx, fy + 72f, 200f, 20f);
            UIFactory.SetTopLeft((RectTransform)authPasswordField.transform, fx, fy + 96f, formW, 36f);
            UIFactory.SetTopLeft((RectTransform)authErrorText.transform, fx, fy + 138f, formW, 36f);
            UIFactory.SetTopLeftCentered((RectTransform)authSubmitButton.transform, cx - btnW * 0.5f, fy + 182f, btnW, btnH);
            UIFactory.SetTopLeftCentered((RectTransform)authCredentialsBackButton.transform, cx - btnW * 0.5f, fy + 182f + btnH + gap, btnW, btnH);

            // 회원가입 닉네임 확인
            float ny = cy - 100f;
            UIFactory.SetTopLeftCentered((RectTransform)signupNicknameTitleText.transform, cx - 300f, ny, 600f, 30f);
            UIFactory.SetTopLeft((RectTransform)signupNicknameField.transform, cx - 140f, ny + 44f, 280f, 40f);
            UIFactory.SetTopLeftCentered((RectTransform)signupNicknameRerollButton.transform, cx - 90f, ny + 96f, 180f, 40f);
            UIFactory.SetTopLeftCentered((RectTransform)signupNicknameDoneButton.transform, cx - btnW * 0.5f, ny + 152f, btnW, btnH);
        }

        private void ReflowHowToPlay(float cx, float cy)
        {
            UIFactory.SetTopLeftCentered((RectTransform)howToPlayTitleText.transform, cx - 300f, cy - 220f, 600f, 50f);
            UIFactory.SetTopLeft((RectTransform)howToPlayBodyText.transform, cx - 320f, cy - 150f, 640f, 320f);

            const float btnW = 200f, btnH = 46f;
            UIFactory.SetTopLeftCentered((RectTransform)howToPlayBackButton.transform, cx - btnW * 0.5f, cy + 200f, btnW, btnH);
        }

        private void ReflowStats(float cx, float cy)
        {
            const float tabW = 140f, tabH = 40f, tabGap = 8f;
            float totalTabW = tabW * 3f + tabGap * 2f;
            float tabX = cx - totalTabW * 0.5f;
            const float tabY = 60f;

            UIFactory.SetTopLeft((RectTransform)statsTabLeaderboardButton.transform, tabX, tabY, tabW, tabH);
            UIFactory.SetTopLeft((RectTransform)statsTabBirdsButton.transform, tabX + tabW + tabGap, tabY, tabW, tabH);
            UIFactory.SetTopLeft((RectTransform)statsTabMissionsButton.transform, tabX + (tabW + tabGap) * 2f, tabY, tabW, tabH);

            UIFactory.SetTopLeftCentered((RectTransform)statsBackButton.transform, cx - 100f, Screen.height - 66f, 200f, 46f);

            ReflowStatsLeaderboardTab(cx);
            ReflowStatsBirdsTab(cx);
            ReflowStatsMissionsTab(cx);
        }

        private void ReflowStatsLeaderboardTab(float cx)
        {
            float y = 120f;
            for (int i = 0; i < MaxLeaderboardLines; i++)
            {
                UIFactory.SetTopLeftCentered((RectTransform)leaderboardLines[i].transform, cx - 200f, y, 400f, 24f);
                y += 26f;
            }
        }

        private void ReflowStatsBirdsTab(float cx)
        {
            UIFactory.SetTopLeft((RectTransform)nicknameField.transform, cx - 220f, 120f, 180f, 32f);
            UIFactory.SetTopLeft((RectTransform)rerollButton.transform, cx - 32f, 120f, 64f, 32f);
            UIFactory.SetTopLeft((RectTransform)coinsText.transform, cx + 60f, 124f, 160f, 28f);

            UIFactory.SetTopLeftCentered((RectTransform)eggButton.transform, cx - 100f, 176f, 200f, 40f);
            UIFactory.SetTopLeftCentered((RectTransform)hatchText.transform, cx - 200f, 220f, 400f, 22f);

            var birds = BirdPool.All;
            const float iconSize = 50f, spacing = 12f;
            float totalW = birds.Length * iconSize + (birds.Length - 1) * spacing;
            float startX = cx - totalW * 0.5f;
            const float y = 260f;

            for (int i = 0; i < birds.Length; i++)
            {
                float x = startX + i * (iconSize + spacing);
                UIFactory.SetTopLeft(birdIconRects[i], x, y, iconSize, iconSize);
                UIFactory.SetTopLeft((RectTransform)birdSelectionBorders[i].transform, x - 3f, y - 3f, iconSize + 6f, iconSize + 6f);
            }
            UIFactory.SetTopLeftCentered((RectTransform)birdNameText.transform, cx - 300f, y + iconSize + 16f, 600f, 20f);
        }

        private void ReflowStatsMissionsTab(float cx)
        {
            UIFactory.SetTopLeftCentered((RectTransform)dailyHeaderText.transform, cx - 200f, 120f, 400f, 24f);
            float y = 154f;
            for (int i = 0; i < MaxDailyMissionLines; i++)
            {
                UIFactory.SetTopLeftCentered((RectTransform)dailyLines[i].transform, cx - 260f, y, 520f, 22f);
                y += 26f;
            }
        }

        // Build*()에서 한 번만 설정되고 그 뒤로 안 건드리는 라벨들 — 언어가
        // 바뀌면 여기서 한꺼번에 다시 씀. 로그인 상태나 회원가입 흐름처럼
        // 언어 말고 다른 상태에도 의존하는 라벨(예: 아이디/비번 제출 버튼의
        // "로그인"/"회원가입" 라벨)은 RefreshContent() 쪽에서 처리.
        private void RefreshStaticLabels()
        {
            playButtonText.text = Localization.Get("menu.play");
            settingsButtonText.text = Localization.Get("menu.settings");
            statsButtonText.text = Localization.Get("menu.stats");
            howToPlayButtonText.text = Localization.Get("menu.howToPlay");

            musicLabelText.text = Localization.Get("settings.music");
            sfxLabelText.text = Localization.Get("settings.sfx");
            languageToggleText.text = Localization.Current == Language.Korean ? "English" : "한국어";
            logoutButtonText.text = Localization.Get("auth.logoutButton");
            authEntryButtonText.text = Localization.Get("settings.accountEntry");
            settingsBackButtonText.text = Localization.Get("menu.back");

            authChoiceLoginButtonText.text = Localization.Get("auth.loginButton");
            authChoiceSignupButtonText.text = Localization.Get("auth.signupButton");
            authChoiceBackButtonText.text = Localization.Get("menu.back");

            authLoginIdLabelText.text = Localization.Get("auth.loginIdLabel");
            authPasswordLabelText.text = Localization.Get("auth.passwordLabel");
            authCredentialsBackButtonText.text = Localization.Get("menu.back");

            signupNicknameTitleText.text = Localization.Get("settings.nicknameStepTitle");
            signupNicknameRerollButtonText.text = Localization.Get("start.nicknameReroll");
            signupNicknameDoneButtonText.text = Localization.Get("settings.nicknameStepDone");

            howToPlayTitleText.text = Localization.Get("menu.howToPlay");
            howToPlayBodyText.text = Localization.Get("howtoplay.body");
            howToPlayBackButtonText.text = Localization.Get("menu.back");

            statsTabLeaderboardText.text = Localization.Get("stats.tabLeaderboard");
            statsTabBirdsText.text = Localization.Get("stats.tabBirds");
            statsTabMissionsText.text = Localization.Get("stats.tabMissions");
            statsBackButtonText.text = Localization.Get("menu.back");

            dailyHeaderText.text = Localization.Get("start.dailyMissionsHeader");
            rerollButtonText.text = Localization.Get("start.nicknameReroll");
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
                case Panel.Settings:
                    RefreshSettings();
                    break;
                case Panel.Stats:
                    RefreshStats();
                    break;
            }
        }

        private void RefreshSettings()
        {
            bool loggedIn = auth != null && auth.IsLoggedIn;
            accountStatusText.gameObject.SetActive(loggedIn);
            logoutButton.gameObject.SetActive(loggedIn);
            authEntryButton.gameObject.SetActive(!loggedIn);
            if (loggedIn) accountStatusText.text = auth.ServerNickname;

            if (currentSettingsView == SettingsView.Credentials)
                authSubmitButtonText.text = isSignupFlow ? Localization.Get("auth.signupButton") : Localization.Get("auth.loginButton");
        }

        private void RefreshStats()
        {
            var activeColor = new Color(1f, 1f, 1f, 0.95f);
            var inactiveColor = new Color(1f, 1f, 1f, 0.55f);
            statsTabLeaderboardBg.color = currentStatsTab == StatsTab.Leaderboard ? activeColor : inactiveColor;
            statsTabBirdsBg.color = currentStatsTab == StatsTab.Birds ? activeColor : inactiveColor;
            statsTabMissionsBg.color = currentStatsTab == StatsTab.Missions ? activeColor : inactiveColor;

            switch (currentStatsTab)
            {
                case StatsTab.Leaderboard:
                    RefreshLeaderboard();
                    break;
                case StatsTab.Birds:
                    if (wallet != null)
                    {
                        coinsText.gameObject.SetActive(true);
                        coinsText.text = $"Coins: {wallet.Coins:N0}";
                    }
                    RefreshBirdRow();
                    break;
                case StatsTab.Missions:
                    RefreshDailyMissions();
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
            if (dailyMissions == null) return;

            var missions = dailyMissions.ActiveMissions;
            for (int i = 0; i < MaxDailyMissionLines; i++)
            {
                if (i >= missions.Length) { dailyLines[i].gameObject.SetActive(false); continue; }

                dailyLines[i].gameObject.SetActive(true);
                bool done = dailyMissions.Completed[i];
                dailyLines[i].color = done ? new Color(0.25f, 0.55f, 0.2f) : new Color(0.32f, 0.2f, 0.2f);
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
