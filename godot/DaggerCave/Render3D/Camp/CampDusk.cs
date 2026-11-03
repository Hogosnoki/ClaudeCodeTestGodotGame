using System;
using System.Collections.Generic;
using Godot;

namespace DaggerCave;

/// <summary>
/// The camp at dusk, built to look real rather than drawn (the heroes stay as they are, to be redone later): the sun on the
/// horizon to the left in a peach-and-violet sky with clouds lit from below, ridge after ridge of mountains going purple into the
/// haze with mist lying in their valleys, pines on every slope and ledge, a great oak framing the left, mossy boulders and tall
/// grass and wild flowers in the meadow, and the cooking fire's warm light pooled on the trodden earth round it. Everything
/// out here wears photographed rock and earth or shaders written to light it as it is; nothing has an ink line.
/// </summary>
public partial class CampScene
{
    // the menu's view, which the whole scene is laid out round (see PlaceCamera)
    private static readonly Vector3 WidePos = new(-2.6f, 1.35f, 13.2f);
    private static readonly Vector3 WideLook = new(3.6f, 3.4f, -9f);

    /// <summary>Which way the sun lies (toward it): low on the horizon, to the left of the menu's view and beyond the mountains.</summary>
    private static Vector3 SunDir
    {
        get
        {
            var f = WideLook - WidePos; f.Y = 0; f = f.Normalized();
            var left = f.Rotated(Vector3.Up, Mathf.DegToRad(16f));
            return (left + Vector3.Up * 0.13f).Normalized();
        }
    }

    // ================================================================== sky, sun, air

    private void BuildDuskSky()
    {
        var skyMat = new ShaderMaterial { Shader = GD.Load<Shader>("res://DaggerCave/Render3D/Shaders/camp_sky.gdshader") };
        var env = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Sky, Sky = new Sky { SkyMaterial = skyMat, RadianceSize = Sky.RadianceSizeEnum.Size256 },
            // (the dusk sky's light: a soft lavender from everywhere, set outright rather than read off the sky, so it is the same on every machine)
            AmbientLightSource = Godot.Environment.AmbientSource.Color, AmbientLightColor = AmbientColor, AmbientLightEnergy = AmbientBase,
            ReflectedLightSource = Godot.Environment.ReflectionSource.Sky,
            TonemapMode = Godot.Environment.ToneMapper.Aces, TonemapExposure = 1.55f, TonemapWhite = 6f,
            // (the sun and the fire bloom; nothing else is bright enough to)
            GlowEnabled = true, GlowIntensity = 0.7f, GlowStrength = 1.0f, GlowBloom = 0.04f, GlowHdrThreshold = 1.1f, GlowHdrScale = 2f,
            GlowBlendMode = Godot.Environment.GlowBlendModeEnum.Softlight,
            // the air: distance goes violet (taking the sky's own colour), and a thin haze hangs in the light
            FogEnabled = true, FogLightColor = new Color(0.3f, 0.25f, 0.36f), FogLightEnergy = 1f, FogDensity = 0.0008f, FogSkyAffect = 0f,
            FogAerialPerspective = 0.25f, FogSunScatter = 0.1f,
            VolumetricFogEnabled = true, VolumetricFogDensity = 0.003f, VolumetricFogAlbedo = new Color(0.7f, 0.62f, 0.72f),
            VolumetricFogAnisotropy = 0.55f, VolumetricFogLength = 90f, VolumetricFogDetailSpread = 2f, VolumetricFogAmbientInject = 0.3f,
            VolumetricFogSkyAffect = 0.2f,
            SsaoEnabled = true, SsaoRadius = 1.2f, SsaoIntensity = 1.4f, SsaoPower = 1.4f,
            AdjustmentEnabled = true, AdjustmentSaturation = 1.0f, AdjustmentContrast = 1.06f,
        };
        AddChild(new WorldEnvironment { Environment = env });
        _env = env;
        // the low sun: orange, raking in from beyond the mountains on the left, long soft shadows
        var sun = new DirectionalLight3D
        {
            LightColor = new Color(1f, 0.58f, 0.36f), LightEnergy = 0.95f, LightSpecular = 0.6f, ShadowEnabled = true, LightAngularDistance = 1.2f,
            DirectionalShadowMaxDistance = 90f, DirectionalShadowMode = DirectionalLight3D.ShadowMode.Parallel4Splits, ShadowBlur = 1.5f,
            LightVolumetricFogEnergy = 1.4f,
        };
        AddChild(sun);
        sun.LookAtFromPosition(SunDir * 100f, Vector3.Zero, Vector3.Up);
        // the sky overhead: a dim violet light from above (no shadows), so what faces the sky is lit more than what faces the land
        var skyLight = new DirectionalLight3D { LightColor = new Color(0.62f, 0.55f, 0.78f), LightEnergy = 0.55f, LightSpecular = 0.15f, ShadowEnabled = false, SkyMode = DirectionalLight3D.SkyModeEnum.LightOnly };
        AddChild(skyLight);
        skyLight.LookAtFromPosition(new Vector3(0.3f, 1f, 0.45f) * 100f, Vector3.Zero, Vector3.Forward);
        // the sunset's glow off the western sky: a warm rose light from the left, raking across the cliff's faces (no shadows: it's the sky, not the sun)
        var glow = new DirectionalLight3D { LightColor = new Color(1f, 0.72f, 0.6f), LightEnergy = 0.62f, LightSpecular = 0.3f, ShadowEnabled = false, SkyMode = DirectionalLight3D.SkyModeEnum.LightOnly };
        AddChild(glow);
        var fromLeft = (SunDir.Rotated(Vector3.Up, Mathf.DegToRad(55f)) with { Y = 0.35f }).Normalized();
        glow.LookAtFromPosition(fromLeft * 100f, Vector3.Zero, Vector3.Up);
    }

    // ================================================================== the land

    /// <summary>Ridged noise: sharp crests, 0..1.</summary>
    private float Ridged(float x, float z, int octaves, float seed)
    {
        float sum = 0f, amp = 0.5f, norm = 0f, f = 1f, prev = 1f;
        for (int o = 0; o < octaves; o++)
        {
            float n = 1f - MathF.Abs(_noise.Sample(x * f, seed + o * 3.1f, z * f));
            n *= n;
            sum += n * amp * prev;
            prev = Math.Clamp(n * 1.6f, 0f, 1f);
            norm += amp;
            amp *= 0.5f; f *= 2.05f;
        }
        return sum / norm;
    }

    /// <summary>
    /// The land's height: flat round the camp, the meadow rolling gently further out, wooded hills, and ridge after ridge of
    /// mountains beyond (highest far off, so they stand over the hills in the haze).
    /// </summary>
    private float Ground(float x, float z)
    {
        float d = MathF.Sqrt(x * x + z * z);
        float lumps = _noise.Fbm(x * 0.35f, 9.1f, z * 0.35f, 2) * 0.07f;
        float roll = _noise.Fbm(x * 0.03f, 0.5f, z * 0.03f, 3) * 2.2f * W3.SmoothStep(14f, 40f, d);
        float hills = (_noise.Fbm(x * 0.011f, 2.5f, z * 0.011f, 4) * 0.5f + 0.5f) * 14f * W3.SmoothStep(45f, 120f, d);
        float mount = Ridged(x * 0.0032f, z * 0.0032f, 6, 17f) * W3.SmoothStep(150f, 600f, d) * (150f + 650f * W3.SmoothStep(500f, 2200f, d));
        return lumps + roll + hills + mount;
    }

    private void BuildLand()
    {
        var mat = new ShaderMaterial { Shader = GD.Load<Shader>("res://DaggerCave/Render3D/Shaders/camp_meadow.gdshader") };
        mat.SetShaderParameter("dirt_tex", GD.Load<Texture2D>("res://DaggerCave/Assets/Textures/rock_ground/rock_ground_diff.jpg"));
        mat.SetShaderParameter("dirt_nor", GD.Load<Texture2D>("res://DaggerCave/Assets/Textures/rock_ground/rock_ground_nor.jpg"));
        mat.SetShaderParameter("path_to", new Vector2(MouthAt.X, MouthAt.Z));
        mat.SetShaderParameter("patch_to", WorldPatchTo);
        // ---- near: a fine grid round the camp
        const float half = 110f;
        const int n = 200;
        var mb = new MeshBuilder();
        float step = half * 2f / n;
        for (int j = 0; j <= n; j++)
            for (int i = 0; i <= n; i++)
            {
                float x = -half + i * step, z = -half + j * step;
                mb.Add(new Vector3(x, Ground(x, z), z), GroundNormal(x, z, 0.5f), Colors.White);
            }
        for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
            {
                int a = j * (n + 1) + i, b = a + 1, c = a + n + 1, d = c + 1;
                // (inside the circle the far land starts at, so the two never overlap)
                float cx = -half + (i + 0.5f) * step, cz = -half + (j + 0.5f) * step;
                if (cx * cx + cz * cz > 106f * 106f) continue;
                mb.Tri(a, c, b); mb.Tri(b, c, d);
            }
        AddChild(new MeshInstance3D { Name = "Meadow", Mesh = mb.ToMesh(mat), CastShadow = GeometryInstance3D.ShadowCastingSetting.On });

        // ---- far: rings going out to the mountains, ever wider apart
        var stone = MakeStoneMaterial(0.05f, 0.006f, 0.6f, 0.9f, false, "rock_wall_08");
        stone.SetShaderParameter("desat", 0.4f);
        // (far rock reads darker and cooler than the cliff: the haze lightens it on the way)
        stone.SetShaderParameter("tint", new Color(0.55f, 0.52f, 0.62f));
        var far = new MeshBuilder();
        const int na = 220, nr = 90;
        for (int r = 0; r <= nr; r++)
        {
            float rad = 104f * MathF.Pow(2600f / 104f, r / (float)nr);
            for (int k = 0; k <= na; k++)
            {
                float a = k / (float)na * Mathf.Tau;
                float x = MathF.Cos(a) * rad, z = MathF.Sin(a) * rad;
                far.Add(new Vector3(x, Ground(x, z), z), GroundNormal(x, z, Math.Max(0.5f, rad * 0.01f)), Colors.White);
            }
        }
        for (int r = 0; r < nr; r++)
            for (int k = 0; k < na; k++)
            {
                int a = r * (na + 1) + k, b = a + 1, c = a + na + 1, d = c + 1;
                far.Tri(a, b, c); far.Tri(b, d, c);
            }
        // (near its inner edge the land is still meadow: the far mesh blends from the meadow's look to rock and forest by the shader's own slope and height rules)
        AddChild(new MeshInstance3D { Name = "Mountains", Mesh = far.ToMesh(stone), CastShadow = GeometryInstance3D.ShadowCastingSetting.On });
    }

    private Vector3 GroundNormal(float x, float z, float e) =>
        new Vector3(Ground(x - e, z) - Ground(x + e, z), 2f * e, Ground(x, z - e) - Ground(x, z + e)).Normalized();

    /// <summary>The rock material: photographed cliff rock, with moss (and on the mountains, grass and forest floor) on what faces up.</summary>
    private ShaderMaterial MakeStoneMaterial(float scale, float macro, float moss, float grass, bool vertexColor, string rock = "cliff_side")
    {
        var mat = new ShaderMaterial { Shader = GD.Load<Shader>("res://DaggerCave/Render3D/Shaders/camp_stone.gdshader") };
        string dir = $"res://DaggerCave/Assets/Textures/{rock}/{rock}";
        mat.SetShaderParameter("rock_tex", GD.Load<Texture2D>(dir + "_diff.jpg"));
        mat.SetShaderParameter("rock_nor", GD.Load<Texture2D>(dir + "_nor.jpg"));
        mat.SetShaderParameter("rock_arm", GD.Load<Texture2D>(dir + "_arm.jpg"));
        mat.SetShaderParameter("moss_tex", GD.Load<Texture2D>("res://DaggerCave/Assets/Textures/mossy_rock/mossy_rock_diff.jpg"));
        mat.SetShaderParameter("scale", scale);
        mat.SetShaderParameter("macro", macro);
        mat.SetShaderParameter("moss_amount", moss);
        mat.SetShaderParameter("grass_amount", grass);
        mat.SetShaderParameter("use_vertex_color", vertexColor ? 1f : 0f);
        return mat;
    }

    // ================================================================== the meadow's grass and flowers

    /// <summary>A tuft: <paramref name="blades"/> tapering, curling blades of <paramref name="segs"/> segments, fanned round its root.</summary>
    private static ArrayMesh TuftMesh(int blades, int segs, float height, Material mat, int seed)
    {
        var rng = new Random(seed);
        var mb = new MeshBuilder();
        for (int k = 0; k < blades; k++)
        {
            float a = k / (float)blades * Mathf.Tau + (float)rng.NextDouble() * 0.8f;
            var dir = new Vector3(MathF.Cos(a), 0, MathF.Sin(a));
            var side = new Vector3(-dir.Z, 0, dir.X);
            float h = height * (0.55f + 0.6f * (float)rng.NextDouble());
            float lean = h * (0.15f + 0.45f * (float)rng.NextDouble());
            float w = 0.012f + 0.01f * (float)rng.NextDouble();
            var root = dir * 0.03f * (float)rng.NextDouble();
            int prevL = -1, prevR = -1;
            for (int s = 0; s <= segs; s++)
            {
                float t = s / (float)segs;
                // (rising, then arching over: the lean grows with the square of the height)
                var c = root + dir * (lean * t * t) + Vector3.Up * (h * t * (1f - 0.18f * t * t));
                float ww = w * (1f - t * 0.92f);
                var nrm = (Vector3.Up * 0.6f + dir * 0.4f).Normalized();
                int l = mb.Add(c - side * ww, nrm, Colors.White, new Vector2(0, t));
                int r = mb.Add(c + side * ww, nrm, Colors.White, new Vector2(1, t));
                if (prevL >= 0) mb.Quad(prevL, prevR, r, l);
                prevL = l; prevR = r;
            }
        }
        return mb.ToMesh(mat);
    }

    private bool InCampClear(float x, float z)
    {
        // the clearing round the fire, the way to the cave, and the cliff's foot
        if (x * x + z * z < 3.9f * 3.9f) return true;
        var a = Vector2.Zero; var b = new Vector2(MouthAt.X, MouthAt.Z); var p = new Vector2(x, z);
        var ab = b - a; float h = Math.Clamp((p - a).Dot(ab) / ab.Dot(ab), 0f, 1f);
        if ((p - (a + ab * h)).Length() < 1.3f) return true;
        // (and the trodden patch out in front of the fire, toward the meadow's edge where the menu's view stands)
        var c2 = WorldPatchTo;
        var ac = c2 - a; float h2 = Math.Clamp((p - a).Dot(ac) / ac.Dot(ac), 0f, 1f);
        if ((p - (a + ac * h2)).Length() < 1.9f * (1f - 0.5f * h2)) return true;
        return false;
    }

    private void BuildMeadow()
    {
        var blades = new ShaderMaterial { Shader = GD.Load<Shader>("res://DaggerCave/Render3D/Shaders/camp_blades.gdshader") };
        var nearMesh = TuftMesh(9, 4, 0.36f, blades, 11);
        var farMesh = TuftMesh(5, 2, 0.34f, blades, 12);
        var tallMesh = TuftMesh(6, 5, 0.72f, blades, 13);
        var nearX = new List<Transform3D>(); var nearC = new List<Color>();
        var farX = new List<Transform3D>(); var farC = new List<Color>();
        var tallX = new List<Transform3D>(); var tallC = new List<Color>();
        var fwd = WideLook - WidePos; fwd.Y = 0; fwd = fwd.Normalized();
        var rng = new Random(21);
        Color Tint(float x, float z)
        {
            // patches: greener, browner, drier straw
            float p = _noise.Sample(x * 0.12f, 6.6f, z * 0.12f);
            float q = (float)rng.NextDouble();
            var c = new Color(1f, 1f, 1f).Lerp(new Color(1.15f, 1.0f, 0.8f), Math.Clamp(p, 0f, 1f)).Lerp(new Color(0.8f, 0.95f, 0.8f), Math.Clamp(-p, 0f, 1f));
            return c * (0.7f + 0.35f * q);
        }
        // the grass is thickest where the camera looks: a wedge out from the menu's view, then thinner all round
        for (int k = 0; k < 70000; k++)
        {
            float x, z;
            if (k < 52000)
            {
                // (in the view: distance from the camera with more near it, across the view's width)
                float dist = 0.6f + 55f * MathF.Pow((float)rng.NextDouble(), 1.7f);
                float ang = ((float)rng.NextDouble() - 0.5f) * Mathf.DegToRad(100f);
                var dir = fwd.Rotated(Vector3.Up, ang);
                x = WidePos.X + dir.X * dist; z = WidePos.Z + dir.Z * dist;
            }
            else
            {
                float r = 4f + 30f * MathF.Sqrt((float)rng.NextDouble());
                float a = (float)rng.NextDouble() * Mathf.Tau;
                x = MathF.Cos(a) * r; z = MathF.Sin(a) * r;
            }
            if (InCampClear(x, z) || NearCliffFoot(x, z)) continue;
            // (thinner in places, so it isn't a carpet)
            if (_noise.Sample(x * 0.21f, 1.3f, z * 0.21f) < -0.2f && rng.NextDouble() < 0.8) continue;
            float camD = new Vector2(x - WidePos.X, z - WidePos.Z).Length();
            float s = 0.75f + 0.6f * (float)rng.NextDouble();
            var basis = new Basis(Vector3.Up, (float)rng.NextDouble() * Mathf.Tau).Scaled(new Vector3(s, s * (0.8f + 0.5f * (float)rng.NextDouble()), s));
            var xf = new Transform3D(basis, new Vector3(x, Ground(x, z) - 0.02f, z));
            var tint = Tint(x, z);
            // (the tall stems only at the front and the sides, never across the view of the camp)
            var off = new Vector2(x - WidePos.X, z - WidePos.Z);
            float across = MathF.Abs(off.X * fwd.Z - off.Y * fwd.X) / Math.Max(1f, camD);
            if (camD < 9f && (camD < 3.5f || across > 0.25f) && rng.NextDouble() < 0.18) { tallX.Add(xf); tallC.Add(tint); }
            else if (camD < 16f) { nearX.Add(xf); nearC.Add(tint); }
            else { farX.Add(xf.ScaledLocal(new Vector3(1.3f, 1.1f, 1.3f))); farC.Add(tint); }
        }
        AddChild(Instances(nearMesh, nearX, nearC, false));
        AddChild(Instances(farMesh, farX, farC, false));
        AddChild(Instances(tallMesh, tallX, tallC, false));

        // wild flowers: white umbels and spikes of lavender, in drifts
        var stemMat = new StandardMaterial3D { VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true, Roughness = 0.8f, CullMode = BaseMaterial3D.CullModeEnum.Disabled, BacklightEnabled = true, Backlight = new Color(0.3f, 0.3f, 0.25f) };
        var umbel = new MeshBuilder();
        umbel.Tube(new List<Vector3> { Vector3.Zero, new(0.02f, 0.25f, 0.01f), new(0.03f, 0.48f, 0f) }, new List<float> { 0.006f, 0.005f, 0.004f }, 4, new Color(0.22f, 0.26f, 0.12f), false);
        for (int k = 0; k < 9; k++)
        {
            float a = k / 9f * Mathf.Tau, r = 0.025f + 0.03f * (k % 3) / 2f;
            DecorMeshes.AddSphere(umbel, new Vector3(0.03f + MathF.Cos(a) * r, 0.49f + 0.012f * (k % 2), MathF.Sin(a) * r), 0.007f, new Color(0.46f, 0.45f, 0.43f), 3);
        }
        var spike = new MeshBuilder();
        spike.Tube(new List<Vector3> { Vector3.Zero, new(0.01f, 0.3f, 0f), new(0.02f, 0.55f, 0f) }, new List<float> { 0.005f, 0.004f, 0.003f }, 4, new Color(0.2f, 0.25f, 0.13f), false);
        for (int k = 0; k < 9; k++)
        {
            float t = k / 8f;
            DecorMeshes.AddSphere(spike, new Vector3(0.012f + 0.008f * t, 0.36f + 0.2f * t, 0f), 0.016f * (1f - 0.4f * t), new Color(0.48f, 0.36f, 0.68f), 3);
        }
        var umX = new List<Transform3D>(); var spX = new List<Transform3D>();
        for (int k = 0; k < 5000; k++)
        {
            float dist = 1f + 26f * MathF.Pow((float)rng.NextDouble(), 1.4f);
            float ang = ((float)rng.NextDouble() - 0.5f) * Mathf.DegToRad(95f);
            var dir = fwd.Rotated(Vector3.Up, ang);
            float x = WidePos.X + dir.X * dist, z = WidePos.Z + dir.Z * dist;
            if (InCampClear(x, z) || NearCliffFoot(x, z)) continue;
            float drift = _noise.Sample(x * 0.18f, 4.2f, z * 0.18f);
            if (drift < 0.38f) continue;
            float s = 0.75f + 0.55f * (float)rng.NextDouble();
            var xf = new Transform3D(new Basis(Vector3.Up, (float)rng.NextDouble() * Mathf.Tau).Scaled(Vector3.One * s), new Vector3(x, Ground(x, z) - 0.02f, z));
            // (lavender to the right of the way to the cave, umbels everywhere)
            if (_noise.Sample(x * 0.09f, 8.8f, z * 0.09f) > 0.2f) spX.Add(xf); else umX.Add(xf);
        }
        AddChild(Instances(umbel.ToMesh(stemMat), umX, null, false));
        AddChild(Instances(spike.ToMesh(stemMat), spX, null, false));
    }

    /// <summary>The end of the trodden patch running out from the fire toward the menu's view.</summary>
    private static Vector2 WorldPatchTo
    {
        get { var d = new Vector2(WidePos.X, WidePos.Z).Normalized(); return d * 6.5f; }
    }

    private bool NearCliffFoot(float x, float z)
    {
        // (behind the line of the cliff's foot)
        var rel = new Vector3(x, 0, z) - MouthAt;
        return rel.Dot(MouthFacing) < 3.5f;
    }

    private static MultiMeshInstance3D Instances(Mesh mesh, List<Transform3D> xforms, List<Color> colors, bool shadows)
    {
        var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = colors != null, Mesh = mesh, InstanceCount = xforms.Count };
        for (int k = 0; k < xforms.Count; k++)
        {
            mm.SetInstanceTransform(k, xforms[k]);
            if (colors != null) mm.SetInstanceColor(k, colors[k]);
        }
        return new MultiMeshInstance3D { Multimesh = mm, CastShadow = shadows ? GeometryInstance3D.ShadowCastingSetting.On : GeometryInstance3D.ShadowCastingSetting.Off };
    }

    // ================================================================== pines

    private static readonly Dictionary<int, ArrayMesh> _pines = new();

    /// <summary>A pine: a straight trunk under whorl after whorl of drooping, ragged boughs, wide below and narrowing to a spire.</summary>
    private ArrayMesh PineMesh(int seed, int whorls, Material mat)
    {
        int key = seed * 100 + whorls;
        if (_pines.TryGetValue(key, out var have)) return have;
        var rng = new Random(seed);
        var mb = new MeshBuilder();
        const float h = 1f;
        mb.Tube(new List<Vector3> { new(0, -0.05f, 0), new(0, h * 0.5f, 0), new(0, h, 0) }, new List<float> { 0.028f, 0.018f, 0.004f }, 6, new Color(0.22f, 0.16f, 0.12f), false);
        for (int w = 0; w < whorls; w++)
        {
            float t = w / (float)(whorls - 1);
            float y = h * (0.16f + 0.8f * t);
            float reach = h * (0.3f - 0.26f * t) * (0.85f + 0.3f * (float)rng.NextDouble());
            int boughs = 6 + rng.Next(4);
            for (int b = 0; b < boughs; b++)
            {
                float a = b / (float)boughs * Mathf.Tau + (float)rng.NextDouble() * 0.7f;
                var dir = new Vector3(MathF.Cos(a), 0, MathF.Sin(a));
                var side = new Vector3(-dir.Z, 0, dir.X);
                float len = reach * (0.7f + 0.5f * (float)rng.NextDouble());
                float droop = len * (0.35f + 0.25f * (float)rng.NextDouble());
                float wide = len * 0.42f;
                var col = new Color(0.10f, 0.14f, 0.07f) * (0.8f + 0.45f * (float)rng.NextDouble());
                // a ragged frond: a spine out and down, its edges toothed
                var root = new Vector3(0, y, 0);
                int prevL = -1, prevR = -1, prevC = -1;
                const int segs = 4;
                for (int s = 0; s <= segs; s++)
                {
                    float u = s / (float)segs;
                    var c = root + dir * len * u - Vector3.Up * droop * u * u + Vector3.Up * 0.02f * MathF.Sin(u * 3f);
                    float ww = wide * MathF.Sin(MathF.PI * MathF.Pow(u, 0.6f)) * (0.75f + 0.5f * (float)rng.NextDouble()) + 0.003f;
                    var nrm = (Vector3.Up + dir * 0.3f).Normalized();
                    int l = mb.Add(c - side * ww - Vector3.Up * ww * 0.25f, nrm, col, new Vector2(0, u), new Vector2(u, 0));
                    int r = mb.Add(c + side * ww - Vector3.Up * ww * 0.25f, nrm, col, new Vector2(1, u), new Vector2(u, 0));
                    int cc = mb.Add(c + Vector3.Up * ww * 0.12f, nrm, col * 1.1f, new Vector2(0.5f, u), new Vector2(u, 0));
                    if (prevL >= 0) { mb.Quad(prevL, prevC, cc, l); mb.Quad(prevC, prevR, r, cc); }
                    prevL = l; prevR = r; prevC = cc;
                }
            }
        }
        var mesh = mb.ToMesh(mat, true);
        _pines[key] = mesh;
        return mesh;
    }

    private StandardMaterial3D _pineMat;
    private StandardMaterial3D PineMat => _pineMat ??= new StandardMaterial3D
    {
        VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true, Roughness = 0.88f, CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        BacklightEnabled = true, Backlight = new Color(0.12f, 0.14f, 0.06f),
    };

    /// <summary>Pines: on the cliff's ledges (<paramref name="ledges"/>), round the meadow's edge, and in their thousands on the far slopes.</summary>
    private void BuildPines(List<Vector3> ledges)
    {
        var rng = new Random(61);
        var near = new List<Transform3D>();
        var far = new List<Transform3D>();
        void Pine(Vector3 at, float height, List<Transform3D> list)
        {
            float s = height;
            var basis = new Basis(Vector3.Up, (float)rng.NextDouble() * Mathf.Tau).Scaled(new Vector3(s * (0.85f + 0.3f * (float)rng.NextDouble()), s, s * (0.85f + 0.3f * (float)rng.NextDouble())));
            list.Add(new Transform3D(basis, at - Vector3.Up * 0.2f));
        }
        // on the cliff's ledges, in clumps
        foreach (var p in ledges)
            if (rng.NextDouble() < 0.7) Pine(p, 3f + 3.5f * (float)rng.NextDouble(), near);
        // round the meadow, behind the camp and off to the left (leaving the cave and the sky clear)
        int meadowPines = 0;
        for (int k = 0; k < 600 && meadowPines < 16; k++)
        {
            float r = 45f + 110f * (float)rng.NextDouble();
            float a = Mathf.DegToRad(150f + 120f * (float)rng.NextDouble());
            float x = MathF.Cos(a) * r, z = MathF.Sin(a) * r;
            if (_noise.Sample(x * 0.04f, 7.7f, z * 0.04f) < 0.0f) continue;
            if (NearCliffFoot(x, z) && new Vector3(x, 0, z).DistanceTo(MouthAt) < 30f) continue;
            Pine(new Vector3(x, Ground(x, z), z), 5f + 5f * (float)rng.NextDouble(), near);
            meadowPines++;
        }
        // the far slopes: forest in patches, thinning out up the mountains
        for (int k = 0; k < 40000 && far.Count < 5000; k++)
        {
            float r = 90f + 900f * MathF.Pow((float)rng.NextDouble(), 1.4f);
            float a = (float)rng.NextDouble() * Mathf.Tau;
            float x = MathF.Cos(a) * r, z = MathF.Sin(a) * r;
            float g = Ground(x, z);
            var nrm = GroundNormal(x, z, 2f);
            if (nrm.Y < 0.8f || g > 260f) continue;
            if (_noise.Sample(x * 0.012f, 3.3f, z * 0.012f) < -0.1f) continue;
            Pine(new Vector3(x, g, z), 8f + 8f * (float)rng.NextDouble(), far);
        }
        AddChild(Instances(PineMesh(5, 16, PineMat), near, null, true));
        AddChild(Instances(PineMesh(6, 9, PineMat), far, null, false));
    }

    // ================================================================== the oak

    /// <summary>A leaf picture: a clump of overlapping oak-ish leaves, alpha-cut, its brightness for shading within the clump.</summary>
    private static ImageTexture LeafTexture()
    {
        const int s = 128;
        var img = Image.CreateEmpty(s, s, true, Image.Format.Rgba8);
        img.Fill(new Color(0, 0, 0, 0));
        var rng = new Random(3);
        for (int k = 0; k < 26; k++)
        {
            // a leaf: a lobed ellipse at an angle
            float cx = 18f + (s - 36f) * (float)rng.NextDouble(), cy = 18f + (s - 36f) * (float)rng.NextDouble();
            float len = 14f + 10f * (float)rng.NextDouble(), wid = len * 0.45f, ang = (float)rng.NextDouble() * Mathf.Tau;
            float shade = 0.5f + 0.5f * (float)rng.NextDouble();
            float ca = MathF.Cos(ang), sa = MathF.Sin(ang);
            for (int y = (int)(cy - len); y <= (int)(cy + len); y++)
                for (int x = (int)(cx - len); x <= (int)(cx + len); x++)
                {
                    if (x < 0 || y < 0 || x >= s || y >= s) continue;
                    float dx = x - cx, dy = y - cy;
                    float u = (dx * ca + dy * sa) / len, v = (-dx * sa + dy * ca) / wid;
                    // lobes along its edge
                    float lobe = 1f + 0.22f * MathF.Sin(u * 14f);
                    if (u * u + v * v / (lobe * lobe) > 1f) continue;
                    float vein = MathF.Abs(v) < 0.08f ? 0.8f : 1f;
                    img.SetPixel(x, y, new Color(shade * vein, shade * vein, shade * vein, 1f));
                }
        }
        img.GenerateMipmaps();
        return ImageTexture.CreateFromImage(img);
    }

    /// <summary>The great oak at the left of the view: a thick, leaning trunk, boughs reaching out over the meadow, and a dark crown of leaves against the sky.</summary>
    private void BuildOak()
    {
        var fwd = WideLook - WidePos; fwd.Y = 0; fwd = fwd.Normalized();
        var left = fwd.Rotated(Vector3.Up, MathF.PI / 2f);
        var at = WidePos + fwd * 11f + left * 10f;
        at.Y = Ground(at.X, at.Z);
        var rng = new Random(44);
        var noise = new Noise3(45);
        var wood = new MeshBuilder();
        var leaves = new MeshBuilder();
        var bark = new Color(0.16f, 0.12f, 0.09f);
        var tips = new List<(Vector3 p, float r)>();
        void Branch(Vector3 from, Vector3 dir, float len, float rad, int depth)
        {
            var pts = new List<Vector3> { from };
            var rads = new List<float> { rad };
            var p = from; var d = dir;
            for (int s = 1; s <= 4; s++)
            {
                d = (d + new Vector3((float)rng.NextDouble() - 0.5f, ((float)rng.NextDouble() - 0.4f) * 0.6f, (float)rng.NextDouble() - 0.5f) * 0.35f).Normalized();
                p += d * len / 4f;
                pts.Add(p); rads.Add(rad * (1f - 0.18f * s));
            }
            wood.Tube(pts, rads, depth < 2 ? 9 : 5, bark * (0.85f + 0.3f * (float)rng.NextDouble()), false, (i, k) => 0.12f * rad * noise.Sample(i * 1.3f + from.X, k * 0.8f, depth));
            if (depth >= 3 || rad < 0.05f) { tips.Add((p, len * 0.9f)); return; }
            int kids = depth == 0 ? 5 : 3;
            for (int c = 0; c < kids; c++)
            {
                float t = 0.45f + 0.55f * (c + 1) / kids;
                int idx = Math.Min(pts.Count - 1, (int)(t * (pts.Count - 1)));
                var nd = (d + new Vector3((float)rng.NextDouble() * 2f - 1f, (float)rng.NextDouble() * 0.9f - 0.1f, (float)rng.NextDouble() * 2f - 1f) * 0.95f + left * 0.35f).Normalized();
                Branch(pts[idx], nd, len * 0.68f, rads[idx] * 0.62f, depth + 1);
            }
            tips.Add((p, len * 0.7f));
        }
        // the trunk leans out over the meadow, toward the camp
        // (leaning out to the left, so its crown frames the corner and leaves the sunset clear)
        var lean = (Vector3.Up + left * 0.3f - fwd * 0.1f).Normalized();
        Branch(at - Vector3.Up * 0.4f, lean, 5.2f, 0.5f, 0);
        // roots swelling into the ground
        for (int k = 0; k < 6; k++)
        {
            float a = k / 6f * Mathf.Tau + (float)rng.NextDouble();
            var d = new Vector3(MathF.Cos(a), 0, MathF.Sin(a));
            wood.Tube(new List<Vector3> { at + Vector3.Up * 0.6f + d * 0.2f, at + Vector3.Up * 0.15f + d * 0.8f, at - Vector3.Up * 0.15f + d * 1.6f },
                new List<float> { 0.32f, 0.18f, 0.06f }, 6, bark, false);
        }
        // leaves: clumps of cards round every twig's end
        foreach (var (p, r) in tips)
        {
            int cards = 7;
            for (int c = 0; c < cards; c++)
            {
                var o = new Vector3((float)rng.NextDouble() * 2f - 1f, (float)rng.NextDouble() * 1.4f - 0.5f, (float)rng.NextDouble() * 2f - 1f) * r * 0.55f;
                var ctr = p + o;
                float size = 0.9f + 0.7f * (float)rng.NextDouble();
                var nrm = new Vector3((float)rng.NextDouble() * 2f - 1f, (float)rng.NextDouble() + 0.3f, (float)rng.NextDouble() * 2f - 1f).Normalized();
                var u = nrm.Cross(Vector3.Forward).Normalized(); if (u.LengthSquared() < 0.1f) u = Vector3.Right;
                var v = nrm.Cross(u).Normalized();
                float shade = 0.75f + 0.4f * (float)rng.NextDouble() + 0.15f * Math.Clamp(o.Y / r, -1f, 1f);
                var col = new Color(shade, shade, shade * 0.9f);
                var uv2 = new Vector2(1f, 0f);
                int a0 = leaves.Add(ctr - u * size - v * size, nrm, col, new Vector2(0, 0), uv2);
                int a1 = leaves.Add(ctr + u * size - v * size, nrm, col, new Vector2(1, 0), uv2);
                int a2 = leaves.Add(ctr + u * size + v * size, nrm, col, new Vector2(1, 1), uv2);
                int a3 = leaves.Add(ctr - u * size + v * size, nrm, col, new Vector2(0, 1), uv2);
                leaves.Quad(a0, a1, a2, a3);
            }
        }
        wood.SmoothNormals();
        var barkMat = MakeStoneMaterial(0.9f, 0.1f, 0.35f, 0f, true, "dark_rock");
        AddChild(new MeshInstance3D { Name = "OakWood", Mesh = wood.ToMesh(barkMat), CastShadow = GeometryInstance3D.ShadowCastingSetting.On });
        var leafMat = new ShaderMaterial { Shader = GD.Load<Shader>("res://DaggerCave/Render3D/Shaders/camp_foliage.gdshader") };
        leafMat.SetShaderParameter("leaf_tex", LeafTexture());
        leafMat.SetShaderParameter("color", new Color(0.17f, 0.21f, 0.08f));
        leafMat.SetShaderParameter("wind", 0.06f);
        AddChild(new MeshInstance3D { Name = "OakLeaves", Mesh = leaves.ToMesh(leafMat, true), CastShadow = GeometryInstance3D.ShadowCastingSetting.On });
    }

    // ================================================================== rocks in the meadow

    private void BuildMeadowRocks()
    {
        var fwd = WideLook - WidePos; fwd.Y = 0; fwd = fwd.Normalized();
        var left = fwd.Rotated(Vector3.Up, MathF.PI / 2f);
        var mb = new MeshBuilder();
        var noise = new Noise3(73);
        int seed = 900;
        void Rock(Vector3 at, float r, float squash = 0.65f)
        {
            at.Y = Ground(at.X, at.Z) + r * squash * 0.35f;
            Boulder(mb, noise, at, new Vector3(r * 1.2f, r * squash, r), (float)(seed * 0.7), new Color(0.78f, 0.76f, 0.77f), seed++, 0.4f);
        }
        // the big lichened boulder at the front left, a smaller one by your feet, stones scattered toward the fire
        Rock(WidePos + fwd * 6.2f + left * 4.9f, 1.55f);
        Rock(WidePos + fwd * 2.2f + left * 1.9f, 0.6f, 0.55f);
        Rock(WidePos + fwd * 3.0f - left * 0.6f, 0.32f, 0.5f);
        Rock(WidePos + fwd * 7.5f - left * 1.6f, 0.42f, 0.5f);
        Rock(WidePos + fwd * 9.5f - left * 6.5f, 0.3f, 0.6f);
        Rock(WidePos + fwd * 4.5f - left * 5.2f, 0.75f, 0.55f);
        mb.SmoothNormals();
        var rockMat = MakeStoneMaterial(0.5f, 0.06f, 0.55f, 0f, true, "mossy_rock");
        rockMat.SetShaderParameter("desat", 0.3f);
        AddChild(new MeshInstance3D { Name = "MeadowRocks", Mesh = mb.ToMesh(rockMat), CastShadow = GeometryInstance3D.ShadowCastingSetting.On });
    }

    // ================================================================== mist

    private void BuildMist()
    {
        var shader = GD.Load<Shader>("res://DaggerCave/Render3D/Shaders/camp_mist.gdshader");
        // (upright banks turned to face the menu's view: seen from down in the meadow, a flat sheet would all but vanish edge-on)
        void Sheet(Vector3 centre, Vector2 size, float density, float scale, float soft, float bright = 1f)
        {
            var toCam = WidePos - centre; toCam.Y = 0; toCam = toCam.Normalized();
            float yaw = MathF.Atan2(toCam.X, toCam.Z);
            var m = new ShaderMaterial { Shader = shader };
            m.SetShaderParameter("density", density);
            m.SetShaderParameter("scale", scale);
            m.SetShaderParameter("soft", soft);
            m.SetShaderParameter("sun_dir", SunDir);
            m.SetShaderParameter("brightness", bright);
            AddChild(new MeshInstance3D
            {
                Mesh = new PlaneMesh { Size = size, SubdivideWidth = 1, SubdivideDepth = 1, Orientation = PlaneMesh.OrientationEnum.Z }, MaterialOverride = m, Position = centre,
                Rotation = new Vector3(0f, yaw, 0f), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            });
        }
        // banks lying in the valleys among the far ridges, layer on layer
        var f = WideLook - WidePos; f.Y = 0; f = f.Normalized();
        var l = f.Rotated(Vector3.Up, MathF.PI / 2f);
        Sheet(WidePos + f * 260f + l * 120f + Vector3.Up * 14f, new Vector2(520f, 50f), 0.75f, 0.012f, 40f);
        Sheet(WidePos + f * 480f + l * 260f + Vector3.Up * 30f, new Vector2(800f, 80f), 0.7f, 0.008f, 60f);
        Sheet(WidePos + f * 750f + l * 420f + Vector3.Up * 55f, new Vector2(1100f, 110f), 0.65f, 0.006f, 80f);
        Sheet(WidePos + f * 150f + l * 60f + Vector3.Up * 6f, new Vector2(260f, 26f), 0.6f, 0.02f, 25f);
        // low round the cliff's foot and drifting out of the cave
        Sheet(MouthAt + MouthFacing * 3f + Vector3.Up * 1.3f, new Vector2(30f, 4.2f), 0.5f, 0.12f, 1.5f, 1.0f);
        Sheet(MouthAt + MouthFacing * 1.2f + Vector3.Up * 2.2f, new Vector2(14f, 5f), 0.42f, 0.16f, 1.5f, 0.95f);
        Sheet(MouthAt + MouthFacing * 7f + CliffRight * 6f + Vector3.Up * 1.0f, new Vector2(40f, 3f), 0.35f, 0.1f, 1.2f, 0.95f);
    }
}
