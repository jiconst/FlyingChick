namespace FlyingChick
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
        public string Description;

        public MissionDefinition(MissionType type, int target, string description)
        {
            Type = type;
            Target = target;
            Description = description;
        }
    }

    public static class MissionPool
    {
        public static readonly MissionDefinition[] Daily =
        {
            new MissionDefinition(MissionType.FeverTriggerCount, 3, "Fever 3회 발동"),
            new MissionDefinition(MissionType.ReachIsland, 6, "6번째 섬 도달"),
            new MissionDefinition(MissionType.CollectCoins, 50, "코인 50개 획득"),
            new MissionDefinition(MissionType.GreatSlideCount, 20, "Great Slide 20회 성공"),
            new MissionDefinition(MissionType.CloudTouchCount, 5, "구름 5개 터치"),
        };

        public static readonly MissionDefinition[] Nest =
        {
            new MissionDefinition(MissionType.CloudTouchCount, 3, "구름 3개 터치"),
            new MissionDefinition(MissionType.FeverDuration, 5, "Fever 5초 이상 유지"),
            new MissionDefinition(MissionType.ScoreReached, 5000, "5000점 획득"),
            new MissionDefinition(MissionType.GreatSlideCount, 10, "Great Slide 10회 성공"),
            new MissionDefinition(MissionType.ReachIsland, 3, "3번째 섬 도달"),
        };
    }
}
