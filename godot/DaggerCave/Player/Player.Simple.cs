using System;
using System.Linq;
using Godot;

namespace DaggerCave;

/// <summary>
/// The simple kits (<see cref="Elements.Simple"/>): the elemental Swordsman, Warden, Elementalist, Vitalist, Aegis and Rogue each have a
/// primary attack (the attack button), a defensive (the dodge button) and a special (the ability button), and three elements to switch
/// between. An ability works the same in every element: the same damage, reach, speed and cooldown. What it strikes takes the element's
/// touch (<see cref="SimpleTouch"/>: its build-up, or wind's shove), and it looks and sounds of its element. Its blows are plain (no
/// creature resists or is weak to them by element), so the numbers never change with the element. Each hero also lays the element on
/// in their own way (<see cref="Tune.Elements"/>): the Swordsman's tipper (and the riposte after a roll), the Warden's counter (his
/// shield, not his cut), the Elementalist's zone (her Burst lingers), the Vitalist's seep (her hex carries it; her drain tears it out),
/// the Aegis's imbue (the heroes she wraps strike with it), and the Rogue's stored knife (released on the recall; a jab from hiding
/// doubles it).
/// <code>
///                 primary          defensive         special
///   Swordsman     Strike (swing)   Roll (dodge)      Cleave (Hard Swing)
///   Warden        Cut (swing)      Shield (block)    Charge (Guarded Charge)
///   Elementalist  Bolt             Ward (barrier)    Burst (at a spot)
///   Vitalist      Drain            Mending (heal)    Hex
///   Aegis         Bubble (shot)    Barrier           Dome (bubble field)
///   Rogue         Jab (strike)     Vanish            Knife (throw)
/// </code>
/// The versions these replaced (and the overcharges, attributes and mixes) stay in the code behind the switch: ABILITY_ARCHIVE.md.
/// </summary>
public partial class Player
{
    /// <summary>This hero has a simple kit.</summary>
    public bool SimpleKit => Elements.SimpleKit(Stats.Hero);

    /// <summary>The version name the shared casting code carries for a simple kit's ability (its hooks then give the element's touch).</summary>
    private const string SimpleVer = "simple";

    /// <summary>The role of the swing under way (its blows' touch).</summary>
    private KitRole _swingRole;

    /// <summary>For the tests: creatures a simple kit's ability has touched (built up or shoved); tippers struck; seeps torn out by the
    /// drain; knives released by the recall.</summary>
    public int SimpleTouches { get; private set; }
    public int Tippers { get; private set; }
    public int CashIns { get; private set; }
    public int Releases { get; private set; }

    /// <summary>The Swordsman's riposte (every blow a tipper) still open after a roll; the blow landing now is a tipper; the Rogue's swing
    /// began in hiding.</summary>
    private float _riposteT;
    private bool _blowTipper, _shadowSwing;
    public bool RiposteOpen => _riposteT > 0;

    /// <summary>The switch's reward: seconds the new element stays primed (its first ability lays it on <see cref="Tune.Elements.PrimeMult"/>
    /// x); the ability that spent it, and until when its touches count.</summary>
    private float _primedT;
    private KitRole _primedRole;
    private double _primedUntil;
    public bool Primed => _primedT > 0;
    public float PrimedLeft => Math.Max(0f, _primedT);
    /// <summary>For the tests: primed touches laid on.</summary>
    public int PrimedTouches { get; private set; }
    /// <summary>The hero's own clock (game time, so a slow frame doesn't shorten the window).</summary>
    private double _kitClock;
    private double Now => _kitClock;

    /// <summary>A simple kit's ability, as the HUD names it ("Searing Strike").</summary>
    public string SimpleName(KitRole r) => Elements.SimpleName(Stats.Hero, r, Mode);

    /// <summary>How much an ability of each role builds up (wind: how hard it shoves).</summary>
    private static float RoleBuild(KitRole r) => r switch
    {
        KitRole.Primary => Tune.Elements.PrimaryBuild, KitRole.Defensive => Tune.Elements.DefensiveBuild, _ => Tune.Elements.SpecialBuild,
    };

    /// <summary>The element's touch on a creature a simple ability reached: its build-up by the ability's role, or, wind, a shove away from
    /// <paramref name="from"/>.</summary>
    private void SimpleTouch(Enemy e, KitRole role, Vector2 from, float mult = 1f)
    {
        if (e == null || !IsInstanceValid(e) || e.Dead) return;
        var el = Mode;
        if (el == HeroElement.None) return;
        // (the first ability after a switch: its touches lay the element on twice over)
        bool primed = role == _primedRole && Now < _primedUntil;
        if (primed) { mult *= Tune.Elements.PrimeMult; PrimedTouches++; }
        float build = RoleBuild(role) * mult;
        if (el == HeroElement.Wind)
        {
            var away = e.GlobalPosition - from;
            away = away.LengthSquared() > 1f ? away.Normalized() : new Vector2(Facing, 0);
            e.Shove(away * build * Tune.Elements.ShovePerBuild + new Vector2(0, -60f));
        }
        else e.Build(el, build, Stats.DamageMult, this);
        SimpleTouches++;
        G.Fx.Burst(e.GlobalPosition, new Color(Elements.ColorOf(el), 0.9f), role == KitRole.Primary ? 4 : 7, 80, 1.6f, 0.3f, 0);
    }

    /// <summary>A simple ability went out: its name, in its element's colour and sound.</summary>
    private void SimpleUsed(KitRole r)
    {
        if (_primedT > 0)
        {
            _primedT = 0f;
            _primedRole = r;
            _primedUntil = Now + Tune.Elements.PrimeWindow;
            G.Fx.Text(GlobalPosition + new Vector2(0, -44), "PRIMED", Elements.ColorOf(Mode), 9, 0.6f);
        }
        _lastEl = Mode;
        Used(SimpleName(r));
    }

    // ================================================================== the buttons

    /// <summary>The attack button.</summary>
    private bool SimplePrimary(Vector2 aim, bool held, bool stick)
    {
        bool ok;
        switch (Stats.Hero)
        {
            case HeroKind.Elementalist: return SimpleBolt(aim);
            case HeroKind.Vitalist:
                _vDrain = SimpleVer;
                if (!CastDrain(aim, held)) { _vDrain = ""; return false; }
                SimpleUsed(KitRole.Primary);
                return true;
            case HeroKind.Aegis:
                if (!CastWardBolt(aim, held, SimpleVer, Mode)) return false;
                SimpleUsed(KitRole.Primary);
                return true;
            case HeroKind.Rogue:
                // (with no dagger in hand, the attack calls them home)
                if (DaggersInHand == 0) return !stick && TryRecall(fromAttack: true);
                ok = _tetherTo == null && TrySwing(aim, held);
                break;
            case HeroKind.Warden: ok = _dashT <= 0 && _bashT <= 0 && TrySwing(aim, held); break;
            default: ok = !Heaving && TrySwing(aim, held); break;
        }
        if (!ok) return false;
        SimpleSwing(KitRole.Primary);
        return true;
    }

    /// <summary>The swing just started carries the element (its blows touch by <paramref name="role"/>).</summary>
    private void SimpleSwing(KitRole role)
    {
        _swingEl = Mode;
        _swingBuild = 0f;
        _swingRole = role;
        SimpleUsed(role);
    }

    /// <summary>The dodge button (the Warden's shield reads the button itself).</summary>
    private bool SimpleDefensive(in PlayerInput inp)
    {
        var aim = inp.AimGiven && inp.Aim.LengthSquared() > 0.01f ? inp.Aim.Normalized() : new Vector2(Facing, 0);
        bool free = Tune.Testing.NoCooldowns;
        switch (Stats.Hero)
        {
            case HeroKind.Warden: return true;
            case HeroKind.Swordsman:
                if (!TryDodge(inp)) return false;
                // (the riposte: for a moment after the roll, every blow is a tipper)
                _riposteT = _dodgeT + Tune.Elements.RiposteWindow;
                break;
            case HeroKind.Elementalist:
                if (!SimpleWard()) return false;
                break;
            case HeroKind.Vitalist:
                if (_healCd > 0 || (!AbilityChargeReady && !free)) return false;
                if (VitalForce < HealCost - 0.001f) return Refuse("NOT ENOUGH VITAL FORCE");
                _vHealNext = SimpleVer; _vHealFree = free;
                if (!TryHeal()) { _vHealNext = ""; _vHealFree = false; return false; }
                break;
            case HeroKind.Aegis:
                if (!AbilityChargeReady && !free) return false;
                _aBarrierNext = SimpleVer; _aBarrierFree = free;
                if (!TryBarrier(aim)) { _aBarrierNext = ""; _aBarrierFree = false; return false; }
                break;
            case HeroKind.Rogue:
                if (!AbilityChargeReady && !free) return false;
                _rEvadeFree = free;
                if (!TryVanish()) { _rEvadeFree = false; return false; }
                break;
            default: return false;
        }
        SimpleUsed(KitRole.Defensive);
        return true;
    }

    /// <summary>The ability button.</summary>
    private bool SimpleSpecial(Vector2 aim)
    {
        // (the Gauntlet: the special button is the hero's great stroke, Player.Big.cs)
        if (Tune.Gauntlet.On) return BigSpecial(aim);
        switch (Stats.Hero)
        {
            case HeroKind.Swordsman: return SimpleCleave(aim);
            case HeroKind.Elementalist: return SimpleBurst(aim);
            case HeroKind.Warden:
                if (_bashT > 0 || !TryShieldDash(aim)) return false;
                break;
            case HeroKind.Vitalist:
            {
                if (!IsVitalist || _hexCd > 0 || !TryHex()) return false;
                if (Tune.Testing.NoCooldowns) _hexCd = Math.Min(_hexCd, 0.25f);
                var c = GlobalPosition + new Vector2(0, -6);
                float r = Tune.Vitalist.HexRadius * Stats.HexRadiusMult;
                // (the hex carries her element: it seeps in for as long as the hex lasts)
                foreach (var e in G.Enemies.ToArray())
                    if (!e.Dead && e.CanBeHit && e.GlobalPosition.DistanceTo(c) <= r + e.HitRadius) e.Seep(Mode, Tune.Elements.HexSeep, Stats.HexSeconds, Stats.DamageMult, this);
                G.Fx.Ring(c, r, new Color(Elements.ColorOf(Mode), 0.7f), 0.45f, FxLayer.RingStyle.Dashed);
                ElementWorld.Touch(Mode, c, r);
                break;
            }
            case HeroKind.Aegis:
                if (!IsAegis || _bubbleCd > 0) return false;
                _aDomeNext = SimpleVer;
                try { TryBubble(); }
                finally { _aDomeNext = ""; }
                if (Tune.Testing.NoCooldowns) _bubbleCd = Math.Min(_bubbleCd, 0.3f);
                break;
            case HeroKind.Rogue:
            {
                // (a knife already out: the button calls it home, releasing what it stored)
                if (DaggersInHand < 2 && _thrown.Any(d => d != null && IsInstanceValid(d) && d.State != ThrownDagger.Phase.Returning)) return TryRecall();
                if (!IsRogue || _throwCd > 0 || DaggersInHand == 0 || _tetherTo != null) return false;
                _rThrowNext = SimpleVer; _rThrowEl = Mode;
                bool ok;
                try { ok = TryThrow(aim); }
                finally { _rThrowNext = ""; }
                if (!ok) return false;
                if (Tune.Testing.NoCooldowns) _throwCd = Math.Min(_throwCd, 0.25f);
                break;
            }
            default: return false;
        }
        SimpleUsed(KitRole.Special);
        return true;
    }

    // ================================================================== the Swordsman's Cleave

    /// <summary>The Hard Swing, the same in every element: its blows touch as a special's.</summary>
    private bool SimpleCleave(Vector2 aim)
    {
        if ((_heaveCd > HeaveSpareTime && !Tune.Testing.NoCooldowns) || Heaving) return false;
        // (on the ground only: the press is held a moment, for landing)
        if (InWater || !IsOnFloor()) return false;
        float scale = Stats.HeaveCooldown / Math.Max(0.01f, Tune.Swordsman.HeaveCooldown);
        _heaveCdTotal = Tune.ElementalSword.HardRest * scale;
        _heaveCd = Math.Max(0f, _heaveCd) + _heaveCdTotal;
        _dodgeT = 0;
        Facing = Math.Abs(aim.X) > 0.2f ? Math.Sign(aim.X) : Facing;
        StartHeave();
        _swingDmg = BaseDamage * Stats.DamageMult * Tune.ElementalSword.HardDamage * Stats.HeaveDamageMult;
        SimpleSwing(KitRole.Special);
        // (where the blade comes down, once it does: the world answers its element)
        var el = Mode;
        float facing = Facing, reach = Tune.Swordsman.Reach * Tune.Swordsman.HeaveReach;
        GetTree().CreateTimer(_windup + _active * 0.6f).Timeout += () =>
        {
            if (!IsInstanceValid(this) || Dead) return;
            ElementWorld.Touch(el, GlobalPosition + new Vector2(facing * reach * 0.7f, 4f), 26f, new Vector2(facing, 0));
        };
        return true;
    }

    // ================================================================== the Elementalist

    /// <summary>The bolt: one gap, one blow, whatever the element.</summary>
    private bool SimpleBolt(Vector2 aim)
    {
        if (_boltCd > 0) return false;
        var el = Mode;
        aim = aim.LengthSquared() > 0.01f ? aim.Normalized() : new Vector2(Facing, 0);
        float range = Tune.Elementalist.BoltRange;
        var target0 = FindSpellTarget(aim, range, Tune.Elementalist.BoltConeDegrees);
        var dir0 = target0 != null ? (target0.GlobalPosition - CastPoint).Normalized() : aim;
        _boltCd = Tune.ElementalStaff.ShotGap / Math.Max(0.2f, Stats.AttackSpeed);
        AttacksStarted++;
        BoltsCast++;
        if (Math.Abs(dir0.X) > 0.15f) Facing = Math.Sign(dir0.X);
        CastDir = dir0;
        Anim.Face((int)Facing, instant: true);
        LastCast = el == HeroElement.Frost ? "frost" : "fire";
        SimpleUsed(KitRole.Primary);
        Hold(Tune.Elementalist.FireDelay, "cast", () =>
        {
            if (Dead) return;
            var target = FindSpellTarget(aim, range, Tune.Elementalist.BoltConeDegrees);
            var dir = target != null ? (target.GlobalPosition - CastPoint).Normalized() : aim;
            if (Math.Abs(dir.X) > 0.15f) Facing = Math.Sign(dir.X);
            CastDir = dir;
            Anim.Face((int)Facing, instant: true);
            _castGlow = 0.7f;
            CastSlow();
            var bolt = new ElementBolt
            {
                Position = CastPoint + dir * 6f, Dir = dir, Range = range, Caster = this,
                Frost = el == HeroElement.Frost, Wind = el == HeroElement.Wind, Plain = el == HeroElement.None,
                Version = SimpleVer, El = el,
                Damage = Tune.Elementalist.FireDamage * Tune.ElementalStaff.ShotDamage * Stats.DamageMult * Stats.PrimaryDamageMult,
            };
            G.Spawn(bolt);
            NetSync.HeroVisual(bolt);
            G.Fx.Flash(CastPoint, 7, bolt.Tint, 0.08f);
            G.Sfx.Play(el == HeroElement.Frost ? "clink" : el == HeroElement.Wind ? "dodge" : "throw", CastPoint, -10, 0.1f, el == HeroElement.Frost ? 1.9f : 1.4f);
        });
        return true;
    }

    /// <summary>A simple bolt met a creature: the blow and the touch.</summary>
    private void SimpleBoltStruck(ElementBolt bolt, Enemy e, Vector2 at)
    {
        float dealt = e.Hurt(bolt.Damage, bolt.Dir * 40f, at, DamageKind.Physical);
        if (dealt > 0) { OnDealtDamage(dealt); e.HitStop(Tune.Feel.HitStopBolt); }
        SimpleTouch(e, KitRole.Primary, at - bolt.Dir * 10f);
        Splash(e, at, bolt.Damage, DamageKind.Physical, Elements.ColorOf(Mode));
    }

    /// <summary>Creatures a caster's splash has struck (tests).</summary>
    public int Splashed { get; private set; }

    /// <summary>
    /// A caster's blow splashes (<see cref="Tune.Splash"/>): every other creature within its radius of where it landed (in sight of it) takes
    /// its share of the blow, and its share of the touch. The melee heroes' swings already strike all they sweep; this is the casters' answer.
    /// </summary>
    private void Splash(Enemy main, Vector2 at, float dmg, DamageKind kind, Color col)
    {
        if (dmg <= 0f || !Tune.Gauntlet.On) return;
        float r = Tune.Splash.Radius;
        G.Fx.Ring(at, r, new Color(col, 0.7f), 0.22f, FxLayer.RingStyle.Burst);
        foreach (var o in G.Enemies.ToArray())
        {
            if (o == main || o.Dead || !o.CanBeHit || o.GlobalPosition.DistanceTo(at) > r + o.HitRadius) continue;
            if (!G.Cave.LineClear(at, o.GlobalPosition)) continue;
            var away = (o.GlobalPosition - at).LengthSquared() > 1f ? (o.GlobalPosition - at).Normalized() : Vector2.Up;
            float d = o.Hurt(dmg * Tune.Splash.Share, away * 50f, o.GlobalPosition - away * o.HitRadius, kind);
            if (d <= 0f) continue;
            OnDealtDamage(d);
            Splashed++;
            if (!o.Dead) SimpleTouch(o, KitRole.Primary, at, Tune.Splash.Share);
        }
    }

    /// <summary>The Ward: a barrier on her and the friends near her.</summary>
    private bool SimpleWard()
    {
        bool free = Tune.Testing.NoCooldowns;
        if (!AbilityChargeReady && !free) return false;
        if (!free)
        {
            SpendAbilityCharge();
            SetSpentAbilityRest(Tune.ElementalStaff.AidRestWard * Stats.AbilityCdMult);
        }
        foreach (var h in HeroesNear(Tune.ElementalStaff.AidRange)) h.GiveBarrier(Tune.ElementalStaff.WardBarrier, Tune.ElementalStaff.AidSeconds);
        G.Fx.Ring(GlobalPosition, Tune.ElementalStaff.AidRange * 0.6f, new Color(Elements.ColorOf(Mode), 0.7f), 0.45f, FxLayer.RingStyle.Dashed);
        Anim.Once("heal", 3, 1.5f);
        _castGlow = 1f;
        G.Sfx.Play("levelup", GlobalPosition, -8, 0.05f, 0.9f);
        return true;
    }

    /// <summary>The Burst: a blast at the spot along the aim, the same in every element.</summary>
    private bool SimpleBurst(Vector2 aim)
    {
        bool free = Tune.Testing.NoCooldowns;
        if (!SnapReady && !free) return false;
        aim = aim.LengthSquared() > 0.01f ? aim.Normalized() : new Vector2(Facing, 0);
        if (Math.Abs(aim.X) > 0.15f) Facing = Math.Sign(aim.X);
        CastDir = aim;
        Anim.Face((int)Facing, instant: true);
        var el = Mode;
        var spot = FixedSpot(aim);
        float r = Tune.ElementalStaff.FixedRadius;
        var col = Elements.ColorOf(el);
        foreach (var e in FoesAt(spot, r))
        {
            var away = (e.GlobalPosition - spot).LengthSquared() > 1f ? (e.GlobalPosition - spot).Normalized() : Vector2.Up;
            Strike(e, Tune.ElementalStaff.BurstDamage * Stats.DamageMult, away * 140f, DamageKind.Physical);
            SimpleTouch(e, KitRole.Special, spot);
        }
        ElementWorld.Touch(el, spot, r, aim);
        // (the zone: the element lingers at the spot, building in all that stand in it; wind draws them in)
        var zone = new Blizzard
        {
            Position = spot, Radius = r, Seconds = Tune.Elements.ZoneSeconds, Ticks = Tune.Elements.ZoneTicks,
            Fire = el == HeroElement.Fire, El = el, Elemental = true, Damage = 0f, FreezeChance = 0f, IgniteChance = 0f,
            Build = el == HeroElement.Wind ? 0f : Tune.Elements.ZoneBuild, Pull = el == HeroElement.Wind ? Tune.Elements.ZonePull : 0f, Caster = this,
        };
        G.Spawn(zone);
        NetSync.HeroVisual(zone);
        LastBlizzard = zone;
        G.Fx.Converge(spot, r, new Color(col, 0.8f), 8, 0.15f);
        G.Fx.Ring(spot, r, new Color(col, 0.9f), 0.3f, FxLayer.RingStyle.Burst);
        G.Fx.Burst(spot, new Color(col, 0.9f), 16, 140, 2.2f, 0.4f, 0);
        Breakables.SpellBurst(spot, r * 0.5f);
        if (!free) { _snapCd = _snapCdTotal = Tune.ElementalStaff.BurstRest; _snapOwes = true; }
        LastCast = el == HeroElement.Fire ? "cinder" : "snap";
        _castGlow = 1f;
        G.Fx.Beam(CastPoint, spot, new Color(col, 0.45f));
        Anim.Once("rupture", 3, 1.6f);
        G.Main.Rumble(0.35f, 0.3f, 0.12f);
        SimpleUsed(KitRole.Special);
        return true;
    }

    // ================================================================== the heroes' own ways

    /// <summary>The Vitalist's drain landed on a creature: the touch, and whatever still seeps into it torn out at once.</summary>
    private void SimpleDrainStruck(Enemy t, float dmg)
    {
        SimpleTouch(t, KitRole.Primary, CastPoint);
        Splash(t, t.GlobalPosition, dmg, DamageKind.Nature, LifeColor);
        var el = t.SeepElement;
        float left = t.TakeSeep();
        if (left <= 0f || t.Dead) return;
        CashIns++;
        float amount = left * Tune.Elements.DrainCash;
        if (el == HeroElement.Wind) t.Shove((t.GlobalPosition - CastPoint).Normalized() * amount * Tune.Elements.ShovePerBuild + new Vector2(0, -60f));
        else t.Build(el, amount, Stats.DamageMult, this);
        G.Fx.Text(t.GlobalPosition + new Vector2(0, -t.HitRadius - 24), "TORN OUT", Elements.ColorOf(el), 10, 0.8f);
        G.Fx.Converge(t.GlobalPosition, t.HitRadius + 14, new Color(Elements.ColorOf(el), 0.9f), 8, 0.15f);
    }

    /// <summary>The Rogue's recall pulls a dagger out of a creature: all it stored there goes in at once.</summary>
    private void ReleaseKnife(ThrownDagger d, Enemy e)
    {
        if (d.Version != SimpleVer || d.Stored <= 0.5f || e.Dead) return;
        Releases++;
        float amount = d.Stored;
        d.Stored = 0f;
        if (d.El == HeroElement.Wind) e.Shove((GlobalPosition - e.GlobalPosition).Normalized() * amount * Tune.Elements.ShovePerBuild + new Vector2(0, -60f));
        else e.Build(d.El, amount, Stats.DamageMult, this);
        G.Fx.Text(e.GlobalPosition + new Vector2(0, -e.HitRadius - 24), "RELEASED", Elements.ColorOf(d.El), 10, 0.8f);
        G.Fx.Burst(e.GlobalPosition, new Color(Elements.ColorOf(d.El), 0.95f), 14, 160, 2.2f, 0.4f, 0);
    }

    /// <summary>The Swordsman's blow about to land: a tipper if it lands with the outer part of the blade, or in the riposte after a roll.</summary>
    private float TipperMult(Vector2 to)
    {
        _blowTipper = SimpleKit && IsSwordsman && (_riposteT > 0 || to.Length() >= _swingReach * Tune.Elements.TipperFrom);
        if (!_blowTipper) return 1f;
        Tippers++;
        var at = GlobalPosition + new Vector2(0, -3) + to;
        G.Fx.Text(at + new Vector2(0, -22), "TIPPER", new Color(1f, 0.95f, 0.7f), 9, 0.5f);
        G.Fx.Spark(at, to.Normalized(), true, new Color(1f, 0.95f, 0.75f));
        return Tune.Elements.TipperDamage;
    }

    // ================================================================== the other ability (each class's own, no element)

    private float _parryT, _parryCd, _blinkCd, _stepCd, _stepCritT;
    /// <summary>The Swordsman's Counter stance is up (a blow landing now is turned aside and answered).</summary>
    public bool Parrying => _parryT > 0;
    /// <summary>For the tests: counters from the stance, blinks, shadowsteps.</summary>
    public int Parries { get; private set; }
    public int Blinks { get; private set; }
    public int Shadowsteps { get; private set; }

    /// <summary>The second ability button: the class's own, the same in every element.</summary>
    private bool SimpleOther(Vector2 aim)
    {
        bool ok = Stats.Hero switch
        {
            HeroKind.Swordsman => TryParry(aim),
            // (in the Gauntlet the Great Bash is the Warden's special, and the Charge comes here)
            HeroKind.Warden => Tune.Gauntlet.On ? _bashT <= 0 && _gbashT <= 0 && TryShieldDash(aim) : _dashT <= 0 && TryShieldBash(aim),
            HeroKind.Elementalist => TryBlink(aim),
            HeroKind.Vitalist => TryRupture(aim),
            HeroKind.Aegis => TryBurden(aim),
            HeroKind.Rogue => TryShadowstep(aim),
            _ => false,
        };
        if (!ok) return false;
        _lastEl = HeroElement.None;
        Used(Stats.Hero == HeroKind.Aegis && Supporting ? "Burden" : Elements.Noun(Stats.Hero, KitRole.Other));
        return true;
    }

    /// <summary>The Swordsman's Counter: a parry stance; the blow that lands in it is answered (see <see cref="TryCounter"/>).</summary>
    private bool TryParry(Vector2 aim)
    {
        if (!IsSwordsman || _parryCd > 0 || Heaving || _dodgeT > 0 || _swingT >= 0) return false;
        _parryT = Tune.OtherAbility.ParryTime;
        _parryCd = Tune.OtherAbility.ParryCooldown;
        if (Math.Abs(aim.X) > 0.2f) Facing = Math.Sign(aim.X);
        Velocity = new Vector2(0, Velocity.Y);
        Anim.Face((int)Facing, instant: true);
        Anim.Once("shove", 3, 1.6f);
        var at = GlobalPosition + new Vector2(Facing * 10f, -6f);
        G.Fx.Glint(at, new Color(0.85f, 0.95f, 1f), 10);
        G.Fx.Ring(at, 14, new Color(0.85f, 0.95f, 1f, 0.8f), 0.25f, FxLayer.RingStyle.Dashed);
        G.Sfx.Play("clink", at, -8, 0.05f, 1.8f);
        return true;
    }

    /// <summary>A body fits here (the hero's middle at <paramref name="p"/>): open from head to feet, and a little each side.</summary>
    private static bool Fits(Vector2 p)
    {
        var cave = G.Cave;
        return !cave.IsSolid(p) && !cave.IsSolid(p + new Vector2(0, -11)) && !cave.IsSolid(p + new Vector2(0, 10)) && !cave.IsSolid(p + new Vector2(-6, 0)) && !cave.IsSolid(p + new Vector2(6, 0));
    }

    /// <summary>The Elementalist's Blink: a step through the air along the aim, short of rock and of rubble walls.</summary>
    private bool TryBlink(Vector2 aim)
    {
        if (!IsElementalist || _blinkCd > 0) return false;
        var d = aim.LengthSquared() > 0.01f ? aim.Normalized() : new Vector2(Facing, 0);
        var from = GlobalPosition;
        G.Cave.Raycast(from, d, Tune.OtherAbility.BlinkRange, out var hit, 4f);
        var to = hit - d * 10f;
        // (short of rock, rubble walls, and a trial's wards: in, never out)
        while (to.DistanceTo(from) > 12f && (!Fits(to) || Breakables.Blocks(from, to) || TrialSeal.BlocksMove(from, to))) to -= d * 6f;
        if (to.DistanceTo(from) <= 12f) { SayNo("NO ROOM"); _blinkCd = 0.4f; return true; }
        Afterimage.Spawn(Anim, new Color(0.75f, 0.6f, 1f), 0.3f);
        G.Fx.Burst(from, new Color(0.8f, 0.7f, 1f, 0.8f), 12, 120, 1.8f, 0.3f, 0);
        GlobalPosition = to;
        Velocity = d * 80f;
        _iframes = Math.Max(_iframes, 0.2f);
        _blinkCd = Tune.OtherAbility.BlinkCooldown;
        if (Math.Abs(d.X) > 0.15f) Facing = Math.Sign(d.X);
        Anim.Face((int)Facing, instant: true);
        G.Fx.Burst(to, new Color(0.8f, 0.7f, 1f, 0.9f), 12, 120, 1.8f, 0.3f, 0);
        G.Fx.Ring(to, 16, new Color(0.8f, 0.7f, 1f, 0.8f), 0.25f, FxLayer.RingStyle.Burst);
        G.Sfx.Play("dodge", to, -4, 0.05f, 1.6f);
        Blinks++;
        return true;
    }

    /// <summary>The Rogue's Shadowstep: to the far side of the creature nearest the aim, facing its back; her next jab is a sure critical.</summary>
    private bool TryShadowstep(Vector2 aim)
    {
        if (!IsRogue || _stepCd > 0 || _tetherTo != null) return false;
        aim = aim.LengthSquared() > 0.01f ? aim.Normalized() : new Vector2(Facing, 0);
        var t = FindSpellTarget(aim, Tune.OtherAbility.StepRange, Tune.OtherAbility.StepCone);
        if (t == null) { SayNo("NO ONE TO STEP TO"); _stepCd = 0.4f; return true; }
        float side = Math.Sign(t.GlobalPosition.X - GlobalPosition.X);
        if (side == 0) side = Facing;
        var to = t.GlobalPosition + new Vector2(side * (t.HitRadius + 12f), 0);
        if (!Fits(to)) to = t.GlobalPosition + new Vector2(side * (t.HitRadius + 12f), -8f);
        if (!Fits(to) || Breakables.Blocks(GlobalPosition, to) || TrialSeal.BlocksMove(GlobalPosition, to)) { SayNo("NO ROOM BEHIND IT"); _stepCd = 0.4f; return true; }
        G.Fx.Smoke(GlobalPosition, 5, new Color(0.18f, 0.12f, 0.24f, 0.75f), 30f);
        GlobalPosition = to;
        Velocity = Vector2.Zero;
        Facing = -side;
        Anim.Face((int)Facing, instant: true);
        Anim.Once("dodge", 2, 1.6f);
        _iframes = Math.Max(_iframes, 0.25f);
        _stepCritT = Tune.OtherAbility.StepCritWindow;
        _stepCd = Tune.OtherAbility.StepCooldown;
        G.Fx.Smoke(to, 5, new Color(0.18f, 0.12f, 0.24f, 0.75f), 30f);
        G.Sfx.Play("dodge", to, -6, 0.05f, 0.6f);
        Shadowsteps++;
        return true;
    }

    // ================================================================== for the HUD

    /// <summary>A role's rest (0 = ready, 1 = just used).</summary>
    public float SimpleFrac(KitRole r)
    {
        float atk = Math.Max(0.2f, Stats.AttackSpeed);
        if (Tune.Gauntlet.On && r == KitRole.Special) return BigFrac;
        if (Tune.Gauntlet.On && r == KitRole.Other && Stats.Hero == HeroKind.Warden) return DashCooldownFrac;
        return (Stats.Hero, r) switch
        {
            (HeroKind.Elementalist, KitRole.Primary) => Math.Clamp(_boltCd / (Tune.ElementalStaff.ShotGap / atk), 0f, 1f),
            (HeroKind.Vitalist, KitRole.Primary) => Math.Clamp(_drainCd / (Tune.Vitalist.DrainCooldown / atk), 0f, 1f),
            (HeroKind.Aegis, KitRole.Primary) => Math.Clamp(_wardCd / (Tune.Aegis.BoltEvery / atk), 0f, 1f),
            (_, KitRole.Primary) => SwingCooldownFrac,
            (HeroKind.Swordsman, KitRole.Other) => Math.Clamp(_parryCd / Tune.OtherAbility.ParryCooldown, 0f, 1f),
            (HeroKind.Warden, KitRole.Other) => BashCooldownFrac,
            (HeroKind.Elementalist, KitRole.Other) => Math.Clamp(_blinkCd / Tune.OtherAbility.BlinkCooldown, 0f, 1f),
            (HeroKind.Vitalist, KitRole.Other) => RuptureCooldownFrac,
            (HeroKind.Aegis, KitRole.Other) => Supporting ? 0f : SmiteCooldownFrac,
            (HeroKind.Rogue, KitRole.Other) => Math.Clamp(_stepCd / Tune.OtherAbility.StepCooldown, 0f, 1f),
            (HeroKind.Swordsman, KitRole.Defensive) => _dodgeCd.Length == 0 || _dodgeCd.Min() <= 0 ? 0f : Math.Clamp(_dodgeCd.Min() / Math.Max(0.01f, DodgeCooldownTotal), 0f, 1f),
            (HeroKind.Warden, KitRole.Defensive) => ShieldBroken ? Math.Clamp(_shieldBrokenT / Math.Max(0.01f, ShieldBreakTotal), 0f, 1f) : 0f,
            (HeroKind.Elementalist, KitRole.Defensive) => ElementAbilityFrac,
            (HeroKind.Vitalist, KitRole.Defensive) => HealCooldownFrac,
            (_, KitRole.Defensive) => AbilityCooldownFrac,
            (HeroKind.Swordsman, _) => HardSwingCooldownFrac,
            (HeroKind.Warden, _) => DashCooldownFrac,
            (HeroKind.Elementalist, _) => FixedCooldownFrac,
            (HeroKind.Vitalist, _) => HexCooldownFrac,
            (HeroKind.Aegis, _) => BubbleCooldownFrac,
            (HeroKind.Rogue, _) => ThrowCooldownFrac,
            _ => 0f,
        };
    }
}
