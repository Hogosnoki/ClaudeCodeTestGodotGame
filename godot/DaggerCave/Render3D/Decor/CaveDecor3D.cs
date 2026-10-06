using System;
using System.Collections.Generic;
using Godot;

namespace DaggerCave;

/// <summary>
/// Dresses the 3D cave: stalactites and stalagmites, grass on walkable ground (green still means
/// you can walk it), glowing mushrooms, crystals and glow-worm threads that light their
/// surroundings, pebbles and boulders, and biome specials (icicles, roots, giant fungi). Pieces
/// sit on the real 3D surface at their depth (found by bisecting the terrain field) and are
/// batched into MultiMeshes per 32 m region.
/// </summary>
public partial class CaveDecor3D : Node3D
{
    private const float Region = 32f;

    private sealed class Kind
    {
        public Mesh Mesh;
        public bool Shadows;
        public readonly Dictionary<(int, int), List<Transform3D>> Placed = new();
    }

    private readonly List<Kind> _kinds = new();
    private TerrainField _f;
    private CaveData _cave;
    private Random _rng;
    private LightPool3D _lights;
    private int _lightCount;
    private const int MaxLights = 360;

    private Kind NewKind(MeshBuilder mb, Material mat, bool shadows)
    {
        var k = new Kind { Mesh = mb.ToMesh(mat), Shadows = shadows };
        _kinds.Add(k);
        return k;
    }

    private void Put(Kind k, Transform3D xf)
    {
        var key = ((int)MathF.Floor(xf.Origin.X / Region), (int)MathF.Floor(xf.Origin.Y / Region));
        if (!k.Placed.TryGetValue(key, out var list)) k.Placed[key] = list = new List<Transform3D>();
        list.Add(xf);
    }

    private float R() => (float)_rng.NextDouble();
    private float R(float a, float b) => a + (b - a) * (float)_rng.NextDouble();

    private void Light(Vector3 p, Color c, float energy, float range, float fog = 0.6f, float flicker = 0f)
    {
        if (_lightCount >= MaxLights) return;
        _lightCount++;
        _lights.Add(new LightSpot { Pos = p, Col = c, Energy = energy, Range = range, Fog = fog, Flicker = flicker });
    }

    /// <summary>Where the rock surface is at depth z near contour point p (cave metres), searching along the open-side normal n.</summary>
    private bool SurfaceAt(Vector2 p, Vector2 n, float z, out Vector3 pos, out Vector3 normal)
    {
        pos = default; normal = default;
        float lo = -1.8f, hi = 1.8f;
        float flo = _f.Eval(p.X + n.X * lo, p.Y + n.Y * lo, z), fhi = _f.Eval(p.X + n.X * hi, p.Y + n.Y * hi, z);
        if (!(flo > 0f && fhi < 0f)) return false;
        for (int k = 0; k < 14; k++)
        {
            float m = (lo + hi) * 0.5f;
            if (_f.Eval(p.X + n.X * m, p.Y + n.Y * m, z) > 0f) lo = m; else hi = m;
        }
        float t = (lo + hi) * 0.5f;
        float x = p.X + n.X * t, y = p.Y + n.Y * t;
        pos = new Vector3(x, -y, z);
        normal = _f.Normal(x, y, z);
        return true;
    }

    /// <summary>The back wall behind an open point (cave metres).</summary>
    private bool BackWallAt(float x, float y, out Vector3 pos, out Vector3 normal)
    {
        pos = default; normal = default;
        float hi = 0f, lo = hi;
        bool found = false;
        for (float z = -0.5f; z > _f.ZLo + 0.5f; z -= 0.3f)
        {
            if (_f.Eval(x, y, z) > 0f) { lo = z; hi = z + 0.3f; found = true; break; }
        }
        if (!found) return false;
        for (int k = 0; k < 12; k++)
        {
            float m = (lo + hi) * 0.5f;
            if (_f.Eval(x, y, m) > 0f) lo = m; else hi = m;
        }
        float zz = (lo + hi) * 0.5f;
        pos = new Vector3(x, -y, zz);
        normal = _f.Normal(x, y, zz);
        return true;
    }

    /// <summary>A basis whose +Y leans from the surface normal toward <paramref name="toward"/>, spun randomly about it.</summary>
    private Basis Orient(Vector3 normal, Vector3 toward, float lean, float scale)
    {
        var up = normal.Lerp(toward, lean).Normalized();
        var side = MathF.Abs(up.Dot(Vector3.Back)) < 0.95f ? up.Cross(Vector3.Back).Normalized() : up.Cross(Vector3.Right).Normalized();
        var fwd = side.Cross(up);
        var b = new Basis(side, up, fwd);
        b = b * new Basis(Vector3.Up, R() * Mathf.Tau);
        return b.Scaled(Vector3.One * scale);
    }

    public void Build(TerrainField f, CaveData cave, ShaderMaterial rockMat, LightPool3D lights)
    {
        ulong t0 = Time.GetTicksMsec();
        _f = f; _cave = cave; _lights = lights;
        _rng = new Random(cave.Seed * 31 + 5);
        var noise = new Noise3(cave.Seed + 11);
        var b = cave.Biome ?? Biomes.Get(BiomeId.Slime);
        var glowShader = GD.Load<Shader>("res://DaggerCave/Render3D/Shaders/glow.gdshader");
        var grassShader = GD.Load<Shader>("res://DaggerCave/Render3D/Shaders/grass.gdshader");
        bool frost = b.IceSheet, entrance = b.Id == BiomeId.Entrance, fungal = b.Id == BiomeId.Fungal, crystalCave = b.Id == BiomeId.Crystal;
        bool lava = cave.Liquid == Liquid.Lava;

        // ---- materials
        var grassMat = new ShaderMaterial { Shader = grassShader };
        grassMat.SetShaderParameter("root_color", b.Moss.Darkened(0.65f));
        grassMat.SetShaderParameter("tip_color", b.Moss.Lightened(0.15f));
        grassMat.SetShaderParameter("tip_glow", fungal ? 0.6f : 0f);
        var mushMat = new ShaderMaterial { Shader = glowShader };
        mushMat.SetShaderParameter("glow_color", b.Glow);
        mushMat.SetShaderParameter("glow_energy", 2.6f);
        var crystalMat = new ShaderMaterial { Shader = glowShader };
        crystalMat.SetShaderParameter("glow_color", b.Glow);
        crystalMat.SetShaderParameter("glow_energy", 1.6f);
        crystalMat.SetShaderParameter("crystal", 1f);
        crystalMat.SetShaderParameter("pulse", 0.2f);
        var wormMat = new ShaderMaterial { Shader = glowShader };
        wormMat.SetShaderParameter("glow_color", b.Glow);
        wormMat.SetShaderParameter("glow_energy", 4f);
        wormMat.SetShaderParameter("pulse", 0.5f);
        var iceMat = new ShaderMaterial { Shader = glowShader };
        iceMat.SetShaderParameter("glow_color", new Color(0.55f, 0.85f, 1f));
        iceMat.SetShaderParameter("glow_energy", 0.5f);
        iceMat.SetShaderParameter("crystal", 1f);
        iceMat.SetShaderParameter("pulse", 0f);
        var rootMat = new StandardMaterial3D { VertexColorUseAsAlbedo = true, Roughness = 0.9f };

        // ---- variants
        var stal = new Kind[4];
        for (int k = 0; k < stal.Length; k++) stal[k] = NewKind(DecorMeshes.Stalactite(_rng, 1f, 0.16f + k * 0.02f, noise), rockMat, true);
        var pebbles = new Kind[3];
        for (int k = 0; k < pebbles.Length; k++) pebbles[k] = NewKind(DecorMeshes.Pebbles(_rng, noise, 3 + k * 2, 0.07f + k * 0.02f), rockMat, false);
        var boulders = new Kind[3];
        for (int k = 0; k < boulders.Length; k++) boulders[k] = NewKind(DecorMeshes.Boulder(_rng, noise, 1f), rockMat, true);
        var grass = new Kind[3];
        for (int k = 0; k < grass.Length; k++) grass[k] = NewKind(DecorMeshes.GrassTuft(_rng, 9 + k * 4, 0.3f), grassMat, false);
        var mush = new Kind[3];
        for (int k = 0; k < mush.Length; k++) mush[k] = NewKind(DecorMeshes.MushroomCluster(_rng, 2 + k * 2, 0.22f, b.Glow), mushMat, true);
        var giant = fungal ? new[] { NewKind(DecorMeshes.MushroomCluster(_rng, 3, 2.2f, b.Glow, giant: true), mushMat, true), NewKind(DecorMeshes.MushroomCluster(_rng, 1, 3f, b.Glow, giant: true), mushMat, true) } : null;
        var crystalBody = b.Glow.Darkened(0.55f).Lerp(new Color(0.2f, 0.25f, 0.35f), 0.4f);
        var crystals = new Kind[3];
        for (int k = 0; k < crystals.Length; k++) crystals[k] = NewKind(DecorMeshes.CrystalCluster(_rng, 4 + k * 3, 0.45f + k * 0.2f, crystalBody), crystalMat, true);
        var worms = new Kind[2];
        for (int k = 0; k < worms.Length; k++) worms[k] = NewKind(DecorMeshes.GlowThreads(_rng, 5 + k * 4, 0.9f + k * 0.5f, b.Glow), wormMat, false);
        var icicles = frost ? new[] { NewKind(DecorMeshes.Icicles(_rng, 5, 0.8f), iceMat, true), NewKind(DecorMeshes.Icicles(_rng, 9, 1.3f), iceMat, true) } : null;
        var roots = entrance || b.GiantRoots ? new[] { NewKind(DecorMeshes.Roots(_rng, 6, 1.6f, noise), rootMat, true), NewKind(DecorMeshes.Roots(_rng, 10, 2.4f, noise), rootMat, true) } : null;
        // the root-choked tunnels: massive roots down through the ceilings from the world above
        var barkMat = new StandardMaterial3D { VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true, Roughness = 0.95f };
        var bark = b.Edge.Lerp(new Color(0.25f, 0.17f, 0.1f), 0.7f);
        Kind[] giantRoots = null;
        if (b.GiantRoots)
        {
            giantRoots = new Kind[4];
            for (int k = 0; k < giantRoots.Length; k++) giantRoots[k] = NewKind(DecorMeshes.GiantRoot(_rng, noise, 3.5f + k * 1.6f, 0.34f + k * 0.08f, bark.Lightened(0.04f * k)), barkMat, true);
        }
        // the fossil graveyards: old bones everywhere underfoot (and ribcages and skulls, below)
        var boneMat = new StandardMaterial3D { VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true, Roughness = 0.78f, RimEnabled = true, Rim = 0.2f };
        var boneCol = b.Glow.Lerp(new Color(0.78f, 0.72f, 0.6f), 0.7f).Darkened(0.12f);
        var bonePiles = b.Leviathans || b.Ossuary ? new[] { NewKind(DecorMeshes.Bones(_rng, noise, 5, 1f, boneCol), boneMat, true), NewKind(DecorMeshes.Bones(_rng, noise, 9, 1.3f, boneCol), boneMat, true) } : null;
        // the catacombs: loose skulls and heaps of them, long bones, burial urns, and skulls to rack up on the walls
        Kind[] skulls = null, skullHeaps = null, longBones = null, urns = null;
        if (b.Ossuary)
        {
            var ivory = new Color(0.76f, 0.72f, 0.62f);
            skulls = new Kind[3];
            for (int k = 0; k < skulls.Length; k++) skulls[k] = NewKind(OssuaryMeshes.HumanSkull(_rng, noise, 0.34f, ivory.Darkened(0.1f * k)), boneMat, true);
            skullHeaps = new Kind[2];
            for (int k = 0; k < skullHeaps.Length; k++)
            {
                var heap = new MeshBuilder();
                int n = 4 + k * 3;
                for (int i = 0; i < n; i++)
                {
                    int tier = i < n - 2 ? 0 : 1;
                    float hx = (i % (n - 2 + 1) - 0.5f * (n - 2)) * 0.3f * (tier == 0 ? 1f : 0.5f) + R(-0.05f, 0.05f);
                    var xf = new Transform3D(new Basis(Vector3.Up, R(0f, Mathf.Tau)) * new Basis(Vector3.Right, R(-0.4f, 0.4f)), new Vector3(hx, 0.14f + tier * 0.22f, R(-0.25f, 0.25f)));
                    heap.Append(OssuaryMeshes.HumanSkull(_rng, noise, R(0.28f, 0.36f), ivory.Darkened(R(0f, 0.25f)), jaw: tier == 0), xf);
                }
                skullHeaps[k] = NewKind(heap, boneMat, true);
            }
            longBones = new Kind[2];
            for (int k = 0; k < longBones.Length; k++) longBones[k] = NewKind(OssuaryMeshes.LongBone(_rng, noise, 0.9f + 0.3f * k, ivory.Darkened(0.08f * k)), boneMat, true);
            urns = new Kind[3];
            var clays = new[] { new Color(0.5f, 0.3f, 0.22f), new Color(0.42f, 0.33f, 0.26f), new Color(0.3f, 0.3f, 0.36f) };
            for (int k = 0; k < urns.Length; k++) urns[k] = NewKind(OssuaryMeshes.Urn(_rng, noise, 0.55f + 0.2f * k, clays[k]), boneMat, true);
        }

        // the catacombs' wall torches: an iron bracket, a stick and a cup, and a small flame (the flame glows by itself)
        Kind[] torch = null;
        var torchSpots = new List<Vector2>();
        if (b.Torches > 0)
        {
            var iron = new Color(0.16f, 0.15f, 0.17f);
            var wood = new Color(0.3f, 0.2f, 0.12f);
            var holder = new MeshBuilder();
            holder.Box(new Transform3D(Basis.Identity, new Vector3(0f, 0.3f, 0f)), new Vector3(0.035f, 0.3f, 0.035f), wood);
            holder.Box(new Transform3D(Basis.Identity, new Vector3(0f, 0.62f, 0f)), new Vector3(0.075f, 0.05f, 0.075f), iron);
            holder.Box(new Transform3D(Basis.Identity, new Vector3(0f, 0.2f, -0.16f)), new Vector3(0.022f, 0.022f, 0.16f), iron);
            var flame = new MeshBuilder();
            var fcol = new Color(1f, 0.72f, 0.3f);
            flame.Box(new Transform3D(Basis.Identity, new Vector3(0f, 0.78f, 0f)), new Vector3(0.045f, 0.13f, 0.045f), fcol);
            flame.Box(new Transform3D(new Basis(Vector3.Up, 0.785f), new Vector3(0f, 0.78f, 0f)), new Vector3(0.045f, 0.13f, 0.045f), fcol);
            var holderMat = new StandardMaterial3D { VertexColorUseAsAlbedo = true, Roughness = 0.85f };
            var flameMat = new StandardMaterial3D
            {
                AlbedoColor = new Color(1f, 0.75f, 0.35f), ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                EmissionEnabled = true, Emission = new Color(1f, 0.6f, 0.2f), EmissionEnergyMultiplier = 3f,
            };
            torch = new[] { NewKind(holder, holderMat, false), NewKind(flame, flameMat, false) };
        }

        float walkable = MathF.Cos(Mathf.DegToRad(Tune.Cave.WalkableSlopeDegrees)) - 0.01f;
        float waterY = cave.WaterY / CaveData.Cell; // cave metres
        var segs = f.Segs;
        var nrms = f.SegNormals;
        for (int s = 0; s < nrms.Count; s++)
        {
            var a = segs[2 * s]; var bb = segs[2 * s + 1];
            var n = nrms[s];
            var m = (a + bb) * 0.5f;
            if (m.X < 1 || m.Y < 1 || m.X > cave.W - 1 || m.Y > cave.H - 1) continue;
            float len = a.DistanceTo(bb);
            float up = -n.Y;
            bool wet = m.Y > waterY && cave.Liquid != Liquid.None;
            bool floor = up > walkable, ceiling = up < -0.55f;

            if (torch != null && floor && !wet && R() < b.Torches * len)
            {
                // a torch on the back wall, a couple of metres above the floor and well apart from the last
                var tp = a.Lerp(bb, R());
                float ty = tp.Y + n.Y * R(2.4f, 3.2f);
                bool apart = true;
                foreach (var o in torchSpots) if (o.DistanceTo(new Vector2(tp.X, ty)) < 9f) { apart = false; break; }
                if (apart)
                {
                    f.Column(tp.X, ty, out float sOpen, out _, out _);
                    if (sOpen <= -1.5f && BackWallAt(tp.X, ty, out var wp, out var wn))
                    {
                        torchSpots.Add(new Vector2(tp.X, ty));
                        var at = wp + wn * 0.3f;
                        var xf = new Transform3D(Basis.Identity.Scaled(Vector3.One * R(1.1f, 1.4f)), at);
                        Put(torch[0], xf);
                        Put(torch[1], xf);
                        // (a warm, flickering light that reaches into the blue)
                        Light(at + new Vector3(0f, 1.0f, 0.35f), new Color(1f, 0.62f, 0.28f), 2.1f, 9f, 1.0f, 1f);
                    }
                }
            }
            if (floor && !wet)
            {
                // grass: denser at the back, short in front of the play plane
                int tufts = (int)(b.Grass * 3.2f * len + R());
                for (int k = 0; k < tufts; k++)
                {
                    float z = R() < 0.7f ? R(-2.4f, -0.15f) : R(0.1f, 0.9f);
                    var p = a.Lerp(bb, R());
                    if (!SurfaceAt(p, n, z, out var pos, out var nn)) continue;
                    float sc = z > 0 ? R(0.35f, 0.65f) : R(0.7f, 1.5f);
                    Put(grass[_rng.Next(grass.Length)], new Transform3D(Orient(nn, Vector3.Up, 0.6f, sc), pos));
                }
                if (R() < b.Mushrooms * len * 1.4f)
                {
                    float z = R() < 0.8f ? R(-2.3f, -0.4f) : R(0.2f, 0.7f);
                    if (SurfaceAt(a.Lerp(bb, R()), n, z, out var pos, out var nn))
                    {
                        float sc = z > 0 ? R(0.6f, 0.9f) : R(0.8f, 1.6f);
                        Put(mush[_rng.Next(mush.Length)], new Transform3D(Orient(nn, Vector3.Up, 0.7f, sc), pos));
                        Light(pos + nn * 0.35f + Vector3.Up * 0.2f, b.Glow, 0.55f * sc, 3.2f + sc);
                    }
                }
                if (giant != null && R() < 0.05f * len)
                {
                    if (SurfaceAt(a.Lerp(bb, R()), n, R(-2.8f, -1.6f), out var pos, out var nn))
                    {
                        Put(giant[_rng.Next(giant.Length)], new Transform3D(Orient(nn, Vector3.Up, 0.85f, R(0.8f, 1.3f)), pos));
                        Light(pos + Vector3.Up * 2.2f + Vector3.Back * 0.6f, b.Glow, 1.4f, 7f, 0.9f);
                    }
                }
            }
            if (floor || (up > 0.2f))
            {
                if (R() < 0.35f * len)
                {
                    float z = R(-2.6f, -0.5f);
                    if (SurfaceAt(a.Lerp(bb, R()), n, z, out var pos, out var nn))
                        Put(pebbles[_rng.Next(pebbles.Length)], new Transform3D(Orient(nn, Vector3.Up, 0.5f, R(0.7f, 1.4f)), pos));
                }
                if (skulls != null && floor && !wet)
                {
                    // the dead underfoot: loose skulls, heaps of them, long bones, a burial urn standing in a corner
                    float roll = R();
                    float z = R() < 0.7f ? R(-2.6f, -0.3f) : R(0.2f, 0.9f);
                    var at = a.Lerp(bb, R());
                    if (roll < 0.2f * len && SurfaceAt(at, n, z, out var pos, out var nn))
                        Put(skulls[_rng.Next(skulls.Length)], new Transform3D(Orient(nn, Vector3.Up, 0.5f, z > 0 ? R(0.6f, 0.9f) : R(0.8f, 1.5f)), pos + nn * 0.08f));
                    else if (roll < 0.28f * len && SurfaceAt(at, n, z, out pos, out nn))
                        Put(skullHeaps[_rng.Next(skullHeaps.Length)], new Transform3D(Orient(nn, Vector3.Up, 0.2f, z > 0 ? R(0.6f, 0.8f) : R(0.8f, 1.4f)), pos));
                    else if (roll < 0.4f * len && SurfaceAt(at, n, z, out pos, out nn))
                        Put(longBones[_rng.Next(longBones.Length)], new Transform3D(Orient(nn, Vector3.Up, 0.9f, R(0.8f, 1.4f)), pos + nn * 0.05f));
                    else if (roll < 0.46f * len && z < 0 && SurfaceAt(at, n, z, out pos, out nn))
                        Put(urns[_rng.Next(urns.Length)], new Transform3D(Orient(nn, Vector3.Up, 0.1f, R(0.9f, 1.5f)), pos - nn * 0.04f));
                }
                if (bonePiles != null && floor && !wet && R() < (b.Ossuary ? 0.5f : 0.22f) * len)
                {
                    float z = R() < 0.75f ? R(-2.6f, -0.3f) : R(0.2f, 0.9f);
                    if (SurfaceAt(a.Lerp(bb, R()), n, z, out var pos, out var nn))
                        Put(bonePiles[_rng.Next(bonePiles.Length)], new Transform3D(Orient(nn, Vector3.Up, 0.8f, z > 0 ? R(0.5f, 0.8f) : R(0.7f, 1.4f)), pos));
                }
                if (floor && R() < 0.05f * len)
                {
                    float z = R(-3f, -1.4f);
                    if (SurfaceAt(a.Lerp(bb, R()), n, z, out var pos, out var nn))
                        Put(boulders[_rng.Next(boulders.Length)], new Transform3D(Orient(nn, Vector3.Up, 0.7f, R(0.35f, 0.9f)), pos - nn * 0.15f));
                }
                if (R() < b.Stalactites * 0.25f * len && !wet)
                {
                    // stalagmites, always behind the action
                    float z = R(-2.8f, -0.9f);
                    if (SurfaceAt(a.Lerp(bb, R()), n, z, out var pos, out var nn))
                    {
                        var xf = new Transform3D(Orient(nn, Vector3.Up, 0.9f, 1f) * Basis.FromScale(new Vector3(R(0.8f, 1.6f), R(0.5f, 1.6f), R(0.8f, 1.6f))), pos);
                        Put(stal[_rng.Next(stal.Length)], xf);
                    }
                }
            }
            if (ceiling)
            {
                if (R() < b.Stalactites * 1.4f * len)
                {
                    bool front = R() < 0.25f;
                    float z = front ? R(0.3f, 0.9f) : R(-2.6f, -0.2f);
                    if (SurfaceAt(a.Lerp(bb, R()), n, z, out var pos, out var nn))
                    {
                        float lenM = front ? R(0.25f, 0.6f) : R(0.4f, 2.2f);
                        var basis = Orient(nn, Vector3.Down, 0.85f, 1f) * Basis.FromScale(new Vector3(R(0.7f, 1.5f), lenM, R(0.7f, 1.5f)));
                        if (frost && R() < 0.7f) Put(icicles[_rng.Next(icicles.Length)], new Transform3D(Orient(nn, Vector3.Down, 0.95f, R(0.7f, 1.5f)), pos));
                        else Put(stal[_rng.Next(stal.Length)], new Transform3D(basis, pos));
                    }
                }
                if (!wet && R() < (fungal ? 0.08f : 0.035f) * len)
                {
                    float z = R(-2.4f, -0.3f);
                    if (SurfaceAt(a.Lerp(bb, R()), n, z, out var pos, out var nn))
                    {
                        Put(worms[_rng.Next(worms.Length)], new Transform3D(Orient(nn, Vector3.Down, 1f, R(0.8f, 1.3f)), pos));
                        Light(pos + Vector3.Down * 0.7f + Vector3.Back * 0.2f, b.Glow, 0.35f, 3.5f, 0.8f);
                    }
                }
                if (giantRoots != null && R() < 0.07f * len)
                {
                    bool front = R() < 0.15f;
                    float z = front ? R(0.7f, 1.05f) : R(-3.6f, -0.9f);
                    if (SurfaceAt(a.Lerp(bb, R()), n, z, out var pos, out var nn))
                    {
                        float sc = front ? R(0.4f, 0.65f) : R(0.8f, 1.3f);
                        var basis = Orient(nn, Vector3.Down, 0.9f, sc);
                        Put(giantRoots[_rng.Next(giantRoots.Length)], new Transform3D(basis, pos));
                        // pale lichen glowing on the bark, so the great roots read in the dark
                        if (!front && R() < 0.6f)
                        {
                            var along = pos + basis.Y.Normalized() * R(1.2f, 3.5f) * sc + Vector3.Back * 0.5f * sc;
                            Put(worms[_rng.Next(worms.Length)], new Transform3D(Orient(nn, Vector3.Down, 1f, R(0.6f, 1f)), along));
                            Light(along + Vector3.Back * 0.4f, b.Glow, 0.45f, 4f, 0.6f);
                        }
                    }
                }
                if (roots != null && R() < (b.GiantRoots ? 0.45f : 0.3f) * len)
                {
                    float z = R(-2.4f, 0.6f);
                    if (SurfaceAt(a.Lerp(bb, R()), n, z, out var pos, out var nn))
                        Put(roots[_rng.Next(roots.Length)], new Transform3D(Orient(nn, Vector3.Down, 1f, z > 0 ? R(0.4f, 0.7f) : R(0.8f, 1.4f)), pos));
                }
            }
            if (!floor && !ceiling && R() < b.Crystals * len * (crystalCave ? 1.4f : 1f))
            {
                float z = R(-2.4f, -0.3f);
                if (SurfaceAt(a.Lerp(bb, R()), n, z, out var pos, out var nn))
                {
                    float sc = R(0.6f, 1.5f);
                    Put(crystals[_rng.Next(crystals.Length)], new Transform3D(Orient(nn, Vector3.Up, 0.25f, sc), pos - nn * 0.1f));
                    Light(pos + nn * 0.5f, b.Glow, 0.9f * sc, 3.5f + sc * 1.5f);
                }
            }
        }

        // ---- the fossil graveyards: a leviathan's ribcage across each great chamber, running from
        // deep in the back to just in front of you, and a skull sunk into the back wall
        if (b.Leviathans)
        {
            foreach (var room in cave.Rooms)
            {
                float rx = room.RxPx / CaveData.Cell, ry = room.RyPx / CaveData.Cell;
                if (rx < 9f || ry < 6f) continue;
                var c = room.Center / CaveData.Cell;
                float topY = c.Y - ry * 0.78f;
                f.Column(c.X, topY, out float sTop, out _, out _);
                if (sTop > -0.6f) continue; // no open space up there for the spine
                int ribs = Math.Clamp((int)(ry * 0.45f), 4, 8);
                const float spacing = 1.15f;
                float z0 = 0.6f - (ribs - 1) * spacing;
                var cage = DecorMeshes.Ribcage(_rng, noise, ribs, spacing, rx * 0.78f, ry * 1.85f, boneCol);
                AddChild(new MeshInstance3D { Mesh = cage.ToMesh(boneMat), Position = new Vector3(c.X, -topY, z0), CastShadow = GeometryInstance3D.ShadowCastingSetting.On });
                // a faint pale light in the cage, so its arches read in the dark
                Light(new Vector3(c.X, -(c.Y - ry * 0.2f), -1.5f), boneCol.Lerp(b.Glow, 0.5f), 0.9f, rx * 1.1f, 0.5f);
                float side = R() < 0.5f ? -1f : 1f;
                if (BackWallAt(c.X + side * rx * R(0.25f, 0.6f), c.Y + ry * R(0.2f, 0.55f), out var wp, out var wn))
                {
                    float size = R(1.6f, 2.6f);
                    var skull = DecorMeshes.Skull(_rng, noise, size, boneCol);
                    var basis = new Basis(Vector3.Up, -MathF.PI / 2f + side * R(0.2f, 0.7f)) * new Basis(Vector3.Right, R(-0.35f, 0.35f));
                    AddChild(new MeshInstance3D { Mesh = skull.ToMesh(boneMat), Transform = new Transform3D(basis, wp - wn * size * 0.35f), CastShadow = GeometryInstance3D.ShadowCastingSetting.On });
                }
            }
        }

        // ---- the catacombs' ossuary walls: a bank of skulls racked up on the back wall of the larger rooms, and a few strays
        if (skulls != null)
        {
            foreach (var room in cave.Rooms)
            {
                float rx = room.RxPx / CaveData.Cell;
                if (rx < 6f || room.Kind == RoomKind.Boss) continue;
                var c = room.Center / CaveData.Cell;
                float floorY = room.Floor.Y / CaveData.Cell;
                int cols = Math.Clamp((int)(rx * 1.1f), 5, 11), rows = 3 + (_rng.Next(2));
                float px = c.X + R(-0.3f, 0.3f) * rx;
                for (int j = 0; j < rows; j++)
                    for (int i = 0; i < cols; i++)
                    {
                        // (each row a little shorter than the one below, so the bank reads as stacked)
                        if (j > 0 && (i < j || i >= cols - j)) continue;
                        float x = px + (i - (cols - 1) * 0.5f) * 0.58f + R(-0.04f, 0.04f), y = floorY - 1.1f - j * 0.52f;
                        _f.Column(x, y, out float sd, out _, out _);
                        if (sd > -1.0f || !BackWallAt(x, y, out var wp, out var wn)) continue;
                        var zb = wn.Normalized();
                        var xb = Vector3.Up.Cross(zb).Normalized();
                        var basis = new Basis(xb, zb.Cross(xb), zb) * new Basis(Vector3.Back, R(-0.2f, 0.2f)) * new Basis(Vector3.Right, R(-0.15f, 0.15f));
                        Put(skulls[_rng.Next(skulls.Length)], new Transform3D(basis.Scaled(Vector3.One * R(0.95f, 1.15f)), wp + zb * 0.02f));
                    }
                // a pale glimmer on the bank so it reads in the dark
                Light(new Vector3(px, -(floorY - 2.3f), -1.4f), boneCol.Lerp(b.Glow, 0.4f), 0.55f, 4.5f, 0.5f);
            }
        }

        // ---- the back walls: glowing growths and crystals catch the eye across big rooms
        int backTries = (int)(cave.W * cave.H * 0.02f);
        for (int k = 0; k < backTries; k++)
        {
            float x = R(2, cave.W - 2), y = R(2, cave.H - 2);
            f.Column(x, y, out float sDist, out _, out _);
            if (sDist > -1.2f) continue;
            bool wet = y > waterY && cave.Liquid != Liquid.None;
            if (wet && lava) continue;
            // (the catacombs' dim blue light, hung on the back walls)
            if (b.WallGlow > 0 && !wet && R() < b.WallGlow && BackWallAt(x, y, out var gp, out var gn))
                Light(gp + gn * 1.2f, b.Glow, 0.8f, 6.5f, 0.9f);
            float roll = R();
            if (roll < b.Crystals * 0.8f + (crystalCave ? 0.1f : 0f))
            {
                if (!BackWallAt(x, y, out var pos, out var nn)) continue;
                float sc = R(0.8f, 2.2f);
                Put(crystals[_rng.Next(crystals.Length)], new Transform3D(Orient(nn, Vector3.Up, 0.2f, sc), pos - nn * 0.15f));
                Light(pos + nn * 0.8f, b.Glow, 1.1f * sc, 4f + sc * 2f, 0.9f);
            }
            else if (!wet && roll < b.Crystals * 0.8f + b.Mushrooms * 0.9f + 0.02f)
            {
                if (!BackWallAt(x, y, out var pos, out var nn)) continue;
                float sc = R(0.8f, 1.8f);
                Put(mush[_rng.Next(mush.Length)], new Transform3D(Orient(nn, Vector3.Up, 0.35f, sc), pos - nn * 0.05f));
                Light(pos + nn * 0.6f, b.Glow, 0.6f * sc, 3.5f + sc, 0.8f);
            }
        }

        // ---- batch
        int instances = 0;
        foreach (var k in _kinds)
            foreach (var kv in k.Placed)
            {
                var list = kv.Value;
                var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = k.Mesh, InstanceCount = list.Count };
                for (int i = 0; i < list.Count; i++) mm.SetInstanceTransform(i, list[i]);
                AddChild(new MultiMeshInstance3D { Multimesh = mm, CastShadow = k.Shadows ? GeometryInstance3D.ShadowCastingSetting.On : GeometryInstance3D.ShadowCastingSetting.Off });
                instances += list.Count;
            }
        GD.Print($"[3D] decor: {instances} pieces in {GetChildCount()} batches, {_lightCount} light spots, {Time.GetTicksMsec() - t0} ms");
    }
}
