using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

/// <summary>
/// The elemental kits (<see cref="Elements"/>): an element is readied with its button (1, 2, 3; the d-pad), and the next attack
/// spends it. The Swordsman (earth, fire, frost): his Swing (the attack) is a plain combo, or with an element the Heaving Swing,
/// Pyre Strike or Razor Ice; the Hard Swing (the second ability) is a plain heavy blow, or ThunderCrush, Pyroclasm or Ice Axe; Empower
/// Hilt (the ability) lends his blows its element for a while. The Elementalist (fire, frost, wind): Missile (the attack: plain,
/// Firebolt, Frostbolt or Gust, an element lasting a few bolts), Extended (the ability: a storm of force, a Firestorm, a Blizzard or a
/// Cyclone), Burst (the second ability: a pulse, Detonate, Shatter or a gusting blast) and Aid (the dodge button: the updraft, or Kindling,
/// Frost Armor or Narrow Draft for the party). Each version rests its ability for its own time (<see cref="Tune.ElementalSword"/>,
/// <see cref="Tune.ElementalStaff"/>).
/// </summary>
public partial class Player
{
    /// <summary>This hero fights with elements (not the classic kit).</summary>
    public bool Elemental => Elements.On(Stats.Hero);
    /// <summary>The element readied for the next attack (none: the attack is plain).</summary>
    public HeroElement Infused { get; private set; }
    private float _infuseT;
    /// <summary>For the tests: elemental versions used, by name.</summary>
    public readonly List<string> ElementalUses = new();

    /// <summary>An element's button: it is readied (pressed again, put away).</summary>
    public bool TryInfuse(int slot)
    {
        var els = Elements.Of(Stats.Hero);
        if (Dead || slot < 0 || slot >= els.Length) return false;
        var el = els[slot];
        if (Infused == el) { Infused = HeroElement.None; G.Sfx.Play("ui", null, -10, 0, 0.7f); return true; }
        Infused = el;
        _infuseT = Tune.Elements.InfuseSeconds;
        var col = Elements.ColorOf(el);
        G.Fx.Ring(GlobalPosition + new Vector2(0, -6), 14, new Color(col, 0.9f), 0.25f, FxLayer.RingStyle.Burst);
        G.Fx.Text(GlobalPosition + new Vector2(0, -30), Elements.Name(el).ToUpperInvariant(), col, 10, 0.6f);
        G.Sfx.Play(el == HeroElement.Fire ? "lava" : el == HeroElement.Frost ? "clink" : el == HeroElement.Wind ? "dodge" : "rock", GlobalPosition, -10, 0.05f, 1.5f);
        Anim.Flash(0.2f);
        Anim.FlashColor = col;
        return true;
    }

    /// <summary>The readied element, spent on an attack.</summary>
    private HeroElement SpendElement()
    {
        var el = Infused;
        Infused = HeroElement.None;
        return el;
    }

    private void Used(string what)
    {
        ElementalUses.Add(what);
        G.Fx.Text(GlobalPosition + new Vector2(0, -32), what.ToUpperInvariant(), Elements.ColorOf(_lastEl), 11, 0.7f);
    }
    private HeroElement _lastEl;

    private void TickElemental(float dt)
    {
        if (!Elemental) return;
        if (Infused != HeroElement.None)
        {
            if ((_infuseT -= dt) <= 0) Infused = HeroElement.None;
            // (a readied element glows about the hand)
            else if (Engine.GetPhysicsFrames() % 4 == 0) G.Fx.Ember(GlobalPosition + new Vector2(Facing * 8, -4) + G.RandDir() * 4, Elements.ColorOf(Infused));
        }
        if (_swingElemRest > 0) _swingElemRest -= dt;
        if (_hiltT > 0 && (_hiltT -= dt) <= 0) _hiltEl = HeroElement.None;
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

    private void ClearSwingElement() { _swingEl = HeroElement.None; _swingBuild = 0; _swingShatter = false; }

    /// <summary>The attack button: a plain swing, or with an element its version (the element rests a moment after).</summary>
    private bool ElementSwing(Vector2 aim, bool held)
    {
        if (_swingCd > 0 || (held && _dodgeT > 0)) return false;
        var el = Infused;
        if (el == HeroElement.None || _swingElemRest > 0) return TrySwing(aim, held);
        if (el == HeroElement.Earth)
        {
            // the Heaving Swing: on your feet only (in the air the swing is plain, and the earth waits)
            if (InWater || !IsOnFloor()) return TrySwing(aim, held);
            SpendElement();
            _lastEl = el;
            _swingElemRest = Tune.ElementalSword.SwingRestEarth;
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
        _swingElemRest = el == HeroElement.Fire ? Tune.ElementalSword.SwingRestFire : Tune.ElementalSword.SwingRestFrost;
        if (_dodgeT > 0) _dodgeT = 0;
        StartSwing(aim);
        _swingDmg *= Tune.ElementalSword.MediumDamage;
        _swingEl = el; _swingBuild = Tune.ElementalSword.MediumBuild;
        Used(el == HeroElement.Fire ? "Pyre Strike" : "Razor Ice");
        return true;
    }

    /// <summary>The second ability: the Hard Swing, plain or by its element (ThunderCrush, Pyroclasm, Ice Axe), resting by which.</summary>
    private bool TryHardSwing(Vector2 aim)
    {
        if (_heaveCd > HeaveSpareTime || Heaving) return false;
        if (InWater || !IsOnFloor()) { _heaveWanted = true; return false; }
        _heaveWanted = false;
        var el = SpendElement();
        _lastEl = el;
        float rest, dmg, wind = 1f;
        string name;
        switch (el)
        {
            case HeroElement.Earth: rest = Tune.ElementalSword.CrushRest; dmg = Tune.ElementalSword.CrushDamage; wind = Tune.ElementalSword.CrushWindup; name = "ThunderCrush"; break;
            case HeroElement.Fire: rest = Tune.ElementalSword.PyroRest; dmg = Tune.ElementalSword.PyroDamage; name = "Pyroclasm"; break;
            case HeroElement.Frost: rest = Tune.ElementalSword.AxeRest; dmg = Tune.ElementalSword.AxeDamage; name = "Ice Axe"; break;
            default: rest = Tune.ElementalSword.HardRest; dmg = Tune.ElementalSword.HardDamage; name = "Hard Swing"; break;
        }
        float scale = Stats.HeaveCooldown / Math.Max(0.01f, Tune.Swordsman.HeaveCooldown);
        _heaveCdTotal = rest * scale;
        _heaveCd = Math.Max(0f, _heaveCd) + _heaveCdTotal;
        _dodgeT = 0;
        float dir = Math.Abs(aim.X) > 0.2f ? Math.Sign(aim.X) : Facing;
        Facing = dir;
        StartHeave();
        _swingDmg = BaseDamage * Stats.DamageMult * dmg * Stats.HeaveDamageMult;
        if (wind > 1f) { float extra = _windup * (wind - 1f); _windup += extra; _heaveRootT += extra; }
        _swingEl = el; _swingBuild = el == HeroElement.None ? 0f : Tune.Elements.Full; _swingShatter = el == HeroElement.Earth;
        Used(name);
        return true;
    }

    /// <summary>The ability: Empower Hilt, plain (harder blows) or lending every blow its element for a while.</summary>
    private bool TryHilt()
    {
        if (!AbilityChargeReady || _hiltT > 0) return false;
        var el = SpendElement();
        _lastEl = el;
        SpendAbilityCharge();
        float rest = el switch
        {
            HeroElement.Earth => Tune.ElementalSword.HiltRestEarth, HeroElement.Frost => Tune.ElementalSword.HiltRestFrost,
            HeroElement.Fire => Tune.ElementalSword.HiltRestFire, _ => Tune.ElementalSword.HiltRest,
        } * Stats.AbilityCdMult;
        SetSpentAbilityRest(rest);
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
        if (_hiltT <= 0 || e.Dead) return;
        switch (_hiltEl)
        {
            case HeroElement.Earth: e.Sap(Tune.Elements.SapSeconds); break;
            case HeroElement.Frost: e.Build(HeroElement.Frost, Tune.ElementalSword.HiltFrost, Stats.DamageMult, this); break;
            case HeroElement.Fire: e.Build(HeroElement.Fire, Tune.ElementalSword.HiltFire, Stats.DamageMult, this); break;
        }
    }

    /// <summary>How much harder a blade blow lands from a plain Empower Hilt.</summary>
    private float HiltDamageMult => Elemental && _hiltT > 0 && _hiltEl == HeroElement.None ? 1f + Tune.ElementalSword.HiltDamage : 1f;

    // ================================================================== the Elementalist

    private HeroElement _missileEl;
    private int _missileShots;
    private float _snapCdTotal = 1f, _updraftCdTotal = 1f;
    /// <summary>For the HUD: the element on the staff's bolts, and how many are left of it.</summary>
    public HeroElement MissileElement => _missileShots > 0 ? _missileEl : HeroElement.None;
    public int MissileShots => _missileShots;
    public float BurstCooldownFrac => Math.Clamp(_snapCd / Math.Max(0.01f, _snapCdTotal), 0, 1);
    public float AidCooldownFrac => Math.Clamp(_updraftCd / Math.Max(0.01f, _updraftCdTotal), 0, 1);
    /// <summary>For the HUD: the ability's rest (its own time for the version used), 0 = a use is ready.</summary>
    public float ElementAbilityFrac => AbilityChargeReady ? 0f : Math.Clamp(AbilityCooldowns.Min() / Math.Max(0.01f, _abilityRestTotal), 0, 1);

    /// <summary>The attack button: a bolt, plain or of the element on the staff (an element readied lasts a few bolts).</summary>
    private bool ElementMissile(Vector2 aim, bool held)
    {
        if (_boltCd > 0) return false;
        if (_boltCharges < 0) _boltCharges = BoltChargesMax;
        if (_boltCharges <= 0) return false;
        if (Infused != HeroElement.None) { _missileEl = SpendElement(); _missileShots = Tune.ElementalStaff.MissileShots; }
        var el = _missileShots > 0 ? _missileEl : HeroElement.None;
        if (_missileShots > 0) _missileShots--;
        aim = aim.LengthSquared() > 0.01f ? aim.Normalized() : new Vector2(Facing, 0);
        float range = Tune.Elementalist.BoltRange;
        var target0 = FindSpellTarget(aim, range, Tune.Elementalist.BoltConeDegrees);
        var dir0 = target0 != null ? (target0.GlobalPosition - CastPoint).Normalized() : aim;
        bool quick = el is HeroElement.Frost or HeroElement.Wind;
        _boltCd = (quick ? Tune.Elementalist.FrostGap : Tune.Elementalist.BoltGap) / Math.Max(0.2f, Stats.AttackSpeed);
        _boltCharges--;
        AttacksStarted++;
        BoltsCast++;
        if (Math.Abs(dir0.X) > 0.15f) Facing = Math.Sign(dir0.X);
        CastDir = dir0;
        Anim.Face((int)Facing, instant: true);
        LastCast = el == HeroElement.Frost ? "frost" : "fire";
        if (el != HeroElement.None) ElementalUses.Add(el switch { HeroElement.Fire => "Firebolt", HeroElement.Frost => "Frostbolt", _ => "Gust" });
        Hold(quick ? Tune.Elementalist.FrostDelay : Tune.Elementalist.FireDelay, "cast", () =>
        {
            if (Dead) return;
            var target = FindSpellTarget(aim, range, Tune.Elementalist.BoltConeDegrees);
            var dir = target != null ? (target.GlobalPosition - CastPoint).Normalized() : aim;
            if (Math.Abs(dir.X) > 0.15f) Facing = Math.Sign(dir.X);
            CastDir = dir;
            Anim.Face((int)Facing, instant: true);
            _castGlow = 0.7f;
            float baseDmg = Tune.Elementalist.FireDamage * el switch
            {
                HeroElement.Fire => 1f, HeroElement.Frost => Tune.Elementalist.FrostDamage / Tune.Elementalist.FireDamage,
                HeroElement.Wind => Tune.ElementalStaff.GustDamage, _ => Tune.ElementalStaff.PlainBolt,
            };
            var bolt = new ElementBolt
            {
                Position = CastPoint + dir * 6f, Dir = dir, Range = range, Caster = this,
                Frost = el == HeroElement.Frost, Wind = el == HeroElement.Wind, Plain = el == HeroElement.None,
                Damage = baseDmg * Stats.DamageMult * Stats.PrimaryDamageMult,
            };
            G.Spawn(bolt);
            NetSync.HeroVisual(bolt);
            G.Fx.Flash(CastPoint, 7, bolt.Tint, 0.08f);
            G.Sfx.Play(el == HeroElement.Frost ? "clink" : el == HeroElement.Wind ? "dodge" : "throw", CastPoint, -10, 0.1f, el == HeroElement.Frost ? 1.9f : 1.4f);
        });
        return true;
    }

    /// <summary>An elemental bolt met a creature: the blow, then its element's build-up (or, a gust, a shove).</summary>
    private void ElementBoltStruck(ElementBolt bolt, Enemy e, Vector2 at)
    {
        var el = bolt.Wind ? HeroElement.Wind : bolt.Plain ? HeroElement.None : bolt.Frost ? HeroElement.Frost : HeroElement.Fire;
        if (el == HeroElement.Fire) FireBurst(bolt, e, at);
        float dealt = e.Hurt(bolt.Damage, bolt.Dir * (el == HeroElement.Wind ? 10f : 40f), at, Elements.KindOf(el));
        if (dealt > 0) { OnDealtDamage(dealt); e.HitStop(Tune.Feel.HitStopBolt); }
        if (e.Dead) return;
        switch (el)
        {
            case HeroElement.Fire: e.Build(HeroElement.Fire, Tune.ElementalStaff.FireBuild, Stats.DamageMult, this); break;
            case HeroElement.Frost: e.Build(HeroElement.Frost, Tune.ElementalStaff.FrostBuild, Stats.DamageMult, this); break;
            case HeroElement.Wind: e.Shove(bolt.Dir * Tune.ElementalStaff.GustPush + new Vector2(0, -60f)); break;
        }
    }

    /// <summary>The creatures a Burst reaches: everything in view (a gust, those near).</summary>
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

    /// <summary>The second ability: Burst. A plain pulse round you, Detonate (fire), Shatter (frost) or a gusting blast (wind).</summary>
    private bool TryBurst()
    {
        if (!SnapReady) return false;
        var el = SpendElement();
        _lastEl = el;
        float rest = el switch
        {
            HeroElement.Fire => Tune.ElementalStaff.DetonateRest, HeroElement.Frost => Tune.ElementalStaff.ShatterRest,
            HeroElement.Wind => Tune.ElementalStaff.GustRest, _ => Tune.ElementalStaff.BurstRest,
        };
        _snapCd = _snapCdTotal = rest;
        _snapOwes = true;
        LastCast = el == HeroElement.Fire ? "cinder" : "snap";
        _castGlow = 1f;
        float power = Stats.DamageMult;
        var col = Elements.ColorOf(el);
        switch (el)
        {
            case HeroElement.Fire:
            {
                int n = 0;
                foreach (var e in InView()) { n++; G.Fx.Converge(e.GlobalPosition, e.HitRadius + 12, col, 5, 0.12f); e.Build(HeroElement.Fire, Tune.Elements.Full, power, this); }
                if (Stats.SnapEcho) GainAlimus(Tune.Elementalist.EchoAlimus * n);
                Used("Detonate");
                break;
            }
            case HeroElement.Frost:
            {
                int n = 0;
                foreach (var e in InView()) { n++; if (!e.Shatter(power, this)) e.Build(HeroElement.Frost, Tune.Elements.Full, power, this); }
                if (Stats.SnapEcho) GainAlimus(Tune.Elementalist.EchoAlimus * n);
                Used("Shatter");
                break;
            }
            case HeroElement.Wind:
                foreach (var e in InView(Tune.ElementalStaff.GustRadius))
                {
                    var away = (e.GlobalPosition - GlobalPosition).Normalized();
                    float d = e.Hurt(Tune.ElementalStaff.GustBurstDamage * power, away * 40f, e.GlobalPosition, DamageKind.Physical);
                    if (d > 0) OnDealtDamage(d);
                    e.Shove(away * Tune.ElementalStaff.GustShove + new Vector2(0, -90f));
                }
                foreach (var pr in G.Main.EnemyProjectiles.ToArray())
                    if (IsInstanceValid(pr) && pr.GlobalPosition.DistanceTo(GlobalPosition) < Tune.ElementalStaff.GustRadius) pr.Deflect();
                G.Fx.Shockwave(GlobalPosition, Tune.ElementalStaff.GustRadius, new Color(col, 0.6f), 0.35f);
                Used("Gust");
                break;
            default:
                foreach (var e in InView(Tune.ElementalStaff.PulseRadius))
                {
                    var away = (e.GlobalPosition - GlobalPosition).Normalized();
                    float d = e.Hurt(Tune.ElementalStaff.PulseDamage * power, away * 120f, e.GlobalPosition, DamageKind.Physical);
                    if (d > 0) OnDealtDamage(d);
                }
                G.Fx.Shockwave(GlobalPosition, Tune.ElementalStaff.PulseRadius, new Color(col, 0.55f), 0.3f);
                Used("Pulse");
                break;
        }
        Anim.Once("rupture", 3, 1.6f);
        G.Sfx.Play(el == HeroElement.Fire ? "lava" : el == HeroElement.Frost ? "clink" : "dodge", GlobalPosition, -4, 0.05f, 0.8f);
        G.Main.Rumble(0.35f, 0.3f, 0.12f);
        return true;
    }

    /// <summary>The ability: Extended, the storm at the aim (plain force, Firestorm, Blizzard or Cyclone); it rests by its element.</summary>
    private bool TryStorm(Vector2 aim, float aimDist)
    {
        if (!AbilityChargeReady) return false;
        var el = SpendElement();
        _lastEl = el;
        SpendAbilityCharge();
        if (_stormOwes.Length != _abilityCd.Length) Array.Resize(ref _stormOwes, _abilityCd.Length);
        float restMult = el switch
        {
            HeroElement.Fire => Tune.ElementalStaff.StormRestFire, HeroElement.Frost => Tune.ElementalStaff.StormRestFrost,
            HeroElement.Wind => Tune.ElementalStaff.StormRestWind, _ => Tune.ElementalStaff.StormRestPlain,
        };
        SetSpentAbilityRest(AbilityRecharge * restMult);
        for (int k = 0; k < _abilityCd.Length; k++) if (_abilityCd[k] > 0 && !_stormOwes[k] && Math.Abs(_abilityCd[k] - _abilityRestTotal) < 0.001f) { _stormOwes[k] = true; break; }
        aim = aim.LengthSquared() > 0.01f ? aim.Normalized() : new Vector2(Facing, 0);
        var at0 = BlizzardSpot(aim, aimDist);
        var dir0 = (at0 - CastPoint).Normalized();
        if (Math.Abs(dir0.X) > 0.15f) Facing = Math.Sign(dir0.X);
        CastDir = dir0;
        Anim.Face((int)Facing, instant: true);
        LastCast = el == HeroElement.Fire ? "firestorm" : "blizzard";
        string name = el switch { HeroElement.Fire => "Firestorm", HeroElement.Frost => "Blizzard", HeroElement.Wind => "Cyclone", _ => "Tempest" };
        Used(name);
        Hold(Tune.Elementalist.BlizzardDelay, "heal", () =>
        {
            if (Dead) return;
            var at = BlizzardSpot(aim, aimDist);
            float secondsMult = Stats.BlizzardSecondsMult;
            float dmg = el switch
            {
                HeroElement.Fire => Tune.Elementalist.FirestormDamage, HeroElement.Frost => Tune.Elementalist.BlizzardDamage,
                HeroElement.Wind => Tune.Elementalist.BlizzardDamage * 0.5f, _ => Tune.Elementalist.BlizzardDamage * Tune.ElementalStaff.PlainStormDamage,
            };
            var storm = new Blizzard
            {
                Position = at,
                Radius = Tune.Elementalist.BlizzardRadius * Stats.BlizzardWideMult,
                Seconds = Tune.Elementalist.BlizzardSeconds * secondsMult,
                Ticks = Mathf.RoundToInt(Tune.Elementalist.BlizzardTicks * secondsMult),
                Fire = el == HeroElement.Fire, El = el, Elemental = true,
                Damage = dmg * Stats.DamageMult, FreezeChance = 0f, IgniteChance = 0f,
                Build = el == HeroElement.Fire ? Tune.ElementalStaff.StormFireBuild : el == HeroElement.Frost ? Tune.ElementalStaff.StormFrostBuild : 0f,
                Pull = el == HeroElement.Wind ? Tune.ElementalStaff.CyclonePull : 0f,
                Caster = this,
            };
            G.Spawn(storm);
            NetSync.HeroVisual(storm);
            LastBlizzard = storm;
            _castGlow = 1f;
            G.Fx.Beam(CastPoint, at, new Color(storm.Tint, 0.6f));
            G.Sfx.Play(el == HeroElement.Fire ? "lava" : el == HeroElement.Wind ? "dodge" : "gasp", at, -4, 0.1f, el == HeroElement.Fire ? 0.8f : 0.5f);
        });
        return true;
    }

    /// <summary>One strike of an elemental storm: the blow, its build-up, and a Cyclone's pull toward its middle.</summary>
    private void ElementStormStruck(Blizzard storm, Enemy e)
    {
        var from = (e.GlobalPosition - storm.GlobalPosition).Normalized();
        float dealt = e.Hurt(storm.Damage, from * 10f, e.GlobalPosition - from * e.HitRadius * 0.5f, Elements.KindOf(storm.El));
        if (dealt > 0) OnDealtDamage(dealt);
        if (e.Dead) return;
        if (storm.Build > 0) e.Build(storm.El, storm.Build, Stats.DamageMult, this);
        if (storm.Pull > 0 && e.GlobalPosition.DistanceTo(storm.GlobalPosition) > 10f) e.Shove(-from * storm.Pull * 2.2f);
    }

    /// <summary>The dodge button: Aid. Plain, the updraft (aimed as ever); with an element, Kindling, Frost Armor or Narrow Draft at once.</summary>
    private bool ElementAid(in PlayerInput inp)
    {
        if (Infused == HeroElement.None || _draftAiming) return BeginDraftAim(inp);
        if (!UpdraftReady) return false;
        var el = SpendElement();
        _lastEl = el;
        float rest = el switch { HeroElement.Wind => Tune.ElementalStaff.AidRestWind, HeroElement.Frost => Tune.ElementalStaff.AidRestFrost, _ => Tune.ElementalStaff.AidRestFire };
        _updraftCd = _updraftCdTotal = rest;
        _updraftOwes = true;
        float secs = Tune.ElementalStaff.AidSeconds;
        var col = Elements.ColorOf(el);
        switch (el)
        {
            case HeroElement.Wind: foreach (var h in HeroesNear(Tune.ElementalStaff.AidRange)) h.GiveBuff(BuffTailwind, secs); Used("Narrow Draft"); break;
            case HeroElement.Frost: GiveBuff(BuffFrostArmor, secs); Used("Frost Armor"); break;
            default: foreach (var h in HeroesNear(Tune.ElementalStaff.AidRange)) h.GiveBuff(BuffKindling, secs); Used("Kindling"); break;
        }
        G.Fx.Ring(GlobalPosition, Tune.ElementalStaff.AidRange * 0.6f, new Color(col, 0.7f), 0.45f, FxLayer.RingStyle.Dashed);
        Anim.Once("hex", 3, 1.5f);
        LastCast = "updraft";
        _castGlow = 1f;
        G.Sfx.Play("levelup", GlobalPosition, -8, 0.05f, el == HeroElement.Wind ? 1.3f : 0.9f);
        return true;
    }

    /// <summary>Frost Armor: a creature that strikes this hero in melee is filled with frost.</summary>
    private void FrostArmorStruck(Enemy source, bool melee)
    {
        if (!melee || source == null || !IsInstanceValid(source) || source.Dead || !HasBuff(BuffFrostArmor)) return;
        source.Build(HeroElement.Frost, Tune.Elements.Full, Stats.DamageMult, this);
        G.Fx.Burst(source.GlobalPosition, Elements.ColorOf(HeroElement.Frost), 8, 90, 1.6f, 0.3f, 60);
    }
}
