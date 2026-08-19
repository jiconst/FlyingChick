using System.Collections.Generic;
using UnityEngine;

namespace FlyingChick
{
    // Green "Sky Flight" orb placement. Pure data (no MonoBehaviour) --
    // SkyOrbSpawner owns pooled visuals and pickup detection, reading this
    // list, same split as CoinField/CloudField. Canvas-space convention.
    //
    // Placement is intentionally simple/random for now (per feedback: this
    // pickup is a test build to validate the Sky Flight feel first --
    // spacing/rarity/height will get a real rebalancing pass later once the
    // effect itself is confirmed fun). Height above ground uses the exact
    // same offset range as CoinField's blue speed coin (per feedback: put
    // the green orb where the blue orb sits) -- stores an Offset, not a
    // fixed CanvasY, so SkyOrbSpawner recomputes canvasY from the CURRENT
    // ground height at that WorldX every frame, same as CoinSpawner does.
    public class SkyOrbField
    {
        public struct OrbEntry
        {
            public float WorldX;
            public float Offset; // height above ground, canvas-space (subtracted from groundY)
            public bool Taken;
        }

        private readonly System.Random rng;
        private readonly List<OrbEntry> entries = new List<OrbEntry>();
        private readonly float startX;

        public IReadOnlyList<OrbEntry> Entries => entries;

        public SkyOrbField(int seed, float startXValue)
        {
            rng = new System.Random(seed);
            startX = startXValue;
        }

        public void EnsureCoverage(float uptoWorldX)
        {
            while (entries.Count == 0 || entries[entries.Count - 1].WorldX < uptoWorldX)
            {
                float baseX = entries.Count > 0 ? entries[entries.Count - 1].WorldX : startX;
                // Rare/random spacing on purpose -- see class comment. The
                // bird's world-space speed ranges roughly 300-980 units/sec
                // (BirdPhysics.SpeedBase/SpeedMax), so the original
                // 900-1800 spacing meant an orb every ~1-6 SECONDS -- far
                // too often for a buff this dramatic ("줌 인/줌 아웃 동작이
                // 이상해졌어, 수시로 너무 큰 줌아웃" feedback). Widened so even
                // at max speed it's at least several seconds between orbs.
                float wx = baseX + 6000f + (float)rng.NextDouble() * 4000f;
                // Same range as CoinField's Speed coin offset.
                float offset = 28f + (float)rng.NextDouble() * 26f;
                entries.Add(new OrbEntry { WorldX = wx, Offset = offset, Taken = false });
            }
        }

        public void MarkTaken(int index)
        {
            var e = entries[index];
            e.Taken = true;
            entries[index] = e;
        }
    }
}
