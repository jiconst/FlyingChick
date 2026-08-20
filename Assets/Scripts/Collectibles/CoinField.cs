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
                    // it's actually catchable mid-flight. Lowered again
                    // (28-54 -> 14-26) per "언덕에 가급적 붙어 있었으면" feedback.
                    entries.Add(new CoinEntry
                    {
                        WorldX = lastCoinX,
                        Offset = 14f + (float)rng.NextDouble() * 12f,
                        Type = CoinType.Speed
                    });
                }
                else if (roll < 0.40)
                {
                    // "노란색 골드나 나오는 횟수를 줄이고" 피드백 -- 골드 코인 런이
                    // 걸릴 확률을 0.65(0.10~0.75)에서 0.30(0.10~0.40)으로 낮춤,
                    // 그만큼 아무것도 없는 구간(roll>=0.40)이 늘어남. 높이도
                    // 42~78 -> 18~34로 낮춰서 언덕에 더 붙어 있게 함.
                    int run = 3 + (int)(rng.NextDouble() * 4);
                    for (int i = 0; i < run; i++)
                    {
                        float off = 18f + Mathf.Sin((float)i / run * Mathf.PI) * 16f;
                        entries.Add(new CoinEntry
                        {
                            WorldX = lastCoinX + i * 38f,
                            Offset = off,
                            Type = CoinType.Coin
                        });
                    }
                    lastCoinX += run * 38f;
                }
                // else (roll >= 0.40): a gap, no coins this cycle.
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
