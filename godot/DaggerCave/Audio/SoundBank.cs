using System.Linq;
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
    /// <summary>Each effect in three takes (the second and third a shade darker and brighter, each in its own room), played by turns at random.</summary>
    private readonly Dictionary<string, AudioStreamWav[]> _takes = new();
    /// <summary>Sounds that stay dry (the menus' own, and loops).</summary>
    private static readonly HashSet<string> Dry = new() { "ui", "milestone", "xp" };
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
        Synth.Rate = 44100;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        BuildSfx();
        // (the elements' charges are made now: every hero readies them often; the attacks' own sounds are made on first hearing)
        foreach (HeroElement el in Enum.GetValues(typeof(HeroElement)))
            if (el != HeroElement.None)
                foreach (float w in new[] { 0.6f, 1f, 1.4f }) BuildDynamic(DynName(ElementSounds.Ev.Charge, "", el, HeroElement.None, w));
        GD.Print($"[sfx] {_sfx.Count} sounds made in {sw.ElapsedMilliseconds} ms");
        // (the music at half the rate: plenty for it, and half the work at start)
        Synth.Rate = 22050;
        _ambient = BuildAmbientMusic();
        _boss = BuildBossMusic();
        _camp = BuildCampMusic();
        Synth.Rate = 44100;
        GameSettings.ApplySound(); // (makes the Music and SFX buses)
        for (int k = 0; k < 40; k++) { var p = new AudioStreamPlayer2D { MaxDistance = 1100, Attenuation = 1.2f, Bus = "SFX" }; AddChild(p); _pool2D.Add(p); }
        for (int k = 0; k < 8; k++) { var p = new AudioStreamPlayer { Bus = "SFX" }; AddChild(p); _poolUi.Add(p); }
        _musicA = new AudioStreamPlayer { VolumeDb = -8, Bus = "Music" };
        _musicB = new AudioStreamPlayer { VolumeDb = -80, Bus = "Music" };
        AddChild(_musicA); AddChild(_musicB);

        var lp = new AudioEffectLowPassFilter { CutoffHz = 700, Resonance = 0.6f };
        AudioServer.AddBusEffect(0, lp);
        _lowPassIdx = AudioServer.GetBusEffectCount(0) - 1;
        AudioServer.SetBusEffectEnabled(0, _lowPassIdx, false);
    }

    /// <summary>For the tests: the bus each pooled player sends to.</summary>
    public string TestBuses() => string.Join(",", _pool2D.Select(p => (string)p.Bus).Concat(_poolUi.Select(p => (string)p.Bus)).Distinct());

    /// <summary>Cuts every sound effect still playing (between runs).</summary>
    public void StopAll()
    {
        foreach (var p in _pool2D) p.Stop();
        foreach (var p in _poolUi) p.Stop();
        SetUnderwater(false);
    }

    public void SetUnderwater(bool on)
    {
        if (_lowPassIdx >= 0 && AudioServer.IsBusEffectEnabled(0, _lowPassIdx) != on)
            AudioServer.SetBusEffectEnabled(0, _lowPassIdx, on);
    }

    public void Play(string name, Vector2? pos = null, float volDb = 0f, float pitchVar = 0.08f, float pitch = 1f)
    {
        bool dyn = name.Length > 0 && name[0] == '@';
        if (!_sfx.TryGetValue(name, out var st) && (!dyn || (st = BuildDynamic(name)) == null)) return;
        if (dyn)
        {
            // (one attack's sound, struck again within a breath, is the same sound: not a stack of it)
            double now = Time.GetTicksMsec() / 1000.0;
            if (_lastPlayed.TryGetValue(name, out var last) && now - last < 0.045) return;
            _lastPlayed[name] = now;
        }
        if (_takes.TryGetValue(name, out var takes)) st = takes[_rng.Next(takes.Length)];
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

    private readonly Dictionary<string, double> _lastPlayed = new();

    /// <summary>
    /// An element's sound for an event (<see cref="ElementSounds"/>): readying it, an attack loosed, its striking a creature, rock, or
    /// dissipating. <paramref name="key"/> is the attack's own name (its own variant of the element's sound; "" the element's plain one);
    /// a pair layers <paramref name="b"/> under <paramref name="a"/>; <paramref name="weight"/> makes it bigger (a pair, an overcharge).
    /// Made on first hearing, then kept; the name carries all it's made from, so another game makes the same sound.
    /// </summary>
    public void Element(ElementSounds.Ev ev, HeroElement a, Vector2 at, string key = null, HeroElement b = HeroElement.None, float weight = 1f, float volDb = 0f)
    {
        float vol = ev switch { ElementSounds.Ev.Charge => -5f, ElementSounds.Ev.Cast => -5f, ElementSounds.Ev.Hit => -6f, ElementSounds.Ev.Wall => -7f, _ => -10f };
        Play(DynName(ev, key ?? "", a, b, weight), at, vol + volDb, ev == ElementSounds.Ev.Charge ? 0.03f : 0.07f);
    }

    private static string DynName(ElementSounds.Ev ev, string key, HeroElement a, HeroElement b, float weight)
        => $"@{(int)ev}|{key}|{(int)a}|{(int)b}|{weight.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)}";

    /// <summary>Makes (and keeps) a sound named by <see cref="DynName"/>; null if the name isn't one.</summary>
    private AudioStreamWav BuildDynamic(string name)
    {
        var parts = name.Substring(1).Split('|');
        if (parts.Length != 5 || !int.TryParse(parts[0], out int ev) || !int.TryParse(parts[2], out int a) || !int.TryParse(parts[3], out int b)
            || !float.TryParse(parts[4], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float w)) return null;
        int seed = ElementSounds.StableHash(name);
        var e = (ElementSounds.Ev)ev;
        var buf = ElementSounds.Build(e, (HeroElement)a, (HeroElement)b, w, seed);
        var fin = Finish(buf, e == ElementSounds.Ev.Fizzle ? 0.18f : 0.12f, seed, 0f);
        Normalize(fin, e switch { ElementSounds.Ev.Charge => 0.6f, ElementSounds.Ev.Cast => 0.7f, ElementSounds.Ev.Hit => 0.75f, ElementSounds.Ev.Wall => 0.65f, _ => 0.45f });
        var st = ToStream(fin);
        _sfx[name] = st;
        return st;
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
        // (the elements' sounds too: every element's five events, plain and as two attacks of their own, and a pair)
        foreach (HeroElement el in Enum.GetValues(typeof(HeroElement)))
            foreach (ElementSounds.Ev ev in Enum.GetValues(typeof(ElementSounds.Ev)))
                foreach (var key in new[] { "", "Sample Strike", "Sample Storm" })
                    BuildDynamic(DynName(ev, key, el, HeroElement.None, 1f));
        BuildDynamic(DynName(ElementSounds.Ev.Cast, "Sample Mix", HeroElement.Fire, HeroElement.Frost, 1.4f));
        foreach (var kv in _sfx)
        {
            string file = kv.Key;
            if (file.StartsWith("@"))
            {
                var p = file.Substring(1).Split('|');
                file = $"el_{(HeroElement)int.Parse(p[2])}_{(ElementSounds.Ev)int.Parse(p[0])}_{(p[1] == "" ? "plain" : p[1].Replace(' ', '_'))}" + (p[3] != "0" ? $"_{(HeroElement)int.Parse(p[3])}" : "") + $"_w{p[4]}";
            }
            kv.Value.SaveToWav($"{dir}/{file}.wav");
        }
    }

    private string _wantedMusic = "";
    /// <summary>The music cut off (the Glitch is here): whatever is asked for meanwhile is kept, and plays once it is let go.</summary>
    public bool Hushed { get; private set; }

    public void Hush(bool on)
    {
        if (Hushed == on) return;
        Hushed = on;
        var want = _wantedMusic;
        if (on) { _musicA.Stop(); _musicB.Stop(); _currentMusic = ""; _musicA.Stream = null; }
        else { _currentMusic = null; SetMusic(want); }
    }

    public void SetMusic(string which)
    {
        _wantedMusic = which;
        if (Hushed) which = "";
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
        // every effect finished the same way: cleaned, warmed, in a small stone room, in three takes
        float room = Dry.Contains(name) ? 0f : 0.16f;
        int seed = name.GetHashCode();
        var takes = new AudioStreamWav[3];
        float[] tilts = { 0f, -0.35f, 0.3f };
        for (int k = 0; k < 3; k++)
        {
            var b = Finish(buf, room, seed + k * 977, tilts[k]);
            Normalize(b, peak);
            takes[k] = ToStream(b);
        }
        _sfx[name] = takes[0];
        _takes[name] = takes;
    }

    private void BuildSfx()
    {
        float[] b;
        // (every sound below is noise shaped like the real thing: thumps are rumbles, droplets and bubbles are blips of noise, voices are
        // breath through a throat; nothing holds a pitch, nothing plays a note)

        // ---------------------------------------------------------------- blades through the air
        // a sword: a band of wind that climbs into the pass and falls away, a breath of edge at the top of it
        b = Buf(0.2f);
        Whoosh(b, 0, 0.2f, t => 700f + 2600f * MathF.Sin(t * MathF.PI * 0.85f), t => MathF.Pow(MathF.Sin(MathF.Min(1f, t * 1.15f) * MathF.PI), 1.4f), 2.2f, 0.4f, 11);
        BandNoise(b, 0, 0.2f, t => 4200f + 1500f * t, Const(6f), t => 0.3f * MathF.Pow(MathF.Sin(t * MathF.PI), 2f));
        Add("swing", b, 0.5f);
        // a heavy blow: a deeper, longer rush, the weight of the arm behind it
        b = Buf(0.32f);
        Whoosh(b, 0, 0.32f, t => 260f + 1500f * MathF.Sin(t * MathF.PI * 0.8f), t => MathF.Pow(MathF.Sin(MathF.Min(1f, t * 1.2f) * MathF.PI), 1.2f), 1.6f, 0.5f, 12);
        BandNoise(b, 0.04f, 0.2f, t => 2600f + 800f * t, Const(5f), t => 0.25f * MathF.Sin(t * MathF.PI));
        Rumble(b, 0, 0.32f, 90f, t => 0.35f * MathF.Sin(t * MathF.PI));
        Add("swing_heavy", b, 0.65f);
        // a shortsword: short and quick, a tight snap of air
        b = Buf(0.15f);
        Whoosh(b, 0, 0.15f, t => 1100f + 2400f * MathF.Sin(t * MathF.PI), t => MathF.Pow(MathF.Sin(t * MathF.PI), 1.6f), 2.6f, 0.3f, 13);
        Add("swing_short", b, 0.45f);
        // a dagger's jab: a thin hiss of air, all edge
        b = Buf(0.1f);
        Whoosh(b, 0, 0.1f, t => 2400f + 2800f * t, t => MathF.Pow(MathF.Sin(t * MathF.PI), 2f), 3.2f, 0.2f, 14);
        Add("swing_dagger", b, 0.4f);
        // a cleaver: a heavy chop of iron through the air, a clank of the machine's arm behind it
        b = Buf(0.3f);
        Whoosh(b, 0, 0.3f, t => 220f + 1100f * MathF.Sin(t * MathF.PI * 0.8f), t => MathF.Pow(MathF.Sin(MathF.Min(1f, t * 1.2f) * MathF.PI), 1.2f), 1.3f, 0.5f, 15);
        Modes(b, 0.0f, 480f, MetalModes, 4f, 0.12f);
        Crackle(b, 0, 0.06f, Const(400f), Const(0.4f), 1800f, 16, 900f);
        Add("swing_cleaver", b, 0.6f);

        // ---------------------------------------------------------------- blows landing
        // flesh and hide: the crack of the contact, a wet slap, the dull weight of the body behind it
        b = Buf(0.22f);
        NoiseBurst(b, 0, 0.004f, Const(1f), Decay(40), true);
        BandNoise(b, 0, 0.05f, t => 2600f - 1200f * t, Const(1.6f), t => 0.8f * MathF.Exp(-t * 9f));
        Rumble(b, 0, 0.18f, 110f, t => 1.1f * MathF.Exp(-t * 9f));
        BandNoise(b, 0.004f, 0.11f, t => 1100f - 500f * t, Const(1.2f), Decay(14));
        NoiseBurst(b, 0, 0.03f, Const(0.5f), Decay(30));
        Crunch(b, 0.35f);
        Add("hit", b, 0.85f);
        // stone, earth and armour: a hard chink with a scatter of grit
        b = Buf(0.25f);
        NoiseBurst(b, 0, 0.012f, Const(1f), Decay(30), true);
        Modes(b, 0, 1750f, StoneModes, 2.4f, 0.45f);
        Modes(b, 0.004f, 620f, StoneModes, 2f, 0.2f);
        Crackle(b, 0.02f, 0.15f, Const(140f), t => 0.7f * (1f - t), 2600f, 21, 800f);
        Crunch(b, 0.5f);
        Add("hit_stone", b, 0.85f);
        // wood, roots and leaf: a crackling thud
        b = Buf(0.3f);
        Modes(b, 0, 150f, WoodModes, 1.6f, 0.6f);
        Rumble(b, 0, 0.12f, 120f, Decay(14));
        Crackle(b, 0.005f, 0.13f, Const(160f), Const(0.8f), 2200f, 22, 900f);
        BandNoise(b, 0, 0.15f, Const(2200f), Const(1.2f), Decay(14));
        Crunch(b, 0.35f);
        Add("hit_wood", b, 0.85f);
        // frost and crystal: a brittle crack, the shatter, a few shards
        b = Buf(0.3f);
        NoiseBurst(b, 0, 0.02f, Const(1f), Decay(25), true);
        Crackle(b, 0, 0.1f, Const(600f), t => 0.8f * (1f - t), 4300f, 23, 1300f);
        Tinkles(b, 0.01f, 0.28f, 10, 5000f, 0.4f, 24, 0.45f, 2f);
        Rumble(b, 0, 0.08f, 180f, Decay(20));
        Add("hit_ice", b, 0.8f);
        // fire: a soft thump, a hiss and sparks
        b = Buf(0.35f);
        Rumble(b, 0, 0.12f, 120f, Decay(14));
        Whoosh(b, 0, 0.3f, t => 3600f - 1800f * t, t => 0.6f * MathF.Exp(-t * 6f), 0.9f, 0.5f, 25);
        Crackle(b, 0.02f, 0.3f, Const(70f), t => 0.9f * (1f - t), 3000f, 26, 1000f);
        Add("hit_fire", b, 0.75f);
        // water: a wet slap and a burst of droplets
        b = Buf(0.3f);
        BandNoise(b, 0, 0.12f, t => 900f - 500f * t, Const(1.3f), Decay(14));
        Rumble(b, 0, 0.1f, 160f, Decay(16));
        Crackle(b, 0.03f, 0.25f, t => 60f * (1f - t), Const(0.6f), 1400f, 27, 180f);
        LowPass(b, 0.5f);
        Add("hit_water", b, 0.7f);
        // a dagger going in: a short bite of steel, the thunk of it seating, a wet edge
        b = Buf(0.18f);
        NoiseBurst(b, 0, 0.006f, Const(1f), Decay(40), true);
        BandNoise(b, 0, 0.04f, t => 3800f - 1500f * t, Const(2f), Decay(18));
        Rumble(b, 0.004f, 0.12f, 150f, Decay(16));
        BandNoise(b, 0.01f, 0.1f, Const(1300f), Const(1.4f), Decay(12));
        Crunch(b, 0.4f);
        Add("dagger_hit", b, 0.75f);
        // a dagger biting into rock: steel on stone (a hard tick and a scrape), grit falling
        b = Buf(0.3f);
        NoiseBurst(b, 0, 0.01f, Const(1f), Decay(35), true);
        Modes(b, 0, 2300f, MetalModes, 5f, 0.25f);
        BandNoise(b, 0.008f, 0.08f, t => 3200f - 1200f * t, Const(2.5f), t => 0.5f * MathF.Exp(-t * 10f));
        Crackle(b, 0.03f, 0.25f, t => 70f * (1f - t), Const(0.5f), 2400f, 28, 700f);
        Crunch(b, 0.45f);
        Add("dagger_wall", b, 0.7f);
        // a dagger glancing off: a bright skitter of steel off stone
        b = Buf(0.22f);
        NoiseBurst(b, 0, 0.008f, Const(1f), Decay(35), true);
        Modes(b, 0, 2900f, MetalModes, 4f, 0.2f);
        Crackle(b, 0.01f, 0.15f, Const(90f), t => 0.6f * (1f - t), 3200f, 29, 900f);
        Add("dagger_glance", b, 0.55f);

        // steel on steel or a shield taking a blow: a hard strike, a short metallic chink (it doesn't sing), grit off it
        b = Buf(0.28f);
        NoiseBurst(b, 0, 0.015f, Const(1f), Decay(25), true);
        Modes(b, 0, 1150f, MetalModes, 3.2f, 0.4f);
        BandNoise(b, 0, 0.06f, Const(3500f), Const(1.5f), Decay(30));
        Crackle(b, 0.005f, 0.08f, Const(250f), Const(0.4f), 3000f, 30, 1200f);
        Crunch(b, 0.3f);
        Add("clink", b, 0.6f);

        b = Buf(0.22f); Whoosh(b, 0, 0.22f, t => 1500f + 3500f * t, t => MathF.Pow(MathF.Sin(MathF.Min(1f, t * 1.3f) * MathF.PI), 1.5f), 2.5f, 0.4f, 31); Add("throw", b, 0.55f);

        // ---------------------------------------------------------------- the body moving
        // a push off the ground: cloth, a scuff of dirt and a low thump
        b = Buf(0.16f);
        BandNoise(b, 0, 0.12f, t => 500f + 900f * t, Const(1.2f), t => MathF.Sin(MathF.Min(1f, t * 1.5f) * MathF.PI) * 0.6f);
        Rumble(b, 0, 0.06f, 100f, Decay(20));
        Grains(b, 0.0f, 0.07f, 7, 3600f, 0.22f, 11);
        Add("jump", b, 0.4f);
        // landing: the weight coming down, the boots' scuff, and grit scattering away
        b = Buf(0.24f);
        Rumble(b, 0, 0.14f, 85f, t => 1.1f * MathF.Exp(-t * 7f));
        BandNoise(b, 0, 0.12f, t => 700f - 300f * t, Const(1f), Decay(20));
        NoiseBurst(b, 0.006f, 0.05f, Const(0.45f), Decay(30), true);
        Grains(b, 0.012f, 0.14f, 14, 3200f, 0.3f, 23);
        Crunch(b, 0.3f);
        Add("land", b, 0.45f);
        b = Buf(0.24f); Whoosh(b, 0, 0.24f, t => 1600f * (1f - 0.7f * t) + 300f, AD(0.2f, 5f), 1.3f, 0.6f, 32); Add("dodge", b, 0.55f);
        b = Buf(0.2f); Whoosh(b, 0, 0.2f, t => 500f + 3500f * t, AD(0.35f, 5f), 1.8f, 0.5f, 33); Add("airdash", b, 0.55f);
        // a lunge: the body thrown forward, a rush and the scuff of the push
        b = Buf(0.22f); Whoosh(b, 0, 0.22f, t => 700f + 1800f * t, AD(0.25f, 6f), 1.4f, 0.5f, 34); Rumble(b, 0, 0.08f, 110f, Decay(18)); Add("lunge", b, 0.5f);

        // ---------------------------------------------------------------- water
        b = Buf(0.5f);
        Whoosh(b, 0, 0.5f, t => 2800f - 2200f * t, t => MathF.Exp(-t * 5f), 0.9f, 0.6f, 34);
        Crackle(b, 0.04f, 0.42f, t => 40f * (1f - t), Const(0.7f), 1200f, 35, 170f);
        LowPass(b, 0.55f);
        Add("splash", b, 0.6f);
        // the hero plunging in: an open crash of spray, the body's thud going under, then the gulp and rush of bubbles
        b = Buf(0.9f);
        NoiseBurst(b, 0, 0.02f, Const(1f), Decay(30));
        Whoosh(b, 0, 0.8f, t => 3200f - 2600f * MathF.Sqrt(t), t => 0.9f * MathF.Exp(-t * 4.2f), 0.8f, 0.6f, 36);
        Rumble(b, 0.01f, 0.3f, 120f, t => 0.9f * MathF.Exp(-t * 6f));
        Gurgle(b, 0.08f, 0.7f, 380f, t => 0.6f * MathF.Exp(-t * 2.5f), 37, 22f);
        LowPass(b, 0.6f);
        Add("splash_in", b, 0.85f);
        // climbing out: water sheeting off and a few drips
        b = Buf(0.5f);
        Whoosh(b, 0, 0.35f, t => 1200f + 900f * t, t => 0.8f * MathF.Sin(MathF.Min(1f, t * 2f) * MathF.PI * 0.5f) * MathF.Exp(-t * 4f), 1.1f, 0.6f, 38);
        Rumble(b, 0.02f, 0.2f, 110f, t => 0.35f * MathF.Exp(-t * 8f));
        Crackle(b, 0.12f, 0.38f, t => 18f * (1f - t), Const(0.5f), 1500f, 39, 160f);
        LowPass(b, 0.5f);
        Add("splash_out", b, 0.5f);
        // a bubble popping at the surface: a tiny blurp of noise
        b = Buf(0.07f); BandNoise(b, 0, 0.06f, t => 900f + 900f * t, Const(7f), t => MathF.Sin(t * MathF.PI)); NoiseBurst(b, 0.05f, 0.01f, Const(1f), Decay(30), true); Add("bubble", b, 0.25f);

        // ---------------------------------------------------------------- bodies
        // the hero struck: a body blow, the smack of it on leather and a short breathy grunt
        b = Buf(0.3f);
        Rumble(b, 0, 0.2f, 110f, Decay(11));
        BandNoise(b, 0, 0.08f, Const(1800f), Const(1.2f), Decay(22));
        Voice(b, 0.025f, 0.22f, t => 150f - 45f * t, new[] { 640f, 1150f, 2400f }, new[] { 90f, 110f, 160f }, t => MathF.Sin(MathF.Min(1f, t * 3f) * MathF.PI * 0.5f) * MathF.Exp(-t * 3.5f) * 1.1f, 0.45f, 5);
        LowPass(b, 0.55f);
        Crunch(b, 0.25f);
        Add("hurt", b, 0.75f);
        // a creature falling: the body hitting the floor, gear and grit scattering
        b = Buf(0.42f);
        Rumble(b, 0, 0.3f, 75f, Decay(8));
        BandNoise(b, 0, 0.38f, t => 1500f - 1100f * t, Const(1f), Decay(6));
        Crackle(b, 0.05f, 0.3f, t => 60f * (1f - t), Const(0.6f), 1800f, 40, 700f);
        LowPass(b, 0.5f);
        Crunch(b, 0.3f);
        Add("enemy_die", b, 0.6f);

        // ---------------------------------------------------------------- the game's own (no real thing to copy: textures, not notes)
        // a mote of experience taken: a soft crystalline sparkle
        b = Buf(0.14f);
        NoiseBurst(b, 0, 0.006f, Const(1f), Decay(30), true);
        Tinkles(b, 0, 0.1f, 5, 6500f, 0.35f, 41, 0.3f, 0.7f);
        Add("xp", b, 0.28f);
        // a level gained: air rushing in and up, a deep bloom of weight, then a bright shower of crystal and sparks falling away
        b = Buf(1.5f);
        Whoosh(b, 0, 0.5f, t => 300f + 3600f * t * t, t => 0.9f * MathF.Pow(t, 1.6f), 1.4f, 0.6f, 42);
        Rumble(b, 0.42f, 0.9f, 70f, t => 1.2f * MathF.Exp(-t * 3.5f));
        NoiseBurst(b, 0.42f, 0.9f, t => 0.05f + 0.1f * (1 - t), t => 0.7f * MathF.Exp(-t * 3.5f));
        Tinkles(b, 0.45f, 0.95f, 40, 6000f, 0.4f, 43, 0.5f, 1.5f);
        Crackle(b, 0.45f, 0.8f, t => 160f * (1f - t), t => 0.6f * (1f - t), 3600f, 44, 1300f);
        Echo(b, 0.11f, 0.3f, 0.25f);
        Add("levelup", b, 0.7f);
        // a milestone point earned: a rising flurry of crystal, brightening, and a breath of air
        b = Buf(1.1f);
        for (int k = 0; k < 5; k++) Tinkles(b, k * 0.07f, 0.35f, 8, 4200f + 900f * k, 0.35f, 45 + k, 0.25f, 0.6f);
        Whoosh(b, 0, 0.6f, t => 1500f + 3000f * t, t => 0.3f * MathF.Sin(t * MathF.PI), 1.6f, 0.5f, 50);
        Echo(b, 0.09f, 0.3f, 0.3f);
        Add("milestone", b, 0.55f);
        // the cards laid out: a riffle of stiff paper and a soft glitter over it
        b = Buf(0.9f);
        for (int k = 0; k < 9; k++)
        {
            float at = k * 0.018f + 0.004f * MathF.Abs(Noise());
            BandNoise(b, at, 0.03f, Const(2600f + 900f * Noise()), Const(2.2f), t => 0.55f * MathF.Exp(-t * 7f));
        }
        Tinkles(b, 0.16f, 0.5f, 16, 5200f, 0.22f, 51, 0.4f, 1f);
        Whoosh(b, 0.16f, 0.6f, t => 2500f + 1500f * t, t => 0.12f * MathF.Sin(t * MathF.PI), 1.4f, 0.5f, 52);
        Add("card_reveal", b, 0.5f);
        // a relic laid out: a deep plate of old metal struck (a gong's wash, inharmonic), a swell of air under it, glitter
        b = Buf(1.6f);
        Modes(b, 0, 96f, MetalModes, 0.45f, 0.6f);
        Rumble(b, 0, 1.4f, 60f, t => 0.5f * MathF.Sin(MathF.Min(1f, t * 3f) * MathF.PI * 0.5f) * MathF.Exp(-t * 2.2f));
        Whoosh(b, 0, 1.2f, t => 800f + 600f * t, t => 0.2f * MathF.Sin(t * MathF.PI), 1f, 0.6f, 53);
        Tinkles(b, 0.25f, 0.6f, 14, 6400f, 0.2f, 54, 0.4f, 1.2f);
        Add("relic_reveal", b, 0.6f);
        // a card taken: a firm stamp of it into the hand, a crisp snap of paper, a glint
        b = Buf(0.6f);
        Rumble(b, 0, 0.1f, 130f, Decay(18));
        BandNoise(b, 0, 0.05f, Const(1400f), Const(1.4f), Decay(25));
        NoiseBurst(b, 0.01f, 0.02f, Const(1f), Decay(30), true);
        Tinkles(b, 0.04f, 0.2f, 6, 6000f, 0.3f, 55, 0.3f, 0.8f);
        Add("card_take", b, 0.55f);
        // the cards set aside: a soft sweep of paper sliding away
        b = Buf(0.35f);
        BandNoise(b, 0, 0.3f, t => 2400f - 1500f * t, Const(1.3f), t => MathF.Sin(MathF.Min(1f, t * 2.5f) * MathF.PI * 0.5f) * MathF.Exp(-t * 4f) * 0.6f);
        Add("card_leave", b, 0.35f);
        // a potion: the clink of the glass and the slosh inside it
        b = Buf(0.45f);
        Modes(b, 0, 2100f, GlassModes, 2.2f, 0.35f);
        Gurgle(b, 0.04f, 0.3f, 600f, t => 0.4f * MathF.Sin(t * MathF.PI), 56, 16f);
        Add("pickup_potion", b, 0.45f);
        // a key: a little jangle of iron on its ring
        b = Buf(0.4f);
        for (int k = 0; k < 4; k++) Modes(b, k * 0.035f + 0.01f * MathF.Abs(Noise()), 2600f + 700f * Noise(), MetalModes, 3.2f, 0.25f);
        Add("pickup_key", b, 0.45f);

        // an old wooden chest: the hinge creaking open, the lid knocking back against the wood, dust settling
        b = Buf(0.9f);
        Creak(b, 0.0f, 0.42f, 300f, 55f, 0.55f, 5);
        Creak(b, 0.05f, 0.36f, 520f, 40f, 0.3f, 9);
        BandNoise(b, 0, 0.4f, t => 900f + 500f * t, Const(3f), t => 0.12f * MathF.Sin(t * MathF.PI));
        Modes(b, 0.46f, 130f, WoodModes, 1f, 0.9f);
        Rumble(b, 0.46f, 0.15f, 100f, Decay(14));
        NoiseBurst(b, 0.46f, 0.02f, Const(0.5f), Decay(30));
        Modes(b, 0.53f, 190f, WoodModes, 1.3f, 0.3f);
        Crackle(b, 0.6f, 0.25f, t => 30f * (1f - t), Const(0.6f), 2600f, 57, 900f);
        LowPass(b, 0.6f);
        Add("chest", b, 0.7f);

        // ---------------------------------------------------------------- creatures
        // a bat: two quick breathy squeaks (noise through a narrow band, sweeping down)
        b = Buf(0.14f);
        BandNoise(b, 0, 0.05f, t => 5200f - 1400f * t, Const(9f), t => MathF.Sin(t * MathF.PI));
        BandNoise(b, 0.07f, 0.05f, t => 5600f - 1600f * t, Const(9f), t => 0.8f * MathF.Sin(t * MathF.PI));
        Add("bat", b, 0.25f);

        // an insect's buzz: a rough hum fluttering with the wing beats, with a breath of wing noise (a real buzz has a pitch: it's the wings)
        {
            const float len = 0.55f;
            b = Buf(len);
            Func<float, float> env = AD(0.12f, 3f);
            float Beat(float t) => 0.6f + 0.4f * MathF.Sin(t * len * MathF.Tau * 38f);
            Osc(b, 0, len, t => 190 + 14 * MathF.Sin(t * len * MathF.Tau * 9f) + 25 * t, t => Beat(t) * env(t), 2);
            NoiseBurst(b, 0, len, Const(0.35f), t => 0.35f * Beat(t) * env(t));
            LowPass(b, 0.35f);
            Add("buzz", b, 0.45f);
        }

        // a frog: a wet croak (a rough throat pulsing, through a mouth)
        b = Buf(0.3f);
        Voice(b, 0, 0.3f, t => 95f + 15f * MathF.Sin(t * 50f), new[] { 420f, 1100f, 2200f }, new[] { 120f, 200f, 300f }, t => (MathF.Sin(t * 70f) > -0.2f ? 1f : 0.2f) * MathF.Exp(-t * 3f), 0.5f, 61);
        LowPass(b, 0.3f); Add("frog", b, 0.45f);
        // a tongue lashing out: a wet flick
        b = Buf(0.18f); Whoosh(b, 0, 0.18f, t => 1400f - 800f * t, Decay(6), 2f, 0.4f, 62); BandNoise(b, 0.12f, 0.05f, Const(900f), Const(1.5f), Decay(20)); Add("tongue", b, 0.3f);
        // a goblin: a rough snarling grunt
        b = Buf(0.24f);
        Voice(b, 0, 0.24f, t => 160f + 40f * MathF.Sin(t * 20f), new[] { 700f, 1300f, 2600f }, new[] { 140f, 180f, 260f }, AD(0.1f, 5f), 0.55f, 63);
        LowPass(b, 0.35f); Crunch(b, 0.3f); Add("goblin", b, 0.5f);
        // a great blow on the ground: the boom of the weight, a crack, debris thrown
        b = Buf(0.7f);
        Rumble(b, 0, 0.6f, 55f, AD(0.01f, 4f));
        NoiseBurst(b, 0, 0.7f, t => 0.06f * (1 - t) + 0.01f, AD(0.02f, 4f));
        NoiseBurst(b, 0, 0.02f, Const(1f), Decay(30), true);
        Crackle(b, 0.01f, 0.5f, t => 260f * (1f - t), t => 0.7f * (1f - t), 1600f, 64, 600f);
        Crunch(b, 0.55f);
        Add("slam", b, 0.9f);
        // lava: thick bubbling and the sizzle of it
        b = Buf(0.45f);
        Gurgle(b, 0, 0.45f, 220f, AD(0.1f, 4f), 65, 14f);
        NoiseBurst(b, 0, 0.45f, Const(0.6f), t => 0.25f * MathF.Exp(-t * 3f), true);
        Crackle(b, 0, 0.4f, Const(60f), t => 0.6f * (1f - t), 2600f, 66, 900f);
        Add("lava", b, 0.35f);
        b = Buf(0.1f); NoiseBurst(b, 0, 0.1f, Const(0.12f), Decay(15)); BandNoise(b, 0, 0.08f, t => 600f - 300f * t, Const(1.2f), Decay(10)); Add("flop", b, 0.35f);
        // an eel: a wet slither and a hiss
        b = Buf(0.35f); Whoosh(b, 0, 0.35f, t => 900f + 300f * MathF.Sin(t * 30f), AD(0.05f, 3f), 1.2f, 0.8f, 67); NoiseBurst(b, 0, 0.35f, Const(0.7f), t => 0.3f * MathF.Exp(-t * 3), true); LowPass(b, 0.5f); Add("eel", b, 0.45f);
        b = Buf(0.2f); Crackle(b, 0, 0.18f, Const(30f), Const(1f), 2600f, 68, 500f); Add("spider", b, 0.3f);
        b = Buf(0.15f); NoiseBurst(b, 0, 0.15f, Const(0.8f), Decay(12), true); Crackle(b, 0, 0.1f, Const(200f), Const(0.6f), 3000f, 69, 900f); Add("spike", b, 0.35f);
        // rock: a crunch of stone, rubble rattling
        b = Buf(0.3f);
        NoiseBurst(b, 0, 0.25f, t => 0.2f * (1 - t) + 0.02f, Decay(9));
        Rumble(b, 0, 0.2f, 80f, Decay(9));
        Crackle(b, 0, 0.28f, t => 300f * (1f - t), t => 0.8f * (1f - t), 1600f, 70, 550f);
        Crunch(b, 0.5f);
        Add("rock", b, 0.5f);
        // a beast's roar: a huge throat growling (a low rough voice, ragged, breathy), the chest's rumble under it
        b = Buf(1.4f);
        Voice(b, 0, 1.4f, t => 72f + 10f * MathF.Sin(t * 35f) + 18f * MathF.Sin(t * MathF.PI), new[] { 380f, 900f, 2100f }, new[] { 140f, 220f, 340f }, AD(0.1f, 2.2f), 0.6f, 71);
        Rumble(b, 0, 1.4f, 70f, AD(0.1f, 2.5f));
        LowPass(b, 0.25f);
        Crunch(b, 0.45f);
        Add("roar", b, 0.9f);

        // a rift: a rising rush of air, a churning under it, then it closes with a soft thud
        b = Buf(1.3f);
        Whoosh(b, 0, 1.2f, t => 250f + 2200f * MathF.Sin(t * MathF.PI * 0.9f), t => 0.9f * MathF.Sin(t * MathF.PI), 1.6f, 0.8f, 72);
        Rumble(b, 0, 1.2f, 70f, t => 0.6f * MathF.Sin(t * MathF.PI));
        Rumble(b, 1.0f, 0.25f, 90f, Decay(12));
        LowPass(b, 0.3f);
        Add("portal", b, 0.6f);

        // a menu tick: a crisp little knock of wood
        b = Buf(0.06f); Modes(b, 0, 760f, WoodModes, 2.2f, 0.5f); NoiseBurst(b, 0, 0.006f, Const(0.6f), Decay(30), true); Add("ui", b, 0.35f);
        // healing: a soft rush of restoring air with motes of light glittering in it
        b = Buf(0.45f);
        Whoosh(b, 0, 0.4f, t => 900f + 2600f * t, t => 0.7f * MathF.Sin(MathF.Min(1f, t * 1.2f) * MathF.PI), 2.5f, 0.6f, 73);
        Tinkles(b, 0.05f, 0.35f, 9, 5800f, 0.25f, 74, 0.4f, 0.8f);
        LowPass(b, 0.55f);
        Add("heal", b, 0.4f);
        // a gasp: a sharp breath drawn in
        b = Buf(0.4f); Whisper(b, 0, 0.4f, new[] { 800f, 1300f, 2700f }, AD(0.4f, 3f), 75); Add("gasp", b, 0.4f);
        // the hero falling: a last exhale, the body striking the floor, gear rattling down
        b = Buf(0.9f);
        Whisper(b, 0, 0.5f, new[] { 600f, 1100f, 2500f }, t => MathF.Sin(MathF.Min(1f, t * 3f) * MathF.PI * 0.5f) * MathF.Exp(-t * 3f), 76);
        Rumble(b, 0.3f, 0.5f, 70f, Decay(6));
        Crackle(b, 0.32f, 0.5f, t => 80f * (1f - t), Const(0.6f), 1800f, 77, 700f);
        LowPass(b, 0.4f);
        Add("player_die", b, 0.8f);
        // a web: sticky strands stretching and tearing
        b = Buf(0.3f); Crackle(b, 0, 0.3f, Const(260f), t => 0.6f * MathF.Sin(t * MathF.PI), 1400f, 78, 400f); BandNoise(b, 0, 0.3f, t => 900f - 400f * t, Const(2f), Decay(5)); Add("web", b, 0.35f);
        // a rope: a soft hemp swish with a little creak in it
        b = Buf(0.28f); Whoosh(b, 0, 0.28f, Const(1200f), Decay(8), 1f, 0.5f, 79); Creak(b, 0.02f, 0.18f, 240f, 60f, 0.25f, 3); Add("rope", b, 0.3f);

        // the drain: a wet rip, then a rising, sucking rush (life pulled out and away)
        b = Buf(0.34f);
        NoiseBurst(b, 0, 0.05f, Const(0.7f), Decay(18));
        Whoosh(b, 0.02f, 0.32f, t => 400f + 1600f * t * t, t => MathF.Sin(MathF.Min(1f, t * 1.4f) * MathF.PI) * 0.8f, 1.6f, 0.6f, 80);
        Gurgle(b, 0.02f, 0.3f, 500f, t => 0.35f * MathF.Sin(t * MathF.PI), 81, 18f);
        LowPass(b, 0.55f); Add("drain", b, 0.6f);

        // the rupture: a deep, wet burst with a crack at its heart
        b = Buf(0.7f);
        NoiseBurst(b, 0, 0.7f, t => 0.35f * (1 - t) + 0.03f, AD(0.01f, 5f));
        Rumble(b, 0, 0.5f, 70f, AD(0.01f, 5f));
        NoiseBurst(b, 0, 0.03f, Const(1f), Decay(25), true);
        Gurgle(b, 0.05f, 0.4f, 300f, t => 0.5f * (1f - t), 82, 16f);
        LowPass(b, 0.45f); Crunch(b, 0.4f); Add("rupture", b, 0.9f);

        // the Glitch: lo-fi and wrong. A screech (a square wave leaping between random pitches, crushed to a few bits and sample-held
        // down to a few kilohertz, with dropouts), a stutter of digital chirps, and a hum that cuts in and out
        {
            var rng = new Random(404);
            float Rnd() => (float)rng.NextDouble();
            float[] Crush(float seconds, Func<float, float> pitch, Func<float, float> env, float noise, int holdHz, float levels, int seed)
            {
                var buf = Buf(seconds);
                rng = new Random(seed);
                float ph = 0, held = 0; int hold = 0;
                for (int i = 0; i < buf.Length; i++)
                {
                    float t = i / (float)Rate;
                    ph += pitch(t) / Rate;
                    float sq = (ph % 1f) < 0.5f ? 1f : -1f;
                    float v = sq * (1f - noise) + (Rnd() * 2f - 1f) * noise;
                    if (hold-- <= 0) { hold = Rate / holdHz; held = MathF.Round(v * levels) / levels; }
                    buf[i] = held * env(t);
                }
                return buf;
            }
            float jump = 1200f;
            b = Crush(0.6f, t => { if (Rnd() < 0.0009f) jump = 250f + 3200f * Rnd(); return jump * (1f + 0.3f * MathF.Sin(t * 60f)); },
                t => MathF.Min(1f, t * 60f) * MathF.Exp(-t * 2.4f) * (((int)(t * 41f)) % 6 == 0 ? 0.1f : 1f), 0.35f, 5000, 2.5f, 41);
            Add("glitch", b, 0.55f);
            b = Crush(0.16f, t => 2600f - 9000f * t + 1800f * MathF.Sign(MathF.Sin(t * 180f)), t => MathF.Min(1f, t * 200f) * (((int)(t * 70f)) % 2 == 0 ? 1f : 0.2f) * (1f - t / 0.16f), 0.15f, 8000, 2f, 77);
            Add("glitch_chirp", b, 0.4f);
            b = Crush(0.9f, t => 55f + 4f * MathF.Sin(t * 13f), t => MathF.Sin(MathF.Min(1f, t * 3f) * MathF.PI * 0.5f) * (1f - t / 0.9f) * (((int)(t * 23f)) % 4 == 1 ? 0f : 1f), 0.25f, 2000, 3f, 91);
            Add("glitch_hum", b, 0.5f);
        }
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

    private static AudioStreamWav _drone;
    /// <summary>
    /// The waterfall's voice, a seamless loop: a deep steady roar (low-passed noise in two bands, slowly breathing) over a faint
    /// sub hum. Made once, on first asking.
    /// </summary>
    public static AudioStreamWav WaterfallDrone
    {
        get
        {
            if (_drone != null) return _drone;
            const float len = 6f, lap = 1f;
            var b = Buf(len + lap);
            BandNoise(b, 0, len + lap, t => 140f + 30f * MathF.Sin(t * 0.7f), Const(0.7f), t => 0.9f + 0.1f * MathF.Sin(t * 1.3f));
            BandNoise(b, 0, len + lap, Const(420f), Const(0.9f), t => 0.35f + 0.08f * MathF.Sin(t * 2.1f + 1f));
            LowPass(b, 0.12f);
            Osc(b, 0, len + lap, Const(48f), Const(0.12f));
            Osc(b, 0, len + lap, Const(72f), Const(0.05f));
            // (the tail fades out as the head fades in: folded over, the seam is a crossfade)
            int nl = (int)(lap * Rate);
            for (int k = 0; k < nl; k++) { float w = k / (float)nl; b[k] *= w; b[b.Length - nl + k] *= 1f - w; }
            return _drone = Loop(b, len);
        }
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
