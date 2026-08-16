using UnityEngine;

namespace FlyingChick
{
    // Every SFX/BGM clip in this game is synthesized at runtime (sine waves
    // + simple attack/release envelopes) -- consistent with the project's
    // "everything code-generated, zero imported assets" approach used for
    // every sprite so far. These are simple beeps/chimes/sweeps, not real
    // sound design; swap in composed audio files later by changing what
    // AudioManager assigns to each AudioSource.
    public static class ProceduralAudio
    {
        private const int SampleRate = 44100;

        public static AudioClip Tone(string name, float frequency, float duration, float volume, float attackFrac = 0.05f, float releaseFrac = 0.4f)
        {
            int samples = Mathf.Max(1, Mathf.CeilToInt(SampleRate * duration));
            var data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                float t01 = (float)i / samples;
                float env = Envelope(t01, attackFrac, releaseFrac);
                data[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * volume * env;
            }
            return Build(name, data);
        }

        // Short sequence of notes played back to back -- coin/great-slide/fever chimes.
        public static AudioClip Chime(string name, float[] frequencies, float noteDuration, float volume)
        {
            int samplesPerNote = Mathf.Max(1, Mathf.CeilToInt(SampleRate * noteDuration));
            var data = new float[samplesPerNote * frequencies.Length];
            for (int n = 0; n < frequencies.Length; n++)
            {
                for (int i = 0; i < samplesPerNote; i++)
                {
                    float t = (float)i / SampleRate;
                    float t01 = (float)i / samplesPerNote;
                    float env = Envelope(t01, 0.05f, 0.55f);
                    data[n * samplesPerNote + i] = Mathf.Sin(2f * Mathf.PI * frequencies[n] * t) * volume * env;
                }
            }
            return Build(name, data);
        }

        // Frequency sweep (rises if endFreq > startFreq) -- launch whoosh, speed boost.
        public static AudioClip Sweep(string name, float startFreq, float endFreq, float duration, float volume)
        {
            int samples = Mathf.Max(1, Mathf.CeilToInt(SampleRate * duration));
            var data = new float[samples];
            float phase = 0f;
            for (int i = 0; i < samples; i++)
            {
                float t01 = (float)i / samples;
                float freq = Mathf.Lerp(startFreq, endFreq, t01);
                phase += 2f * Mathf.PI * freq / SampleRate;
                float env = Envelope(t01, 0.05f, 0.5f);
                data[i] = Mathf.Sin(phase) * volume * env;
            }
            return Build(name, data);
        }

        // Short white-noise burst -- neutral UI click.
        public static AudioClip NoiseBurst(string name, float duration, float volume)
        {
            int samples = Mathf.Max(1, Mathf.CeilToInt(SampleRate * duration));
            var data = new float[samples];
            var rng = new System.Random(12345);
            for (int i = 0; i < samples; i++)
            {
                float t01 = (float)i / samples;
                float env = Envelope(t01, 0.02f, 0.8f);
                data[i] = ((float)rng.NextDouble() * 2f - 1f) * volume * env;
            }
            return Build(name, data);
        }

        // Two-layer sine pad, faded to silence at both ends so AudioSource's
        // hard loop point never clicks -- a placeholder ambient bed, not real
        // composed music. Real BGM should replace this once available.
        public static AudioClip Pad(string name, float freq1, float freq2, float duration, float volume)
        {
            int samples = Mathf.Max(1, Mathf.CeilToInt(SampleRate * duration));
            var data = new float[samples];
            float fadeSamples = SampleRate * 0.5f;
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                float fadeIn = Mathf.Clamp01(i / fadeSamples);
                float fadeOut = Mathf.Clamp01((samples - 1 - i) / fadeSamples);
                float env = Mathf.Min(fadeIn, fadeOut);
                float wave = Mathf.Sin(2f * Mathf.PI * freq1 * t) * 0.6f + Mathf.Sin(2f * Mathf.PI * freq2 * t) * 0.4f;
                data[i] = wave * volume * env * 0.5f;
            }
            return Build(name, data);
        }

        private static float Envelope(float t01, float attackFrac, float releaseFrac)
        {
            float attack = attackFrac <= 0f ? 1f : Mathf.Clamp01(t01 / attackFrac);
            float release = releaseFrac <= 0f ? 1f : Mathf.Clamp01((1f - t01) / releaseFrac);
            return Mathf.Min(attack, release);
        }

        private static AudioClip Build(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
