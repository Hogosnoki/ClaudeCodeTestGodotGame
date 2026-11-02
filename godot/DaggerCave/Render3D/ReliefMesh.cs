using System;
using Godot;

namespace DaggerCave;

/// <summary>
/// A solid made from a picture's height map (tools/shrine_prep.py, tools/logo_prep.py write them): the map holds, for every solid pixel,
/// its distance D to the nearest edge (16 bits: red the high byte, green the low). Both faces stand off from the middle by a height
/// worked out from D, so the shape's depth comes straight from its outline: sharp at its edges, swelling along its middle. The
/// picture itself is its texture (the UVs run over it), trimmed to the exact outline by the picture's alpha. Used by the shrine
/// flowers (ShrineRelief.cs) and the title (UI/TitleLogo.cs). Each vertex carries its whole thickness there (picture pixels) in
/// UV2.x, so a shader can leave out everything thinner than a line it is given (the title's grow-in).
/// </summary>
public static class ReliefMesh
{
    /// <summary>A height map's distance at a pixel (picture pixels; 0 outside the shape).</summary>
    public static float DistanceAt(Image himg, float maxDist, int x, int y)
    {
        if (x < 0 || y < 0 || x >= himg.GetWidth() || y >= himg.GetHeight()) return 0f;
        var c = himg.GetPixel(x, y);
        return (Mathf.Round(c.R * 255f) * 256f + Mathf.Round(c.G * 255f)) / 65535f * maxDist;
    }

    /// <summary>
    /// The relief: <paramref name="faceHeight"/> turns a distance D (picture pixels) into how far each face stands off the middle
    /// (picture pixels; half the whole thickness). The grid is sampled every <paramref name="step"/> pixels; <paramref name="scale"/> is
    /// metres per pixel; (<paramref name="pivotX"/>, <paramref name="pivotY"/>) is the picture point placed at the origin.
    /// </summary>
    public static ArrayMesh Build(Image himg, float maxDist, Func<float, float> faceHeight, float scale, float pivotX, float pivotY, int step = 3)
        => Build(himg, maxDist, faceHeight, scale, pivotX, pivotY, step, out _);

    /// <summary>
    /// As above; <paramref name="ranks"/> is the solid grid's thicknesses (picture pixels), thickest first, sampled at 257 even steps
    /// through them: a vanish line walked down it uncovers the same share of the shape at every step.
    /// </summary>
    public static ArrayMesh Build(Image himg, float maxDist, Func<float, float> faceHeight, float scale, float pivotX, float pivotY, int step, out float[] ranks)
    {
        if (himg.IsCompressed()) himg.Decompress();
        int w = himg.GetWidth(), h = himg.GetHeight();
        float Height(int x, int y)
        {
            float d = DistanceAt(himg, maxDist, x, y);
            return d > 0f ? faceHeight(d) : 0f;
        }
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
            var pos = new Vector3((i * step - pivotX) * scale, (pivotY - j * step) * scale, hs[i, j] * scale * sz);
            return idx[s, i, j] = mb.Add(pos, n, Colors.White, new Vector2((float)(i * step) / (w - 1), (float)(j * step) / (h - 1)), new Vector2(hs[i, j] * 2f, 0f));
        }
        for (int i = 0; i < nx - 1; i++)
            for (int j = 0; j < ny - 1; j++)
            {
                // (a cell that touches the shape at all; the picture's alpha trims it to the exact outline)
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
        var solid = new System.Collections.Generic.List<float>();
        foreach (var v in hs) if (v > 0f) solid.Add(v * 2f);
        solid.Sort((a, b) => b.CompareTo(a));
        ranks = new float[257];
        for (int q = 0; q <= 256; q++) ranks[q] = solid.Count > 0 ? solid[Math.Min(solid.Count - 1, (int)(q / 256f * (solid.Count - 1)))] : 0f;
        return mb.ToMesh(null, true);
    }
}
