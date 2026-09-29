using System;
using Godot;

namespace DaggerCave;

/// <summary>Tiny offline synthesizer: every sound in the game is generated into PCM at startup.</summary>
public static class Synth
{
    public const int Rate = 22050;

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
            float c = Math.Clamp(cutoff(t), 0.001f, 1f);
            float x = Noise();
            lp += (x - lp) * c;
            buf[s0 + k] += (highpass ? x - lp : lp) * amp(t);
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
            float hi = Noise() - lo - damp * bp;
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
        float lp = 0;
        for (int k = 0; k < buf.Length; k++) { lp += (buf[k] - lp) * coef; buf[k] -= lp; }
    }

    public static void LowPass(float[] buf, float coef)
    {
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

    public static float NoteHz(int midi) => 440f * MathF.Pow(2f, (midi - 69) / 12f);
}
