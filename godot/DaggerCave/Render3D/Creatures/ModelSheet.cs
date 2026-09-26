using System;
using System.Collections.Generic;
using System.Globalization;
using Godot;

namespace DaggerCave;

/// <summary>
/// Look development for creatures: `--modelsheet=NAME[,NAME2...] [--sheetclips=clip:t,clip:t...]
/// [--sheetyaw=DEG] --shots=DIR` lines each creature up in several clips under studio lights and
/// saves DIR/sheet_NAME.png (no level is built).
/// </summary>
public partial class ModelSheet : Node3D
{
    private readonly List<string> _names = new();
    private string _dir = "/tmp";
    private (string clip, float t)[] _clips;
    private float _yaw = -22f;
    private int _index = -1, _frame;
    private readonly List<(CreatureModel m, string clip, float t)> _row = new();
    private Camera3D _cam;
    private float _time;

    public static bool Wanted => Array.Exists(OS.GetCmdlineUserArgs(), a => a.StartsWith("--modelsheet="));

    public override void _Ready()
    {
        foreach (var a in OS.GetCmdlineUserArgs())
        {
            if (a.StartsWith("--modelsheet=")) _names.AddRange(a[13..].Split(','));
            else if (a.StartsWith("--shots=")) _dir = a[8..];
            else if (a.StartsWith("--sheetyaw=")) _yaw = float.Parse(a[11..], CultureInfo.InvariantCulture);
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
            BackgroundColor = new Color(0.09f, 0.095f, 0.11f),
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = new Color(0.55f, 0.58f, 0.65f),
            AmbientLightEnergy = 0.45f,
            TonemapMode = Godot.Environment.ToneMapper.Agx,
            SsaoEnabled = true,
            GlowEnabled = true,
            GlowIntensity = 0.8f,
            GlowHdrThreshold = 1.0f,
        };
        AddChild(new WorldEnvironment { Environment = env });
        var key = new DirectionalLight3D { LightEnergy = 1.6f, LightColor = new Color(1f, 0.92f, 0.82f), ShadowEnabled = true };
        AddChild(key);
        key.LookAtFromPosition(new Vector3(3, 4, 5), Vector3.Zero, Vector3.Up);
        var rim = new DirectionalLight3D { LightEnergy = 1.2f, LightColor = new Color(0.6f, 0.75f, 1f) };
        AddChild(rim);
        rim.LookAtFromPosition(new Vector3(-3, 2, -4), Vector3.Zero, Vector3.Up);
        var floor = new MeshInstance3D { Mesh = new PlaneMesh { Size = new Vector2(80, 30) }, MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.16f, 0.16f, 0.17f), Roughness = 0.9f } };
        AddChild(floor);
        _cam = new Camera3D { Fov = 30f, Current = true };
        AddChild(_cam);
        Next();
    }

    private void Next()
    {
        foreach (var (m, _, _) in _row) m.QueueFree();
        _row.Clear();
        _index++;
        _frame = 0;
        _time = 0;
        if (_index >= _names.Count) { GetTree().Quit(); return; }
        string name = _names[_index];
        var clips = _clips ?? DefaultClips(name);
        float span = 0;
        var probe = CreatureModel.Create(name);
        if (probe == null) { GD.PrintErr($"[modelsheet] no design '{name}'"); Next(); return; }
        var bounds = probe.Kit.Sculpt.Bounds;
        float floorY = probe.Design.FloorY;
        probe.Free();
        float w = Math.Max(bounds.Size.X, bounds.Size.Z) * 1.25f + 0.3f;
        span = w * clips.Length;
        float footY = float.IsNaN(floorY) ? bounds.Position.Y : floorY;
        for (int k = 0; k < clips.Length; k++)
        {
            var m = CreatureModel.Create(name);
            AddChild(m);
            m.Position = new Vector3(-span * 0.5f + w * (k + 0.5f), -footY, 0);
            _row.Add((m, clips[k].clip, clips[k].t));
        }
        float h = bounds.Size.Y;
        float dist = Math.Max(span * 0.5f / MathF.Tan(Mathf.DegToRad(_cam.Fov * 0.5f)) / 1.6f, h * 2.2f / MathF.Tan(Mathf.DegToRad(_cam.Fov * 0.5f)) * 0.5f) + 1f;
        _cam.Position = new Vector3(0, h * 0.62f, dist);
        _cam.LookAt(new Vector3(0, h * 0.45f, 0), Vector3.Up);
    }

    private static (string, float)[] DefaultClips(string name) => new[]
    {
        ("idle", 0.3f), ("run", 0.1f), ("run", 0.6f), ("slash_a_fwd", 0.28f), ("slash_a_fwd", 0.5f), ("slash_b_upfwd", 0.5f), ("jump_rise", 0.5f), ("hurt", 0.4f),
    };

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _time += dt;
        foreach (var (m, clip, t) in _row)
        {
            var a = new AnimInput { Clip = clip, T = t, Frame = (int)(t * 8), Frames = 8, Time = _time, Dt = dt, Facing = 1, OnFloor = true, Vel = clip.StartsWith("run") ? new Vector2(6f, 0) : Vector2.Zero, ClipTime = 0.5f };
            m.Face(1, clip, t, 1f);
            m.Animate(a);
            m.UpdatePivot(Vector2.One, 0f, 0.8f, false);
            var pr = m.Pivot.RotationDegrees;
            m.Pivot.RotationDegrees = new Vector3(pr.X, _yaw, pr.Z);
        }
        if (++_frame == 14)
        {
            string path = $"{_dir}/sheet_{_names[_index]}.png";
            GetViewport().GetTexture().GetImage().SavePng(path);
            GD.Print($"[modelsheet] saved {path}");
            Next();
        }
    }
}
