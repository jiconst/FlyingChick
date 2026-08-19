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

        // Two-layer sine pad for AudioManager's looping BGM_Ambient
        // (AudioSource.loop = true). An earlier version faded to silence at
        // both ends to hide the click where the loop seams together --
        // that turned into a worse problem: fading in/out every single
        // loop (every `duration` seconds) is an audible tremolo/swell, which
        // is exactly the periodic "웅- 웅-" hum-like pulsing players reported
        // hearing under everything else. The actual fix isn't to hide the
        // seam better, it's to not HAVE a seam: freq1/freq2 are snapped to
        // the nearest frequency that completes a whole number of cycles
        // within the clip's real length, so the waveform's value AND slope
        // already match at the wrap-around point -- a genuinely seamless
        // loop, no envelope needed (and the snap is inaudible: at 6s it's
        // at most ~0.08Hz off the requested pitch).
        public static AudioClip Pad(string name, float freq1, float freq2, float duration, float volume)
        {
            int samples = Mathf.Max(1, Mathf.CeilToInt(SampleRate * duration));
            float loopDuration = samples / (float)SampleRate;
            float loopFreq1 = Mathf.Round(freq1 * loopDuration) / loopDuration;
            float loopFreq2 = Mathf.Round(freq2 * loopDuration) / loopDuration;

            var data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                float wave = Mathf.Sin(2f * Mathf.PI * loopFreq1 * t) * 0.6f + Mathf.Sin(2f * Mathf.PI * loopFreq2 * t) * 0.4f;
                data[i] = wave * volume * 0.5f;
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
