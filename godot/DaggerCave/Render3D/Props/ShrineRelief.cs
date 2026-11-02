using System;
using System.Collections.Generic;
using Godot;

namespace DaggerCave;

/// <summary>
/// The shrine flowers made from their pictures (Art/Shrines, cut from the reference sheet by tools/shrine_prep.py): each picture is the
/// texture, and its height map holds, for every solid pixel, its distance to the nearest edge. The model is a relief built from that:
/// both faces stand off from the middle by a share of that distance (<see cref="ChestView.ShrineThickness"/>), so every piece is
/// knife-sharp at its edges and swells along its middle, its depth read straight from its outline. The finer flowers' red glow is the
/// picture itself (black and all), drawn additively just behind.
/// </summary>
public partial class ChestView
{
    private sealed class Relief
    {
        public ArrayMesh Mesh;
        public Texture2D Tex, Glow;
        public float W, H, PivotX, PivotY, Top;
    }

    private static readonly Relief[] _reliefs = new Relief[4];
    private static Godot.Collections.Dictionary _reliefMeta;
    /// <summary>Metres per picture pixel for a level's shrine (a guardian's is bigger).</summary>
    private const float ReliefScale = 0.0034f;
    private float _flowerMid = -1f, _flowerTop = -1f;

    /// <summary>The relief for a grade (0 Uncommon .. 3 Legendary), built once and kept; null if its pictures can't be had.</summary>
    private static Relief ReliefFor(int g)
    {
        if (_reliefs[g] != null) return _reliefs[g];
        try
        {
            const string dir = "res://DaggerCave/Art/Shrines/";
            _reliefMeta ??= Json.ParseString(FileAccess.GetFileAsString(dir + "shrines.json")).AsGodotDictionary();
            var m = _reliefMeta[g.ToString()].AsGodotDictionary();
            var tex = GD.Load<Texture2D>($"{dir}shrine_{g}.png");
            var glow = GD.Load<Texture2D>($"{dir}shrine_{g}_glow.png");
            var himg = GD.Load<Texture2D>($"{dir}shrine_{g}_h.png")?.GetImage();
            if (tex == null || himg == null) return null;
            if (himg.IsCompressed()) himg.Decompress();
            float maxDist = (float)m["maxDist"].AsDouble(), px = (float)m["pivotX"].AsDouble(), py = (float)m["pivotY"].AsDouble();
            int w = himg.GetWidth(), h = himg.GetHeight();
            // (the height, in picture pixels, of each face above the middle at a pixel: a share of its distance to the nearest edge)
            float share = ShrineThickness * 0.5f;
            float Height(int x, int y)
            {
                if (x < 0 || y < 0 || x >= w || y >= h) return 0f;
                var c = himg.GetPixel(x, y);
                float d = (Mathf.Round(c.R * 255f) * 256f + Mathf.Round(c.G * 255f)) / 65535f * maxDist;
                return d * share;
            }
            const int step = 3;
            int nx = (w - 1) / step + 1, ny = (h - 1) / step + 1;
            var hs = new float[nx, ny];
            for (int i = 0; i < nx; i++) for (int j = 0; j < ny; j++) hs[i, j] = Height(i * step, j * step);
            var mb = new MeshBuilder();
            var idx = new int[2, nx, ny];
            for (int s = 0; s < 2; s++) for (int i = 0; i < nx; i++) for (int j = 0; j < ny; j++) idx[s, i, j] = -1;
            int Vert(int s, int i, int j)
            {
                if (idx[s, i, j] >= 0) return idx[s, i, j];
                float sz = s == 0 ? 1f : -1f;
                float hx = (hs[Math.Min(nx - 1, i + 1), j] - hs[Math.Max(0, i - 1), j]) / (2f * step);
                float hy = (hs[i, Math.Min(ny - 1, j + 1)] - hs[i, Math.Max(0, j - 1)]) / (2f * step);
                // (picture y runs down, the world's up: the slope's sign turns over)
                var n = new Vector3(-hx * sz, hy * sz, 1f * sz).Normalized();
                var pos = new Vector3((i * step - px) * ReliefScale, (py - j * step) * ReliefScale, hs[i, j] * ReliefScale * sz);
                return idx[s, i, j] = mb.Add(pos, n, Colors.White, new Vector2((float)(i * step) / (w - 1), (float)(j * step) / (h - 1)));
            }
            for (int i = 0; i < nx - 1; i++)
                for (int j = 0; j < ny - 1; j++)
                {
                    // (a cell that touches the flower at all; the picture's alpha trims it to the exact outline)
                    if (hs[i, j] <= 0f && hs[i + 1, j] <= 0f && hs[i, j + 1] <= 0f && hs[i + 1, j + 1] <= 0f)
                    {
                        // (a pixel inside a cell can be solid though its corners aren't: look at its middle too)
                        if (Height(i * step + step / 2, j * step + step / 2) <= 0f) continue;
                    }
                    for (int s = 0; s < 2; s++)
                    {
                        int a = Vert(s, i, j), b = Vert(s, i + 1, j), c = Vert(s, i + 1, j + 1), d = Vert(s, i, j + 1);
                        if (s == 0) { mb.Tri(a, b, c); mb.Tri(a, c, d); } else { mb.Tri(a, c, b); mb.Tri(a, d, c); }
                    }
                }
            return _reliefs[g] = new Relief
            {
                Mesh = mb.ToMesh(), Tex = tex, Glow = glow, W = w, H = h, PivotX = px, PivotY = py, Top = (float)m["top"].AsDouble(),
            };
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[shrine] relief {g} couldn't be built: {ex.Message}");
            return null;
        }
    }

    /// <summary>The flower of this grade as a relief from its picture (false if its pictures are missing: then it is built by hand).</summary>
    private bool BuildRelief(bool boss, CardGrade grade)
    {
        int g = GradeStep(grade);
        var r = ReliefFor(g);
        if (r == null) return false;
        float k = boss ? 2.6f / 1.9f : 1f;
        var glowCol = CrystalGlow(grade);
        _floatY = 0.19f * k;
        _float = new Node3D { Position = new Vector3(0, _floatY, 0) };
        AddChild(_float);
        var art = new Node3D { Scale = Vector3.One * k };
        _float.AddChild(art);
        // the relief: the picture as its colour, a little light of its own tinted by the grade's glow (so it reads in the dark)
        _crystalMat = new StandardMaterial3D
        {
            AlbedoTexture = r.Tex, Transparency = BaseMaterial3D.TransparencyEnum.AlphaScissor, AlphaScissorThreshold = 0.5f,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled, Metallic = 0.35f, Roughness = 0.32f,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic,
            EmissionEnabled = true, EmissionTexture = r.Tex, EmissionOperator = BaseMaterial3D.EmissionOperatorEnum.Multiply,
            Emission = glowCol.Lerp(Colors.White, 0.45f), EmissionEnergyMultiplier = ReliefGlow(g),
        };
        art.AddChild(new MeshInstance3D { Mesh = r.Mesh, MaterialOverride = _crystalMat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        _redMat = null;
        float wM = r.W * ReliefScale, hM = r.H * ReliefScale;
        var centre = new Vector3((r.W * 0.5f - r.PivotX) * ReliefScale, (r.PivotY - r.H * 0.5f) * ReliefScale, 0f);
        // the finer ones' red glow: the picture itself, added on behind
        if (g >= 2 && r.Glow != null)
        {
            var glowMat = new StandardMaterial3D
            {
                AlbedoTexture = r.Glow, AlbedoColor = new Color(g >= 3 ? 0.75f : 0.55f, g >= 3 ? 0.75f : 0.55f, g >= 3 ? 0.75f : 0.55f),
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, BlendMode = BaseMaterial3D.BlendModeEnum.Add,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled, DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Disabled,
                TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmaps,
            };
            art.AddChild(new MeshInstance3D { Mesh = new QuadMesh { Size = new Vector2(wM, hM) }, MaterialOverride = glowMat, Position = centre + new Vector3(0, 0, -0.06f), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
            _gemGlow = PropViews.Sprite(new Color(1f, 0.15f, 0.15f), 0, g >= 3 ? 0.12f : 0.08f, Math.Max(wM, hM) * 0.9f);
            _gemGlow.Position = centre + new Vector3(0, 0, -0.12f);
            art.AddChild(_gemGlow);
        }
        var halo = PropViews.Sprite(glowCol, 0, g == 0 ? 0.14f : g == 1 ? 0.12f : 0.07f, Math.Max(wM, hM) * 1.1f);
        halo.Position = centre + new Vector3(0, 0, -0.15f);
        art.AddChild(halo);
        _flowerMid = centre.Y * k;
        _flowerTop = (r.PivotY - r.Top) * ReliefScale * k;
        // rays in a ring round it (hollow in the middle, so the flower stays clear), and for a Legendary a pillar of light above it
        if (g >= 2)
        {
            bool leg = g >= 3;
            _rays = PropViews.Sprite(leg ? new Color(1f, 0.88f, 0.45f) : new Color(1f, 0.66f, 0.4f), 8, leg ? 0.26f : 0.13f, Math.Max(wM, hM) * k * (leg ? 2.3f : 2.1f), leg ? 0.3f : -0.18f);
            AddChild(_rays);
            if (leg)
            {
                _beam = PropViews.Sprite(new Color(1f, 0.88f, 0.6f), 0, 0.18f, 1f);
                _beam.Scale = new Vector3(0.25f * k, 8f, 1f);
                AddChild(_beam);
            }
        }
        return true;
    }

    /// <summary>How brightly a relief glows by grade (the warmer glows look brighter, so they're set lower).</summary>
    private static float ReliefGlow(int g) => g switch { 1 => 0.5f, 2 => 0.36f, 3 => 0.3f, _ => 0.38f };
}
