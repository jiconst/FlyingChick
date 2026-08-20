using UnityEngine;

namespace FlyingChick
{
    // "총 이동한 거리값을 가지고 레벨업을 하게 하고 싶어" 요청 -- 이 레벨은
    // 매 런 리셋되는 GameManager.Island(그날그날의 섬 진행도, 배수를 올림)와
    // 완전히 다른 개념: 런이 끝나도 안 사라지는 "계정 전체 평생 누적 이동
    // 거리" 기반 레벨. Not a singleton (GameManager/ScoreManager/SaveSystem
    // 만 허용) -- GameBootstrapper가 CoinWallet/Leaderboard처럼 만들고 연결.
    //
    // 레벨업 기준(거리 구간)은 이 클래스가 정하지 않음 -- LevelConfig.cs가
    // 읽는 공유 JSON 테이블(엑셀에서 export)이 유일한 기준.
    public class PlayerLevel : MonoBehaviour
    {
        public float TotalDistance { get; private set; }

        // 로컬에서 계산한 레벨과 서버 계정에 저장된 레벨 중 더 높은 쪽 --
        // 오프라인 진행(아직 서버에 동기화 못 함)이나 다른 기기의 진행
        // 어느 쪽이든 놓치지 않기 위함. 로그아웃 상태면 서버 값은 항상 1
        // (AuthService의 기본값)이라 사실상 로컬 값만 씀.
        public int Level => Mathf.Max(LevelConfig.LevelForDistance(TotalDistance), auth != null ? auth.ServerLevel : 1);

        private GameManager gameManager;
        private AuthService auth;

        public void Configure(GameManager gameManagerRef, AuthService authRef)
        {
            gameManager = gameManagerRef;
            auth = authRef;

            var data = SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;
            if (data != null) TotalDistance = data.totalDistanceTraveled;

            gameManager.OnRunEnd += HandleRunEnd;
            auth.OnLoggedIn += HandleLoggedIn;
        }

        private void OnDestroy()
        {
            if (gameManager != null) gameManager.OnRunEnd -= HandleRunEnd;
            if (auth != null) auth.OnLoggedIn -= HandleLoggedIn;
        }

        // 그 런의 최종 ScrollX(이동 거리)를 평생 누적치에 더함 -- OnRunEnd는
        // 낮 타이머가 다 됐을 때 State가 DayOver로 바뀌기 직전에 불려서,
        // 그 시점 ScrollX가 이 런에서 실제로 이동한 총 거리 그대로임.
        private void HandleRunEnd()
        {
            TotalDistance += gameManager.ScrollX;
            Persist();

            // 로그인 상태면 서버에도 반영(서버가 공유 테이블로 다시 계산해서
            // max로만 갱신 -- AuthService.SyncLevel/서버 PUT /auth/level 참고).
            // 비로그인이면 AuthService.SyncLevel이 조용히 아무 일도 안 함.
            auth?.SyncLevel(TotalDistance);
        }

        // 로그인/토큰 검증 성공 직후에도 한 번 동기화 -- 다른 기기에서 쌓인
        // 오프라인 진행이 있었다면 이번 기기의 로컬 누적치를 서버에 반영하고,
        // 반대로 서버 쪽이 더 높았다면(다른 기기에서 더 많이 플레이함)
        // AuthService.ServerLevel이 이미 그 값을 갖고 있어서 Level
        // 프로퍼티(Mathf.Max)가 알아서 그쪽을 보여줌 -- 별도 처리 불필요.
        private void HandleLoggedIn() => auth?.SyncLevel(TotalDistance);

        private void Persist()
        {
            if (SaveSystem.Instance == null) return;
            SaveSystem.Instance.Data.totalDistanceTraveled = TotalDistance;
            SaveSystem.Instance.Save();
        }
    }
}
