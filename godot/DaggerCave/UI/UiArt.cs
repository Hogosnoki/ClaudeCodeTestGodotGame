using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

/// <summary>
/// The menus' look, made by code: tileable surface textures (each kind of choice has its own: wood for a chest's treasure, velvet for a
/// relic, a night sky for a milestone, gold for a guardian's hoard, veined stone for a vault, and a surface per kind of card), rounded
/// frames whose ornament grows with an upgrade's rarity, and the small marks a card wears (whose it is, what it is for, what it rules out).
/// Nothing in the menus is a flat rectangle of one colour, and no corner is square.
/// </summary>
public static class UiArt
{
    // ================================================================== surfaces

    public enum Surface { Panel, Stone, Wood, Velvet, Gold, Vault, Night, Leather, Crystal, Vellum, Smoke, Iron, Arcane }

    private const int TexSize = 256;
    private static readonly Dictionary<Surface, ImageTexture> _tex = new();

    /// <summary>A tileable texture of a surface (256 px, repeats seamlessly).</summary>
    public static Texture2D Tex(Surface s)
    {
        if (_tex.TryGetValue(s, out var t)) return t;
        var img = MakeSurface(s, TexSize);
        img.GenerateMipmaps();
        return _tex[s] = ImageTexture.CreateFromImage(img);
    }

    private static float Hash(int x, int y, int seed)
    {
        unchecked
        {
            uint h = (uint)(x * 374761393 + y * 668265263 + seed * 2147483647);
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xffffff) / (float)0xffffff;
        }
    }

    /// <summary>Value noise that wraps every <paramref name="period"/> cells (so the texture tiles).</summary>
    private static float VNoise(float x, float y, int period, int seed)
    {
        int xi = (int)MathF.Floor(x), yi = (int)MathF.Floor(y);
        float fx = x - xi, fy = y - yi;
        fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
        int M(int v) => ((v % period) + period) % period;
        float a = Hash(M(xi), M(yi), seed), b = Hash(M(xi + 1), M(yi), seed), c = Hash(M(xi), M(yi + 1), seed), d = Hash(M(xi + 1), M(yi + 1), seed);
        return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
    }

    /// <summary>Tileable fractal noise over a square of <paramref name="size"/> px: u, v in pixels, base cells across the square.</summary>
    private static float Fbm(float u, float v, int size, int cells, int octaves, int seed)
    {
        float s = 0, a = 0.5f, norm = 0;
        for (int o = 0; o < octaves; o++)
        {
            int c = cells << o;
            s += a * VNoise(u / size * c, v / size * c, c, seed + o * 31);
            norm += a; a *= 0.5f;
        }
        return s / norm;
    }

    /// <summary>Distance to the nearest of a tileable scatter of points (cells across), and the second nearest: for facets and dents.</summary>
    private static (float f1, float f2, int id) Cells(float u, float v, int size, int cells, int seed)
    {
        float x = u / size * cells, y = v / size * cells;
        int xi = (int)MathF.Floor(x), yi = (int)MathF.Floor(y);
        float f1 = 9, f2 = 9; int id = 0;
        for (int j = -1; j <= 1; j++)
            for (int i = -1; i <= 1; i++)
            {
                int cx = xi + i, cy = yi + j;
                int mx = ((cx % cells) + cells) % cells, my = ((cy % cells) + cells) % cells;
                float px = cx + Hash(mx, my, seed), py = cy + Hash(mx, my, seed + 7);
                float d = MathF.Sqrt((px - x) * (px - x) + (py - y) * (py - y));
                if (d < f1) { f2 = f1; f1 = d; id = mx * 131 + my; }
                else if (d < f2) f2 = d;
            }
        return (f1, f2, id);
    }

    private static Color Mix(Color a, Color b, float t) => a.Lerp(b, Math.Clamp(t, 0f, 1f));

    private static Image MakeSurface(Surface s, int n)
    {
        var img = Image.CreateEmpty(n, n, false, Image.Format.Rgba8);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
                img.SetPixel(x, y, SurfacePixel(s, x, y, n));
        return img;
    }

    private static Color SurfacePixel(Surface s, int x, int y, int n)
    {
        float f = Fbm(x, y, n, 4, 4, (int)s * 97 + 3);
        float fine = Fbm(x, y, n, 32, 2, (int)s * 53 + 11);
        switch (s)
        {
            case Surface.Panel:
            {
                // dark linen: a fine weave over a soft mottle
                float weave = 0.5f + 0.25f * MathF.Sin(x * MathF.Tau / 4f) * MathF.Sin(y * MathF.Tau / 4f);
                var c = Mix(new Color(0.06f, 0.055f, 0.07f), new Color(0.1f, 0.09f, 0.11f), f);
                return (c * (0.92f + 0.12f * weave + 0.08f * fine)) with { A = 1 };
            }
            case Surface.Stone:
            {
                var (f1, f2, _) = Cells(x, y, n, 6, 5);
                float crack = 1f - Mathf.SmoothStep(0f, 0.06f, f2 - f1);
                var c = Mix(new Color(0.1f, 0.11f, 0.13f), new Color(0.17f, 0.18f, 0.21f), f * 0.8f + fine * 0.3f);
                return (c * (1f - 0.45f * crack)) with { A = 1 };
            }
            case Surface.Wood:
            {
                // planks across, each its own shade, a grain that waves along them, dark seams between
                const int plank = 64;
                int row = y / plank;
                float inRow = (y % plank) / (float)plank;
                float shade = 0.85f + 0.3f * Hash(row % (n / plank), 0, 41);
                float grain = 0.5f + 0.5f * MathF.Sin((y + Fbm(x, y, n, 2, 3, 77 + row) * 40f) * 0.55f + x * 0.02f);
                var c = Mix(new Color(0.17f, 0.1f, 0.055f), new Color(0.27f, 0.17f, 0.09f), grain * 0.6f + fine * 0.4f) * shade;
                float seam = 1f - Mathf.SmoothStep(0f, 0.05f, MathF.Min(inRow, 1f - inRow));
                return (c * (1f - 0.6f * seam)) with { A = 1 };
            }
            case Surface.Velvet:
            {
                // deep crimson with a damask lattice and a soft sheen
                float u = x / (float)n * 4f, v = y / (float)n * 4f;
                float dx = MathF.Abs(u - MathF.Floor(u) - 0.5f), dy = MathF.Abs(v - MathF.Floor(v) - 0.5f);
                float diamond = dx + dy;
                float motif = Mathf.SmoothStep(0.34f, 0.3f, MathF.Abs(diamond - 0.32f)) * 0.5f + Mathf.SmoothStep(0.12f, 0.06f, diamond) * 0.6f;
                var c = Mix(new Color(0.16f, 0.025f, 0.05f), new Color(0.26f, 0.05f, 0.09f), f);
                return (c * (0.9f + 0.35f * motif + 0.1f * fine)) with { A = 1 };
            }
            case Surface.Gold:
            {
                // hammered metal: dents catching the light
                var (f1, _, id) = Cells(x, y, n, 10, 9);
                float dent = 1f - f1 * 1.4f;
                float tilt = Hash(id, 1, 5);
                var c = Mix(new Color(0.24f, 0.17f, 0.05f), new Color(0.42f, 0.31f, 0.1f), 0.4f + 0.4f * dent * tilt + 0.2f * f);
                return c with { A = 1 };
            }
            case Surface.Vault:
            {
                // dark ashlar blocks with veins of gold
                const int bw = 128, bh = 64;
                int row = y / bh;
                int xs = (x + (row % 2) * bw / 2) % n;
                float ex = MathF.Min(xs % bw, bw - xs % bw), ey = MathF.Min(y % bh, bh - y % bh);
                float joint = 1f - Mathf.SmoothStep(0f, 3f, MathF.Min(ex, ey));
                float vein = 1f - Mathf.SmoothStep(0f, 0.03f, MathF.Abs(Fbm(x, y, n, 3, 4, 909) - 0.5f));
                var c = Mix(new Color(0.07f, 0.07f, 0.08f), new Color(0.13f, 0.12f, 0.12f), f * 0.7f + fine * 0.3f);
                c = Mix(c, new Color(0.55f, 0.42f, 0.15f), vein * 0.55f);
                return (c * (1f - 0.55f * joint)) with { A = 1 };
            }
            case Surface.Night:
            {
                // a milestone's sky: deep blue, a faint nebula, scattered stars
                float neb = Fbm(x, y, n, 3, 5, 444);
                var c = Mix(new Color(0.025f, 0.03f, 0.08f), new Color(0.08f, 0.06f, 0.18f), Mathf.SmoothStep(0.45f, 0.8f, neb));
                float star = Hash(x, y, 1234);
                if (star > 0.9965f) c = Mix(c, new Color(0.95f, 0.92f, 0.8f), 0.6f + 0.4f * Hash(y, x, 5));
                else if (star > 0.992f) c = Mix(c, new Color(0.7f, 0.75f, 1f), 0.25f);
                return c with { A = 1 };
            }
            case Surface.Leather:
            {
                float pores = Fbm(x, y, n, 48, 2, 321);
                var c = Mix(new Color(0.13f, 0.08f, 0.05f), new Color(0.22f, 0.14f, 0.08f), f * 0.7f + pores * 0.3f);
                return (c * (0.9f + 0.15f * Mathf.SmoothStep(0.55f, 0.75f, pores))) with { A = 1 };
            }
            case Surface.Crystal:
            {
                var (f1, f2, id) = Cells(x, y, n, 5, 17);
                float facet = 0.4f + 0.6f * Hash(id, 3, 9);
                float edge = 1f - Mathf.SmoothStep(0f, 0.04f, f2 - f1);
                var c = Mix(new Color(0.03f, 0.1f, 0.11f), new Color(0.07f, 0.22f, 0.22f), facet * 0.7f + f * 0.3f);
                return Mix(c, new Color(0.4f, 0.9f, 0.85f), edge * 0.35f) with { A = 1 };
            }
            case Surface.Vellum:
            {
                // a green-tinged vellum, fibrous
                float fib = Fbm(x * 0.3f, y * 3f, n, 6, 3, 555);
                var c = Mix(new Color(0.07f, 0.1f, 0.06f), new Color(0.13f, 0.17f, 0.1f), f * 0.6f + fib * 0.4f);
                return c with { A = 1 };
            }
            case Surface.Smoke:
            {
                // a bargain's marble: violet with pale veins
                float warp = Fbm(x, y, n, 3, 4, 666);
                float vein = 1f - Mathf.SmoothStep(0f, 0.05f, MathF.Abs(MathF.Sin((x / (float)n + warp * 1.5f) * MathF.Tau * 2f)));
                var c = Mix(new Color(0.09f, 0.04f, 0.13f), new Color(0.17f, 0.08f, 0.22f), f);
                return Mix(c, new Color(0.6f, 0.45f, 0.75f), vein * 0.3f) with { A = 1 };
            }
            case Surface.Arcane:
            {
                // an upgrade's slate: deep indigo stone carved with faint glowing runes (a ring, its ticks, a stroke through it) on a grid
                const int cell = 64;
                int cx = x / cell, cy = y / cell, cells = n / cell;
                float lx = x % cell - cell / 2f + 0.5f, ly = y % cell - cell / 2f + 0.5f;
                float hsh = Hash(cx % cells, cy % cells, 77);
                var c = Mix(new Color(0.05f, 0.055f, 0.12f), new Color(0.1f, 0.09f, 0.2f), f * 0.8f + fine * 0.2f);
                float rr = MathF.Sqrt(lx * lx + ly * ly);
                float ringR = 13f + 6f * hsh;
                float ring = 1f - Mathf.SmoothStep(0.6f, 1.6f, MathF.Abs(rr - ringR));
                float ang = MathF.Atan2(ly, lx);
                // (a gap in the ring, and ticks round it)
                ring *= Mathf.SmoothStep(0.2f, 0.5f, MathF.Abs(MathF.Sin((ang + hsh * 6f) * 0.5f)));
                float tick = (1f - Mathf.SmoothStep(0.5f, 1.4f, MathF.Abs(rr - ringR - 5f) * 0.6f)) * Mathf.SmoothStep(0.85f, 0.97f, MathF.Cos(ang * (3 + (int)(hsh * 4)) + hsh * 9f));
                float sa = hsh * MathF.PI;
                float dl = MathF.Abs(-MathF.Sin(sa) * lx + MathF.Cos(sa) * ly);
                float stroke = (1f - Mathf.SmoothStep(0.5f, 1.3f, dl)) * (rr < ringR - 3f ? 1f : 0f);
                float glyph = Math.Clamp(ring + tick + stroke * 0.8f, 0f, 1f) * (0.35f + 0.65f * Hash(cy, cx, 5));
                return Mix(c, new Color(0.45f, 0.75f, 1f), glyph * 0.32f) with { A = 1 };
            }
            default: // Iron
            {
                float brush = Fbm(x * 4f, y * 0.25f, n, 4, 3, 888);
                var c = Mix(new Color(0.11f, 0.115f, 0.13f), new Color(0.2f, 0.205f, 0.23f), brush * 0.6f + f * 0.4f);
                return c with { A = 1 };
            }
        }
    }

    // ================================================================== shapes

    /// <summary>The outline of a rounded rectangle, clockwise (corners of radius <paramref name="rad"/>, <paramref name="seg"/> steps each).</summary>
    public static Vector2[] Rounded(Rect2 r, float rad, int seg = 8)
    {
        // (a rectangle too small to round is a plain one; and the radius stays a hair under half the short side, so neighbouring corners
        // never meet in the same point: duplicate points make the polygon fail to triangulate)
        r = new Rect2(r.Position, new Vector2(MathF.Max(r.Size.X, 0.5f), MathF.Max(r.Size.Y, 0.5f)));
        float half = MathF.Min(r.Size.X, r.Size.Y) * 0.5f;
        rad = Math.Clamp(rad, 0f, half - 0.15f);
        if (rad < 0.3f)
        {
            var sq = new[] { r.Position, new Vector2(r.End.X, r.Position.Y), r.End, new Vector2(r.Position.X, r.End.Y) };
            return Pad(sq, seg * 4 + 4);
        }
        var pts = new List<Vector2>(seg * 4 + 4);
        void Corner(Vector2 c, float a0)
        {
            for (int k = 0; k <= seg; k++)
            {
                float a = a0 + k / (float)seg * MathF.PI / 2f;
                pts.Add(c + new Vector2(MathF.Cos(a), MathF.Sin(a)) * rad);
            }
        }
        Corner(r.Position + new Vector2(r.Size.X - rad, rad), -MathF.PI / 2f);
        Corner(r.End - new Vector2(rad, rad), 0f);
        Corner(r.Position + new Vector2(rad, r.Size.Y - rad), MathF.PI / 2f);
        Corner(r.Position + new Vector2(rad, rad), MathF.PI);
        return pts.ToArray();
    }

    /// <summary>A plain rectangle's four corners spread over <paramref name="n"/> points along its edges (callers that pair two outlines point by point need the same count), no two the same.</summary>
    private static Vector2[] Pad(Vector2[] sq, int n)
    {
        var o = new Vector2[n];
        for (int k = 0; k < n; k++)
        {
            float t = k / (float)n * 4f;
            int e = (int)t;
            o[k] = sq[e % 4].Lerp(sq[(e + 1) % 4], t - e);
        }
        return o;
    }

    private static Vector2[] Closed(Vector2[] p) { var q = new Vector2[p.Length + 1]; p.CopyTo(q, 0); q[^1] = p[0]; return q; }

    /// <summary>A rounded panel filled with a surface (tinted), shaded darker toward its rim.</summary>
    public static void FillRounded(CanvasItem ci, Rect2 r, float rad, Surface s, Color tint, float texScale = 1f, Vector2? texOffset = null)
    {
        var pts = Rounded(r, rad);
        var uvs = new Vector2[pts.Length];
        var off = texOffset ?? r.Position;
        for (int k = 0; k < pts.Length; k++) uvs[k] = (pts[k] - off) / (TexSize * texScale);
        var cols = new Color[pts.Length];
        Array.Fill(cols, tint);
        ci.DrawPolygon(pts, cols, uvs, Tex(s));
        // the rim falls into shadow: a ring of darkening just inside the edge
        var inner = Rounded(r.Grow(-Math.Min(18f, Math.Min(r.Size.X, r.Size.Y) * 0.2f)), Math.Max(2f, rad - 8f));
        if (inner.Length == pts.Length)
        {
            for (int k = 0; k < pts.Length; k++)
            {
                int k2 = (k + 1) % pts.Length;
                ci.DrawPolygon(new[] { pts[k], pts[k2], inner[k2], inner[k] },
                    new[] { new Color(0, 0, 0, 0.42f), new Color(0, 0, 0, 0.42f), new Color(0, 0, 0, 0f), new Color(0, 0, 0, 0f) });
            }
        }
    }

    /// <summary>A picture drawn into a rounded rectangle (its corners clipped round).</summary>
    public static void RoundedTexture(CanvasItem ci, Texture2D tex, Rect2 r, float rad, Color mod)
    {
        var pts = Rounded(r, rad, 6);
        var uvs = pts.Select(p => (p - r.Position) / r.Size).ToArray();
        var cols = new Color[pts.Length];
        Array.Fill(cols, mod);
        ci.DrawPolygon(pts, cols, uvs, tex);
    }

    /// <summary>A soft shadow under a rounded panel.</summary>
    public static void Shadow(CanvasItem ci, Rect2 r, float rad, float alpha = 0.45f)
    {
        for (int k = 0; k < 4; k++)
        {
            var rr = new Rect2(r.Position + new Vector2(2, 5), r.Size).Grow(k * 3f);
            ci.DrawColoredPolygon(Rounded(rr, rad + k * 3f), new Color(0, 0, 0, alpha * (0.4f - k * 0.08f)));
        }
    }

    public static void Outline(CanvasItem ci, Rect2 r, float rad, Color col, float width) =>
        ci.DrawPolyline(Closed(Rounded(r, rad)), col, width, true);

    /// <summary>
    /// A gauge: a rounded, sunken trough, filled to <paramref name="frac"/> with a rounded bar that has a light along its top and a darker
    /// belly, in a thin rim. Optionally a second, paler fill behind (what was just lost).
    /// </summary>
    public static void Bar(CanvasItem ci, Rect2 r, float frac, Color fill, Color? rim = null, float ghost = -1f)
    {
        float rad = r.Size.Y / 2f;
        ci.DrawColoredPolygon(Rounded(r.Grow(2.5f), rad + 2.5f, 6), new Color(0, 0, 0, 0.6f));
        ci.DrawColoredPolygon(Rounded(r, rad, 6), new Color(fill.Darkened(0.82f), 0.95f));
        // the trough's shade along its top
        ci.DrawColoredPolygon(Rounded(new Rect2(r.Position, new Vector2(r.Size.X, r.Size.Y * 0.4f)), MathF.Min(rad, r.Size.Y * 0.2f), 4), new Color(0, 0, 0, 0.3f));
        if (ghost > frac)
        {
            float gw = r.Size.X * Math.Clamp(ghost, 0f, 1f);
            if (gw > 2f) ci.DrawColoredPolygon(Rounded(new Rect2(r.Position, new Vector2(gw, r.Size.Y)), MathF.Min(rad, gw / 2f), 6), new Color(1f, 0.95f, 0.85f, 0.45f));
        }
        float w = r.Size.X * Math.Clamp(frac, 0f, 1f);
        if (w > 1.5f)
        {
            var fr = new Rect2(r.Position, new Vector2(w, r.Size.Y));
            float fr0 = MathF.Min(rad, w / 2f);
            var pts = Rounded(fr, fr0, 6);
            // lighter at the top, darker at the bottom
            ci.DrawPolygon(pts, pts.Select(p => (p.Y < r.Position.Y + r.Size.Y * 0.5f ? fill.Lightened(0.15f) : fill.Darkened(0.25f))).ToArray());
            ci.DrawColoredPolygon(Rounded(new Rect2(fr.Position + new Vector2(2, 1.2f), new Vector2(MathF.Max(0f, w - 4), r.Size.Y * 0.32f)), MathF.Min(r.Size.Y * 0.16f, MathF.Max(0.5f, (w - 4) / 2f)), 4), new Color(1, 1, 1, 0.22f));
        }
        Outline(ci, r.Grow(1f), rad + 1f, rim ?? new Color(0.62f, 0.52f, 0.36f, 0.85f), 1.2f);
    }

    // ================================================================== rarity and frames

    /// <summary>How rare a card is, as its frame shows it.</summary>
    public enum Rarity { Common, Uncommon, Rare, Relic, Bargain, Plain }

    public static Rarity RarityOf(Upgrade u) =>
        u.Icon == "skip" ? Rarity.Plain :
        u.Kind == UpgradeKind.Relic ? Rarity.Relic :
        u.Kind == UpgradeKind.RiskReward ? Rarity.Bargain :
        u.Kind == UpgradeKind.Alteration || u.Tier == UpgradeTier.Rare ? Rarity.Rare :
        u.Tier == UpgradeTier.Ability || u.Kind is UpgradeKind.Class or UpgradeKind.Technique ? Rarity.Uncommon : Rarity.Common;

    public static string RarityName(Rarity r) => r switch
    {
        Rarity.Uncommon => "UNCOMMON", Rarity.Rare => "RARE", Rarity.Relic => "RELIC", Rarity.Bargain => "BARGAIN", Rarity.Plain => "", _ => "COMMON",
    };

    /// <summary>The metal of a rarity's frame: iron, bronze, gold, rose-gold with gems, a bargain's violet.</summary>
    public static Color FrameColor(Rarity r) => r switch
    {
        Rarity.Uncommon => new Color(0.78f, 0.55f, 0.32f),
        Rarity.Rare => new Color(1f, 0.82f, 0.4f),
        Rarity.Relic => new Color(1f, 0.6f, 0.62f),
        Rarity.Bargain => new Color(0.78f, 0.5f, 1f),
        Rarity.Plain => new Color(0.5f, 0.48f, 0.46f),
        _ => new Color(0.62f, 0.64f, 0.7f),
    };

    /// <summary>
    /// A frame round a rounded panel, ornamented by rarity: a plain band (common); a band with an inner line and studs at the corners
    /// (uncommon); a double band with filigree curls at the corners and a diamond at the top (rare); a double band, a gem at each corner,
    /// a crest and a breathing glow (relic); thorns at the corners (a bargain).
    /// </summary>
    public static void Frame(CanvasItem ci, Rect2 r, float rad, Rarity rar, float t, bool bright = false, Color? gemColor = null)
    {
        var c = FrameColor(rar);
        if (!bright) c = c.Darkened(0.18f);
        float w = rar switch { Rarity.Common or Rarity.Plain => 2f, Rarity.Uncommon => 2.5f, _ => 3f } + (bright ? 0.8f : 0f);
        if (rar == Rarity.Relic || bright)
        {
            // a glow outside the frame
            float g = rar == Rarity.Relic ? 0.22f + 0.1f * MathF.Sin(t * 2.4f) : 0.18f;
            for (int k = 1; k <= 4; k++) Outline(ci, r.Grow(k * 2.2f), rad + k * 2.2f, new Color(c, g * (1f - k / 5f)), 2.2f);
        }
        Outline(ci, r, rad, new Color(0, 0, 0, 0.6f), w + 2.5f);
        Outline(ci, r, rad, c, w);
        // a highlight along the top of the band
        ci.DrawPolyline(Rounded(r, rad).Where(p => p.Y < r.Position.Y + rad * 0.7f).ToArray(), new Color(1, 1, 1, 0.18f), 1f, true);
        if (rar is Rarity.Uncommon or Rarity.Rare or Rarity.Relic)
            Outline(ci, r.Grow(-6f), Math.Max(2f, rad - 5f), new Color(c, rar == Rarity.Uncommon ? 0.35f : 0.55f), 1.2f);
        if (rar is Rarity.Rare or Rarity.Relic)
            Outline(ci, r.Grow(3.5f), rad + 3.5f, new Color(c, 0.6f), 1.2f);
        var corners = new[] { r.Position, new Vector2(r.End.X, r.Position.Y), r.End, new Vector2(r.Position.X, r.End.Y) };
        var inward = new[] { new Vector2(1, 1), new Vector2(-1, 1), new Vector2(-1, -1), new Vector2(1, -1) };
        for (int k = 0; k < 4; k++)
        {
            var at = corners[k] + inward[k] * rad * 0.42f;
            switch (rar)
            {
                case Rarity.Uncommon:
                    ci.DrawCircle(at, 3.2f, new Color(0, 0, 0, 0.6f));
                    ci.DrawCircle(at, 2.4f, c);
                    ci.DrawCircle(at - new Vector2(0.7f, 0.7f), 0.9f, new Color(1, 1, 1, 0.6f));
                    break;
                case Rarity.Rare:
                    Filigree(ci, corners[k], inward[k], rad, c);
                    break;
                case Rarity.Relic:
                    Filigree(ci, corners[k], inward[k], rad, c);
                    Gem(ci, at, 5.2f, gemColor ?? new Color(0.95f, 0.25f, 0.4f), t + k);
                    break;
                case Rarity.Bargain:
                {
                    // two thorns at each corner, pointing out
                    var side = new Vector2(-inward[k].Y, inward[k].X);
                    foreach (float sd in new[] { -1f, 1f })
                    {
                        var b = corners[k] + inward[k] * rad * 0.32f + side * sd * rad * 0.42f;
                        var tip = b - inward[k] * 9f + side * sd * 3f;
                        ci.DrawColoredPolygon(new[] { b - side * 3f, b + side * 3f, tip }, c);
                    }
                    break;
                }
            }
        }
        if (rar is Rarity.Rare or Rarity.Relic)
        {
            // the crest at the top: a diamond (rare) or a gem in a mount (relic)
            var top = new Vector2(r.Position.X + r.Size.X / 2, r.Position.Y);
            float d = rar == Rarity.Relic ? 9f : 6f;
            ci.DrawColoredPolygon(new[] { top + new Vector2(0, -d), top + new Vector2(d, 0), top + new Vector2(0, d), top + new Vector2(-d, 0) }, new Color(0, 0, 0, 0.7f));
            ci.DrawColoredPolygon(new[] { top + new Vector2(0, -d + 1.5f), top + new Vector2(d - 1.5f, 0), top + new Vector2(0, d - 1.5f), top + new Vector2(-d + 1.5f, 0) }, c);
            if (rar == Rarity.Relic) Gem(ci, top, 4f, gemColor ?? new Color(0.95f, 0.25f, 0.4f), t * 1.3f);
        }
    }

    /// <summary>Two curls of metal in a corner.</summary>
    private static void Filigree(CanvasItem ci, Vector2 corner, Vector2 inward, float rad, Color c)
    {
        var o = corner + inward * rad * 0.55f;
        float a0 = MathF.Atan2(-inward.Y, -inward.X);
        ci.DrawArc(o + new Vector2(inward.X * rad * 0.75f, 0), rad * 0.32f, a0 - 1.4f, a0 + 1.6f, 10, new Color(c, 0.85f), 1.4f, true);
        ci.DrawArc(o + new Vector2(0, inward.Y * rad * 0.75f), rad * 0.32f, a0 - 1.6f, a0 + 1.4f, 10, new Color(c, 0.85f), 1.4f, true);
        ci.DrawCircle(o, 2f, c);
    }

    /// <summary>A cut gem: a dark mount, the stone, a moving glint.</summary>
    public static void Gem(CanvasItem ci, Vector2 at, float r, Color col, float t)
    {
        ci.DrawCircle(at, r + 1.6f, new Color(0, 0, 0, 0.7f));
        ci.DrawCircle(at, r, col.Darkened(0.25f));
        ci.DrawCircle(at - new Vector2(r * 0.2f, r * 0.2f), r * 0.65f, col);
        float g = 0.5f + 0.5f * MathF.Sin(t * 2.2f);
        ci.DrawCircle(at - new Vector2(r * 0.35f, r * 0.4f), r * 0.28f, new Color(1, 1, 1, 0.5f + 0.4f * g));
    }

    // ================================================================== what a card is for

    /// <summary>What an upgrade is chiefly for.</summary>
    public enum Role { Offense, Survival, Mobility, Bargain, None }

    public static Role RoleOf(Upgrade u) => u.Icon switch
    {
        "blade" or "spell" or "charge" or "throw" => Role.Offense,
        "shield" or "life" => Role.Survival,
        "move" or "dodge" => Role.Mobility,
        "risk" => Role.Bargain,
        "skip" => Role.None,
        _ => u.Kind == UpgradeKind.Relic ? Role.None : Role.Offense,
    };

    public static Color RoleColor(Role r) => r switch
    {
        Role.Offense => new Color(1f, 0.5f, 0.35f),
        Role.Survival => new Color(0.45f, 0.9f, 0.6f),
        Role.Mobility => new Color(0.5f, 0.8f, 1f),
        Role.Bargain => new Color(0.82f, 0.55f, 1f),
        _ => new Color(0.75f, 0.75f, 0.8f),
    };

    public static string RoleName(Role r) => r switch { Role.Offense => "OFFENSE", Role.Survival => "SURVIVAL", Role.Mobility => "MOBILITY", Role.Bargain => "BARGAIN", _ => "" };

    /// <summary>A small symbol for a role: crossed blades, a shield, a wing (two chevrons), a pair of scales.</summary>
    public static void RoleGlyph(CanvasItem ci, Vector2 c, float s, Role r, Color col)
    {
        switch (r)
        {
            case Role.Offense:
                ci.DrawLine(c + new Vector2(-s, s), c + new Vector2(s, -s), col, 2f, true);
                ci.DrawLine(c + new Vector2(s, s), c + new Vector2(-s, -s), col, 2f, true);
                ci.DrawLine(c + new Vector2(-s * 0.9f, s * 0.35f), c + new Vector2(-s * 0.35f, s * 0.9f), col, 2f, true);
                ci.DrawLine(c + new Vector2(s * 0.9f, s * 0.35f), c + new Vector2(s * 0.35f, s * 0.9f), col, 2f, true);
                break;
            case Role.Survival:
                ci.DrawColoredPolygon(new[] { c + new Vector2(-s, -s), c + new Vector2(s, -s), c + new Vector2(s, s * 0.1f), c + new Vector2(0, s * 1.1f), c + new Vector2(-s, s * 0.1f) }, col);
                ci.DrawLine(c + new Vector2(0, -s * 0.7f), c + new Vector2(0, s * 0.7f), new Color(0, 0, 0, 0.5f), 1.5f);
                break;
            case Role.Mobility:
                for (int k = 0; k < 2; k++)
                {
                    float dx = -s * 0.7f + k * s * 0.8f;
                    ci.DrawPolyline(new[] { c + new Vector2(dx, -s), c + new Vector2(dx + s * 0.7f, 0), c + new Vector2(dx, s) }, col, 2f, true);
                }
                break;
            case Role.Bargain:
                ci.DrawLine(c + new Vector2(0, -s), c + new Vector2(0, s), col, 1.6f, true);
                ci.DrawLine(c + new Vector2(-s, -s * 0.5f), c + new Vector2(s, -s * 0.5f), col, 1.6f, true);
                ci.DrawArc(c + new Vector2(-s, 0), s * 0.45f, 0, MathF.PI, 6, col, 1.4f, true);
                ci.DrawArc(c + new Vector2(s, 0), s * 0.45f, 0, MathF.PI, 6, col, 1.4f, true);
                break;
        }
    }

    /// <summary>A hero's emblem (or, for a card anyone can take, a compass rose): a sword, a kite shield, a drop of life, a flame, a dagger, a ward, a paw, a cog.</summary>
    public static void HeroEmblem(CanvasItem ci, Vector2 c, float s, HeroKind? hero, Color col)
    {
        switch (hero)
        {
            case null:
                for (int k = 0; k < 4; k++)
                {
                    var d = Vector2.Up.Rotated(k * MathF.PI / 2f);
                    var side = d.Rotated(MathF.PI / 2f);
                    ci.DrawColoredPolygon(new[] { c + d * s, c + side * s * 0.28f, c - side * s * 0.28f }, k % 2 == 0 ? col : col.Darkened(0.3f));
                }
                ci.DrawCircle(c, s * 0.2f, col);
                break;
            case HeroKind.Swordsman:
                ci.DrawLine(c + new Vector2(0, s), c + new Vector2(0, -s), col, 2.2f, true);
                ci.DrawLine(c + new Vector2(-s * 0.55f, s * 0.45f), c + new Vector2(s * 0.55f, s * 0.45f), col, 2.2f, true);
                break;
            case HeroKind.Warden:
                ci.DrawColoredPolygon(new[] { c + new Vector2(-s * 0.8f, -s), c + new Vector2(s * 0.8f, -s), c + new Vector2(s * 0.8f, 0), c + new Vector2(0, s), c + new Vector2(-s * 0.8f, 0) }, col);
                break;
            case HeroKind.Vitalist:
                ci.DrawCircle(c + new Vector2(0, s * 0.3f), s * 0.6f, col);
                ci.DrawColoredPolygon(new[] { c + new Vector2(0, -s), c + new Vector2(s * 0.55f, s * 0.15f), c + new Vector2(-s * 0.55f, s * 0.15f) }, col);
                break;
            case HeroKind.Elementalist:
                ci.DrawColoredPolygon(new[] { c + new Vector2(0, -s), c + new Vector2(s * 0.6f, s * 0.2f), c + new Vector2(s * 0.3f, s), c + new Vector2(-s * 0.3f, s), c + new Vector2(-s * 0.6f, s * 0.2f) }, col);
                ci.DrawColoredPolygon(new[] { c + new Vector2(0, -s * 0.1f), c + new Vector2(s * 0.3f, s * 0.5f), c + new Vector2(0, s * 0.85f), c + new Vector2(-s * 0.3f, s * 0.5f) }, col.Darkened(0.45f));
                break;
            case HeroKind.Rogue:
                ci.DrawColoredPolygon(new[] { c + new Vector2(0, -s), c + new Vector2(s * 0.25f, s * 0.3f), c + new Vector2(-s * 0.25f, s * 0.3f) }, col);
                ci.DrawLine(c + new Vector2(-s * 0.45f, s * 0.35f), c + new Vector2(s * 0.45f, s * 0.35f), col, 2f, true);
                ci.DrawLine(c + new Vector2(0, s * 0.35f), c + new Vector2(0, s), col, 2.4f, true);
                break;
            case HeroKind.Aegis:
                ci.DrawArc(c, s * 0.85f, 0, MathF.Tau, 16, col, 2f, true);
                ci.DrawArc(c, s * 0.4f, 0, MathF.Tau, 12, col, 2f, true);
                break;
            case HeroKind.ShapeShifter:
                ci.DrawCircle(c + new Vector2(0, s * 0.35f), s * 0.45f, col);
                for (int k = -1; k <= 1; k++) ci.DrawCircle(c + new Vector2(k * s * 0.5f, -s * 0.35f - (k == 0 ? s * 0.15f : 0)), s * 0.2f, col);
                break;
            case HeroKind.Automaton:
                for (int k = 0; k < 8; k++)
                {
                    var d = Vector2.Up.Rotated(k * MathF.Tau / 8f);
                    ci.DrawLine(c + d * s * 0.6f, c + d * s, col, 2.4f, true);
                }
                ci.DrawArc(c, s * 0.55f, 0, MathF.Tau, 14, col, 2.4f, true);
                break;
        }
    }

    /// <summary>A chain link: the card rules out (or is ruled out by) another.</summary>
    public static void ChainGlyph(CanvasItem ci, Vector2 c, float s, Color col)
    {
        ci.DrawArc(c + new Vector2(-s * 0.45f, 0), s * 0.55f, 0, MathF.Tau, 14, col, 1.8f, true);
        ci.DrawArc(c + new Vector2(s * 0.45f, 0), s * 0.55f, 0, MathF.Tau, 14, col, 1.8f, true);
        ci.DrawLine(c + new Vector2(-s * 1.1f, s * 1.1f), c + new Vector2(s * 1.1f, -s * 1.1f), new Color(1f, 0.45f, 0.4f), 1.8f, true);
    }

    /// <summary>The upgrades a card can't be had with: those it names, and those that name it.</summary>
    public static IEnumerable<Upgrade> ExclusiveWith(Upgrade u)
    {
        foreach (var id in u.Excludes) { var x = Upgrades.Find(id); if (x != null) yield return x; }
        foreach (var o in Upgrades.Chest.Concat(Upgrades.Relics))
            if (o != u && o.Excludes.Contains(u.Id) && !u.Excludes.Contains(o.Id)) yield return o;
    }

    /// <summary>The surface a card is printed on, by its kind.</summary>
    public static Surface CardSurface(Upgrade u) => u.Icon == "skip" ? Surface.Iron : u.Kind switch
    {
        UpgradeKind.Class => Surface.Leather,
        UpgradeKind.Alteration => Surface.Crystal,
        UpgradeKind.Conditional => Surface.Vellum,
        UpgradeKind.RiskReward => Surface.Smoke,
        UpgradeKind.Relic => Surface.Velvet,
        UpgradeKind.Technique => Surface.Night,
        _ => Surface.Stone,
    };

    // ================================================================== the theme's boxes (panels and buttons)

    /// <summary>
    /// A nine-patch box for the theme: a rounded frame of <paramref name="border"/> (with studs at the corners if <paramref name="studs"/>)
    /// round a tiled surface. The middle and the edges tile seamlessly.
    /// </summary>
    public static StyleBoxTexture Box(Surface s, Color tint, Color border, int radius, float borderW, bool studs, int padX, int padY, float alpha = 1f, float sheen = 0f)
    {
        // (a sheen: lighter at the top, darker at the foot; such a box stretches rather than tiles, so the light stays one light)
        int m = Math.Max(12, radius + 4), p = 128;
        int n = p + 2 * m;
        var surf = MakeSurface(s, p);
        var img = Image.CreateEmpty(n, n, false, Image.Format.Rgba8);
        var rect = new Rect2(1.5f, 1.5f, n - 3f, n - 3f);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                // the signed distance to the rounded rectangle (negative inside)
                float px = x + 0.5f, py = y + 0.5f;
                float qx = MathF.Abs(px - n / 2f) - (rect.Size.X / 2f - radius), qy = MathF.Abs(py - n / 2f) - (rect.Size.Y / 2f - radius);
                float d = MathF.Sqrt(MathF.Max(qx, 0) * MathF.Max(qx, 0) + MathF.Max(qy, 0) * MathF.Max(qy, 0)) + MathF.Min(MathF.Max(qx, qy), 0) - radius;
                if (d > 0.8f) { img.SetPixel(x, y, new Color(0, 0, 0, 0)); continue; }
                var c = surf.GetPixel(((x - m) % p + p) % p, ((y - m) % p + p) % p) * tint;
                // darker toward the rim, and the band of the frame
                float rim = Mathf.SmoothStep(-14f, 0f, d);
                c = c.Lerp(new Color(0, 0, 0), rim * 0.35f);
                float band = Mathf.SmoothStep(-borderW - 1f, -borderW, d) * (1f - Mathf.SmoothStep(-0.6f, 0.4f, d));
                c = c.Lerp(border, band);
                float hi = Mathf.SmoothStep(-borderW - 2.5f, -borderW - 1.5f, d) * (1f - Mathf.SmoothStep(-borderW - 1.5f, -borderW - 0.8f, d));
                c = c.Lerp(new Color(1, 1, 1), hi * 0.08f);
                if (sheen > 0) { float v = y / (float)n; c = c.Lightened(sheen * 0.16f * (1f - v)).Darkened(sheen * 0.22f * v); }
                float a = (1f - Mathf.SmoothStep(-0.4f, 0.8f, d)) * alpha;
                img.SetPixel(x, y, new Color(c.R, c.G, c.B, a));
            }
        if (studs)
        {
            foreach (var (cx, cy) in new[] { (radius * 0.75f, radius * 0.75f), (n - radius * 0.75f, radius * 0.75f), (radius * 0.75f, n - radius * 0.75f), (n - radius * 0.75f, n - radius * 0.75f) })
                for (int y = (int)cy - 4; y <= (int)cy + 4; y++)
                    for (int x = (int)cx - 4; x <= (int)cx + 4; x++)
                    {
                        float dd = MathF.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy));
                        if (dd > 3.2f) continue;
                        var c = img.GetPixel(x, y).Lerp(border.Lightened(dd < 1.4f ? 0.4f : 0f), 1f - Mathf.SmoothStep(2.4f, 3.2f, dd));
                        img.SetPixel(x, y, c with { A = Math.Max(img.GetPixel(x, y).A, alpha) });
                    }
        }
        return new StyleBoxTexture
        {
            Texture = ImageTexture.CreateFromImage(img),
            TextureMarginLeft = m, TextureMarginRight = m, TextureMarginTop = m, TextureMarginBottom = m,
            AxisStretchHorizontal = sheen > 0 ? StyleBoxTexture.AxisStretchMode.Stretch : StyleBoxTexture.AxisStretchMode.Tile,
            AxisStretchVertical = sheen > 0 ? StyleBoxTexture.AxisStretchMode.Stretch : StyleBoxTexture.AxisStretchMode.Tile,
            ContentMarginLeft = padX, ContentMarginRight = padX, ContentMarginTop = padY, ContentMarginBottom = padY,
        };
    }
}
