using System;
using Godot;

namespace DaggerCave;

/// <summary>
/// The game's name on the main menu: the title logo (Art/Title, cut from its picture by tools/logo_prep.py) as a carved 3D relief in
/// a little 3D view of its own. Its thickness at every point is twice its distance to the nearest edge (T = 2·D, as the shrine
/// flowers), the picture its texture; the red smoke and glow round it are drawn flat, added on behind. It sways slowly from side to
/// side, never far enough to stop reading, while a light drifts over it, so the depth catches the eye.
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
    private static Texture2D _tex, _glow;
    private static float _w, _h;

    /// <summary>
    /// The logo, <paramref name="width"/> pixels wide, standing in a slot <paramref name="slotHeight"/> high; it reaches
    /// <paramref name="overTop"/> above the slot (its flourishes overhang). Null if its pictures can't be had.
    /// </summary>
    public static TitleLogo Create(float width, float slotHeight, float overTop)
    {
        if (!Load()) return null;
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
            _mesh = ReliefMesh.Build(himg, maxDist, d => d * Thickness * 0.5f, Scale, _w * 0.5f, _h * 0.5f, 2);
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
            box.AddChild(new TextureRect
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
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoTexture = _tex, Transparency = BaseMaterial3D.TransparencyEnum.AlphaScissor, AlphaScissorThreshold = 0.5f,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled, Metallic = 0.35f, Roughness = 0.34f,
                TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic,
                EmissionEnabled = true, EmissionTexture = _tex, EmissionOperator = BaseMaterial3D.EmissionOperatorEnum.Multiply,
                Emission = new Color(1f, 0.95f, 0.95f), EmissionEnergyMultiplier = 0.36f,
            },
        });
    }

    public override void _Process(double delta)
    {
        if (_art == null || !IsVisibleInTree()) return;
        _t += (float)delta;
        // (a slow sway, never far enough to stop reading; a light drifting over it)
        _art.Rotation = new Vector3(0.06f * MathF.Sin(_t * 0.37f), 0.3f * MathF.Sin(_t * 0.42f), 0f);
        _key.Rotation = new Vector3(-0.55f + 0.15f * MathF.Sin(_t * 0.23f), -0.5f + 0.5f * MathF.Sin(_t * 0.17f), 0f);
    }
}
