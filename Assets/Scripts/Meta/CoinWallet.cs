using System;
using UnityEngine;

namespace HillyWings
{
    // 코인 지갑. SaveSystem.Data.coins(로컬)과 서버 DB(로그인 시) 양쪽에 유지됨.
    //
    // 오프라인: 로컬 JSON에만 저장.
    // 로그인 후: 서버 값이 권위 있는 소스 — 로그인 시 서버 값을 가져와 로컬에
    // 반영하고, 이후 AddCoins/SpendCoins마다 서버에 즉시 push(fire-and-forget).
    // 싱글톤 아님 — GameBootstrapper가 Configure/BindAuth로 연결함.
    //
    // 점수→코인 환산 비율(score/50)은 레퍼런스(hilly-wings.html)에 원본이
    // 없는 발명 값 — 실제 밸런스 패스 때 scoreToCoinsRatio 하나로 조절.
    public class CoinWallet : MonoBehaviour
    {
        [SerializeField] private float scoreToCoinsRatio = 1f / 50f;

        public int Coins { get; private set; }
        public int LastRunCoinsAwarded { get; private set; }

        public event Action<int> OnCoinsChanged;

        private GameManager gameManager;
        private ScoreManager score;
        private AuthService auth; // null = 비로그인/오프라인

        public void Configure(GameManager gameManagerRef, ScoreManager scoreRef)
        {
            gameManager = gameManagerRef;
            score = scoreRef;
            gameManager.OnRunEnd += HandleRunEnd;
        }

        // 로그인 연동 -- GameBootstrapper가 Configure 이후에 호출.
        // auth가 null이면 오프라인 전용 모드로 동작(로컬 저장만).
        public void BindAuth(AuthService authRef)
        {
            auth = authRef;
            if (auth == null) return;
            auth.OnLoggedIn += HandleLoggedIn;
            auth.OnLoggedOut += HandleLoggedOut;
        }

        private void Awake()
        {
            Coins = SaveSystem.Instance != null ? SaveSystem.Instance.Data.coins : 0;
        }

        private void OnDestroy()
        {
            if (gameManager != null) gameManager.OnRunEnd -= HandleRunEnd;
            if (auth != null)
            {
                auth.OnLoggedIn -= HandleLoggedIn;
                auth.OnLoggedOut -= HandleLoggedOut;
            }
        }

        // 로그인 성공 시 호출 -- 서버 코인을 권위 있는 값으로 삼되,
        // 오프라인 중 더 많이 번 경우를 보호하기 위해 max(로컬, 서버)를 씀.
        // 로컬이 더 크면 서버에도 push해서 둘을 일치시킴.
        private void HandleLoggedIn()
        {
            int serverCoins = auth.ServerCoins;
            int merged = Mathf.Max(Coins, serverCoins);
            if (merged != Coins)
            {
                Coins = merged;
                Persist();
                OnCoinsChanged?.Invoke(Coins);
            }
            // 로컬이 서버보다 크면 즉시 서버에도 반영
            if (merged > serverCoins)
                auth.SyncCoins(Coins);
        }

        private void HandleLoggedOut()
        {
            // 로그아웃해도 코인은 로컬에 그대로 유지(리셋 안 함)
        }

        public void AddCoins(int amount)
        {
            if (amount == 0) return;
            Coins += amount;
            Persist();
            OnCoinsChanged?.Invoke(Coins);
            auth?.SyncCoins(Coins);
        }

        // 알 구매 등에서 사용. 잔액 부족이면 false 반환(차감 없음).
        public bool SpendCoins(int amount)
        {
            if (amount <= 0 || Coins < amount) return false;
            Coins -= amount;
            Persist();
            OnCoinsChanged?.Invoke(Coins);
            auth?.SyncCoins(Coins);
            return true;
        }

        private void HandleRunEnd()
        {
            LastRunCoinsAwarded = Mathf.FloorToInt(score.Score * scoreToCoinsRatio);
            AddCoins(LastRunCoinsAwarded);
        }

        private void Persist()
        {
            if (SaveSystem.Instance == null) return;
            SaveSystem.Instance.Data.coins = Coins;
            SaveSystem.Instance.Save();
        }
    }
}
