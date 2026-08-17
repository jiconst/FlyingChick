using System;
using System.Collections.Generic;

namespace FlyingChick
{
    public enum Language
    {
        Korean,
        English
    }

    // Plain static class, not a MonoBehaviour singleton -- this project only
    // allows GameManager/ScoreManager/SaveSystem as singletons (see
    // CLAUDE.md conventions). Persists the chosen language through
    // SaveSystem.Data.language, same pattern as CoinWallet/BirdCollection
    // reading/writing their own slice of the save blob.
    //
    // Scope note: only strings that are ACTUALLY Korean in the current UI
    // are in the table below. A lot of existing HUD/toast text ("GREAT
    // SLIDE", "STREAK RESET", "Island {0} - {1}x", debug readouts, ...) was
    // already written in English as a stylistic choice from early in the
    // project (matching the arcade-game feel) and reads the same in both
    // languages, so it was left as plain string literals at the call site
    // rather than round-tripped through a translation table for no reason.
    public static class Localization
    {
        private static Language current = Language.Korean;

        public static event Action OnLanguageChanged;

        public static Language Current
        {
            get => current;
            set
            {
                if (current == value) return;
                current = value;
                if (SaveSystem.Instance != null)
                {
                    SaveSystem.Instance.Data.language = (int)value;
                    SaveSystem.Instance.Save();
                }
                OnLanguageChanged?.Invoke();
            }
        }

        // Called once from GameBootstrapper, before anything calls Get() --
        // restores the saved language directly (bypassing the Current
        // setter) so this doesn't re-save what was just loaded or fire
        // OnLanguageChanged before any UI exists to hear it.
        public static void LoadSaved()
        {
            if (SaveSystem.Instance == null) return;
            current = (Language)SaveSystem.Instance.Data.language;
        }

        // Missing keys return the key itself rather than throwing or
        // returning an empty string -- a wrong/typo'd key shows up in-game
        // as visible garbage text instead of silently disappearing or
        // crashing, which is easier to spot and fix.
        public static string Get(string key)
        {
            if (!Table.TryGetValue(key, out var entry)) return key;
            return current == Language.Korean ? entry.ko : entry.en;
        }

        private static readonly Dictionary<string, (string ko, string en)> Table = new Dictionary<string, (string ko, string en)>
        {
            ["start.subtitle2"] = ("터치 / 클릭 / 스페이스바로 시작", "Tap / click / press space to start"),
            ["start.leaderboardButton"] = ("기록 보기", "Leaderboard"),
            ["start.eggButtonBuy"] = ("알 구매 ({0} 코인)", "Buy Egg ({0} coins)"),
            ["start.eggButtonAllOwned"] = ("새를 모두 모았어요", "You've collected them all!"),
            ["start.hatchMessage"] = ("부화! {0} 획득", "Hatched! Got {0}"),
            ["start.dailyMissionsHeader"] = ("오늘의 미션", "Daily Missions"),
            ["start.nicknameReroll"] = ("재생성", "Reroll"),
            ["leaderboard.header"] = ("기록", "Leaderboard"),
            ["leaderboard.empty"] = ("아직 기록이 없어요", "No records yet"),
            ["leaderboard.totalSlides"] = ("총 슬라이드: {0:N0}", "Total slides: {0:N0}"),
            ["leaderboard.totalRuns"] = ("총 비행일 수: {0:N0}", "Total days flown: {0:N0}"),
            ["leaderboard.close"] = ("닫기", "Close"),

            ["auth.loginButton"] = ("로그인", "Log In"),
            ["auth.logoutButton"] = ("로그아웃", "Log Out"),
            ["auth.signupButton"] = ("회원가입", "Sign Up"),
            ["auth.loginIdLabel"] = ("아이디", "ID"),
            ["auth.passwordLabel"] = ("비밀번호", "Password"),

            // 메인 메뉴 (게임플레이/설정/게임 방법/기록 4개 버튼) 및 하위 화면들.
            ["menu.play"] = ("게임플레이", "Play"),
            ["menu.settings"] = ("설정", "Settings"),
            ["menu.howToPlay"] = ("게임 방법", "How to Play"),
            ["menu.back"] = ("뒤로", "Back"),

            ["settings.music"] = ("음악", "Music"),
            ["settings.sfx"] = ("효과음", "Sound Effects"),

            ["howtoplay.body"] = (
                "내리막에서 화면을 누르고 있으면 병아리가 빠르게 하강합니다.\n" +
                "오르막 정상 부근에서 손을 떼면 하늘로 발사됩니다.\n\n" +
                "다이빙 직후 착지에 성공하면 'Great Slide'! 3번 연속 성공하면 Fever가 발동해 점수가 2배가 됩니다.\n\n" +
                "노란 코인과 파란 스피드 코인을 모으고, 공중에 떠 있을 때 구름을 터치하면 추가 점수를 얻습니다.\n" +
                "섬을 하나 통과할 때마다 점수 배수가 올라갑니다.",
                "Hold the screen while going downhill to dive.\n" +
                "Release near the top of a hill to launch into the air.\n\n" +
                "Land right after diving to score a Great Slide! Three in a row triggers Fever Mode, doubling your score.\n\n" +
                "Collect yellow coins and blue speed coins, and touch clouds while airborne for bonus points.\n" +
                "Each island you pass increases your score multiplier."),

            ["dayover.title"] = ("해가 졌어요", "The sun has set"),
            ["dayover.restart"] = ("다시하기", "Play Again"),
            ["dayover.home"] = ("홈", "Home"),

            ["hud.nestHeader"] = ("Nest 목표 (+{0} 배수)", "Nest Goals (+{0}x)"),

            ["mission.FeverTriggerCount"] = ("Fever {0}회 발동", "Trigger Fever {0} times"),
            ["mission.ReachIsland"] = ("{0}번째 섬 도달", "Reach island {0}"),
            ["mission.CollectCoins"] = ("코인 {0}개 획득", "Collect {0} coins"),
            ["mission.GreatSlideCount"] = ("Great Slide {0}회 성공", "Land {0} Great Slides"),
            ["mission.CloudTouchCount"] = ("구름 {0}개 터치", "Touch {0} clouds"),
            ["mission.ScoreReached"] = ("{0}점 획득", "Reach {0} points"),
            ["mission.FeverDuration"] = ("Fever {0}초 이상 유지", "Keep Fever active for {0}s"),

            ["bird.chick_yellow"] = ("노랑 병아리", "Yellow Chick"),
            ["bird.chick_red"] = ("빨강 병아리", "Red Chick"),
            ["bird.chick_blue"] = ("파랑 병아리", "Blue Chick"),
            ["bird.chick_green"] = ("초록 병아리", "Green Chick"),
            ["bird.chick_purple"] = ("보라 병아리", "Purple Chick"),

            ["perk.none"] = ("기본 병아리", "Basic Chick"),
            ["perk.SlideScoreBonus"] = ("슬라이드 점수 +{0:0}%", "+{0:0}% slide score"),
            ["perk.FeverDurationBonus"] = ("Fever 지속시간 +{0:0}초", "+{0:0}s Fever duration"),
            ["perk.CoinMagnet"] = ("코인 획득 범위 +{0:0}", "+{0:0} coin pickup range"),
            ["perk.StartSpeedBonus"] = ("시작 속도 +{0:0}", "+{0:0} starting speed"),
        };
    }
}
