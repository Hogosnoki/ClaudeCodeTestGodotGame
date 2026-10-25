using System;
using Godot;

namespace DaggerCave;

/// <summary>
/// The five heroes, one rig. The Swordsman (Bran, see HeroDesign.Bran.cs): a broad, bare-armed warrior with short dark hair and
/// stubble, a slate cowl and torn cloak, a fur mantle on one shoulder, leather straps over a dark tunic,
/// a ragged tabard, and a longsword. The Warden: built broad (shoulders and hips set wider on the rig), an open-faced bascinet
/// over a bearded face, great pauldrons, mail under a blue tabard with a gold sash, a shortsword and a kite shield. The Vitalist: a deep green robe and cowl over a bone mask with eyes that glow,
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
    private readonly bool _warden, _vitalist, _elementalist, _caster, _rogue, _swordsman, _aegis, _shifter, _automaton;
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
        _automaton = kind == HeroKind.Automaton;
        // (the casters hold a staff, wear a robe, and share their poses)
        _caster = _vitalist || _elementalist || _aegis;
        _st = StyleFor(kind);
    }
    public override string Name => Player.SheetName(_kind);
    // (fine enough for a face's features)
    public override float Cell => 0.01f;
    public override float ThreeQuarter => 20f;
    public override float FloorY => Floor;

    public override CreatureLook Look => new()
    {
        Eye = _automaton ? new Color(1f, 0.6f, 0.2f) : _shifter ? new Color(0.92f, 0.95f, 1f) : _vitalist ? new Color(0.5f, 1f, 0.42f) : _elementalist ? new Color(1f, 0.72f, 0.4f) : _aegis ? new Color(0.6f, 1f, 0.92f) : new Color(1f, 0.9f, 0.75f),
        EyeEnergy = _automaton ? 2.2f : _shifter ? 1.6f : _vitalist ? 2.4f : _elementalist ? 1.2f : _aegis ? 1.0f : 0.5f,
        Glow = new Color(1f, 0.68f, 0.32f), GlowEnergy = 6f,
        Rim = new Color(0.55f, 0.68f, 0.95f), RimEnergy = 0.28f,
        DetailScale = 34f, DetailStrength = 0.6f,
    };

    // (the soles: lower than they were sculpted, the legs being lengthened, see Proportion)
    private const float Floor = -0.81f - SoleDrop;
    private static readonly Vector3 LanternAt = new(0.02f, -0.02f, -0.19f);
    /// <summary>How far out the shoulder joints and the hips' sockets sit (rest space): the Warden is built broader than the rest.</summary>
    private float ArmZ => _warden ? 0.225f : 0.19f;
    private float LegZ => _warden ? 0.11f : 0.095f;
    /// <summary>Where the hand grips (rest space); a caster's staff runs through it along +X, crystal (or orb) forward.</summary>
    private static readonly Vector3 Grip = new(0.02f, -0.11f, 0.2f);
    private const float StaffUp = 0.6f, StaffDown = 0.98f, GemAt = 0.72f;
    private static readonly Color Life = new(0.45f, 1f, 0.4f);
    /// <summary>The Elementalist's orb: its fire, and its frost.</summary>
    /// <summary>The Aegis's staff disc: a pale teal light.</summary>
    private static readonly Color AegisGlow = new(0.55f, 1f, 0.9f);
    private static readonly Color OrbFire = new(1f, 0.55f, 0.16f), OrbFrost = new(0.62f, 0.88f, 1f);
    /// <summary>The Elementalist's cooldown orbs: the bolts (orange, white with Frostbolt), earth, wind and the storm (white, orange with Firestorm).</summary>
    private static readonly Color OrbBoltFire = new(1f, 0.52f, 0.12f), OrbWhite = new(0.95f, 0.98f, 1f), OrbEarth = new(1f, 0.86f, 0.18f), OrbWind = new(0.66f, 0.68f, 0.74f);
    /// <summary>Up to eight bolt orbs (the first in the staff hand, the rest by the other), then the updraft's, the snap's and the storm's.</summary>
    private const int BoltOrbs = 8;
    private static readonly string[] OrbNames = { "OrbBolt0", "OrbBolt1", "OrbBolt2", "OrbBolt3", "OrbBolt4", "OrbBolt5", "OrbBolt6", "OrbBolt7", "OrbWind", "OrbEarth", "OrbStorm" };

    private int hips, spine, chest, neck, head, cape0, cape1, cape2, cape3, scarf0, scarf1, scarf2;
    /// <summary>The Warden's shield on a bone of its own (-1 for the others): the elemental Warden's is shrunk away and his mode's shield
    /// (a slab of stone, a pane of ice, a veil of air) held in its place.</summary>
    private int shieldBone = -1;
    private readonly int[] clav = new int[2], uarm = new int[2], farm = new int[2], hand = new int[2], thigh = new int[2], shin = new int[2], foot = new int[2];

    protected override void OnBonesBound()
    {
        hips = B("hips"); spine = B("spine"); chest = B("chest"); neck = B("neck"); head = B("head");
        cape0 = B("cape0"); cape1 = B("cape1"); cape2 = B("cape2"); cape3 = B("cape3");
        scarf0 = B("scarf0"); scarf1 = B("scarf1"); scarf2 = B("scarf2");
        shieldBone = B("shield");
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
        // (the Warden's plate a darker, cooler steel, so it reads as armour under the lantern's warm light, not as white)
        if (_warden) steel = C(0.46f, 0.48f, 0.53f);
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
            int ua = s.Bone("uarm" + sfx, cl, new(0, 0.46f, ArmZ * z));
            int fa = s.Bone("farm" + sfx, ua, new(0, 0.19f, (ArmZ + 0.01f) * z));
            s.Bone("hand" + sfx, fa, new(0, -0.05f, (ArmZ + 0.01f) * z));
            int th = s.Bone("thigh" + sfx, hipsB, new(0, 0.0f, LegZ * z));
            int sh = s.Bone("shin" + sfx, th, new(0.01f, -0.39f, LegZ * z));
            s.Bone("foot" + sfx, sh, new(-0.01f, -0.75f, LegZ * z));
        }
        int c0 = s.Bone("cape0", chestB, new(-0.09f, 0.46f, 0));
        int c1 = s.Bone("cape1", c0, new(-0.12f, 0.2f, 0));
        int c2 = s.Bone("cape2", c1, new(-0.14f, -0.08f, 0));
        int c3 = s.Bone("cape3", c2, new(-0.15f, -0.35f, 0));
        int s0 = s.Bone("scarf0", neckB, new(-0.05f, 0.57f, 0));
        int s1 = s.Bone("scarf1", s0, new(-0.2f, 0.56f, 0.02f));
        int s2 = s.Bone("scarf2", s1, new(-0.35f, 0.53f, 0.03f));
        // (the Warden's shield hangs from a bone of its own, at the shield, so it can be shrunk away for the elemental shield)
        if (_warden) s.Bone("shield", "hand_l", ShieldAt);

        if (_swordsman)
        {
            SculptBran(s);
            SculptLantern(s, hipsB);
            Proportion(s);
            return;
        }
        if (_automaton) { SculptAutomaton(s); Proportion(s); return; }

        // ---- torso
        // (the Warden: a barrel chest, a broad back and heavy shoulders, thick through the waist)
        float wb = _warden ? 1f : 0f;
        s.Egg(hipsB, new(0, 0.03f, 0), new(0.125f + 0.015f * wb, 0.1f + 0.01f * wb, 0.145f + 0.027f * wb), dark, Mat.Cloth, 0.04f);
        s.Limb(spineB, new(0, 0.05f, 0), new(0, 0.26f, 0), 0.118f + 0.018f * wb, 0.138f + 0.027f * wb, body, bodyMat, 0.04f);
        s.Egg(chestB, new(0.012f + 0.006f * wb, 0.36f, 0), new(0.13f + 0.02f * wb, 0.15f + 0.012f * wb, _warden ? 0.245f : 0.185f), body, bodyMat, 0.05f);
        s.Limb(chestB, new(-0.01f, 0.445f, -0.15f - 0.04f * wb), new(-0.01f, 0.445f, 0.15f + 0.04f * wb), 0.072f + 0.014f * wb, 0.072f + 0.014f * wb, body, bodyMat, 0.04f);
        if (_warden)
        {
            // tabard over the mail, gold sash across the chest
            s.Egg(chestB, new(0.04f, 0.3f, 0), new(0.14f, 0.21f, 0.2f), cloak, Mat.Cloth, 0.02f);
            s.Egg(hipsB, new(0.02f, -0.1f, 0), new(0.145f, 0.18f, 0.18f), cloak, Mat.Cloth, 0.03f);
            s.Limb(chestB, new(0.13f, 0.44f, -0.19f), new(0.14f, 0.12f, 0.18f), 0.03f, 0.03f, gold, Mat.Cloth, 0.01f);
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
            if (_shifter)
                for (int k = 0; k < 2; k++)
                    s.Egg(chestB, new(0.1f, 0.36f, (k == 0 ? 1 : -1) * 0.058f), new(0.05f, 0.046f, 0.05f), body, bodyMat, 0.03f);
        }
        s.Egg(hipsB, new(0, 0.075f, 0), new(0.14f + 0.017f * wb, 0.03f, 0.158f + 0.028f * wb), leatherDk, Mat.Leather, 0.01f);
        s.Block(hipsB, new(0.14f + 0.017f * wb, 0.075f, 0), new(0.012f, 0.022f, 0.028f), 0.004f, _vitalist ? boneWhite : gold, _vitalist ? Mat.Bone : Mat.Gold, 0.004f);

        // ---- neck and head
        s.Limb(neckB, new(0.01f, 0.47f, 0), new(0.02f, 0.6f, 0), _warden ? 0.07f : 0.056f, _warden ? 0.064f : 0.052f, skin, Mat.Skin, 0.03f);
        if (_warden)
        {
            // an open-faced bascinet: the steel dome down over the brow and the cheeks, a nasal bar down the nose, a mail aventail
            // hung from it round the neck, a short red crest; and in it a broad, weathered face, square-jawed and stern under a heavy
            // brow, the nose broken once, a short auburn beard
            var helm = C(0.44f, 0.46f, 0.5f);
            s.Egg(headB, new(0.004f, 0.77f, 0), new(0.116f, 0.112f, 0.104f), helm, Mat.Metal, 0.02f).Crisp = true;
            s.CarveBall(headB, new(0.166f, 0.69f, 0), 0.1f, 0.012f);
            var f = BaseFace(C(0.6f, 0.42f, 0.33f));
            f.Offset = new Vector3(0.012f, -0.03f, 0f); f.Scale = 1f; f.Wide = 1.12f;
            f.Chin = 1.15f; f.Cheek = 1.12f; f.Ridge = 1.35f; f.NoseLen = 1.04f; f.NoseHook = 0.7f; f.NoseWide = 1.2f;
            f.Lips = 0.9f; f.Smile = -0.6f; f.BrowTilt = -0.9f; f.BrowThick = 1.25f; f.Socket = 1.12f; f.Age = 0.45f;
            f.Hair = C(0.34f, 0.19f, 0.1f); f.Beard = Beard.Short; f.BeardCol = C(0.36f, 0.2f, 0.1f); f.Iris = C(0.3f, 0.38f, 0.44f);
            SculptFace(s, headB, f);
            s.Ring(headB, new(0.004f, 0.786f, 0), 0.118f, 0.106f, 0.009f, helm, Mat.Metal, 0.004f);
            s.Limb(headB, new(0.118f, 0.792f, 0), new(0.136f, 0.716f, 0), 0.0075f, 0.0065f, helm, Mat.Metal, 0.004f);
            s.Limb(headB, new(0.02f, 0.88f, 0), new(-0.1f, 0.85f, 0), 0.016f, 0.012f, helm, Mat.Metal, 0.008f);
            s.Limb(headB, new(0.0f, 0.9f, 0), new(-0.11f, 0.86f, 0), 0.026f, 0.016f, C(0.55f, 0.06f, 0.05f), Mat.Cloth, 0.016f, 0.002f);
            s.Egg(neckB, new(-0.035f, 0.63f, 0), new(0.1f, 0.085f, 0.13f), body, Mat.Metal, 0.025f, bump: 0.0015f).Crisp = true;
            s.Egg(neckB, new(-0.02f, 0.555f, 0), new(0.13f, 0.05f, 0.17f), body, Mat.Metal, 0.03f, bump: 0.0015f);
        }
        else if (_vitalist)
        {
            // a deep cowl, and in its shadow a bone mask carved like a skull: a heavy brow over deep sockets lit green from within,
            // high cheekbones, the nose a hollow, the teeth set in a grim line, a crack running down from the brow
            s.Egg(headB, new(-0.002f, 0.748f, 0), new(0.138f, 0.152f, 0.126f), cloak, Mat.Cloth, 0.02f);
            s.Limb(headB, new(-0.07f, 0.76f, 0), new(-0.12f, 0.52f, 0), 0.09f, 0.12f, cloak, Mat.Cloth, 0.04f);
            s.Limb(headB, new(0.03f, 0.88f, 0), new(-0.08f, 0.9f, 0), 0.03f, 0.012f, cloak, Mat.Cloth, 0.03f);
            s.CarveBall(headB, new(0.17f, 0.7f, 0), 0.102f, 0.02f);
            s.Egg(headB, new(0.036f, 0.71f, 0), new(0.088f, 0.105f, 0.076f), boneWhite, Mat.Bone, 0.012f).Crisp = true;
            s.Limb(headB, new(0.1f, 0.752f, -0.05f), new(0.116f, 0.756f, 0), 0.011f, 0.012f, boneWhite, Mat.Bone, 0.01f);
            s.Limb(headB, new(0.116f, 0.756f, 0), new(0.1f, 0.752f, 0.05f), 0.012f, 0.011f, boneWhite, Mat.Bone, 0.01f);
            for (int k = 0; k < 2; k++)
            {
                float z = k == 0 ? 1 : -1;
                s.Egg(headB, new(0.092f, 0.698f, 0.046f * z), new(0.026f, 0.017f, 0.02f), boneWhite, Mat.Bone, 0.012f);
                s.Limb(headB, new(0.03f, 0.7f, 0.068f * z), new(0.085f, 0.652f, 0.04f * z), 0.014f, 0.016f, boneWhite, Mat.Bone, 0.014f);
                s.CarveBall(headB, new(0.118f, 0.726f, 0.033f * z), 0.019f, 0.006f);
                s.CarveBall(headB, new(0.128f, 0.699f, 0.007f * z), 0.0075f, 0.003f);
            }
            s.Egg(headB, new(0.098f, 0.648f, 0), new(0.034f, 0.022f, 0.044f), boneWhite, Mat.Bone, 0.01f);
            s.CarveLimb(headB, new(0.128f, 0.664f, -0.03f), new(0.128f, 0.664f, 0.03f), 0.0035f, 0.0035f, 0.002f);
            for (int j = -3; j <= 3; j++)
                s.CarveLimb(headB, new(0.128f, 0.672f, j * 0.0085f), new(0.126f, 0.656f, j * 0.0085f), 0.0016f, 0.0014f, 0.001f);
            Strand(s, headB, new[] { new Vector3(0.12f, 0.776f, 0.012f), new Vector3(0.123f, 0.762f, 0.018f), new Vector3(0.121f, 0.75f, 0.014f) },
                new[] { 0.0012f, 0.0016f, 0.0008f }, C(0.2f, 0.17f, 0.13f), Mat.Leather);
            s.Eye(headB, new(0.106f, 0.726f, 0.033f), 0.0085f, Life, 0.55f);
            s.Eye(headB, new(0.106f, 0.726f, -0.033f), 0.0085f, Life, 0.55f);
        }
        else if (_rogue)
        {
            // a close hood, and a dark bandana tied over his face to just under the eyes: it hides the nose, mouth and jaw, its top hem
            // pulled taut across the bridge of the nose from cheek to cheek; above it, intent brows, red eyes, and a lock of pale hair
            // fallen from the hood across one eye
            s.Egg(headB, new(0.004f, 0.738f, 0), new(0.126f, 0.14f, 0.116f), cloak, Mat.Cloth, 0.02f);
            s.Limb(headB, new(-0.07f, 0.75f, 0), new(-0.11f, 0.55f, 0), 0.082f, 0.1f, cloak, Mat.Cloth, 0.04f);
            s.CarveBall(headB, new(0.16f, 0.705f, 0), 0.098f, 0.02f);
            var f = BaseFace(C(0.66f, 0.5f, 0.42f));
            f.Scale = 0.93f; f.Wide = 0.95f; f.Jaw = 0.5f; f.Chin = 0.9f; f.Cheek = 1.18f; f.CheekY = 0.003f; f.Ridge = 0.9f;
            f.BrowTilt = -0.55f; f.BrowThick = 0.85f; f.Hair = C(0.62f, 0.52f, 0.32f); f.Iris = C(0.6f, 0.07f, 0.1f); f.EyeGlow = 0.3f;
            SculptFace(s, headB, f);
            var band = C(0.1f, 0.085f, 0.11f);
            var hem = band.Darkened(0.35f);
            // (crisp, closely blended: cloth with a clean edge, not a smudge)
            s.Egg(headB, new(0.056f, 0.654f, 0), new(0.09f, 0.064f, 0.092f), band, Mat.Cloth, 0.006f).Crisp = true;
            s.Egg(headB, new(0.072f, 0.69f, 0), new(0.062f, 0.022f, 0.074f), band, Mat.Cloth, 0.005f).Crisp = true;
            s.Limb(headB, FaceAt(f, 0.11f, 0.746f, 0), FaceAt(f, 0.129f, 0.714f, 0), 0.0118f, 0.0134f, band, Mat.Cloth, 0.005f).Crisp = true;
            s.Egg(neckB, new(0.03f, 0.622f, 0), new(0.08f, 0.052f, 0.092f), band, Mat.Cloth, 0.008f).Crisp = true;
            // the hem: across the bridge of the nose and back along each cheek under the eye to the hood
            for (int k = 0; k < 2; k++)
            {
                float z = k == 0 ? 1 : -1;
                var bridge = FaceAt(f, 0.113f, 0.744f, 0.004f * z);
                var cheek = FaceAt(f, 0.088f, 0.732f, 0.047f * z);
                var side = FaceAt(f, 0.035f, 0.722f, 0.072f * z);
                s.Limb(headB, bridge, cheek, 0.0045f, 0.0045f, hem, Mat.Cloth, 0.003f).Crisp = true;
                s.Limb(headB, cheek, side, 0.0045f, 0.004f, hem, Mat.Cloth, 0.003f).Crisp = true;
                // a fold pulled down from under each cheekbone
                s.Limb(headB, FaceAt(f, 0.098f, 0.712f, 0.034f * z), FaceAt(f, 0.105f, 0.672f, 0.024f * z), 0.0032f, 0.0022f, hem, Mat.Cloth, 0.003f).Crisp = true;
            }
            var hair = C(0.78f, 0.68f, 0.45f);
            s.Limb(headB, new(0.075f, 0.81f, -0.006f), new(0.112f, 0.748f, -0.042f), 0.012f, 0.0035f, hair, HairMat, 0.005f, 0.0012f).Crisp = true;
            for (int j = 0; j < 4; j++)
            {
                float z = 0.012f + 0.016f * j;
                s.Limb(headB, new(0.07f, 0.815f, z), new(0.098f, 0.79f - 0.004f * j, z * 1.1f), 0.011f, 0.004f, hair, HairMat, 0.005f, 0.0012f).Crisp = true;
            }
        }
        else if (_elementalist)
        {
            // a tall hood whose point droops back, open at an old, weathered face: gaunt under high cheekbones, deep-set eyes catching
            // the orb's light under bushy grey brows, a hooked nose, the lines of age, and a long grey beard
            s.Egg(headB, new(0.0f, 0.745f, 0), new(0.134f, 0.15f, 0.122f), cloak, Mat.Cloth, 0.02f);
            s.Limb(headB, new(-0.07f, 0.76f, 0), new(-0.12f, 0.52f, 0), 0.088f, 0.118f, cloak, Mat.Cloth, 0.04f);
            s.Limb(headB, new(-0.01f, 0.86f, 0), new(-0.1f, 0.99f, 0), 0.075f, 0.03f, cloak, Mat.Cloth, 0.03f);
            s.Limb(headB, new(-0.1f, 0.99f, 0), new(-0.19f, 0.98f, 0), 0.03f, 0.008f, cloak, Mat.Cloth, 0.02f);
            s.CarveBall(headB, new(0.16f, 0.7f, 0), 0.098f, 0.02f);
            var grey = C(0.52f, 0.5f, 0.47f);
            var f = BaseFace(C(0.64f, 0.48f, 0.38f));
            f.Wide = 0.9f; f.Jaw = 0.55f; f.Cheek = 1.3f; f.CheekY = 0.004f; f.Ridge = 1.25f; f.NoseLen = 1.2f; f.NoseHook = 1f; f.NoseTip = 0.9f;
            f.Lips = 0.72f; f.Smile = -0.2f; f.BrowTilt = 0.35f; f.BrowThick = 1.15f; f.Socket = 1.3f; f.Age = 1f;
            f.Hair = grey; f.Beard = Beard.Long; f.BeardCol = grey; f.Iris = C(0.85f, 0.5f, 0.18f); f.EyeGlow = 0.6f;
            SculptFace(s, headB, f);
            // an ember collar at the throat
            s.Egg(neckB, new(0.02f, 0.625f, 0), new(0.08f, 0.048f, 0.095f), ember, Mat.Cloth, 0.01f).Crisp = true;
        }
        else if (_aegis)
        {
            // a pale hood thrown back from a calm, soft-featured face: a smooth brow, a straight nose, full lips at ease, dark hair parted
            // under a gold circlet set with a teal stone
            s.Egg(headB, new(0.0f, 0.74f, 0), new(0.13f, 0.142f, 0.12f), robe, Mat.Cloth, 0.02f);
            s.Limb(headB, new(-0.07f, 0.75f, 0), new(-0.115f, 0.53f, 0), 0.085f, 0.115f, robe, Mat.Cloth, 0.04f);
            s.CarveBall(headB, new(0.16f, 0.705f, 0), 0.098f, 0.02f);
            var hair = C(0.06f, 0.05f, 0.05f);
            var f = BaseFace(C(0.46f, 0.31f, 0.23f));
            f.Wide = 0.96f; f.Jaw = 0.2f; f.Chin = 0.9f; f.Cheek = 1.05f; f.Ridge = 0.72f; f.NoseLen = 0.95f; f.NoseTip = 0.92f; f.NoseWide = 0.95f;
            f.Lips = 1.2f; f.Smile = 0.45f; f.BrowTilt = 0.05f; f.BrowThick = 0.8f; f.Socket = 0.9f;
            f.Hair = hair; f.Iris = C(0.3f, 0.72f, 0.66f); f.EyeGlow = 0.35f;
            SculptFace(s, headB, f);
            for (int k = 0; k < 2; k++)
            {
                float z = k == 0 ? 1 : -1;
                s.Limb(headB, new(0.085f, 0.818f, 0.004f * z), new(0.07f, 0.775f, 0.07f * z), 0.016f, 0.012f, hair, HairMat, 0.005f, 0.0012f).Crisp = true;
            }
            s.Limb(headB, new(0.085f, 0.795f, -0.085f), new(0.115f, 0.79f, 0), 0.011f, 0.011f, gold, Mat.Gold, 0.004f);
            s.Limb(headB, new(0.115f, 0.79f, 0), new(0.085f, 0.795f, 0.085f), 0.011f, 0.011f, gold, Mat.Gold, 0.004f);
            s.Ball(headB, new(0.12f, 0.79f, 0), 0.016f, C(0.5f, 0.95f, 0.88f), Mat.Crystal, 0.004f);
            s.Egg(neckB, new(0.03f, 0.625f, 0), new(0.078f, 0.05f, 0.09f), cloak, Mat.Cloth, 0.01f).Crisp = true;
        }
        else
        {
            // the Shape Shifter, a young woman: a hood cut open at a lean, wild face, high sharp cheekbones over a fine jaw, pale eyes
            // that shine, a sly half-smile, long ash-grey hair falling from the hood to her collarbones and swept across her brow, a band
            // of charcoal across the eyes and claw marks down one cheek
            s.Egg(headB, new(0.005f, 0.735f, 0), new(0.128f, 0.142f, 0.118f), cloak, Mat.Cloth, 0.02f);
            s.Limb(headB, new(-0.07f, 0.75f, 0), new(-0.115f, 0.53f, 0), 0.085f, 0.115f, cloak, Mat.Cloth, 0.04f);
            s.CarveBall(headB, new(0.16f, 0.705f, 0), 0.098f, 0.02f);
            var ash = C(0.5f, 0.51f, 0.55f);
            var f = BaseFace(C(0.66f, 0.5f, 0.41f));
            f.Scale = 0.92f; f.Wide = 0.88f; f.Jaw = 0.3f; f.Chin = 0.82f; f.Cheek = 1.3f; f.CheekY = 0.002f; f.Ridge = 0.72f;
            f.NoseLen = 0.9f; f.NoseTip = 0.8f; f.NoseWide = 0.82f; f.Lips = 1.12f; f.Mouth = 0.92f; f.Smile = 0.25f;
            f.BrowTilt = -0.15f; f.BrowThick = 0.62f; f.Hair = ash; f.Iris = C(0.85f, 0.9f, 0.96f); f.EyeGlow = 0.6f; f.WarPaint = true;
            f.LipUp = f.Skin.Darkened(0.3f).Lerp(C(0.5f, 0.2f, 0.22f), 0.18f); f.LipLow = f.Skin.Darkened(0.2f).Lerp(C(0.55f, 0.24f, 0.25f), 0.18f);
            SculptFace(s, headB, f);
            for (int k = 0; k < 2; k++)
            {
                float z = k == 0 ? 1 : -1;
                // long locks from under the hood down past the jaw, framing the face
                s.Limb(headB, new(0.07f, 0.8f, 0.07f * z), new(0.062f, 0.67f, 0.078f * z), 0.017f, 0.015f, ash, HairMat, 0.005f, 0.0012f).Crisp = true;
                s.Limb(headB, new(0.062f, 0.67f, 0.078f * z), new(0.072f, 0.56f, 0.07f * z), 0.015f, 0.006f, ash, HairMat, 0.005f, 0.0012f).Crisp = true;
                s.Limb(headB, new(0.045f, 0.76f, 0.085f * z), new(0.04f, 0.6f, 0.09f * z), 0.014f, 0.006f, ash, HairMat, 0.005f, 0.0012f).Crisp = true;
            }
            // the fringe, swept across her brow to one side
            for (int j = 0; j < 4; j++)
            {
                float t = j / 3f;
                s.Limb(headB, new(0.075f, 0.825f, 0.05f - 0.03f * t), new(0.1f, 0.792f - 0.006f * t, -0.02f - 0.03f * t), 0.011f, 0.0035f, ash, HairMat, 0.005f, 0.0012f).Crisp = true;
            }
            s.Egg(neckB, new(0.012f, 0.598f, 0), new(0.074f, 0.036f, 0.088f), C(0.5f, 0.07f, 0.05f), Mat.Cloth, 0.008f).Crisp = true;
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
            float az = ArmZ * z, fz = (ArmZ + 0.01f) * z;
            if (_warden)
            {
                // great layered pauldrons, mail sleeves over thick arms, steel vambraces and gauntlets
                s.Egg(ua, new(0, 0.47f, az + 0.01f * z), new(0.1f, 0.08f, 0.1f), steel, Mat.Metal, 0.02f);
                s.Egg(ua, new(0, 0.418f, az + 0.016f * z), new(0.088f, 0.034f, 0.094f), steel, Mat.Metal, 0.012f);
                s.Ring(ua, new(0, 0.445f, az + 0.012f * z), 0.098f, 0.098f, 0.008f, gold, Mat.Gold, 0.004f);
                s.Limb(ua, new(0, 0.45f, az), new(0, 0.2f, fz), 0.062f, 0.054f, body, Mat.Metal, 0.02f, 0.0012f);
                s.Ball(fa, new(0, 0.19f, fz), 0.054f, steel, Mat.Metal, 0.015f);
                s.Limb(fa, new(0, 0.18f, fz), new(0, -0.03f, fz), 0.056f, 0.05f, steel, Mat.Metal, 0.015f);
                s.Limb(hd, new(0.005f, -0.05f, fz), new(0.018f, -0.125f, fz), 0.043f, 0.036f, steel, Mat.Metal, 0.012f);
                continue;
            }
            s.Egg(ua, new(0, 0.45f, az), new(0.078f, 0.068f, 0.072f), leather, Mat.Leather, 0.02f);
            s.Limb(ua, new(0, 0.45f, az), new(0, 0.2f, fz), 0.05f, 0.043f, dark, Mat.Cloth, 0.02f);
            s.Ball(fa, new(0, 0.19f, fz), 0.043f, dark, Mat.Cloth, 0.015f);
            s.Limb(fa, new(0, 0.18f, fz), new(0, -0.03f, fz), 0.045f, 0.039f, leather, Mat.Leather, 0.015f);
            s.Limb(hd, new(0.005f, -0.05f, fz), new(0.018f, -0.125f, fz), 0.036f, 0.03f, leatherDk, Mat.Leather, 0.012f);
        }

        // ---- legs
        for (int k = 0; k < 2; k++)
        {
            float z = k == 0 ? 1 : -1;
            string sfx = k == 0 ? "_r" : "_l";
            int th = s["thigh" + sfx], sh = s["shin" + sfx], ft = s["foot" + sfx];
            float lz = LegZ * z, lw = _warden ? 1.17f : 1f;
            s.Limb(th, new(0, 0.0f, lz), new(0.012f, -0.39f, lz), 0.072f * lw, 0.055f * lw, dark, Mat.Cloth, 0.03f);
            s.Ball(sh, new(0.022f, -0.39f, lz), 0.05f * lw, _warden ? steel : leather, _warden ? Mat.Metal : Mat.Leather, 0.012f);
            s.Limb(sh, new(0.01f, -0.39f, lz), new(-0.01f, -0.73f, lz), 0.05f * lw, 0.04f * lw, dark, Mat.Cloth, 0.015f);
            s.Limb(sh, new(0.002f, -0.52f, lz), new(-0.01f, -0.765f, lz), 0.058f * lw, 0.05f * lw, leatherDk, Mat.Leather, 0.012f);
            s.Egg(sh, new(0.002f, -0.52f, lz), new Vector3(0.066f, 0.028f, 0.066f) * lw, leather, Mat.Leather, 0.01f);
            s.Limb(ft, new(-0.03f, -0.775f, lz), new(0.115f, -0.785f, lz), 0.046f * lw, 0.036f * lw, leatherDk, Mat.Leather, 0.012f);
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
                if (_warden) w *= 1.18f;
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
            // a wildwood staff, held like a staff (the cuts are still a sword's): a crooked shaft gripped about its middle, the foot showing as far
            // behind the hand as the head is ahead of it, a leather wrap and a bead charm under the head, and the head a knot of burl with two antler
            // tines curling back (along the sword's -Y, where a blade would be). About 1.5 long, so it reaches half of that ahead of the fist
            var grip = new Transform3D(Basis.Identity, new Vector3(0.016f, -0.1f, 0.2f));
            var rng = new Random(23);
            var wood = C(0.34f, 0.25f, 0.16f);
            var path = new System.Collections.Generic.List<Vector3>();
            var radii = new System.Collections.Generic.List<float>();
            const int n = 12;
            for (int k = 0; k <= n; k++)
            {
                float t = k / (float)n, y = 0.75f - 1.5f * t;
                var wob = k == 0 || k == n ? Vector3.Zero : new Vector3(((float)rng.NextDouble() - 0.5f) * 0.016f, 0, ((float)rng.NextDouble() - 0.5f) * 0.012f);
                path.Add(new Vector3(0, y, 0) + wob);
                radii.Add(0.02f + 0.006f * t + (k % 3 == 2 ? 0.005f : 0f));
            }
            var shaft = new MeshBuilder();
            shaft.Tube(path, radii, 7, wood);
            var sw = new MeshBuilder();
            sw.Append(shaft, grip);
            s.Rigid(handR, sw, Mat.Wood);
            // the head: a knot of burl, two short antler tines curling back from it, like a stag's brow
            var knot = new MeshBuilder();
            DecorMeshes.AddSphere(knot, new Vector3(0.004f, -0.78f, 0), 0.042f, wood.Darkened(0.1f), 6);
            DecorMeshes.AddSphere(knot, new Vector3(-0.012f, -0.815f, 0.012f), 0.03f, wood.Darkened(0.18f), 5);
            var kn = new MeshBuilder();
            kn.Append(knot, grip);
            s.Rigid(handR, kn, Mat.Wood);
            var bone = C(0.84f, 0.8f, 0.7f);
            var antler = new MeshBuilder();
            foreach (float z in new[] { 1f, -1f })
                antler.Tube(new[] { new Vector3(0.01f, -0.8f, 0.03f * z), new Vector3(0.05f, -0.78f, 0.07f * z), new Vector3(0.1f, -0.74f, 0.085f * z), new Vector3(0.13f, -0.68f, 0.08f * z) },
                    new[] { 0.013f, 0.01f, 0.007f, 0.003f }, 6, bone);
            antler.Tube(new[] { new Vector3(0.05f, -0.78f, 0.07f), new Vector3(0.055f, -0.83f, 0.1f), new Vector3(0.05f, -0.87f, 0.11f) }, new[] { 0.007f, 0.005f, 0.002f }, 5, bone);
            var an = new MeshBuilder();
            an.Append(antler, grip);
            s.Rigid(handR, an, Mat.Bone);
            // the wrap and the charm
            var wrap = new MeshBuilder();
            wrap.Tube(new[] { new Vector3(0, -0.62f, 0), new Vector3(0, -0.7f, 0) }, new[] { 0.03f, 0.03f }, 8, leatherDk);
            wrap.Tube(new[] { new Vector3(0, -0.66f, 0.028f), new Vector3(0.012f, -0.6f, 0.05f), new Vector3(0.016f, -0.54f, 0.055f) }, new[] { 0.004f, 0.004f, 0.003f }, 4, leatherDk);
            DecorMeshes.AddSphere(wrap, new Vector3(0.016f, -0.53f, 0.055f), 0.015f, C(0.55f, 0.75f, 0.8f), 4);
            var wr = new MeshBuilder();
            wr.Append(wrap, grip);
            s.Rigid(handR, wr, Mat.Leather);
        }
        else if (_rogue) { /* (the daggers are attachments, so they can leave the hands) */ }
        else
        {
            var grip = new Transform3D(Basis.Identity, new Vector3(0.016f, -0.1f, ArmZ + 0.01f));
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
            sh.Append(shield, new Transform3D(Basis.Identity, ShieldAt));
            s.Rigid(s["shield"], sh, Mat.Metal);
        }
        SculptLantern(s, hipsB);
        Proportion(s);
    }

    // ================================================================== proportions

    /// <summary>How much the torso is shortened (hips to the base of the neck) and the legs lengthened (hip to ankle).</summary>
    private const float TorsoK = 0.88f, LegK = 1.08f;
    private const float NeckY = 0.5f, ShoulderY = 0.46f, AnkleY = -0.75f;
    /// <summary>How far the ankles (and the soles under them) end up below where they were sculpted.</summary>
    private const float SoleDrop = -AnkleY * (LegK - 1f);

    /// <summary>A point of the trunk and legs, reshaped: squeezed between the hips and the neck, stretched between the hips and the ankles,
    /// moved without stretching above the neck and below the ankles (so the head, the feet and their soles keep their shapes).</summary>
    private static float ProportionY(float y) =>
        y >= NeckY ? y - NeckY * (1f - TorsoK) : y >= 0f ? y * TorsoK : y >= AnkleY ? y * LegK : y - SoleDrop;

    private static float UnproportionY(float y) =>
        y >= NeckY * TorsoK ? y + NeckY * (1f - TorsoK) : y >= 0f ? y / TorsoK : y >= AnkleY * LegK ? y / LegK : y + SoleDrop;

    /// <summary>
    /// The heroes' proportions, applied to the finished figure: a shorter torso and longer legs, the height about the same. The neck and
    /// head, and the arms (from the shoulder down, with whatever they hold), move down with the shoulders whole; the trunk, the cloak and
    /// the legs are reshaped point by point.
    /// </summary>
    private static void Proportion(Sculptor s)
    {
        var mode = new int[s.Bones.Count];
        for (int k = 0; k < mode.Length; k++)
        {
            string n = s.Bones[k].Name;
            mode[k] = n is "neck" or "head" || n.StartsWith("scarf") ? 1
                : n.StartsWith("clav") || n.StartsWith("uarm") || n.StartsWith("farm") || n.StartsWith("hand") || n == "shield" ? 2 : 0;
        }
        float headDy = -NeckY * (1f - TorsoK), armDy = ShoulderY * TorsoK - ShoulderY;
        s.Reshape((p, bone) => bone < 0 || bone >= mode.Length || mode[bone] == 0
                ? new Vector3(p.X, ProportionY(p.Y), p.Z)
                : new Vector3(p.X, p.Y + (mode[bone] == 1 ? headDy : armDy), p.Z),
            q => new Vector3(q.X, UnproportionY(q.Y), q.Z));
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
            // a readied element: a great halo of its colour about the orb (added light, seen at a glance)
            var haloMat = new StandardMaterial3D
            {
                AlbedoColor = new Color(1f, 0.5f, 0.2f, 0.4f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BlendMode = BaseMaterial3D.BlendModeEnum.Add,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, CullMode = BaseMaterial3D.CullModeEnum.Disabled,
                EmissionEnabled = true, Emission = OrbFire, EmissionEnergyMultiplier = 4f,
            };
            hand.AddChild(new MeshInstance3D
            {
                Name = "OrbHalo", Mesh = new SphereMesh { Radius = 0.32f, Height = 0.64f, RadialSegments = 16, Rings = 8 }, MaterialOverride = haloMat,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Position = orbAt, Visible = false,
            });
        }
        else if (_rogue)
        {
            // a dagger in each hand (hidden while it's thrown)
            var mat = AmbientOnly.Metal(new StandardMaterial3D { VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true, Metallic = 0.8f, Roughness = 0.3f });
            var dagger = PropMeshes.Sword(0.3f, 0.026f, C(0.78f, 0.8f, 0.84f), C(0.6f, 0.46f, 0.2f), C(0.1f, 0.08f, 0.07f), 0.05f).ToMesh(mat);
            for (int k = 0; k < 2; k++)
            {
                var hand = m.AttachTo(k == 0 ? "hand_r" : "hand_l");
                hand.Name = k == 0 ? "DaggerR" : "DaggerL";
                hand.AddChild(new MeshInstance3D { Name = "Blade", Mesh = dagger, Position = new Vector3(0.016f, -0.05f, 0f) });
            }
        }
        else
        {
            // the Charged Strike: a burning edge along the blade, shown while the blade is charged (the Warden's shortsword: a readied element)
            var hand = m.AttachTo("hand_r");
            hand.Name = "BladeHand";
            var edgeMat = new StandardMaterial3D
            {
                AlbedoColor = new Color(1f, 0.5f, 0.2f, 0.5f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BlendMode = BaseMaterial3D.BlendModeEnum.Add,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, CullMode = BaseMaterial3D.CullModeEnum.Disabled, NoDepthTest = false,
                EmissionEnabled = true, Emission = new Color(1f, 0.45f, 0.15f), EmissionEnergyMultiplier = 3f,
            };
            // along the blade (it runs down -Y from the grip), in the hand's local space
            float blade = _warden ? 0.73f : 1.04f;
            var mid = new Vector3(0.016f, -0.1f - 0.03f - blade * 0.5f, 0.2f) - new Vector3(0f, -0.05f, 0.2f);
            hand.AddChild(new MeshInstance3D
            {
                Name = "ChargeEdge", Mesh = new BoxMesh { Size = new Vector3(0.1f, blade, 0.03f) }, MaterialOverride = edgeMat,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Position = mid, Visible = false,
            });
            // a readied element: a wide aura of its colour over the whole blade (added light, seen at a glance), with the edge burning within
            var auraMat = new StandardMaterial3D
            {
                AlbedoColor = new Color(1f, 0.5f, 0.2f, 0.42f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BlendMode = BaseMaterial3D.BlendModeEnum.Add,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, CullMode = BaseMaterial3D.CullModeEnum.Disabled,
                EmissionEnabled = true, Emission = new Color(1f, 0.45f, 0.15f), EmissionEnergyMultiplier = 4f,
            };
            hand.AddChild(new MeshInstance3D
            {
                Name = "ElementAura", Mesh = new CapsuleMesh { Radius = 0.16f, Height = blade + 0.36f, RadialSegments = 12, Rings = 4 }, MaterialOverride = auraMat,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Position = mid, Visible = false,
            });
            if (_warden)
            {
                // the elemental Warden in earth mode: his shortsword grown into a club of packed dirt and stone (hidden otherwise)
                hand.AddChild(new MeshInstance3D { Name = "StoneClub", Mesh = StoneClubMesh(blade), Position = new Vector3(0.016f, 0f, 0f), Visible = false });
                // the Warden's shield: a glow of the readied element (or the guard it holds) over its face
                var shieldHand = m.AttachTo("hand_l");
                shieldHand.Name = "ShieldHand";
                shieldHand.AddChild(new MeshInstance3D
                {
                    Name = "ShieldGlow", Mesh = new SphereMesh { Radius = 0.36f, Height = 0.72f, RadialSegments = 16, Rings = 8 }, MaterialOverride = (Material)auraMat.Duplicate(),
                    CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Position = new Vector3(0.05f, -0.12f, -0.2f), Scale = new Vector3(0.75f, 1f, 0.75f), Visible = false,
                });
                // the elemental Warden's shield, on the shield's own bone where the steel one is shrunk away: his mode's (see SyncModeShield)
                var shieldAt = m.AttachTo("shield");
                shieldAt.Name = "ShieldAt";
                var modeShield = new MeshInstance3D
                {
                    Name = "ModeShield", Mesh = PropMeshes.KiteShield(0.62f, 0.4f, Colors.White, Colors.White, Colors.White).ToMesh(),
                    MaterialOverride = new StandardMaterial3D { Transparency = BaseMaterial3D.TransparencyEnum.Alpha, RimEnabled = true },
                    CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Scale = Vector3.One * 1.06f, Visible = false,
                };
                shieldAt.AddChild(modeShield);
            }
            // the Ice Axe and the Glacier Cleaver: heads of ice that grow out of the blade as they swing (HeroDesign.Axe.cs)
            BuildIceAxes(hand);
            hand.AddChild(new OmniLight3D
            {
                Name = "ChargeLight", LightColor = new Color(1f, 0.55f, 0.25f), LightEnergy = 0f, OmniRange = 2.8f, OmniAttenuation = 1.3f,
                ShadowEnabled = false, LightVolumetricFogEnergy = 0.4f, Position = mid, Visible = false,
            });
        }
        if (_elementalist) AttachOrbs(m);
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

    /// <summary>
    /// The Elementalist's cooldown orbs: small glowing spheres, two in his hands (and a third by the left
    /// with Frostbolt) for the bolts he holds, and three that circle him for the updraft, the snap and the storm.
    /// </summary>
    private void AttachOrbs(CreatureModel m)
    {
        var handL = m.AttachTo("hand_l");
        handL.Name = "OrbHandL";
        var handR = m.GetNodeOrNull<Node3D>("Pivot/Skeleton/StaffHand");
        var chest = m.AttachTo("chest");
        chest.Name = "OrbRing";
        for (int k = 0; k < OrbNames.Length; k++)
        {
            var parent = k == 0 ? handR : k < BoltOrbs ? handL : chest;
            if (parent == null) continue;
            var orb = new Node3D { Name = OrbNames[k] };
            var core = new StandardMaterial3D
            {
                AlbedoColor = Colors.White, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            };
            var halo = new StandardMaterial3D
            {
                AlbedoColor = new Color(1f, 1f, 1f, 0.22f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BlendMode = BaseMaterial3D.BlendModeEnum.Add,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            };
            orb.AddChild(new MeshInstance3D { Name = "Core", Mesh = new SphereMesh { Radius = 0.05f, Height = 0.1f, RadialSegments = 12, Rings = 6, Material = core }, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
            orb.AddChild(new MeshInstance3D { Name = "Halo", Mesh = new SphereMesh { Radius = 0.1f, Height = 0.2f, RadialSegments = 12, Rings = 6, Material = halo }, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
            orb.AddChild(new OmniLight3D
            {
                Name = "Light", LightColor = Colors.White, LightEnergy = 0.18f, OmniRange = 1.6f, OmniAttenuation = 1.4f,
                ShadowEnabled = false, LightVolumetricFogEnergy = 0.2f,
            });
            parent.AddChild(orb);
        }
    }

    /// <summary>Where each orb sits for one frame (the bolts' relative to the hand, in the hero's own space; the others about the chest).</summary>
    private static Vector3 OrbAt(int k, float time, int bolts)
    {
        // (a palm's width ahead of the hand: they bob a little; SyncOrbs puts them there)
        if (k == 0) return new Vector3(0.17f, 0.09f + 0.012f * MathF.Sin(time * 3.1f), 0.06f);
        if (k == 1) return new Vector3(0.2f, 0.05f + 0.012f * MathF.Sin(time * 3.1f + 1.3f), 0.0f);
        if (k < BoltOrbs)
        {
            // (the bolts beyond a pair circle the left hand, evenly spaced)
            int extra = Math.Max(1, bolts - 2);
            float ang = time * 3.2f + (k - 2) * Mathf.Tau / extra;
            float r = 0.09f + 0.012f * extra;
            return new Vector3(0.2f + r * MathF.Cos(ang), 0.17f + 0.02f * MathF.Sin(time * 3f + k), r * MathF.Sin(ang));
        }
        // (the updraft, the snap and the storm: one third of a turn apart, each on its own height)
        int i = k - BoltOrbs;
        float a = time * (1.5f + 0.2f * i) + i * Mathf.Tau / 3f;
        float rad = 0.62f + 0.04f * i;
        return new Vector3(MathF.Cos(a) * rad, 0.18f - 0.16f * i + 0.04f * MathF.Sin(time * 2.3f + i * 2f), MathF.Sin(a) * rad);
    }

    /// <summary>The orbs for this frame: each shows while its spell is ready, shrinking out as it's spent and swelling back as it returns.</summary>
    private void SyncOrbs(CreatureModel m, in AnimInput a, Player player)
    {
        bool dead = player != null && player.Dead;
        bool frost = player?.FrostElement ?? false;
        bool fire = player?.FireStorm ?? false;
        int charges = player?.BoltCharges ?? 2, max = player?.BoltChargesMax ?? 2;
        for (int k = 0; k < OrbNames.Length; k++)
        {
            var orb = m.GetNodeOrNull<Node3D>(k == 0 ? "Pivot/Skeleton/StaffHand/" + OrbNames[k] : k < BoltOrbs ? "Pivot/Skeleton/OrbHandL/" + OrbNames[k] : "Pivot/Skeleton/OrbRing/" + OrbNames[k]);
            if (orb == null) continue;
            // (the elemental Elementalist has no orbs: his bolts aren't counted, and his spells' readiness is on the HUD)
            bool on = !dead && !(player?.Elemental ?? false) && k switch
            {
                < BoltOrbs => k < max && charges > k,
                BoltOrbs => player?.UpdraftOrb ?? true,
                BoltOrbs + 1 => player?.SnapOrb ?? true,
                _ => player?.StormOrb ?? true,
            };
            Color col = k < BoltOrbs ? (frost ? OrbWhite : OrbBoltFire) : k == BoltOrbs ? OrbWind : k == BoltOrbs + 1 ? OrbEarth : fire ? OrbBoltFire : OrbWhite;
            float s = Mathf.MoveToward(orb.Scale.X, on ? 1f : 0f, a.Dt * (on ? 5f : 9f));
            orb.Scale = Vector3.One * Math.Max(0.001f, s);
            orb.Visible = s > 0.01f;
            if (!orb.Visible) continue;
            var at = OrbAt(k, a.Time, max);
            if (k < BoltOrbs)
            {
                // (ahead of the hand, on the side the hero faces: placed in the hero's space, not the hand's)
                var hand = orb.GetParent<Node3D>();
                orb.GlobalPosition = hand.GlobalPosition + m.GetNode<Node3D>("Pivot").GlobalTransform.Basis * at;
            }
            else orb.Position = at;
            // a fire's glow flickers, frost and the rest breathe
            float live = k < BoltOrbs && !frost || (k == BoltOrbs + 2 && fire) ? 0.8f + 0.2f * MathF.Sin(a.Time * 13f + k) * MathF.Sin(a.Time * 5.1f) : 0.9f + 0.1f * MathF.Sin(a.Time * 2.4f + k);
            var core = orb.GetNode<MeshInstance3D>("Core");
            if (core.Mesh is SphereMesh cm && cm.Material is StandardMaterial3D cmat)
            {
                cmat.AlbedoColor = col * (0.72f + 0.28f * live);
            }
            var halo = orb.GetNode<MeshInstance3D>("Halo");
            if (halo.Mesh is SphereMesh hm && hm.Material is StandardMaterial3D hmat) hmat.AlbedoColor = new Color(col, 0.2f * live);
            var light = orb.GetNode<OmniLight3D>("Light");
            light.LightColor = col;
            light.LightEnergy = 0.18f * live;
        }
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
            // (elemental: the crystal burns in the mode's colour, an attribute pulsing through it, an overcharge flaring it)
            ModeGlow(player, dead, a.Time, ref col, ref glow, ref pulse);
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
            ModeGlow(player, dead, a.Time, ref col, ref glow, ref pulse);
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
            SyncOrbs(m, a, player);
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
            // (an element readied: the orb burns in its colour)
            var readied = player?.GlowElement ?? HeroElement.None;
            if (readied != HeroElement.None) { col = Elements.ColorOf(readied); glow = Math.Max(glow, 0.45f); frost = readied == HeroElement.Frost; }
            // fire flickers; frost breathes slowly
            float live = frost ? 0.88f + 0.12f * MathF.Sin(a.Time * 1.8f) : 0.8f + 0.2f * MathF.Sin(a.Time * 13f) * MathF.Sin(a.Time * 5.1f);
            if (orb.Mesh is SphereMesh sm && sm.Material is StandardMaterial3D mat)
            {
                mat.Emission = col;
                mat.EmissionEnergyMultiplier = dead ? 0.3f : (2.2f + 7f * glow) * live;
            }
            bool elem = readied != HeroElement.None && !dead;
            if (elem && orb.Mesh is SphereMesh sm2 && sm2.Material is StandardMaterial3D mat2) mat2.EmissionEnergyMultiplier = (9f + 4f * glow) * live;
            orb.Scale = Vector3.One * ((elem ? 2.4f : 1f) + 0.35f * glow);
            light.LightColor = col;
            light.LightEnergy = dead ? 0f : (elem ? 3.2f : 0.45f + 2.8f * glow) * live;
            light.OmniRange = elem ? 4.8f : 3.2f + 2.5f * glow;
            var halo = m.GetNodeOrNull<MeshInstance3D>("Pivot/Skeleton/StaffHand/OrbHalo");
            if (halo != null)
            {
                halo.Visible = elem;
                if (elem && halo.MaterialOverride is StandardMaterial3D hm)
                {
                    // (the mode's colour; an attribute on top, its colour; an overcharge, a flare)
                    float breathe = (player.Overcharged ? 1.5f : 0.8f) + 0.15f * MathF.Sin(a.Time * (player.Overcharged ? 11f : 6f)) + 2.2f * player.InfuseFlash;
                    var hc = player.Attribute != HeroElement.None ? Elements.ColorOf(player.Attribute) : col;
                    hm.AlbedoColor = new Color(hc, 0.5f * breathe);
                    hm.Emission = hc;
                    hm.EmissionEnergyMultiplier = 4.5f * breathe;
                    halo.Scale = Vector3.One * (0.9f + 0.15f * breathe);
                }
            }
        }
        else if (_rogue)
        {
            var r = m.GetNodeOrNull<MeshInstance3D>("Pivot/Skeleton/DaggerR/Blade");
            var l = m.GetNodeOrNull<MeshInstance3D>("Pivot/Skeleton/DaggerL/Blade");
            if (r != null) r.Visible = player == null || player.DaggerInHand(0);
            if (l != null) l.Visible = player == null || player.DaggerInHand(1);
        }
        else
        {
            var edge = m.GetNodeOrNull<MeshInstance3D>("Pivot/Skeleton/BladeHand/ChargeEdge");
            if (_warden) { SyncShieldGlow(m, player, a.Time); SyncModeShield(m, player, a.Time); }
            var light = m.GetNodeOrNull<OmniLight3D>("Pivot/Skeleton/BladeHand/ChargeLight");
            SyncIceAxes(m, player, a.Time);
            if (edge == null || light == null) return;
            // the edge burns while the blade is charged, or in an element's colour while one is readied (then an aura over the whole blade too;
            // all frost while an axe of ice grows out of it)
            var readied = player == null || player.Dead ? HeroElement.None : player.AxeKind > 0 ? HeroElement.Frost : player.GlowElement;
            var aura = m.GetNodeOrNull<MeshInstance3D>("Pivot/Skeleton/BladeHand/ElementAura");
            // (the elemental Warden in earth mode swings a club of stone: no aura over it, the stone is the element)
            var club = m.GetNodeOrNull<MeshInstance3D>("Pivot/Skeleton/BladeHand/StoneClub");
            bool stone = _warden && club != null && player != null && !player.Dead && player.Elemental && player.Mode == HeroElement.Earth;
            if (club != null) club.Visible = stone;
            if (aura != null)
            {
                aura.Visible = readied != HeroElement.None && !stone;
                if (aura.Visible && aura.MaterialOverride is StandardMaterial3D am)
                {
                    var ac = Elements.ColorOf(readied);
                    // (the mode's colour, steady; an overcharge flares it; it flares up as an element is pressed)
                    bool over = player != null && player.Overcharged;
                    float breathe = (over ? 1.5f : 0.75f) + 0.15f * MathF.Sin(a.Time * (over ? 11f : 6f)) + 2.2f * (player?.InfuseFlash ?? 0f);
                    am.AlbedoColor = new Color(ac, 0.5f * breathe);
                    am.Emission = ac;
                    am.EmissionEnergyMultiplier = 4.5f * breathe;
                    aura.Scale = new Vector3(1f, 1f, 1f) * (0.95f + 0.08f * breathe);
                }
            }
            bool on = player != null && !player.Dead && (player.Charged > 0 || player.SwingCharged || readied != HeroElement.None);
            edge.Visible = on;
            light.Visible = on;
            if (!on) return;
            // (the edge burns in the attribute's colour when one rides on top of the mode)
            var attr = player.AxeKind > 0 ? HeroElement.None : player.Attribute;
            var tint = attr != HeroElement.None ? Elements.ColorOf(attr) : readied != HeroElement.None ? Elements.ColorOf(readied) : new Color(1f, 0.45f, 0.15f);
            float flick = readied is HeroElement.Frost or HeroElement.Earth ? 0.88f + 0.12f * MathF.Sin(a.Time * 2.4f) : 0.75f + 0.25f * MathF.Sin(a.Time * 23f) * MathF.Sin(a.Time * 7.3f);
            bool elem = readied != HeroElement.None;
            if (edge.MaterialOverride is StandardMaterial3D em)
            {
                em.EmissionEnergyMultiplier = elem ? 7f + 2f * flick : 2.2f + 1.6f * flick;
                em.Emission = elem ? tint.Lightened(0.25f) : tint;
                em.AlbedoColor = new Color(tint, elem ? 0.9f : 0.5f);
            }
            edge.Scale = elem ? new Vector3(1.6f, 1.05f, 1.6f) : Vector3.One;
            light.LightColor = tint;
            light.LightEnergy = (elem ? 3.2f : 1.1f) * flick;
            light.OmniRange = elem ? 4.5f : 2.8f;
        }
    }

    /// <summary>An elemental caster's focus (the Vitalist's crystal, the Aegis's disc) in its mode: the mode's colour, an attribute pulsing
    /// through it, an overcharge flaring it; the cast's own flash still shows over it.</summary>
    private static void ModeGlow(Player player, bool dead, float time, ref Color col, ref float glow, ref float pulse)
    {
        var mode = player == null || dead ? HeroElement.None : player.GlowElement;
        if (mode == HeroElement.None) return;
        var mc = Elements.ColorOf(mode);
        if (player.Attribute != HeroElement.None) mc = mc.Lerp(Elements.ColorOf(player.Attribute), 0.5f + 0.5f * MathF.Sin(time * 4f));
        col = mc.Lerp(col, Math.Clamp(glow, 0f, 1f) * 0.6f);
        glow = Math.Max(glow, player.Overcharged ? 0.55f : 0.3f) + 0.3f * player.InfuseFlash;
        if (player.Overcharged) pulse = 0.8f + 0.25f * MathF.Sin(time * 11f);
    }

    /// <summary>The elemental Warden's club in earth mode: a lumpy haft of packed dirt along the blade, swelling into a knot of stones at its end.</summary>
    private static ArrayMesh StoneClubMesh(float blade)
    {
        var noise = new Noise3(77);
        var rng = new Random(77);
        var dirt = new Color(0.36f, 0.26f, 0.17f);
        var stone = new Color(0.5f, 0.45f, 0.4f);
        var mb = new MeshBuilder();
        var path = new System.Collections.Generic.List<Vector3>();
        var radii = new System.Collections.Generic.List<float>();
        for (int i = 0; i <= 6; i++)
        {
            float k = i / 6f;
            path.Add(new Vector3(0f, -0.1f - blade * k, 0f));
            radii.Add(0.035f + 0.075f * k * k);
        }
        mb.Tube(path, radii, 7, dirt, true, (i, k) => 0.3f * noise.Fbm(i * 1.7f, k * 1.3f, 0.5f, 3));
        // (stones packed into it, thickest at the head)
        for (int n = 0; n < 9; n++)
        {
            float k = 0.35f + 0.65f * (float)rng.NextDouble();
            float a = (float)rng.NextDouble() * Mathf.Tau;
            float r = 0.035f + 0.075f * k * k;
            var c = new Vector3(MathF.Cos(a) * r * 0.8f, -0.1f - blade * k, MathF.Sin(a) * r * 0.8f);
            mb.Blob(c, Vector3.One * (0.025f + 0.035f * k), 3, stone.Lerp(dirt, (float)rng.NextDouble() * 0.5f), noise, 0.4f, 3f);
        }
        mb.Blob(path[^1], Vector3.One * 0.11f, 4, stone.Darkened(0.1f), noise, 0.35f, 2.5f);
        var mat = MaterialGains.Tint(new StandardMaterial3D { VertexColorUseAsAlbedo = true, Roughness = 0.95f }, "stone");
        return mb.Faceted().ToMesh(mat);
    }

    /// <summary>Where the Warden's shield sits (model space): its bone's head, and the steel shield's origin.</summary>
    private Vector3 ShieldAt => new(0.05f, -0.12f, -(ArmZ + 0.01f));

    /// <summary>
    /// The elemental Warden's shield is his mode's: a slab of brown-grey stone, solid, in earth; a pane of pale blue ice, half clear, in frost;
    /// a veil of light grey air, faint (a fifth), in wind. It flashes when struck, and it's gone while broken (the steel one beneath is shrunk
    /// away by the shield's bone, in Animate).
    /// </summary>
    private static void SyncModeShield(CreatureModel m, Player player, float time)
    {
        var shield = m.GetNodeOrNull<MeshInstance3D>("Pivot/Skeleton/ShieldAt/ModeShield");
        if (shield == null) return;
        bool on = player != null && player.Elemental && !player.Dead && !player.ShieldBroken && player.ShieldHp > 0.5f;
        shield.Visible = on;
        if (!on || shield.MaterialOverride is not StandardMaterial3D mat) return;
        float flash = Math.Clamp(player.ShieldFlash * 5f, 0f, 1f);
        switch (player.Mode)
        {
            case HeroElement.Earth:
                mat.AlbedoColor = new Color(0.46f, 0.42f, 0.38f, 1f); mat.Roughness = 0.95f; mat.Metallic = 0f; mat.Rim = 0.15f;
                mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.PerPixel; mat.EmissionEnabled = flash > 0f; mat.Emission = new Color(0.7f, 0.6f, 0.45f);
                break;
            case HeroElement.Frost:
                mat.AlbedoColor = new Color(0.62f, 0.84f, 1f, 0.55f); mat.Roughness = 0.08f; mat.Metallic = 0f; mat.Rim = 0.9f;
                mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.PerPixel; mat.EmissionEnabled = true; mat.Emission = new Color(0.3f, 0.55f, 0.85f);
                break;
            default:
                mat.AlbedoColor = new Color(0.86f, 0.88f, 0.9f, 0.2f); mat.Roughness = 1f; mat.Metallic = 0f; mat.Rim = 0.5f;
                mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded; mat.EmissionEnabled = false;
                break;
        }
        mat.EmissionEnergyMultiplier = (player.Mode == HeroElement.Frost ? 0.25f : 0f) + flash * 1.2f;
        // (the air breathes a little)
        if (player.Mode == HeroElement.Wind) mat.AlbedoColor = mat.AlbedoColor with { A = 0.2f + 0.04f * MathF.Sin(time * 5f) + flash * 0.2f };
    }

    /// <summary>The Warden's shield glows with the readied element, or (brighter, steady) with the guard it holds while raised; the
    /// elemental Warden's shows its mode itself, so it glows only with a guard.</summary>
    private static void SyncShieldGlow(CreatureModel m, Player player, float time)
    {
        var glow = m.GetNodeOrNull<MeshInstance3D>("Pivot/Skeleton/ShieldHand/ShieldGlow");
        if (glow == null) return;
        bool alive = player != null && !player.Dead;
        var guard = alive ? player.GuardElement : HeroElement.None;
        var el = guard != HeroElement.None ? guard : alive && !player.Elemental ? player.GlowElement : HeroElement.None;
        glow.Visible = el != HeroElement.None;
        if (!glow.Visible || glow.MaterialOverride is not StandardMaterial3D mat) return;
        var c = Elements.ColorOf(el);
        float breathe = guard != HeroElement.None ? 1.25f : 0.8f + 0.15f * MathF.Sin(time * 6f) + 2f * player.InfuseFlash;
        mat.AlbedoColor = new Color(c, 0.4f * breathe);
        mat.Emission = c;
        mat.EmissionEnergyMultiplier = 3.5f * breathe;
    }
}
