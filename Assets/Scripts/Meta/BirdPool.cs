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
        public Color BodyColor;
        public Color WingColor;
        public Color BellyColor;
        public PerkType Perk;
        public float PerkValue;

        // Localized at read time, same reasoning as MissionDefinition.Description.
        public string Name => Localization.Get($"bird.{Id}");

        public string PerkDescription => Perk == PerkType.None
            ? Localization.Get("perk.none")
            : string.Format(Localization.Get($"perk.{Perk}"), Perk == PerkType.SlideScoreBonus ? PerkValue * 100f : PerkValue);

        public BirdDefinition(string id, Color body, Color wing, Color belly, PerkType perk, float perkValue)
        {
            Id = id;
            BodyColor = body;
            WingColor = wing;
            BellyColor = belly;
            Perk = perk;
            PerkValue = perkValue;
        }
    }

    public static class BirdPool
    {
        public const string DefaultBirdId = "chick_yellow";
        public const int EggCostCoins = 500;

        public static readonly BirdDefinition[] All =
        {
            new BirdDefinition("chick_yellow",
                new Color(1f, 0.86f, 0.25f), new Color(0.93f, 0.72f, 0.15f), new Color(1f, 0.97f, 0.82f),
                PerkType.None, 0f),

            new BirdDefinition("chick_red",
                new Color(0.95f, 0.35f, 0.25f), new Color(0.8f, 0.2f, 0.15f), new Color(1f, 0.85f, 0.75f),
                PerkType.SlideScoreBonus, 0.10f),

            new BirdDefinition("chick_blue",
                new Color(0.3f, 0.6f, 0.95f), new Color(0.2f, 0.45f, 0.8f), new Color(0.85f, 0.93f, 1f),
                PerkType.FeverDurationBonus, 1f),

            new BirdDefinition("chick_green",
                new Color(0.4f, 0.8f, 0.35f), new Color(0.25f, 0.6f, 0.2f), new Color(0.9f, 1f, 0.85f),
                PerkType.CoinMagnet, 20f),

            new BirdDefinition("chick_purple",
                new Color(0.65f, 0.4f, 0.9f), new Color(0.5f, 0.25f, 0.75f), new Color(0.92f, 0.85f, 1f),
                PerkType.StartSpeedBonus, 50f),
        };

        public static BirdDefinition Find(string id)
        {
            foreach (var b in All)
                if (b.Id == id) return b;
            return All[0];
        }
    }
}
