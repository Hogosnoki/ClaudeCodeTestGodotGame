using System;
using System.Collections.Generic;
using Godot;
using static DaggerCave.Synth;

namespace DaggerCave;

/// <summary>
/// Generates and plays all sound effects and music. Positional effects go through a pool of
/// AudioStreamPlayer2D; UI sounds and music are non-positional. While the player's head is
/// underwater a low-pass filter on the master bus muffles everything.
/// </summary>
public partial class SoundBank : Node
{
    private readonly Dictionary<string, AudioStreamWav> _sfx = new();
    private readonly List<AudioStreamPlayer2D> _pool2D = new();
    private readonly List<AudioStreamPlayer> _poolUi = new();
    private AudioStreamPlayer _musicA, _musicB;
    private AudioStreamWav _ambient, _boss;
    private string _currentMusic = "";
    private float _fade = 1f;
    private int _lowPassIdx = -1;
    private readonly Random _rng = new();

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        BuildSfx();
        _ambient = BuildAmbientMusic();
        _boss = BuildBossMusic();
        for (int k = 0; k < 28; k++) { var p = new AudioStreamPlayer2D { MaxDistance = 1100, Attenuation = 1.2f }; AddChild(p); _pool2D.Add(p); }
        for (int k = 0; k < 8; k++) { var p = new AudioStreamPlayer(); AddChild(p); _poolUi.Add(p); }
        _musicA = new AudioStreamPlayer { VolumeDb = -8 };
        _musicB = new AudioStreamPlayer { VolumeDb = -80 };
        AddChild(_musicA); AddChild(_musicB);

        var lp = new AudioEffectLowPassFilter { CutoffHz = 700, Resonance = 0.6f };
        AudioServer.AddBusEffect(0, lp);
        _lowPassIdx = AudioServer.GetBusEffectCount(0) - 1;
        AudioServer.SetBusEffectEnabled(0, _lowPassIdx, false);
    }

    public void SetUnderwater(bool on)
    {
        if (_lowPassIdx >= 0 && AudioServer.IsBusEffectEnabled(0, _lowPassIdx) != on)
            AudioServer.SetBusEffectEnabled(0, _lowPassIdx, on);
    }

    public void Play(string name, Vector2? pos = null, float volDb = 0f, float pitchVar = 0.08f, float pitch = 1f)
    {
        if (!_sfx.TryGetValue(name, out var st)) return;
        float ps = pitch * (1f + ((float)_rng.NextDouble() * 2 - 1) * pitchVar);
        if (pos is Vector2 p)
        {
            AudioStreamPlayer2D free = null;
            foreach (var pl in _pool2D) if (!pl.Playing) { free = pl; break; }
            free ??= _pool2D[_rng.Next(_pool2D.Count)];
            free.Stream = st; free.GlobalPosition = p; free.VolumeDb = volDb; free.PitchScale = ps;
            free.Play();
        }
        else
        {
            AudioStreamPlayer free = null;
            foreach (var pl in _poolUi) if (!pl.Playing) { free = pl; break; }
            free ??= _poolUi[0];
            free.Stream = st; free.VolumeDb = volDb; free.PitchScale = ps;
            free.Play();
        }
    }

    public void SetMusic(string which)
    {
        if (which == _currentMusic) return;
        _currentMusic = which;
        // swap roles: B becomes the new track fading in
        (_musicA, _musicB) = (_musicB, _musicA);
        _musicA.Stream = which == "boss" ? _boss : which == "ambient" ? _ambient : null;
        _musicA.VolumeDb = -40;
        if (_musicA.Stream != null) _musicA.Play();
        _fade = 0f;
    }

    public override void _Process(double delta)
    {
        if (_fade < 1f)
        {
            _fade = Math.Min(1f, _fade + (float)delta * 0.5f);
            _musicA.VolumeDb = Mathf.LinearToDb(Math.Max(0.0001f, _fade)) - 8f;
            _musicB.VolumeDb = Mathf.LinearToDb(Math.Max(0.0001f, 1 - _fade)) - 8f;
            if (_fade >= 1f) _musicB.Stop();
        }
    }

    private void Add(string name, float[] buf, float peak = 0.8f)
    {
        Normalize(buf, peak);
        _sfx[name] = ToStream(buf);
    }

    private void BuildSfx()
    {
        float[] b;

        b = Buf(0.16f); NoiseBurst(b, 0, 0.16f, t => 0.05f + 0.5f * MathF.Sin(t * MathF.PI), AD(0.25f, 5f)); Add("swing", b, 0.5f);
        b = Buf(0.22f); NoiseBurst(b, 0, 0.22f, t => 0.1f + 0.6f * MathF.Sin(t * MathF.PI), AD(0.3f, 4f)); Add("swing_heavy", b, 0.65f);

        b = Buf(0.16f);
        Osc(b, 0, 0.16f, ExpSweep(200, 55), Decay(9));
        NoiseBurst(b, 0, 0.05f, Const(0.6f), Decay(20));
        Add("hit", b, 0.85f);

        b = Buf(0.1f); Osc(b, 0, 0.1f, Const(2400), Decay(30)); Osc(b, 0, 0.1f, Const(3700), Decay(35)); NoiseBurst(b, 0, 0.02f, Const(0.9f), Decay(10)); Add("clink", b, 0.5f);

        b = Buf(0.22f); NoiseBurst(b, 0, 0.22f, t => 0.3f + 0.5f * t, AD(0.1f, 6f), true); Osc(b, 0.02f, 0.2f, Const(1800), Decay(15)); Add("throw", b, 0.6f);

        b = Buf(0.12f); Osc(b, 0, 0.12f, ExpSweep(180, 420), AD(0.05f, 8f), 1); LowPass(b, 0.3f); Add("jump", b, 0.35f);
        b = Buf(0.1f); NoiseBurst(b, 0, 0.1f, Const(0.08f), Decay(12)); Osc(b, 0, 0.08f, ExpSweep(120, 50), Decay(12)); Add("land", b, 0.4f);
        b = Buf(0.22f); NoiseBurst(b, 0, 0.22f, t => 0.05f + 0.35f * (1 - t), AD(0.15f, 4f)); Add("dodge", b, 0.55f);
        b = Buf(0.18f); NoiseBurst(b, 0, 0.18f, t => 0.2f + 0.6f * t, AD(0.2f, 5f)); Osc(b, 0, 0.18f, ExpSweep(300, 900), AD(0.1f, 6f), 3); Add("airdash", b, 0.55f);

        b = Buf(0.5f); NoiseBurst(b, 0, 0.5f, t => 0.25f * (1 - t) + 0.02f, Decay(5));
        for (int k = 0; k < 6; k++) { float st = 0.05f + k * 0.06f; float f = 500 + k * 130; Osc(b, st, 0.05f, ExpSweep(f, f * 2.2f), Decay(8)); }
        Add("splash", b, 0.6f);

        b = Buf(0.06f); Osc(b, 0, 0.06f, ExpSweep(700, 1600), AD(0.1f, 6f)); Add("bubble", b, 0.25f);

        b = Buf(0.28f); Osc(b, 0, 0.28f, ExpSweep(320, 110), AD(0.05f, 5f), 2); NoiseBurst(b, 0, 0.1f, Const(0.4f), Decay(10)); LowPass(b, 0.4f); Add("hurt", b, 0.75f);

        b = Buf(0.35f); NoiseBurst(b, 0, 0.3f, t => 0.3f * (1 - t) + 0.03f, Decay(6)); Osc(b, 0, 0.35f, ExpSweep(420, 70), Decay(6), 1); LowPass(b, 0.35f); Add("enemy_die", b, 0.6f);

        b = Buf(0.08f); Osc(b, 0, 0.08f, ExpSweep(1100, 1700), AD(0.05f, 7f)); Osc(b, 0, 0.08f, ExpSweep(2200, 3400), AD(0.05f, 9f)); Add("xp", b, 0.22f);

        b = Buf(0.9f);
        int[] arp = { 72, 76, 79, 84, 88 };
        for (int k = 0; k < arp.Length; k++) { float f = NoteHz(arp[k]); Osc(b, k * 0.08f, 0.5f, Const(f), Decay(6), 1); Osc(b, k * 0.08f, 0.6f, Const(f * 2), Decay(8)); }
        LowPass(b, 0.45f); Echo(b, 0.12f, 0.3f, 0.3f); Add("levelup", b, 0.55f);

        b = Buf(0.8f);
        NoiseBurst(b, 0, 0.15f, Const(0.15f), Decay(8));
        for (int k = 0; k < 7; k++) { float f = NoteHz(79 + k * 3); Osc(b, 0.1f + k * 0.05f, 0.35f, Const(f), Decay(9)); }
        Echo(b, 0.09f, 0.35f, 0.35f); Add("chest", b, 0.55f);

        b = Buf(0.12f); Osc(b, 0, 0.06f, ExpSweep(4200, 2800), Decay(8)); Osc(b, 0.06f, 0.05f, ExpSweep(4600, 3000), Decay(8)); Add("bat", b, 0.25f);

        b = Buf(0.3f); Osc(b, 0, 0.3f, t => 110 + 20 * MathF.Sin(t * 60), t => (MathF.Sin(t * 180) > 0 ? 1 : 0.3f) * MathF.Exp(-t * 3), 2); LowPass(b, 0.25f); Add("frog", b, 0.45f);
        b = Buf(0.18f); Osc(b, 0, 0.18f, ExpSweep(260, 110), Decay(4), 1); LowPass(b, 0.2f); Add("tongue", b, 0.3f);

        b = Buf(0.22f); Osc(b, 0, 0.22f, t => 150 + 40 * MathF.Sin(t * 20), AD(0.1f, 5f), 2); LowPass(b, 0.12f); Add("goblin", b, 0.5f);

        b = Buf(0.7f); NoiseBurst(b, 0, 0.7f, t => 0.06f * (1 - t) + 0.01f, AD(0.02f, 4f)); Osc(b, 0, 0.6f, ExpSweep(70, 30), Decay(4)); Add("slam", b, 0.9f);

        b = Buf(0.45f); NoiseBurst(b, 0, 0.45f, Const(0.5f), AD(0.1f, 4f), true);
        for (int k = 0; k < 4; k++) Osc(b, 0.05f + k * 0.09f, 0.04f, ExpSweep(300, 700), Decay(6));
        Add("lava", b, 0.35f);

        b = Buf(0.1f); NoiseBurst(b, 0, 0.1f, Const(0.12f), Decay(15)); Osc(b, 0, 0.08f, ExpSweep(300, 150), Decay(10)); Add("flop", b, 0.35f);

        b = Buf(0.35f); Osc(b, 0, 0.35f, t => 90 + 30 * MathF.Sin(t * 300), AD(0.05f, 3f), 1); NoiseBurst(b, 0, 0.35f, Const(0.7f), t => 0.3f * MathF.Exp(-t * 3), true); LowPass(b, 0.5f); Add("eel", b, 0.45f);

        b = Buf(0.2f); for (int k = 0; k < 5; k++) NoiseBurst(b, k * 0.035f, 0.015f, Const(0.9f), Decay(10), true); Add("spider", b, 0.3f);

        b = Buf(0.15f); NoiseBurst(b, 0, 0.15f, Const(0.8f), Decay(12), true); Add("spike", b, 0.35f);

        b = Buf(0.25f); NoiseBurst(b, 0, 0.25f, t => 0.2f * (1 - t) + 0.02f, Decay(9)); Osc(b, 0, 0.2f, ExpSweep(90, 40), Decay(9)); Add("rock", b, 0.5f);

        b = Buf(1.4f);
        Osc(b, 0, 1.4f, t => 70 + 8 * MathF.Sin(t * 40), AD(0.1f, 2.2f), 2);
        Osc(b, 0, 1.4f, t => 93 + 8 * MathF.Sin(t * 37), AD(0.1f, 2.2f), 2);
        NoiseBurst(b, 0, 1.4f, Const(0.08f), AD(0.1f, 2.5f));
        LowPass(b, 0.2f); Add("roar", b, 0.9f);

        b = Buf(1.2f);
        for (int k = 0; k < 10; k++) Osc(b, k * 0.07f, 0.5f, Const(NoteHz(60 + k * 2)), Decay(5), 3);
        Echo(b, 0.15f, 0.4f, 0.4f); Add("portal", b, 0.6f);

        b = Buf(0.05f); Osc(b, 0, 0.05f, Const(880), Decay(8), 1); LowPass(b, 0.3f); Add("ui", b, 0.3f);
        b = Buf(0.3f); Osc(b, 0, 0.3f, ExpSweep(500, 900), AD(0.1f, 5f)); Osc(b, 0.05f, 0.25f, ExpSweep(700, 1200), AD(0.1f, 6f)); Add("heal", b, 0.4f);
        b = Buf(0.4f); NoiseBurst(b, 0, 0.4f, t => 0.1f + 0.2f * t, AD(0.4f, 3f)); Osc(b, 0, 0.4f, ExpSweep(200, 500), AD(0.5f, 4f), 3); Add("gasp", b, 0.4f);
        b = Buf(0.9f); Osc(b, 0, 0.9f, ExpSweep(400, 60), AD(0.02f, 2f), 2); NoiseBurst(b, 0, 0.9f, Const(0.1f), Decay(3)); LowPass(b, 0.3f); Add("player_die", b, 0.8f);
        b = Buf(0.3f); Osc(b, 0, 0.3f, ExpSweep(600, 200), Decay(4), 3); NoiseBurst(b, 0, 0.3f, Const(0.3f), Decay(6)); Add("web", b, 0.35f);
    }

    /// <summary>A 32 s seamless ambient loop: filtered drone, slow pad chords, echoing pentatonic plinks and drips.</summary>
    private static AudioStreamWav BuildAmbientMusic()
    {
        const float len = 32f, tail = 4f;
        var b = Buf(len + tail);
        var rng = new Random(77);

        // Drone
        {
            int n = (int)((len + tail) * Rate);
            double p1 = 0, p2 = 0, p3 = 0; float lp = 0;
            for (int k = 0; k < n; k++)
            {
                float t = k / (float)Rate;
                p1 += 55.0 / Rate; p2 += 55.3 / Rate; p3 += 82.4 / Rate;
                float s = (float)((p1 % 1) * 2 - 1 + (p2 % 1) * 2 - 1) * 0.5f + (float)((p3 % 1) * 2 - 1) * 0.3f;
                float cut = 0.02f + 0.015f * MathF.Sin(t * MathF.Tau / len * 2);
                lp += (s - lp) * cut;
                b[k] += lp * 0.55f;
            }
        }
        // Pads (Am - F - C - G), 8 s each
        int[][] chords = { new[] { 57, 60, 64 }, new[] { 53, 57, 60 }, new[] { 48, 55, 64 }, new[] { 55, 59, 62 } };
        for (int c = 0; c < 4; c++)
            foreach (var m in chords[c])
            {
                float f = NoteHz(m);
                Osc(b, c * 8f, 9.5f, t => f * (1 + 0.003f * MathF.Sin(t * 30)), t => 0.07f * MathF.Sin(MathF.Min(1, t * 2.2f) * MathF.PI * 0.5f) * (t > 0.75f ? (1 - t) / 0.25f : 1), 3);
            }
        // Plinks with echo
        var plink = Buf(len + tail);
        int[] scale = { 69, 72, 74, 76, 79, 81, 84 };
        for (float t = 0.5f; t < len; t += 0.6f + (float)rng.NextDouble() * 1.6f)
        {
            float f = NoteHz(scale[rng.Next(scale.Length)]);
            Osc(plink, t, 1.2f, Const(f), Decay(7), 0);
            Osc(plink, t, 0.8f, Const(f * 2.01f), Decay(10), 0);
        }
        // Drips
        for (float t = 1.3f; t < len; t += 2.5f + (float)rng.NextDouble() * 4f)
            Osc(plink, t, 0.05f, ExpSweep(1900, 900), Decay(6));
        for (int k = 0; k < plink.Length; k++) plink[k] *= 0.12f;
        Echo(plink, 0.42f, 0.5f, 0.6f);
        for (int k = 0; k < b.Length; k++) b[k] += plink[k];

        return Loop(b, len);
    }

    /// <summary>A 16 s driving boss loop in D minor: kick/snare, saw bass ostinato, stabs.</summary>
    private static AudioStreamWav BuildBossMusic()
    {
        const float bpm = 140f, beat = 60f / bpm;
        const int bars = 8;
        float len = bars * 4 * beat, tail = 2f;
        var b = Buf(len + tail);
        int[] bassLine = { 38, 38, 50, 38, 41, 38, 48, 46 };
        for (int bar = 0; bar < bars; bar++)
        {
            int root = bar % 4 == 3 ? 36 : bar % 4 == 2 ? 34 : 38;
            for (int s = 0; s < 8; s++)
            {
                float t = (bar * 4 + s * 0.5f) * beat;
                float f = NoteHz(bassLine[s] - 38 + root);
                Osc(b, t, beat * 0.45f, Const(f), AD(0.02f, 4f), 2);
            }
            for (int q = 0; q < 4; q++)
            {
                float t = (bar * 4 + q) * beat;
                Osc(b, t, 0.25f, ExpSweep(150, 40), Decay(10));
                if (q % 2 == 1) NoiseBurst(b, t, 0.18f, Const(0.5f), Decay(14));
                NoiseBurst(b, t + beat * 0.5f, 0.04f, Const(0.9f), Decay(20), true);
            }
            if (bar % 2 == 0)
            {
                foreach (int m in new[] { root + 24, root + 27, root + 31 })
                    Osc(b, bar * 4 * beat, beat * 1.5f, Const(NoteHz(m)), AD(0.02f, 3f), 1);
            }
        }
        LowPass(b, 0.35f);
        return Loop(b, len);
    }

    private static AudioStreamWav Loop(float[] b, float len)
    {
        int n = (int)(len * Rate);
        var outBuf = new float[n];
        for (int k = 0; k < b.Length; k++) outBuf[k % n] += b[k];
        Normalize(outBuf, 0.7f);
        return ToStream(outBuf, true);
    }
}
