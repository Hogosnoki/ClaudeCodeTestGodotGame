using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

/// <summary>
/// The elemental Rogue (dark, wind, nature; <see cref="Tune.ElementalRogue"/>). Her strike (the jab), her evade (the dodge button: the vanish)
/// and her throw (the ability) each take the mode's version, or a pair's (<see cref="Elements.DualName"/>):
///  * the strike: Shadow Strike, Gale Jab, Venom Jab; Nightfall, Flurry, Deathcap, Thornwind, Shade Step, Nightshade;
///  * the evade: Shadowmeld, Gale Step, Spore Cloud; Umbral Veil, Wind Walk, Overgrowth Veil, Leaf Storm, Shadow Dash, Nightbloom;
///  * the throw: Shadow Knife, Gale Knife, Barbed Knife; Umbral Knife, Fan of Knives, Thorn Knife, Pollen Knife, Shadow Tether,
///    Nightshade Knife.
/// Dark blinds and lays creatures open, wind makes her quick and flings, nature poisons and roots. The recall is as it was.
/// </summary>
public partial class Player
{
    /// <summary>The strike version under way (for its blows), and whether its Shade Step has been taken.</summary>
    private string _rSwing = "";
    private bool _shadeStepped;
    /// <summary>The throw version being thrown (read as the daggers leave the hand) and its element; whether the evade is free (testing).</summary>
    private string _rThrowNext = "";
    private HeroElement _rThrowEl;
    private bool _rEvadeFree;

    /// <summary>For the tests: the daggers home in the hand at once.</summary>
    public void TestCatchDaggers()
    {
        for (int k = 0; k < 2; k++)
        {
            if (_thrown[k] != null && IsInstanceValid(_thrown[k])) _thrown[k].QueueFree();
            _thrown[k] = null;
        }
        _tetherTo = null;
        _throwCd = 0;
    }

    private void RogueUsed(string name, HeroElement a, HeroElement b)
    {
        _lastEl = a;
        Used(name, b == HeroElement.None ? Elements.ColorOf(a) : Elements.DualColor(a, b));
    }

    // ================================================================== the strike

    /// <summary>The attack (daggers in hand): a jab in the mode's version (its pace, weight, reach and what its blows add).</summary>
    private bool RogueStrike(Vector2 aim, bool held)
    {
        if (_swingCd > 0 || (held && _dodgeT > 0)) return false;
        var (name, a, b) = SpendFor(ElemSlot.Attack);
        float pace = name is "Gale Jab" or "Thornwind" ? Tune.ElementalRogue.GalePace : name == "Flurry" ? Tune.ElementalRogue.FlurryPace : 1f;
        if (_dodgeT > 0) _dodgeT = 0;
        StartSwing(aim, pace: pace);
        _rSwing = name;
        _shadeStepped = false;
        float build = Tune.ElementalRogue.JabBuild, full = Tune.Elements.Full;
        switch (name)
        {
            case "Shadow Strike": _swingDmg *= Tune.ElementalRogue.ShadowDamage; _swingEl = HeroElement.Dark; _swingBuild = build; break;
            case "Gale Jab": _swingDmg *= Tune.ElementalRogue.GaleDamage; _swingReach *= Tune.ElementalRogue.GaleReach; break;
            case "Venom Jab": _swingEl = HeroElement.Nature; _swingBuild = build; break;
            case "Nightfall": _swingDmg *= Tune.ElementalRogue.NightfallDamage; _swingEl = HeroElement.Dark; _swingBuild = full; break;
            case "Flurry": _swingArc = Mathf.Pi; _swingReach *= Tune.ElementalRogue.GaleReach; _swingDmg *= Tune.ElementalRogue.GaleDamage; break;
            case "Deathcap": _swingEl = HeroElement.Nature; _swingBuild = full; break;
            case "Thornwind": _swingReach *= Tune.ElementalRogue.ThornReach; _swingEl = HeroElement.Nature; _swingBuild = build; break;
            case "Shade Step": _swingEl = HeroElement.Dark; _swingBuild = build; break;
            case "Nightshade": _swingEl = HeroElement.Dark; _swingBuild = build; _swingEl2 = HeroElement.Nature; _swingBuild2 = build; break;
        }
        // (how it looks as it begins: a wisp of shadow, a flick of wind, a drip of venom)
        var origin = GlobalPosition + new Vector2(0, -3);
        var col = b == HeroElement.None ? Elements.ColorOf(a) : Elements.DualColor(a, b);
        if (PairOf(a, b, HeroElement.Wind)) G.Fx.Directional(origin + _swingDir * 6f, _swingDir, 0.3f, new Color(col, 0.8f), 6, 280, 1.4f, 0.2f, 0, 1);
        if (PairOf(a, b, HeroElement.Dark)) G.Fx.Smoke(origin + _swingDir * 10f, 2, new Color(0.18f, 0.1f, 0.26f, 0.7f), 20f);
        if (PairOf(a, b, HeroElement.Nature)) G.Fx.Burst(origin + _swingDir * 12f, new Color(Elements.ColorOf(HeroElement.Nature), 0.9f), 3, 50, 1.4f, 0.3f, 200);
        RogueUsed(name, a, b);
        return true;
    }

    private static bool PairOf(HeroElement a, HeroElement b, HeroElement x) => a == x || b == x;

    /// <summary>A blow of the elemental jab landed: what the version adds beyond its build-up.</summary>
    private void RogueBladeHit(Enemy e)
    {
        if (_rSwing == "" || e.Dead) return;
        var away = (e.GlobalPosition - GlobalPosition).Normalized();
        float p = Stats.DamageMult;
        switch (_rSwing)
        {
            case "Gale Jab":
            case "Flurry":
            case "Thornwind":
                e.Shove(away * Tune.ElementalRogue.JabShove + new Vector2(0, -50f));
                break;
            case "Venom Jab":
            case "Nightshade":
                e.Envenom(Tune.ElementalRogue.Venom * p, Tune.ElementalRogue.VenomSeconds);
                break;
            case "Deathcap":
                e.Envenom(Tune.ElementalRogue.Venom * 2f * p, Tune.ElementalRogue.VenomSeconds);
                break;
            case "Shade Step":
                if (!_shadeStepped) ShadeStep(e);
                break;
        }
    }

    /// <summary>Shade Step: through the creature to its far side, in a breath of shadow (where there's room), turned to face its back.</summary>
    private void ShadeStep(Enemy e)
    {
        _shadeStepped = true;
        float side = Math.Sign(e.GlobalPosition.X - GlobalPosition.X);
        if (side == 0) side = Facing;
        var spot = new Vector2(e.GlobalPosition.X + side * (e.HitRadius + 14f), GlobalPosition.Y);
        if (G.Cave.IsSolid(spot) || G.Cave.IsSolid(spot + new Vector2(0, -12f))) return;
        G.Fx.Smoke(GlobalPosition, 4, new Color(0.18f, 0.1f, 0.26f, 0.75f), 30f);
        Afterimage.Spawn(Anim, new Color(Elements.ColorOf(HeroElement.Dark), 0.8f), 0.25f);
        GlobalPosition = spot;
        Velocity = Vector2.Zero;
        Facing = -side;
        Anim.Face((int)Facing, instant: true);
        G.Fx.Smoke(GlobalPosition, 4, new Color(0.18f, 0.1f, 0.26f, 0.75f), 30f);
        G.Sfx.Play("dodge", GlobalPosition, -6, 0.05f, 0.6f);
        ShadeSteps++;
    }
    /// <summary>For the tests: Shade Steps taken.</summary>
    public int ShadeSteps { get; private set; }

    // ================================================================== the throw

    /// <summary>The ability: a throw in the mode's version (its speed, how many, what its blow does).</summary>
    private bool RogueThrow(Vector2 aim)
    {
        if (!IsRogue || _throwCd > 0 || DaggersInHand == 0 || _tetherTo != null) return false;
        var (name, a, b) = SpendFor(ElemSlot.Aid);
        _rThrowNext = name; _rThrowEl = a;
        bool ok;
        try { ok = TryThrow(aim); }
        finally { _rThrowNext = ""; }
        if (!ok) return false;
        if (Tune.Testing.NoCooldowns) _throwCd = Math.Min(_throwCd, 0.25f);
        RogueUsed(name, a, b);
        return true;
    }

    /// <summary>An elemental dagger met a creature: what its version does besides the blow.</summary>
    private void RogueKnifeStruck(ThrownDagger d, Enemy e, Vector2 at)
    {
        if (e.Dead) return;
        if (d.Version == SimpleVer) { SimpleTouch(e, KitRole.Special, at - d.Dir * 10f); return; }
        float p = Stats.DamageMult, build = Tune.ElementalRogue.KnifeBuild, full = Tune.Elements.Full;
        var col = Elements.ColorOf(d.El);
        switch (d.Version)
        {
            case "Shadow Knife": e.Build(HeroElement.Dark, build, p, this); break;
            case "Gale Knife":
            case "Fan of Knives": e.Shove(d.Dir * Tune.ElementalRogue.KnifeShove + new Vector2(0, -50f)); break;
            case "Barbed Knife":
                e.Build(HeroElement.Nature, build, p, this);
                if (!e.Dead) e.Envenom(Tune.ElementalRogue.Venom * p, Tune.ElementalRogue.VenomSeconds);
                break;
            case "Umbral Knife": e.Build(HeroElement.Dark, full, p, this); break;
            case "Thorn Knife":
                e.Build(HeroElement.Nature, full, p, this);
                if (!e.Dead) e.Bleed(Tune.ElementalRogue.ThornBleed * p, Tune.ElementalRogue.VenomSeconds);
                break;
            case "Pollen Knife":
                e.Build(HeroElement.Nature, build, p, this);
                foreach (var o in G.Enemies.ToArray())
                    if (o != e && !o.Dead && o.CanBeHit && o.GlobalPosition.DistanceTo(at) < Tune.ElementalRogue.PollenRadius + o.HitRadius) o.Build(HeroElement.Nature, build, p, this);
                for (int k = 0; k < 12; k++) G.Fx.Ember(at + G.RandDir() * G.Range(0f, Tune.ElementalRogue.PollenRadius), new Color(1f, 0.95f, 0.5f));
                break;
            case "Shadow Tether":
                // she is drawn along the dark to her dagger, and strikes again as she arrives
                e.Build(HeroElement.Dark, build, p, this);
                if (_tetherTo == null) { _tetherTo = d; _tetherT = 0f; _tetherSurprise = false; }
                G.Fx.Beam(GlobalPosition + new Vector2(0, -6), at, new Color(Elements.ColorOf(HeroElement.Dark), 0.85f));
                break;
            case "Nightshade Knife":
                e.Build(HeroElement.Dark, build, p, this);
                if (!e.Dead) e.Build(HeroElement.Nature, build, p, this);
                if (!e.Dead) e.Envenom(Tune.ElementalRogue.Venom * p, Tune.ElementalRogue.VenomSeconds);
                break;
        }
        G.Fx.Burst(at, new Color(col, 0.9f), 6, 90, 1.6f, 0.3f, 0);
    }

    // ================================================================== the evade

    /// <summary>The dodge button: the vanish in the mode's version (longer, with a dash, or with what it leaves about her).</summary>
    private bool RogueEvade()
    {
        if (!IsRogue) return false;
        bool free = Tune.Testing.NoCooldowns;
        if (!AbilityChargeReady && !free) return false;
        var (name, a, b) = SpendFor(ElemSlot.Move);
        _rEvadeFree = free;
        if (!TryVanish()) return false;
        float p = Stats.DamageMult, full = Tune.Elements.Full;
        float r = Tune.ElementalRogue.EvadeRadius;
        var me = GlobalPosition + new Vector2(0, -6);
        var foes = G.Enemies.Where(e => !e.Dead && e.CanBeHit && e.GlobalPosition.DistanceTo(me) <= r + e.HitRadius).ToList();
        void Dash(float mult)
        {
            _airDashDir = new Vector2(Facing, 0);
            _airDashT = 0.18f * mult;
            Afterimage.Spawn(Anim, new Color(Elements.ColorOf(HeroElement.Wind), 0.7f), 0.2f);
        }
        void Smoke()
        {
            var cloud = new SmokeCloud { Position = me, Radius = Tune.ElementalRogue.SmokeRadius, Life = Tune.ElementalRogue.SmokeSeconds };
            G.Spawn(cloud);
            NetSync.HeroVisual(cloud);
        }
        switch (name)
        {
            case "Shadowmeld": if (_vanishT > 0) _vanishT *= Tune.ElementalRogue.MeldTime; break;
            case "Gale Step": Dash(1f); break;
            case "Spore Cloud":
                foreach (var e in foes) e.Build(HeroElement.Nature, Tune.ElementalRogue.EvadeBuild, p, this);
                for (int k = 0; k < 14; k++) G.Fx.Ember(me + G.RandDir() * G.Range(6, r), new Color(1f, 0.95f, 0.5f));
                break;
            case "Umbral Veil":
                if (_vanishT > 0) _vanishT *= Tune.ElementalRogue.VeilTime;
                foreach (var e in foes) e.Build(HeroElement.Dark, full, p, this);
                G.Fx.Smoke(me, 10, new Color(0.15f, 0.08f, 0.22f, 0.8f), 60f);
                break;
            case "Wind Walk":
                Dash(1f);
                GiveBuff(BuffTailwind, Tune.ElementalRogue.TailwindSeconds);
                break;
            case "Overgrowth Veil":
                foreach (var e in foes) e.Build(HeroElement.Nature, full, p, this);
                break;
            case "Leaf Storm":
                foreach (var e in foes)
                {
                    e.Build(HeroElement.Nature, Tune.ElementalRogue.EvadeBuild, p, this);
                    if (!e.Dead) e.Shove((e.GlobalPosition - me).Normalized() * Tune.ElementalRogue.EvadeShove + new Vector2(0, -60f));
                }
                for (int k = 0; k < 10; k++) { float ang = k * Mathf.Tau / 10f; G.Fx.Directional(me, Vector2.Right.Rotated(ang), 0.2f, new Color(Elements.ColorOf(HeroElement.Nature), 0.8f), 1, r * 2.5f, 1.6f, 0.35f, 0, 1); }
                break;
            case "Shadow Dash":
                Smoke();
                Dash(Tune.ElementalRogue.LongDash);
                break;
            case "Nightbloom":
                Smoke();
                foreach (var e in foes)
                {
                    e.Build(HeroElement.Dark, Tune.ElementalRogue.EvadeBuild, p, this);
                    if (!e.Dead) e.Build(HeroElement.Nature, Tune.ElementalRogue.EvadeBuild, p, this);
                }
                break;
        }
        EvadeFoesStruck += foes.Count;
        RogueUsed(name, a, b);
        return true;
    }
    /// <summary>For the tests: creatures the elemental evades have reached.</summary>
    public int EvadeFoesStruck { get; private set; }
}
