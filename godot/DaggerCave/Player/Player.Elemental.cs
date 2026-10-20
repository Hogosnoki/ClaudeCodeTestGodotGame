using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

/// <summary>
/// The elemental kits (<see cref="Elements"/>): an element is readied with its button (1, 2, 3; the d-pad), and the next use of a
/// button spends it on that button's version (<see cref="Elements.SingleName"/>; two charges, the pair's, <see cref="Elements.DualName"/>).
/// The Swordsman (earth, fire, frost): Swing (the attack: the Heaving Swing, Searing Strike, Razor Ice), the jump (Earthfall, Explosive
/// Lunge, Glacial Vault), the Hard Swing (ThunderCrush, Pyroclasty, Ice Axe; the duals, <c>Player.Duals.cs</c>) and Empower Hilt (its
/// element on every blow for a while). The Elementalist (fire, frost, wind): the bolt (the attack: Firebolt, Frostbolt, Gust, an element
/// lasting a few bolts), movement (the dodge button: Lift, Fire Walk, Tundra, Updraft), the fixed-range spell (the Burst button: Force
/// Blast, Burst, Snap, Cyclone) and Aid (the Extended button: Ward, Kindling, Frost Armor, Narrow Draft). Each version rests its button for
/// its own time (<see cref="Tune.ElementalSword"/>, <see cref="Tune.ElementalStaff"/>).
/// </summary>
public partial class Player
{
    /// <summary>This hero fights with elements (not the classic kit).</summary>
    public bool Elemental => Elements.On(Stats.Hero);

    // ---------------------------------------------------------------- modes
    // The hero is always in one of their elements (the mode): every ability is its version. One slot sits on top of the mode: the mode's
    // own element there is an overcharge (the next ability that has a greater version spends it), another element is an attribute (its
    // effect added to every blow and spell, until replaced). Pressing the attribute's element again switches the mode to it.

    private HeroElement _mode, _top;
    private float _modeCd;
    private readonly float[] _overCd = new float[5];
    private HeroElement _netMode, _netTop;

    /// <summary>The element the hero is in: all their abilities are its versions.</summary>
    public HeroElement Mode
    {
        get
        {
            if (IsRemote) return _netMode;
            if (!Elemental) return HeroElement.None;
            var els = Elements.Of(Stats.Hero);
            return _mode != HeroElement.None && Array.IndexOf(els, _mode) >= 0 ? _mode : Elements.Signature(Stats.Hero);
        }
    }
    /// <summary>What sits on top of the mode (none, the mode's own element for an overcharge, or another as the attribute).</summary>
    public HeroElement Top => IsRemote ? _netTop : _top;
    /// <summary>The mode's own element is on top: the next ability with a greater version is that.</summary>
    public bool Overcharged => Top != HeroElement.None && Top == Mode;
    /// <summary>Another element is on top: its effect rides on every blow and spell.</summary>
    public HeroElement Attribute => Top != HeroElement.None && Top != Mode ? Top : HeroElement.None;
    /// <summary>For the HUD: the mode's switch (0..1 left), and an element's overcharge recharging (0..1 left; seconds).</summary>
    public float ModeCooldownFrac => Math.Clamp(_modeCd / Math.Max(0.01f, Tune.Elements.ModeCooldown), 0f, 1f);
    public float ModeCooldown => Math.Max(0f, _modeCd);
    public float OverchargeFrac(HeroElement el) => Math.Clamp(_overCd[(int)el] / Math.Max(0.01f, Tune.Elements.OverchargeRecharge), 0f, 1f);
    public float OverchargeSeconds(HeroElement el) => Math.Max(0f, _overCd[(int)el]);

    /// <summary>The mode and what's on top, as the HUD shows them (the mode first).</summary>
    public IReadOnlyList<HeroElement> Charges => Mode == HeroElement.None ? Array.Empty<HeroElement>() : Top != HeroElement.None ? new[] { Mode, Top } : new[] { Mode };
    /// <summary>The element the hero's weapon glows with: the mode's.</summary>
    public HeroElement GlowElement => Mode;

    /// <summary>The element the next ability is a version of: always the mode's.</summary>
    public HeroElement Infused => Mode;
    /// <summary>Something's on top: the greater versions (an overcharge) or the mixes (an attribute) wait on their buttons.</summary>
    public bool DualReady => Top != HeroElement.None;
    /// <summary>The pair's ability on a button now: overcharged, the mode's greater version; with an attribute on top, the mix of the two
    /// ("" with nothing on top, or where the button has none).</summary>
    public string DualNameFor(ElemSlot slot) => Overcharged ? Elements.DualName(Stats.Hero, slot, Mode, Mode)
        : Attribute != HeroElement.None ? Elements.DualName(Stats.Hero, slot, Mode, Attribute) : "";
    /// <summary>What the button would do now (for the HUD): the greater version, or the mode's version.</summary>
    public string NameFor(ElemSlot slot) { var d = DualNameFor(slot); return d != "" ? d : Elements.SingleName(Stats.Hero, slot, Infused); }
    /// <summary>For the tests: elemental versions used, by name.</summary>
    public readonly List<string> ElementalUses = new();

    /// <summary>Overcharges are open: once dual charges are unlocked (<see cref="Meta.DualCharges"/>).</summary>
    public bool OverchargeOpen => Meta.DualCharges;

    /// <summary>An element's version's rest (<see cref="Tune.Elements.VersionsRest"/>; none while <see cref="Tune.Testing.NoCooldowns"/>).</summary>
    private static float ElemRest(float rest) => Tune.Elements.VersionsRest && !Tune.Testing.NoCooldowns ? rest : 0f;
    /// <summary>The next use waits on no cooldown (and costs nothing): only while cooldowns are off for testing.</summary>
    private bool FreeVersion => Tune.Testing.NoCooldowns || (!Tune.Elements.VersionsRest && Overcharged);

    /// <summary>An element's button as pressed: taken now, or (inside the press's short cooldown) as soon as it's over.</summary>
    private void BufferInfuse(int slot)
    {
        if (_infuseCd > 0) { _infuseQueued = slot; return; }
        TryInfuse(slot);
    }
    private float _infuseCd;
    private int _infuseQueued = -1;
    public float InfuseCooldownFrac => Math.Clamp(_infuseCd / Math.Max(0.01f, Tune.Elements.ChargeCooldown), 0f, 1f);
    public float InfuseCooldown => Math.Max(0f, _infuseCd);

    /// <summary>An element's button: the mode's own (an overcharge on top), another (it goes on top as the attribute, replacing what was
    /// there), or the attribute's again (the mode switches to it).</summary>
    public bool TryInfuse(int slot)
    {
        var els = Elements.Of(Stats.Hero);
        if (Dead || slot < 0 || slot >= els.Length || _infuseCd > 0) return false;
        _infuseCd = Tune.Elements.ChargeCooldown;
        var el = els[slot];
        var mode = Mode;
        var col = Elements.ColorOf(el);
        if (el == mode)
        {
            // the mode's own element: an overcharge on top (replacing an attribute)
            if (Overcharged) return false;
            if (!OverchargeOpen) { SayNo($"IN {Elements.Name(el).ToUpperInvariant()} MODE"); return false; }
            if (_overCd[(int)el] > 0) { SayNo($"OVERCHARGE {_overCd[(int)el]:0}s"); return false; }
            _top = el;
            WeaponTouch(el);
            G.Fx.Text(GlobalPosition + new Vector2(0, -30), $"{Elements.Name(el).ToUpperInvariant()} OVERCHARGED", col, 11, 0.8f);
            G.Fx.Ring(GlobalPosition + new Vector2(0, -6), 20, new Color(col, 0.95f), 0.35f, FxLayer.RingStyle.Burst);
            G.Sfx.Play(ElementSound(el), GlobalPosition, -6, 0.05f, 0.9f);
            Anim.Flash(0.3f);
            Anim.FlashColor = col;
            return true;
        }
        if (_top == el)
        {
            // the attribute's element again: the mode switches to it
            if (_modeCd > 0) { SayNo($"MODE {_modeCd:0.0}s"); return false; }
            _mode = el;
            _top = HeroElement.None;
            _modeCd = Tune.Testing.NoCooldowns ? 0f : Tune.Elements.ModeCooldown;
            WeaponTouch(el);
            G.Fx.Text(GlobalPosition + new Vector2(0, -30), $"{Elements.Name(el).ToUpperInvariant()} MODE", col, 12, 0.9f);
            G.Fx.Ring(GlobalPosition + new Vector2(0, -6), 26, new Color(col, 0.9f), 0.4f, FxLayer.RingStyle.Dashed);
            G.Sfx.Play(ElementSound(el), GlobalPosition, -4, 0.05f, 0.7f);
            Anim.Flash(0.35f);
            Anim.FlashColor = col;
            return true;
        }
        // another element: on top, as the attribute (replacing whatever was there)
        _top = el;
        WeaponTouch(el);
        G.Fx.Text(GlobalPosition + new Vector2(0, -30), $"+{Elements.Name(el).ToUpperInvariant()}", col, 10, 0.7f);
        G.Sfx.Play(ElementSound(el), GlobalPosition, -10, 0.05f, 1.5f);
        Anim.Flash(0.2f);
        Anim.FlashColor = col;
        return true;
    }

    private static string ElementSound(HeroElement el) => el == HeroElement.Fire ? "lava" : el == HeroElement.Frost ? "clink" : el == HeroElement.Wind ? "dodge" : "rock";

    /// <summary>For the tests: straight into a mode (no cooldown), nothing on top; an overcharge on top at once.</summary>
    public void TestMode(HeroElement el) { _mode = el; _top = HeroElement.None; _modeCd = 0; _infuseCd = 0; _infuseQueued = -1; }
    public void TestOvercharge() { _overCd[(int)Mode] = 0; _top = Mode; }
    public void TestResetModeCooldowns() { _modeCd = 0; _infuseCd = 0; Array.Clear(_overCd); }
    public void TestAttribute(HeroElement el) { _top = el; }
    public void TestSpendOvercharge() => SpendPair();

    /// <summary>The attribute's effect on a creature a blow or spell of this hero reached: its element's build-up (wind: a shove).</summary>
    public void AttributeHit(Enemy e, Vector2 from)
    {
        var at = Attribute;
        if (at == HeroElement.None || e == null || !IsInstanceValid(e) || e.Dead) return;
        if (at == HeroElement.Wind) { e.Shove((e.GlobalPosition - from).Normalized() * Tune.Elements.AttributeShove + new Vector2(0, -40f)); return; }
        e.Build(at, Tune.Elements.AttributeBuild, Stats.DamageMult, this);
    }

    /// <summary>For the 3D view: a moment ago an element went onto the weapon (its glow flares up as it takes).</summary>
    public float InfuseFlash => Math.Max(0f, _infuseFlash);
    private float _infuseFlash;

    /// <summary>The element going onto the weapon: a puff of flame along it, a sheen of ice running up it, a crackle of grit, a twist of wind.</summary>
    private void WeaponTouch(HeroElement el)
    {
        _infuseFlash = 0.45f;
        // (the weapon, roughly: the sword held up and forward from the hand; the staff from the hand up to its orb)
        Vector2 a, b;
        if (Stats.Hero == HeroKind.Elementalist) { a = GlobalPosition + new Vector2(Facing * 7, 8); b = CastPoint; }
        else { a = GlobalPosition + new Vector2(Facing * 6, -2); b = a + new Vector2(Facing, -0.55f).Normalized() * 28f; }
        var col = Elements.ColorOf(el);
        switch (el)
        {
            case HeroElement.Fire:
                // a puff of flame bursting along it
                for (int k = 0; k <= 6; k++)
                {
                    var at = a.Lerp(b, k / 6f);
                    G.Fx.Directional(at, Vector2.Up, 0.5f, G.Chance(0.5f) ? new Color(1f, 0.45f, 0.1f) : new Color(1f, 0.8f, 0.3f), 3, G.Range(60, 140), G.Range(2.6f, 3.8f), 0.4f, -90, 0);
                }
                G.Fx.Flash(b, 12, col, 0.12f);
                G.Fx.Smoke(b + new Vector2(0, -6), 2, new Color(0.3f, 0.27f, 0.25f, 0.4f), 25f);
                break;
            case HeroElement.Frost:
                // a sheen of ice running up it from hand to tip, rime left glinting
                for (int k = 0; k <= 7; k++)
                {
                    var at = a.Lerp(b, k / 7f);
                    G.Fx.Burst(at, new Color(0.92f, 0.98f, 1f, 0.95f), 1, 12, 3f - k * 0.15f, 0.12f + k * 0.035f, 0, 0);
                }
                G.Fx.Glint(b, col, 9f);
                G.Fx.Burst(b, new Color(col, 0.9f), 6, 60, 1.6f, 0.35f, 40);
                break;
            case HeroElement.Earth:
                // grit and stone dust crackling along it
                for (int k = 0; k <= 5; k++) G.Fx.Burst(a.Lerp(b, k / 5f), new Color(col.Darkened(0.2f), 0.9f), 2, 50, 1.8f, 0.35f, 300);
                G.Fx.Dust(a, 3, 1f, new Color(0.55f, 0.46f, 0.36f, 0.6f));
                break;
            default:
                // a twist of wind wound round it
                for (int k = 0; k < 8; k++)
                {
                    var at = a.Lerp(b, k / 7f);
                    var along = (b - a).Normalized();
                    G.Fx.Directional(at, along.Orthogonal() * (k % 2 == 0 ? 1 : -1), 0.3f, new Color(col, 0.8f), 1, 120, 1.6f, 0.25f, 0, 0);
                }
                break;
        }
    }

    /// <summary>The element an ability is a version of: the mode's (nothing is spent).</summary>
    private HeroElement SpendElement() => Mode;

    /// <summary>What's on top, used by a pair's ability: an overcharge is spent on the greater version (that element's overcharge starts
    /// coming back); an attribute stays, and its mixes with the mode go on being there.</summary>
    private (HeroElement a, HeroElement b) SpendPair()
    {
        var m = Mode;
        if (Attribute != HeroElement.None) return Elements.Pair(m, Attribute);
        _top = HeroElement.None;
        _overCd[(int)m] = Tune.Testing.NoCooldowns ? 0f : Tune.Elements.OverchargeRecharge;
        return (m, m);
    }

    /// <summary>What's on top goes (the hero fell); the mode stays.</summary>
    private void ClearCharges() => _top = HeroElement.None;
    public void TestClearCharges() { ClearCharges(); TestResetModeCooldowns(); }

    private void Used(string what)
    {
        ElementalUses.Add(what);
        G.Fx.Text(GlobalPosition + new Vector2(0, -32), what.ToUpperInvariant(), Elements.ColorOf(_lastEl), 11, 0.7f);
    }
    private HeroElement _lastEl;

    private void TickElemental(float dt)
    {
        if (!Elemental) return;
        if (_infuseCd > 0 && (_infuseCd -= dt) <= 0 && _infuseQueued >= 0) { int q = _infuseQueued; _infuseQueued = -1; TryInfuse(q); }
        if (_infuseFlash > 0) _infuseFlash -= dt;
        if (_modeCd > 0) _modeCd -= dt;
        for (int k = 0; k < _overCd.Length; k++) if (_overCd[k] > 0) _overCd[k] -= dt;
        // (the mode stays; what's on top goes with a fall)
        if (Dead && _top != HeroElement.None) ClearCharges();
        TickDuals(dt);
        TickAxe(dt);
        if (IsWarden) TickWardenElements(dt);
        if (_swingElemRest > 0) _swingElemRest -= dt;
        if (_jumpElemRest > 0) _jumpElemRest -= dt;
        if (_lungeLeapT > 0) _lungeLeapT -= dt;
        if (_dualBoltCd > 0) _dualBoltCd -= dt;
        if (_hiltT > 0 && (_hiltT -= dt) <= 0) { _hiltEl = _hiltEl2 = HeroElement.None; _hiltBig = 1f; }
        if (_hiltT > 0 && Engine.GetPhysicsFrames() % 5 == 0)
        {
            var along = SwingAim != Vector2.Zero ? SwingAim : new Vector2(Facing, -0.2f).Normalized();
            G.Fx.Ember(GlobalPosition + new Vector2(0, -4) + along * G.Range(6, 18), _hiltEl == HeroElement.None ? new Color(1f, 0.85f, 0.5f) : Elements.ColorOf(_hiltEl));
        }
    }

    // ================================================================== the Swordsman

    /// <summary>The swing under way: its element, what it adds to a creature's build-up, and whether it shatters the frozen.</summary>
    private HeroElement _swingEl;
    private float _swingBuild, _swingElemRest;
    private bool _swingShatter;
    /// <summary>Empower Hilt: the element every blow carries (none: they hit harder), and for how long.</summary>
    private HeroElement _hiltEl;
    private float _hiltT, _heaveCdTotal = 1f;
    public float HiltLeft => _hiltT;
    public HeroElement HiltElement => _hiltEl;
    /// <summary>For the HUD: the Hard Swing's rest, 0 = ready.</summary>
    public float HardSwingCooldownFrac => Math.Clamp((_heaveCd - HeaveSpareTime) / Math.Max(0.01f, _heaveCdTotal), 0, 1);
    /// <summary>For the HUD: a readied element can't go on a swing yet (it will on the next once this is 0).</summary>
    public float SwingElementRest => Math.Max(0f, _swingElemRest);
    private float _swingRestTotal = 1f;
    public float SwingElementRestFrac => Math.Clamp(_swingElemRest / _swingRestTotal, 0f, 1f);
    /// <summary>For the HUD: seconds left on the Hard Swing, and on the ability (Empower Hilt; the Elementalist's Aid).</summary>
    public float HardSwingSeconds => Math.Max(0f, _heaveCd - HeaveSpareTime);
    public float AbilitySeconds => AbilityChargeReady ? 0f : Math.Max(0f, AbilityCooldowns.Min());

    private void ClearSwingElement() { _swingEl = _swingEl2 = HeroElement.None; _swingBuild = _swingBuild2 = 0; _swingShatter = false; _wSwing = ""; ClearDualSwing(); }
    /// <summary>A pair's swing carries a second element too (its build-up on each blow).</summary>
    private HeroElement _swingEl2;
    private float _swingBuild2;

    /// <summary>The attack with something on top: the mode's greater swing (Fault Strike, Flame Crescent, Frost Lance) or its mix with the
    /// attribute (Magma Strike, Permafrost Strike, Steam Strike).</summary>
    private bool DualSwing(Vector2 aim)
    {
        var (a, b) = SpendPair();
        string name = Elements.DualName(Stats.Hero, ElemSlot.Attack, a, b);
        if (_dodgeT > 0) _dodgeT = 0;
        StartSwing(aim);
        var dir = _swingDir;
        var origin = GlobalPosition + new Vector2(0, -3);
        float blow = BaseDamage * Stats.DamageMult, full = Tune.Elements.Full;
        switch (name)
        {
            case "Fault Strike":
                // a heavy blow of stone, and the floor splits on ahead of it
                _swingDmg *= 1.5f; _swingEl = HeroElement.Earth; _swingBuild = full; _swingShatter = true;
                Launch(new Fissure { Position = FloorAt(GlobalPosition), Dir = Math.Abs(dir.X) > 0.1f ? Math.Sign(dir.X) : Facing, Range = 150f, Damage = blow });
                break;
            case "Flame Crescent":
            {
                // a searing blow, and a crescent of flame bursting on from its tip
                _swingDmg *= 1.25f; _swingEl = HeroElement.Fire; _swingBuild = Tune.ElementalSword.MediumBuild;
                var at = origin + dir * 34f;
                foreach (var e in G.Enemies.ToArray())
                {
                    if (e.Dead || !e.CanBeHit || e.GlobalPosition.DistanceTo(at) > 40f + e.HitRadius) continue;
                    float d = e.Hurt(blow * 0.6f, dir * 60f, e.GlobalPosition, DamageKind.Fire);
                    if (d > 0) OnDealtDamage(d);
                    if (!e.Dead) e.Build(HeroElement.Fire, 50f, Stats.DamageMult, this);
                }
                G.Fx.Explosion(at, Elements.ColorOf(HeroElement.Fire), 0.6f);
                break;
            }
            case "Frost Lance":
            {
                // the cut, and a lance of ice thrown on along it
                _swingEl = HeroElement.Frost; _swingBuild = Tune.ElementalSword.MediumBuild;
                var lance = new ElementBolt
                {
                    Position = origin + dir * 10f, Dir = dir, Range = 220f, Caster = this, Frost = true,
                    BuildMult = full / Math.Max(1f, Tune.ElementalStaff.FrostBuild), Damage = blow * 1.2f,
                };
                G.Spawn(lance);
                NetSync.HeroVisual(lance);
                G.Sfx.Play("clink", origin, -4, 0.05f, 1.5f);
                break;
            }
            case "Magma Strike":
                // a blow of molten rock: fire and earth, and a little pool of magma where it lands
                _swingDmg *= 1.4f; _swingEl = HeroElement.Fire; _swingBuild = 50f; _swingEl2 = HeroElement.Earth; _swingBuild2 = 50f;
                Launch(new MagmaPool { Position = FloorAt(GlobalPosition + dir * 24f), Half = 30f, Seconds = 2.5f, TickDamage = blow * 0.1f });
                break;
            case "Permafrost Strike":
                // frozen stone: earth and frost, shattering the frozen
                _swingDmg *= 1.4f; _swingEl = HeroElement.Earth; _swingBuild = 50f; _swingEl2 = HeroElement.Frost; _swingBuild2 = 50f; _swingShatter = true;
                G.Fx.Burst(origin + dir * 16f, new Color(0.9f, 0.97f, 1f), 6, 80, 1.8f, 0.3f, 40);
                break;
            default:
            {
                // Steam Strike: fire and frost meet in the blade, and a cloud of steam bursts where it strikes (friends in it are hidden)
                _swingDmg *= 1.3f; _swingEl = HeroElement.Fire; _swingBuild = 30f; _swingEl2 = HeroElement.Frost; _swingBuild2 = 30f;
                var cloud = new SmokeCloud { Position = origin + dir * 24f, Radius = 34f, Life = 3f };
                G.Spawn(cloud);
                NetSync.HeroVisual(cloud);
                break;
            }
        }
        _lastEl = a;
        Used(name, Elements.DualColor(a, b));
        return true;
    }

    /// <summary>The attack button: a plain swing, or with an element its version (the element rests a moment after).</summary>
    private bool ElementSwing(Vector2 aim, bool held)
    {
        if (_swingCd > 0 || (held && _dodgeT > 0)) return false;
        if (DualNameFor(ElemSlot.Attack) != "") return DualSwing(aim);
        var el = Infused;
        if (el == HeroElement.None || _swingElemRest > 0) return TrySwing(aim, held);
        if (el == HeroElement.Earth)
        {
            // the Heaving Swing: on your feet only (in the air the swing is plain, and the earth waits)
            if (InWater || !IsOnFloor()) return TrySwing(aim, held);
            SpendElement();
            _lastEl = el;
            _swingElemRest = _swingRestTotal = ElemRest(Tune.ElementalSword.SwingRestEarth);
            _dodgeT = 0;
            float dir = Math.Abs(aim.X) > 0.2f ? Math.Sign(aim.X) : Facing;
            Facing = dir;
            StartHeave();
            _swingDmg = BaseDamage * Stats.DamageMult * Tune.ElementalSword.HeaveDamage * Stats.HeaveDamageMult;
            _swingEl = el; _swingBuild = Tune.ElementalSword.EarthBuild;
            Used("Heaving Swing");
            return true;
        }
        SpendElement();
        _lastEl = el;
        _swingElemRest = _swingRestTotal = ElemRest(el == HeroElement.Fire ? Tune.ElementalSword.SwingRestFire : Tune.ElementalSword.SwingRestFrost);
        if (_dodgeT > 0) _dodgeT = 0;
        StartSwing(aim);
        _swingDmg *= Tune.ElementalSword.MediumDamage;
        _swingEl = el; _swingBuild = Tune.ElementalSword.MediumBuild;
        Used(el == HeroElement.Fire ? "Searing Strike" : "Razor Ice");
        return true;
    }

    /// <summary>The second ability: the Hard Swing, plain or by its element (ThunderCrush, Pyroclasm, Ice Axe), resting by which.</summary>
    private bool TryHardSwing(Vector2 aim)
    {
        if ((_heaveCd > HeaveSpareTime && !FreeVersion) || Heaving) return false;
        if (InWater || !IsOnFloor()) { _heaveWanted = true; return false; }
        _heaveWanted = false;
        if (DualNameFor(ElemSlot.Heavy) != "") return DualSword(aim);
        var el = SpendElement();
        _lastEl = el;
        float rest, dmg, wind = 1f;
        string name;
        switch (el)
        {
            case HeroElement.Earth: rest = Tune.ElementalSword.CrushRest; dmg = Tune.ElementalSword.CrushDamage; wind = Tune.ElementalSword.CrushWindup; name = "ThunderCrush"; break;
            case HeroElement.Fire: rest = Tune.ElementalSword.PyroRest; dmg = Tune.ElementalSword.PyroDamage; name = "Pyroclasty"; break;
            case HeroElement.Frost: rest = Tune.ElementalSword.AxeRest; dmg = Tune.ElementalSword.AxeDamage; name = "Ice Axe"; break;
            default: rest = Tune.ElementalSword.HardRest; dmg = Tune.ElementalSword.HardDamage; name = "Hard Swing"; break;
        }
        float scale = Stats.HeaveCooldown / Math.Max(0.01f, Tune.Swordsman.HeaveCooldown);
        if (el != HeroElement.None) rest = ElemRest(rest);
        if (rest > 0f) { _heaveCdTotal = rest * scale; _heaveCd = Math.Max(0f, _heaveCd) + _heaveCdTotal; }
        _dodgeT = 0;
        float dir = Math.Abs(aim.X) > 0.2f ? Math.Sign(aim.X) : Facing;
        Facing = dir;
        StartHeave();
        _swingDmg = BaseDamage * Stats.DamageMult * dmg * Stats.HeaveDamageMult;
        if (wind > 1f) { float extra = _windup * (wind - 1f); _windup += extra; _heaveRootT += extra; }
        _swingEl = el; _swingBuild = el == HeroElement.None ? 0f : Tune.Elements.Full; _swingShatter = el == HeroElement.Earth;
        if (el == HeroElement.Frost) StartAxe(1, Tune.ElementalSword.AxeWindup);
        Used(name);
        return true;
    }

    // ================================================================== the Ice Axe and the Glacier Cleaver

    /// <summary>The axe under way: 1 the Ice Axe, 2 the Glacier Cleaver (0: none).</summary>
    private int _axe;
    private int _netAxe;
    private float _netAxeStage;
    private float _axeFxT, _axeLastGrow;

    /// <summary>For the 3D view: the axe of ice being swung (1 the Ice Axe, 2 the Glacier Cleaver; 0 none).</summary>
    public int AxeKind => IsRemote ? _netAxe : _swingT >= 0 && _heave ? _axe : 0;
    /// <summary>Where the axe's blow is: 0 to 1 the wind-up (the sword raised behind the head, the head growing), 1 to 2 the swing, 2 to 3
    /// the follow-through.</summary>
    public float AxeStage
    {
        get
        {
            if (IsRemote) return _netAxeStage;
            if (AxeKind == 0) return 0f;
            float sweep = SweepT;
            if (sweep < 0) return Math.Clamp(_swingT / Math.Max(0.01f, _windup), 0f, 1f);
            if (sweep < _active) return 1f + sweep / Math.Max(0.01f, _active);
            return 2f + Math.Clamp((sweep - _active) / Tune.Swordsman.HeaveRecover, 0f, 1f);
        }
    }
    /// <summary>How far the axe's head has grown out of the blade (0 to 1): through the wind-up (the Glacier Cleaver's slower, and later),
    /// whole through the swing, shattering away at its end.</summary>
    public static float AxeGrowAt(int kind, float stage)
    {
        if (kind == 0) return 0f;
        float grow = kind == 2 ? W3.SmoothStep(0.22f, 0.94f, stage) : W3.SmoothStep(0.3f, 0.82f, stage);
        return grow * (1f - W3.SmoothStep(2.55f, 3f, stage));
    }
    public float AxeGrow => AxeGrowAt(AxeKind, AxeStage);

    /// <summary>The Hard Swing becomes the axe's: a longer wind-up (the sword raised over the head and behind while the head grows), and a blow
    /// that comes down in front only.</summary>
    private void StartAxe(int kind, float windMult)
    {
        _axe = kind;
        _axeLastGrow = 0f;
        _axeFxT = 0f;
        float extra = _windup * (windMult - 1f);
        _windup += extra; _heaveRootT += extra;
        // (the clip's wind-up frames are the axe's wind-up)
        Anim.Once("heave", 4, 7f / 24f / _windup);
        _swingDir = new Vector2(Facing, 0.3f).Normalized();
        _swingArc = Mathf.DegToRad(Tune.ElementalSword.AxeArcDegrees) * (kind == 2 ? Tune.Dual.AxeArc : 1f);
        G.Fx.Converge(GlobalPosition + new Vector2(0, -8), 26, new Color(0.85f, 0.96f, 1f), 14, 0.35f);
        G.Sfx.Play("clink", GlobalPosition, -6, 0.05f, kind == 2 ? 0.45f : 0.7f);
    }

    /// <summary>The axe's frost: motes of ice drawn to the raised blade as its head grows (each step of it with a crack of ice), a burst of
    /// white as it comes down, and shards as it melts away.</summary>
    private void TickAxe(float dt)
    {
        int kind = AxeKind;
        if (kind == 0 || IsRemote) { if (_swingT < 0) _axe = 0; return; }
        float stage = AxeStage, grow = AxeGrow;
        var white = new Color(0.92f, 0.98f, 1f);
        var blue = Elements.ColorOf(HeroElement.Frost);
        float big = kind == 2 ? 1.8f : 1f;
        // (where the head is: up behind the head through the wind-up, then sweeping down in front)
        var head = stage < 1f
            ? GlobalPosition + new Vector2(-Facing * 12f * big, -30f - 6f * big)
            : GlobalPosition + new Vector2(Facing * _swingReach * 0.7f, Mathf.Lerp(-30f, 8f, Math.Clamp(stage - 1f, 0f, 1f)));
        if ((_axeFxT -= dt) <= 0f && stage < 2.5f)
        {
            _axeFxT = 0.05f;
            if (stage < 1f)
            {
                G.Fx.Mote(head + G.RandDir() * G.Range(18f, 34f) * big, head, G.Chance(0.3f) ? white : blue);
                if (G.Chance(0.35f)) G.Fx.Glint(head + G.RandDir() * 8f * big, blue, 2f + 1.5f * big);
                G.Fx.Burst(GlobalPosition + new Vector2(G.Range(-10f, 10f), 12f), new Color(blue, 0.6f), 1, 30, 2.4f, 0.5f, -30, 0);
            }
        }
        // each step of the head growing: a crack of ice, rising
        if (grow - _axeLastGrow >= 0.25f && stage < 1f)
        {
            _axeLastGrow = grow;
            G.Fx.Burst(head, white, (int)(6 * big), 90, 2f, 0.3f, 0);
            G.Sfx.Play("clink", head, -8, 0.05f, (kind == 2 ? 0.5f : 0.8f) + grow * 0.5f);
        }
        if (stage >= 1f && _axeLastGrow < 2f)
        {
            // released: down it comes, trailing white
            _axeLastGrow = 2f;
            G.Fx.Flash(head, 14f * big, white, 0.12f);
            G.Fx.Directional(head, new Vector2(Facing, 0.8f).Normalized(), 0.5f, white, (int)(12 * big), 240, 2.4f, 0.35f, 100, 0);
            G.Sfx.Play("dodge", GlobalPosition, -4, 0.05f, kind == 2 ? 0.5f : 0.75f);
        }
        if (stage >= 2.55f && _axeLastGrow < 3f)
        {
            _axeLastGrow = 3f;
            G.Fx.Burst(head, white, (int)(10 * big), 140, 2.2f, 0.45f, 300);
            G.Sfx.Play("clink", head, -6, 0.05f, 1.3f);
        }
    }

    /// <summary>The ability: Empower Hilt, plain (harder blows) or lending every blow its element for a while.</summary>
    private bool TryHilt()
    {
        bool free = FreeVersion;
        if ((!AbilityChargeReady && !free) || (_hiltT > 0 && !free)) return false;
        if (DualNameFor(ElemSlot.Aid) != "") return DualHilt(free);
        _hiltEl2 = HeroElement.None; _hiltBig = 1f;
        var el = SpendElement();
        _lastEl = el;
        if (!free)
        {
            SpendAbilityCharge();
            float rest = el switch
            {
                HeroElement.Earth => Tune.ElementalSword.HiltRestEarth, HeroElement.Frost => Tune.ElementalSword.HiltRestFrost,
                HeroElement.Fire => Tune.ElementalSword.HiltRestFire, _ => Tune.ElementalSword.HiltRest,
            } * Stats.AbilityCdMult;
            SetSpentAbilityRest(rest);
        }
        _hiltEl = el;
        _hiltT = Tune.ElementalSword.HiltSeconds;
        var col = el == HeroElement.None ? new Color(1f, 0.85f, 0.5f) : Elements.ColorOf(el);
        G.Fx.Flash(GlobalPosition + new Vector2(0, -6), 18, col);
        G.Fx.Ring(GlobalPosition + new Vector2(0, -4), 16, col);
        G.Sfx.Play("levelup", GlobalPosition, -8, 0.05f, 0.75f);
        Anim.Flash(0.35f);
        Used(el == HeroElement.None ? "Empower Hilt" : $"{Elements.Name(el)} Hilt");
        return true;
    }

    /// <summary>A pair's hilt: a second element on every blow (the mix), or the mode's twice as strong for half again as long (overcharged).</summary>
    private HeroElement _hiltEl2;
    private float _hiltBig = 1f;

    /// <summary>Empower Hilt with something on top: the mode's greater hilt (Stoneheart, Blazing, Glacial) or its mix with the attribute
    /// (Magma, Permafrost, Steam: both elements on every blow).</summary>
    private bool DualHilt(bool free)
    {
        var (a, b) = SpendPair();
        string name = Elements.DualName(Stats.Hero, ElemSlot.Aid, a, b);
        if (!free) { SpendAbilityCharge(); SetSpentAbilityRest(Tune.ElementalSword.HiltRestEarth * Stats.AbilityCdMult); }
        _hiltEl = a;
        _hiltEl2 = a == b ? HeroElement.None : b;
        _hiltBig = a == b ? 2f : 1f;
        _hiltT = Tune.ElementalSword.HiltSeconds * (a == b ? 1.5f : 1f);
        var col = Elements.DualColor(a, b);
        G.Fx.Flash(GlobalPosition + new Vector2(0, -6), 22, col);
        G.Fx.Ring(GlobalPosition + new Vector2(0, -4), 20, new Color(col, 0.95f), 0.35f, FxLayer.RingStyle.Burst);
        G.Sfx.Play("levelup", GlobalPosition, -6, 0.05f, 0.65f);
        Anim.Flash(0.35f);
        _lastEl = a;
        Used(name, col);
        return true;
    }

    /// <summary>One element of the hilt on a blow (<paramref name="mult"/> x its usual).</summary>
    private void HiltTouch(Enemy e, HeroElement el, float mult)
    {
        if (e.Dead) return;
        switch (el)
        {
            case HeroElement.Earth: e.Sap(Tune.Elements.SapSeconds * mult); break;
            case HeroElement.Frost: e.Build(HeroElement.Frost, Tune.ElementalSword.HiltFrost * mult, Stats.DamageMult, this); break;
            case HeroElement.Fire: e.Build(HeroElement.Fire, Tune.ElementalSword.HiltFire * mult, Stats.DamageMult, this); break;
        }
    }

    /// <summary>The use just spent rests for <paramref name="seconds"/> (its own time, not the ability's usual one).</summary>
    private void SetSpentAbilityRest(float seconds)
    {
        int at = -1; float most = -1;
        for (int k = 0; k < _abilityCd.Length; k++) if (_abilityCd[k] > most) { most = _abilityCd[k]; at = k; }
        if (at >= 0) _abilityCd[at] = seconds;
        _abilityRestTotal = seconds;
    }
    private float _abilityRestTotal = 1f;

    /// <summary>A blade blow landed: what the element of the swing, and of an empowered hilt, does to the creature.</summary>
    private void ElementalBladeHit(Enemy e)
    {
        if (!Elemental || e.Dead) return;
        if (_swingEl != HeroElement.None && _swingBuild > 0) e.Build(_swingEl, _swingBuild, Stats.DamageMult, this);
        if (_swingEl2 != HeroElement.None && _swingBuild2 > 0 && !e.Dead) e.Build(_swingEl2, _swingBuild2, Stats.DamageMult, this);
        AttributeHit(e, GlobalPosition);
        DualBladeHit(e);
        if (IsWarden) WardenBladeHit(e);
        if (_hiltT <= 0 || e.Dead) return;
        HiltTouch(e, _hiltEl, _hiltBig);
        if (_hiltEl2 != HeroElement.None) HiltTouch(e, _hiltEl2, 1f);
    }

    /// <summary>How much harder a blade blow lands from a plain Empower Hilt.</summary>
    private float HiltDamageMult => Elemental && _hiltT > 0 && _hiltEl == HeroElement.None ? 1f + Tune.ElementalSword.HiltDamage : 1f;

    // ================================================================== the elements' movement (the dodge button)

    /// <summary>The movement's element rests (the dodge is plain meanwhile and the charge waits), the leap's carry, and Earthfall waiting to land.</summary>
    private float _jumpElemRest, _lungeLeapT, _lungeLeapVX;
    private HeroElement _leapEl;
    private bool _earthfall;
    /// <summary>The last Glacial Vault's ice decoy (for the tests).</summary>
    public IceDecoy LastDecoy { get; private set; }
    private float _jumpRestTotal = 1f;
    /// <summary>An elemental lift (Lift, Frost Tower): the speed upward the movement this frame begins from.</summary>
    private float? _liftTo;
    public float JumpElementRest => Math.Max(0f, _jumpElemRest);
    public float JumpElementRestFrac => Math.Clamp(_jumpElemRest / _jumpRestTotal, 0f, 1f);

    /// <summary>A jump from the ground: with an element readied (and its rest over), its version. Returns the jump's velocity.</summary>
    /// <summary>The motion an element's movement set off this frame (applied before the hero's own movement).</summary>
    private Vector2? _moveTo;

    /// <summary>The dodge button would cast the readied element's movement (its rest over, out of the water; the Swordsman's leaps only from
    /// the ground).</summary>
    private bool ElementMoveReady => Elemental && Infused != HeroElement.None && _jumpElemRest <= 0 && !InWater && (Stats.Hero != HeroKind.Swordsman || IsOnFloor());

    /// <summary>The dodge button with a charge readied: the element's movement. The Swordsman's leap from the ground (Explosive Lunge,
    /// Glacial Vault, Earthfall); the Elementalist's Fire Walk, Tundra or Updraft (two charges, the pair's).</summary>
    private bool ElementDodge(in PlayerInput inp)
    {
        float jumpV = BaseJumpV * MathF.Sqrt(Stats.JumpMult);
        var v = Velocity;
        if (Stats.Hero == HeroKind.Swordsman)
        {
            v = SwordMove(new Vector2(v.X, -jumpV), jumpV, inp);
        }
        else v = StaffJump(v, jumpV, inp);
        _moveTo = v;
        return true;
    }

    private Vector2 SwordMove(Vector2 v, float jumpV, in PlayerInput inp)
    {
        if (DualNameFor(ElemSlot.Move) != "") return DualLeap(v, jumpV, inp);
        var el = SpendElement();
        v = SwordLeap(v, jumpV, inp, el);
        Used(Elements.SingleName(Stats.Hero, ElemSlot.Move, el));
        return v;
    }

    /// <summary>What the Earthfall brings down where it lands besides its shockwave (a pair's leap): its strength, and magma or frost there.</summary>
    private float _fallMult = 1f;
    private HeroElement _fallLeaves;

    /// <summary>The dodge with something on top: the mode's greater leap (Meteor Fall, Blast Lunge, Frozen Effigy) or its mix with the
    /// attribute (Magma Leap, Frostfall, Steam Vault).</summary>
    private Vector2 DualLeap(Vector2 v, float jumpV, in PlayerInput inp)
    {
        var (a, b) = SpendPair();
        string name = Elements.DualName(Stats.Hero, ElemSlot.Move, a, b);
        var feet = GlobalPosition + new Vector2(0, 12);
        switch (name)
        {
            case "Meteor Fall":
                // the Earthfall, but you come down like a falling star: a far greater shockwave
                v = SwordLeap(v, jumpV, inp, HeroElement.Earth);
                _fallMult = 1.6f;
                break;
            case "Blast Lunge":
                // a greater blast and a longer leap, fire burning where you pass
                v = SwordLeap(v, jumpV, inp, HeroElement.Fire);
                _lungeLeapVX *= 1.3f; v.X = _lungeLeapVX;
                Launch(new FireTrail { Position = GlobalPosition, Seconds = 2.5f });
                break;
            case "Frozen Effigy":
            {
                // the Glacial Vault, but the likeness left is harder (four blows), and as it shatters it freezes those beside it solid
                v = SwordLeap(v, jumpV, inp, HeroElement.Frost);
                if (LastDecoy != null && IsInstanceValid(LastDecoy)) { LastDecoy.Hits = 4; LastDecoy.MaxHp = LastDecoy.Hp = 4f; LastDecoy.Great = true; }
                break;
            }
            case "Magma Leap":
                v = SwordLeap(v, jumpV, inp, HeroElement.Earth);
                _fallLeaves = HeroElement.Fire;
                break;
            case "Frostfall":
                v = SwordLeap(v, jumpV, inp, HeroElement.Earth);
                _fallLeaves = HeroElement.Frost;
                break;
            default:
            {
                // Steam Vault: the spring back, and a cloud of steam where you stood (friends in it are hidden)
                v = SwordLeap(v, jumpV, inp, HeroElement.Frost);
                var cloud = new SmokeCloud { Position = feet + new Vector2(0, -14), Radius = 46f, Life = 4f };
                G.Spawn(cloud);
                NetSync.HeroVisual(cloud);
                break;
            }
        }
        _lastEl = a;
        Used(name, Elements.DualColor(a, b));
        return v;
    }

    /// <summary>One element's leap (Explosive Lunge, Glacial Vault, Earthfall). Returns the hero's motion from here.</summary>
    private Vector2 SwordLeap(Vector2 v, float jumpV, in PlayerInput inp, HeroElement el)
    {
        _lastEl = el;
        float power = Stats.DamageMult, blow = BaseDamage * power;
        var feet = GlobalPosition + new Vector2(0, 12);
        var col = Elements.ColorOf(el);
        switch (el)
        {
            case HeroElement.Fire:
            {
                // Explosive Lunge: the ground bursts behind you and throws you forward, low
                _jumpElemRest = _jumpRestTotal = ElemRest(Tune.ElementalSword.LungeRest);
                float dir = Math.Abs(inp.Move.X) > 0.2f ? Math.Sign(inp.Move.X) : Facing;
                Facing = dir;
                Anim.Face((int)Facing, instant: true);
                _lungeLeapVX = dir * Tune.ElementalSword.LungeSpeed;
                _lungeLeapT = Tune.ElementalSword.LungeCarry;
                _leapEl = HeroElement.Fire;
                v = new Vector2(_lungeLeapVX, -jumpV * Tune.ElementalSword.LungeLift);
                foreach (var e in G.Enemies.ToArray())
                {
                    if (e.Dead || !e.CanBeHit || e.GlobalPosition.DistanceTo(feet) > Tune.ElementalSword.LungeRadius + e.HitRadius) continue;
                    var away = (e.GlobalPosition - feet).Normalized();
                    float d = e.Hurt(blow * Tune.ElementalSword.LungeDamage, away * 160f, e.GlobalPosition, DamageKind.Fire);
                    if (d > 0) OnDealtDamage(d);
                    if (!e.Dead) e.Build(HeroElement.Fire, Tune.ElementalSword.LungeBuild, power, this);
                }
                GasCloud.IgniteAt(feet, Tune.ElementalSword.LungeRadius);
                G.Fx.Explosion(feet + new Vector2(-dir * 8, -4), col, 0.7f);
                G.Fx.Directional(feet, new Vector2(-dir, -0.3f).Normalized(), 0.5f, col, 14, 260, 2.6f, 0.4f, 200, 0);
                G.Fx.AddShake(2.5f);
                G.Sfx.Play("lava", feet, -2, 0.05f, 1.1f);
                Afterimage.Spawn(Anim, col, 0.25f);
                break;
            }
            case HeroElement.Frost:
            {
                // Glacial Vault: you spring back off a burst of ice, and leave your likeness in ice where you stood (creatures go for it);
                // what stands beside you is caught by the burst
                _jumpElemRest = _jumpRestTotal = ElemRest(Tune.ElementalSword.VaultRest);
                float back = -Facing;
                _lungeLeapVX = back * Tune.ElementalSword.VaultBack;
                _lungeLeapT = Tune.ElementalSword.VaultCarry;
                _leapEl = HeroElement.Frost;
                v = new Vector2(_lungeLeapVX, -jumpV * Tune.ElementalSword.VaultJump);
                var decoy = new IceDecoy { Position = feet + new Vector2(0, 1), Caster = this };
                G.Spawn(decoy);
                LastDecoy = decoy;
                foreach (var e in G.Enemies.ToArray())
                {
                    if (e.Dead || !e.CanBeHit || e.GlobalPosition.DistanceTo(feet) > Tune.ElementalSword.VaultRadius + e.HitRadius) continue;
                    float d = e.Hurt(blow * Tune.ElementalSword.VaultDamage, new Vector2(0, -80f), e.GlobalPosition, DamageKind.Frost);
                    if (d > 0) OnDealtDamage(d);
                    if (!e.Dead) e.Build(HeroElement.Frost, Tune.ElementalSword.VaultBuild, power, this);
                }
                G.Fx.Directional(feet, new Vector2(back, -0.6f).Normalized(), 0.35f, new Color(0.92f, 0.98f, 1f), 18, 320, 3f, 0.4f, 300, 0);
                G.Fx.Ring(feet, Tune.ElementalSword.VaultRadius, new Color(col, 0.9f), 0.3f, FxLayer.RingStyle.Burst);
                G.Sfx.Play("clink", feet, -2, 0.05f, 0.7f);
                Afterimage.Spawn(Anim, col, 0.25f);
                break;
            }
            case HeroElement.Earth:
                // Earthfall: a leap, and you come down like a rock (see ElementLanded)
                _jumpElemRest = _jumpRestTotal = ElemRest(Tune.ElementalSword.FallRest);
                _earthfall = true;
                _fallMult = 1f;
                _fallLeaves = HeroElement.None;
                G.Fx.Debris(feet, col, 6, 140);
                break;
        }
        return v;
    }

    /// <summary>The leap's carry: for a moment after an Explosive Lunge (or a Glacial Vault's spring back), it keeps its speed through the air.</summary>
    private Vector2 LeapCarry(Vector2 v, bool onFloor)
    {
        if (_lungeLeapT <= 0) return v;
        float carry = _leapEl == HeroElement.Frost ? Tune.ElementalSword.VaultCarry : Tune.ElementalSword.LungeCarry;
        if (onFloor && _lungeLeapT < carry - 0.1f) { _lungeLeapT = 0; return v; }
        // (embers streaming off the leap; frost, a glitter of ice)
        if (Engine.GetPhysicsFrames() % 2 == 0)
        {
            if (_leapEl == HeroElement.Frost) G.Fx.Glint(GlobalPosition + G.RandDir() * 7, Elements.ColorOf(HeroElement.Frost), 3f);
            else G.Fx.Ember(GlobalPosition + G.RandDir() * 6, Elements.ColorOf(HeroElement.Fire));
        }
        return new Vector2(_lungeLeapVX, v.Y);
    }

    /// <summary>Back on the ground: an Earthfall lands, in a shockwave the harder the fall.</summary>
    private void ElementLanded(float fallSpeed)
    {
        if (!_earthfall) return;
        _earthfall = false;
        var feet = GlobalPosition + new Vector2(0, 12);
        float power = Stats.DamageMult;
        float hard = (1f + Math.Clamp((fallSpeed - 300f) / 600f, 0f, 1.5f)) * _fallMult;
        float r = Tune.ElementalSword.FallRadius * (0.85f + 0.15f * hard);
        // (a pair's leap leaves magma or frozen ground where it lands)
        if (_fallLeaves == HeroElement.Fire) Launch(new MagmaPool { Position = FloorAt(GlobalPosition), Half = 50f, Seconds = 4f, TickDamage = BaseDamage * power * 0.12f });
        else if (_fallLeaves == HeroElement.Frost) Launch(new TundraField { Position = FloorAt(GlobalPosition), Half = 80f, Seconds = 5f });
        bool frostfall = _fallLeaves == HeroElement.Frost;
        _fallMult = 1f; _fallLeaves = HeroElement.None;
        var col = Elements.ColorOf(HeroElement.Earth);
        foreach (var e in G.Enemies.ToArray())
        {
            if (e.Dead || !e.CanBeHit || e.GlobalPosition.DistanceTo(feet) > r + e.HitRadius) continue;
            var away = new Vector2(Math.Sign(e.GlobalPosition.X - feet.X), -0.8f).Normalized();
            float d = e.Hurt(BaseDamage * power * Tune.ElementalSword.FallDamage * hard, away * 120f, e.GlobalPosition, DamageKind.Physical);
            if (d > 0) OnDealtDamage(d);
            if (e.Dead) continue;
            e.Build(HeroElement.Earth, Tune.ElementalSword.FallBuild * hard, power, this);
            if (frostfall) e.Build(HeroElement.Frost, Tune.ElementalSword.FallBuild, power, this);
            e.Shove(away * Tune.ElementalSword.FallToss);
        }
        Breakables.SpellBurst(feet, r * 0.5f);
        foreach (var rb in Rubble.All.ToArray()) if (IsInstanceValid(rb) && !rb.Cleared && rb.GlobalPosition.DistanceTo(feet) < r) rb.Strike(feet);
        G.Fx.Shockwave(feet, r, new Color(col, 0.75f), 0.35f);
        G.Fx.Debris(feet, col.Darkened(0.2f), 16, 260);
        G.Fx.Dust(feet, 12, 2.4f);
        G.Fx.AddShake(3f + 2f * hard);
        G.Main.Rumble(0.5f, 0.7f, 0.18f);
        G.Sfx.Play("slam", feet, -2, 0.05f, 0.85f);
        ElementalUses.Add("Earthfall!");
    }

    // ================================================================== the Elementalist

    private HeroElement _missileEl;
    private int _missileShots;
    private float _snapCdTotal = 1f, _updraftCdTotal = 1f, _dualBoltCd, _dualBoltTotal = 1f;
    /// <summary>For the HUD: the element on the staff's bolts, and how many are left of it.</summary>
    public HeroElement MissileElement => _missileShots > 0 ? _missileEl : HeroElement.None;
    public int MissileShots => _missileShots;
    /// <summary>For the HUD: the rests of the fixed-range spell, movement, a dual bolt, and Aid (0 = ready).</summary>
    public float FixedCooldownFrac => Math.Clamp(_snapCd / Math.Max(0.01f, _snapCdTotal), 0, 1);
    public float MoveCooldownFrac => Math.Clamp(_updraftCd / Math.Max(0.01f, _updraftCdTotal), 0, 1);
    public void TestResetDualBolt() => _dualBoltCd = 0;
    /// <summary>For the HUD: seconds left on Lift, the fixed-range spell and the pair's bolt.</summary>
    public float LiftSeconds => Math.Max(0f, _updraftCd);
    public float FixedSeconds => Math.Max(0f, _snapCd);
    public float DualBoltSeconds => Math.Max(0f, _dualBoltCd);

    public float DualBoltCooldownFrac => Math.Clamp(_dualBoltCd / Math.Max(0.01f, _dualBoltTotal), 0, 1);
    public float ElementAbilityFrac => AbilityChargeReady ? 0f : Math.Clamp(AbilityCooldowns.Min() / Math.Max(0.01f, _abilityRestTotal), 0, 1);

    /// <summary>The attack button: a pop with nothing readied, or a bolt of the element on the staff (a charge readied makes one bolt);
    /// with two charges that make a bolt of their own (and its rest over), that. Nothing is counted: he casts as often as the gap allows.</summary>
    private bool StaffBolt(Vector2 aim, bool held)
    {
        if (_boltCd > 0) return false;
        if (DualNameFor(ElemSlot.Attack) != "" && _dualBoltCd <= 0) return DualBolt(aim);
        // (no bolts to count: he casts as freely as the gap between casts allows; every bolt is the mode's)
        if (Infused != HeroElement.None) { _missileEl = SpendElement(); _missileShots = Tune.ElementalStaff.MissileShots; }
        var el = _missileShots > 0 ? _missileEl : HeroElement.None;
        if (_missileShots > 0) _missileShots--;
        aim = aim.LengthSquared() > 0.01f ? aim.Normalized() : new Vector2(Facing, 0);
        float range = Tune.Elementalist.BoltRange;
        var target0 = FindSpellTarget(aim, range, Tune.Elementalist.BoltConeDegrees);
        var dir0 = target0 != null ? (target0.GlobalPosition - CastPoint).Normalized() : aim;
        bool quick = el is HeroElement.Frost or HeroElement.Wind;
        _boltCd = (quick ? Tune.ElementalStaff.QuickGap : Tune.ElementalStaff.ShotGap) / Math.Max(0.2f, Stats.AttackSpeed);
        AttacksStarted++;
        BoltsCast++;
        if (Math.Abs(dir0.X) > 0.15f) Facing = Math.Sign(dir0.X);
        CastDir = dir0;
        Anim.Face((int)Facing, instant: true);
        LastCast = el == HeroElement.Frost ? "frost" : "fire";
        if (el == HeroElement.None) { Pop(aim); return true; }
        _lastEl = el;
        Used(Elements.SingleName(Stats.Hero, ElemSlot.Attack, el));
        Hold(quick ? Tune.Elementalist.FrostDelay : Tune.Elementalist.FireDelay, "cast", () =>
        {
            if (Dead) return;
            var target = FindSpellTarget(aim, range, Tune.Elementalist.BoltConeDegrees);
            var dir = target != null ? (target.GlobalPosition - CastPoint).Normalized() : aim;
            if (Math.Abs(dir.X) > 0.15f) Facing = Math.Sign(dir.X);
            CastDir = dir;
            Anim.Face((int)Facing, instant: true);
            _castGlow = 0.7f;
            LooseBolt(dir, el, Tune.ElementalStaff.ShotDamage, range);
        });
        return true;
    }

    /// <summary>With nothing readied, the attack is a pop: no bolt, a small burst on the creature nearest the aim within a short reach.</summary>
    private void Pop(Vector2 aim)
    {
        float range = Tune.ElementalStaff.PopRange;
        Hold(Tune.Elementalist.FrostDelay, "cast", () =>
        {
            if (Dead) return;
            var target = FindSpellTarget(aim, range, Tune.ElementalStaff.PopCone);
            var at = target != null ? target.GlobalPosition : CastPoint + aim * range * 0.6f;
            if (target == null) G.Cave.Raycast(CastPoint, aim, range * 0.6f, out at, 4f);
            var dir = (at - CastPoint).Normalized();
            if (Math.Abs(dir.X) > 0.15f) Facing = Math.Sign(dir.X);
            CastDir = dir;
            Anim.Face((int)Facing, instant: true);
            _castGlow = 0.6f;
            var col = ElementBolt.PlainColor;
            G.Fx.Pop(at, col, Tune.ElementalStaff.PopRadius + 6f);
            G.Fx.Burst(at, new Color(col, 0.9f), 6, 70, 1.8f, 0.25f, 0);
            G.Fx.Flash(CastPoint, 5, col, 0.06f);
            G.Sfx.Play("hit", at, -8, 0.1f, 1.6f);
            _lastEl = HeroElement.None;
            Used("Pop");
            if (target == null || target.Dead) return;
            float d = target.Hurt(Tune.ElementalStaff.PopDamage * Tune.ElementalStaff.ShotDamage * Stats.DamageMult * Stats.PrimaryDamageMult, dir * 30f, at, DamageKind.Physical);
            if (d > 0) { OnDealtDamage(d); target.HitStop(Tune.Feel.HitStopBolt * 0.6f); }
        });
    }

    /// <summary>One elemental bolt from the staff.</summary>
    private void LooseBolt(Vector2 dir, HeroElement el, float damageMult, float range)
    {
        CastSlow();
        float baseDmg = Tune.Elementalist.FireDamage * el switch
        {
            HeroElement.Fire => 1f, HeroElement.Frost => Tune.Elementalist.FrostDamage / Tune.Elementalist.FireDamage,
            HeroElement.Wind => Tune.ElementalStaff.GustDamage, _ => Tune.ElementalStaff.PlainBolt,
        };
        var bolt = new ElementBolt
        {
            Position = CastPoint + dir * 6f, Dir = dir, Range = range, Caster = this,
            Frost = el == HeroElement.Frost, Wind = el == HeroElement.Wind, Plain = el == HeroElement.None,
            Damage = baseDmg * damageMult * Stats.DamageMult * Stats.PrimaryDamageMult,
        };
        G.Spawn(bolt);
        NetSync.HeroVisual(bolt);
        G.Fx.Flash(CastPoint, 7, bolt.Tint, 0.08f);
        G.Sfx.Play(el == HeroElement.Frost ? "clink" : el == HeroElement.Wind ? "dodge" : "throw", CastPoint, -10, 0.1f, el == HeroElement.Frost ? 1.9f : 1.4f);
    }

    /// <summary>An elemental bolt met a creature: the blow, then its element's build-up (or, a gust, a shove).</summary>
    private void ElementBoltStruck(ElementBolt bolt, Enemy e, Vector2 at)
    {
        var el = bolt.Wind ? HeroElement.Wind : bolt.Plain ? HeroElement.None : bolt.Frost ? HeroElement.Frost : HeroElement.Fire;
        if (el == HeroElement.Fire) FireBurst(bolt, e, at);
        float dealt = e.Hurt(bolt.Damage, bolt.Dir * (el == HeroElement.Wind ? 10f : 40f), at, Elements.KindOf(el));
        if (dealt > 0) { OnDealtDamage(dealt); e.HitStop(Tune.Feel.HitStopBolt); AttributeHit(e, at - bolt.Dir * 10f); }
        if (e.Dead) return;
        switch (el)
        {
            case HeroElement.Fire: e.Build(HeroElement.Fire, Tune.ElementalStaff.FireBuild * bolt.BuildMult, Stats.DamageMult, this); break;
            case HeroElement.Frost: e.Build(HeroElement.Frost, Tune.ElementalStaff.FrostBuild * bolt.BuildMult, Stats.DamageMult, this); break;
            case HeroElement.Wind: e.Shove(bolt.Dir * Tune.ElementalStaff.GustPush + new Vector2(0, -60f)); break;
        }
    }

    /// <summary>The creatures in view (or, given a radius, those within it).</summary>
    private IEnumerable<Enemy> InView(float radius = -1f)
    {
        var me = GlobalPosition;
        foreach (var e in G.Enemies.ToArray())
        {
            if (e.Dead || !e.CanBeHit) continue;
            var d = e.GlobalPosition - me;
            if (radius > 0 ? d.Length() > radius + e.HitRadius : Math.Abs(d.X) > Tune.Elementalist.SnapViewX || Math.Abs(d.Y) > Tune.Elementalist.SnapViewY) continue;
            yield return e;
        }
    }

    /// <summary>The dodge button: with a charge readied (its rest over), the element's movement; otherwise Lift, a push of air straight up
    /// (on the ground or in the air).</summary>
    private bool ElementMove(in PlayerInput inp)
    {
        if (ElementMoveReady) return ElementDodge(inp);
        if (!UpdraftReady) return false;
        _liftTo = -BaseJumpV * Tune.ElementalStaff.LiftJump;
        _terminalT = 0f;
        G.Fx.Directional(GlobalPosition + new Vector2(0, 12), Vector2.Down, 0.6f, new Color(0.85f, 0.9f, 1f, 0.8f), 10, 160, 2f, 0.35f, 0, 0);
        G.Fx.Ring(GlobalPosition + new Vector2(0, 10), 14, new Color(0.85f, 0.9f, 1f, 0.8f), 0.25f);
        _updraftCd = _updraftCdTotal = Tune.ElementalStaff.LiftRest;
        _updraftOwes = true;
        Anim.Once("hex", 3, 1.5f);
        LastCast = "updraft";
        _castGlow = 1f;
        G.Sfx.Play("dodge", GlobalPosition, -8, 0.05f, 1.2f);
        _lastEl = HeroElement.None;
        Used("Lift");
        return true;
    }

    /// <summary>The dodge with an element readied (its rest over): the element's movement, Fire Walk, Tundra or the Updraft; two charges,
    /// the pair's. Returns the hero's motion from here.</summary>
    private Vector2 StaffJump(Vector2 v, float jumpV, in PlayerInput inp)
    {
        if (Infused == HeroElement.None || _jumpElemRest > 0 || InWater) return v;
        var aim = inp.AimGiven && inp.Aim.LengthSquared() > 0.01f ? inp.Aim.Normalized() : new Vector2(0, -1f);
        if (DualNameFor(ElemSlot.Move) != "") return DualMove(v, jumpV, aim);
        var el = SpendElement();
        _lastEl = el;
        float rest;
        switch (el)
        {
            case HeroElement.Fire:
            {
                // Fire Walk: a quicker step, and fire left burning where you walk
                rest = Tune.ElementalStaff.WalkRest;
                GiveBuff(BuffFireWalk, Tune.ElementalStaff.WalkSeconds);
                var trail = new FireTrail { Position = GlobalPosition, Caster = this, Power = Stats.DamageMult, Seconds = Tune.ElementalStaff.WalkSeconds };
                G.Spawn(trail);
                LastElementSpell = trail;
                break;
            }
            case HeroElement.Frost:
            {
                // Tundra: the ground about you freezes; heroes on it go quicker, creatures on it are chilled and filled with frost
                rest = Tune.ElementalStaff.TundraRest;
                if (!Ground.Below(GlobalPosition, 80, out var floor)) floor = GlobalPosition + new Vector2(0, 12);
                var field = new TundraField { Position = floor, Caster = this, Power = Stats.DamageMult };
                G.Spawn(field);
                LastElementSpell = field;
                break;
            }
            default:
                // the Updraft: a column of air raised along the aim (straight up, aimed nowhere), to ride the jump up
                rest = Tune.ElementalStaff.UpdraftRest;
                CastUpdraft(aim, spend: false);
                break;
        }
        _jumpElemRest = _jumpRestTotal = ElemRest(rest);
        _castGlow = 1f;
        G.Sfx.Play(el == HeroElement.Fire ? "lava" : el == HeroElement.Frost ? "clink" : "dodge", GlobalPosition, -8, 0.05f, 1.2f);
        Used(Elements.SingleName(Stats.Hero, ElemSlot.Move, el));
        return v;
    }

    /// <summary>The spot a fixed-range spell lands on: <see cref="Tune.ElementalStaff.FixedRange"/> along the aim, short of rock.</summary>
    private Vector2 FixedSpot(Vector2 aim)
    {
        aim = aim.LengthSquared() > 0.01f ? aim.Normalized() : new Vector2(Facing, 0);
        G.Cave.Raycast(CastPoint, aim, Tune.ElementalStaff.FixedRange, out var hit, 4f);
        return hit - aim * (hit.DistanceTo(CastPoint) < Tune.ElementalStaff.FixedRange - 1f ? 8f : 0f);
    }

    /// <summary>The Burst button: the fixed-range spell. Force Blast (plain), Burst, Snap or Cyclone; two charges, the pair's.</summary>
    private bool TryFixed(Vector2 aim)
    {
        bool free = FreeVersion;
        if (!SnapReady && !free) return false;
        aim = aim.LengthSquared() > 0.01f ? aim.Normalized() : new Vector2(Facing, 0);
        if (Math.Abs(aim.X) > 0.15f) Facing = Math.Sign(aim.X);
        CastDir = aim;
        Anim.Face((int)Facing, instant: true);
        if (DualNameFor(ElemSlot.Heavy) != "") return DualFixed(aim);
        var el = SpendElement();
        _lastEl = el;
        var spot = FixedSpot(aim);
        float r = Tune.ElementalStaff.FixedRadius, power = Stats.DamageMult;
        var col = Elements.ColorOf(el);
        float rest;
        switch (el)
        {
            case HeroElement.Fire:
                // Burst: an explosion at the spot; and every creature in view already burning, wherever it is, takes a full build-up (and combusts)
                rest = Tune.ElementalStaff.BurstRest;
                var burst = new HashSet<Enemy>();
                foreach (var e in FoesAt(spot, r))
                {
                    burst.Add(e);
                    bool lit = e.Ignited;
                    Strike(e, Tune.ElementalStaff.BurstDamage * power, (e.GlobalPosition - spot).Normalized() * 140f, DamageKind.Fire);
                    if (!e.Dead) e.Build(HeroElement.Fire, lit ? Tune.Elements.Full : Tune.ElementalStaff.BurstBuild, power, this);
                }
                foreach (var e in InView())
                {
                    if (burst.Contains(e) || !e.Ignited) continue;
                    G.Fx.Converge(e.GlobalPosition, e.HitRadius + 12, col, 5, 0.12f);
                    e.Build(HeroElement.Fire, Tune.Elements.Full, power, this);
                }
                GasCloud.IgniteAt(spot, r);
                G.Fx.Explosion(spot, col, 1.1f);
                break;
            case HeroElement.Frost:
                // Snap: the cold closes like a fist on the spot, filling what's there with frost; and every creature in view frozen solid,
                // wherever it is, shatters
                rest = Tune.ElementalStaff.SnapRest;
                foreach (var e in InView()) if (e.FrozenSolid) e.Shatter(power, this);
                foreach (var e in FoesAt(spot, r))
                {
                    if (e.Dead) continue;
                    Strike(e, Tune.ElementalStaff.SnapDamage * power, Vector2.Zero, DamageKind.Frost);
                    if (!e.Dead) e.Build(HeroElement.Frost, Tune.ElementalStaff.SnapBuild, power, this);
                }
                G.Fx.Converge(spot, r, new Color(col, 0.9f), 10, 0.18f);
                G.Fx.Ring(spot, r, new Color(col, 0.9f), 0.3f, FxLayer.RingStyle.Burst);
                G.Fx.Burst(spot, new Color(0.9f, 0.97f, 1f), 16, 140, 2.2f, 0.4f, 120);
                break;
            case HeroElement.Wind:
                // Cyclone: the storm at the spot, dragging light creatures in and swallowing shots
                rest = Tune.ElementalStaff.CycloneRest;
                SpawnStorm(spot, HeroElement.Wind, Tune.Elementalist.BlizzardDamage * 0.5f, 0f, Tune.ElementalStaff.CyclonePull);
                break;
            default:
                // Force Blast: a wide, weak burst of force at the spot, throwing creatures out of it
                rest = Tune.ElementalStaff.BlastRest;
                r = Tune.ElementalStaff.BlastRadius;
                foreach (var e in FoesAt(spot, r))
                {
                    var away = (e.GlobalPosition - spot).Normalized();
                    Strike(e, Tune.ElementalStaff.BlastDamage * power, away * 160f, DamageKind.Physical);
                    e.Shove(away * 240f + new Vector2(0, -60f));
                }
                G.Fx.Shockwave(spot, r, new Color(ElementBolt.PlainColor, 0.6f), 0.3f);
                break;
        }
        Breakables.SpellBurst(spot, r * 0.5f);
        if (!free) { _snapCd = _snapCdTotal = rest; _snapOwes = true; }
        LastCast = el == HeroElement.Fire ? "cinder" : "snap";
        _castGlow = 1f;
        G.Fx.Beam(CastPoint, spot, new Color(col, 0.45f));
        Anim.Once("rupture", 3, 1.6f);
        G.Sfx.Play(el == HeroElement.Fire ? "lava" : el == HeroElement.Frost ? "clink" : "dodge", spot, -4, 0.05f, 0.8f);
        G.Main.Rumble(0.35f, 0.3f, 0.12f);
        Used(Elements.SingleName(Stats.Hero, ElemSlot.Heavy, el));
        return true;
    }

    /// <summary>The creatures within <paramref name="r"/> of a spot, that a spell there can reach (not through rock).</summary>
    private static IEnumerable<Enemy> FoesAt(Vector2 at, float r)
    {
        foreach (var e in G.Enemies.ToArray())
            if (!e.Dead && e.CanBeHit && e.GlobalPosition.DistanceTo(at) <= r + e.HitRadius && G.Cave.LineClear(at, e.GlobalPosition)) yield return e;
    }

    /// <summary>A spell's blow, credited to the hero.</summary>
    private void Strike(Enemy e, float dmg, Vector2 knock, DamageKind kind)
    {
        float d = e.Hurt(dmg, knock, e.GlobalPosition, kind);
        if (d > 0) OnDealtDamage(d);
    }

    /// <summary>An elemental storm at a spot (the Cyclone, the Fire Storm, the Blizzard).</summary>
    private Blizzard SpawnStorm(Vector2 at, HeroElement el, float damage, float build, float pull)
    {
        float secondsMult = Stats.BlizzardSecondsMult;
        var storm = new Blizzard
        {
            Position = at,
            Radius = Tune.Elementalist.BlizzardRadius * Stats.BlizzardWideMult,
            Seconds = Tune.Elementalist.BlizzardSeconds * secondsMult,
            Ticks = Mathf.RoundToInt(Tune.Elementalist.BlizzardTicks * secondsMult),
            Fire = el == HeroElement.Fire, El = el, Elemental = true,
            Damage = damage * Stats.DamageMult, FreezeChance = 0f, IgniteChance = 0f,
            Build = build, Pull = pull, Caster = this,
        };
        G.Spawn(storm);
        NetSync.HeroVisual(storm);
        LastBlizzard = storm;
        return storm;
    }

    /// <summary>One strike of an elemental storm: the blow, its build-up, and the wind's pull toward its middle.</summary>
    private void ElementStormStruck(Blizzard storm, Enemy e)
    {
        var from = (e.GlobalPosition - storm.GlobalPosition).Normalized();
        float dealt = e.Hurt(storm.Damage, from * 10f, e.GlobalPosition - from * e.HitRadius * 0.5f, Elements.KindOf(storm.El));
        if (dealt > 0) OnDealtDamage(dealt);
        if (e.Dead) return;
        if (storm.Build > 0) e.Build(storm.El, storm.Build, Stats.DamageMult, this);
        if (storm.Pull > 0 && e.GlobalPosition.DistanceTo(storm.GlobalPosition) > 10f) e.Shove(-from * storm.Pull * 2.2f);
    }

    /// <summary>The Extended button: Aid for the party. Ward (plain: a barrier), Kindling, Frost Armor or Narrow Draft (it spends one charge).</summary>
    private bool TryAid()
    {
        bool free = FreeVersion;
        if (!AbilityChargeReady && !free) return false;
        if (DualNameFor(ElemSlot.Aid) != "") return DualAid(free);
        var el = SpendElement();
        _lastEl = el;
        if (!free)
        {
            SpendAbilityCharge();
            if (_stormOwes.Length != _abilityCd.Length) Array.Resize(ref _stormOwes, _abilityCd.Length);
            float rest = el switch
            {
                HeroElement.Wind => Tune.ElementalStaff.AidRestWind, HeroElement.Frost => Tune.ElementalStaff.AidRestFrost,
                HeroElement.Fire => Tune.ElementalStaff.AidRestFire, _ => Tune.ElementalStaff.AidRestWard,
            } * Stats.AbilityCdMult;
            SetSpentAbilityRest(rest);
            for (int k = 0; k < _abilityCd.Length; k++) if (_abilityCd[k] > 0 && !_stormOwes[k] && Math.Abs(_abilityCd[k] - _abilityRestTotal) < 0.001f) { _stormOwes[k] = true; break; }
        }
        float secs = Tune.ElementalStaff.AidSeconds;
        var col = el == HeroElement.None ? new Color(0.75f, 0.9f, 1f) : Elements.ColorOf(el);
        switch (el)
        {
            case HeroElement.Wind: foreach (var h in HeroesNear(Tune.ElementalStaff.AidRange)) h.GiveBuff(BuffTailwind, Tune.ElementalStaff.TailwindSeconds); break;
            case HeroElement.Frost: GiveBuff(BuffFrostArmor, secs); break;
            case HeroElement.Fire: foreach (var h in HeroesNear(Tune.ElementalStaff.AidRange)) h.GiveBuff(BuffKindling, secs); break;
            default: foreach (var h in HeroesNear(Tune.ElementalStaff.AidRange)) h.GiveBarrier(Tune.ElementalStaff.WardBarrier, secs); break;
        }
        G.Fx.Ring(GlobalPosition, Tune.ElementalStaff.AidRange * 0.6f, new Color(col, 0.7f), 0.45f, FxLayer.RingStyle.Dashed);
        Anim.Once("heal", 3, 1.5f);
        _castGlow = 1f;
        G.Sfx.Play("levelup", GlobalPosition, -8, 0.05f, el == HeroElement.Wind ? 1.3f : 0.9f);
        Used(Elements.SingleName(Stats.Hero, ElemSlot.Aid, el));
        return true;
    }

    /// <summary>Aid with something on top: the mode's greater Aid (Bonfire, Ice Armor, Gale Draft) or its mix with the attribute (Thermal
    /// Armor, Wildfire, Frostwind: both Aids at once).</summary>
    private bool DualAid(bool free)
    {
        var (a, b) = SpendPair();
        string name = Elements.DualName(Stats.Hero, ElemSlot.Aid, a, b);
        if (!free) { SpendAbilityCharge(); SetSpentAbilityRest(Tune.ElementalStaff.AidRestWard * Stats.AbilityCdMult); }
        float secs = Tune.ElementalStaff.AidSeconds, range = Tune.ElementalStaff.AidRange;
        var near = HeroesNear(range);
        switch (name)
        {
            case "Bonfire":
                // Kindling for half again as long, and a ring of fire bursting out about you
                foreach (var h in near) h.GiveBuff(BuffKindling, secs * 1.5f);
                foreach (var e in InView(range * 0.6f)) e.Build(HeroElement.Fire, 50f, Stats.DamageMult, this);
                G.Fx.Ring(GlobalPosition, range * 0.6f, new Color(Elements.ColorOf(HeroElement.Fire), 0.9f), 0.4f, FxLayer.RingStyle.Burst);
                break;
            case "Ice Armor":
                // Frost Armor, and a shell of ice that soaks blows besides
                GiveBuff(BuffFrostArmor, secs * 1.5f);
                GiveBarrier(Tune.ElementalStaff.WardBarrier * 1.5f, secs);
                break;
            case "Gale Draft":
                // Narrow Draft for twice as long, and a gust that throws the creatures about you back
                foreach (var h in near) h.GiveBuff(BuffTailwind, Tune.ElementalStaff.TailwindSeconds * 2f);
                foreach (var e in InView(range * 0.5f)) e.Shove((e.GlobalPosition - GlobalPosition).Normalized() * 380f + new Vector2(0, -60f));
                break;
            case "Thermal Armor":
                GiveBuff(BuffFrostArmor, secs);
                foreach (var h in near) h.GiveBuff(BuffKindling, secs);
                break;
            case "Wildfire":
                foreach (var h in near) { h.GiveBuff(BuffKindling, secs); h.GiveBuff(BuffTailwind, Tune.ElementalStaff.TailwindSeconds); }
                break;
            default:
                // Frostwind
                GiveBuff(BuffFrostArmor, secs);
                foreach (var h in near) h.GiveBuff(BuffTailwind, Tune.ElementalStaff.TailwindSeconds);
                break;
        }
        var col = Elements.DualColor(a, b);
        G.Fx.Ring(GlobalPosition, range * 0.6f, new Color(col, 0.7f), 0.45f, FxLayer.RingStyle.Dashed);
        Anim.Once("heal", 3, 1.5f);
        _castGlow = 1f;
        G.Sfx.Play("levelup", GlobalPosition, -6, 0.05f, 0.8f);
        _lastEl = a;
        Used(name, col);
        return true;
    }

    /// <summary>Frost Armor: a creature that strikes this hero in melee is filled with frost. Frost Barrier: the next one, once.</summary>
    private void FrostArmorStruck(Enemy source, bool melee)
    {
        if (!melee || source == null || !IsInstanceValid(source) || source.Dead) return;
        var col = Elements.ColorOf(HeroElement.Frost);
        if (HasBuff(BuffFrostWard))
        {
            EndBuff(BuffFrostWard);
            source.Build(HeroElement.Frost, Tune.Dual.WardBuild, Stats.DamageMult, this);
            G.Fx.Burst(GlobalPosition + new Vector2(0, -4), new Color(0.9f, 0.97f, 1f), 18, 160, 2.4f, 0.4f, 120);
            G.Fx.Ring(GlobalPosition + new Vector2(0, -4), 22, new Color(col, 0.95f), 0.3f, FxLayer.RingStyle.Burst);
            G.Fx.Text(source.GlobalPosition + new Vector2(0, -source.HitRadius - 16), "FROST BARRIER", col, 10, 0.8f);
            G.Sfx.Play("clink", GlobalPosition, -2, 0.05f, 0.6f);
            ElementalUses.Add("Frost Barrier!");
        }
        if (source.Dead || !HasBuff(BuffFrostArmor)) return;
        source.Build(HeroElement.Frost, Tune.Elements.Full, Stats.DamageMult, this);
        G.Fx.Burst(source.GlobalPosition, col, 8, 90, 1.6f, 0.3f, 60);
    }
}
