using System;
using System.Linq;
using Godot;

namespace DaggerCave;

/// <summary>Tiny offline synthesizer: every sound in the game is generated into PCM at startup.</summary>
public static class Synth
{
    /// <summary>
    /// The sample rate sounds are made at: 44.1 kHz for the effects (the music is made at 22.05 kHz, which is plenty for it and twice as
    /// quick at start). Filter settings and noise levels are given as they were at 22.05 kHz and converted, so a sound is the same sound
    /// at either rate, only cleaner.
    /// </summary>
    public static int Rate { get; set; } = 44100;
    private const float BaseRate = 22050f;

    /// <summary>A one-pole coefficient given at 22.05 kHz, at the current rate (the same cutoff in Hz).</summary>
    public static float K(float c) => 1f - MathF.Pow(1f - Math.Clamp(c, 0f, 0.9999f), BaseRate / Rate);
    /// <summary>What noise is scaled by so a band of it is as loud at the current rate as at 22.05 kHz.</summary>
    public static float NoiseGain => MathF.Sqrt(Rate / BaseRate);

    public static float[] Buf(float seconds) => new float[(int)(seconds * Rate)];

    private static readonly Random Rng = new(1234);
    public static float Noise() => (float)Rng.NextDouble() * 2f - 1f;

    /// <summary>Adds an oscillator whose frequency and amplitude follow the given envelopes (t = 0..1 over the note).</summary>
    public static void Osc(float[] buf, float start, float dur, Func<float, float> freq, Func<float, float> amp, int wave = 0, float offsetPhase = 0)
    {
        int s0 = (int)(start * Rate), n = (int)(dur * Rate);
        double phase = offsetPhase;
        for (int k = 0; k < n && s0 + k < buf.Length; k++)
        {
            if (s0 + k < 0) continue;
            float t = k / (float)n;
            phase += freq(t) / Rate;
            float p = (float)(phase - Math.Floor(phase));
            float v = wave switch
            {
                0 => MathF.Sin(p * MathF.Tau),
                1 => p < 0.5f ? 1f : -1f,                 // square
                2 => 2f * p - 1f,                          // saw
                3 => 1f - 4f * MathF.Abs(p - 0.5f),        // triangle
                _ => Noise(),
            };
            buf[s0 + k] += v * amp(t);
        }
    }

    /// <summary>Adds low-passed noise with a moving cutoff (0..1 of the one-pole coefficient range).</summary>
    public static void NoiseBurst(float[] buf, float start, float dur, Func<float, float> cutoff, Func<float, float> amp, bool highpass = false)
    {
        int s0 = (int)(start * Rate), n = (int)(dur * Rate);
        float lp = 0;
        for (int k = 0; k < n && s0 + k < buf.Length; k++)
        {
            float t = k / (float)n;
            float c = K(Math.Clamp(cutoff(t), 0.001f, 1f));
            float x = Noise();
            lp += (x - lp) * c;
            buf[s0 + k] += (highpass ? x - lp : lp * NoiseGain) * amp(t);
        }
    }

    public static Func<float, float> Decay(float k) => t => MathF.Exp(-t * k);
    public static Func<float, float> AD(float attack, float k) => t => t < attack ? t / attack : MathF.Exp(-(t - attack) * k);
    public static Func<float, float> Const(float v) => _ => v;
    public static Func<float, float> Sweep(float a, float b) => t => a + (b - a) * t;
    public static Func<float, float> ExpSweep(float a, float b) => t => a * MathF.Pow(b / a, t);


    /// <summary>Noise through a resonant band-pass (two-pole), the centre frequency following an envelope: whooshes, splashes, scrapes.</summary>
    public static void BandNoise(float[] buf, float start, float dur, Func<float, float> hz, Func<float, float> q, Func<float, float> amp)
    {
        int s0 = (int)(start * Rate), n = (int)(dur * Rate);
        float lo = 0, bp = 0;
        for (int k = 0; k < n && s0 + k < buf.Length; k++)
        {
            float t = k / (float)n;
            float f = 2f * MathF.Sin(MathF.PI * Math.Clamp(hz(t), 20f, Rate * 0.18f) / Rate);
            float damp = 1f / Math.Max(0.5f, q(t));
            lo += f * bp;
            float hi = Noise() * NoiseGain - lo - damp * bp;
            bp += f * hi;
            float a = amp(t);
            if (float.IsNaN(a)) a = 0f; // (an envelope's pow of a hair below zero at its end)
            if (s0 + k >= 0) buf[s0 + k] += bp * a * MathF.Sqrt(damp);
        }
    }

    /// <summary>A struck object: a bank of decaying inharmonic partials (ratio, gain, decay-per-second) on a fundamental.</summary>
    public static void Modes(float[] buf, float start, float fund, float[][] modes, float decayScale = 1f, float gain = 1f)
    {
        foreach (var m in modes)
        {
            float f = fund * m[0], g = gain * m[1], d = m[2] / decayScale;
            if (f > Rate * 0.45f) continue;
            int s0 = (int)(start * Rate), n = (int)(Math.Min(6f / d, 1.5f) * Rate);
            double ph = 0;
            for (int k = 0; k < n && s0 + k < buf.Length; k++)
            {
                ph += f / Rate;
                buf[s0 + k] += MathF.Sin((float)(ph * Math.PI * 2)) * g * MathF.Exp(-k / (float)Rate * d);
            }
        }
    }

    public static readonly float[][] StoneModes = { new[] { 1f, 1f, 60f }, new[] { 1.53f, 0.7f, 75f }, new[] { 2.41f, 0.5f, 95f }, new[] { 3.87f, 0.35f, 130f } };
    public static readonly float[][] WoodModes = { new[] { 1f, 1f, 38f }, new[] { 2.1f, 0.6f, 52f }, new[] { 3.4f, 0.4f, 75f }, new[] { 5.2f, 0.2f, 110f } };
    public static readonly float[][] MetalModes = { new[] { 1f, 1f, 22f }, new[] { 2.76f, 0.7f, 30f }, new[] { 5.4f, 0.5f, 45f }, new[] { 8.93f, 0.3f, 70f }, new[] { 13.3f, 0.2f, 110f } };
    public static readonly float[][] GlassModes = { new[] { 1f, 1f, 30f }, new[] { 2.32f, 0.6f, 40f }, new[] { 4.25f, 0.5f, 55f }, new[] { 6.63f, 0.3f, 80f } };

    /// <summary>Wood under strain: a run of tiny stick-slip pulses, each a resonant click, the rate and pitch wandering. (a creak)</summary>
    public static void Creak(float[] buf, float start, float dur, float pitch, float rate, float amp, int seed)
    {
        var r = new Random(seed);
        float t = 0;
        float f = pitch;
        while (t < dur)
        {
            f = Math.Clamp(f * (0.9f + 0.2f * (float)r.NextDouble()), pitch * 0.6f, pitch * 1.7f);
            float u = t / dur;
            float env = MathF.Sin(MathF.PI * u);
            int s0 = (int)((start + t) * Rate), n = (int)(0.012f * Rate);
            double ph = 0;
            for (int k = 0; k < n && s0 + k < buf.Length; k++)
            {
                ph += f / Rate;
                float sq = (float)(ph % 1) * 2f - 1f;
                buf[s0 + k] += (sq * 0.6f + Noise() * 0.4f) * MathF.Exp(-k / (float)Rate * 320f) * env * amp;
            }
            t += 1f / (rate * (0.6f + 0.8f * (float)r.NextDouble()));
        }
    }

    public static void HighPass(float[] buf, float coef)
    {
        coef = K(coef);
        float lp = 0;
        for (int k = 0; k < buf.Length; k++) { lp += (buf[k] - lp) * coef; buf[k] -= lp; }
    }

    public static void LowPass(float[] buf, float coef)
    {
        coef = K(coef);
        float lp = 0;
        for (int k = 0; k < buf.Length; k++) { lp += (buf[k] - lp) * coef; buf[k] = lp; }
    }

    public static void Echo(float[] buf, float delaySec, float feedback, float mix)
    {
        int d = (int)(delaySec * Rate);
        var wet = new float[buf.Length];
        for (int k = 0; k < buf.Length; k++)
        {
            float prev = k >= d ? wet[k - d] : 0f;
            wet[k] = buf[k] + prev * feedback;
        }
        for (int k = 0; k < buf.Length; k++) buf[k] = buf[k] * (1 - mix) + wet[k] * mix;
    }

    public static void Normalize(float[] buf, float peak = 0.9f)
    {
        float m = 1e-6f;
        foreach (var v in buf) m = Math.Max(m, Math.Abs(v));
        float g = peak / m;
        for (int k = 0; k < buf.Length; k++) buf[k] *= g;
    }

    public static AudioStreamWav ToStream(float[] buf, bool loop = false)
    {
        var bytes = new byte[buf.Length * 2];
        for (int k = 0; k < buf.Length; k++)
        {
            short s = (short)Math.Clamp((int)(buf[k] * 32767f), -32768, 32767);
            bytes[2 * k] = (byte)(s & 0xFF);
            bytes[2 * k + 1] = (byte)((s >> 8) & 0xFF);
        }
        var st = new AudioStreamWav
        {
            Format = AudioStreamWav.FormatEnum.Format16Bits,
            MixRate = Rate,
            Stereo = false,
            Data = bytes,
        };
        if (loop)
        {
            st.LoopMode = AudioStreamWav.LoopModeEnum.Forward;
            st.LoopBegin = 0;
            st.LoopEnd = buf.Length;
        }
        return st;
    }

    /// <summary>
    /// A voice: a glottal pulse train (a soft saw with a little jitter and breath in it) at <paramref name="f0"/>, shaped by formant
    /// resonances (centre Hz and bandwidth Hz each) as a throat and mouth shape it. For grunts, gasps and growls.
    /// </summary>
    public static void Voice(float[] buf, float start, float dur, Func<float, float> f0, float[] formants, float[] widths, Func<float, float> amp, float breath = 0.25f, int seed = 3)
    {
        var r = new Random(seed);
        int s0 = (int)(start * Rate), n = (int)(dur * Rate);
        var src = new float[n];
        double ph = 0; float jit = 0;
        for (int k = 0; k < n; k++)
        {
            float t = k / (float)n;
            if (k % 64 == 0) jit = ((float)r.NextDouble() * 2f - 1f) * 0.02f;
            ph += f0(t) * (1f + jit) / Rate;
            float p = (float)(ph - Math.Floor(ph));
            // (a glottal pulse: quick close, slow open; then breath noise riding on it)
            float g = p < 0.6f ? MathF.Sin(p / 0.6f * MathF.PI * 0.5f) : MathF.Cos((p - 0.6f) / 0.4f * MathF.PI * 0.5f);
            src[k] = (g * 2f - 1f) * (1f - breath) + ((float)r.NextDouble() * 2f - 1f) * breath;
        }
        var outb = new float[n];
        for (int f = 0; f < formants.Length; f++)
        {
            float fc = 2f * MathF.Sin(MathF.PI * Math.Min(formants[f], Rate * 0.2f) / Rate);
            float q = formants[f] / Math.Max(30f, widths[f]);
            float damp = 1f / Math.Max(0.5f, q);
            float lo = 0, bp = 0;
            float gain = 1f / (f + 1);
            for (int k = 0; k < n; k++)
            {
                lo += fc * bp;
                float hi = src[k] - lo - damp * bp;
                bp += fc * hi;
                outb[k] += bp * gain;
            }
        }
        for (int k = 0; k < n && s0 + k < buf.Length; k++)
        {
            if (s0 + k < 0) continue;
            float a = amp(k / (float)n);
            buf[s0 + k] += outb[k] * a * 0.6f;
        }
    }

    /// <summary>Grit underfoot: a scatter of tiny bright grains over <paramref name="dur"/>, thinning out.</summary>
    public static void Grains(float[] buf, float start, float dur, int count, float hz, float amp, int seed)
    {
        var r = new Random(seed);
        for (int g = 0; g < count; g++)
        {
            float t = start + dur * MathF.Pow((float)r.NextDouble(), 1.6f);
            float a = amp * (0.4f + 0.6f * (float)r.NextDouble()) * (1f - (t - start) / (dur * 1.3f));
            float f = hz * (0.7f + 0.6f * (float)r.NextDouble());
            BandNoise(buf, t, 0.006f + 0.008f * (float)r.NextDouble(), Const(f), Const(2.5f), tt => a * MathF.Exp(-tt * 6f));
        }
    }

    // ---------------------------------------------------------------- finishing: what makes a made sound sit in a real place

    /// <summary>A one-pole low-pass at <paramref name="hz"/> (true Hz, at the current rate).</summary>
    public static void LowPassHz(float[] buf, float hz)
    {
        float c = 1f - MathF.Exp(-MathF.Tau * hz / Rate), lp = 0;
        for (int k = 0; k < buf.Length; k++) { lp += (buf[k] - lp) * c; buf[k] = lp; }
    }

    /// <summary>A one-pole high-pass at <paramref name="hz"/> (true Hz): takes out rumble and any offset.</summary>
    public static void HighPassHz(float[] buf, float hz)
    {
        float c = 1f - MathF.Exp(-MathF.Tau * hz / Rate), lp = 0;
        for (int k = 0; k < buf.Length; k++) { lp += (buf[k] - lp) * c; buf[k] -= lp; }
    }

    /// <summary>
    /// A small stone room round a sound: a few early reflections, then a short diffuse tail (four damped combs into two all-passes,
    /// after Schroeder), mixed in at <paramref name="wet"/>. <paramref name="seed"/> nudges the delays, so two takes of a sound don't
    /// ring alike. Returns the sound with room for its tail.
    /// </summary>
    public static float[] Room(float[] dry, float wet, float tail, int seed)
    {
        var r = new Random(seed);
        int n = dry.Length + (int)(tail * Rate);
        var o = new float[n];
        Array.Copy(dry, o, dry.Length);
        if (wet <= 0f) return o;
        var w = new float[n];
        // early reflections: off the near walls
        float[] taps = { 0.0071f, 0.0119f, 0.0173f, 0.0262f, 0.0331f };
        float[] gains = { 0.42f, 0.33f, 0.27f, 0.2f, 0.14f };
        for (int t = 0; t < taps.Length; t++)
        {
            int d = (int)((taps[t] * (0.92f + 0.16f * (float)r.NextDouble())) * Rate);
            for (int k = 0; k < dry.Length && k + d < n; k++) w[k + d] += dry[k] * gains[t];
        }
        // the tail: four combs with damping in their loops
        float[] combs = { 0.0297f, 0.0371f, 0.0411f, 0.0437f };
        var tailBuf = new float[n];
        float fb = MathF.Pow(0.001f, combs.Average() / MathF.Max(0.05f, tail * 0.8f));
        foreach (var cs in combs)
        {
            int d = Math.Max(1, (int)(cs * (0.95f + 0.1f * (float)r.NextDouble()) * Rate));
            var line = new float[d];
            int idx = 0; float damp = 0;
            for (int k = 0; k < n; k++)
            {
                float x = k < dry.Length ? dry[k] : 0f;
                float y = line[idx];
                damp += (y - damp) * 0.45f;
                line[idx] = x + damp * fb;
                idx = (idx + 1) % d;
                tailBuf[k] += y * 0.25f;
            }
        }
        foreach (var aps in new[] { 0.005f, 0.0017f })
        {
            int d = Math.Max(1, (int)(aps * Rate));
            var line = new float[d];
            int idx = 0;
            for (int k = 0; k < n; k++)
            {
                float x = tailBuf[k];
                float y = line[idx];
                float v = x + y * 0.6f;
                line[idx] = v;
                tailBuf[k] = y - v * 0.6f;
                idx = (idx + 1) % d;
            }
        }
        for (int k = 0; k < n; k++) w[k] += tailBuf[k] * 0.7f;
        // (the room swallows the highest air: stone is not glass)
        LowPassHz(w, 5200f);
        for (int k = 0; k < n; k++) o[k] += w[k] * wet;
        return o;
    }

    /// <summary>
    /// The finishing pass every effect gets: rumble and offset out, a soft 1.5 ms fade-in and a fade at the very end (no clicks), the
    /// peaks rounded with a gentle saturation (warmth rather than a hard edge), the room, and a spectral tilt (a take's own colour).
    /// </summary>
    public static float[] Finish(float[] buf, float room, int seed, float tilt = 0f)
    {
        var b = (float[])buf.Clone();
        HighPassHz(b, 28f);
        int fin = Math.Min(b.Length, (int)(0.0015f * Rate));
        for (int k = 0; k < fin; k++) b[k] *= k / (float)fin;
        Normalize(b, 1f);
        const float drive = 1.6f;
        float norm = MathF.Tanh(drive);
        for (int k = 0; k < b.Length; k++) b[k] = MathF.Tanh(b[k] * drive) / norm;
        if (tilt != 0f)
        {
            // a take a shade darker (tilt < 0) or brighter (tilt > 0)
            var lo = (float[])b.Clone();
            LowPassHz(lo, 2500f);
            for (int k = 0; k < b.Length; k++) b[k] = tilt < 0 ? b[k] + (lo[k] - b[k]) * -tilt : b[k] + (b[k] - lo[k]) * tilt;
        }
        b = Room(b, room, room > 0 ? 0.35f : 0.02f, seed);
        int fout = Math.Min(b.Length, (int)(0.012f * Rate));
        for (int k = 0; k < fout; k++) b[b.Length - 1 - k] *= k / (float)fout;
        return b;
    }

    public static float NoteHz(int midi) => 440f * MathF.Pow(2f, (midi - 69) / 12f);
}
