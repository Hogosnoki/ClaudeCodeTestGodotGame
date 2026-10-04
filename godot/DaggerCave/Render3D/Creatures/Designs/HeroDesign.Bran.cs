using System;
using System.Collections.Generic;
using System.IO;
using Godot;

namespace DaggerCave;

/// <summary>
/// The Swordsman, Bran: a broad, weathered warrior from the concept art (short dark hair and
/// stubble, a slate cowl, a fur mantle on his left shoulder, straps and bracers over a dark tunic,
/// a torn tabard, fur-cuffed boots). His body is a mesh shaped from the art (<c>Art/bran.mesh</c>: a
/// clean, decimated body with painted material zones and skinning weights for this same hero
/// skeleton), so every pose and stroke is shared with the others; only the sword (his longsword,
/// gripped where the rig's hand is) and the eyes are added here. See tools/bran/ for how it was made.
/// </summary>
public sealed partial class HeroDesign
{
    private const string BranPath = "res://DaggerCave/Art/bran.mesh";
    // class ids in the file (tools/bran/zones.py)
    private const int ClSkin = 0, ClHair = 1, ClNavy = 2, ClLeather = 3, ClTunic = 4, ClFur = 5, ClBoot = 6, ClGlove = 7, ClStubble = 8, ClKnee = 9;

    private static Mat BranMat(int cls) => cls switch
    {
        ClSkin or ClStubble => Mat.Skin,
        ClFur or ClHair => Mat.Fur,
        ClNavy or ClTunic => Mat.Cloth,
        _ => Mat.Leather,
    };

    private void SculptBran(Sculptor s)
    {
        LoadBranBody(s);
        // the eyes, dark, in the face's deep-set sockets
        int headB = s["head"];
        s.Eye(headB, new(0.166f, 0.8f, 0.016f), 0.0105f, C(0.09f, 0.065f, 0.05f), 0.15f);
        s.Eye(headB, new(0.168f, 0.8f, -0.046f), 0.0105f, C(0.09f, 0.065f, 0.05f), 0.15f);
        // the longsword, gripped in the right fist
        int handR = s["hand_r"];
        var grip = new Transform3D(Basis.Identity, new Vector3(0.016f, -0.1f, 0.2f));
        var sw = new MeshBuilder();
        sw.Append(PropMeshes.Longsword(1.05f, 0.036f, C(0.72f, 0.74f, 0.78f), C(0.42f, 0.44f, 0.47f), C(0.1f, 0.06f, 0.04f)), grip);
        s.Rigid(handR, sw, Mat.Metal);
    }

    /// <summary>Each class's own colour (the art's, cleaned): what the blotches settle toward.</summary>
    private static Color BranBase(int cls) => cls switch
    {
        ClSkin => C(0.52f, 0.36f, 0.27f),
        ClStubble => C(0.4f, 0.3f, 0.24f),
        ClHair => C(0.16f, 0.1f, 0.075f),
        ClNavy => C(0.1f, 0.14f, 0.26f),
        ClLeather => C(0.3f, 0.19f, 0.11f),
        ClTunic => C(0.13f, 0.11f, 0.13f),
        ClFur => C(0.4f, 0.28f, 0.19f),
        ClBoot => C(0.2f, 0.14f, 0.1f),
        ClGlove => C(0.24f, 0.16f, 0.1f),
        _ => C(0.28f, 0.2f, 0.14f),
    };

    private static float Lum(Color c) => 0.3f * c.R + 0.59f * c.G + 0.11f * c.B;

    /// <summary>
    /// The mesh came out of an image-to-3D model with the art's colours projected onto it, which left it
    /// lumpy and blotched (pale smears across the arms, pink flecks in the hair). Here the surface is relaxed a
    /// little (a Taubin smooth: it softens the lumps without shrinking the body) and each material's colour
    /// is spread smooth over its own surface and pulled toward the colour that material is meant to be,
    /// keeping the art's light and shade as the variation.
    /// </summary>
    private static void Refine(Vector3[] pos, Color[] col, byte[] cls, int[] idx)
    {
        int nv = pos.Length;
        var nbr = new List<int>[nv];
        for (int v = 0; v < nv; v++) nbr[v] = new List<int>(6);
        void Link(int a, int b) { if (!nbr[a].Contains(b)) nbr[a].Add(b); if (!nbr[b].Contains(a)) nbr[b].Add(a); }
        for (int f = 0; f < idx.Length; f += 3) { Link(idx[f], idx[f + 1]); Link(idx[f + 1], idx[f + 2]); Link(idx[f + 2], idx[f]); }

        // geometry: two Taubin passes (shrink by lambda, swell by mu), only between vertices of one material so
        // the seams between cloth, skin and leather keep their edges
        var tmp = new Vector3[nv];
        for (int pass = 0; pass < 4; pass++)
        {
            float k = pass % 2 == 0 ? 0.45f : -0.47f;
            for (int v = 0; v < nv; v++)
            {
                Vector3 sum = Vector3.Zero; int n = 0;
                foreach (int u in nbr[v]) if (cls[u] == cls[v]) { sum += pos[u]; n++; }
                tmp[v] = n == 0 ? pos[v] : pos[v] + (sum / n - pos[v]) * k;
            }
            Array.Copy(tmp, pos, nv);
        }

        // colour: spread smooth over each material (6 passes), then pull toward the material's own colour
        var cur = (Color[])col.Clone();
        var next = new Color[nv];
        for (int pass = 0; pass < 6; pass++)
        {
            for (int v = 0; v < nv; v++)
            {
                float r = cur[v].R, g = cur[v].G, b = cur[v].B; int n = 1;
                foreach (int u in nbr[v]) if (cls[u] == cls[v]) { r += cur[u].R; g += cur[u].G; b += cur[u].B; n++; }
                next[v] = new Color(r / n, g / n, b / n);
            }
            (cur, next) = (next, cur);
        }
        var meanLum = new float[16]; var count = new int[16];
        for (int v = 0; v < nv; v++) { meanLum[cls[v]] += Lum(cur[v]); count[cls[v]]++; }
        for (int k = 0; k < 16; k++) if (count[k] > 0) meanLum[k] /= count[k];
        for (int v = 0; v < nv; v++)
        {
            var baseCol = BranBase(cls[v]);
            // the shade the art gave this spot, relative to its material's average (kept, but softened)
            float shade = meanLum[cls[v]] > 0.01f ? Mathf.Clamp(Lum(cur[v]) / meanLum[cls[v]], 0.55f, 1.5f) : 1f;
            shade = 1f + (shade - 1f) * (cls[v] == ClSkin ? 0.45f : 0.7f);
            var target = new Color(Math.Min(1f, baseCol.R * shade), Math.Min(1f, baseCol.G * shade), Math.Min(1f, baseCol.B * shade));
            col[v] = cur[v].Lerp(target, cls[v] == ClSkin ? 0.85f : 0.65f);
        }
    }

    /// <summary>Reads the body mesh and adds it as skinned parts, one per material.</summary>
    private static void LoadBranBody(Sculptor s)
    {
        using var fa = Godot.FileAccess.Open(BranPath, Godot.FileAccess.ModeFlags.Read);
        if (fa == null) { GD.PushError($"[bran] can't open {BranPath}"); return; }
        var bytes = fa.GetBuffer((long)fa.GetLength());
        using var br = new BinaryReader(new MemoryStream(bytes));
        if (new string(br.ReadChars(4)) != "BRAN") { GD.PushError("[bran] bad mesh file"); return; }
        int nv = (int)br.ReadUInt32(), nf = (int)br.ReadUInt32(), nb = (int)br.ReadUInt32();
        var boneIds = new int[nb];
        for (int k = 0; k < nb; k++)
        {
            int len = br.ReadByte();
            boneIds[k] = s[new string(br.ReadChars(len))];
        }
        var pos = new Vector3[nv];
        for (int k = 0; k < nv; k++) pos[k] = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
        var col = new Color[nv];
        for (int k = 0; k < nv; k++) col[k] = new Color(br.ReadByte() / 255f, br.ReadByte() / 255f, br.ReadByte() / 255f);
        var cls = br.ReadBytes(nv);
        var b0 = br.ReadBytes(nv); var b1 = br.ReadBytes(nv); var wt = br.ReadBytes(nv);
        var idx = new int[nf * 3];
        for (int k = 0; k < idx.Length; k++) idx[k] = br.ReadUInt16();

        Refine(pos, col, cls, idx);

        // one part per material: a triangle goes to the material most of its corners have
        var parts = new Dictionary<Mat, (MeshBuilder mb, List<(int, int, float)> binds, Dictionary<int, int> map)>();
        for (int f = 0; f < nf; f++)
        {
            int a = idx[f * 3], b = idx[f * 3 + 1], c = idx[f * 3 + 2];
            int vote = cls[a] == cls[b] || cls[a] == cls[c] ? cls[a] : cls[b] == cls[c] ? cls[b] : cls[a];
            var mat = BranMat(vote);
            if (!parts.TryGetValue(mat, out var part)) parts[mat] = part = (new MeshBuilder(), new List<(int, int, float)>(), new Dictionary<int, int>());
            int Map(int v)
            {
                if (part.map.TryGetValue(v, out int m)) return m;
                m = part.mb.Add(pos[v], Vector3.Up, col[v]);
                part.binds.Add((boneIds[b0[v]], boneIds[b1[v]], wt[v] / 255f));
                part.map[v] = m;
                return m;
            }
            part.mb.Tri(Map(a), Map(b), Map(c));
        }
        foreach (var (mat, part) in parts)
        {
            part.mb.SmoothNormals();
            s.Skinned(part.mb, part.binds.ToArray(), mat);
        }
    }
}
