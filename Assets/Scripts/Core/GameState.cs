namespace FlyingChick
{
    // Start: idle on the title screen (bird sits still, any input begins).
    // Playing: normal gameplay -- physics/scroll/scoring/spawning all tick.
    // DayOver: day-length timer ran out, final-stats screen shown.
    public enum GameState
    {
        Start,
        Playing,
        DayOver
    }
}
