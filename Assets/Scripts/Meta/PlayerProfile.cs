using System;
using UnityEngine;

namespace FlyingChick
{
    // Holds the player's local display nickname, persisted through
    // SaveSystem.Data.nickname. Not a singleton (only GameManager/
    // ScoreManager/SaveSystem are, per project convention) -- StartScreen
    // gets it via Configure. First run auto-generates one via
    // NicknameGenerator; after that it only changes via explicit reroll or
    // manual edit on the start screen.
    //
    // Scope decision: the nickname is shown on the start screen but is NOT
    // attached to individual Leaderboard rows -- the local Top 10 is all
    // this same single player's runs, so tagging every row with the same
    // name would add no information. If this ever grows into a shared/
    // online leaderboard, that's the point to revisit.
    public class PlayerProfile : MonoBehaviour
    {
        public string Nickname { get; private set; } = "";

        public event Action OnNicknameChanged;

        public void Configure()
        {
            var data = SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;
            if (data != null && !string.IsNullOrEmpty(data.nickname))
            {
                Nickname = data.nickname;
                return;
            }

            Nickname = NicknameGenerator.Generate();
            Persist();
        }

        public void Reroll()
        {
            Nickname = NicknameGenerator.Generate();
            Persist();
            OnNicknameChanged?.Invoke();
        }

        // Trims and length-caps so a runaway paste can't blow out the UI
        // layout; empty input is ignored rather than allowing a blank name.
        public void SetNickname(string nickname)
        {
            nickname = (nickname ?? string.Empty).Trim();
            if (nickname.Length == 0 || nickname == Nickname) return;
            if (nickname.Length > 16) nickname = nickname.Substring(0, 16);

            Nickname = nickname;
            Persist();
            OnNicknameChanged?.Invoke();
        }

        private void Persist()
        {
            if (SaveSystem.Instance == null) return;
            SaveSystem.Instance.Data.nickname = Nickname;
            SaveSystem.Instance.Save();
        }
    }
}
