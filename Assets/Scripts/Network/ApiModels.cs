using System;

namespace HillyWings
{
    // Request/response DTOs for HillyWings-Server, deserialized via
    // JsonUtility (same approach the local save system already uses --
    // see Meta/SaveData.cs -- so no new JSON library is introduced).
    // JsonUtility maps fields by exact name with no renaming support, so
    // these fields are snake_case to match the server's JSON keys
    // (FastAPI/Pydantic) rather than normal C# PascalCase.

    [Serializable]
    public class SignupRequest
    {
        public string login_id;
        public string password;
    }

    [Serializable]
    public class LoginRequest
    {
        public string login_id;
        public string password;
    }

    [Serializable]
    public class TokenResponse
    {
        public string access_token;
        public string token_type;
        public string nickname;
        public int user_level;
        public int coins;
        public int total_slides;
        public int total_runs;
    }

    [Serializable]
    public class MeResponse
    {
        public string login_id;
        public string nickname;
        public int user_level;
        public int coins;
        public int total_slides;
        public int total_runs;
        public string created_at;
    }

    [Serializable]
    public class NicknameResponse
    {
        public string nickname;
    }

    [Serializable]
    public class NicknameSetRequest
    {
        public string nickname;
    }

    [Serializable]
    public class RankingEntry
    {
        public int rank;
        public string nickname;
        public int score;
        public string achieved_at;
    }

    [Serializable]
    public class RankingResponse
    {
        public string period;
        public RankingEntry[] entries;
    }

    [Serializable]
    public class MyRankResponse
    {
        public string period;
        public int rank; // 0 when the server sent null (no score in this window) -- see RankingService comment
        public int best_score;
    }

    // 런 종료 결과 -- DayOverScreen → AuthService.SubmitScore()가 사용.
    [Serializable]
    public class ScoreSubmitRequest
    {
        public int score;
        public int island;
        public int great_slides;
    }

    // 코인 동기화 -- CoinWallet이 현재 보유 코인 총액을 PUT /auth/coins로 보냄.
    [Serializable]
    public class CoinSyncRequest
    {
        public int coins;
    }

    [Serializable]
    public class CoinResponse
    {
        public int coins;
    }

    // GET /scores/me 응답 -- 이 유저 본인의 개인 최고 기록 Top-N.
    [Serializable]
    public class UserScoreEntry
    {
        public int score;
        public int island;
        public int great_slides;
        public string created_at;
    }

    [Serializable]
    public class UserScoresResponse
    {
        public UserScoreEntry[] scores;
    }

    // "총 이동한 거리값을 가지고 레벨업" 요청 -- 클라이언트는 누적 거리만
    // 보내고, 레벨은 서버가 공유 레벨 테이블(level_ranges.json, 유니티도
    // 같은 파일을 갖고 있음 -- Meta/LevelConfig.cs)로 다시 계산해서 돌려줌.
    [Serializable]
    public class LevelSyncRequest
    {
        public float total_distance;
    }

    [Serializable]
    public class LevelResponse
    {
        public int user_level;
    }
}
