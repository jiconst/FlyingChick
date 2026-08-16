using System.Collections.Generic;
using UnityEngine;

namespace FlyingChick
{
    // Coin placement, ported 1:1 from flying-chick.html's ensureCoins().
    // Pure data (no MonoBehaviour) -- CoinSpawner owns pooled visuals and
    // collision, reading this list. Canvas-space convention.
    public class CoinField
    {
        public enum CoinType { Coin, Speed }

        public struct CoinEntry
        {
            public float WorldX;
            public float Offset; // height above ground, canvas-space (subtracted from groundY)
            public CoinType Type;
            public bool Taken;
        }

        private readonly System.Random rng;
        private readonly List<CoinEntry> entries = new List<CoinEntry>();
        private float lastCoinX;

        public IReadOnlyList<CoinEntry> Entries => entries;

        public CoinField(int seed, float startX)
        {
            rng = new System.Random(seed);
            lastCoinX = startX;
        }

        public void EnsureCoverage(float uptoWorldX)
        {
            while (lastCoinX < uptoWorldX)
            {
                lastCoinX += 90f + (float)rng.NextDouble() * 60f;
                double roll = rng.NextDouble();

                if (roll < 0.10)
                {
                    // Reference offset (60-100) assumed taller hills/higher
                    // launches than our current tuning reaches -- lowered so
                    // it's actually catchable mid-flight.
                    entries.Add(new CoinEntry
                    {
                        WorldX = lastCoinX,
                        Offset = 28f + (float)rng.NextDouble() * 26f,
                        Type = CoinType.Speed
                    });
                }
                else if (roll < 0.75)
                {
                    int run = 3 + (int)(rng.NextDouble() * 4);
                    for (int i = 0; i < run; i++)
                    {
                        float off = 42f + Mathf.Sin((float)i / run * Mathf.PI) * 36f;
                        entries.Add(new CoinEntry
                        {
                            WorldX = lastCoinX + i * 38f,
                            Offset = off,
                            Type = CoinType.Coin
                        });
                    }
                    lastCoinX += run * 38f;
                }
                // else (roll >= 0.75): a gap, no coins this cycle -- matches reference pacing.
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
