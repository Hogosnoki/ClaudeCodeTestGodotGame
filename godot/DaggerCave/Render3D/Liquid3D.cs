using System;
using Godot;

namespace DaggerCave;

/// <summary>
/// The level's water or lava in 3D. Everything open below the liquid line is flooded, so one
/// surface plane and one "cut face" toward the camera cover it (rock hides both wherever it is
/// solid). Water also fills with a light-scattering fog volume; lava glows, lights the cave
/// around it and makes the air above it shimmer.
/// </summary>
public partial class Liquid3D : Node3D
{
    private static NoiseTexture2D _ripple;

    public static NoiseTexture2D Ripple => _ripple ??= new NoiseTexture2D
    {
        Width = 256, Height = 256, Seamless = true, AsNormalMap = true, BumpStrength = 6f, GenerateMipmaps = true,
        Noise = new FastNoiseLite { NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth, Frequency = 0.03f, FractalOctaves = 3, Seed = 3 },
    };

    public void Build(CaveData cave, TerrainField f, LightPool3D lights)
    {
        if (cave.Liquid == Liquid.None) return;
        var b = cave.Biome ?? Biomes.Get(BiomeId.Slime);
        float sy = -cave.WaterY / W3.Ppu;
        float x0 = -TerrainField.Margin, x1 = cave.W + TerrainField.Margin;
        float yBottom = -(cave.H + TerrainField.Margin);
        float zBack = f.ZLo, zFront = f.Style.Fe;
        float width = x1 - x0, cx = (x0 + x1) * 0.5f;
        float depthZ = zFront - zBack;
        bool lava = cave.Liquid == Liquid.Lava;

        // the surface
        var surf = new PlaneMesh { Size = new Vector2(width, depthZ), SubdivideWidth = (int)(width / 1.5f), SubdivideDepth = 6 };
        var surfInst = new MeshInstance3D { Mesh = surf, Position = new Vector3(cx, sy, (zBack + zFront) * 0.5f), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        // the cut face toward the camera
        float faceH = sy - yBottom;
        var face = new QuadMesh { Size = new Vector2(width, faceH + 0.6f) };
        var faceInst = new MeshInstance3D { Mesh = face, Position = new Vector3(cx, sy - faceH * 0.5f + 0.3f, zFront), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };

        if (!lava)
        {
            var shallow = new Color(b.LiquidTop.R, b.LiquidTop.G, b.LiquidTop.B);
            var deep = new Color(b.LiquidBottom.R, b.LiquidBottom.G, b.LiquidBottom.B);
            var sm = new ShaderMaterial { Shader = GD.Load<Shader>("res://DaggerCave/Render3D/Shaders/water_surface.gdshader"), RenderPriority = -10 };
            sm.SetShaderParameter("ripple", Ripple);
            sm.SetShaderParameter("shallow", shallow);
            sm.SetShaderParameter("deep", deep);
            sm.SetShaderParameter("flow", cave.Flow * 0.6f / W3.Ppu);
            surf.Material = sm;
            var fm = new ShaderMaterial { Shader = GD.Load<Shader>("res://DaggerCave/Render3D/Shaders/water_face.gdshader"), RenderPriority = -10 };
            fm.SetShaderParameter("shallow", shallow);
            fm.SetShaderParameter("deep", deep);
            fm.SetShaderParameter("line_color", new Color(b.LiquidLine.R, b.LiquidLine.G, b.LiquidLine.B));
            fm.SetShaderParameter("surface_y", sy);
            fm.SetShaderParameter("flow", cave.Flow * 0.6f / W3.Ppu);
            fm.SetShaderParameter("absorb", b.IceSheet ? 0.12f : 0.16f);
            face.Material = fm;
            // light shafts and haze in the water itself
            var fog = new FogVolume
            {
                Shape = RenderingServer.FogVolumeShape.Box,
                Size = new Vector3(width, faceH, depthZ + 0.5f),
                Position = new Vector3(cx, sy - faceH * 0.5f, (zBack + zFront) * 0.5f),
                Material = new FogMaterial
                {
                    Density = 0.11f,
                    Albedo = shallow.Lightened(0.35f),
                    Emission = deep * 0.4f,
                    EdgeFade = 0.05f,
                },
            };
            AddChild(fog);
        }
        else
        {
            var noise = TerrainLook.MaskNoise;
            var lm = new ShaderMaterial { Shader = GD.Load<Shader>("res://DaggerCave/Render3D/Shaders/lava.gdshader") };
            lm.SetShaderParameter("noise_tex", noise);
            lm.SetShaderParameter("surface_y", sy);
            surf.Material = lm;
            var lf = (ShaderMaterial)lm.Duplicate();
            lf.SetShaderParameter("face", 1f);
            face.Material = lf;
            // shimmer above the lava, behind the action
            var haze = new QuadMesh { Size = new Vector2(width, 3.2f) };
            var hm = new ShaderMaterial { Shader = GD.Load<Shader>("res://DaggerCave/Render3D/Shaders/heat_haze.gdshader"), RenderPriority = -10 };
            hm.SetShaderParameter("noise_tex", noise);
            haze.Material = hm;
            AddChild(new MeshInstance3D { Mesh = haze, Position = new Vector3(cx, sy + 1.6f, -1.2f), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
            // the lava lights the cave above it
            for (float x = 2; x < cave.W - 2; x += 5.5f)
            {
                var above = new Vector2(x * W3.Ppu, cave.WaterY - 10);
                if (cave.IsSolid(above)) continue;
                lights.Add(new LightSpot
                {
                    Pos = new Vector3(x, sy + 1.2f, -0.6f), Col = new Color(1f, 0.45f, 0.12f), Energy = 2.2f, Range = 9f, Flicker = 0.5f, Fog = 1.2f,
                });
            }
        }
        AddChild(surfInst);
        AddChild(faceInst);
    }
}
