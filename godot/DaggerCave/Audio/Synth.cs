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
