using System.Collections.Generic;
using UnityEngine;

namespace FlyingChick
{
    // Randomized rolling-hill terrain: a sequence of control points at
    // random x-spacing (hill width) and random y-offset (hill height),
    // smoothly cosine-interpolated between them (zero slope exactly at each
    // peak/valley, so there's no kink for the physics to snag on). Replaces
    // the earlier fixed sine-wave terrain per playtest feedback -- hills now
    // vary in both width and height instead of repeating one period.
    //
    // Canvas-space convention: y grows downward. Stateful and seeded
    // (deterministic per run), so it's an instance, not a static class.
    // GameManager owns the single instance; TerrainGenerator and BirdPhysics
    // both query it so the rendered hill and the physics ground line never
    // disagree.
    public class GroundSampler
    {
        private struct ControlPoint
        {
            public float X;
            public float Y;
        }

        private readonly System.Random rng;
        private readonly List<ControlPoint> points = new List<ControlPoint>();
        private readonly float baseY;
        private readonly float minHalfWave;
        private readonly float maxHalfWave;
        private readonly float minAmp;
        private readonly float maxAmp;
        private float lastDirection = -1f;

        public float BaseY => baseY;
        public float MaxAmplitude => maxAmp;

        public GroundSampler(int seed, float viewHeight)
        {
            rng = new System.Random(seed);
            baseY = viewHeight * 0.66f;
            // Narrowed range (was 0.22-0.58 / 0.10-0.30) -- full random
            // width/height variety was too hard to read/play. Kept as a
            // gentle interim baseline; proper difficulty-curve design for
            // this is deferred to a later level-balance pass. Widened again
            // (0.30-0.42 -> 0.36-0.50) per playtest feedback -- more time
            // between hills to react. Height range left untouched.
            //
            // Briefly gentled further (0.36-0.50 -> 0.45-0.62 / 0.11-0.16 ->
            // 0.07-0.10) per "언덕이 좀더 완만하고" feedback, then reverted
            // (2026-08-20) after "게임이 너무 쉬워졌다" -- restored to these
            // values.
            minHalfWave = viewHeight * 0.36f;
            maxHalfWave = viewHeight * 0.50f;
            minAmp = viewHeight * 0.11f;
            maxAmp = viewHeight * 0.16f;

            // A short flat run behind and right at the start so the bird
            // spawns on calm ground, and so groundSlope's +-2 finite
            // difference near x=0 never reads past the front of the list.
            points.Add(new ControlPoint { X = -maxHalfWave * 2f, Y = baseY });
            points.Add(new ControlPoint { X = 0f, Y = baseY });
            EnsureCoverage(maxHalfWave * 4f);
        }

        public float GroundY(float worldX)
        {
            EnsureCoverage(worldX + maxHalfWave);
            int i = FindSegment(worldX);
            ControlPoint a = points[i];
            ControlPoint b = points[i + 1];
            float t = Mathf.InverseLerp(a.X, b.X, worldX);
            float eased = (1f - Mathf.Cos(t * Mathf.PI)) * 0.5f;
            return Mathf.Lerp(a.Y, b.Y, eased);
        }

        public float GroundSlope(float worldX)
        {
            const float d = 2f;
            return (GroundY(worldX + d) - GroundY(worldX - d)) / (2f * d);
        }

        private void EnsureCoverage(float upToX)
        {
            while (points[points.Count - 1].X < upToX)
            {
                float halfWave = Mathf.Lerp(minHalfWave, maxHalfWave, (float)rng.NextDouble());
                float amp = Mathf.Lerp(minAmp, maxAmp, (float)rng.NextDouble());
                lastDirection *= -1f;
                float nextX = points[points.Count - 1].X + halfWave;
                float nextY = baseY + lastDirection * amp;
                points.Add(new ControlPoint { X = nextX, Y = nextY });
            }
        }

        private int FindSegment(float worldX)
        {
            int lo = 0, hi = points.Count - 2;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                if (points[mid].X <= worldX) lo = mid; else hi = mid - 1;
            }
            return lo;
        }
    }
}
