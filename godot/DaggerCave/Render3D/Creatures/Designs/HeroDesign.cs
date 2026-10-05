using System;
using Godot;

namespace DaggerCave;

/// <summary>
/// The five heroes, one rig. The Swordsman (Bran, see HeroDesign.Bran.cs): a broad, bare-armed warrior with short dark hair and
/// stubble, a slate cowl and torn cloak, a fur mantle on one shoulder, leather straps over a dark tunic,
/// a ragged tabard, and a longsword. The Warden: great helm, mail under a blue tabard with a gold sash, a shortsword and
/// a kite shield. The Vitalist: a deep green robe and cowl over a bone mask with eyes that glow,
/// a blood-red sash, and a gnarled staff whose crystal flares with every spell. The Elementalist:
/// a violet robe and a tall pointed hood, an ember stole, and a pale staff crowned with an orb in
/// a bronze cage, burning orange (or, with Frostbolt, glowing frost-blue) and flaring with every
/// spell. The Rogue: a plum hood and a dark mask over the face, a charcoal jerkin and a short
/// cape, and a dagger in each hand (each gone from the hand while it's thrown). All five carry a
/// lantern at the hip that lights the cave around them.
///
/// Animation: the legs follow real movement (a gait phase driven by ground speed, air poses by
/// vertical speed) while the upper body plays the clip, so strikes while running look right.
/// Sword swings follow the gameplay swing exactly: wind-up, woosh and follow-through beats at the
/// clip's frame boundaries, sweeping the same arc through the actual aim.
/// </summary>
public sealed partial class HeroDesign : CreatureDesign
{
    private readonly HeroKind _kind;
    public override float LifeScale => 0f;
    private readonly bool _warden, _vitalist, _elementalist, _caster, _rogue, _swordsman, _aegis, _shifter;
    public HeroDesign(HeroKind kind)
    {
        _kind = kind;
        _warden = kind == HeroKind.Warden;
        _vitalist = kind == HeroKind.Vitalist;
        _elementalist = kind == HeroKind.Elementalist;
        _rogue = kind == HeroKind.Rogue;
        _swordsman = kind == HeroKind.Swordsman;
        _aegis = kind == HeroKind.Aegis;
        _shifter = kind == HeroKind.ShapeShifter;
        // (the casters hold a staff, wear a robe, and share their poses)
        _caster = _vitalist || _elementalist || _aegis;
        _st = StyleFor(kind);
    }
    public override string Name => Player.SheetName(_kind);
    public override float Cell => 0.013f;
    public override float ThreeQuarter => 20f;
    public override float FloorY => Floor;

    public override CreatureLook Look => new()
    {
        Eye = _shifter ? new Color(0.92f, 0.95f, 1f) : _vitalist ? new Color(0.5f, 1f, 0.42f) : _elementalist ? new Color(1f, 0.72f, 0.4f) : _aegis ? new Color(0.6f, 1f, 0.92f) : new Color(1f, 0.9f, 0.75f),
        EyeEnergy = _shifter ? 1.6f : _vitalist ? 2.4f : _elementalist ? 1.2f : _aegis ? 1.0f : 0.5f,
        Glow = new Color(1f, 0.68f, 0.32f), GlowEnergy = 6f,
        Rim = new Color(0.55f, 0.68f, 0.95f), RimEnergy = 0.28f,
        DetailScale = 34f, DetailStrength = 0.6f,
    };

    private const float Floor = -0.81f;
    private static readonly Vector3 LanternAt = new(0.02f, -0.02f, -0.19f);
    /// <summary>Where the hand grips (rest space); a caster's staff runs through it along +X, crystal (or orb) forward.</summary>
    private static readonly Vector3 Grip = new(0.02f, -0.11f, 0.2f);
    private const float StaffUp = 0.6f, StaffDown = 0.98f, GemAt = 0.72f;
    private static readonly Color Life = new(0.45f, 1f, 0.4f);
    /// <summary>The Elementalist's orb: its fire, and its frost.</summary>
    /// <summary>The Aegis's staff disc: a pale teal light.</summary>
    private static readonly Color AegisGlow = new(0.55f, 1f, 0.9f);
    private static readonly Color OrbFire = new(1f, 0.55f, 0.16f), OrbFrost = new(0.62f, 0.88f, 1f);

    private int hips, spine, chest, neck, head, cape0, cape1, cape2, cape3, scarf0, scarf1, scarf2;
    private readonly int[] clav = new int[2], uarm = new int[2], farm = new int[2], hand = new int[2], thigh = new int[2], shin = new int[2], foot = new int[2];

    protected override void OnBonesBound()
    {
        hips = B("hips"); spine = B("spine"); chest = B("chest"); neck = B("neck"); head = B("head");
        cape0 = B("cape0"); cape1 = B("cape1"); cape2 = B("cape2"); cape3 = B("cape3");
        scarf0 = B("scarf0"); scarf1 = B("scarf1"); scarf2 = B("scarf2");
        for (int k = 0; k < 2; k++)
        {
            string s = k == 0 ? "_r" : "_l";
            clav[k] = B("clav" + s); uarm[k] = B("uarm" + s); farm[k] = B("farm" + s); hand[k] = B("hand" + s);
            thigh[k] = B("thigh" + s); shin[k] = B("shin" + s); foot[k] = B("foot" + s);
        }
    }

    // ================================================================== sculpt

    private static Color C(float r, float g, float b) => new(r, g, b);

    public override void Sculpt(Sculptor s)
    {
        var skin = C(0.72f, 0.52f, 0.42f);
        var dark = C(0.12f, 0.13f, 0.15f);
        // dark, oiled leather: well apart from the skin tone under the warm lantern light
        var leather = C(0.25f, 0.15f, 0.085f);
        var leatherDk = C(0.15f, 0.095f, 0.06f);
        var steel = C(0.62f, 0.64f, 0.67f);
        var gold = C(0.78f, 0.58f, 0.24f);
        var robe = C(0.075f, 0.18f, 0.115f);
        var boneWhite = C(0.8f, 0.76f, 0.66f);
        var blood = C(0.46f, 0.05f, 0.06f);
        var violet = C(0.19f, 0.11f, 0.3f);
        var ember = C(0.8f, 0.34f, 0.08f);
        if (_elementalist) robe = violet;
        if (_aegis) robe = C(0.5f, 0.47f, 0.4f);
        if (_shifter) robe = C(0.36f, 0.38f, 0.42f);
        var cloak = _shifter ? C(0.2f, 0.22f, 0.27f) : _warden ? C(0.1f, 0.16f, 0.4f) : _vitalist ? C(0.045f, 0.1f, 0.068f) : _elementalist ? C(0.1f, 0.06f, 0.17f) : _aegis ? C(0.05f, 0.27f, 0.27f) : _rogue ? C(0.16f, 0.08f, 0.17f) : C(0.07f, 0.27f, 0.28f);
        if (_rogue) { leather = C(0.12f, 0.1f, 0.12f); leatherDk = C(0.07f, 0.06f, 0.075f); }
        var body = _warden ? C(0.42f, 0.44f, 0.47f) : _caster || _shifter ? robe : leather; // mail, robe, jerkin
        var bodyMat = _warden ? Mat.Metal : _caster || _shifter ? Mat.Cloth : Mat.Leather;
        if (_vitalist) skin = C(0.6f, 0.5f, 0.45f); // pale, bloodless hands

        int hipsB = s.Bone("hips", -1, new(0, 0.02f, 0));
        int spineB = s.Bone("spine", hipsB, new(0, 0.14f, 0));
        int chestB = s.Bone("chest", spineB, new(0, 0.30f, 0));
        int neckB = s.Bone("neck", chestB, new(0.01f, 0.50f, 0));
        int headB = s.Bone("head", neckB, new(0.02f, 0.58f, 0));
        for (int k = 0; k < 2; k++)
        {
            float z = k == 0 ? 1 : -1;
            string sfx = k == 0 ? "_r" : "_l";
            int cl = s.Bone("clav" + sfx, chestB, new(0, 0.47f, 0.05f * z));
            int ua = s.Bone("uarm" + sfx, cl, new(0, 0.46f, 0.19f * z));
            int fa = s.Bone("farm" + sfx, ua, new(0, 0.19f, 0.2f * z));
            s.Bone("hand" + sfx, fa, new(0, -0.05f, 0.2f * z));
            int th = s.Bone("thigh" + sfx, hipsB, new(0, 0.0f, 0.095f * z));
            int sh = s.Bone("shin" + sfx, th, new(0.01f, -0.39f, 0.095f * z));
            s.Bone("foot" + sfx, sh, new(-0.01f, -0.75f, 0.095f * z));
        }
        int c0 = s.Bone("cape0", chestB, new(-0.09f, 0.46f, 0));
        int c1 = s.Bone("cape1", c0, new(-0.12f, 0.2f, 0));
        int c2 = s.Bone("cape2", c1, new(-0.14f, -0.08f, 0));
        int c3 = s.Bone("cape3", c2, new(-0.15f, -0.35f, 0));
        int s0 = s.Bone("scarf0", neckB, new(-0.05f, 0.57f, 0));
        int s1 = s.Bone("scarf1", s0, new(-0.2f, 0.56f, 0.02f));
        int s2 = s.Bone("scarf2", s1, new(-0.35f, 0.53f, 0.03f));

        if (_swordsman)
        {
            SculptBran(s);
            SculptLantern(s, hipsB);
            return;
        }

        // ---- torso
        s.Egg(hipsB, new(0, 0.03f, 0), new(0.125f, 0.1f, 0.145f), dark, Mat.Cloth, 0.04f);
        s.Limb(spineB, new(0, 0.05f, 0), new(0, 0.26f, 0), 0.118f, 0.138f, body, bodyMat, 0.04f);
        s.Egg(chestB, new(0.012f, 0.36f, 0), new(0.13f, 0.15f, _warden ? 0.2f : 0.185f), body, bodyMat, 0.05f);
        s.Limb(chestB, new(-0.01f, 0.445f, -0.15f), new(-0.01f, 0.445f, 0.15f), 0.072f, 0.072f, body, bodyMat, 0.04f);
        if (_warden)
        {
            // tabard over the mail, gold sash across the chest
            s.Egg(chestB, new(0.03f, 0.3f, 0), new(0.125f, 0.2f, 0.17f), cloak, Mat.Cloth, 0.02f);
            s.Egg(hipsB, new(0.02f, -0.1f, 0), new(0.13f, 0.17f, 0.15f), cloak, Mat.Cloth, 0.03f);
            s.Limb(chestB, new(0.1f, 0.44f, -0.16f), new(0.12f, 0.12f, 0.15f), 0.028f, 0.028f, gold, Mat.Cloth, 0.01f);
        }
        else if (_vitalist)
        {
            // a heavy mantle over the shoulders, the robe falling to mid-thigh, a blood-red sash
            s.Egg(chestB, new(0f, 0.44f, 0), new(0.15f, 0.065f, 0.205f), cloak, Mat.Cloth, 0.03f);
            s.Egg(hipsB, new(0.012f, -0.15f, 0), new(0.15f, 0.21f, 0.168f), robe, Mat.Cloth, 0.03f);
            s.Limb(chestB, new(0.1f, 0.43f, 0.15f), new(0.11f, 0.1f, -0.15f), 0.024f, 0.022f, blood, Mat.Cloth, 0.008f);
            s.Limb(hipsB, new(0.14f, 0.07f, -0.1f), new(0.15f, -0.2f, -0.12f), 0.018f, 0.012f, blood, Mat.Cloth, 0.006f);
        }
        else if (_elementalist)
        {
            // a mantle over the shoulders, the robe falling to mid-thigh, an ember sash with a gold clasp
            s.Egg(chestB, new(0f, 0.44f, 0), new(0.148f, 0.06f, 0.2f), cloak, Mat.Cloth, 0.03f);
            s.Egg(hipsB, new(0.012f, -0.15f, 0), new(0.15f, 0.21f, 0.168f), robe, Mat.Cloth, 0.03f);
            s.Limb(chestB, new(0.1f, 0.43f, -0.15f), new(0.11f, 0.1f, 0.15f), 0.022f, 0.02f, ember, Mat.Cloth, 0.008f);
            s.Ball(chestB, new(0.125f, 0.27f, 0.0f), 0.018f, gold, Mat.Gold, 0.004f);
            s.Limb(hipsB, new(0.14f, 0.07f, 0.1f), new(0.15f, -0.24f, 0.12f), 0.018f, 0.01f, ember, Mat.Cloth, 0.006f);
            // runes stitched in gold down the robe's front
            s.Limb(hipsB, new(0.16f, 0.02f, 0f), new(0.155f, -0.3f, 0f), 0.006f, 0.005f, gold, Mat.Gold, 0.002f);
        }
        else if (_aegis)
        {
            // a teal mantle over the shoulders, an ivory robe to mid-thigh, a gold sash and an emblem on the breast
            s.Egg(chestB, new(0f, 0.44f, 0), new(0.15f, 0.065f, 0.205f), cloak, Mat.Cloth, 0.03f);
            s.Egg(hipsB, new(0.012f, -0.15f, 0), new(0.15f, 0.21f, 0.168f), robe, Mat.Cloth, 0.03f);
            s.Limb(chestB, new(0.1f, 0.43f, -0.15f), new(0.11f, 0.1f, 0.15f), 0.024f, 0.022f, gold, Mat.Gold, 0.008f);
            s.Limb(hipsB, new(0.14f, 0.07f, 0.1f), new(0.15f, -0.22f, 0.12f), 0.018f, 0.012f, cloak, Mat.Cloth, 0.006f);
            s.Ball(chestB, new(0.126f, 0.3f, 0f), 0.032f, gold, Mat.Gold, 0.006f);
            s.Ball(chestB, new(0.14f, 0.3f, 0f), 0.014f, C(0.5f, 0.95f, 0.88f), Mat.Crystal, 0.004f);
        }
        else
        {
            // leather flaps over the hips
            s.Egg(hipsB, new(0.075f, -0.09f, 0), new(0.045f, 0.12f, 0.12f), leather, Mat.Leather, 0.02f);
            s.Egg(hipsB, new(-0.075f, -0.09f, 0), new(0.045f, 0.12f, 0.12f), leather, Mat.Leather, 0.02f);
            // a strap across the chest
            s.Limb(chestB, new(0.1f, 0.43f, 0.15f), new(0.11f, 0.12f, -0.15f), 0.018f, 0.018f, leatherDk, Mat.Leather, 0.008f);
        }
        s.Egg(hipsB, new(0, 0.075f, 0), new(0.14f, 0.03f, 0.158f), leatherDk, Mat.Leather, 0.01f);
        s.Block(hipsB, new(0.14f, 0.075f, 0), new(0.012f, 0.022f, 0.028f), 0.004f, _vitalist ? boneWhite : gold, _vitalist ? Mat.Bone : Mat.Gold, 0.004f);

        // ---- neck and head
        s.Limb(neckB, new(0.01f, 0.47f, 0), new(0.02f, 0.6f, 0), 0.056f, 0.052f, skin, Mat.Skin, 0.03f);
        if (_warden)
        {
            // a great helm with a visor slit and a crest
            s.Limb(headB, new(0.025f, 0.62f, 0), new(0.025f, 0.8f, 0), 0.105f, 0.1f, steel, Mat.Metal, 0.03f);
            s.Egg(headB, new(0.02f, 0.8f, 0), new(0.1f, 0.06f, 0.095f), steel, Mat.Metal, 0.03f);
            s.CarveLimb(headB, new(0.14f, 0.725f, -0.06f), new(0.14f, 0.725f, 0.06f), 0.012f, 0.012f, 0.004f);
            s.Limb(headB, new(0.125f, 0.7f, 0), new(0.13f, 0.63f, 0), 0.012f, 0.01f, steel, Mat.Metal, 0.008f);
            s.Limb(headB, new(-0.02f, 0.87f, 0), new(-0.09f, 0.83f, 0), 0.03f, 0.02f, C(0.55f, 0.06f, 0.05f), Mat.Cloth, 0.02f);
            s.Eye(headB, new(0.13f, 0.725f, 0.035f), 0.009f, C(0.9f, 0.85f, 0.7f), 0.4f);
            s.Eye(headB, new(0.13f, 0.725f, -0.035f), 0.009f, C(0.9f, 0.85f, 0.7f), 0.4f);
        }
        else if (_vitalist)
        {
            // a deep cowl, and in its shadow a bone mask: hollow eyes lit green from within, a slit mouth
            s.Egg(headB, new(-0.002f, 0.748f, 0), new(0.138f, 0.152f, 0.126f), cloak, Mat.Cloth, 0.02f);
            s.Limb(headB, new(-0.07f, 0.76f, 0), new(-0.12f, 0.52f, 0), 0.09f, 0.12f, cloak, Mat.Cloth, 0.04f);
            s.Limb(headB, new(0.03f, 0.88f, 0), new(-0.08f, 0.9f, 0), 0.03f, 0.012f, cloak, Mat.Cloth, 0.03f);
            s.CarveBall(headB, new(0.17f, 0.7f, 0), 0.102f, 0.02f);
            s.Egg(headB, new(0.036f, 0.705f, 0), new(0.09f, 0.11f, 0.08f), boneWhite, Mat.Bone, 0.012f);
            s.Limb(headB, new(0.112f, 0.735f, 0), new(0.128f, 0.69f, 0), 0.014f, 0.01f, boneWhite, Mat.Bone, 0.01f);
            s.CarveBall(headB, new(0.12f, 0.73f, 0.035f), 0.02f, 0.006f);
            s.CarveBall(headB, new(0.12f, 0.73f, -0.035f), 0.02f, 0.006f);
            s.CarveLimb(headB, new(0.124f, 0.664f, -0.024f), new(0.124f, 0.664f, 0.024f), 0.005f, 0.005f, 0.003f);
            s.Eye(headB, new(0.106f, 0.73f, 0.035f), 0.011f, Life, 1f);
            s.Eye(headB, new(0.106f, 0.73f, -0.035f), 0.011f, Life, 1f);
        }
        else if (_rogue)
        {
            // a close hood, and a dark cloth mask over the nose and mouth: only the eyes show
            s.Egg(headB, new(0.004f, 0.738f, 0), new(0.126f, 0.14f, 0.116f), cloak, Mat.Cloth, 0.02f);
            s.Limb(headB, new(-0.07f, 0.75f, 0), new(-0.11f, 0.55f, 0), 0.082f, 0.1f, cloak, Mat.Cloth, 0.04f);
            s.CarveBall(headB, new(0.16f, 0.705f, 0), 0.098f, 0.02f);
            s.Egg(headB, new(0.03f, 0.705f, 0), new(0.092f, 0.112f, 0.082f), skin, Mat.Skin, 0.015f);
            s.Egg(headB, new(0.05f, 0.668f, 0), new(0.085f, 0.06f, 0.088f), C(0.08f, 0.07f, 0.09f), Mat.Cloth, 0.02f);
            s.Egg(neckB, new(0.03f, 0.625f, 0), new(0.078f, 0.05f, 0.09f), C(0.08f, 0.07f, 0.09f), Mat.Cloth, 0.025f);
            s.Eye(headB, new(0.112f, 0.73f, 0.034f), 0.0115f, C(0.85f, 0.8f, 0.6f), 0.35f);
            s.Eye(headB, new(0.112f, 0.73f, -0.034f), 0.0115f, C(0.85f, 0.8f, 0.6f), 0.35f);
        }
        else if (_elementalist)
        {
            // a tall hood whose point droops back, open at a weathered face with a grey beard, the
            // eyes catching the orb's light
            s.Egg(headB, new(0.0f, 0.745f, 0), new(0.134f, 0.15f, 0.122f), cloak, Mat.Cloth, 0.02f);
            s.Limb(headB, new(-0.07f, 0.76f, 0), new(-0.12f, 0.52f, 0), 0.088f, 0.118f, cloak, Mat.Cloth, 0.04f);
            s.Limb(headB, new(-0.01f, 0.86f, 0), new(-0.1f, 0.99f, 0), 0.075f, 0.03f, cloak, Mat.Cloth, 0.03f);
            s.Limb(headB, new(-0.1f, 0.99f, 0), new(-0.19f, 0.98f, 0), 0.03f, 0.008f, cloak, Mat.Cloth, 0.02f);
            s.CarveBall(headB, new(0.16f, 0.7f, 0), 0.098f, 0.02f);
            s.Egg(headB, new(0.03f, 0.705f, 0), new(0.09f, 0.11f, 0.08f), skin, Mat.Skin, 0.015f);
            s.Limb(headB, new(0.105f, 0.712f, 0), new(0.118f, 0.69f, 0), 0.013f, 0.009f, skin, Mat.Skin, 0.012f);
            s.Limb(headB, new(0.075f, 0.655f, 0), new(0.1f, 0.575f, 0), 0.05f, 0.018f, C(0.62f, 0.6f, 0.58f), Mat.Cloth, 0.02f);
            s.Eye(headB, new(0.11f, 0.728f, 0.034f), 0.011f, C(1f, 0.72f, 0.4f), 0.6f);
            s.Eye(headB, new(0.11f, 0.728f, -0.034f), 0.011f, C(1f, 0.72f, 0.4f), 0.6f);
            // an ember collar at the throat
            s.Egg(neckB, new(0.02f, 0.625f, 0), new(0.08f, 0.048f, 0.095f), ember, Mat.Cloth, 0.025f);
        }
        else if (_aegis)
        {
            // a pale hood thrown back from a calm face, a gold circlet across the brow with a teal stone
            s.Egg(headB, new(0.0f, 0.74f, 0), new(0.13f, 0.142f, 0.12f), robe, Mat.Cloth, 0.02f);
            s.Limb(headB, new(-0.07f, 0.75f, 0), new(-0.115f, 0.53f, 0), 0.085f, 0.115f, robe, Mat.Cloth, 0.04f);
            s.CarveBall(headB, new(0.16f, 0.705f, 0), 0.098f, 0.02f);
            s.Egg(headB, new(0.03f, 0.705f, 0), new(0.092f, 0.112f, 0.082f), skin, Mat.Skin, 0.015f);
            s.Limb(headB, new(0.105f, 0.712f, 0), new(0.116f, 0.695f, 0), 0.013f, 0.009f, skin, Mat.Skin, 0.012f);
            s.Limb(headB, new(0.085f, 0.795f, -0.085f), new(0.115f, 0.79f, 0), 0.011f, 0.011f, gold, Mat.Gold, 0.004f);
            s.Limb(headB, new(0.115f, 0.79f, 0), new(0.085f, 0.795f, 0.085f), 0.011f, 0.011f, gold, Mat.Gold, 0.004f);
            s.Ball(headB, new(0.12f, 0.79f, 0), 0.016f, C(0.5f, 0.95f, 0.88f), Mat.Crystal, 0.004f);
            s.Egg(neckB, new(0.03f, 0.625f, 0), new(0.078f, 0.05f, 0.09f), cloak, Mat.Cloth, 0.025f);
            s.Eye(headB, new(0.112f, 0.728f, 0.034f), 0.0115f, C(0.4f, 0.85f, 0.8f), 0.6f);
            s.Eye(headB, new(0.112f, 0.728f, -0.034f), 0.0115f, C(0.4f, 0.85f, 0.8f), 0.6f);
        }
        else
        {
            // hood, cut open at the face, draping into the cloak
            s.Egg(headB, new(0.005f, 0.735f, 0), new(0.128f, 0.142f, 0.118f), cloak, Mat.Cloth, 0.02f);
            s.Limb(headB, new(-0.07f, 0.75f, 0), new(-0.115f, 0.53f, 0), 0.085f, 0.115f, cloak, Mat.Cloth, 0.04f);
            s.CarveBall(headB, new(0.16f, 0.705f, 0), 0.098f, 0.02f);
            s.Egg(headB, new(0.03f, 0.705f, 0), new(0.092f, 0.112f, 0.082f), skin, Mat.Skin, 0.015f);
            s.Limb(headB, new(0.105f, 0.712f, 0), new(0.116f, 0.695f, 0), 0.013f, 0.009f, skin, Mat.Skin, 0.012f);
            s.Egg(neckB, new(0.03f, 0.625f, 0), new(0.078f, 0.05f, 0.09f), C(0.55f, 0.07f, 0.05f), Mat.Cloth, 0.025f);
            s.Eye(headB, new(0.112f, 0.728f, 0.034f), 0.0115f, C(0.12f, 0.09f, 0.07f), 0.15f);
            s.Eye(headB, new(0.112f, 0.728f, -0.034f), 0.0115f, C(0.12f, 0.09f, 0.07f), 0.15f);
        }
        // scarf knot and tail (the others have none, but keep the bones)
        var scarfCol = C(0.55f, 0.07f, 0.05f);
        if (!_warden && !_caster && !_rogue)
        {
            s.Ball(s0, new(-0.07f, 0.595f, 0), 0.042f, scarfCol, Mat.Cloth, 0.02f);
            s.Limb(s0, new(-0.07f, 0.59f, 0.005f), new(-0.2f, 0.57f, 0.025f), 0.04f, 0.034f, scarfCol, Mat.Cloth, 0.015f);
            s.Limb(s1, new(-0.2f, 0.57f, 0.025f), new(-0.36f, 0.54f, 0.035f), 0.034f, 0.018f, scarfCol, Mat.Cloth, 0.012f);
            s.Limb(s2, new(-0.36f, 0.54f, 0.035f), new(-0.44f, 0.51f, 0.04f), 0.018f, 0.008f, scarfCol, Mat.Cloth, 0.01f);
        }

        // ---- arms
        for (int k = 0; k < 2; k++)
        {
            float z = k == 0 ? 1 : -1;
            string sfx = k == 0 ? "_r" : "_l";
            int ua = s["uarm" + sfx], fa = s["farm" + sfx], hd = s["hand" + sfx];
            if (_caster)
            {
                s.Egg(ua, new(0, 0.45f, 0.19f * z), new(0.08f, 0.07f, 0.074f), cloak, Mat.Cloth, 0.02f);
                s.Limb(ua, new(0, 0.45f, 0.19f * z), new(0, 0.2f, 0.2f * z), 0.052f, 0.047f, robe, Mat.Cloth, 0.02f);
                s.Ball(fa, new(0, 0.19f, 0.2f * z), 0.046f, robe, Mat.Cloth, 0.015f);
                s.Limb(fa, new(0, 0.18f, 0.2f * z), new(0, -0.015f, 0.2f * z), 0.046f, 0.06f, robe, Mat.Cloth, 0.015f);
                s.Limb(hd, new(0.005f, -0.04f, 0.2f * z), new(0.018f, -0.125f, 0.2f * z), 0.03f, 0.024f, skin, Mat.Skin, 0.012f);
                continue;
            }
            s.Egg(ua, new(0, 0.45f, 0.19f * z), new(0.078f, 0.068f, 0.072f), _warden ? steel : leather, _warden ? Mat.Metal : Mat.Leather, 0.02f);
            s.Limb(ua, new(0, 0.45f, 0.19f * z), new(0, 0.2f, 0.2f * z), 0.05f, 0.043f, _warden ? body : dark, _warden ? Mat.Metal : Mat.Cloth, 0.02f);
            s.Ball(fa, new(0, 0.19f, 0.2f * z), 0.043f, _warden ? body : dark, Mat.Cloth, 0.015f);
            s.Limb(fa, new(0, 0.18f, 0.2f * z), new(0, -0.03f, 0.2f * z), 0.045f, 0.039f, _warden ? steel : leather, _warden ? Mat.Metal : Mat.Leather, 0.015f);
            s.Limb(hd, new(0.005f, -0.05f, 0.2f * z), new(0.018f, -0.125f, 0.2f * z), 0.036f, 0.03f, leatherDk, Mat.Leather, 0.012f);
        }

        // ---- legs
        for (int k = 0; k < 2; k++)
        {
            float z = k == 0 ? 1 : -1;
            string sfx = k == 0 ? "_r" : "_l";
            int th = s["thigh" + sfx], sh = s["shin" + sfx], ft = s["foot" + sfx];
            s.Limb(th, new(0, 0.0f, 0.095f * z), new(0.012f, -0.39f, 0.095f * z), 0.072f, 0.055f, dark, Mat.Cloth, 0.03f);
            s.Ball(sh, new(0.022f, -0.39f, 0.095f * z), 0.05f, _warden ? steel : leather, _warden ? Mat.Metal : Mat.Leather, 0.012f);
            s.Limb(sh, new(0.01f, -0.39f, 0.095f * z), new(-0.01f, -0.73f, 0.095f * z), 0.05f, 0.04f, dark, Mat.Cloth, 0.015f);
            s.Limb(sh, new(0.002f, -0.52f, 0.095f * z), new(-0.01f, -0.765f, 0.095f * z), 0.058f, 0.05f, leatherDk, Mat.Leather, 0.012f);
            s.Egg(sh, new(0.002f, -0.52f, 0.095f * z), new(0.066f, 0.028f, 0.066f), leather, Mat.Leather, 0.01f);
            s.Limb(ft, new(-0.03f, -0.775f, 0.095f * z), new(0.115f, -0.785f, 0.095f * z), 0.046f, 0.036f, leatherDk, Mat.Leather, 0.012f);
        }

        // ---- cloak down the back: draped cloth, wrapping forward at the sides and flaring a
        // little toward the hem, with folds that deepen as it falls
        {
            (float y, float w, float x, float wrap)[] rowSpec =
            {
                (0.5f, 0.15f, -0.095f, 0.08f),
                (0.24f, 0.2f, -0.15f, 0.07f),
                (-0.06f, 0.212f, -0.168f, 0.055f),
                (-0.34f, 0.22f, -0.178f, 0.05f),
                (_caster ? -0.64f : _rogue ? -0.4f : -0.56f, 0.215f, -0.186f, 0.045f),
            };
            const int cols = 9;
            var grid = new Vector3[rowSpec.Length][];
            for (int r = 0; r < rowSpec.Length; r++)
            {
                var (y, w, x, wrap) = rowSpec[r];
                float fall = r / (float)(rowSpec.Length - 1);
                grid[r] = new Vector3[cols];
                for (int c = 0; c < cols; c++)
                {
                    float a = -1f + 2f * c / (cols - 1);
                    float fold = 0.016f * fall * MathF.Sin(a * MathF.PI * 2f + 0.6f);
                    float hem = r == rowSpec.Length - 1 ? 0.02f * MathF.Sin(a * MathF.PI * 3f + 1.1f) : 0f;
                    grid[r][c] = new Vector3(x + wrap * a * a - fold, y + hem, w * a);
                }
            }
            s.Cloth(new[] { c0, c1, c2, c3, c3 }, grid, cloak, new Vector3(-1, 0, 0), Mat.Cloth, 0.006f, 3);
        }

        // ---- equipment
        int handR = s["hand_r"], handL = s["hand_l"];
        if (_vitalist) SculptStaff(s, handR, blood);
        else if (_elementalist) SculptOrbStaff(s, handR, ember, gold);
        else if (_aegis) SculptWardStaff(s, handR, gold);
        else if (_shifter)
        {
            // a plain, pale staff carried like a blade (the cuts are a sword's)
            var grip = new Transform3D(Basis.Identity, new Vector3(0.016f, -0.1f, 0.2f));
            var staff = PropMeshes.Sword(1.05f, 0.05f, C(0.62f, 0.55f, 0.44f), C(0.78f, 0.8f, 0.86f), leatherDk, 0.04f);
            var sw = new MeshBuilder();
            sw.Append(staff, grip);
            s.Rigid(handR, sw, Mat.Leather);
        }
        else if (_rogue) { /* (the daggers are attachments, so they can leave the hands) */ }
        else
        {
            var grip = new Transform3D(Basis.Identity, new Vector3(0.016f, -0.1f, 0.2f));
            var sword = _warden
                ? PropMeshes.Sword(0.74f, 0.03f, C(0.82f, 0.84f, 0.88f), gold, leatherDk, 0.075f)
                : PropMeshes.Sword(1.05f, 0.032f, C(0.8f, 0.82f, 0.86f), steel, leatherDk, 0.11f);
            var sw = new MeshBuilder();
            sw.Append(sword, grip);
            s.Rigid(handR, sw, Mat.Metal);
        }
        if (_warden)
        {
            var shield = PropMeshes.KiteShield(0.62f, 0.4f, C(0.1f, 0.18f, 0.45f), gold, C(0.85f, 0.7f, 0.3f));
            var sh = new MeshBuilder();
            sh.Append(shield, new Transform3D(Basis.Identity, new Vector3(0.05f, -0.12f, -0.2f)));
            s.Rigid(handL, sh, Mat.Metal);
        }
        SculptLantern(s, hipsB);
    }

    /// <summary>The lantern at the left hip: its glass glows in the look's glow colour.</summary>
    private static void SculptLantern(Sculptor s, int hipsB)
    {
        var lantern = PropMeshes.Lantern(C(0.2f, 0.18f, 0.16f), C(1f, 0.8f, 0.5f));
        var frame = new MeshBuilder();
        var glass = new MeshBuilder();
        for (int k = 0; k < lantern.I.Count; k += 3)
        {
            bool isGlass = lantern.C[lantern.I[k]].A > 0.5f;
            var dst = isGlass ? glass : frame;
            int a = dst.Add(lantern.V[lantern.I[k]], lantern.N[lantern.I[k]], lantern.C[lantern.I[k]]);
            int b = dst.Add(lantern.V[lantern.I[k + 1]], lantern.N[lantern.I[k + 1]], lantern.C[lantern.I[k + 1]]);
            int c = dst.Add(lantern.V[lantern.I[k + 2]], lantern.N[lantern.I[k + 2]], lantern.C[lantern.I[k + 2]]);
            dst.Tri(a, b, c);
        }
        var at = new Transform3D(Basis.Identity, LanternAt);
        var fr = new MeshBuilder(); fr.Append(frame, at);
        var gl = new MeshBuilder(); gl.Append(glass, at);
        s.Rigid(hipsB, fr, Mat.Metal);
        s.Rigid(hipsB, gl, Mat.Crystal, 1f);
    }

    /// <summary>
    /// The Vitalist's staff, through the fist along +X: a gnarled shaft, a blood-red binding under
    /// the head, and three wooden prongs curling around the crystal (the crystal itself is a lit
    /// attachment, so it can flare with each spell).
    /// </summary>
    private static void SculptStaff(Sculptor s, int handR, Color blood)
    {
        var wood = new Color(0.27f, 0.18f, 0.1f);
        var rng = new Random(11);
        var path = new System.Collections.Generic.List<Vector3>();
        var radii = new System.Collections.Generic.List<float>();
        const int n = 9;
        for (int k = 0; k <= n; k++)
        {
            float t = k / (float)n;
            float x = -StaffDown + (StaffDown + StaffUp) * t;
            var wob = k == 0 || k == n ? Vector3.Zero : new Vector3(0, ((float)rng.NextDouble() - 0.5f) * 0.018f, ((float)rng.NextDouble() - 0.5f) * 0.014f);
            path.Add(Grip + new Vector3(x, 0, 0) + wob);
            radii.Add(0.017f + 0.005f * t + (k % 3 == 1 ? 0.004f : 0f));
        }
        var mb = new MeshBuilder();
        mb.Tube(path, radii, 7, wood);
        for (int k = 0; k < 3; k++)
        {
            float a = k * Mathf.Tau / 3f + 0.4f;
            var d = new Vector3(0, MathF.Cos(a), MathF.Sin(a));
            var prong = new[] { Grip + new Vector3(StaffUp - 0.04f, 0, 0) + d * 0.01f, Grip + new Vector3(GemAt - 0.07f, 0, 0) + d * 0.05f,
                                Grip + new Vector3(GemAt + 0.04f, 0, 0) + d * 0.058f, Grip + new Vector3(GemAt + 0.12f, 0, 0) + d * 0.022f };
            mb.Tube(prong, new[] { 0.011f, 0.009f, 0.007f, 0.004f }, 5, wood);
        }
        s.Rigid(handR, mb, Mat.Wood);
        var wrap = new MeshBuilder();
        wrap.Tube(new[] { Grip + new Vector3(StaffUp - 0.2f, 0, 0), Grip + new Vector3(StaffUp - 0.1f, 0, 0) }, new[] { 0.026f, 0.026f }, 8, blood);
        wrap.Tube(new[] { Grip + new Vector3(StaffUp - 0.2f, 0, 0), Grip + new Vector3(StaffUp - 0.32f, -0.06f, 0.012f) }, new[] { 0.01f, 0.005f }, 5, blood);
        s.Rigid(handR, wrap, Mat.Cloth);
    }

    /// <summary>
    /// The Elementalist's staff, through the fist along +X: a straight shaft of pale ash, an ember
    /// wrap under the head, and a bronze cage of four curling tines around the orb (the orb itself
    /// is a lit attachment, so it can burn, freeze and flare with each spell).
    /// </summary>
    private static void SculptOrbStaff(Sculptor s, int handR, Color ember, Color bronze)
    {
        var ash = new Color(0.72f, 0.66f, 0.56f);
        var path = new System.Collections.Generic.List<Vector3>();
        var radii = new System.Collections.Generic.List<float>();
        const int n = 6;
        for (int k = 0; k <= n; k++)
        {
            float t = k / (float)n;
            path.Add(Grip + new Vector3(-StaffDown + (StaffDown + StaffUp) * t, 0, 0));
            radii.Add(0.015f + 0.004f * t);
        }
        var mb = new MeshBuilder();
        mb.Tube(path, radii, 7, ash);
        s.Rigid(handR, mb, Mat.Wood);
        var cage = new MeshBuilder();
        for (int k = 0; k < 4; k++)
        {
            float a = k * Mathf.Tau / 4f + 0.3f;
            var d = new Vector3(0, MathF.Cos(a), MathF.Sin(a));
            var tine = new[] { Grip + new Vector3(StaffUp - 0.02f, 0, 0) + d * 0.012f, Grip + new Vector3(GemAt - 0.05f, 0, 0) + d * 0.058f,
                               Grip + new Vector3(GemAt + 0.05f, 0, 0) + d * 0.06f, Grip + new Vector3(GemAt + 0.11f, 0, 0) + d * 0.018f };
            cage.Tube(tine, new[] { 0.01f, 0.008f, 0.007f, 0.004f }, 5, bronze);
        }
        cage.Tube(new[] { Grip + new Vector3(StaffUp - 0.035f, 0, 0), Grip + new Vector3(StaffUp + 0.005f, 0, 0) }, new[] { 0.024f, 0.024f }, 8, bronze);
        s.Rigid(handR, cage, Mat.Gold);
        var wrap = new MeshBuilder();
        wrap.Tube(new[] { Grip + new Vector3(StaffUp - 0.2f, 0, 0), Grip + new Vector3(StaffUp - 0.08f, 0, 0) }, new[] { 0.023f, 0.023f }, 8, ember);
        wrap.Tube(new[] { Grip + new Vector3(StaffUp - 0.12f, 0, 0), Grip + new Vector3(StaffUp - 0.26f, -0.08f, -0.012f) }, new[] { 0.009f, 0.004f }, 5, ember);
        s.Rigid(handR, wrap, Mat.Cloth);
    }

    /// <summary>
    /// The Aegis's staff, through the fist along +X: a straight shaft of pale wood with a gold collar, and above it a gold ring
    /// standing in the view plane, holding a glowing disc (the disc is a lit attachment, so it flares with each ward).
    /// </summary>
    private static void SculptWardStaff(Sculptor s, int handR, Color gold)
    {
        var pale = new Color(0.55f, 0.5f, 0.42f);
        var path = new System.Collections.Generic.List<Vector3>();
        var radii = new System.Collections.Generic.List<float>();
        const int n = 6;
        for (int k = 0; k <= n; k++)
        {
            float t = k / (float)n;
            path.Add(Grip + new Vector3(-StaffDown + (StaffDown + StaffUp) * t, 0, 0));
            radii.Add(0.016f + 0.004f * t);
        }
        var mb = new MeshBuilder();
        mb.Tube(path, radii, 7, pale);
        s.Rigid(handR, mb, Mat.Wood);
        var metal = new MeshBuilder();
        metal.Tube(new[] { Grip + new Vector3(StaffUp - 0.05f, 0, 0), Grip + new Vector3(StaffUp, 0, 0) }, new[] { 0.026f, 0.026f }, 8, gold);
        // the ring: a circle in the view (X-Y) plane, centred on the disc
        var ring = new System.Collections.Generic.List<Vector3>();
        var rr = new System.Collections.Generic.List<float>();
        const int seg = 20;
        var centre = Grip + new Vector3(GemAt + 0.03f, 0.0f, 0);
        for (int k = 0; k <= seg; k++)
        {
            float a = k / (float)seg * Mathf.Tau;
            ring.Add(centre + new Vector3(MathF.Cos(a) * 0.092f, MathF.Sin(a) * 0.092f, 0));
            rr.Add(0.011f);
        }
        metal.Tube(ring, rr, 6, gold, false);
        // two small spokes joining the ring to the shaft
        metal.Tube(new[] { Grip + new Vector3(StaffUp, 0, 0), centre + new Vector3(-0.092f, 0, 0) }, new[] { 0.012f, 0.011f }, 5, gold);
        s.Rigid(handR, metal, Mat.Gold);
    }

    public override void Attach(CreatureModel m)
    {
        if (_aegis)
        {
            // the staff's disc: a flattened, glowing sphere inside the ring, and its light
            var hand = m.AttachTo("hand_r");
            hand.Name = "StaffHand";
            var discAt = Grip + new Vector3(GemAt + 0.03f, 0f, 0f) - new Vector3(0f, -0.05f, 0.2f);
            var discMat = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.7f, 1f, 0.95f, 0.85f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, Roughness = 0.2f,
                EmissionEnabled = true, Emission = AegisGlow, EmissionEnergyMultiplier = 2.6f, RimEnabled = true, Rim = 0.7f,
            };
            hand.AddChild(new MeshInstance3D
            {
                Name = "Disc", Mesh = new SphereMesh { Radius = 0.08f, Height = 0.16f, RadialSegments = 16, Rings = 8, Material = discMat },
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Position = discAt, Scale = new Vector3(1f, 1f, 0.3f),
            });
            hand.AddChild(new OmniLight3D
            {
                Name = "DiscLight", LightColor = AegisGlow, LightEnergy = 0.5f, OmniRange = 3.4f, OmniAttenuation = 1.4f,
                ShadowEnabled = false, LightVolumetricFogEnergy = 0.4f, Position = discAt,
            });
        }
        else if (_vitalist)
        {
            // the staff's crystal: its own glowing mesh and a small light, both flaring with spells
            var hand = m.AttachTo("hand_r");
            hand.Name = "StaffHand";
            var mb = new MeshBuilder();
            mb.Crystal(0.035f, 0.1f, 0.05f, Colors.White);
            var lower = new MeshBuilder();
            lower.Append(mb, new Transform3D(new Basis(Vector3.Right, MathF.PI), Vector3.Zero));
            mb.Append(lower, Transform3D.Identity);
            var gemMat = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.55f, 1f, 0.5f, 0.85f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, Roughness = 0.15f,
                EmissionEnabled = true, Emission = Life, EmissionEnergyMultiplier = 2.5f, RimEnabled = true, Rim = 0.6f,
            };
            // the crystal lies along the staff (the hand's +X), grown from the rest-space grip
            var gemAt = Grip + new Vector3(GemAt, 0, 0) - new Vector3(0f, -0.05f, 0.2f);
            hand.AddChild(new MeshInstance3D
            {
                Name = "Gem", Mesh = mb.ToMesh(gemMat), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                Transform = new Transform3D(new Basis(Vector3.Back, -MathF.PI / 2f), gemAt),
            });
            hand.AddChild(new OmniLight3D
            {
                Name = "GemLight", LightColor = Life, LightEnergy = 0.4f, OmniRange = 3.2f, OmniAttenuation = 1.4f,
                ShadowEnabled = false, LightVolumetricFogEnergy = 0.3f, Position = gemAt,
            });
        }
        else if (_elementalist)
        {
            // the staff's orb: a glowing sphere (a hot core inside a halo) and its light
            var hand = m.AttachTo("hand_r");
            hand.Name = "StaffHand";
            var orbAt = Grip + new Vector3(GemAt, 0, 0) - new Vector3(0f, -0.05f, 0.2f);
            var orbMat = new StandardMaterial3D
            {
                AlbedoColor = new Color(1f, 0.75f, 0.45f, 0.9f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, Roughness = 0.2f,
                EmissionEnabled = true, Emission = OrbFire, EmissionEnergyMultiplier = 3f, RimEnabled = true, Rim = 0.7f,
            };
            hand.AddChild(new MeshInstance3D
            {
                Name = "Orb", Mesh = new SphereMesh { Radius = 0.042f, Height = 0.084f, RadialSegments = 16, Rings = 8, Material = orbMat },
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Position = orbAt,
            });
            hand.AddChild(new OmniLight3D
            {
                Name = "OrbLight", LightColor = OrbFire, LightEnergy = 0.5f, OmniRange = 3.2f, OmniAttenuation = 1.4f,
                ShadowEnabled = false, LightVolumetricFogEnergy = 0.4f, Position = orbAt,
            });
        }
        else if (_rogue)
        {
            // a dagger in each hand (hidden while it's thrown)
            var mat = new StandardMaterial3D { VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true, Metallic = 0.8f, Roughness = 0.3f };
            var dagger = PropMeshes.Sword(0.3f, 0.026f, C(0.78f, 0.8f, 0.84f), C(0.6f, 0.46f, 0.2f), C(0.1f, 0.08f, 0.07f), 0.05f).ToMesh(mat);
            for (int k = 0; k < 2; k++)
            {
                var hand = m.AttachTo(k == 0 ? "hand_r" : "hand_l");
                hand.Name = k == 0 ? "DaggerR" : "DaggerL";
                hand.AddChild(new MeshInstance3D { Name = "Blade", Mesh = dagger, Position = new Vector3(0.016f, -0.05f, 0f) });
            }
        }
        else if (!_warden)
        {
            // the Charged Strike: a burning edge along the blade, shown while the blade is charged
            var hand = m.AttachTo("hand_r");
            hand.Name = "BladeHand";
            var edgeMat = new StandardMaterial3D
            {
                AlbedoColor = new Color(1f, 0.5f, 0.2f, 0.5f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BlendMode = BaseMaterial3D.BlendModeEnum.Add,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, CullMode = BaseMaterial3D.CullModeEnum.Disabled, NoDepthTest = false,
                EmissionEnabled = true, Emission = new Color(1f, 0.45f, 0.15f), EmissionEnergyMultiplier = 3f,
            };
            // along the blade (it runs down -Y from the grip), in the hand's local space
            var mid = new Vector3(0.016f, -0.1f - 0.03f - 0.52f, 0.2f) - new Vector3(0f, -0.05f, 0.2f);
            hand.AddChild(new MeshInstance3D
            {
                Name = "ChargeEdge", Mesh = new BoxMesh { Size = new Vector3(0.1f, 1.04f, 0.03f) }, MaterialOverride = edgeMat,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Position = mid, Visible = false,
            });
            hand.AddChild(new OmniLight3D
            {
                Name = "ChargeLight", LightColor = new Color(1f, 0.55f, 0.25f), LightEnergy = 0f, OmniRange = 2.8f, OmniAttenuation = 1.3f,
                ShadowEnabled = false, LightVolumetricFogEnergy = 0.4f, Position = mid, Visible = false,
            });
        }
        // the lantern lights the cave; the hero's own body doesn't block it
        m.Body.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        var at = m.AttachTo("hips");
        var lampAt = LanternAt + new Vector3(0.12f, -0.02f, 0.25f);
        var col = new Color(1f, 0.76f, 0.48f);
        at.AddChild(new OmniLight3D
        {
            LightColor = col, LightEnergy = 2.9f, OmniRange = 15f, OmniAttenuation = 1.25f,
            ShadowEnabled = true, LightVolumetricFogEnergy = 0f, LightSize = 0.08f, ShadowBias = 0.06f,
            Position = lampAt,
        });
        // its glow in the cave's haze comes from a twin that lights only the fog (the fog ignores
        // cull masks) and casts no shadows: shadowed fog breaks up into streaks at this froxel size
        at.AddChild(new OmniLight3D
        {
            LightColor = col, LightEnergy = 2.9f, OmniRange = 15f, OmniAttenuation = 1.25f,
            ShadowEnabled = false, LightVolumetricFogEnergy = 1.2f, LightCullMask = 0, LightSpecular = 0f,
            Position = lampAt,
        });
    }

    public override void Frame(CreatureModel m, in AnimInput a)
    {
        SampleBlade(m, a);
        var player = a.Owner as Player;
        if (_vitalist)
        {
            var gem = m.GetNodeOrNull<MeshInstance3D>("Pivot/Skeleton/StaffHand/Gem");
            var light = m.GetNodeOrNull<OmniLight3D>("Pivot/Skeleton/StaffHand/GemLight");
            if (gem == null || light == null) return;
            float glow = player?.CastGlow ?? 0f;
            bool dead = player == null || player.Dead;
            float pulse = 0.85f + 0.15f * MathF.Sin(a.Time * 2.6f);
            // the crystal takes the colour of the spell it just cast: pink for a heal, crimson for
            // stolen life, its own green at rest and for the hex
            var spell = player?.LastCast switch
            {
                "heal" => Player.HealColor,
                "drain" or "rupture" => new Color(0.6f, 1f, 0.55f),
                _ => Life,
            };
            var col = Life.Lerp(spell, Math.Clamp(glow * 1.6f, 0f, 1f));
            if (gem.Mesh.SurfaceGetMaterial(0) is StandardMaterial3D mat)
            {
                mat.EmissionEnergyMultiplier = dead ? 0.4f : (1.8f + 7f * glow) * pulse;
                mat.Emission = col;
            }
            light.LightColor = col;
            light.LightEnergy = dead ? 0f : (0.35f + 2.6f * glow) * pulse;
            light.OmniRange = 3.2f + 2.5f * glow;
        }
        else if (_aegis)
        {
            var disc = m.GetNodeOrNull<MeshInstance3D>("Pivot/Skeleton/StaffHand/Disc");
            var light = m.GetNodeOrNull<OmniLight3D>("Pivot/Skeleton/StaffHand/DiscLight");
            if (disc == null || light == null) return;
            float glow = player?.CastGlow ?? 0f;
            bool dead = player == null || player.Dead;
            float pulse = 0.85f + 0.15f * MathF.Sin(a.Time * 2.2f);
            // teal at rest; gold for the burden, the ward bolt's warm white when it flings one
            var spell = player?.LastCast switch
            {
                "burden" => new Color(1f, 0.85f, 0.45f),
                "ward" => new Color(1f, 0.93f, 0.6f),
                "barrier" => new Color(0.75f, 0.92f, 1f),
                _ => AegisGlow,
            };
            var col = AegisGlow.Lerp(spell, Math.Clamp(glow * 1.6f, 0f, 1f));
            if (disc.Mesh is SphereMesh sm && sm.Material is StandardMaterial3D mat)
            {
                mat.Emission = col;
                mat.EmissionEnergyMultiplier = dead ? 0.3f : (2.2f + 7f * glow) * pulse;
            }
            disc.Scale = new Vector3(1f + 0.3f * glow, 1f + 0.3f * glow, 0.3f);
            light.LightColor = col;
            light.LightEnergy = dead ? 0f : (0.45f + 2.8f * glow) * pulse;
            light.OmniRange = 3.4f + 2.5f * glow;
        }
        else if (_elementalist)
        {
            var orb = m.GetNodeOrNull<MeshInstance3D>("Pivot/Skeleton/StaffHand/Orb");
            var light = m.GetNodeOrNull<OmniLight3D>("Pivot/Skeleton/StaffHand/OrbLight");
            if (orb == null || light == null) return;
            float glow = player?.CastGlow ?? 0f;
            bool dead = player == null || player.Dead;
            bool frost = player?.FrostElement ?? false;
            // its own element at rest (fire, or with Frostbolt frost); the spell it just cast when it flares
            var rest = frost ? OrbFrost : OrbFire;
            var spell = player?.LastCast switch
            {
                "fire" or "firestorm" or "cinder" => OrbFire,
                "frost" or "blizzard" or "snap" => OrbFrost,
                "updraft" => new Color(0.85f, 0.95f, 1f),
                _ => rest,
            };
            var col = rest.Lerp(spell, Math.Clamp(glow * 1.6f, 0f, 1f));
            // fire flickers; frost breathes slowly
            float live = frost ? 0.88f + 0.12f * MathF.Sin(a.Time * 1.8f) : 0.8f + 0.2f * MathF.Sin(a.Time * 13f) * MathF.Sin(a.Time * 5.1f);
            if (orb.Mesh is SphereMesh sm && sm.Material is StandardMaterial3D mat)
            {
                mat.Emission = col;
                mat.EmissionEnergyMultiplier = dead ? 0.3f : (2.2f + 7f * glow) * live;
            }
            orb.Scale = Vector3.One * (1f + 0.35f * glow);
            light.LightColor = col;
            light.LightEnergy = dead ? 0f : (0.45f + 2.8f * glow) * live;
            light.OmniRange = 3.2f + 2.5f * glow;
        }
        else if (_rogue)
        {
            var r = m.GetNodeOrNull<MeshInstance3D>("Pivot/Skeleton/DaggerR/Blade");
            var l = m.GetNodeOrNull<MeshInstance3D>("Pivot/Skeleton/DaggerL/Blade");
            if (r != null) r.Visible = player == null || player.DaggerInHand(0);
            if (l != null) l.Visible = player == null || player.DaggerInHand(1);
        }
        else if (!_warden)
        {
            var edge = m.GetNodeOrNull<MeshInstance3D>("Pivot/Skeleton/BladeHand/ChargeEdge");
            var light = m.GetNodeOrNull<OmniLight3D>("Pivot/Skeleton/BladeHand/ChargeLight");
            if (edge == null || light == null) return;
            bool on = player != null && !player.Dead && (player.Charged > 0 || player.SwingCharged);
            edge.Visible = on;
            light.Visible = on;
            if (!on) return;
            float flick = 0.75f + 0.25f * MathF.Sin(a.Time * 23f) * MathF.Sin(a.Time * 7.3f);
            if (edge.MaterialOverride is StandardMaterial3D em) em.EmissionEnergyMultiplier = 2.2f + 1.6f * flick;
            light.LightEnergy = 1.1f * flick;
        }
    }
}
