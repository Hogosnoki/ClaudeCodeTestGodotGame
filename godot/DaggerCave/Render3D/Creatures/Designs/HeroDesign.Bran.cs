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
