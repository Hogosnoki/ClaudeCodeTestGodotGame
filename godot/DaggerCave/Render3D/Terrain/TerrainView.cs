using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

namespace DaggerCave;

/// <summary>
/// The level's rock in 3D: meshes the <see cref="TerrainField"/> in 16 x 16 m chunks (in
/// parallel), and dresses them in the biome's rock material.
/// </summary>
public partial class TerrainView : Node3D
{
    private const int ChunkCells = 32; // grid cells per chunk side (16 m)

    public TerrainField Field { get; private set; }
    public ShaderMaterial Material { get; private set; }
    public int Triangles { get; private set; }

    public void Build(CaveData cave)
    {
        ulong t0 = Time.GetTicksMsec();
        var style = TerrainStyle.For(cave.Biome);
        Field = new TerrainField(cave, style);
        ulong t1 = Time.GetTicksMsec();
        int cellsX = Field.Nx - 1, cellsY = Field.Ny - 1;
        int cw = (cellsX + ChunkCells - 1) / ChunkCells, ch = (cellsY + ChunkCells - 1) / ChunkCells;
        var datas = new TerrainChunkData[cw * ch];
        Parallel.For(0, cw * ch, k =>
        {
            int cx = k % cw, cy = k / cw;
            int i0 = cx * ChunkCells, j0 = cy * ChunkCells;
            datas[k] = TerrainMesher.Build(Field, i0, j0, Math.Min(cellsX, i0 + ChunkCells), Math.Min(cellsY, j0 + ChunkCells));
        });
        ulong t2 = Time.GetTicksMsec();
        Material = TerrainLook.Make(cave);
        int tris = 0;
        foreach (var d in datas)
        {
            if (d.Indices.Length == 0) continue;
            var arrays = new Godot.Collections.Array();
            arrays.Resize((int)Mesh.ArrayType.Max);
            arrays[(int)Mesh.ArrayType.Vertex] = d.Verts;
            arrays[(int)Mesh.ArrayType.Normal] = d.Normals;
            arrays[(int)Mesh.ArrayType.Color] = d.Colors;
            arrays[(int)Mesh.ArrayType.Index] = d.Indices;
            var mesh = new ArrayMesh();
            mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
            mesh.SurfaceSetMaterial(0, Material);
            AddChild(new MeshInstance3D { Mesh = mesh, CastShadow = GeometryInstance3D.ShadowCastingSetting.On });
            tris += d.Indices.Length / 3;
        }
        Triangles = tris;
        GD.Print($"[3D] terrain {cave.W}x{cave.H}: field {t1 - t0} ms, mesh {t2 - t1} ms, upload {Time.GetTicksMsec() - t2} ms, {tris / 1000}k triangles in {GetChildCount()} chunks");
    }
}

/// <summary>Per-biome rock materials built from the CC0 photo textures.</summary>
public static class TerrainLook
{
    private static readonly Dictionary<string, Texture2D> Cache = new();
    private static Shader _shader;
    private static NoiseTexture2D _mask;

    public static Texture2D Tex(string set, string map)
    {
        string path = $"res://DaggerCave/Assets/Textures/{set}/{set}_{map}.jpg";
        if (!Cache.TryGetValue(path, out var t)) { t = GD.Load<Texture2D>(path); Cache[path] = t; }
        return t;
    }

    /// <summary>A tileable smooth-noise texture used for masks (moss patches, veins, sparkle).</summary>
    public static NoiseTexture2D MaskNoise
    {
        get
        {
            if (_mask != null) return _mask;
            _mask = new NoiseTexture2D
            {
                Width = 512, Height = 512, Seamless = true, GenerateMipmaps = true,
                Noise = new FastNoiseLite { NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth, Frequency = 0.012f, FractalOctaves = 4, Seed = 7 },
            };
            return _mask;
        }
    }

    private static void Set(ShaderMaterial m, string slot, string set, bool arm = true)
    {
        m.SetShaderParameter(slot + "_alb", Tex(set, "diff"));
        m.SetShaderParameter(slot + "_nrm", Tex(set, "nor"));
        if (arm) m.SetShaderParameter(slot + "_arm", Tex(set, "arm"));
    }

    public static ShaderMaterial Make(CaveData cave)
    {
        _shader ??= GD.Load<Shader>("res://DaggerCave/Render3D/Shaders/rock.gdshader");
        var b = cave.Biome ?? Biomes.Get(BiomeId.Slime);
        var m = new ShaderMaterial { Shader = _shader };
        string a = "cliff_side", bb = "rock_face";
        if (b.Id is BiomeId.Den or BiomeId.Ruins) (a, bb) = (bb, a);
        Set(m, "a", a);
        Set(m, "b", bb);
        Set(m, "g", "rock_ground");
        Set(m, "m", "mossy_rock", arm: false);
        m.SetShaderParameter("mask_noise", MaskNoise);
        m.SetShaderParameter("tint_edge", b.Edge);
        m.SetShaderParameter("tint_deep", b.Deep);
        m.SetShaderParameter("moss_tint", b.Moss);
        m.SetShaderParameter("glow_color", b.Glow);
        m.SetShaderParameter("moss_amount", Math.Clamp(b.Grass * 1.2f + 0.1f, 0f, 0.85f));
        float tintStrength = 0.62f, brightness = 1f;
        int special = 0;
        switch (b.Id)
        {
            case BiomeId.Entrance: tintStrength = 0.45f; brightness = 1.05f; break;
            case BiomeId.Ruins: special = 1; Set(m, "s", "rock_wall_08"); tintStrength = 0.5f; break;
            case BiomeId.Fungal: special = 5; tintStrength = 0.75f; break;
            case BiomeId.Frost: special = 2; Set(m, "s", "snow_02"); tintStrength = 0.8f; brightness = 1.15f; break;
            case BiomeId.Crystal: special = 4; tintStrength = 0.85f; break;
            case BiomeId.Magma: case BiomeId.Lair: special = 3; Set(m, "s", "dark_rock"); tintStrength = 0.55f; break;
            case BiomeId.Roots: special = 6; tintStrength = 0.7f; break;
            case BiomeId.Mine: tintStrength = 0.7f; brightness = 1.05f; break;
            case BiomeId.Fossils: special = 7; tintStrength = 0.6f; brightness = 1.05f; break;
        }
        m.SetShaderParameter("tint_strength", tintStrength);
        m.SetShaderParameter("brightness", brightness);
        m.SetShaderParameter("special_mode", special);
        m.SetShaderParameter("liquid_mode", cave.Liquid switch { Liquid.Water => 1, Liquid.Lava => 2, _ => 0 });
        m.SetShaderParameter("liquid_y", -cave.WaterY / W3.Ppu);
        m.SetShaderParameter("liquid_glow", b.LiquidLine);
        m.SetShaderParameter("water_light", b.LiquidLine);
        m.SetShaderParameter("lip_z", TerrainStyle.For(b).Fe);
        return m;
    }
}
