using System;
using UnityEngine;

namespace FlyingChick
{
    // Wraps FlyingChick-Server's /auth/* endpoints. Not a singleton (only
    // GameManager/ScoreManager/SaveSystem are, per this project's
    // convention) -- GameBootstrapper creates and wires it like
    // CoinWallet/BirdCollection.
    //
    // Login is entirely OPTIONAL -- the offline game (local PlayerProfile
    // nickname, local save, local leaderboard) works exactly as before
    // regardless of whether this ever succeeds. Every method reports
    // failure through a callback/event instead of throwing, and a failed
    // or missing server connection just leaves IsLoggedIn false; nothing
    // here may block startup or gameplay.
    //
    // The nickname this service tracks (ServerNickname) is the server's
    // unique account nickname -- a separate concept from the local
    // PlayerProfile nickname (Meta/PlayerProfile.cs). They are
    // intentionally not merged; see CLAUDE.md.
    public class AuthService : MonoBehaviour
    {
        private ApiClient api;

        public bool IsLoggedIn { get; private set; }
        public string ServerNickname { get; private set; }
        // 총 이동 거리 기반 레벨(서버 계정에 저장된 값) -- 로그인/토큰 검증
        // 성공 시 서버가 알려주는 값을 그대로 반영. 기본 1은 로그아웃
        // 상태거나 아직 서버에서 값을 못 받아온 상태의 안전한 초기값.
        public int ServerLevel { get; private set; } = 1;
        // 서버에 저장된 코인 총액 -- 로그인/me 검증 시 서버가 알려주는 값.
        // CoinWallet이 로그인 시 max(로컬, 서버)를 취해 권위 있는 값으로 삼음.
        public int ServerCoins { get; private set; }
        // 서버 집계 누적치 -- 로그인/me 검증 시 서버가 알려주는 값.
        // POST /scores 응답에는 포함되지 않으므로 클라이언트가 런 종료 시
        // 로컬 Leaderboard로부터 임시로 더해두고, 다음 로그인에 서버 값이
        // 덮어씀. 기록 화면 "나의 기록" 섹션 표시에만 사용됨.
        public int ServerTotalSlides { get; private set; }
        public int ServerTotalRuns { get; private set; }
        public string LastError { get; private set; }

        public event Action OnLoggedIn;
        public event Action OnLoggedOut;
        public event Action<string> OnAuthError; // human-readable message
        public event Action OnNicknameChanged; // ServerNickname 값이 바뀔 때마다(로그인/회원가입 포함) — PlayerProfile.OnNicknameChanged와 같은 패턴
        public event Action OnLevelChanged;     // ServerLevel 값이 바뀔 때마다(로그인/검증/동기화 포함)
        public event Action OnCoinsChanged;     // ServerCoins 값이 바뀔 때마다

        public void Configure(ApiClient apiClient)
        {
            api = apiClient;
        }

        public void Signup(string loginId, string password)
        {
            api.Post<TokenResponse>("/auth/signup", new SignupRequest { login_id = loginId, password = password }, HandleTokenResult);
        }

        public void Login(string loginId, string password)
        {
            api.Post<TokenResponse>("/auth/login", new LoginRequest { login_id = loginId, password = password }, HandleTokenResult);
        }

        // Called once at startup with a token from a previous session
        // (SaveData.authToken). Validates it against the server; an
        // expired/invalid token or an unreachable server both just leave
        // the player logged out -- see GameBootstrapper.
        public void ValidateStoredToken(string token)
        {
            if (string.IsNullOrEmpty(token)) return;

            api.AuthToken = token;
            api.Get<MeResponse>("/auth/me", result =>
            {
                if (result.Success)
                {
                    ServerNickname = result.Data.nickname;
                    ServerLevel = result.Data.user_level;
                    ServerCoins = result.Data.coins;
                    ServerTotalSlides = result.Data.total_slides;
                    ServerTotalRuns = result.Data.total_runs;
                    IsLoggedIn = true;
                    OnLoggedIn?.Invoke();
                    OnLevelChanged?.Invoke();
                }
                else
                {
                    api.AuthToken = null;
                    Persist(null);
                }
            });
        }

        public void Logout()
        {
            if (!IsLoggedIn) return;
            api.AuthToken = null;
            IsLoggedIn = false;
            ServerNickname = null;
            ServerLevel = 1;
            ServerCoins = 0;
            ServerTotalSlides = 0;
            ServerTotalRuns = 0;
            Persist(null);
            OnLoggedOut?.Invoke();
        }

        // 코인 동기화 -- CoinWallet이 로그인 상태일 때 모든 코인 변경(획득/지출)
        // 직후 호출함. 실패해도 무시(다음 로그인 시 서버 값이 덮어씀).
        // 비로그인 상태면 아무 일도 안 함(로그인 optional 원칙).
        public void SyncCoins(int currentCoins)
        {
            if (!IsLoggedIn) return;
            api.Put<CoinResponse>("/auth/coins", new CoinSyncRequest { coins = currentCoins }, result =>
            {
                if (!result.Success) return;
                if (result.Data.coins == ServerCoins) return;
                ServerCoins = result.Data.coins;
                OnCoinsChanged?.Invoke();
            });
        }

        // "총 이동한 거리값을 가지고 레벨업" 요청 -- 클라이언트는 레벨을 직접
        // 계산해서 보내지 않고 누적 거리만 보냄, 서버가 공유 테이블로 다시
        // 계산해서 돌려준 값을 신뢰함(Meta/PlayerLevel.cs가 호출).
        // 비로그인 상태면 아무 일도 안 함(로그인 optional 원칙).
        // 런 종료 시 점수/슬라이드/섬 결과를 서버에 제출하고 로컬 통계를
        // 즉시 반영함 -- fire-and-forget, 실패해도 다음 로그인 시 서버 값으로
        // 갱신되므로 재시도 큐 없음(오프라인 플레이 optional 원칙).
        // 비로그인 상태면 아무 일도 안 함.
        public void SubmitScore(int runScore, int island, int greatSlides)
        {
            if (!IsLoggedIn) return;
            ServerTotalSlides += greatSlides;
            ServerTotalRuns += 1;
            api.Post<object>("/scores",
                new ScoreSubmitRequest { score = runScore, island = island, great_slides = greatSlides },
                result => { if (!result.Success) ReportError(result.Error); });
        }

        public void SyncLevel(float totalDistance)
        {
            if (!IsLoggedIn) return;
            api.Put<LevelResponse>("/auth/level", new LevelSyncRequest { total_distance = totalDistance }, result =>
            {
                if (!result.Success) { ReportError(result.Error); return; }
                if (result.Data.user_level == ServerLevel) return;
                ServerLevel = result.Data.user_level;
                OnLevelChanged?.Invoke();
            });
        }

        public void RerollNickname()
        {
            if (!IsLoggedIn) return;
            api.Post<NicknameResponse>("/auth/nickname/reroll", result =>
            {
                if (result.Success) { ServerNickname = result.Data.nickname; OnNicknameChanged?.Invoke(); }
                else ReportError(result.Error);
            });
        }

        public void SetNickname(string nickname)
        {
            if (!IsLoggedIn || string.IsNullOrEmpty(nickname) || nickname == ServerNickname) return;
            api.Put<NicknameResponse>("/auth/nickname", new NicknameSetRequest { nickname = nickname }, result =>
            {
                if (result.Success) { ServerNickname = result.Data.nickname; OnNicknameChanged?.Invoke(); }
                else ReportError(result.Error);
            });
        }

        private void HandleTokenResult(ApiClient.ApiResult<TokenResponse> result)
        {
            if (!result.Success)
            {
                ReportError(result.Error);
                return;
            }

            api.AuthToken = result.Data.access_token;
            ServerNickname = result.Data.nickname;
            ServerLevel = result.Data.user_level;
            ServerCoins = result.Data.coins;
            ServerTotalSlides = result.Data.total_slides;
            ServerTotalRuns = result.Data.total_runs;
            IsLoggedIn = true;
            Persist(result.Data.access_token);
            OnLoggedIn?.Invoke();
            OnNicknameChanged?.Invoke();
            OnLevelChanged?.Invoke();
        }

        private void ReportError(string error)
        {
            LastError = string.IsNullOrEmpty(error) ? "Network error" : error;
            OnAuthError?.Invoke(LastError);
        }

        private void Persist(string token)
        {
            if (SaveSystem.Instance == null) return;
            SaveSystem.Instance.Data.authToken = token ?? "";
            SaveSystem.Instance.Save();
        }
    }
}
