using System.Collections.Generic;
using UnityEngine;

namespace FlyingChick
{
    // Cloud placement, ported from flying-chick.html's ensureClouds(). Clouds
    // sit at a fixed canvas-Y band (not tied to ground height). Pure data --
    // CloudSpawner owns pooled visuals and collision. Canvas-space convention.
    public class CloudField
    {
        public struct CloudEntry
        {
            public float WorldX;
            public float CanvasY;
            public float Scale;
            public bool Touched;
        }

        private readonly System.Random rng;
        private readonly List<CloudEntry> entries = new List<CloudEntry>();
        private readonly float viewHeight;
        private readonly float startX;

        public IReadOnlyList<CloudEntry> Entries => entries;

        public CloudField(int seed, float viewHeightValue, float startXValue)
        {
            rng = new System.Random(seed);
            viewHeight = viewHeightValue;
            startX = startXValue;
        }

        public void EnsureCoverage(float uptoWorldX)
        {
            while (entries.Count == 0 || entries[entries.Count - 1].WorldX < uptoWorldX)
            {
                float baseX = entries.Count > 0 ? entries[entries.Count - 1].WorldX : startX;
                float wx = baseX + 380f + (float)rng.NextDouble() * 420f;
                float y = viewHeight * 0.10f + (float)rng.NextDouble() * viewHeight * 0.14f;
                float s = 0.8f + (float)rng.NextDouble() * 0.7f;
                entries.Add(new CloudEntry { WorldX = wx, CanvasY = y, Scale = s, Touched = false });
            }
        }

        public void MarkTouched(int index)
        {
            var e = entries[index];
            e.Touched = true;
            entries[index] = e;
        }
    }
}
