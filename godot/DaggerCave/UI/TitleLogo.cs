using System;
using Godot;

namespace DaggerCave;

/// <summary>
/// The game's name on the main menu: the title logo (Art/Title, cut from its picture by tools/logo_prep.py) as a carved 3D relief in
/// a little 3D view of its own. Its thickness at every point is twice its distance to the nearest edge (T = 2·D, as the shrine
/// flowers), the picture its texture; the red smoke and glow round it are drawn flat, added on behind. It sways slowly from side to
/// side, never far enough to stop reading, while a light drifts over it, so the depth catches the eye.
/// <para>It can also grow in (<see cref="Grow"/>, the intro at launch): its shader leaves out everything thinner than a vanish line,
/// and the line sweeps down from above the thickest point to nothing, so the letters' thick cores form first and the thin spikes,
/// curls and points grow out of them last. The line walks down the logo's thicknesses in order, so the shown share of it grows
/// evenly; the red glow behind comes up only near the end.</para>
/// </summary>
public partial class TitleLogo : Control
{
    private const string Dir = "res://DaggerCave/Art/Title/";
    /// <summary>Metres per picture pixel inside its little world.</summary>
    private const float Scale = 1f / 300f;
    /// <summary>The whole thickness as a share of the distance to the nearest edge (each face stands off half of it).</summary>
    public const float Thickness = 2f;

    private Node3D _art;
    private DirectionalLight3D _key;
    private float _t;
    private static ArrayMesh _mesh;
    private static float[] _ranks;
    private ShaderMaterial _mat;
    private TextureRect _glowRect;
    private float _growT = -1f, _growDur;

    /// <summary>The carved title's look (as a StandardMaterial3D would draw it), plus the vanish line: thinner than <c>cut</c> (picture
    /// pixels of thickness, carried in UV2.x) is left out.</summary>
    private const string ShaderCode = @"
shader_type spatial;
render_mode cull_disabled;
uniform sampler2D tex : source_color, filter_linear_mipmap_anisotropic;
uniform float cut = 0.0;
uniform vec3 emission_tint = vec3(1.0, 0.95, 0.95);
uniform float emission_energy = 0.36;
varying float thick;
void vertex() { thick = UV2.x; }
void fragment() {
    vec4 c = texture(tex, UV);
    if (cut > 0.0 && thick < cut) discard;
    ALBEDO = c.rgb;
    ALPHA = c.a;
    ALPHA_SCISSOR_THRESHOLD = 0.5;
    METALLIC = 0.35;
    ROUGHNESS = 0.34;
    EMISSION = c.rgb * emission_tint * emission_energy;
}";
    private static Texture2D _tex, _glow;
    private static float _w, _h;

    /// <summary>
    /// The logo, <paramref name="width"/> pixels wide, standing in a slot <paramref name="slotHeight"/> high; it reaches
    /// <paramref name="overTop"/> above the slot (its flourishes overhang). Null if its pictures can't be had.
    /// </summary>
    public static TitleLogo Create(float width, float slotHeight, float overTop)
    {
        if (!Load()) return null;
        // (no slot height asked: the view's own, the picture's shape and a little room to sway)
        if (slotHeight <= 0f) slotHeight = width / (_w / _h) * 1.08f;
        var t = new TitleLogo { CustomMinimumSize = new Vector2(width, slotHeight), MouseFilter = MouseFilterEnum.Ignore };
        t._width = width; t._overTop = overTop;
        return t;
    }

    private float _width, _overTop;

    private static bool Load()
    {
        if (_mesh != null) return true;
        try
        {
            var meta = Json.ParseString(FileAccess.GetFileAsString(Dir + "title.json")).AsGodotDictionary();
            _tex = GD.Load<Texture2D>(Dir + "title.png");
            _glow = GD.Load<Texture2D>(Dir + "title_glow.png");
            var himg = GD.Load<Texture2D>(Dir + "title_h.png")?.GetImage();
            if (_tex == null || himg == null) return false;
            float maxDist = (float)meta["maxDist"].AsDouble();
            _w = himg.GetWidth(); _h = himg.GetHeight();
            _mesh = ReliefMesh.Build(himg, maxDist, d => d * Thickness * 0.5f, Scale, _w * 0.5f, _h * 0.5f, 2, out _ranks);
            return true;
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[title] the 3D title couldn't be built: {ex.Message}");
            return false;
        }
    }

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        // the view: as wide as asked, a little taller than the picture's own shape (room to sway)
        const float margin = 1.08f;
        float aspect = _w / _h;
        float viewH = _width / aspect * margin;
        var box = new Control { MouseFilter = MouseFilterEnum.Ignore, Position = new Vector2(0, -_overTop), Size = new Vector2(_width, viewH) };
        AddChild(box);
        // the red smoke and glow, flat behind (added, so it lights what's behind it), fitted to the picture's place in the view
        if (_glow != null)
        {
            box.AddChild(_glowRect = new TextureRect
            {
                Texture = _glow, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                Size = new Vector2(_width, viewH), MouseFilter = MouseFilterEnum.Ignore, Modulate = new Color(1f, 1f, 1f, 0.9f),
                Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
            });
        }
        var holder = new SubViewportContainer { Stretch = true, Size = new Vector2(_width, viewH), MouseFilter = MouseFilterEnum.Ignore };
        box.AddChild(holder);
        var vp = new SubViewport
        {
            TransparentBg = true, OwnWorld3D = true, Msaa3D = Viewport.Msaa.Msaa4X, Size = new Vector2I((int)_width, (int)viewH),
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
        };
        holder.AddChild(vp);
        float hM = _h * Scale * margin;
        const float dist = 5f;
        vp.AddChild(new Camera3D { Position = new Vector3(0, 0, dist), Fov = Mathf.RadToDeg(2f * MathF.Atan(hM * 0.5f / dist)), Current = true });
        vp.AddChild(new WorldEnvironment
        {
            Environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.Color, BackgroundColor = new Color(0, 0, 0, 0),
                AmbientLightSource = Godot.Environment.AmbientSource.Color, AmbientLightColor = new Color(0.78f, 0.78f, 0.84f), AmbientLightEnergy = 0.8f,
                TonemapMode = Godot.Environment.ToneMapper.Filmic,
            },
        });
        // a warm key from the upper left that drifts, a red rim from behind to catch the carved edges
        _key = new DirectionalLight3D { LightColor = new Color(1f, 0.96f, 0.9f), LightEnergy = 2.0f, Rotation = new Vector3(-0.55f, -0.5f, 0f) };
        vp.AddChild(_key);
        vp.AddChild(new DirectionalLight3D { LightColor = new Color(1f, 0.4f, 0.35f), LightEnergy = 0.55f, Rotation = new Vector3(-0.2f, 2.6f, 0f) });
        _art = new Node3D();
        vp.AddChild(_art);
        _art.AddChild(new MeshInstance3D
        {
            Mesh = _mesh, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            MaterialOverride = _mat = new ShaderMaterial { Shader = new Shader { Code = ShaderCode } },
        });
        _mat.SetShaderParameter("tex", _tex);
        ApplyGrow();
    }

    /// <summary>Grows the title in over <paramref name="seconds"/> (from nothing: call before it is first drawn).</summary>
    public void Grow(float seconds)
    {
        _growDur = Math.Max(0.1f, seconds);
        _growT = 0f;
        ApplyGrow();
    }

    /// <summary>Still growing in.</summary>
    public bool Growing => _growT >= 0f;

    /// <summary>Done at once (the intro skipped).</summary>
    public void FinishGrow() { _growT = -1f; ApplyGrow(); }

    private void ApplyGrow()
    {
        if (_mat == null) return;
        float t = _growT < 0f ? 1f : Math.Min(1f, _growT / _growDur);
        float cut = 0f;
        if (t < 1f && _ranks != null)
        {
            // (down the thicknesses in order: the shown share grows evenly, the thinnest, the points, last)
            float x = t * 256f;
            int i = Math.Min(255, (int)x);
            cut = t <= 0f ? _ranks[0] + 1f : Mathf.Lerp(_ranks[i], _ranks[i + 1], x - i);
            cut = Math.Max(cut, 0.01f);
        }
        _mat.SetShaderParameter("cut", cut);
        if (_glowRect != null)
        {
            float g = Math.Clamp((t - 0.6f) / 0.4f, 0f, 1f);
            _glowRect.Modulate = new Color(1f, 1f, 1f, 0.9f * g * g);
        }
    }

    public override void _Process(double delta)
    {
        if (_art == null || !IsVisibleInTree()) return;
        _t += (float)delta;
        if (_growT >= 0f)
        {
            _growT += MathF.Min((float)delta, 1f / 20f); // (a long frame doesn't jump the growth)
            if (_growT >= _growDur) _growT = -1f;
            ApplyGrow();
        }
        // (a slow sway, never far enough to stop reading; a light drifting over it)
        _art.Rotation = new Vector3(0.06f * MathF.Sin(_t * 0.37f), 0.3f * MathF.Sin(_t * 0.42f), 0f);
        _key.Rotation = new Vector3(-0.55f + 0.15f * MathF.Sin(_t * 0.23f), -0.5f + 0.5f * MathF.Sin(_t * 0.17f), 0f);
    }
}
