using System;
using System.Collections.Generic;
using Godot;

namespace DaggerCave;

/// <summary>
/// Turns a <see cref="CaveData"/> field into visible, collidable terrain via marching squares.
/// Each 32x32-square chunk gets a vertex-colored triangle mesh (rock), a StaticBody2D with the
/// iso-line segments as a ConcavePolygonShape2D (collision), and a rim/decoration layer (moss on
/// floors, stalactites on ceilings, the odd glowing crystal or mushroom).
/// </summary>
public partial class CaveView : Node2D
{
    private const int Chunk = 32;

    /// <summary>The level's colours (from its biome).</summary>
    public static (Color edge, Color deep, Color moss, Color rim, Color glow) Palette(CaveData cave)
        => (cave.Biome ?? Biomes.Get(BiomeId.Slime)).Palette;

    public void Build(CaveData cave)
    {
        int cw = (cave.W + Chunk - 1) / Chunk, chh = (cave.H + Chunk - 1) / Chunk;
        var pal = Palette(cave);
        for (int cy = 0; cy < chh; cy++)
            for (int cx = 0; cx < cw; cx++)
                BuildChunk(cave, cx, cy, pal);
    }

    private static uint Hash(int x, int y, int s)
    {
        unchecked
        {
            uint h = (uint)(x * 374761393 + y * 668265263 + s * 1442695041);
            h = (h ^ (h >> 13)) * 1274126177u;
            return h ^ (h >> 16);
        }
    }

    private static float Hash01(int x, int y, int s) => (Hash(x, y, s) & 0xFFFFFF) / (float)0x1000000;

    private void BuildChunk(CaveData cave, int cx, int cy, (Color edge, Color deep, Color moss, Color rim, Color glow) pal)
    {
        float cell = CaveData.Cell;
        var verts = new List<Vector2>(4096);
        var cols = new List<Color>(4096);
        var segs = new List<Vector2>(512);
        var normals = new List<Vector2>(256);

        Color RockColor(Vector2 p)
        {
            float d = SampleDepth(cave, p.X / cell, p.Y / cell);
            float t = Math.Clamp(d / 5f, 0f, 1f);
            var c = pal.edge.Lerp(pal.deep, t * t * (3 - 2 * t));
            float n = Hash01((int)(p.X / 24), (int)(p.Y / 24), cave.Seed) * 0.12f - 0.06f;
            return new Color(c.R + n, c.G + n, c.B + n);
        }

        Span<Vector2> poly = stackalloc Vector2[8];
        Span<bool> isEdge = stackalloc bool[8];
        Span<Vector2> cp = stackalloc Vector2[4];
        Span<float> cv = stackalloc float[4];

        int iEnd = Math.Min(cave.W, (cx + 1) * Chunk), jEnd = Math.Min(cave.H, (cy + 1) * Chunk);
        for (int j = cy * Chunk; j < jEnd; j++)
            for (int i = cx * Chunk; i < iEnd; i++)
            {
                cv[0] = cave.Corner(i, j); cv[1] = cave.Corner(i + 1, j); cv[2] = cave.Corner(i + 1, j + 1); cv[3] = cave.Corner(i, j + 1);
                bool s0 = cv[0] < 0.5f, s1 = cv[1] < 0.5f, s2 = cv[2] < 0.5f, s3 = cv[3] < 0.5f;
                if (!s0 && !s1 && !s2 && !s3) continue;
                cp[0] = new Vector2(i, j) * cell; cp[1] = new Vector2(i + 1, j) * cell; cp[2] = new Vector2(i + 1, j + 1) * cell; cp[3] = new Vector2(i, j + 1) * cell;

                int n = 0;
                for (int k = 0; k < 4; k++)
                {
                    int k2 = (k + 1) & 3;
                    bool sa = cv[k] < 0.5f, sb = cv[k2] < 0.5f;
                    if (sa) { poly[n] = cp[k]; isEdge[n] = false; n++; }
                    if (sa != sb)
                    {
                        float t = (0.5f - cv[k]) / (cv[k2] - cv[k]);
                        poly[n] = cp[k].Lerp(cp[k2], Math.Clamp(t, 0.02f, 0.98f)); isEdge[n] = true; n++;
                    }
                }
                for (int k = 1; k < n - 1; k++)
                {
                    verts.Add(poly[0]); verts.Add(poly[k]); verts.Add(poly[k + 1]);
                    cols.Add(RockColor(poly[0])); cols.Add(RockColor(poly[k])); cols.Add(RockColor(poly[k + 1]));
                }
                for (int k = 0; k < n; k++)
                {
                    int k2 = (k + 1) % n;
                    if (isEdge[k] && isEdge[k2])
                    {
                        Vector2 a = poly[k], b = poly[k2];
                        if (a.DistanceSquaredTo(b) < 0.01f) continue;
                        segs.Add(a); segs.Add(b);
                        var m = (a + b) * 0.5f;
                        var nrm = new Vector2(-(b - a).Y, (b - a).X).Normalized();
                        if (cave.Sample(m + nrm * 3) < cave.Sample(m - nrm * 3)) nrm = -nrm;
                        normals.Add(nrm);
                    }
                }
            }

        if (verts.Count > 0)
        {
            var arrays = new Godot.Collections.Array();
            arrays.Resize((int)Mesh.ArrayType.Max);
            arrays[(int)Mesh.ArrayType.Vertex] = verts.ToArray();
            arrays[(int)Mesh.ArrayType.Color] = cols.ToArray();
            var mesh = new ArrayMesh();
            mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
            AddChild(new MeshInstance2D { Mesh = mesh, ZIndex = 10 });
            if (cave.Biome != null && cave.Biome.Bricks) AddChild(new BrickLayer { ZIndex = 10, Cave = cave, Cx = cx, Cy = cy, Col = pal.deep.Lerp(pal.edge, 0.2f) });
        }

        if (segs.Count > 0)
        {
            var body = new StaticBody2D { CollisionLayer = G.LayerTerrain, CollisionMask = 0 };
            body.AddChild(new CollisionShape2D { Shape = new ConcavePolygonShape2D { Segments = segs.ToArray() } });
            AddChild(body);
            var rim = new RimLayer { ZIndex = 11 };
            rim.Setup(cave, segs, normals, pal);
            AddChild(rim);
        }
    }

    private static float SampleDepth(CaveData cave, float x, float y)
    {
        int i = Math.Clamp((int)MathF.Round(x), 0, cave.W), j = Math.Clamp((int)MathF.Round(y), 0, cave.H);
        return cave.RockDepth[j * (cave.W + 1) + i];
    }

    /// <summary>Masonry lines on the rock near the open space (the ruins).</summary>
    private partial class BrickLayer : Node2D
    {
        public CaveData Cave;
        public int Cx, Cy;
        public Color Col;

        public override void _Draw()
        {
            float cell = CaveData.Cell;
            int iEnd = Math.Min(Cave.W, (Cx + 1) * Chunk), jEnd = Math.Min(Cave.H, (Cy + 1) * Chunk);
            var col = new Color(Col, 0.8f);
            var hi = new Color(1, 1, 1, 0.05f);
            for (int j = Cy * Chunk; j < jEnd; j++)
                for (int i = Cx * Chunk; i < iEnd; i++)
                {
                    // solid cells within a few cells of the open space
                    if (Cave.Corner(i, j) >= 0.5f || Cave.Corner(i + 1, j) >= 0.5f || Cave.Corner(i, j + 1) >= 0.5f || Cave.Corner(i + 1, j + 1) >= 0.5f) continue;
                    if (Cave.RockDepth[j * (Cave.W + 1) + i] > 4) continue;
                    // two courses of bricks per cell, offset like a running bond
                    for (int course = 0; course < 2; course++)
                    {
                        float y = j * cell + course * cell * 0.5f;
                        DrawLine(new Vector2(i * cell, y), new Vector2((i + 1) * cell, y), col, 1f);
                        DrawLine(new Vector2(i * cell, y + 1), new Vector2((i + 1) * cell, y + 1), hi, 1f);
                        float x = i * cell + (((j * 2 + course) % 2 == 0) ? 0 : cell * 0.5f);
                        if (x < (i + 1) * cell) DrawLine(new Vector2(x, y), new Vector2(x, y + cell * 0.5f), col, 1f);
                    }
                }
        }
    }

    /// <summary>Rim highlights and surface decorations for one chunk; drawn once and cached by the canvas.</summary>
    private partial class RimLayer : Node2D
    {
        private Vector2[] _rimPts;
        private Color[] _rimCols;
        private readonly List<(Vector2[] pts, Color col)> _polys = new();
        private readonly List<(Vector2 a, Vector2 b, Color col, float w)> _lines = new();
        private readonly List<(Vector2 p, float r, Color col)> _glows = new();

        public void Setup(CaveData cave, List<Vector2> segs, List<Vector2> normals, (Color edge, Color deep, Color moss, Color rim, Color glow) pal)
        {
            _rimPts = segs.ToArray();
            _rimCols = new Color[segs.Count / 2];
            for (int k = 0; k < normals.Count; k++)
            {
                var nrm = normals[k];
                Vector2 a = segs[2 * k], b = segs[2 * k + 1];
                var m = (a + b) * 0.5f;
                bool underwater = m.Y > cave.WaterY;
                float up = -nrm.Y;
                // moss and grass grow exactly where the ground is walkable, so green = you can walk it
                float walkable = MathF.Cos(Mathf.DegToRad(Tune.Cave.WalkableSlopeDegrees)) - 0.01f;
                _rimCols[k] = up > walkable && !underwater ? pal.moss : pal.rim * (underwater ? 0.8f : 1f);
                _rimCols[k].A = 1;
                int hx = (int)(m.X / 5), hy = (int)(m.Y / 5);
                float r = Hash01(hx, hy, cave.Seed + 3);
                var bd = cave.Biome ?? Biomes.Get(BiomeId.Slime);
                if (up > walkable && !underwater)
                {
                    if (r < bd.Grass)
                    {
                        // grass tuft
                        for (int g = 0; g < 3; g++)
                        {
                            float off = (g - 1) * 3f;
                            var root = m + (b - a).Normalized() * off;
                            float h = 4 + Hash01(hx + g, hy, cave.Seed) * 6;
                            _lines.Add((root, root + new Vector2((g - 1) * 1.5f, -h), pal.moss.Lightened(0.15f), 1.3f));
                        }
                    }
                    else if (r < bd.Grass + bd.Mushrooms)
                    {
                        // glowing mushroom
                        var stem = m + new Vector2(0, -5);
                        _lines.Add((m, stem, new Color(0.85f, 0.82f, 0.7f), 2f));
                        _polys.Add((new[] { stem + new Vector2(-4, 1), stem + new Vector2(0, -3), stem + new Vector2(4, 1) }, pal.glow));
                        _glows.Add((stem, 14, new Color(pal.glow, 0.10f)));
                    }
                    else if (bd.IceSheet && r < bd.Grass + bd.Mushrooms + 0.3f)
                    {
                        // frost rime
                        _lines.Add((a + new Vector2(0, -1), b + new Vector2(0, -1), new Color(0.92f, 0.98f, 1f, 0.8f), 2f));
                    }
                }
                else if (up < -0.6f)
                {
                    if (r < bd.Stalactites)
                    {
                        float len = 6 + Hash01(hx, hy, cave.Seed + 9) * 16;
                        float wdt = 3 + Hash01(hx, hy, cave.Seed + 5) * 4;
                        var c = bd.IceSheet ? new Color(0.8f, 0.93f, 1f, 0.85f) : pal.rim.Darkened(0.25f);
                        _polys.Add((new[] { m + new Vector2(-wdt, -2), m + new Vector2(wdt, -2), m + new Vector2(0, len) }, c));
                    }
                    else if (r < bd.Stalactites + 0.03f && !underwater)
                    {
                        // hanging glow worm thread
                        float len = 10 + Hash01(hx, hy, cave.Seed + 7) * 22;
                        _lines.Add((m, m + new Vector2(0, len), new Color(pal.glow, 0.35f), 1f));
                        _glows.Add((m + new Vector2(0, len), 6, new Color(pal.glow, 0.5f)));
                    }
                }
                else if (r < bd.Crystals)
                {
                    // crystal cluster on walls
                    var dir = nrm;
                    for (int s = -1; s <= 1; s++)
                    {
                        var d = dir.Rotated(s * 0.5f);
                        float len = 7 + (1 - Math.Abs(s)) * 6;
                        var side = new Vector2(-d.Y, d.X) * 2.5f;
                        _polys.Add((new[] { m - side, m + d * len, m + side }, pal.glow.Lerp(Colors.White, 0.3f)));
                    }
                    _glows.Add((m + dir * 6, 20, new Color(pal.glow, 0.12f)));
                }
            }
        }

        public override void _Draw()
        {
            foreach (var g in _glows)
            {
                DrawCircle(g.p, g.r, g.col);
                DrawCircle(g.p, g.r * 0.5f, new Color(g.col, g.col.A * 1.5f));
            }
            DrawMultilineColors(_rimPts, _rimCols, 3f);
            foreach (var l in _lines) DrawLine(l.a, l.b, l.col, l.w);
            foreach (var p in _polys) DrawColoredPolygon(p.pts, p.col);
        }
    }
}
