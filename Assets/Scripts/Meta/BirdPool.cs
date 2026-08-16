using UnityEngine;

namespace FlyingChick
{
    // Plain C# data, not ScriptableObject assets -- same reasoning as
    // MissionPool (M5): this project stays fully code-driven with zero
    // manual Editor authoring, and hand-written .asset YAML would be fragile.
    public enum PerkType
    {
        None,
        SlideScoreBonus,    // +% score on every Great Slide
        FeverDurationBonus, // +seconds to Fever's base/extend duration
        CoinMagnet,         // +radius on coin pickup detection
        StartSpeedBonus     // +speed at the start of every run
    }

    public struct BirdDefinition
    {
        public string Id;
        public string Name;
        public Color BodyColor;
        public Color WingColor;
        public Color BellyColor;
        public PerkType Perk;
        public float PerkValue;
        public string PerkDescription;

        public BirdDefinition(string id, string name, Color body, Color wing, Color belly, PerkType perk, float perkValue, string perkDescription)
        {
            Id = id;
            Name = name;
            BodyColor = body;
            WingColor = wing;
            BellyColor = belly;
            Perk = perk;
            PerkValue = perkValue;
            PerkDescription = perkDescription;
        }
    }

    public static class BirdPool
    {
        public const string DefaultBirdId = "chick_yellow";
        public const int EggCostCoins = 500;

        public static readonly BirdDefinition[] All =
        {
            new BirdDefinition("chick_yellow", "노랑 병아리",
                new Color(1f, 0.86f, 0.25f), new Color(0.93f, 0.72f, 0.15f), new Color(1f, 0.97f, 0.82f),
                PerkType.None, 0f, "기본 병아리"),

            new BirdDefinition("chick_red", "빨강 병아리",
                new Color(0.95f, 0.35f, 0.25f), new Color(0.8f, 0.2f, 0.15f), new Color(1f, 0.85f, 0.75f),
                PerkType.SlideScoreBonus, 0.10f, "슬라이드 점수 +10%"),

            new BirdDefinition("chick_blue", "파랑 병아리",
                new Color(0.3f, 0.6f, 0.95f), new Color(0.2f, 0.45f, 0.8f), new Color(0.85f, 0.93f, 1f),
                PerkType.FeverDurationBonus, 1f, "Fever 지속시간 +1초"),

            new BirdDefinition("chick_green", "초록 병아리",
                new Color(0.4f, 0.8f, 0.35f), new Color(0.25f, 0.6f, 0.2f), new Color(0.9f, 1f, 0.85f),
                PerkType.CoinMagnet, 20f, "코인 획득 범위 +20"),

            new BirdDefinition("chick_purple", "보라 병아리",
                new Color(0.65f, 0.4f, 0.9f), new Color(0.5f, 0.25f, 0.75f), new Color(0.92f, 0.85f, 1f),
                PerkType.StartSpeedBonus, 50f, "시작 속도 +50"),
        };

        public static BirdDefinition Find(string id)
        {
            foreach (var b in All)
                if (b.Id == id) return b;
            return All[0];
        }
    }
}
