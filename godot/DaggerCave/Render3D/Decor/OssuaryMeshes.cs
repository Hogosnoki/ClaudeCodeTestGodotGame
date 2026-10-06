using System;
using Godot;

namespace DaggerCave;

/// <summary>The catacombs' dressing: human skulls, long bones and burial urns (the beasts' skulls and bone scatters are in DecorMeshes).</summary>
public static class OssuaryMeshes
{
    /// <summary>A human skull facing +Z, its crown up: a rounded cranium, hollow eyes, a nose, cheekbones, a row of teeth over a jaw.</summary>
    public static MeshBuilder HumanSkull(Random rng, Noise3 noise, float size, Color bone, bool jaw = true)
    {
        var mb = new MeshBuilder();
        float R() => (float)rng.NextDouble();
        var dark = new Color(0.025f, 0.02f, 0.02f);
        var tooth = bone.Lightened(0.14f);
        var stain = bone.Darkened(0.22f);
        Vector3 S(float x, float y, float z) => new Vector3(x, y, z) * size;
        // the cranium, a little longer front to back, stained darker at the crown
        mb.Blob(S(0f, 0.12f, -0.04f), S(0.5f, 0.5f, 0.56f), 7, bone, noise, 0.07f, 2.4f);
        mb.Blob(S(0f, 0.34f, -0.12f), S(0.4f, 0.28f, 0.42f), 5, stain, noise, 0.05f, 2.8f);
        // the brow, the face under it and the cheekbones
        mb.Blob(S(0f, -0.04f, 0.34f), S(0.4f, 0.12f, 0.2f), 5, bone, noise, 0.06f, 3f);
        mb.Blob(S(0f, -0.22f, 0.3f), S(0.3f, 0.26f, 0.22f), 6, bone, noise, 0.07f, 3f);
        foreach (float sx in new[] { -1f, 1f })
        {
            mb.Blob(S(0.3f * sx, -0.14f, 0.36f), S(0.12f, 0.09f, 0.11f), 4, bone, noise, 0.08f, 3f);
            // the eye sockets: deep and dark under the brow
            mb.Blob(S(0.19f * sx, -0.06f, 0.5f), S(0.14f, 0.15f, 0.08f), 5, dark, noise, 0f, 1f);
            // (and the temples and the jaw's hinge)
            mb.Blob(S(0.42f * sx, -0.18f, 0.02f), S(0.1f, 0.18f, 0.2f), 4, bone, noise, 0.06f, 3f);
        }
        // the nose: a dark hollow, a ridge above
        mb.Blob(S(0f, -0.2f, 0.52f), S(0.06f, 0.1f, 0.05f), 4, dark, noise, 0f, 1f);
        mb.Blob(S(0f, -0.04f, 0.5f), S(0.05f, 0.1f, 0.05f), 4, bone, noise, 0.05f, 3f);
        // teeth: a row of blocks under the cheeks
        var tbox = new MeshBuilder();
        tbox.Box(Transform3D.Identity, new Vector3(0.026f, 0.034f, 0.024f) * size, tooth);
        for (int i = -4; i <= 4; i++)
        {
            float t = i / 4f, zz = 0.5f - 0.26f * t * t;
            mb.Append(tbox, new Transform3D(new Basis(Vector3.Up, -t * 0.55f), S(i * 0.062f, -0.42f - 0.01f * (i & 1), zz)));
        }
        if (jaw)
        {
            // the lower jaw, hanging a little open
            mb.Blob(S(0f, -0.58f, 0.3f), S(0.27f, 0.09f, 0.24f), 5, bone.Darkened(0.05f), noise, 0.06f, 3f);
            foreach (float sx in new[] { -1f, 1f })
                mb.Tube(new[] { S(0.36f * sx, -0.22f, 0.0f), S(0.34f * sx, -0.5f, 0.1f), S(0.22f * sx, -0.6f, 0.38f) }, new[] { 0.06f * size, 0.06f * size, 0.05f * size }, 5, bone.Darkened(0.05f), capStart: true);
            for (int i = -3; i <= 3; i++)
            {
                float t = i / 3f, zz = 0.44f - 0.2f * t * t;
                mb.Append(tbox, new Transform3D(new Basis(Vector3.Up, -t * 0.5f) * Basis.FromScale(new Vector3(0.9f, 0.85f, 0.9f)), S(i * 0.062f, -0.5f, zz)));
            }
        }
        // a crack or two across the crown
        if (R() < 0.5f)
            mb.Tube(new[] { S(-0.1f, 0.58f, 0.1f), S(0.04f, 0.5f, 0.3f), S(0.0f, 0.36f, 0.46f) }, new[] { 0.012f * size, 0.012f * size, 0.01f * size }, 3, dark, capStart: false);
        mb.SmoothNormals();
        return mb;
    }

    /// <summary>One long bone along +X (a thigh or shin), a knob at each end.</summary>
    public static MeshBuilder LongBone(Random rng, Noise3 noise, float length, Color bone)
    {
        var mb = new MeshBuilder();
        float r = length * 0.055f;
        var a = new Vector3(-length * 0.5f, 0f, 0f);
        var b = new Vector3(length * 0.5f, 0f, 0f);
        mb.Tube(new[] { a, Vector3.Zero, b }, new[] { r * 1.0f, r * 0.75f, r * 0.95f }, 6, bone, capStart: false);
        foreach (float sx in new[] { -1f, 1f })
        {
            var e = new Vector3(sx * length * 0.5f, 0f, 0f);
            mb.Blob(e + new Vector3(0, r * 0.7f, r * 0.5f), Vector3.One * r * 1.7f, 4, bone, noise, 0.18f, 4f);
            mb.Blob(e + new Vector3(0, -r * 0.5f, -r * 0.3f), Vector3.One * r * 1.6f, 4, bone, noise, 0.18f, 4f);
        }
        mb.SmoothNormals();
        return mb;
    }

    /// <summary>A burial urn: a round belly, a neck, a flared lip and two small handles, glazed a dull terracotta with a dark band.</summary>
    public static MeshBuilder Urn(Random rng, Noise3 noise, float size, Color clay)
    {
        var mb = new MeshBuilder();
        var band = clay.Darkened(0.4f);
        Vector3 S(float x, float y, float z) => new Vector3(x, y, z) * size;
        mb.Blob(S(0f, 0.34f, 0f), S(0.32f, 0.34f, 0.32f), 7, clay, noise, 0.03f, 2f, 0.3f);
        mb.Blob(S(0f, 0.36f, 0f), S(0.335f, 0.045f, 0.335f), 6, band, noise, 0f, 1f);
        mb.Tube(new[] { S(0f, 0.62f, 0f), S(0f, 0.8f, 0f), S(0f, 0.92f, 0f) }, new[] { 0.2f * size, 0.13f * size, 0.17f * size }, 8, clay, capStart: false);
        mb.Blob(S(0f, 0.94f, 0f), S(0.19f, 0.04f, 0.19f), 6, band, noise, 0f, 1f);
        foreach (float sx in new[] { -1f, 1f })
            mb.Tube(new[] { S(0.15f * sx, 0.86f, 0f), S(0.3f * sx, 0.78f, 0f), S(0.3f * sx, 0.6f, 0f) }, new[] { 0.025f * size, 0.025f * size, 0.025f * size }, 4, clay, capStart: true);
        mb.SmoothNormals();
        return mb;
    }
}
