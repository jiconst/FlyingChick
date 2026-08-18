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
        public string LastError { get; private set; }

        public event Action OnLoggedIn;
        public event Action OnLoggedOut;
        public event Action<string> OnAuthError; // human-readable message
        public event Action OnNicknameChanged; // ServerNickname 값이 바뀔 때마다(로그인/회원가입 포함) — PlayerProfile.OnNicknameChanged와 같은 패턴

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
                    IsLoggedIn = true;
                    OnLoggedIn?.Invoke();
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
            Persist(null);
            OnLoggedOut?.Invoke();
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
            IsLoggedIn = true;
            Persist(result.Data.access_token);
            OnLoggedIn?.Invoke();
            OnNicknameChanged?.Invoke();
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
