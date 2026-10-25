using System;
using System.Linq;
using Godot;

namespace DaggerCave;

/// <summary>
/// The simple kits (<see cref="Elements.Simple"/>): the elemental Swordsman, Warden, Elementalist, Vitalist, Aegis and Rogue each have a
/// primary attack (the attack button), a defensive (the dodge button) and a special (the ability button), and three elements to switch
/// between. An ability works the same in every element: the same damage, reach, speed and cooldown. What it strikes takes the element's
/// touch (<see cref="SimpleTouch"/>: its build-up, or wind's shove), and it looks and sounds of its element. Its blows are plain (no
/// creature resists or is weak to them by element), so the numbers never change with the element. Every defensive also sends a
/// pulse of the element out about the hero (<see cref="SimplePulse"/>).
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

    /// <summary>For the tests: creatures a simple kit's ability has touched (built up or shoved), and defensive pulses sent.</summary>
    public int SimpleTouches { get; private set; }
    public int SimplePulses { get; private set; }

    /// <summary>A simple kit's ability, as the HUD names it ("Searing Strike").</summary>
    public string SimpleName(KitRole r) => Elements.SimpleName(Stats.Hero, r, Mode);

    /// <summary>How much an ability of each role builds up (wind: how hard it shoves).</summary>
    private static float RoleBuild(KitRole r) => r switch
    {
        KitRole.Primary => Tune.Elements.PrimaryBuild, KitRole.Defensive => Tune.Elements.DefensiveBuild, _ => Tune.Elements.SpecialBuild,
    };

    /// <summary>The element's touch on a creature a simple ability reached: its build-up by the ability's role, or, wind, a shove away from
    /// <paramref name="from"/>.</summary>
    private void SimpleTouch(Enemy e, KitRole role, Vector2 from)
    {
        if (e == null || !IsInstanceValid(e) || e.Dead) return;
        var el = Mode;
        if (el == HeroElement.None) return;
        float build = RoleBuild(role);
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

    /// <summary>A defensive's pulse: every creature about the hero takes the element's touch (no blow).</summary>
    private void SimplePulse()
    {
        var c = GlobalPosition + new Vector2(0, -6);
        float r = Tune.Elements.PulseRadius;
        foreach (var e in FoesAt(c, r)) SimpleTouch(e, KitRole.Defensive, c);
        var col = Elements.ColorOf(Mode);
        G.Fx.Ring(c, r, new Color(col, 0.8f), 0.35f, FxLayer.RingStyle.Burst);
        G.Fx.Burst(c, new Color(col, 0.7f), 10, 120, 1.8f, 0.35f, 0);
        SimplePulses++;
    }

    /// <summary>A simple ability went out: its name, in its element's colour and sound.</summary>
    private void SimpleUsed(KitRole r)
    {
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
        SimplePulse();
        SimpleUsed(KitRole.Defensive);
        return true;
    }

    /// <summary>The ability button.</summary>
    private bool SimpleSpecial(Vector2 aim)
    {
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
                foreach (var e in G.Enemies.ToArray())
                    if (!e.Dead && e.CanBeHit && e.GlobalPosition.DistanceTo(c) <= r + e.HitRadius) SimpleTouch(e, KitRole.Special, c);
                G.Fx.Ring(c, r, new Color(Elements.ColorOf(Mode), 0.7f), 0.45f, FxLayer.RingStyle.Dashed);
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
        if (el == HeroElement.Fire) GasCloud.IgniteAt(spot, r);
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

    // ================================================================== for the HUD

    /// <summary>A role's rest (0 = ready, 1 = just used).</summary>
    public float SimpleFrac(KitRole r)
    {
        float atk = Math.Max(0.2f, Stats.AttackSpeed);
        return (Stats.Hero, r) switch
        {
            (HeroKind.Elementalist, KitRole.Primary) => Math.Clamp(_boltCd / (Tune.ElementalStaff.ShotGap / atk), 0f, 1f),
            (HeroKind.Vitalist, KitRole.Primary) => Math.Clamp(_drainCd / (Tune.Vitalist.DrainCooldown / atk), 0f, 1f),
            (HeroKind.Aegis, KitRole.Primary) => Math.Clamp(_wardCd / (Tune.Aegis.BoltEvery / atk), 0f, 1f),
            (_, KitRole.Primary) => SwingCooldownFrac,
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
