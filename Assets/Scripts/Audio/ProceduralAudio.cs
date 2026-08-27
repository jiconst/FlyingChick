using UnityEngine;

namespace HillyWings
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

        // 게임 분위기(밝고 통통 튀는 Tiny Wings 스타일)에 맞는 3레이어 BGM.
        // AudioSource.loop = true로 무한 반복. 루프 이음새 클릭 방지:
        //  - 코드 패드(지속음): 루프 길이 안에서 정수 사이클로 주파수를 스냅
        //  - 베이스/멜로디(펄스음): envelope attack/release로 0→amplitude→0이므로
        //    이음새 클릭이 원래 없음.
        // 구성: C장조 펜타토닉, 120BPM, 4마디(8초) 루프
        //  · 코드 패드: C4+E4+G4+C5 메이저 코드, 부드럽게 지속
        //  · 베이스 펄스: C3 루트음, 비트마다 짧게 어택
        //  · 멜로디: 실로폰/글로켄슈필 느낌의 8분음표 아르페지오 32음
        // 160BPM, 4마디(6초) 루프. 4레이어:
        //  1) 코드 패드  — 배경 하모니 (지속음, 루프 정수 사이클 스냅)
        //  2) 베이스     — C2/G2 번갈아, 2·3배음 포함 → 두꺼운 펀치감
        //  3) 아르페지오 — 16분음표 C4-E4-G4-E4 반복 → 리듬 텍스처
        //  4) 멜로디     — 8분음표 기반, 2·3배음 → 밝고 풍성한 음색
        public static AudioClip GameBGM(string name, float bpm = 160f)
        {
            const int Measures = 4;
            float beatSec  = 60f / bpm;                          // 0.375s @ 160BPM
            float loopSec  = Measures * 4f * beatSec;            // 6.0s
            int   totalSmp = Mathf.RoundToInt(SampleRate * loopSec);
            float loopDur  = totalSmp / (float)SampleRate;
            var data = new float[totalSmp];

            // ── 레이어 1: 코드 패드 (C4+E4+G4, 배경 세게 안 나오게) ──────
            float[] padHz  = { 261.63f, 329.63f, 392.00f };
            float[] padAmp = {   0.12f,   0.09f,   0.08f };
            for (int fi = 0; fi < padHz.Length; fi++)
            {
                float f = Mathf.Round(padHz[fi] * loopDur) / loopDur; // 정수 사이클 스냅
                float a = padAmp[fi];
                for (int i = 0; i < totalSmp; i++)
                    data[i] += Mathf.Sin(2f * Mathf.PI * f * (i / (float)SampleRate)) * a;
            }

            // ── 레이어 2: 베이스 (비트 1·3=C2, 비트 2·4=G2, 2·3배음 포함) ──
            // 베이스음이 번갈아 → 화성적 움직임으로 훨씬 신남.
            float[] bassPattern = { 65.41f, 98.00f, 65.41f, 98.00f }; // C2, G2, C2, G2
            int totalBeats  = Measures * 4;
            int beatSmp     = Mathf.RoundToInt(SampleRate * beatSec);
            int bassDecSmp  = Mathf.RoundToInt(SampleRate * 0.22f);
            float bassAmp   = 0.45f;
            for (int b = 0; b < totalBeats; b++)
            {
                int   bStart = b * beatSmp;
                float bf     = bassPattern[b % bassPattern.Length];
                for (int i = 0; i < bassDecSmp && bStart + i < totalSmp; i++)
                {
                    float t   = (bStart + i) / (float)SampleRate;
                    float att = Mathf.Clamp01(i / (SampleRate * 0.004f));
                    float dec = Mathf.Exp(-11f * i / SampleRate);
                    // 기음 + 2배음(옥타브) + 3배음(5도) → 탁하지 않고 두꺼운 소리
                    float wave = Mathf.Sin(2f * Mathf.PI * bf * t)
                               + Mathf.Sin(4f * Mathf.PI * bf * t) * 0.45f
                               + Mathf.Sin(6f * Mathf.PI * bf * t) * 0.20f;
                    data[bStart + i] += wave * bassAmp * att * dec;
                }
            }

            // ── 레이어 3: 아르페지오 (C4→E4→G4→E4, 16분음표 순환) ─────────
            // 빠른 어택·빠른 감쇠 → 리듬 그루브 텍스처.
            float[] arpHz = { 261.63f, 329.63f, 392.00f, 329.63f }; // C4, E4, G4, E4
            float sixteenthSec = beatSec * 0.25f;
            int   sixteenthSmp = Mathf.RoundToInt(SampleRate * sixteenthSec);
            int   arpDecSmp    = Mathf.RoundToInt(SampleRate * 0.07f);
            float arpAmp       = 0.22f;
            int   totalSixteenths = totalBeats * 4;
            for (int s = 0; s < totalSixteenths; s++)
            {
                int sStart = s * sixteenthSmp;
                if (sStart >= totalSmp) break;
                float af = arpHz[s % arpHz.Length];
                int   sEnd = Mathf.Min(totalSmp, sStart + arpDecSmp);
                for (int i = 0; i < sEnd - sStart; i++)
                {
                    float t   = (sStart + i) / (float)SampleRate;
                    float att = Mathf.Clamp01(i / (SampleRate * 0.003f));
                    float dec = Mathf.Exp(-30f * i / SampleRate);
                    data[sStart + i] += Mathf.Sin(2f * Mathf.PI * af * t) * arpAmp * att * dec;
                }
            }

            // ── 레이어 4: 멜로디 (8분음표, C장조 펜타토닉+B5) ───────────────
            // C5=523.25 D5=587.33 E5=659.25 G5=784.00 A5=880.00 B5=987.77 C6=1046.50
            // 32음 × 8분음표(0.1875s @ 160BPM) = 6.0s = 루프 정확히 1회
            // 구조: 도약·상승·하강이 섞인 4구절 — 각 구절 8음
            float[] mel = {
                // 구절 A: 힘차게 상승, 옥타브 도약
                523.25f, 784.00f, 659.25f, 880.00f, 784.00f, 1046.50f, 880.00f, 784.00f,
                // 구절 B: 고음역 머물다 하강
                880.00f, 1046.50f, 880.00f, 784.00f, 659.25f, 784.00f, 659.25f, 523.25f,
                // 구절 C: 중간 도약 → 활기찬 변주
                659.25f, 880.00f, 784.00f, 987.77f, 880.00f, 784.00f, 659.25f, 784.00f,
                // 구절 D: 점차 하강, C5로 자연스럽게 루프 연결
                880.00f, 784.00f, 659.25f, 784.00f, 659.25f, 587.33f, 523.25f, 523.25f,
            };

            float eighthSec = beatSec * 0.5f;
            int   eighthSmp = Mathf.RoundToInt(SampleRate * eighthSec);
            int   noteSmp   = Mathf.RoundToInt(eighthSmp * 0.75f); // 75% 스타카토
            float melAmp    = 0.46f;
            float attSmp    = SampleRate * 0.005f;  // 5ms 날카로운 어택 → 펀치감
            float relSmp    = SampleRate * 0.09f;   // 90ms 릴리즈

            for (int n = 0; n < mel.Length; n++)
            {
                int nStart = Mathf.RoundToInt(n * eighthSec * SampleRate);
                if (nStart >= totalSmp) break;
                int   nEnd = Mathf.Min(totalSmp, nStart + noteSmp);
                float freq = mel[n];
                for (int i = 0; i < nEnd - nStart; i++)
                {
                    float t   = (nStart + i) / (float)SampleRate;
                    float att = Mathf.Clamp01(i / attSmp);
                    float rel = Mathf.Clamp01((noteSmp - i) / relSmp);
                    float env = att * rel;
                    // 기음 + 2배음 + 3배음 → 실로폰/글로켄슈필 음색
                    float wave = Mathf.Sin(2f * Mathf.PI * freq * t)
                               + Mathf.Sin(4f * Mathf.PI * freq * t) * 0.30f
                               + Mathf.Sin(6f * Mathf.PI * freq * t) * 0.12f;
                    data[nStart + i] += wave * melAmp * env;
                }
            }

            // 피크 클리핑 방지 정규화
            float peak = 0f;
            for (int i = 0; i < totalSmp; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
            if (peak > 0f)
            {
                float scale = 0.88f / peak;
                for (int i = 0; i < totalSmp; i++) data[i] *= scale;
            }

            return Build(name, data);
        }

        // Two-layer sine pad -- 이전 placeholder BGM. GameBGM으로 교체됨.
        // API는 AudioManager 외부 참조가 없어서 그대로 유지하지만 실제로는 안 쓰임.
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
