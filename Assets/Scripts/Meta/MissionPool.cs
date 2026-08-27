namespace HillyWings
{
    // Shared mission vocabulary for DailyMissions (persisted, cumulative
    // across a calendar day) and NestMultiplier (per-run, evaluated once at
    // Day Over against that run's live stats). "버블 트램폴린" from the
    // original spec isn't implemented, so it's excluded from both pools --
    // every mission here maps to a stat that already exists.
    //
    // Plain C# data rather than ScriptableObject assets: this project has
    // been fully code-driven end to end (zero manual Editor setup needed to
    // press Play), and hand-authoring .asset YAML without the Editor open
    // would be fragile. Worth revisiting as real ScriptableObjects once
    // there's an in-Editor content-authoring workflow.
    public enum MissionType
    {
        FeverTriggerCount,
        ReachIsland,
        CollectCoins,
        GreatSlideCount,
        CloudTouchCount,
        ScoreReached,
        FeverDuration
    }

    public struct MissionDefinition
    {
        public MissionType Type;
        public int Target;

        // Localized at read time (Localization.Current can change any time
        // via the start screen's language toggle) rather than baked in as a
        // fixed string -- the format template is keyed by Type alone since
        // Target already parameterizes it (see Localization's mission.*
        // entries), so this works for any target value without needing one
        // table entry per (Type, Target) combination.
        public string Description => string.Format(Localization.Get($"mission.{Type}"), Target);

        public MissionDefinition(MissionType type, int target)
        {
            Type = type;
            Target = target;
        }
    }

    public static class MissionPool
    {
        public static readonly MissionDefinition[] Daily =
        {
            new MissionDefinition(MissionType.FeverTriggerCount, 3),
            new MissionDefinition(MissionType.ReachIsland, 6),
            new MissionDefinition(MissionType.CollectCoins, 50),
            new MissionDefinition(MissionType.GreatSlideCount, 20),
            new MissionDefinition(MissionType.CloudTouchCount, 5),
        };

        public static readonly MissionDefinition[] Nest =
        {
            new MissionDefinition(MissionType.CloudTouchCount, 3),
            new MissionDefinition(MissionType.FeverDuration, 5),
            new MissionDefinition(MissionType.ScoreReached, 5000),
            new MissionDefinition(MissionType.GreatSlideCount, 10),
            new MissionDefinition(MissionType.ReachIsland, 3),
        };
    }
}
