using System;
using System.Collections.Generic;
using System.Globalization;
using Godot;

namespace DaggerCave;

/// <summary>
/// Look development for creatures: `--modelsheet=NAME[,NAME2...] [--sheetclips=clip:t,clip:t...]
/// [--sheetyaw=DEG] --shots=DIR` lines each creature up in several clips under studio lights and
/// saves DIR/sheet_NAME.png (no level is built).
/// `--sheetfilm=CLIP:SECONDS[:FPS]` films one creature playing the clip instead (the first clip's framing, or --sheetfocus's):
/// a second of it played first so its springs settle, then DIR/film_NAME_NNN.png every frame, time stepping exactly 1/FPS
/// (24 by default) whatever the machine's speed. A clip that loops (run, idle, fly...) goes round once in the SECONDS; any
/// other plays from start to end in them.
/// </summary>
public partial class ModelSheet : Node3D
{
    private readonly List<string> _names = new();
    private string _dir = "/tmp";
    private (string clip, float t)[] _clips;
    private float _yaw = -22f;
    /// <summary>Ground speed (m/s) for the clips that run ("run:PHASE" lays the gait out at exactly that phase).</summary>
    private float _speed = 9f;
    private int _cols;
    private bool m_debug, _soft, _clay;
    /// <summary>--sheetfocus=Y,H: frames the first clip alone, centred at height Y (model space), H metres tall (close-ups).</summary>
    private Vector2? _focus;
    private int _index = -1, _frame;
    private readonly List<(CreatureModel m, string clip, float t)> _row = new();
    private Camera3D _cam;
    private float _time;
    private string _filmClip;
    private float _filmSeconds, _filmFps = 24f;

    public static bool Wanted => Array.Exists(OS.GetCmdlineUserArgs(), a => a.StartsWith("--modelsheet="));

    public override void _Ready()
    {
        CreatureLibrary.CellScale = 1f; // full detail for close-ups
        foreach (var a in OS.GetCmdlineUserArgs())
        {
            if (a.StartsWith("--modelsheet=")) _names.AddRange(a[13..].Split(','));
            else if (a.StartsWith("--shots=")) _dir = a[8..];
            else if (a.StartsWith("--sheetyaw=")) _yaw = float.Parse(a[11..], CultureInfo.InvariantCulture);
            else if (a.StartsWith("--sheetspeed=")) _speed = float.Parse(a[13..], CultureInfo.InvariantCulture);
            else if (a.StartsWith("--sheetcols=")) _cols = int.Parse(a[12..]);
            else if (a.StartsWith("--sheetfocus=")) { var f = a[13..].Split(','); _focus = new Vector2(float.Parse(f[0], CultureInfo.InvariantCulture), float.Parse(f[1], CultureInfo.InvariantCulture)); }
            else if (a == "--sheetsoft") _soft = true;
            // (--sheetclay: a light grey backdrop and bright, even light, for judging forms rather than mood)
            else if (a == "--sheetclay") _clay = true;
            else if (a == "--sheetfx") _fxSheet = true;
            else if (a.StartsWith("--sheetfilm="))
            {
                var f = a[12..].Split(':');
                _filmClip = f[0];
                _filmSeconds = f.Length > 1 ? float.Parse(f[1], CultureInfo.InvariantCulture) : 1f;
                if (f.Length > 2) _filmFps = float.Parse(f[2], CultureInfo.InvariantCulture);
            }
            else if (a == "--sheetdebug") { m_debug = true; HeroDesign.DebugLog = true; }
            else if (a.StartsWith("--sheetclips="))
            {
                var list = new List<(string, float)>();
                foreach (var c in a[13..].Split(','))
                {
                    var kv = c.Split(':');
                    list.Add((kv[0], kv.Length > 1 ? float.Parse(kv[1], CultureInfo.InvariantCulture) : 0.3f));
                }
                _clips = list.ToArray();
            }
        }
        // the game's stage has its own environment and lights: keep them out of the studio
        if (Stage3D.I != null) { Stage3D.I.Visible = false; Stage3D.I.WorldEnv.QueueFree(); }
        var env = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Color,
            BackgroundColor = _clay ? new Color(0.42f, 0.43f, 0.45f) : new Color(0.17f, 0.18f, 0.2f),
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = new Color(0.55f, 0.58f, 0.65f),
            AmbientLightEnergy = _clay ? 1.1f : _soft ? 0.7f : 0.45f,
            TonemapMode = Godot.Environment.ToneMapper.Agx,
            SsaoEnabled = true,
            GlowEnabled = true,
            GlowIntensity = 0.9f,
            GlowStrength = 1.0f,
            GlowBloom = 0.04f,
            GlowHdrThreshold = 1.0f,
            GlowBlendMode = Godot.Environment.GlowBlendModeEnum.Additive,
        };
        for (int k = 0; k < 7; k++) env.SetGlowLevel(k, k is >= 1 and <= 4 ? 1f : 0f);
        AddChild(new WorldEnvironment { Environment = env });
        var key = new DirectionalLight3D { LightEnergy = _clay ? 3.4f : _soft ? 1.5f : 2.6f, LightColor = new Color(1f, 0.93f, 0.85f), ShadowEnabled = true };
        AddChild(key);
        key.LookAtFromPosition(new Vector3(3, 4, 5), Vector3.Zero, Vector3.Up);
        var fill = new DirectionalLight3D { LightEnergy = 0.8f, LightColor = new Color(0.75f, 0.8f, 1f) };
        AddChild(fill);
        fill.LookAtFromPosition(new Vector3(-4, 1, 3), Vector3.Zero, Vector3.Up);
        var rim = new DirectionalLight3D { LightEnergy = _soft ? 0.5f : 1.6f, LightColor = new Color(0.6f, 0.75f, 1f) };
        AddChild(rim);
        rim.LookAtFromPosition(new Vector3(-3, 2, -4), Vector3.Zero, Vector3.Up);
        _cam = new Camera3D { Fov = 30f, Current = true };
        AddChild(_cam);
        if (_fxSheet) { SetupFx(); return; }
        Next();
    }

    private void Next()
    {
        foreach (var (m, _, _) in _row) m.QueueFree();
        _row.Clear();
        _index++;
        _frame = 0;
        _time = 0;
        if (_index >= _names.Count) { SafeQuit.Request(this); return; }
        string name = _names[_index];
        var clips = _clips ?? DefaultClips(name);
        if (_filmClip != null) clips = new[] { (_filmClip, 0f) };
        if (_focus != null) clips = new[] { clips[0] };
        float span = 0;
        var probe = CreatureModel.Create(name);
        if (probe == null) { GD.PrintErr($"[modelsheet] no design '{name}'"); Next(); return; }
        var bounds = probe.Kit.Sculpt.Bounds;
        float floorY = probe.Design.FloorY;
        probe.Free();
        // a grid, roughly twice as wide as tall, framed to fill the view
        float h = bounds.Size.Y * 1.15f + 0.2f;
        float w = Math.Max(Math.Max(bounds.Size.X, bounds.Size.Z) * 1.1f + 0.2f, h * 0.9f); // poses reach wider than the rest pose
        int cols = Math.Max(1, (int)MathF.Ceiling(MathF.Sqrt(clips.Length * 2.2f * h / w)));
        if (_cols > 0) cols = _cols;
        cols = Math.Min(cols, clips.Length);
        int rows = (clips.Length + cols - 1) / cols;
        span = w * cols;
        float footY = float.IsNaN(floorY) ? bounds.Position.Y : floorY;
        for (int k = 0; k < clips.Length; k++)
        {
            int cx = k % cols, cy = k / cols;
            var m = CreatureModel.Create(name);
            AddChild(m);
            m.Position = new Vector3(-span * 0.5f + w * (cx + 0.5f), -footY + (rows - 1 - cy) * h, -cy * 0.01f);
            _row.Add((m, clips[k].clip, clips[k].t));
        }
        float tanV = MathF.Tan(Mathf.DegToRad(_cam.Fov * 0.5f));
        if (_focus is Vector2 fc)
        {
            _row[0].m.Position = new Vector3(0, -footY, 0);
            float fy = fc.X - footY;
            _cam.Position = new Vector3(0, fy, fc.Y * 0.5f / tanV);
            _cam.LookAt(new Vector3(0, fy, 0), Vector3.Up);
            return;
        }
        var vp = GetViewport().GetVisibleRect().Size;
        float tanH = tanV * vp.X / vp.Y;
        float totalH = h * rows;
        float dist = Math.Max(span * 0.5f / tanH, totalH * 0.5f / tanV) * 1.08f + Math.Max(bounds.Size.Z, 0.5f);
        _cam.Position = new Vector3(0, totalH * 0.5f, dist);
        _cam.LookAt(new Vector3(0, totalH * 0.5f - 0.05f, 0), Vector3.Up);
    }

    private static (string, float)[] DefaultClips(string name)
    {
        if (name is "swordsman" or "warden")
            return new[]
            {
                ("idle", 0.3f), ("run", 0.1f), ("run", 0.6f), ("slash_a_fwd", 0.28f), ("slash_a_fwd", 0.5f), ("slash_b_upfwd", 0.5f), ("jump_rise", 0.5f), ("hurt", 0.4f),
            };
        // every clip of the sprite set (turns aside); attacks at two moments
        var list = new List<(string, float)>();
        var seen = new HashSet<string>();
        var names = new List<string>(SpriteSet.Get(name).Names);
        names.Sort(StringComparer.Ordinal);
        foreach (var n in names)
        {
            string c = n.EndsWith("_r") || n.EndsWith("_l") ? n[..^2] : n;
            if (c.StartsWith("turn") || !seen.Add(c)) continue;
            bool attack = c is "windup" or "strike" or "slash" or "bite" or "swipe" or "throw" or "dive" or "pounce" or "rear" or "roar"
                or "spit" or "sting" or "slam" or "breath" or "tongue" or "charge" or "attack" or "shoot" or "stomp" or "smash" or "lunge";
            if (attack) { list.Add((c, 0.3f)); list.Add((c, 0.7f)); }
            else if (c == "death") { list.Add((c, 0.45f)); list.Add((c, 1f)); }
            else list.Add((c, 0.3f));
        }
        return list.ToArray();
    }

    /// <summary>One frame of a film: the clip at this frame's moment (after a warm-up of still frames for the shaders, and a
    /// second's run-in for the springs), saved when it counts.</summary>
    private void FilmFrame()
    {
        const int Warm = 20;
        int frames = Math.Max(1, (int)MathF.Round(_filmSeconds * _filmFps));
        int runIn = (int)_filmFps;
        _frame++;
        float dt = 1f / _filmFps;
        int k = _frame - Warm - runIn;   // the frame being filmed (negative: the run-in)
        bool loops = Moving(_filmClip) || _filmClip is "idle" or "float" or "hover" or "fly" or "swim";
        float t = loops ? ((k % frames) + frames) % frames / (float)frames : Math.Clamp(k / (float)Math.Max(1, frames - 1), 0f, 1f);
        float time = Math.Max(0, _frame - Warm) * dt;
        foreach (var (m, clip, _) in _row)
        {
            var a = new AnimInput { Clip = clip, T = t, Frame = (int)(t * 8), Frames = 8, Time = time, Dt = _frame > Warm ? dt : 0f, Facing = 1, OnFloor = true,
                Vel = Moving(clip) ? new Vector2(clip == "walk" ? 2.5f : clip == "run" ? _speed : 6f, 0) : Vector2.Zero, ClipTime = _filmSeconds };
            m.Face(1, clip, t, 1f);
            if (clip == "run") m.Animate(a with { FixedGait = true, Gait = t });
            else m.Animate(a);
            m.UpdatePivot(Vector2.One, 0f, 0.8f, false);
            var pr = m.Pivot.RotationDegrees;
            m.Pivot.RotationDegrees = new Vector3(pr.X, _yaw, pr.Z);
        }
        if (k >= 0 && k < frames)
            GetViewport().GetTexture().GetImage().SavePng($"{_dir}/film_{_names[_index]}_{k:000}.png");
        if (k >= frames - 1) Next();
    }

    private static bool Moving(string clip) => clip.StartsWith("run") || clip is "walk" or "crawl" or "fly" or "swim" or "hop" or "slither";

    public override void _Process(double delta)
    {
        if (_fxSheet) { FxFrame(); return; }
        if (_filmClip != null) { FilmFrame(); return; }
        float dt = (float)delta;
        _time += dt;
        foreach (var (m, clip, t) in _row)
        {
            var a = new AnimInput { Clip = clip, T = t, Frame = (int)(t * 8), Frames = 8, Time = _time, Dt = dt, Facing = 1, OnFloor = true, Vel = Moving(clip) ? new Vector2(clip == "walk" ? 2.5f : clip == "run" ? _speed : 6f, 0) : Vector2.Zero, ClipTime = 0.5f };
            m.Face(1, clip, t, 1f);
            // ("air:T" poses the body in the air: T 0 rising hard, 0.5 the apex, 1 falling fast)
            if (clip == "air") a = a with { Clip = "jump", OnFloor = false, Vel = new Vector2(3f, (0.5f - t) * 14f) };
            if (clip == "run") m.Animate(a with { FixedGait = true, Gait = t });
            else m.Animate(a);
            m.UpdatePivot(Vector2.One, 0f, 0.8f, false);
            var pr = m.Pivot.RotationDegrees;
            m.Pivot.RotationDegrees = new Vector3(pr.X, _yaw, pr.Z);
        }
        if (_frame == 28 && m_debug)
        {
            foreach (var (m, clip, t) in _row)
                if (m.Design is HeroDesign hd && hd.BladeWorld(m, 0, out var g, out var tip))
                {
                    var d = tip - g;
                    GD.Print($"[sheet] {clip}:{t:0.00} blade guard ({g.X:0.00},{g.Y:0.00},{g.Z:0.00}) dir angle {Mathf.RadToDeg(MathF.Atan2(d.Y, d.X)):0}°");
                }
        }
        if (++_frame == 30)
        {
            string path = $"{_dir}/sheet_{_names[_index]}.png";
            GetViewport().GetTexture().GetImage().SavePng(path);
            GD.Print($"[modelsheet] saved {path}");
            Next();
        }
    }
}
