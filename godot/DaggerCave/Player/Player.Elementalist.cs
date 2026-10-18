using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

/// <summary>
/// The Elementalist's kit. A spell costs nothing to cast; alimus (which comes back by itself, its only
/// source) is what a spell's recharge costs. A spell that has been cast does not begin to recharge until there is enough alimus, and
/// then spends it to start (the bolts one at a time, the updraft, the snap and the storm each in turn). The attack button hurls a firebolt that can set a creature
/// alight (Frostbolt: quicker, weaker bolts of frost that slow, and now and then freeze a
/// creature solid). The dodge button raises an Updraft, a column of air in which every
/// hero has a fraction of gravity and a fraction of the fall speed (floating up on a jump, drifting down). The ability button calls down a Blizzard at the aim point: nine
/// small strikes that can each freeze a creature (Firestorm: fire that sets them alight). The
/// second ability snaps: every frozen creature in view shatters, hurting whatever is near it
/// (Cinder Snap: every burning creature bursts). The staff holds two bolts (Frostbolt: three) and
/// gets them back one at a time; each spell's readiness shows as an orb about the hero, which
/// vanishes while that spell recharges. Alterations: Frostbolt, Firestorm and Cinder Snap.
/// </summary>
public partial class Player
{
    private bool IsElementalist => Stats.Hero == HeroKind.Elementalist;

    /// <summary>The Elementalist's reserve: spells cost it, and it comes back by itself.</summary>
    public float Alimus { get; private set; }
    private float _boltCd, _updraftCd, _snapCd, _snapAt = -1;
    // a cast spell is free; what it costs is paid when it comes back (the orb returns): the bolt's next charge, the updraft, the snap, each use of the storm. Its recharge runs
    // as usual, but at the end of it the alimus is taken, and with too little the spell stays out (held a hair short of ready) until there is enough
    private bool _updraftOwes, _snapOwes;
    private const float Hair = 0.001f;
    private bool[] _stormOwes = new bool[1];
    /// <summary>Bolts in the staff (-1 = not yet filled for this run), and how far the next one has recharged (seconds).</summary>
    private int _boltCharges = -1;
    private float _boltRecharge;
    private List<Enemy> _snapping;

    /// <summary>Bolts cast, blizzards called, snaps made (for the tests).</summary>
    public int BoltsCast { get; private set; }
    public Blizzard LastBlizzard { get; private set; }
    public Updraft LastUpdraft { get; private set; }
    /// <summary>Creatures the last snap burst.</summary>
    public int LastSnapCount { get; private set; }

    public float AlimusRegen => Tune.Elementalist.AlimusRegen * Stats.AlimusRegenMult;
    public float UpdraftCost => Tune.Elementalist.UpdraftCost;
    public float BlizzardCost => Tune.Elementalist.BlizzardCost;
    public float SnapCost => Tune.Elementalist.SnapCost;
    /// <summary>For the HUD: the updraft's short recovery and the snap's, 0 = ready.</summary>
    public float UpdraftCooldownFrac => Math.Clamp(_updraftCd / Tune.Elementalist.UpdraftRecharge, 0, 1);
    public float SnapCooldownFrac => Math.Clamp(_snapCd / SnapCooldown, 0, 1);
    /// <summary>The snap's recharge (Cinder Snap: it leaves the flames lit, and takes longer to come back).</summary>
    private float SnapCooldown => Tune.Elementalist.SnapCooldown + (Stats.CinderSnap ? Tune.Elementalist.CinderSnapExtra : 0f);
    /// <summary>The spell is ready to cast (not recharging, and not waiting for the alimus to come back).</summary>
    public bool UpdraftReady => _updraftCd <= 0 && !_updraftOwes;
    public bool SnapReady => _snapCd <= 0 && !_snapOwes && _snapAt < 0;
    /// <summary>For the HUD: a cast spell is waiting for enough alimus to begin recharging (the bolt's next charge, the updraft, the snap, the storm).</summary>
    public bool BoltWaiting => _boltCharges >= 0 && _boltCharges < BoltChargesMax && _boltRecharge >= BoltRechargeSeconds - Hair;
    public bool UpdraftWaiting => _updraftOwes && _updraftCd <= Hair * 1.5f;
    public bool SnapWaiting => _snapOwes && _snapCd <= Hair * 1.5f;
    public bool StormWaiting { get { for (int k = 0; k < _stormOwes.Length && k < _abilityCd.Length; k++) if (_stormOwes[k] && _abilityCd[k] <= Hair * 1.5f) return true; return false; } }
    /// <summary>What a snap would burst right now (for the HUD): the frozen creatures in view, or with Cinder Snap the burning ones.</summary>
    public int SnapTargets => Snappable().Count();

    /// <summary>Bolts the Elementalist can hold: two of fire, four of frost (Spare Bolt and Frozen Quiver add more).</summary>
    public int BoltChargesMax => IsRemote ? _netBoltMax : Stats.Frostbolt ? Tune.Elementalist.FrostCharges + Stats.FrostChargeBonus : Tune.Elementalist.FireCharges + Stats.FireChargeBonus;
    /// <summary>Bolts ready to throw (a copy goes by what its game says).</summary>
    public int BoltCharges => IsRemote ? _netBolts : Math.Clamp(_boltCharges < 0 ? BoltChargesMax : _boltCharges, 0, BoltChargesMax);
    private int _netBolts, _netBoltMax;
    /// <summary>Seconds one bolt takes to come back.</summary>
    private float BoltRechargeSeconds => Stats.Frostbolt ? Tune.Elementalist.FrostEvery : Tune.Elementalist.FireEvery;
    /// <summary>For the orbs and the HUD: the ready state of the updraft, the snap and the storm (a copy goes by its game's flags).</summary>
    public bool UpdraftOrb => IsRemote ? (_netFlags & HfOrbWind) != 0 : UpdraftReady;
    public bool SnapOrb => IsRemote ? (_netFlags & HfOrbSnap) != 0 : SnapReady;
    public bool StormOrb => IsRemote ? (_netFlags & HfOrbStorm) != 0 : AbilityChargeReady;
    /// <summary>The storm is fire (Firestorm).</summary>
    public bool FireStorm => IsRemote ? (_netFlags & HfFire) != 0 : Stats.Firestorm;

    /// <summary>The Elementalist's bolts are frost (Frostbolt); a copy goes by its game's flags (for the staff's orb).</summary>
    public bool FrostElement => IsRemote ? (_netFlags & HfFrost) != 0 : Stats.Frostbolt;

    /// <summary>Test harness and level changes: set the reserve directly.</summary>
    /// <summary>Test aid: the staff is ready again.</summary>
    /// <summary>Test aid: the updraft is ready again.</summary>
    public void TestResetUpdraft() { _updraftCd = 0; _updraftOwes = false; }
    public void TestResetBolt() { _boltCd = 0; _boltCharges = BoltChargesMax; _boltRecharge = 0; }
    /// <summary>Test aid: every spell ready again, nothing owed.</summary>
    public void TestResetSpells() { TestResetUpdraft(); TestResetBolt(); _snapCd = 0; _snapOwes = false; for (int k = 0; k < _abilityCd.Length; k++) { _abilityCd[k] = 0; if (k < _stormOwes.Length) _stormOwes[k] = false; } }

    public void SetAlimus(float value) => Alimus = Math.Clamp(value, 0, Stats.AlimusMax);

    public void GainAlimus(float amount)
    {
        if (!IsElementalist || amount <= 0) return;
        Alimus = Math.Min(Stats.AlimusMax, Alimus + amount);
    }

    private void TickElementalist(float dt)
    {
        _castGlow = Math.Max(0f, _castGlow - dt * 2.5f);
        _boltCd -= dt;
        Alimus = Math.Min(Stats.AlimusMax, Alimus + AlimusRegen * dt);
        // a cast spell is free; its recharge runs, and when it is done the alimus is taken to bring it back (the dearest first, so the cheap ones don't keep it from the
        // great ones). With too little, it waits, a hair short of ready, until there is enough.
        if (_stormOwes.Length != _abilityCd.Length) Array.Resize(ref _stormOwes, _abilityCd.Length);
        for (int k = 0; k < _stormOwes.Length; k++)
        {
            if (!_stormOwes[k] || _abilityCd[k] > Hair) continue;
            if (SpendAlimus(BlizzardCost)) { _stormOwes[k] = false; _abilityCd[k] = 0; } else _abilityCd[k] = Hair;
        }
        _updraftCd -= dt; _snapCd -= dt;
        if (_updraftOwes && _updraftCd <= Hair)
        {
            if (SpendAlimus(UpdraftCost)) { _updraftOwes = false; _updraftCd = 0; } else _updraftCd = Hair;
        }
        if (_snapOwes && _snapCd <= Hair)
        {
            if (SpendAlimus(SnapCost)) { _snapOwes = false; _snapCd = 0; } else _snapCd = Hair;
        }
        // the staff holds a couple of bolts and gains them back one at a time (each pays its cost as it comes back)
        int max = BoltChargesMax;
        if (_boltCharges < 0) _boltCharges = max;
        if (_boltCharges > max) _boltCharges = max;
        if (_boltCharges < max)
        {
            float secs = BoltRechargeSeconds;
            _boltRecharge = Math.Min(secs, _boltRecharge + dt * Math.Max(0.2f, Stats.AttackSpeed));
            if (_boltRecharge >= secs - Hair && SpendAlimus(Stats.Frostbolt ? Tune.Elementalist.FrostCost : Tune.Elementalist.FireCost)) { _boltCharges++; _boltRecharge = 0; }
        }
        else _boltRecharge = 0;
        if (_snapAt >= 0 && (_snapAt -= dt) < 0) BurstSnap();
    }

    /// <summary>Spends alimus for a spell, or says there isn't enough (the press is spent either way).</summary>
    private bool PayAlimus(float cost)
    {
        if (SpendAlimus(cost)) return true;
        SayNo("NOT ENOUGH ALIMUS");
        return false;
    }

    /// <summary>Takes the cost from the alimus held; with Blood Channeling, what's lacking is paid in health (never the last of it).</summary>
    private bool SpendAlimus(float cost)
    {
        if (Alimus >= cost - 0.001f) { Alimus = Math.Max(0, Alimus - cost); return true; }
        if (!Stats.BloodCast) return false;
        float blood = (cost - Alimus) * Tune.Relics.BloodPerAlimus;
        if (Hp <= blood + 1f) return false;
        Alimus = 0;
        float hp0 = Hp;
        Hp -= blood;
        if (Num.Delta(hp0, Hp) > 0) G.Fx.Text(GlobalPosition + new Vector2(0, -22), "-" + Num.Delta(hp0, Hp), new Color(0.85f, 0.15f, 0.2f), 12, 0.7f);
        return true;
    }

    // ---------------------------------------------------------------- bolts

    private bool CastBolt(Vector2 aim, bool held = false)
    {
        if (_boltCd > 0) return false;
        if (_boltCharges < 0) _boltCharges = BoltChargesMax;
        if (_boltCharges <= 0) return false;
        aim = aim.LengthSquared() > 0.01f ? aim.Normalized() : new Vector2(Facing, 0);
        bool frost = Stats.Frostbolt;
        float range = Tune.Elementalist.BoltRange;
        // (a creature near the line of aim draws the bolt to it)
        var target0 = FindSpellTarget(aim, range, Tune.Elementalist.BoltConeDegrees);
        var dir0 = target0 != null ? (target0.GlobalPosition - CastPoint).Normalized() : aim;
        // (the next waits a moment; the one spent starts coming back, unless another is already on its way)
        _boltCd = (frost ? Tune.Elementalist.FrostGap : Tune.Elementalist.BoltGap) / Math.Max(0.2f, Stats.AttackSpeed);
        _boltCharges--;
        AttacksStarted++;
        BoltsCast++;
        if (Math.Abs(dir0.X) > 0.15f) Facing = Math.Sign(dir0.X);
        CastDir = dir0;
        Anim.Face((int)Facing, instant: true);
        LastCast = frost ? "frost" : "fire";
        // (the staff gathers the bolt first, the hero held still and animated: 0.2 s for a firebolt, 0.1 s for a frostbolt)
        Hold(frost ? Tune.Elementalist.FrostDelay : Tune.Elementalist.FireDelay, "cast", () =>
        {
            if (Dead) return;
            // (it flies where the aim and the nearest creature are now)
            var target = FindSpellTarget(aim, range, Tune.Elementalist.BoltConeDegrees);
            var dir = target != null ? (target.GlobalPosition - CastPoint).Normalized() : aim;
            if (Math.Abs(dir.X) > 0.15f) Facing = Math.Sign(dir.X);
            CastDir = dir;
            Anim.Face((int)Facing, instant: true);
            _castGlow = 0.7f;
            float dmg = (frost ? Tune.Elementalist.FrostDamage : Tune.Elementalist.FireDamage) * Stats.DamageMult * Stats.PrimaryDamageMult;
            var bolt = new ElementBolt { Position = CastPoint + dir * 6f, Dir = dir, Frost = frost, Damage = dmg, Range = range, Caster = this };
            G.Spawn(bolt);
            NetSync.HeroVisual(bolt);
            G.Fx.Flash(CastPoint, 7, bolt.Tint, 0.08f);
            G.Sfx.Play(frost ? "clink" : "throw", CastPoint, frost ? -12 : -9, 0.1f, frost ? 1.9f : 1.3f);
        });
        return true;
    }

    /// <summary>
    /// A bolt of this hero's met a creature: the blow, and what the element does to it (a fire
    /// may set it alight; frost chills it, and may freeze a regular creature solid).
    /// </summary>
    public void BoltStruck(ElementBolt bolt, Enemy e, Vector2 at)
    {
        if (!IsInstanceValid(e) || e.Dead) return;
        if (Elemental) { ElementBoltStruck(bolt, e, at); return; }
        // a firebolt bursts as it lands: a little of the blow splashes the creatures pressed close about it
        if (!bolt.Frost) FireBurst(bolt, e, at);
        float dealt = e.Hurt(bolt.Damage, bolt.Dir * (bolt.Frost ? 30f : 60f), at, bolt.Frost ? DamageKind.Frost : DamageKind.Fire);
        if (dealt > 0)
        {
            OnDealtDamage(dealt);
            e.HitStop(Tune.Feel.HitStopBolt);
        }
        if (e.Dead) return;
        if (bolt.Frost)
        {
            e.Chill(Tune.Elementalist.ChillSlow, Tune.Elementalist.ChillSeconds);
            if (!Stats.NoFreeze && G.Chance(Tune.Elementalist.FreezeChance + Stats.FreezeBonus)) e.FreezeSolid(Tune.Elementalist.FreezeSeconds);
        }
        else if (G.Chance(Stats.IgniteChance)) e.Ignite(Tune.Elementalist.IgniteDps * Stats.DamageMult * Stats.BurnMult, Tune.Elementalist.IgniteSeconds);
    }

    /// <summary>A firebolt's burst: FireBurstShare of the blow to every other creature within FireBurstRadius px (Flashpoint widens and deepens it).</summary>
    private void FireBurst(ElementBolt bolt, Enemy e, Vector2 at)
    {
        float reach = Tune.Elementalist.FireBurstRadius * Stats.FireBurstMult;
        G.Fx.Ring(at, reach, new Color(ElementBolt.FireColor, 0.75f), 0.22f, FxLayer.RingStyle.Burst);
        foreach (var o in G.Enemies.ToArray())
        {
            if (o == e || o.Dead || !o.CanBeHit || o.GlobalPosition.DistanceTo(at) > reach + o.HitRadius) continue;
            if (!G.Cave.LineClear(at, o.GlobalPosition)) continue;
            float d = o.Hurt(bolt.Damage * Stats.FireBurstShare, (o.GlobalPosition - at).Normalized() * 40f, o.GlobalPosition, DamageKind.Fire);
            if (d > 0) { OnDealtDamage(d); FireBursts++; }
        }
    }

    /// <summary>Creatures a firebolt's burst has splashed (tests).</summary>
    public int FireBursts { get; private set; }

    // ---------------------------------------------------------------- updraft

    // The dodge button aims the updraft: held down, a faint column shows where it would stand (it follows the mouse, the right stick, or
    // the way you push the move keys; with none of them, it stands straight up); let go, and it is raised at that angle.
    private bool _draftAiming;
    private Vector2 _draftDir = Vector2.Up;
    private DraftAim _draftPreview;
    /// <summary>For the HUD and the tests: the updraft is being aimed.</summary>
    public bool AimingDraft => _draftAiming;

    /// <summary>Where a column aimed along <paramref name="dir"/> would start: from the ground at your feet when it points up, else from your middle.</summary>
    private Vector2 DraftOrigin(Vector2 dir)
    {
        if (-dir.Y > 0.7f)
        {
            // (from the ground at your feet, or from where you hang in the air, if the ground is far)
            var foot = GlobalPosition + new Vector2(0, 13f);
            if (!IsOnFloor() && G.Cave.FindFloor(GlobalPosition, 70f, out var floor)) foot = floor;
            return foot;
        }
        return GlobalPosition - dir * 4f;
    }

    /// <summary>The press: you take aim (or are told you can't).</summary>
    private bool BeginDraftAim(in PlayerInput inp)
    {
        if (!IsElementalist || !UpdraftReady) return false;
        if (_draftAiming) return true;
        _draftAiming = true;
        _draftDir = inp.AimGiven && inp.Aim.LengthSquared() > 0.01f ? inp.Aim.Normalized() : Vector2.Up;
        _draftPreview = new DraftAim();
        G.Spawn(_draftPreview);
        UpdateDraftPreview();
        G.Sfx.Play("dodge", GlobalPosition, -14, 0.05f, 1.6f);
        return true;
    }

    private void UpdateDraftPreview()
    {
        if (_draftPreview == null || !IsInstanceValid(_draftPreview)) return;
        var o = DraftOrigin(_draftDir);
        _draftPreview.GlobalPosition = o;
        _draftPreview.Angle = MathF.Atan2(_draftDir.X, -_draftDir.Y);
        _draftPreview.Width = Tune.Elementalist.UpdraftWidth;
        _draftPreview.Height = Updraft.CutHeight(o, _draftDir, Tune.Elementalist.UpdraftHeight);
    }

    private void EndDraftAim()
    {
        _draftAiming = false;
        if (_draftPreview != null && IsInstanceValid(_draftPreview)) _draftPreview.QueueFree();
        _draftPreview = null;
    }

    /// <summary>Each tick while aiming: follow the aim while the button is held; when it is let go, raise the column at the final angle.</summary>
    private void TickDraftAim(in PlayerInput inp)
    {
        if (!_draftAiming) return;
        if (Dead || _frozenT > 0 || G.Main.MenuOpen) { EndDraftAim(); return; }
        if (inp.GuardHeld)
        {
            // (aimed nowhere, it stands straight up)
            _draftDir = inp.AimGiven && inp.Aim.LengthSquared() > 0.01f ? inp.Aim.Normalized() : Vector2.Up;
            CastDir = _draftDir;
            UpdateDraftPreview();
            return;
        }
        var dir = _draftDir;
        EndDraftAim();
        CastUpdraft(dir);
    }

    /// <summary>The button is let go: the column is raised at the angle aimed.</summary>
    private void CastUpdraft(Vector2 dir)
    {
        // (free to raise; its recharge is what costs the alimus)
        _updraftCd = Tune.Elementalist.UpdraftRecharge;
        _updraftOwes = true;
        var origin = DraftOrigin(dir);
        var draft = new Updraft
        {
            Position = origin,
            Width = Tune.Elementalist.UpdraftWidth,
            Height = Tune.Elementalist.UpdraftHeight,
            Life = Tune.Elementalist.UpdraftSeconds,
            // (any angle at all: up, along, or down)
            Angle = MathF.Atan2(dir.X, -dir.Y),
        };
        G.Spawn(draft);
        NetSync.HeroVisual(draft);
        LastUpdraft = draft;
        if (Math.Abs(dir.X) > 0.3f) { Facing = Math.Sign(dir.X); Anim.Face((int)Facing, instant: true); }
        CastDir = dir;
        Anim.Once("hex", 3, 1.5f);
        LastCast = "updraft";
        _castGlow = 1f;
        G.Fx.Shockwave(origin, 34, new Color(0.85f, 0.95f, 1f, 0.3f), 0.4f);
        G.Fx.Dust(origin, 6, 1.4f, new Color(0.85f, 0.9f, 0.95f, 0.35f));
        G.Sfx.Play("dodge", GlobalPosition, -4, 0.05f, 0.6f);
        G.Main.Rumble(0.25f, 0.1f, 0.12f);
    }

    /// <summary>
    /// Inside a column of air: gravity is a fraction of itself and so is the speed you fall at, so a
    /// jump goes far higher and a fall is a slow drift. Nothing lifts you. A fall already faster
    /// than the column allows is slowed to it over a moment, not snapped. A leaning column's relief
    /// scales with how upright it is.
    /// </summary>
    private void UpdraftEase(Updraft draft, ref float gravMult, ref float fallCap, ref Vector2 v, float dt)
    {
        // (the relief from gravity is the column's upward share: all of it standing straight up, none lying flat;
        // its sideways share is the push, in Platform)
        float k = draft.Strength * Math.Max(0f, -draft.Up.Y);
        // (a column pointing down is a downdraft: it drives you down it, as the upward one eases you up)
        float down = draft.Strength * Math.Max(0f, draft.Up.Y);
        gravMult = Mathf.Lerp(Mathf.Lerp(1f, Tune.Elementalist.DowndraftGravityMult, down), Tune.Elementalist.UpdraftGravityMult, k);
        fallCap = MaxFall * Mathf.Lerp(Mathf.Lerp(1f, Tune.Elementalist.DowndraftFallMult, down), Tune.Elementalist.UpdraftFallMult, k);
        if (v.Y > fallCap)
        {
            v.Y = Mathf.MoveToward(v.Y, fallCap, 2400f * dt);
            fallCap = Math.Max(fallCap, v.Y);
        }
    }

    // ---------------------------------------------------------------- blizzard

    private bool TryBlizzard(Vector2 aim, float aimDist)
    {
        if (!IsElementalist || !AbilityChargeReady) return false;
        SpendAbilityCharge();
        // (free to call down; the use that was spent waits for the alimus before it begins to come back)
        if (_stormOwes.Length != _abilityCd.Length) Array.Resize(ref _stormOwes, _abilityCd.Length);
        for (int k = 0; k < _abilityCd.Length; k++) if (_abilityCd[k] > 0 && !_stormOwes[k] && _abilityCd[k] >= AbilityRecharge - 0.001f) { _stormOwes[k] = true; break; }
        aim = aim.LengthSquared() > 0.01f ? aim.Normalized() : new Vector2(Facing, 0);
        var at0 = BlizzardSpot(aim, aimDist);
        var dir0 = (at0 - CastPoint).Normalized();
        if (Math.Abs(dir0.X) > 0.15f) Facing = Math.Sign(dir0.X);
        CastDir = dir0;
        Anim.Face((int)Facing, instant: true);
        bool fire = Stats.Firestorm;
        LastCast = fire ? "firestorm" : "blizzard";
        // (the storm is gathered first: the Elementalist holds still, the staff raised, for the half second it takes to call it down)
        Hold(Tune.Elementalist.BlizzardDelay, "heal", () =>
        {
        if (Dead) return;
        var at = BlizzardSpot(aim, aimDist);
        float secondsMult = Stats.BlizzardSecondsMult;
        var storm = new Blizzard
        {
            Position = at,
            Radius = Tune.Elementalist.BlizzardRadius * Stats.BlizzardWideMult,
            Seconds = Tune.Elementalist.BlizzardSeconds * secondsMult,
            Ticks = Mathf.RoundToInt(Tune.Elementalist.BlizzardTicks * secondsMult),
            Fire = fire,
            Damage = (fire ? Tune.Elementalist.FirestormDamage : Tune.Elementalist.BlizzardDamage) * Stats.DamageMult,
            FreezeChance = fire || Stats.NoFreeze ? 0f : Tune.Elementalist.BlizzardFreeze + Stats.FreezeBonus,
            // (Kindling's extra chance lights a Firestorm too)
            IgniteChance = fire ? Tune.Elementalist.FirestormIgnite + (Stats.IgniteChance - Tune.Elementalist.IgniteChance) : 0f,
            IgniteDps = Tune.Elementalist.IgniteDps * Stats.DamageMult * Stats.BurnMult, IgniteSeconds = Tune.Elementalist.IgniteSeconds,
            Caster = this,
        };
        G.Spawn(storm);
        NetSync.HeroVisual(storm);
        LastBlizzard = storm;
        _castGlow = 1f;
        G.Fx.Beam(CastPoint, at, fire ? new Color(1f, 0.55f, 0.2f, 0.6f) : new Color(0.8f, 0.94f, 1f, 0.6f));
        G.Sfx.Play(fire ? "lava" : "gasp", at, -4, 0.1f, fire ? 0.8f : 0.5f);
        });
        return true;
    }

    /// <summary>
    /// Where the storm is called down: on the creature nearest your aim within reach; else where
    /// the mouse points (up to the reach) or, on a controller, a good way along your aim. A spot
    /// in the open air settles down onto the ground just below it.
    /// </summary>
    private Vector2 BlizzardSpot(Vector2 aim, float aimDist)
    {
        float reach = Tune.Elementalist.BlizzardRange;
        var target = FindSpellTarget(aim, reach, 30f);
        if (target != null) return target.GlobalPosition;
        float d = aimDist > 1f ? Math.Min(aimDist, reach) : reach * 0.6f;
        // (never through rock: it stops where the way is blocked)
        var from = CastPoint;
        var at = from;
        for (float t = 8f; t <= d; t += 8f)
        {
            var p = from + aim * t;
            if (G.Cave.IsSolid(p)) break;
            at = p;
        }
        if (G.Cave.FindFloor(at, 48f, out var floor)) at = floor - new Vector2(0, Tune.Elementalist.BlizzardRadius * 0.7f);
        return at;
    }

    /// <summary>One strike of this hero's storm on a creature: frost that may freeze it, or fire that may set it alight.</summary>
    public void StormStruck(Blizzard storm, Enemy e)
    {
        if (!IsInstanceValid(e) || e.Dead) return;
        if (storm.Elemental) { ElementStormStruck(storm, e); return; }
        var from = (e.GlobalPosition - storm.GlobalPosition).Normalized();
        float dealt = e.Hurt(storm.Damage, from * 10f, e.GlobalPosition - from * e.HitRadius * 0.5f, storm.Fire ? DamageKind.Fire : DamageKind.Frost);
        if (dealt > 0) OnDealtDamage(dealt);
        if (e.Dead) return;
        if (storm.Fire) { if (G.Chance(storm.IgniteChance)) e.Ignite(storm.IgniteDps, storm.IgniteSeconds); }
        else
        {
            e.Chill(Tune.Elementalist.ChillSlow * 0.5f, 0.5f);
            if (G.Chance(storm.FreezeChance)) e.FreezeSolid(Tune.Elementalist.FreezeSeconds);
        }
    }

    // ---------------------------------------------------------------- snap

    /// <summary>What a snap bursts: every frozen creature in view, and every burning one.</summary>
    private IEnumerable<Enemy> Snappable()
    {
        var me = GlobalPosition;
        foreach (var e in G.Enemies)
        {
            if (e.Dead || !e.CanBeHit) continue;
            var d = e.GlobalPosition - me;
            if (Math.Abs(d.X) > Tune.Elementalist.SnapViewX || Math.Abs(d.Y) > Tune.Elementalist.SnapViewY) continue;
            if (e.FrozenSolid || e.Ignited) yield return e;
        }
    }

    private bool TrySnap()
    {
        if (!IsElementalist || !SnapReady) return false;
        var marked = Snappable().ToList();
        if (marked.Count == 0) { _snapCd = 0.3f; SayNo("NOTHING FROZEN OR BURNING"); return true; }
        _snapCd = SnapCooldown;
        _snapOwes = true;
        _snapping = marked;
        // the fist closes (the clip's clench): for a beat the hero and everything marked are held still, and then it all bursts
        _snapAt = Tune.Elementalist.SnapFreeze;
        foreach (var m in marked) m.Freeze(Tune.Elementalist.SnapFreeze + 0.05f, hold: true);
        var dir = (marked[0].GlobalPosition - CastPoint).Normalized();
        if (Math.Abs(dir.X) > 0.15f) Facing = Math.Sign(dir.X);
        CastDir = dir;
        Anim.Face((int)Facing, instant: true);
        Hold(Tune.Elementalist.SnapFreeze, "rupture", () => { });
        LastCast = marked.Any(e => !e.FrozenSolid) ? "cinder" : "snap";
        _castGlow = 1f;
        foreach (var e in marked) G.Fx.Converge(e.GlobalPosition, e.HitRadius + 14, e.FrozenSolid ? ElementBolt.FrostColor : ElementBolt.FireColor, 6, 0.12f);
        return true;
    }

    /// <summary>The snap lands: each marked creature bursts, and the burst splashes whatever stands near it.</summary>
    private void BurstSnap()
    {
        _snapAt = -1;
        var marked = _snapping ?? new List<Enemy>();
        _snapping = null;
        float radius = Tune.Elementalist.SnapRadius * Stats.SnapWideMult;
        int burst = 0;
        foreach (var e in marked)
        {
            if (!IsInstanceValid(e) || e.Dead || !e.CanBeHit) continue;
            burst++;
            // (ice shatters; a creature burning but not frozen bursts in cinders)
            bool cinder = !e.FrozenSolid;
            float dmg = (cinder ? Tune.Elementalist.CinderDamage : Tune.Elementalist.SnapDamage) * Tune.Elementalist.SnapMult * Stats.DamageMult;
            float splash = (cinder ? Tune.Elementalist.CinderSplash : Tune.Elementalist.SnapSplash) * Tune.Elementalist.SnapMult * Stats.DamageMult;
            BurstCreature(e, dmg, splash, radius, cinder, false);
        }
        LastSnapCount = burst;
        if (this == G.Player) Achievements.Snapped(burst);
        if (burst > 0)
        {
            G.Fx.AddShake(2f + burst);
            G.Main.Rumble(0.4f, 0.4f, 0.15f);
            if (Stats.SnapEcho) GainAlimus(Tune.Elementalist.EchoAlimus * burst);
        }
    }
}
