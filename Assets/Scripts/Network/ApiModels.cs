using System;

namespace FlyingChick
{
    // Request/response DTOs for FlyingChick-Server, deserialized via
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
    }

    [Serializable]
    public class MeResponse
    {
        public string login_id;
        public string nickname;
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
}
