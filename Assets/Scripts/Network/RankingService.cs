using System;
using UnityEngine;

namespace FlyingChick
{
    // Wraps FlyingChick-Server's /rankings endpoints. Not a singleton, same
    // reasoning as AuthService. GetRankings is public (no login needed to
    // VIEW rankings); GetMyRank needs AuthService to be logged in first
    // (the server 401s otherwise -- callers should check
    // AuthService.IsLoggedIn before calling it).
    //
    // UI consumption of this (the Day Over ranking panel) is a later piece
    // of work -- this class only wraps the HTTP calls.
    public class RankingService : MonoBehaviour
    {
        public const string PeriodDaily = "daily";
        public const string PeriodWeekly = "weekly";
        public const string PeriodAllTime = "alltime";

        private ApiClient api;

        public void Configure(ApiClient apiClient)
        {
            api = apiClient;
        }

        public void GetRankings(string period, int limit, Action<ApiClient.ApiResult<RankingResponse>> callback)
        {
            api.Get($"/rankings?period={period}&limit={limit}", callback);
        }

        // rank <= 0 in the result means "no score recorded in this window"
        // (the server sends null for both fields in that case; JsonUtility
        // has no nullable-int support, so 0 -- an otherwise-impossible rank
        // -- is the sentinel; see ApiModels.cs).
        public void GetMyRank(string period, Action<ApiClient.ApiResult<MyRankResponse>> callback)
        {
            api.Get($"/rankings/me?period={period}", callback);
        }
    }
}
