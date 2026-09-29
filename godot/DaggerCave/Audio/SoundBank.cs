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
    private AudioStreamWav _ambient, _boss, _camp;
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
        _camp = BuildCampMusic();
        GameSettings.ApplySound(); // (makes the Music and SFX buses)
        for (int k = 0; k < 28; k++) { var p = new AudioStreamPlayer2D { MaxDistance = 1100, Attenuation = 1.2f, Bus = "SFX" }; AddChild(p); _pool2D.Add(p); }
        for (int k = 0; k < 8; k++) { var p = new AudioStreamPlayer { Bus = "SFX" }; AddChild(p); _poolUi.Add(p); }
        _musicA = new AudioStreamPlayer { VolumeDb = -8, Bus = "Music" };
        _musicB = new AudioStreamPlayer { VolumeDb = -80, Bus = "Music" };
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
        // online: a sound out in the world, from something the other games need to hear, goes to them too
        if (pos is Vector2 at && NetSync.Recording) NetSync.FxBegin(FxLayer.SoundOp).Str(name).Vec(at).Half(volDb).Half(pitchVar).Half(pitch);
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

    /// <summary>A sound another game sent.</summary>
    public void PlayNet(NetIn r)
    {
        string name = r.Str();
        var at = r.Vec();
        float vol = r.Half(), var = r.Half(), pitch = r.Half();
        Play(name, at, vol, var, pitch);
    }

    /// <summary>--musicdump=DIR: the music tracks as .wav files, for a listen.</summary>
    public void DumpMusic(string dir)
    {
        _ambient.SaveToWav($"{dir}/ambient.wav");
        _boss.SaveToWav($"{dir}/boss.wav");
        _camp.SaveToWav($"{dir}/camp.wav");
    }

    /// <summary>--sfxdump=DIR: every sound effect as a .wav file, for a listen.</summary>
    public void DumpSfx(string dir)
    {
        foreach (var kv in _sfx) kv.Value.SaveToWav($"{dir}/{kv.Key}.wav");
    }

    public void SetMusic(string which)
    {
        if (which == _currentMusic) return;
        _currentMusic = which;
        // swap roles: B becomes the new track fading in
        (_musicA, _musicB) = (_musicB, _musicA);
        _musicA.Stream = which switch { "boss" => _boss, "ambient" => _ambient, "camp" => _camp, _ => null };
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

        // sword through air: a band of wind that climbs into the pass and falls away, with a thin edge-ring at the top of it
        b = Buf(0.2f);
        BandNoise(b, 0, 0.2f, t => 700f + 2600f * MathF.Sin(t * MathF.PI * 0.85f), Const(2.2f), t => MathF.Pow(MathF.Sin(MathF.Min(1f, t * 1.15f) * MathF.PI), 1.4f));
        BandNoise(b, 0, 0.2f, t => 3800f + 1500f * t, Const(9f), t => 0.35f * MathF.Pow(MathF.Sin(t * MathF.PI), 2f));
        Modes(b, 0.07f, 2500f, MetalModes, 2.5f, 0.06f);
        Add("swing", b, 0.5f);
        b = Buf(0.3f);
        BandNoise(b, 0, 0.3f, t => 260f + 1500f * MathF.Sin(t * MathF.PI * 0.8f), Const(1.6f), t => MathF.Pow(MathF.Sin(MathF.Min(1f, t * 1.2f) * MathF.PI), 1.2f));
        BandNoise(b, 0.04f, 0.2f, t => 2400f + 800f * t, Const(7f), t => 0.3f * MathF.Sin(t * MathF.PI));
        Osc(b, 0, 0.3f, ExpSweep(110, 55), t => 0.25f * MathF.Sin(t * MathF.PI));
        Add("swing_heavy", b, 0.65f);

        // flesh and hide: a dull thump with a wet slap
        b = Buf(0.18f);
        Osc(b, 0, 0.16f, ExpSweep(170, 58), Decay(11));
        BandNoise(b, 0, 0.09f, t => 1400f - 700f * t, Const(1.4f), Decay(16));
        NoiseBurst(b, 0, 0.03f, Const(0.5f), Decay(30));
        Add("hit", b, 0.85f);
        // stone, earth and armour: a hard "chink" with a scatter of grit
        b = Buf(0.25f);
        NoiseBurst(b, 0, 0.012f, Const(1f), Decay(30), true);
        Modes(b, 0, 1750f, StoneModes, 1.4f, 0.55f);
        Modes(b, 0.004f, 620f, StoneModes, 1.2f, 0.25f);
        for (int k = 0; k < 4; k++) NoiseBurst(b, 0.03f + k * 0.022f + 0.01f * Noise(), 0.008f, Const(0.9f), Decay(12), true);
        Add("hit_stone", b, 0.85f);
        // wood, roots and leaf: a crackling thud
        b = Buf(0.3f);
        Modes(b, 0, 150f, WoodModes, 1f, 0.7f);
        Osc(b, 0, 0.12f, ExpSweep(130, 55), Decay(14));
        for (int k = 0; k < 7; k++) NoiseBurst(b, 0.005f + k * 0.017f + 0.012f * MathF.Abs(Noise()), 0.007f + 0.006f * MathF.Abs(Noise()), Const(0.85f), Decay(10), true);
        BandNoise(b, 0, 0.15f, Const(2200f), Const(1.2f), Decay(14));
        Add("hit_wood", b, 0.85f);
        // frost and crystal: a brittle crack with a few splinters
        b = Buf(0.3f);
        NoiseBurst(b, 0, 0.02f, Const(1f), Decay(25), true);
        BandNoise(b, 0, 0.1f, t => 5200f - 2500f * t, Const(2f), Decay(18));
        Modes(b, 0.002f, 2900f, GlassModes, 1.8f, 0.3f);
        for (int k = 0; k < 5; k++) Modes(b, 0.03f + k * 0.03f + 0.015f * MathF.Abs(Noise()), 3400f + 900f * Noise(), GlassModes, 3f, 0.09f);
        Osc(b, 0, 0.08f, ExpSweep(200, 80), Decay(20));
        Add("hit_ice", b, 0.8f);
        // fire: a soft thump, a hiss and sparks
        b = Buf(0.35f);
        Osc(b, 0, 0.12f, ExpSweep(140, 60), Decay(14));
        BandNoise(b, 0, 0.3f, t => 4000f - 1800f * t, Const(0.9f), t => 0.7f * MathF.Exp(-t * 6f));
        for (int k = 0; k < 6; k++) NoiseBurst(b, 0.02f + k * 0.04f + 0.02f * MathF.Abs(Noise()), 0.006f, Const(0.9f), Decay(10), true);
        Add("hit_fire", b, 0.75f);
        // water: a wet slap and a burst of droplets
        b = Buf(0.3f);
        BandNoise(b, 0, 0.12f, t => 900f - 500f * t, Const(1.3f), Decay(14));
        Osc(b, 0, 0.1f, ExpSweep(240, 110), Decay(16));
        for (int k = 0; k < 5; k++) { float f = 600f + 700f * MathF.Abs(Noise()); Osc(b, 0.04f + k * 0.03f, 0.04f, ExpSweep(f, f * 1.8f), Decay(9)); }
        LowPass(b, 0.5f);
        Add("hit_water", b, 0.7f);

        // steel on steel or a shield taking a blow: a bright strike over an inharmonic ring
        b = Buf(0.35f);
        NoiseBurst(b, 0, 0.015f, Const(1f), Decay(25), true);
        Modes(b, 0, 1150f, MetalModes, 1.7f, 0.5f);
        BandNoise(b, 0, 0.06f, Const(3500f), Const(1.5f), Decay(30));
        Add("clink", b, 0.6f);

        b = Buf(0.22f); BandNoise(b, 0, 0.22f, t => 1500f + 3500f * t, Const(2.5f), t => MathF.Pow(MathF.Sin(MathF.Min(1f, t * 1.3f) * MathF.PI), 1.5f)); Add("throw", b, 0.55f);

        // a push off the ground: cloth, a scuff of dirt and a short low thump (no pitch to it)
        b = Buf(0.13f);
        BandNoise(b, 0, 0.11f, t => 500f + 900f * t, Const(1.2f), t => MathF.Sin(MathF.Min(1f, t * 1.5f) * MathF.PI) * 0.7f);
        Osc(b, 0, 0.06f, ExpSweep(110, 60), Decay(20));
        Add("jump", b, 0.4f);
        b = Buf(0.16f);
        Osc(b, 0, 0.1f, ExpSweep(100, 45), Decay(20));
        BandNoise(b, 0, 0.12f, t => 700f - 300f * t, Const(1f), Decay(20));
        NoiseBurst(b, 0.01f, 0.06f, Const(0.5f), Decay(30), true);
        Add("land", b, 0.45f);
        b = Buf(0.24f); BandNoise(b, 0, 0.24f, t => 1600f * (1f - 0.7f * t) + 300f, Const(1.3f), AD(0.2f, 5f)); Add("dodge", b, 0.55f);
        b = Buf(0.2f); BandNoise(b, 0, 0.2f, t => 500f + 3500f * t, Const(1.8f), AD(0.35f, 5f)); Add("airdash", b, 0.55f);

        b = Buf(0.5f);
        BandNoise(b, 0, 0.5f, t => 2800f - 2200f * t, Const(0.9f), t => MathF.Exp(-t * 5f));
        for (int k = 0; k < 8; k++) { float st = 0.04f + k * 0.045f; float f = 500 + 900 * MathF.Abs(Noise()); Osc(b, st, 0.04f, ExpSweep(f, f * 1.9f), Decay(9)); }
        LowPass(b, 0.55f);
        Add("splash", b, 0.6f);
        // the hero plunging in: an open crash of spray, the body's thud going under, then the gulp and rush of bubbles
        b = Buf(0.9f);
        NoiseBurst(b, 0, 0.02f, Const(1f), Decay(30));
        BandNoise(b, 0, 0.8f, t => 3200f - 2600f * MathF.Sqrt(t), Const(0.8f), t => 0.9f * MathF.Exp(-t * 4.2f));
        Osc(b, 0.01f, 0.3f, ExpSweep(150, 45), t => 0.9f * MathF.Exp(-t * 6f));
        BandNoise(b, 0.05f, 0.45f, t => 250f + 200f * MathF.Sin(t * 20f), Const(1.5f), t => 0.7f * MathF.Sin(t * MathF.PI));
        for (int k = 0; k < 14; k++) { float st = 0.1f + k * 0.045f + 0.02f * MathF.Abs(Noise()); float f = 350 + 1200 * MathF.Abs(Noise()); float fade = 1f - st / 1.2f; Osc(b, st, 0.05f, ExpSweep(f, f * 2f), t => 0.35f * MathF.Exp(-t * 7f) * fade); }
        LowPass(b, 0.6f);
        Add("splash_in", b, 0.85f);
        // climbing out: milder, water sheeting off and a few drips
        b = Buf(0.5f);
        BandNoise(b, 0, 0.35f, t => 1200f + 900f * t, Const(1.1f), t => 0.8f * MathF.Sin(MathF.Min(1f, t * 2f) * MathF.PI * 0.5f) * MathF.Exp(-t * 4f));
        Osc(b, 0.02f, 0.2f, ExpSweep(120, 70), t => 0.35f * MathF.Exp(-t * 8f));
        for (int k = 0; k < 6; k++) { float st = 0.12f + k * 0.055f + 0.03f * MathF.Abs(Noise()); float f = 700 + 800 * MathF.Abs(Noise()); Osc(b, st, 0.035f, ExpSweep(f * 1.6f, f), t => 0.3f * MathF.Exp(-t * 8f)); }
        LowPass(b, 0.5f);
        Add("splash_out", b, 0.5f);

        b = Buf(0.06f); Osc(b, 0, 0.06f, ExpSweep(700, 1600), AD(0.1f, 6f)); Add("bubble", b, 0.25f);

        // the hero struck: a body blow, the smack of it on leather and a short breathy grunt
        b = Buf(0.3f);
        Osc(b, 0, 0.2f, ExpSweep(150, 48), Decay(11));
        BandNoise(b, 0, 0.08f, Const(1800f), Const(1.2f), Decay(22));
        BandNoise(b, 0.03f, 0.2f, t => 520f - 150f * t, Const(4f), t => MathF.Sin(MathF.Min(1f, t * 1.4f) * MathF.PI) * 0.8f);
        Osc(b, 0.03f, 0.2f, ExpSweep(190, 120), t => 0.35f * MathF.Sin(t * MathF.PI), 2);
        LowPass(b, 0.4f);
        Add("hurt", b, 0.75f);

        b = Buf(0.4f);
        Osc(b, 0, 0.3f, ExpSweep(160, 40), Decay(8));
        BandNoise(b, 0, 0.38f, t => 1500f - 1100f * t, Const(1f), Decay(6));
        for (int k = 0; k < 5; k++) NoiseBurst(b, 0.05f + k * 0.05f, 0.03f, Const(0.4f), Decay(10));
        LowPass(b, 0.5f);
        Add("enemy_die", b, 0.6f);

        // a small bright gem-tick: a short strike, no sustained tone
        b = Buf(0.1f);
        NoiseBurst(b, 0, 0.008f, Const(1f), Decay(25), true);
        Modes(b, 0, 2600f, GlassModes, 3.2f, 0.4f);
        BandNoise(b, 0, 0.04f, Const(5000f), Const(3f), Decay(30));
        Add("xp", b, 0.28f);

        // a swell of power: sub boom, wind gathering upward, a brass-like chord blooming open, and a rolling impact with a fall of sparks
        b = Buf(1.5f);
        Osc(b, 0.42f, 0.9f, ExpSweep(85, 38), t => 1.0f * MathF.Exp(-t * 3.5f));
        NoiseBurst(b, 0.42f, 0.9f, t => 0.05f + 0.1f * (1 - t), t => 0.8f * MathF.Exp(-t * 3.5f));
        BandNoise(b, 0, 0.5f, t => 300f + 3600f * t * t, Const(1.6f), t => 0.9f * MathF.Pow(t, 1.6f));
        var chord = Buf(1.5f);
        foreach (int m in new[] { 45, 52, 57, 61 })
        {
            float f = NoteHz(m);
            Osc(chord, 0.1f, 1.2f, t => f * (1f + 0.004f * MathF.Sin(t * 40f)), t => 0.25f * MathF.Pow(MathF.Min(1f, t * 3.5f), 2f) * MathF.Exp(-MathF.Max(0f, t - 0.35f) * 3f), 2);
            Osc(chord, 0.1f, 1.2f, Const(f * 1.006f), t => 0.2f * MathF.Pow(MathF.Min(1f, t * 3.5f), 2f) * MathF.Exp(-MathF.Max(0f, t - 0.35f) * 3f), 2);
        }
        LowPass(chord, 0.14f);
        for (int k = 0; k < b.Length; k++) b[k] += chord[k];
        for (int k = 0; k < 16; k++) { float fade = 1f - k / 18f; NoiseBurst(b, 0.45f + k * 0.045f + 0.02f * MathF.Abs(Noise()), 0.01f, Const(0.9f), t => 0.3f * MathF.Exp(-t * 6f) * fade, true); }
        Echo(b, 0.11f, 0.3f, 0.25f);
        Add("levelup", b, 0.7f);

        // an old wooden chest: the hinge creaking open, the lid knocking back against the wood, dust settling
        b = Buf(0.9f);
        Creak(b, 0.0f, 0.42f, 300f, 55f, 0.55f, 5);
        Creak(b, 0.05f, 0.36f, 520f, 40f, 0.3f, 9);
        BandNoise(b, 0, 0.4f, t => 900f + 500f * t, Const(3f), t => 0.12f * MathF.Sin(t * MathF.PI));
        Modes(b, 0.46f, 130f, WoodModes, 1f, 0.9f);
        Osc(b, 0.46f, 0.15f, ExpSweep(110, 50), Decay(14));
        NoiseBurst(b, 0.46f, 0.02f, Const(0.5f), Decay(30));
        Modes(b, 0.53f, 190f, WoodModes, 1.3f, 0.3f);
        for (int k = 0; k < 4; k++) NoiseBurst(b, 0.6f + k * 0.05f, 0.01f, Const(0.9f), Decay(12), true);
        LowPass(b, 0.6f);
        Add("chest", b, 0.7f);

        b = Buf(0.12f); Osc(b, 0, 0.06f, ExpSweep(4200, 2800), Decay(8)); Osc(b, 0.06f, 0.05f, ExpSweep(4600, 3000), Decay(8)); Add("bat", b, 0.25f);

        // an insect's buzz: a rough hum near 190 Hz (and its octave) fluttering with the wing beats,
        // wavering a little, with a breath of wing noise
        {
            const float len = 0.55f;
            b = Buf(len);
            Func<float, float> env = AD(0.12f, 3f);
            float Beat(float t) => 0.6f + 0.4f * MathF.Sin(t * len * MathF.Tau * 38f);
            Osc(b, 0, len, t => 190 + 14 * MathF.Sin(t * len * MathF.Tau * 9f) + 25 * t, t => Beat(t) * env(t), 2);
            Osc(b, 0, len, t => 381 + 26 * MathF.Sin(t * len * MathF.Tau * 9f) + 50 * t, t => 0.35f * Beat(t) * env(t), 1);
            NoiseBurst(b, 0, len, Const(0.35f), t => 0.18f * Beat(t) * env(t));
            LowPass(b, 0.35f);
            Add("buzz", b, 0.45f);
        }

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

        // a rift: a rising rush of air over a wavering low drone, then it closes with a soft thud
        b = Buf(1.3f);
        BandNoise(b, 0, 1.2f, t => 250f + 2200f * MathF.Sin(t * MathF.PI * 0.9f), Const(2f), t => 0.9f * MathF.Sin(t * MathF.PI));
        Osc(b, 0, 1.2f, t => 70f + 25f * MathF.Sin(t * 22f) + 40f * t, t => 0.6f * MathF.Sin(t * MathF.PI), 2);
        Osc(b, 1.0f, 0.25f, ExpSweep(90, 40), Decay(12));
        LowPass(b, 0.3f);
        Add("portal", b, 0.6f);

        b = Buf(0.06f); Modes(b, 0, 760f, WoodModes, 1.5f, 0.6f); NoiseBurst(b, 0, 0.006f, Const(0.5f), Decay(30), true); Add("ui", b, 0.35f);
        // a soft rush of restoring air with a few motes of light, nothing sung
        b = Buf(0.4f);
        BandNoise(b, 0, 0.4f, t => 900f + 2600f * t, Const(2.5f), t => 0.7f * MathF.Sin(MathF.Min(1f, t * 1.2f) * MathF.PI));
        Osc(b, 0, 0.4f, ExpSweep(170, 260), t => 0.25f * MathF.Sin(t * MathF.PI), 3);
        for (int k = 0; k < 6; k++) { float f = 2400f + 1800f * MathF.Abs(Noise()); Modes(b, 0.05f + k * 0.045f, f, GlassModes, 5f, 0.05f); }
        LowPass(b, 0.55f);
        Add("heal", b, 0.4f);
        b = Buf(0.4f); NoiseBurst(b, 0, 0.4f, t => 0.1f + 0.2f * t, AD(0.4f, 3f)); Osc(b, 0, 0.4f, ExpSweep(200, 500), AD(0.5f, 4f), 3); Add("gasp", b, 0.4f);
        b = Buf(0.9f); Osc(b, 0, 0.9f, ExpSweep(400, 60), AD(0.02f, 2f), 2); NoiseBurst(b, 0, 0.9f, Const(0.1f), Decay(3)); LowPass(b, 0.3f); Add("player_die", b, 0.8f);
        b = Buf(0.3f); Osc(b, 0, 0.3f, ExpSweep(600, 200), Decay(4), 3); NoiseBurst(b, 0, 0.3f, Const(0.3f), Decay(6)); Add("web", b, 0.35f);

        // the drain: a wet rip, then a rising, sucking rush (life pulled out and away)
        b = Buf(0.34f);
        NoiseBurst(b, 0, 0.05f, Const(0.7f), Decay(18));
        NoiseBurst(b, 0.02f, 0.32f, t => 0.04f + 0.4f * t * t, t => MathF.Sin(MathF.Min(1f, t * 1.4f) * MathF.PI) * 0.8f);
        Osc(b, 0.02f, 0.3f, ExpSweep(160, 620), t => MathF.Sin(t * MathF.PI) * 0.5f, 3);
        Osc(b, 0.04f, 0.26f, ExpSweep(240, 900), t => MathF.Sin(t * MathF.PI) * 0.25f, 0);
        LowPass(b, 0.55f); Add("drain", b, 0.6f);

        // the rupture: a deep, wet burst with a crack at its heart
        b = Buf(0.7f);
        NoiseBurst(b, 0, 0.7f, t => 0.35f * (1 - t) + 0.03f, AD(0.01f, 5f));
        Osc(b, 0, 0.5f, ExpSweep(110, 38), AD(0.01f, 5f), 2);
        Osc(b, 0, 0.3f, ExpSweep(420, 90), Decay(9), 1);
        NoiseBurst(b, 0, 0.03f, Const(1f), Decay(25), true);
        for (int k = 0; k < 5; k++) NoiseBurst(b, 0.08f + k * 0.07f, 0.04f, Const(0.5f), Decay(14));
        LowPass(b, 0.45f); Add("rupture", b, 0.9f);
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

    /// <summary>
    /// The camp outside the cave: a 30 s loop in G major, unhurried and sunny. Warm pads through
    /// G - D - Em - C, a soft bass, a plucked arpeggio and a few bell notes echoing, with birdsong
    /// and the fire's crackle under it all.
    /// </summary>
    private static AudioStreamWav BuildCampMusic()
    {
        const float bpm = 64f, beat = 60f / bpm;
        const int bars = 8;
        float len = bars * 4 * beat, tail = 4f;
        var b = Buf(len + tail);
        var pluck = Buf(len + tail);
        var rng = new Random(311);
        static float Swell(float t) => Math.Min(1f, t / 0.18f) * (t > 0.72f ? Math.Max(0f, (1f - t) / 0.28f) : 1f);
        int[][] chords = { new[] { 55, 59, 62 }, new[] { 50, 54, 57 }, new[] { 52, 55, 59 }, new[] { 48, 52, 55 } };
        int[] bass = { 43, 38, 40, 36 };
        int[] melody = { 67, 69, 71, 74, 76, 79 };
        for (int c = 0; c < 4; c++)
        {
            float t0 = c * 8 * beat, dur = 8 * beat;
            // pads: two soft voices a hair apart on each note, swelling in and out
            foreach (int m in chords[c])
            {
                float f = NoteHz(m + 12);
                Osc(b, t0, dur + 1.4f, t => f * (1f + 0.002f * MathF.Sin(t * 25f)), t => 0.05f * Swell(t), 3);
                Osc(b, t0, dur + 1.4f, Const(f * 1.004f), t => 0.035f * Swell(t), 0);
            }
            // the bass: the root on each half bar
            for (int k = 0; k < 8; k += 2)
                Osc(b, t0 + k * beat, beat * 1.9f, Const(NoteHz(bass[c])), AD(0.05f, 3.2f), 0);
            // the arpeggio: up and down the chord in eighths, plucked
            int[] tones = { chords[c][0] + 12, chords[c][1] + 12, chords[c][2] + 12, chords[c][0] + 24 };
            int[] pattern = { 0, 1, 2, 3, 2, 1, 2, 1 };
            for (int e = 0; e < 16; e++)
            {
                float f = NoteHz(tones[pattern[e % 8]]);
                float t = t0 + e * beat * 0.5f;
                float accent = e % 4 == 0 ? 1f : 0.7f;
                Osc(pluck, t, 1.2f, Const(f), t => accent * MathF.Exp(-t * 6f), 0);
                Osc(pluck, t, 0.5f, Const(f * 2f), t => accent * 0.35f * MathF.Exp(-t * 11f), 0);
            }
            // a few bell notes on top, from the major pentatonic
            for (int k = 0; k < 3; k++)
            {
                float t = t0 + (1 + rng.Next(14)) * beat * 0.5f;
                float f = NoteHz(melody[rng.Next(melody.Length)]);
                Osc(pluck, t, 2.4f, Const(f), t => 0.55f * MathF.Exp(-t * 3.2f), 0);
                Osc(pluck, t, 1.4f, Const(f * 3.01f), t => 0.12f * MathF.Exp(-t * 6f), 0);
            }
        }
        for (int k = 0; k < pluck.Length; k++) pluck[k] *= 0.09f;
        Echo(pluck, beat * 0.75f, 0.38f, 0.45f);
        for (int k = 0; k < b.Length; k++) b[k] += pluck[k];

        // birdsong: now and then a little trill of rising chirps, far off
        for (float t = 1.2f; t < len - 1f; t += 2.2f + (float)rng.NextDouble() * 3.5f)
        {
            int notes = 2 + rng.Next(4);
            float f0 = 2600f + 1200f * (float)rng.NextDouble();
            for (int k = 0; k < notes; k++)
                Osc(b, t + k * 0.085f, 0.07f, Sweep(f0, f0 * 1.35f), t => 0.022f * MathF.Sin(t * MathF.PI), 0);
        }
        // the fire: a soft bed of crackle and the odd pop
        for (float t = 0.1f; t < len; t += 0.08f + (float)rng.NextDouble() * 0.6f)
        {
            float amp = rng.NextDouble() < 0.12 ? 0.07f : 0.025f;
            NoiseBurst(b, t, 0.012f + 0.02f * (float)rng.NextDouble(), Const(0.6f), t => amp * MathF.Exp(-t * 6f), true);
        }
        NoiseBurst(b, 0, len, Const(0.02f), Const(0.012f));
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
