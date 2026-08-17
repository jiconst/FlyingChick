using UnityEngine;

namespace FlyingChick
{
    // Random "adjective + chick + number" nickname (e.g. "용감한 병아리123" /
    // "BraveChick123"), generated fresh for every new player and whenever
    // the player hits "reroll" on the start screen. Word lists are picked
    // from whichever language is active AT GENERATION TIME
    // (Localization.Current) -- the nickname itself doesn't change if the
    // player later switches languages (it's an identity, not UI text), only
    // an explicit reroll or manual edit changes it.
    public static class NicknameGenerator
    {
        private static readonly string[] KoreanAdjectives =
        {
            "용감한", "날쌘", "졸린", "배고픈", "행복한", "씩씩한", "포근한", "엉뚱한", "느긋한", "반짝이는"
        };

        private static readonly string[] EnglishAdjectives =
        {
            "Brave", "Swift", "Sleepy", "Hungry", "Happy", "Bold", "Cozy", "Silly", "Chill", "Shiny"
        };

        public static string Generate()
        {
            int number = Random.Range(0, 1000);
            if (Localization.Current == Language.Korean)
            {
                string adj = KoreanAdjectives[Random.Range(0, KoreanAdjectives.Length)];
                return $"{adj} 병아리{number}";
            }
            else
            {
                string adj = EnglishAdjectives[Random.Range(0, EnglishAdjectives.Length)];
                return $"{adj}Chick{number}";
            }
        }
    }
}
