using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace HillyWings
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
        // 닉네임 표시 줄(신규) 1개 추가 여유분 -- 원래 12(점수 최대 10줄 + 총
        // 슬라이드 + 총 비행일 수)로 꽉 차 있었음.
        private const int MaxLeaderboardLines = 13;

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
        private RankingService ranking;
        private PlayerLevel playerLevel;

        // 로그인 유저의 서버 기록. null = 아직 미조회(또는 비로그인).
        // [] = 조회 완료했지만 기록 없음. 조회 중 중복 요청 방지 플래그도 함께.
        private int[] serverScores;
        private bool serverScoresFetchInFlight;

        private Panel currentPanel = Panel.MainMenu;
        private StatsTab currentStatsTab = StatsTab.Leaderboard;
        private SettingsView currentSettingsView = SettingsView.Preferences;
        private bool isSignupFlow;
        // 서버 응답 오는 동안 중복 제출 방지 -- 실제로 로그에서 관찰된 문제:
        // 422(비밀번호가 너무 짧음 등) 에러 메시지가 예전엔 제대로 안 보여서
        // 사용자가 짧은 시간에 엔터를 여러 번 눌러 같은 요청이 반복 전송됨.
        // 에러 메시지 자체는 ApiClient.cs에서 고쳤지만, 응답 오기 전 중복
        // 제출을 막는 게 근본적으로 더 안전함.
        private bool authSubmitInFlight;

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
        private RectTransform howToPlayCardRT;   // 본문 카드 배경
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
                auth.OnLoggedOut += HandleLoggedOut;
                auth.OnAuthError += HandleAuthError;
                auth.OnNicknameChanged += HandleAuthNicknameChanged;
            }
        }

        // 서버 기록 조회용 -- GameBootstrapper가 Bind 이후 호출(선택적, null이면 미연동).
        public void BindRanking(RankingService rankingRef) => ranking = rankingRef;

        // "총 이동한 거리값을 가지고 레벨업" 요청 -- 기록(Leaderboard) 탭
        // 닉네임 줄 옆에 표시(RefreshLeaderboard). 별도 이벤트 구독 없이
        // 매 프레임 RefreshLeaderboard가 이미 하듯 그냥 값을 다시 읽음.
        public void BindLevel(PlayerLevel playerLevelRef) => playerLevel = playerLevelRef;

        private void OnDestroy()
        {
            if (profile != null) profile.OnNicknameChanged -= HandleNicknameChanged;
            Localization.OnLanguageChanged -= RefreshStaticLabels;
            if (auth != null)
            {
                auth.OnLoggedIn -= HandleLoggedIn;
                auth.OnLoggedOut -= HandleLoggedOut;
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
            authSubmitInFlight = false;
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

        // 로그아웃 시 서버 점수 캐시를 비워서 다음 번 기록 탭 열 때
        // 로컬 기록을 보여주고(재조회 없이), 재로그인 시 다시 조회하도록 함.
        private void HandleLoggedOut()
        {
            serverScores = null;
            serverScoresFetchInFlight = false;
        }

        private void HandleAuthError(string message)
        {
            authSubmitInFlight = false;
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

            // 리더보드 탭 전환 시 로그인 유저면 서버 기록 새로 조회.
            // 이미 조회 중이거나 비로그인이면 건너뜀.
            if (tab == StatsTab.Leaderboard && ranking != null
                && auth != null && auth.IsLoggedIn && !serverScoresFetchInFlight)
                FetchServerScores();
        }

        private void FetchServerScores()
        {
            serverScoresFetchInFlight = true;
            ranking.GetMyScores(10, result =>
            {
                serverScoresFetchInFlight = false;
                if (!result.Success || result.Data?.scores == null)
                {
                    // 조회 실패 시 로컬 기록 그대로 사용(serverScores 건드리지 않음)
                    return;
                }
                var entries = result.Data.scores;
                serverScores = new int[entries.Length];
                for (int i = 0; i < entries.Length; i++)
                    serverScores[i] = entries[i].score;
            });
        }

        private void BuildMainMenu(Transform parent)
        {
            var brown = new Color(0.36f, 0.24f, 0.1f);

            var title = UIFactory.CreateText(parent, "Title", 44, brown, TextAlignmentOptions.Center, FontStyles.Bold);
            titleRect = (RectTransform)title.transform;
            title.text = "Hilly Wings";

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
            authSubmitInFlight = false;
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

            // 엔터 = 회원가입/로그인 버튼 클릭. TMP_InputField.onSubmit은
            // onEndEdit과 달리 포커스를 잃어서가 아니라 엔터/리턴을 눌러서
            // 편집이 끝났을 때만 불림.
            authPasswordField.onSubmit.AddListener(_ => SubmitAuthCredentials());

            authErrorText = UIFactory.CreateText(parent, "ErrorText", 13, new Color(0.7f, 0.15f, 0.15f), TextAlignmentOptions.Center);
            authErrorText.gameObject.SetActive(false);

            authSubmitButton = UIFactory.CreateButton(parent, "AuthSubmitButton", "", 16, brown, out authSubmitButtonText);
            authSubmitButton.onClick.AddListener(SubmitAuthCredentials);

            authCredentialsBackButton = UIFactory.CreateButton(parent, "AuthCredentialsBack", "", 15, brown, out authCredentialsBackButtonText);
            authCredentialsBackButton.onClick.AddListener(() => { audio?.PlayClick(); SwitchSettingsView(SettingsView.AuthChoice); });
        }

        private void SubmitAuthCredentials()
        {
            if (authSubmitInFlight) return; // 응답 오기 전 중복 제출(엔터 연타 등) 방지
            authSubmitInFlight = true;

            audio?.PlayClick();
            if (isSignupFlow) auth?.Signup(authLoginIdField.text, authPasswordField.text);
            else auth?.Login(authLoginIdField.text, authPasswordField.text);
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

            howToPlayTitleText = UIFactory.CreateText(parent, "Title", 46,
                new Color(0.98f, 0.92f, 0.75f), TextAlignmentOptions.Center, FontStyles.Bold);

            // 카드 배경 — 본문 텍스트보다 먼저 생성해야 뒤에 렌더링됨
            var card = UIFactory.CreatePanel(parent, "HowToPlayCard", new Color(0.06f, 0.03f, 0.12f, 0.82f));
            howToPlayCardRT = (RectTransform)card.transform;

            howToPlayBodyText = UIFactory.CreateText(parent, "Body", 24,
                new Color(0.93f, 0.88f, 0.8f), TextAlignmentOptions.TopLeft, FontStyles.Bold);

            howToPlayBackButton = UIFactory.CreateButton(parent, "HowToPlayBackButton", "", 24, brown, out howToPlayBackButtonText);
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
            // 요청: "배경색 때문에 글자가 잘 안보여 볼드체로... 폰트 사이즈를
            // 두배 정도" -- 카드 배경(반투명 크림색) 뒤로 게임 월드(하늘/언덕
            // 색상, 낮/밤에 따라 계속 바뀜)가 비쳐서 배경 대비가 불안정했음.
            // 폰트를 16->28로 키우고 Bold를 추가, 색도 더 짙게(0.32,0.2,0.2 ->
            // 0.15,0.08,0.05) 낮춰서 어떤 배경에서도 잘 읽히도록 함.
            leaderboardLines = new TextMeshProUGUI[MaxLeaderboardLines];
            for (int i = 0; i < MaxLeaderboardLines; i++)
                leaderboardLines[i] = UIFactory.CreateText(parent, $"Line{i}", 28, new Color(0.15f, 0.08f, 0.05f), TextAlignmentOptions.TopLeft, FontStyles.Bold);
        }

        private void BuildStatsBirdsTab(Transform parent)
        {
            var brown = new Color(0.42f, 0.29f, 0.12f);

            BuildNicknameRow(parent, brown);

            coinsText = UIFactory.CreateText(parent, "Coins", 32, new Color(0.85f, 0.6f, 0.1f), TextAlignmentOptions.TopLeft, FontStyles.Bold);

            eggButton = UIFactory.CreateButton(parent, "EggButton", "", 15, brown, out eggButtonText);
            eggButton.onClick.AddListener(OnEggButtonClicked);

            hatchText = UIFactory.CreateText(parent, "HatchMessage", 28, new Color(1f, 0.6f, 0.15f), TextAlignmentOptions.Center, FontStyles.Bold);
            hatchText.gameObject.SetActive(false);

            birdNameText = UIFactory.CreateText(parent, "BirdName", 24, brown, TextAlignmentOptions.Center, FontStyles.Bold);

            BuildBirdRow(parent);
        }

        private void BuildStatsMissionsTab(Transform parent)
        {
            dailyHeaderText = UIFactory.CreateText(parent, "DailyHeader", 30, new Color(0.42f, 0.29f, 0.12f), TextAlignmentOptions.Center, FontStyles.Bold);

            dailyLines = new TextMeshProUGUI[MaxDailyMissionLines];
            for (int i = 0; i < MaxDailyMissionLines; i++)
                dailyLines[i] = UIFactory.CreateText(parent, $"DailyLine{i}", 26, new Color(0.15f, 0.08f, 0.05f), TextAlignmentOptions.TopLeft, FontStyles.Bold);
        }

        private void BuildNicknameRow(Transform parent, Color brown)
        {
            nicknameField = UIFactory.CreateInputField(parent, "NicknameField", 28, brown, 16);
            // UIFactory.CreateInputField엔 Bold 옵션이 없어서 생성 후 직접 설정.
            nicknameField.textComponent.fontStyle = FontStyles.Bold;
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

            // 아이디 입력 필드에서 탭 -> 암호 필드로 포커스 이동. uGUI/TMP_InputField는
            // 기본적으로 탭 키로 다음 필드 이동을 지원하지 않아서 직접 구현해야 함
            // (Move 액션은 보통 방향키/게임패드용이라 탭이 안 걸림).
            if (authLoginIdField != null && authLoginIdField.isFocused
                && Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame)
            {
                authPasswordField.Select();
                authPasswordField.ActivateInputField();
            }

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
            const float cardW   = 680f;
            const float cardPad = 16f;
            const float titleH  = 64f;
            const float bodyH   = 310f;   // 본문 텍스트 영역 높이
            const float btnW    = 220f;
            const float btnH    = 58f;

            float titleTop = cy - 240f;
            UIFactory.SetTopLeftCentered((RectTransform)howToPlayTitleText.transform,
                cx - cardW * 0.5f, titleTop, cardW, titleH);

            // 본문 텍스트
            float bodyTop = titleTop + titleH + 10f;
            UIFactory.SetTopLeft((RectTransform)howToPlayBodyText.transform,
                cx - cardW * 0.5f + cardPad, bodyTop + cardPad,
                cardW - cardPad * 2f, bodyH);

            // 카드 박스 (본문 텍스트 감쌈)
            UIFactory.SetTopLeft(howToPlayCardRT,
                cx - cardW * 0.5f, bodyTop,
                cardW, bodyH + cardPad * 2f);

            float btnY = bodyTop + bodyH + cardPad * 2f + 14f;
            UIFactory.SetTopLeftCentered((RectTransform)howToPlayBackButton.transform,
                cx - btnW * 0.5f, btnY, btnW, btnH);
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
            // 폰트가 16->28로 커진 만큼 줄 간격/박스도 같이 키움(26->40, 400x24->520x36).
            float y = 120f;
            for (int i = 0; i < MaxLeaderboardLines; i++)
            {
                UIFactory.SetTopLeftCentered((RectTransform)leaderboardLines[i].transform, cx - 260f, y, 520f, 36f);
                y += 40f;
            }
        }

        private void ReflowStatsBirdsTab(float cx)
        {
            // 닉네임/코인 텍스트가 커진 만큼(15/18->28/32) 아래 요소들도 전부
            // 세로로 밀림 -- eggButton 176->190, hatchText 220->244, 새 아이콘
            // 줄 260->292, birdNameText도 그만큼 따라 내려감.
            UIFactory.SetTopLeft((RectTransform)nicknameField.transform, cx - 220f, 120f, 180f, 46f);
            UIFactory.SetTopLeft((RectTransform)rerollButton.transform, cx - 32f, 120f, 64f, 46f);
            UIFactory.SetTopLeft((RectTransform)coinsText.transform, cx + 60f, 128f, 220f, 40f);

            UIFactory.SetTopLeftCentered((RectTransform)eggButton.transform, cx - 100f, 190f, 200f, 40f);
            UIFactory.SetTopLeftCentered((RectTransform)hatchText.transform, cx - 220f, 244f, 440f, 34f);

            var birds = BirdPool.All;
            const float iconSize = 50f, spacing = 12f;
            float totalW = birds.Length * iconSize + (birds.Length - 1) * spacing;
            float startX = cx - totalW * 0.5f;
            const float y = 292f;

            for (int i = 0; i < birds.Length; i++)
            {
                float x = startX + i * (iconSize + spacing);
                UIFactory.SetTopLeft(birdIconRects[i], x, y, iconSize, iconSize);
                UIFactory.SetTopLeft((RectTransform)birdSelectionBorders[i].transform, x - 3f, y - 3f, iconSize + 6f, iconSize + 6f);
            }
            UIFactory.SetTopLeftCentered((RectTransform)birdNameText.transform, cx - 300f, y + iconSize + 20f, 600f, 30f);
        }

        private void ReflowStatsMissionsTab(float cx)
        {
            // 폰트가 16/14->30/26으로 커진 만큼 간격도 같이 키움(154->170, 26->42).
            UIFactory.SetTopLeftCentered((RectTransform)dailyHeaderText.transform, cx - 200f, 120f, 400f, 38f);
            float y = 170f;
            for (int i = 0; i < MaxDailyMissionLines; i++)
            {
                UIFactory.SetTopLeftCentered((RectTransform)dailyLines[i].transform, cx - 300f, y, 600f, 34f);
                y += 42f;
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
                dailyLines[i].color = done ? new Color(0.15f, 0.45f, 0.1f) : new Color(0.15f, 0.08f, 0.05f);
                string mark = done ? "O" : $"{dailyMissions.Progress[i]}/{missions[i].Target}";
                dailyLines[i].text = $"{missions[i].Description} ({mark})";
            }
        }

        private void RefreshLeaderboard()
        {
            if (leaderboard == null) return;

            var defaultColor = new Color(0.15f, 0.08f, 0.05f);
            var nicknameColor = new Color(0.75f, 0.45f, 0.05f);
            int line = 0;

            bool loggedIn = auth != null && !string.IsNullOrEmpty(auth.ServerNickname);

            // 닉네임: 서버 닉네임 우선, 없으면 로컬 닉네임.
            string playerNick = loggedIn ? auth.ServerNickname
                : (profile != null ? profile.Nickname : "-");

            // 점수 소스: 로그인 유저는 serverScores(DB), 비로그인은 로컬 TopScores.
            // serverScores가 아직 조회 중이면(null) 로컬 기록을 임시로 보여줌.
            IReadOnlyList<int> localScores = leaderboard.TopScores;
            bool usingServerScores = loggedIn && serverScores != null;
            // 배열을 IReadOnlyList로 직접 사용할 수 없어서 아래에서 인덱서로 접근
            int scoreCount = usingServerScores ? serverScores.Length : localScores.Count;
            System.Func<int, int> getScore = usingServerScores
                ? (i => serverScores[i])
                : (i => localScores[i]);
            if (scoreCount == 0)
            {
                leaderboardLines[line].gameObject.SetActive(true);
                leaderboardLines[line].color = defaultColor;
                leaderboardLines[line].text = (loggedIn && serverScores == null && serverScoresFetchInFlight)
                    ? Localization.Get("leaderboard.loading")
                    : Localization.Get("leaderboard.empty");
                line++;
            }
            else
            {
                for (int i = 0; i < scoreCount && line < MaxLeaderboardLines; i++, line++)
                {
                    leaderboardLines[line].gameObject.SetActive(true);
                    leaderboardLines[line].color = defaultColor;
                    leaderboardLines[line].text = $"{i + 1}.  {getScore(i):N0}  {playerNick}";
                }
            }

            // "나의 기록" 섹션 -- 로그인한 유저에게만 표시(서버에 저장된 누적치).
            // 비로그인이면 이 섹션 전체가 숨겨짐.
            if (loggedIn)
            {
                if (line < MaxLeaderboardLines)
                {
                    leaderboardLines[line].gameObject.SetActive(true);
                    leaderboardLines[line].color = nicknameColor;
                    string levelStr = playerLevel != null ? $" · Lv.{playerLevel.Level}" : "";
                    leaderboardLines[line].text = $"★ {auth.ServerNickname}{levelStr}";
                    line++;
                }
                if (line < MaxLeaderboardLines)
                {
                    leaderboardLines[line].gameObject.SetActive(true);
                    leaderboardLines[line].color = defaultColor;
                    leaderboardLines[line].text = string.Format(Localization.Get("leaderboard.totalSlides"), auth.ServerTotalSlides)
                        + "  " + string.Format(Localization.Get("leaderboard.totalRuns"), auth.ServerTotalRuns);
                    line++;
                }
            }

            for (; line < MaxLeaderboardLines; line++)
                leaderboardLines[line].gameObject.SetActive(false);
        }
    }
}
