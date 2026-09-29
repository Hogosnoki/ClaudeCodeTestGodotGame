using System;
using Godot;

namespace DaggerCave;

/// <summary>
/// The five heroes, one rig. The Swordsman: hood and teal cloak, a red scarf over the face, a
/// long sword. The Warden: great helm, mail under a blue tabard with a gold sash, a shortsword and
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
public sealed class HeroDesign : CreatureDesign
{
    private readonly HeroKind _kind;
    private readonly bool _warden, _vitalist, _elementalist, _caster, _rogue;
    public HeroDesign(HeroKind kind)
    {
        _kind = kind;
        _warden = kind == HeroKind.Warden;
        _vitalist = kind == HeroKind.Vitalist;
        _elementalist = kind == HeroKind.Elementalist;
        _rogue = kind == HeroKind.Rogue;
        // (the two casters hold a staff, wear a robe, and share their poses)
        _caster = _vitalist || _elementalist;
    }
    public override string Name => Player.SheetName(_kind);
    public override float Cell => 0.013f;
    public override float ThreeQuarter => 20f;
    public override float FloorY => Floor;

    public override CreatureLook Look => new()
    {
        Eye = _vitalist ? new Color(0.5f, 1f, 0.42f) : _elementalist ? new Color(1f, 0.72f, 0.4f) : new Color(1f, 0.9f, 0.75f),
        EyeEnergy = _vitalist ? 2.4f : _elementalist ? 1.2f : 0.5f,
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
        var cloak = _warden ? C(0.1f, 0.16f, 0.4f) : _vitalist ? C(0.045f, 0.1f, 0.068f) : _elementalist ? C(0.1f, 0.06f, 0.17f) : _rogue ? C(0.16f, 0.08f, 0.17f) : C(0.07f, 0.27f, 0.28f);
        if (_rogue) { leather = C(0.12f, 0.1f, 0.12f); leatherDk = C(0.07f, 0.06f, 0.075f); }
        var body = _warden ? C(0.42f, 0.44f, 0.47f) : _caster ? robe : leather; // mail, robe, jerkin
        var bodyMat = _warden ? Mat.Metal : _caster ? Mat.Cloth : Mat.Leather;
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
        else if (_rogue) { /* (the daggers are attachments, so they can leave the hands) */ }
        else
        {
            var grip = new Transform3D(Basis.Identity, new Vector3(0.016f, -0.1f, 0.2f));
            var sword = _warden
                ? PropMeshes.Sword(0.58f, 0.03f, C(0.82f, 0.84f, 0.88f), gold, leatherDk, 0.075f)
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
        // the lantern at the left hip (its glass glows in the look's glow colour)
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

    public override void Attach(CreatureModel m)
    {
        if (_vitalist)
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
                "drain" or "rupture" => new Color(1f, 0.22f, 0.3f),
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

    // ================================================================== animation

    private struct Body
    {
        public float Lean, Twist, Bank, HeadPitch, HeadYaw, Roll;
        public Vector3 Root;
        public float SR, AR, ER, WR, WRy;
        public float SL, AL, EL, WL, WLy;
        public float HR, KR, AnR, HRx, HL, KL, AnL, HLx;
    }

    private static Body Lerp(Body a, Body b, float t)
    {
        if (t <= 0f) return a;
        if (t >= 1f) return b;
        Body r;
        r.Lean = Mathf.Lerp(a.Lean, b.Lean, t); r.Twist = Mathf.Lerp(a.Twist, b.Twist, t); r.Bank = Mathf.Lerp(a.Bank, b.Bank, t);
        r.HeadPitch = Mathf.Lerp(a.HeadPitch, b.HeadPitch, t); r.HeadYaw = Mathf.Lerp(a.HeadYaw, b.HeadYaw, t); r.Roll = Mathf.Lerp(a.Roll, b.Roll, t);
        r.Root = a.Root.Lerp(b.Root, t);
        r.SR = Mathf.Lerp(a.SR, b.SR, t); r.AR = Mathf.Lerp(a.AR, b.AR, t); r.ER = Mathf.Lerp(a.ER, b.ER, t); r.WR = Mathf.Lerp(a.WR, b.WR, t); r.WRy = Mathf.Lerp(a.WRy, b.WRy, t);
        r.SL = Mathf.Lerp(a.SL, b.SL, t); r.AL = Mathf.Lerp(a.AL, b.AL, t); r.EL = Mathf.Lerp(a.EL, b.EL, t); r.WL = Mathf.Lerp(a.WL, b.WL, t); r.WLy = Mathf.Lerp(a.WLy, b.WLy, t);
        r.HR = Mathf.Lerp(a.HR, b.HR, t); r.KR = Mathf.Lerp(a.KR, b.KR, t); r.AnR = Mathf.Lerp(a.AnR, b.AnR, t); r.HRx = Mathf.Lerp(a.HRx, b.HRx, t);
        r.HL = Mathf.Lerp(a.HL, b.HL, t); r.KL = Mathf.Lerp(a.KL, b.KL, t); r.AnL = Mathf.Lerp(a.AnL, b.AnL, t); r.HLx = Mathf.Lerp(a.HLx, b.HLx, t);
        return r;
    }

    private Body Idle(float time)
    {
        float b = MathF.Sin(time * 1.9f);
        var o = new Body
        {
            Lean = 5 + b * 1.2f, HeadPitch = 2 - b, Root = new Vector3(0, -0.025f + b * 0.004f, 0),
            SR = 24 + b * 2, ER = 34, WR = 58, AR = 8,
            SL = -2 - b * 2, EL = 14, AL = 8,
            HR = 12, KR = 16, HRx = 5, HL = -8, KL = 10, HLx = 5,
        };
        if (_warden) { o.SL = 36; o.EL = 78; o.AL = 4; o.WL = -(o.SL + o.EL); o.WR = 40; o.SR = 18; }
        if (_caster) { o.SR = 20 + b * 1.5f; o.ER = 75; o.AR = 10; o.SL = -6 - b * 2; o.EL = 22; o.AL = 10; }
        if (_rogue) { o.Lean = 12 + b; o.Root.Y -= 0.04f; o.SR = 28 + b * 2; o.ER = 78; o.WR = 70; o.SL = 34 - b * 2; o.EL = 72; o.AL = 12; o.KR += 14; o.KL += 14; o.HR += 8; o.HL += 8; }
        return o;
    }

    /// <summary>
    /// Sitting on a log by the camp fire: thighs level, shins down, forearms on the knees and
    /// leaning in toward the warmth, breathing slowly (the hips drop a thigh's length so the
    /// feet stay on the ground).
    /// </summary>
    private Body Sit(float time)
    {
        float b = MathF.Sin(time * 1.3f);
        var o = new Body
        {
            Lean = 14 + b * 1.5f, HeadPitch = 8 - b * 1.2f, Root = new Vector3(-0.02f, -0.36f, 0),
            SR = 38 + b, ER = 62, WR = 20, AR = 12,
            SL = 34 - b, EL = 66, AL = 12,
            HR = 84, KR = 90, HRx = 9, HL = 80, KL = 86, HLx = 9,
        };
        if (_warden) { o.SL = 22; o.EL = 70; o.AL = 14; o.WL = -(o.SL + o.EL) + 20; }
        if (_caster) { o.SR = 30 + b; o.ER = 48; }
        return o;
    }

    private Body Run(float phase, float amount)
    {
        float sn = MathF.Sin(phase * Mathf.Tau);
        float swingR = MathF.Cos(phase * Mathf.Tau), swingL = -swingR;
        var o = new Body
        {
            Lean = 16, Twist = 12 * sn, HeadPitch = -8,
            Root = new Vector3(0, -0.05f + 0.045f * MathF.Abs(sn), 0),
            SR = 30 - 26 * sn, ER = 62, WR = 30, AR = 10,
            SL = 10 + 38 * sn, EL = 80, AL = 10,
            HR = 10 + 42 * sn, KR = 20 + 70 * Math.Max(0, swingR) + 12 * Math.Max(0, -sn),
            HL = 10 - 42 * sn, KL = 20 + 70 * Math.Max(0, swingL) + 12 * Math.Max(0, sn),
            HRx = 3, HLx = 3,
        };
        if (_warden) { o.SL = 40 + 6 * sn; o.EL = 80; o.WL = -(o.SL + o.EL); }
        if (_caster) { o.SR = 24 + 8 * sn; o.ER = 72; o.AR = 12; }
        return Lerp(Idle(0), o, amount);
    }

    private Body Air(float vy)
    {
        var rise = new Body { Lean = 6, Root = Vector3.Zero, SR = 45, ER = 55, WR = 30, SL = 70, EL = 35, AL = 10, AR = 10, HR = 48, KR = 80, HL = 2, KL = 38, HRx = 4, HLx = 4 };
        var apex = new Body { Lean = 10, SR = 62, ER = 50, WR = 25, AR = 18, SL = 55, EL = 45, AL = 18, HR = 55, KR = 85, HL = 32, KL = 70, HRx = 6, HLx = 6 };
        var fall = new Body { Lean = -2, HeadPitch = 6, SR = 105, ER = 25, WR = 15, AR = 28, SL = 118, EL = 20, AL = 30, HR = 18, KR = 28, HL = -6, KL = 18, HRx = 7, HLx = 7 };
        Body o = vy > 0 ? Lerp(apex, rise, W3.SmoothStep(0.5f, 5f, vy)) : Lerp(apex, fall, W3.SmoothStep(-0.5f, -6f, vy));
        if (_warden) { o.SL = 40; o.EL = 80; o.WL = -(o.SL + o.EL); }
        if (_caster) { o.SR = 40; o.ER = 60; o.AR = 14; }
        return o;
    }

    private Body Swim(float t, bool moving, float time)
    {
        if (moving)
        {
            float st = t * Mathf.Tau;
            return new Body
            {
                Roll = -72, HeadPitch = -45, Lean = 0,
                SR = 160 - 70 * Sin01(st), ER = 30 * Sin01(st + 1.5f), AR = 30, WR = 10,
                SL = 160 - 70 * Sin01(st), EL = 30 * Sin01(st + 1.5f), AL = 30,
                HR = 8 * MathF.Sin(st * 2f), KR = 18 + 10 * MathF.Sin(st * 2f + 1f),
                HL = -8 * MathF.Sin(st * 2f), KL = 18 - 10 * MathF.Sin(st * 2f + 1f), HRx = 4, HLx = 4,
            };
        }
        float w = time * 3.2f;
        return new Body
        {
            Roll = -8, Lean = 4, HeadPitch = 0,
            SR = 38 + 18 * MathF.Sin(w), AR = 38, ER = 40, WR = 30,
            SL = 38 + 18 * MathF.Sin(w + 1.3f), AL = 38, EL = 40,
            HR = 25 + 22 * MathF.Sin(w * 0.8f), KR = 55 + 25 * MathF.Sin(w * 0.8f + 1f),
            HL = 25 - 22 * MathF.Sin(w * 0.8f), KL = 55 - 25 * MathF.Sin(w * 0.8f + 1f), HRx = 8, HLx = 8,
        };
    }

    private static float AimAngle(string dir) => dir switch { "up" => 90f, "upfwd" => 45f, "downfwd" => -45f, "down" => -90f, _ => 0f };

    /// <summary>The sword swing: the blade angle over the clip, with the arm, torso and stance around it.</summary>
    private Body Slash(Body basePose, string clip, float t, float aim, bool grounded)
    {
        char letter = clip[6];
        bool fin = letter == 'c';
        float frames = fin ? 9f : 7f;
        float w = (fin ? 3f : 2f) / frames, u = w + 2f / frames;
        float arc = fin ? 170f : 115f;
        float sign = letter == 'b' ? -1f : 1f;
        float start = aim + sign * arc * 0.5f, end = aim - sign * arc * 0.5f;
        float cock = start + sign * (fin ? 30f : 20f), over = end - sign * 12f;
        float blade;         // blade direction, degrees from forward (up positive)
        float sweep;         // 0 wind-up .. 1 through the strike
        if (t < w) { float k = W3.Smooth01(t / w); blade = Mathf.Lerp(basePose.SR - 90f + basePose.ER * 0.5f, cock, k); sweep = 0f; }
        else if (t < u) { float k = (t - w) / (u - w); k = 1f - (1f - k) * (1f - k); blade = Mathf.Lerp(cock, over, k); sweep = k; }
        else
        {
            float k = (t - u) / (1f - u);
            blade = Mathf.Lerp(over, over + sign * 10f, W3.SmoothStep(0.45f, 1f, k));
            sweep = 1f;
        }
        var o = basePose;
        // the blade extends the arm: shoulder angle from hanging, elbow nearly straight in the strike
        float bend = t < w ? 55f : t < u ? Mathf.Lerp(40f, 4f, (t - w) / (u - w)) : 8f;
        o.SR = 90f + blade - bend * 0.35f;
        o.ER = bend;
        o.WR = -bend * 0.55f;
        o.AR = 12f;
        o.WRy = 0;
        // torso winds up and unwinds; chops pull the chest down, rising cuts lift it
        float twist = t < w ? -18f * sign : Mathf.Lerp(-18f * sign, 22f * sign, sweep);
        o.Twist = twist;
        o.Lean = 6f + (sign > 0 ? 14f : -6f) * sweep + (fin ? 6f : 0f) - aim * 0.08f;
        o.HeadPitch = -aim * 0.25f;
        // the free arm counterbalances (the warden keeps her shield up front)
        if (_warden) { o.SL = 45; o.EL = 75; o.WL = -(o.SL + o.EL); }
        else { o.SL = -25f - 25f * sweep; o.AL = 25f; o.EL = 35f; }
        if (grounded && MathF.Abs(aim) < 60f)
        {
            // a lunge into forward strikes
            float lunge = t < w ? 0.3f : 1f;
            o.HR = Mathf.Lerp(o.HR, 38f, lunge); o.KR = Mathf.Lerp(o.KR, 42f, lunge);
            o.HL = Mathf.Lerp(o.HL, -22f, lunge); o.KL = Mathf.Lerp(o.KL, 14f, lunge);
            o.Root.Y = Mathf.Lerp(o.Root.Y, -0.07f, lunge);
        }
        return o;
    }

    /// <summary>
    /// The Swordsman's heaving swing: both hands on the hilt, the sword hauled up over the head and
    /// far behind (the long wind-up, rooted), then brought over and down in one great arc into a
    /// deep lunge, and held there a moment.
    /// </summary>
    private static Body Heave(Body basePose, float t, bool grounded)
    {
        const float w = 7f / 16f, u = 9f / 16f;
        float blade, lean, twist;
        if (t < w)
        {
            float k = W3.Smooth01(t / w);
            blade = Mathf.Lerp(basePose.SR - 90f + basePose.ER * 0.5f, 158f, k);
            lean = Mathf.Lerp(basePose.Lean, -14f, k);
            twist = -22f * k;
        }
        else if (t < u)
        {
            float k = (t - w) / (u - w);
            k = 1f - (1f - k) * (1f - k);
            blade = Mathf.Lerp(158f, -64f, k);
            lean = Mathf.Lerp(-14f, 36f, k);
            twist = Mathf.Lerp(-22f, 26f, k);
        }
        else
        {
            float k = W3.SmoothStep(0.3f, 1f, (t - u) / (1f - u));
            blade = Mathf.Lerp(-64f, -40f, k);
            lean = Mathf.Lerp(36f, 14f, k);
            twist = Mathf.Lerp(26f, 8f, k);
        }
        var o = basePose;
        float bend = t < w ? Mathf.Lerp(40f, 70f, t / w) : t < u ? Mathf.Lerp(70f, 6f, (t - w) / (u - w)) : 10f;
        o.SR = 90f + blade - bend * 0.35f; o.ER = bend; o.WR = -bend * 0.55f; o.AR = 14f; o.WRy = 0;
        // the off hand joins the sword hand on the hilt
        o.SL = o.SR - 10f; o.EL = bend + 12f; o.AL = 26f; o.WL = o.WR;
        o.Lean = lean; o.Twist = twist;
        o.HeadPitch = t < w ? -16f * (t / w) : 8f;
        if (grounded)
        {
            float low = t < w ? 0.35f * (t / w) : t < u ? Mathf.Lerp(0.35f, 1f, (t - w) / (u - w)) : Mathf.Lerp(1f, 0.6f, (t - u) / (1f - u));
            o.HR = Mathf.Lerp(o.HR, 46f, low); o.KR = Mathf.Lerp(o.KR, 64f, low);
            o.HL = Mathf.Lerp(o.HL, -28f, low); o.KL = Mathf.Lerp(o.KL, 22f, low);
            o.Root.Y = Mathf.Lerp(o.Root.Y, -0.13f, low);
        }
        return o;
    }

    public override void Animate(CreaturePose p, in AnimInput a)
    {
        string c = a.Clip ?? "idle";
        float t = a.T;
        float speed = MathF.Abs(a.Vel.X);
        // gait phase advances with ground covered (1.9 m per stride pair at a sprint)
        ref float phase = ref p.Vars[0];
        if (a.OnFloor) phase = (phase + speed / 3.7f * a.Dt) % 1f;
        float runAmount = a.OnFloor ? W3.SmoothStep(0.4f, 3.5f, speed) : 0f;

        // ---- base: where the body is and how it moves
        Body o;
        if (a.InWater && !c.StartsWith("dodge")) o = Swim(t, c == "swim", a.Time);
        else if (!a.OnFloor && c != "wall_slide") o = Air(a.Vel.Y);
        else o = runAmount > 0.01f ? Run(phase, runAmount) : Idle(a.Time);

        // ---- the clip on top
        var player = a.Owner as Player;
        // a caster's staff: its angle in the body's frame (degrees from forward, up positive)
        float staff = 78f;
        if (c.StartsWith("slash_"))
        {
            float aim = AimAngle(c[8..]);
            if (player != null && player.SwingAim != Vector2.Zero)
                aim = Mathf.RadToDeg(MathF.Atan2(-player.SwingAim.Y, MathF.Max(player.SwingAim.X * a.Facing, -0.25f)));
            o = Slash(o, c, t, aim, a.OnFloor);
        }
        else switch (c)
        {
            case "sit":
                // (at the camp: T blends from sitting, 0, to standing ready, 1)
                o = Lerp(Sit(a.Time), Idle(a.Time), W3.Smooth01(t));
                if (_caster) staff = Mathf.Lerp(92f, 78f, t);
                break;
            case "jump_start":
                o.Root.Y += Key(t, (0, 0), (0.45f, -0.12f), (1, 0.02f));
                o.KR += Key(t, (0, 10), (0.45f, 60), (1, 0)); o.KL += Key(t, (0, 10), (0.45f, 60), (1, 0));
                o.HR += Key(t, (0, 5), (0.45f, 35), (1, 0)); o.HL += Key(t, (0, 5), (0.45f, 35), (1, 0));
                o.SR -= Key(t, (0, 0), (0.45f, 30), (1, -20)); o.SL -= Key(t, (0, 0), (0.45f, 30), (1, -20));
                break;
            case "land":
                {
                    float k = Key(t, (0, 1), (1, 0));
                    o.Root.Y -= 0.14f * k; o.Lean += 16 * k;
                    o.HR += 40 * k; o.KR += 65 * k; o.HL += 40 * k; o.KL += 65 * k;
                    o.SR -= 15 * k; o.SL -= 10 * k;
                    break;
                }
            case "run_start":
                o.Lean += Key(t, (0, 4), (0.5f, 14), (1, 6));
                break;
            case "run_stop":
                {
                    float k = Key(t, (0, 1), (0.6f, 0.8f), (1, 0));
                    o.Lean -= 22 * k; o.Root.Y -= 0.06f * k;
                    o.HR = Mathf.Lerp(o.HR, 42, k); o.KR = Mathf.Lerp(o.KR, 22, k);
                    o.HL = Mathf.Lerp(o.HL, 4, k); o.KL = Mathf.Lerp(o.KL, 38, k);
                    o.SL += 30 * k; o.AL += 20 * k;
                    break;
                }
            case "turn_r2l":
            case "turn_l2r":
                o.Root.Y -= 0.03f * MathF.Sin(t * MathF.PI);
                break;
            case "dodge":
                {
                    float tuck = MathF.Sin(Math.Clamp(t * 1.15f, 0, 1) * MathF.PI);
                    o.Roll = -360f * W3.Smooth01(t);
                    o.Root.Y = -0.28f * tuck;
                    o.HR = 20 + 95 * tuck; o.KR = 15 + 120 * tuck; o.HL = 20 + 95 * tuck; o.KL = 15 + 120 * tuck;
                    o.SR = 20 + 50 * tuck; o.ER = 40 + 70 * tuck; o.SL = 20 + 50 * tuck; o.EL = 40 + 70 * tuck;
                    o.Lean = 30 * tuck; o.HeadPitch = 30 * tuck;
                    break;
                }
            case "airdash":
                o = new Body { Lean = 62, HeadPitch = -40, SR = -30, ER = 30, WR = 60, AR = 12, SL = -45, EL = 20, AL = 12, HR = -18, KR = 38, HL = -32, KL = 18, HRx = 4, HLx = 4 };
                if (_warden) { o.SL = 60; o.EL = 70; o.WL = -(o.SL + o.EL); }
                break;
            case "throw":
                {
                    // the off hand flings the dagger (the sword stays ready)
                    float k = t;
                    o.SL = Key(k, (0, 20), (0.35f, -65), (0.55f, 115), (1, 70));
                    o.EL = Key(k, (0, 30), (0.35f, 85), (0.55f, 5), (1, 25));
                    o.AL = 18;
                    o.Twist = Key(k, (0, 0), (0.35f, 22), (0.55f, -20), (1, -8));
                    o.Lean += Key(k, (0, 0), (0.35f, -8), (0.55f, 12), (1, 4));
                    break;
                }
            case "bash":
                {
                    // the Warden's Guarded Charge: driven low behind the shield, sword held back
                    float k = Key(t, (0, 0.3f), (0.25f, 1f), (1, 1f));
                    o.Lean = Mathf.Lerp(o.Lean, 28, k); o.Twist = Mathf.Lerp(o.Twist, 12, k); o.HeadPitch = Mathf.Lerp(o.HeadPitch, -12, k);
                    o.Root.Y = Mathf.Lerp(o.Root.Y, -0.08f, k);
                    o.SR = Mathf.Lerp(o.SR, -35, k); o.ER = Mathf.Lerp(o.ER, 55, k); o.WR = Mathf.Lerp(o.WR, 25, k); o.AR = 14;
                    o.HR = Mathf.Lerp(o.HR, 48, k); o.KR = Mathf.Lerp(o.KR, 40, k); o.HL = Mathf.Lerp(o.HL, -30, k); o.KL = Mathf.Lerp(o.KL, 14, k);
                    break;
                }
            case "cast":
                {
                    // the drain bolt: staff drawn back, then thrust crystal-first along the aim
                    float aim = 0f;
                    if (player != null) aim = Math.Clamp(Mathf.RadToDeg(MathF.Atan2(-player.CastDir.Y, MathF.Max(player.CastDir.X * a.Facing, -0.2f))), -70f, 80f);
                    float draw = Key(t, (0, 0.4f), (0.3f, 1f), (0.45f, 0f));
                    float thrust = Key(t, (0.3f, 0f), (0.45f, 1f), (0.8f, 1f), (1f, 0f));
                    o.SR = Mathf.Lerp(Mathf.Lerp(o.SR, 35, draw), 78 + aim * 0.75f, thrust);
                    o.ER = Mathf.Lerp(Mathf.Lerp(o.ER, 85, draw), 12, thrust);
                    o.Twist += -12 * draw + 14 * thrust;
                    o.Lean += -4 * draw + 8 * thrust - aim * 0.05f * thrust;
                    o.HeadPitch += -aim * 0.2f * thrust;
                    o.SL = Mathf.Lerp(o.SL, 62 + aim * 0.5f, thrust); o.EL = Mathf.Lerp(o.EL, 16, thrust); o.AL = Mathf.Lerp(o.AL, 20, thrust);
                    staff = Mathf.Lerp(Mathf.Lerp(staff, 108, draw), aim + 18, thrust);
                    break;
                }
            case "rupture":
                {
                    // the staff raised high behind, the free hand thrust out at the creature, open...
                    // then clenched and torn back to the chest as it bursts
                    float aim = 0f;
                    if (player != null) aim = Math.Clamp(Mathf.RadToDeg(MathF.Atan2(-player.CastDir.Y, MathF.Max(player.CastDir.X * a.Facing, -0.2f))), -60f, 70f);
                    float reach = Key(t, (0, 0), (0.3f, 1f), (0.46f, 1f), (0.56f, 0f));
                    float yank = Key(t, (0.46f, 0f), (0.56f, 1f), (0.8f, 1f), (1f, 0f));
                    o.SL = Mathf.Lerp(Mathf.Lerp(o.SL, 84 + aim * 0.8f, reach), 30, yank);
                    o.EL = Mathf.Lerp(Mathf.Lerp(o.EL, 6, reach), 118, yank);
                    o.AL = Mathf.Lerp(o.AL, 24, Math.Max(reach, yank));
                    o.WL = Mathf.Lerp(-18f * reach, 40f, yank);
                    o.SR = Mathf.Lerp(o.SR, 150, Math.Max(reach, yank)); o.ER = Mathf.Lerp(o.ER, 30, Math.Max(reach, yank));
                    o.Lean += 12 * reach - 10 * yank - aim * 0.05f * reach;
                    o.Twist += -16 * reach + 20 * yank;
                    o.HeadPitch += -aim * 0.25f * reach;
                    o.HR += 20 * reach; o.KR += 18 * reach; o.HL -= 12 * reach;
                    staff = Mathf.Lerp(staff, 104, Math.Max(reach, yank));
                    break;
                }
            case "heave":
                o = Heave(o, t, a.OnFloor);
                break;
            case "shove":
                {
                    // the Warden's shield bash: a short step and the shield punched straight out
                    float k = Key(t, (0, 0), (0.22f, 1f), (0.55f, 1f), (1f, 0f));
                    o.SL = Mathf.Lerp(45, 96, k); o.EL = Mathf.Lerp(75, 8, k); o.AL = 8;
                    o.WL = -(o.SL + o.EL) + 90f * k;
                    o.WLy = a.Facing > 0 ? -30 : 30;
                    o.SR = Mathf.Lerp(o.SR, -25, k); o.ER = Mathf.Lerp(o.ER, 70, k);
                    o.Lean = Mathf.Lerp(o.Lean, 26, k); o.Twist = Mathf.Lerp(o.Twist, -18, k); o.HeadPitch = Mathf.Lerp(o.HeadPitch, -6, k);
                    o.HR = Mathf.Lerp(o.HR, 40, k); o.KR = Mathf.Lerp(o.KR, 36, k); o.HL = Mathf.Lerp(o.HL, -26, k); o.KL = Mathf.Lerp(o.KL, 12, k);
                    o.Root.Y = Mathf.Lerp(o.Root.Y, -0.06f, k);
                    break;
                }
            case "hex":
                {
                    // raised overhead in both hands, then driven down into the ground
                    float up = Key(t, (0, 0), (0.4f, 1f), (0.52f, 0f));
                    float slam = Key(t, (0.4f, 0f), (0.52f, 1f), (0.8f, 1f), (1f, 0f));
                    // (the fist stays high enough in the slam for the heel to strike the floor, not sink into it)
                    o.SR = Mathf.Lerp(Mathf.Lerp(o.SR, 150, up), 70, slam); o.ER = Mathf.Lerp(Mathf.Lerp(o.ER, 25, up), 42, slam);
                    o.SL = Mathf.Lerp(Mathf.Lerp(o.SL, 148, up), 60, slam); o.EL = Mathf.Lerp(Mathf.Lerp(o.EL, 30, up), 40, slam); o.AL = 18;
                    o.Lean += -8 * up + 24 * slam; o.HeadPitch += -12 * up + 10 * slam;
                    o.Root.Y -= 0.12f * slam;
                    o.HR += 35 * slam; o.KR += 55 * slam; o.HL += 30 * slam; o.KL += 50 * slam;
                    staff = Mathf.Lerp(Mathf.Lerp(staff, 92, up), 86, slam);
                    break;
                }
            case "heal":
                {
                    // the staff lifted high, the free hand opened, the face turned upward
                    float k = Key(t, (0, 0), (0.25f, 1f), (0.75f, 1f), (1f, 0f));
                    o.SR = Mathf.Lerp(o.SR, 165, k); o.ER = Mathf.Lerp(o.ER, 8, k); o.AR = Mathf.Lerp(o.AR, 14, k);
                    o.SL = Mathf.Lerp(o.SL, 105, k); o.EL = Mathf.Lerp(o.EL, 12, k); o.AL = Mathf.Lerp(o.AL, 50, k);
                    o.HeadPitch += -22 * k; o.Lean += -6 * k;
                    o.Root.Y += 0.02f * k;
                    staff = Mathf.Lerp(staff, 90, k);
                    break;
                }
            case "hurt":
                {
                    float k = Key(t, (0, 0), (0.2f, 1), (1, 0.15f));
                    o.Lean -= 26 * k; o.HeadPitch -= 22 * k; o.Twist += 10 * k;
                    o.SR += 30 * k; o.AR += 30 * k; o.SL += 45 * k; o.AL += 35 * k;
                    o.Root += new Vector3(-0.05f * k, -0.04f * k, 0);
                    o.KR += 25 * k; o.KL += 20 * k;
                    break;
                }
            case "death":
                {
                    float k1 = W3.SmoothStep(0f, 0.35f, t), k2 = W3.SmoothStep(0.3f, 0.85f, t);
                    o = Idle(0);
                    o.Lean = -15 * k1; o.KR = 16 + 60 * k1; o.KL = 10 + 70 * k1; o.HR = 12 + 30 * k1; o.HL = -8 + 30 * k1;
                    o.Roll = 84 * k2;
                    o.Root = new Vector3(-0.1f * k2, -0.05f * k1 - 0.62f * k2, 0);
                    o.SR = 24 + 80 * k2; o.AR = 30 * k2; o.ER = 34 - 20 * k2; o.SL = -2 + 70 * k2; o.AL = 30 * k2;
                    o.HeadPitch = -30 * k2; o.KR = Mathf.Lerp(o.KR, 25, k2); o.KL = Mathf.Lerp(o.KL, 55, k2);
                    break;
                }
            case "wall_slide":
                o = new Body
                {
                    Lean = -8, HeadPitch = -10, Root = new Vector3(-0.02f, -0.04f, 0),
                    SR = 60, ER = 40, WR = 40, AR = 12,
                    SL = 140, EL = 25, AL = 10,
                    HR = 55, KR = 75, HL = 15, KL = 25, HRx = 6, HLx = 6,
                };
                if (_warden) { o.SL = 70; o.EL = 60; o.WL = -(o.SL + o.EL); }
                break;
        }

        // ---- a caster's hand holds the staff at its angle, whatever the arm is doing
        if (_caster && c != "death") o.WR = staff + o.Lean - o.SR - o.ER;

        // ---- the warden's shield, raised toward where she guards
        if (_warden && player != null && player.ShieldRaised && !c.StartsWith("dodge") && c != "death" && c != "shove")
        {
            var d = player.ShieldDir;
            float ang = Mathf.RadToDeg(MathF.Atan2(-d.Y, d.X * a.Facing));
            o.SL = 90 + ang - 25; o.EL = 45; o.AL = 10;
            o.WL = ang - (o.SL + o.EL);
            o.WLy = a.Facing > 0 ? -30 : 30;
        }

        // ---- cloak and scarf trail behind the motion
        float fwd = a.Vel.X * a.Facing;
        float trail = -Math.Clamp(fwd * 5.5f, -30f, 70f) - Math.Clamp(-a.Vel.Y * 4f, 0f, 45f);
        if (a.InWater) trail *= 0.5f;
        float flutter = MathF.Sin(a.Time * 9f) * Math.Min(1f, (speed + MathF.Abs(a.Vel.Y)) / 6f) * 6f;
        float cp0 = p.Spring(0, trail * 0.35f + o.Lean * 0.3f, a.Dt, 70f, 9f);
        float cp1 = p.Spring(1, trail * 0.35f + flutter, a.Dt, 60f, 8f);
        float cp2 = p.Spring(2, trail * 0.3f - flutter, a.Dt, 55f, 7f);
        float cp3 = p.Spring(3, trail * 0.2f + flutter * 1.5f, a.Dt, 50f, 6f);
        float sf0 = p.Spring(4, trail * 0.4f - 6f + flutter, a.Dt, 45f, 6f);
        float sf1 = p.Spring(5, trail * 0.3f - flutter * 1.8f, a.Dt, 40f, 5f);
        float sf2 = p.Spring(6, trail * 0.2f + flutter * 2.5f, a.Dt, 35f, 4f);

        Apply(p, o);
        p.Bend(cape0, cp0); p.Bend(cape1, cp1); p.Bend(cape2, cp2); p.Bend(cape3, cp3);
        p.Bend(scarf0, sf0 * 0.6f); p.Add(scarf0, 0, sf1 * 0.3f, 0); p.Bend(scarf1, sf1); p.Bend(scarf2, sf2);
    }

    private void Apply(CreaturePose p, Body o)
    {
        p.Set(hips, 0, o.Twist * 0.25f, o.Roll);
        p.Move(hips, o.Root);
        p.Set(spine, o.Bank * 0.5f, o.Twist * 0.35f, -o.Lean * 0.45f);
        p.Set(chest, o.Bank * 0.5f, o.Twist * 0.4f, -o.Lean * 0.55f);
        p.Set(neck, 0, 0, o.Lean * 0.3f - o.HeadPitch * 0.4f);
        p.Set(head, 0, o.HeadYaw, o.Lean * 0.45f - o.HeadPitch * 0.6f);
        // arms: swing about the side axis first, then out from the body
        p.Rot[uarm[0]] = CreaturePose.Q(-o.AR, 0, 0) * CreaturePose.Q(0, 0, o.SR);
        p.Set(farm[0], 0, 0, o.ER);
        p.Set(hand[0], 0, o.WRy, o.WR);
        p.Rot[uarm[1]] = CreaturePose.Q(o.AL, 0, 0) * CreaturePose.Q(0, 0, o.SL);
        p.Set(farm[1], 0, 0, o.EL);
        p.Set(hand[1], 0, o.WLy, o.WL);
        // legs: hip, knee, and a foot that stays near level
        p.Rot[thigh[0]] = CreaturePose.Q(-o.HRx, 0, 0) * CreaturePose.Q(0, 0, o.HR);
        p.Set(shin[0], 0, 0, -o.KR);
        p.Set(foot[0], 0, 0, -(o.HR - o.KR) * 0.8f + o.AnR);
        p.Rot[thigh[1]] = CreaturePose.Q(o.HLx, 0, 0) * CreaturePose.Q(0, 0, o.HL);
        p.Set(shin[1], 0, 0, -o.KL);
        p.Set(foot[1], 0, 0, -(o.HL - o.KL) * 0.8f + o.AnL);
    }
}
