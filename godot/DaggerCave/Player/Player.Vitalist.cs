using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

/// <summary>
/// The Vitalist's kit (a vitality manipulator, with no blade). The attack button drains: the
/// creature you aim at is struck the instant you cast (no travel time), and the life torn out of
/// it flies back to you as a mote that becomes vital force when it arrives. The dodge button casts a
/// hex: creatures around you slow down and take more damage for a while. The ability button
/// spends vital force on a heal, shared among everyone nearby who is hurt, by how hurt each of them
/// is. The second ability spends a full reserve on a rupture: the creature you aim at is seized
/// where it stands and bursts, splashing everything around it. Its alterations: Blight Burst (the
/// hex strikes too) or Endless Hex (vital force instead of a cooldown), Slow Mending (heals over time)
/// and Lifebloom (the rupture blooms on a friend, healing).
/// </summary>
public partial class Player
{
    /// <summary>The Vitalist's reserve (like mana): earned by draining life, spent on heals and ruptures.</summary>
    public float VitalForce { get; private set; }
    private float _drainCd, _hexCd, _healCd, _ruptureCd, _castGlow;

    /// <summary>Healing's colour, everywhere (the potion's pink).</summary>
    public static readonly Color HealColor = new(1f, 0.5f, 0.66f), HealColorLight = new(1f, 0.75f, 0.84f);
    /// <summary>Life torn out of a creature (the drain, the rupture).</summary>
    public static readonly Color LifeColor = new(0.25f, 0.95f, 0.32f), LifeColorLight = new(0.78f, 1f, 0.7f);

    /// <summary>What a heal costs now.</summary>
    public float HealCost => Tune.Vitalist.HealCost * Stats.HealCostMult;
    /// <summary>What a rupture costs now.</summary>
    public float RuptureCost => Tune.Vitalist.RuptureCost * Stats.RuptureCostMult;
    public float HexCooldownFrac => Math.Clamp(_hexCd / Math.Max(0.01f, Stats.HexCooldown), 0, 1);
    public float HealCooldownFrac => AbilityCooldownFrac;
    public float RuptureCooldownFrac => Math.Clamp(_ruptureCd / Tune.Vitalist.RuptureCooldown, 0, 1);
    /// <summary>A rupture could be cast now (vital force and cooldown allowing).</summary>
    public bool RuptureReady => _ruptureCd <= 0 && _ruptureT < 0 && VitalForce >= RuptureCost - 0.001f;
    /// <summary>For the 3D model: 1 the moment a spell leaves the hands, fading to 0.</summary>
    public float CastGlow => _castGlow;
    /// <summary>For the 3D model: the last spell cast ("drain", "hex", "heal" or "rupture").</summary>
    public string LastCast { get; private set; } = "";
    /// <summary>For the 3D model: where the last spell was aimed.</summary>
    public Vector2 CastDir { get; private set; } = Vector2.Right;

    private float DrainRange => Tune.Vitalist.DrainRange * Stats.DaggerReach;
    /// <summary>Where spells leave from, and stolen life returns to: the crystal atop the staff.</summary>
    public Vector2 CastPoint => GlobalPosition + new Vector2(Facing * 7, -12);

    public void GainVitalForce(float amount)
    {
        if (!IsVitalist || amount <= 0) return;
        VitalForce = Math.Min(Stats.VitalForceMax, VitalForce + amount);
    }

    /// <summary>Test harness and level changes: set the reserve directly.</summary>
    public void SetVitalForce(float value) => VitalForce = Math.Clamp(value, 0, Stats.VitalForceMax);

    private void TickVitalist(float dt)
    {
        _castGlow = Math.Max(0f, _castGlow - dt * 2.5f);
        if (VitalForce > Stats.VitalForceMax) VitalForce = Stats.VitalForceMax;
        if (_drainTarget != null)
        {
            _drainAt -= dt;
            if (_drainAt <= 0) StrikeDrain();
        }
        TickRupture(dt);
    }

    // ---------------------------------------------------------------- drain

    private bool CastDrain(Vector2 aim, bool held = false)
    {
        if (_drainCd > 0) return false;
        aim = aim.LengthSquared() > 0.01f ? aim.Normalized() : new Vector2(Facing, 0);
        var target = FindSpellTarget(aim, DrainRange);
        // held down, it drains whatever comes into reach (without casting wisps at nothing)
        if (target == null && held) return false;
        var dir = target != null ? (target.GlobalPosition - CastPoint).Normalized() : aim;
        if (Math.Abs(dir.X) > 0.15f) Facing = Math.Sign(dir.X);
        CastDir = dir;
        Anim.Face((int)Facing, instant: true);
        Anim.Once("cast", 3, 1.6f);
        LastCast = "drain";
        if (target == null)
        {
            // nothing within reach to drain: a wisp goes out and comes to nothing
            _drainCd = Tune.Vitalist.DrainWhiffCooldown / Math.Max(0.2f, Stats.AttackSpeed);
            G.Fx.Directional(CastPoint, dir, 0.35f, new Color(LifeColor, 0.5f), 5, 110, 1.4f, 0.25f, 0, 0);
            G.Sfx.Play("throw", CastPoint, -14, 0.1f, 1.7f);
            _castGlow = 0.4f;
            return true;
        }
        _drainCd = Tune.Vitalist.DrainCooldown / Math.Max(0.2f, Stats.AttackSpeed);
        AttacksStarted++;
        // the strike lands as the staff comes forward (the cast's thrust), not while it's drawn back
        _drainTarget = target;
        _drainDmg = Tune.Vitalist.DrainDamage * Stats.DamageMult * Stats.PrimaryDamageMult * G.Range(0.92f, 1.08f);
        _drainAt = Tune.Vitalist.DrainStrikeDelay;
        _castGlow = 0.45f;
        return true;
    }

    private Enemy _drainTarget;
    private float _drainDmg, _drainAt;

    /// <summary>
    /// The drain's strike, on the cast's thrust: the crystal flares, a tether of life snaps out to
    /// the creature and it bursts (and with Many Mouths, the ones beside it too).
    /// </summary>
    private void StrikeDrain()
    {
        var target = _drainTarget;
        _drainTarget = null;
        if (Dead || !IsInstanceValid(target) || target.Dead || !target.CanBeHit) return;
        float dmg = _drainDmg;
        // a green glint on the staff, at the very moment the creature bursts
        G.Fx.Flash(CastPoint, 12, LifeColorLight, 0.14f);
        G.Fx.Spark(CastPoint, Vector2.Right, false, LifeColorLight);
        G.Fx.Burst(CastPoint, LifeColorLight, 7, 70, 1.6f, 0.3f, -30f);
        G.Fx.Beam(CastPoint, target.GlobalPosition, new Color(LifeColor, 0.9f));
        DrainFrom(target, dmg, 1f);
        // Many Mouths: the creatures nearest the target give up their life too
        if (Stats.DrainExtra > 0)
        {
            var near = G.Enemies
                .Where(e => e != target && !e.Dead && e.CanBeHit && e.GlobalPosition.DistanceTo(target.GlobalPosition) < Tune.Vitalist.MultiRadius + e.HitRadius
                            && InSight(target.GlobalPosition, e))
                .OrderBy(e => e.GlobalPosition.DistanceSquaredTo(target.GlobalPosition))
                .Take(Stats.DrainExtra).ToList();
            foreach (var e in near)
            {
                G.Fx.Beam(target.GlobalPosition, e.GlobalPosition, new Color(LifeColor, 0.7f));
                DrainFrom(e, dmg * Tune.Vitalist.MultiShare, 0.7f);
            }
        }
        G.Sfx.Play("drain", target.GlobalPosition, -3, 0.1f);
        _castGlow = 1f;
    }

    /// <summary>
    /// Tears the life out of one creature: it's struck on the spot (tugged toward you, shuddering),
    /// bursting in a spray of crimson, and what it lost flies back to you as a mote carrying the vital force.
    /// </summary>
    private void DrainFrom(Enemy e, float dmg, float size)
    {
        var toMe = (CastPoint - e.GlobalPosition).Normalized();
        var at = e.GlobalPosition + toMe * e.HitRadius * 0.5f;
        float dealt = e.Hurt(dmg, toMe * 40f, at, DamageKind.Nature);
        if (dealt <= 0)
        {
            G.Sfx.Play("clink", at, -6);
            G.Fx.Spark(at, toMe, false, new Color(0.8f, 0.8f, 0.85f));
            return;
        }
        OnDealtDamage(dealt, vitalForceByMote: true);
        if (!e.Dead) e.Freeze(Tune.Feel.HitStopBolt);
        // the burst: a flare and a tear of light, a spray of crimson, a ring racing out, and the
        // life streaming out of it toward you
        // (bright and big: a green flare, a burst of it, a ring racing out, and the life torn out toward you)
        G.Fx.Flash(at, 20 * size + 8, LifeColorLight, 0.16f);
        G.Fx.Flash(at, 12 * size + 5, LifeColor, 0.22f);
        G.Fx.Spark(at, -toMe, true, LifeColorLight);
        G.Fx.Burst(at, LifeColor, (int)(20 * size), 190, 2.6f, 0.4f, 60f, 0, 3f);
        G.Fx.Directional(at, toMe, 0.6f, LifeColorLight, (int)(18 * size), 260, 2.4f, 0.36f, 0, 0);
        G.Fx.Ring(at, 14 + 16 * size, new Color(LifeColor, 0.95f), 0.3f);
        G.Main.Rumble(0.25f, 0.08f, 0.08f);
        var mote = new LifeMote { Position = at, Caster = this, VitalForce = dealt * Stats.VitalForceGain, Size = size };
        G.Spawn(mote);
        NetSync.HeroVisual(mote);
    }

    /// <summary>Stolen life reaching the staff: it becomes vital force.</summary>
    public void AbsorbMote(float vitalForce, float size)
    {
        if (!IsRemote) GainVitalForce(vitalForce);
        _castGlow = Math.Max(_castGlow, 0.35f * size);
        G.Fx.Flash(CastPoint, 5 + 4 * size, LifeColorLight, 0.08f);
        if (G.Chance(0.5f)) G.Sfx.Play("bubble", CastPoint, -16, 0.2f, 0.7f);
    }

    /// <summary>The creature a spell should seize: in the cone of your aim (<paramref name="coneDegrees"/>
    /// either side; the drain's by default), in range and in sight, nearest the line of aim.</summary>
    private Enemy FindSpellTarget(Vector2 aim, float range, float coneDegrees = -1f)
    {
        Enemy best = null;
        float bestScore = float.MaxValue, cone = Mathf.DegToRad(coneDegrees > 0 ? coneDegrees : Tune.Vitalist.DrainConeDegrees);
        var origin = CastPoint;
        foreach (var e in G.Enemies)
        {
            if (e.Dead || !e.CanBeHit) continue;
            var to = e.GlobalPosition - origin;
            float d = to.Length();
            if (d > range + e.HitRadius) continue;
            float ang = Math.Abs(aim.AngleTo(to));
            if (d > 12 && ang > cone + MathF.Atan2(e.HitRadius, d)) continue;
            if (!InSight(origin, e)) continue;
            float score = d * (1f + ang * 1.5f);
            if (score < bestScore) { bestScore = score; best = e; }
        }
        return best;
    }

    /// <summary>
    /// Whether a spell from <paramref name="from"/> can reach a creature: a clear line to its
    /// middle, or to the top of its body (a creature a step lower, or behind a lip of rock, shows
    /// its head over it), or over the top from a little higher up.
    /// </summary>
    private static bool InSight(Vector2 from, Enemy e)
    {
        var c = e.GlobalPosition;
        var top = c - new Vector2(0, e.HitRadius * 0.8f);
        var cave = G.Cave;
        return cave.LineClear(from, c) || cave.LineClear(from, top) || (!cave.IsSolid(from - new Vector2(0, 10)) && cave.LineClear(from - new Vector2(0, 10), top));
    }

    /// <summary>
    /// Whether a burst at <paramref name="from"/> spreads to <paramref name="to"/> through open
    /// space, going no further than <paramref name="reach"/> px (so it rounds a lip of rock or
    /// drops down a step, but never goes through a wall).
    /// </summary>
    private static bool Spreads(Vector2 from, Vector2 to, float reach)
    {
        var cave = G.Cave;
        const float C = CaveData.Cell;
        int si = (int)(from.X / C), sj = (int)(from.Y / C), ti = (int)(to.X / C), tj = (int)(to.Y / C);
        int steps = (int)MathF.Ceiling(reach / C) + 1;
        var seen = new HashSet<(int, int)>();
        var q = new Queue<(int i, int j, int d)>();
        if (cave.CellOpen(si, sj)) { q.Enqueue((si, sj, 0)); seen.Add((si, sj)); }
        while (q.Count > 0)
        {
            var (i, j, d) = q.Dequeue();
            if (Math.Abs(i - ti) <= 0 && Math.Abs(j - tj) <= 1) return true;
            if (d >= steps) continue;
            for (int dj = -1; dj <= 1; dj++)
                for (int di = -1; di <= 1; di++)
                {
                    if (di == 0 && dj == 0) continue;
                    int a = i + di, b = j + dj;
                    if (a < 0 || b < 0 || a >= cave.W || b >= cave.H || !cave.CellOpen(a, b) || !seen.Add((a, b))) continue;
                    // (no squeezing diagonally between two rock corners)
                    if (di != 0 && dj != 0 && !cave.CellOpen(i + di, j) && !cave.CellOpen(i, j + dj)) continue;
                    q.Enqueue((a, b, d + 1));
                }
        }
        return false;
    }

    // ---------------------------------------------------------------- hex

    private bool TryHex()
    {
        if (!IsVitalist || _hexCd > 0) return false;
        if (Stats.EndlessHex)
        {
            // Endless Hex: vital force instead of a cooldown (a beat between casts all the same)
            if (VitalForce < Tune.Vitalist.EndlessHexCost - 0.001f) { _hexCd = 0.4f; SayNo("NOT ENOUGH VITAL FORCE"); return true; }
            VitalForce = Math.Max(0, VitalForce - Tune.Vitalist.EndlessHexCost);
            _hexCd = 0.35f;
        }
        else _hexCd = Stats.HexCooldown;
        float r = Tune.Vitalist.HexRadius * Stats.HexRadiusMult;
        var c = GlobalPosition + new Vector2(0, -6);
        // Blight Burst: it strikes as it spreads, but slows and weakens half as much
        float weaker = Stats.BlightBurst ? Tune.Vitalist.BlightWeaker : 0f;
        float vuln = 1f + (Tune.Vitalist.HexVulnerability - 1f) * (1f - weaker);
        float slow = 1f - (1f - Tune.Vitalist.HexSlow) * (1f - weaker);
        foreach (var e in G.Enemies.ToArray())
        {
            if (e.Dead || e.GlobalPosition.DistanceTo(c) > r + e.HitRadius) continue;
            e.Hex(vuln, slow, Stats.HexSeconds, Stats.HexRot * Stats.DamageMult);
            G.Fx.Burst(e.GlobalPosition, new Color(0.55f, 1f, 0.4f, 0.9f), 6, 60, 1.8f, 0.6f, -60);
            if (Stats.BlightBurst && !e.Dead)
            {
                var away = (e.GlobalPosition - c).LengthSquared() > 1 ? (e.GlobalPosition - c).Normalized() : Vector2.Up;
                float dealt = e.Hurt(Tune.Vitalist.BlightDamage * Stats.DamageMult, away * 90f, e.GlobalPosition - away * e.HitRadius, DamageKind.Nature);
                if (dealt > 0) OnDealtDamage(dealt);
                G.Fx.Burst(e.GlobalPosition, new Color(0.35f, 0.8f, 0.2f), 8, 140, 2f, 0.4f);
            }
        }
        var col = new Color(0.5f, 1f, 0.4f);
        G.Fx.Shockwave(GlobalPosition + new Vector2(0, 12), r, new Color(col, 0.8f), 0.45f);
        G.Fx.Ring(c, r * 0.9f, new Color(col, 0.7f), 0.5f);
        G.Fx.Flash(c, 16, new Color(0.6f, 1f, 0.5f), 0.12f);
        for (int k = 0; k < 14; k++) G.Fx.Ember(c + G.RandDir() * G.Range(10, r), new Color(0.55f, 1f, 0.45f));
        G.Sfx.Play("gasp", c, -4, 0.1f, 0.6f);
        G.Sfx.Play("roar", c, -16, 0.1f, 2.2f);
        G.Main.Rumble(0.3f, 0.2f, 0.15f);
        Anim.Once("hex", 3);
        _castGlow = 1f;
        LastCast = "hex";
        return true;
    }

    // ---------------------------------------------------------------- heal

    /// <summary>
    /// Shares HealAmount among everyone in range who is hurt, by the share of their health each
    /// is missing: with sumP the sum of those shares, each gets amount x (their share / sumP).
    /// </summary>
    private bool TryHeal()
    {
        if (!IsVitalist || _healCd > 0 || !AbilityChargeReady) return false;
        var hurt = new List<(Player who, float miss)>();
        float sumP = 0;
        foreach (var p in G.Players)
        {
            if (p.Dead || p.GlobalPosition.DistanceTo(GlobalPosition) > Tune.Vitalist.HealRange) continue;
            float miss = (p.Stats.MaxHp - p.Hp) / Math.Max(1f, p.Stats.MaxHp);
            if (miss <= 0.001f) continue;
            hurt.Add((p, miss));
            sumP += miss;
        }
        if (hurt.Count == 0) return Refuse("NO ONE IS HURT");
        float cost = HealCost;
        if (VitalForce < cost - 0.001f) return Refuse("NOT ENOUGH VITAL FORCE");
        VitalForce = Math.Max(0, VitalForce - cost);
        SpendAbilityCharge();
        _healCd = 0.4f; // (with a second charge, not both in the same instant)
        float amount = Tune.Vitalist.HealAmount * Stats.HealMult;
        var from = CastPoint;
        foreach (var (p, miss) in hurt)
        {
            float share = amount * miss / sumP;
            var at = p.GlobalPosition + new Vector2(0, -6);
            if (p != this) G.Fx.Beam(from, at, HealColor);
            G.Fx.Flash(at, 20, HealColor, 0.18f);
            G.Fx.Ring(at, 18, new Color(HealColorLight, 0.9f));
            for (int k = 0; k < 10; k++) G.Fx.Ember(p.GlobalPosition + new Vector2(G.Range(-9, 9), G.Range(-4, 12)), HealColor);
            p.Anim.Flash(0.4f);
            p.Anim.FlashColor = HealColorLight;
            if (!Stats.SlowMending) { p.Heal(share); continue; }
            // Slow Mending: half now, half over the next seconds (Patient: all of it, and more)
            float later = Stats.PatientMending ? share * (1f + Tune.Vitalist.PatientBonus) : share * 0.5f;
            float now = Stats.PatientMending ? 0f : share * 0.5f;
            if (now > 0) p.Heal(now);
            p.GiveMending(later, Tune.Vitalist.MendSeconds, Stats.WardingMending);
        }
        G.Sfx.Play("heal", from, -2, 0.05f, 1.1f);
        Anim.Once("heal", 3);
        _castGlow = 1f;
        LastCast = "heal";
        return true;
    }

    /// <summary>A heal that can't be cast: say why (the press is spent, and it can't be spammed).</summary>
    private bool Refuse(string why)
    {
        _healCd = Math.Max(_healCd, 0.5f);
        SayNo(why);
        return true;
    }

    private void SayNo(string why)
    {
        G.Fx.Text(GlobalPosition + new Vector2(0, -28), why, new Color(0.85f, 0.8f, 0.8f), 9, 0.8f);
        G.Sfx.Play("clink", GlobalPosition, -14, 0.05f, 0.5f);
    }

    // ---------------------------------------------------------------- rupture

    private Enemy _ruptureTarget;
    private Vector2 _rupturePos, _ruptureDir;
    private float _ruptureT = -1, _ruptureGatherT;

    /// <summary>For the 3D model: 0..1 through the rupture's seizing (the hand closing), -1 when none.</summary>
    public float RuptureGrip => _ruptureT < 0 ? -1f : 1f - _ruptureT / Tune.Vitalist.RuptureWindup;

    private bool TryRupture(Vector2 aim)
    {
        if (!IsVitalist || _ruptureCd > 0 || _ruptureT >= 0) return false;
        if (VitalForce < RuptureCost - 0.001f) { _ruptureCd = 0.5f; SayNo("NOT ENOUGH VITAL FORCE"); return true; }
        aim = aim.LengthSquared() > 0.01f ? aim.Normalized() : new Vector2(Facing, 0);
        if (Stats.Lifebloom) return StartBloom(aim);
        var target = FindSpellTarget(aim, Tune.Vitalist.RuptureRange * Stats.DaggerReach);
        if (target == null) { _ruptureCd = 0.3f; SayNo("NOTHING TO RUPTURE"); return true; }
        VitalForce = Math.Max(0, VitalForce - RuptureCost);
        _ruptureCd = Tune.Vitalist.RuptureCooldown;
        _ruptureTarget = target;
        _rupturePos = target.GlobalPosition;
        _ruptureDir = (target.GlobalPosition - CastPoint).Normalized();
        _ruptureT = Tune.Vitalist.RuptureWindup;
        _ruptureGatherT = 0;
        if (Math.Abs(_ruptureDir.X) > 0.15f) Facing = Math.Sign(_ruptureDir.X);
        CastDir = _ruptureDir;
        Anim.Face((int)Facing, instant: true);
        Anim.Once("rupture", 3);
        // it begins at the creature: seized where it stands, its life drawn in to a point
        float r = target.HitRadius;
        target.Freeze(Tune.Vitalist.RuptureWindup, hold: true);
        G.Fx.Ring(target.GlobalPosition, r + 16, new Color(LifeColor, 0.9f), Tune.Vitalist.RuptureWindup + 0.05f);
        G.Fx.Converge(target.GlobalPosition, r + 30, LifeColor, 14, Tune.Vitalist.RuptureWindup);
        G.Fx.Flash(target.GlobalPosition, r + 6, new Color(0.5f, 0.05f, 0.1f), Tune.Vitalist.RuptureWindup);
        G.Sfx.Play("gasp", target.GlobalPosition, -2, 0.05f, 0.45f);
        G.Sfx.Play("drain", target.GlobalPosition, -6, 0.05f, 0.6f);
        _castGlow = 1f;
        LastCast = "rupture";
        return true;
    }

    private void TickRupture(float dt)
    {
        if (_ruptureT < 0) return;
        if (_bloomTarget != null && IsInstanceValid(_bloomTarget) && !_bloomTarget.Dead) _rupturePos = _bloomTarget.GlobalPosition;
        if (_ruptureTarget != null && IsInstanceValid(_ruptureTarget) && !_ruptureTarget.Dead) _rupturePos = _ruptureTarget.GlobalPosition;
        _ruptureGatherT -= dt;
        if (_ruptureGatherT <= 0)
        {
            _ruptureGatherT = 0.05f;
            G.Fx.Converge(_rupturePos, 26, LifeColorLight, 3, 0.15f);
        }
        _ruptureT -= dt;
        if (_ruptureT < 0) { if (_bloomTarget != null) BloomBurst(); else RuptureBurst(); }
    }

    // ---------------------------------------------------------------- Lifebloom

    private Player _bloomTarget;
    /// <summary>Blooms cast this run (for the tests).</summary>
    public int Blooms { get; private set; }

    /// <summary>
    /// Lifebloom: the rupture gathers on the friend nearest your aim (within its reach; alone,
    /// on you) and, a beat later, bursts into healing: the friend gets the most, everyone else
    /// in the burst a share.
    /// </summary>
    private bool StartBloom(Vector2 aim)
    {
        float range = Tune.Vitalist.RuptureRange * Stats.DaggerReach;
        Player best = null;
        float bestScore = float.MaxValue;
        foreach (var p in G.Players)
        {
            if (p == this || p.Dead || !IsInstanceValid(p)) continue;
            var to = p.GlobalPosition - GlobalPosition;
            float d = to.Length();
            if (d > range) continue;
            float score = d * (1f + Math.Abs(aim.AngleTo(to)) * 1.5f);
            if (score < bestScore) { bestScore = score; best = p; }
        }
        var target = best ?? this;
        VitalForce = Math.Max(0, VitalForce - RuptureCost);
        _ruptureCd = Tune.Vitalist.RuptureCooldown;
        _bloomTarget = target;
        _ruptureTarget = null;
        _rupturePos = target.GlobalPosition;
        _ruptureDir = target == this ? new Vector2(Facing, 0) : (target.GlobalPosition - CastPoint).Normalized();
        _ruptureT = Tune.Vitalist.RuptureWindup;
        _ruptureGatherT = 0;
        if (Math.Abs(_ruptureDir.X) > 0.15f) Facing = Math.Sign(_ruptureDir.X);
        CastDir = _ruptureDir;
        Anim.Face((int)Facing, instant: true);
        Anim.Once("rupture", 3);
        G.Fx.Ring(target.GlobalPosition, 26, new Color(HealColor, 0.9f), Tune.Vitalist.RuptureWindup + 0.05f);
        G.Fx.Converge(target.GlobalPosition, 40, HealColorLight, 14, Tune.Vitalist.RuptureWindup);
        if (target != this) G.Fx.Beam(CastPoint, target.GlobalPosition + new Vector2(0, -6), new Color(HealColor, 0.8f));
        G.Sfx.Play("heal", target.GlobalPosition, -6, 0.05f, 0.7f);
        _castGlow = 1f;
        LastCast = "rupture";
        Blooms++;
        return true;
    }

    /// <summary>The bloom bursts: the friend it gathered on is healed most, everyone else in the burst a share.</summary>
    private void BloomBurst()
    {
        _ruptureT = -1;
        var at = _rupturePos;
        var main = _bloomTarget != null && IsInstanceValid(_bloomTarget) && !_bloomTarget.Dead ? _bloomTarget : null;
        _bloomTarget = null;
        float radius = Tune.Vitalist.RuptureRadius * Stats.RuptureRadiusMult;
        if (main != null) main.Heal(Tune.Vitalist.BloomHeal * Stats.HealMult);
        foreach (var p in G.Players)
        {
            if (p == main || p.Dead || !IsInstanceValid(p) || p.GlobalPosition.DistanceTo(at) > radius) continue;
            p.Heal(Tune.Vitalist.BloomSplash * Stats.RuptureSplashMult * Stats.HealMult);
        }
        // Healing Pool: it lingers where it burst (every game keeps one; each heals its own hero)
        if (Stats.BloomPool)
        {
            var pool = new HealingPool { Position = at, Radius = radius, Rate = Tune.Vitalist.PoolRate * Stats.HealMult, Life = Tune.Vitalist.PoolSeconds };
            G.Spawn(pool);
            NetSync.HeroVisual(pool);
        }
        G.Fx.Flash(at, 30, HealColorLight, 0.16f);
        G.Fx.Shockwave(at, radius, new Color(HealColor, 0.85f), 0.4f);
        G.Fx.Ring(at, radius * 0.85f, new Color(HealColorLight, 0.8f), 0.35f);
        G.Fx.Burst(at, HealColor, 24, 240, 2.4f, 0.55f, -60);
        for (int k = 0; k < 14; k++) G.Fx.Ember(at + G.RandDir() * G.Range(6, radius * 0.8f), HealColorLight);
        G.Main.Rumble(0.4f, 0.3f, 0.15f);
        G.Sfx.Play("heal", at, 0, 0.05f, 1.2f);
        _castGlow = 1f;
    }

    /// <summary>The seized creature bursts: full damage to it, a splash to everything around it.</summary>
    private void RuptureBurst()
    {
        _ruptureT = -1;
        var at = _rupturePos;
        var main = _ruptureTarget != null && IsInstanceValid(_ruptureTarget) && !_ruptureTarget.Dead ? _ruptureTarget : null;
        _ruptureTarget = null;
        float mult = Stats.DamageMult;
        if (main != null)
        {
            float dealt = main.Hurt(Tune.Vitalist.RuptureDamage * mult * G.Range(0.95f, 1.05f), _ruptureDir * 60f, at, DamageKind.Nature);
            if (dealt > 0)
            {
                OnDealtDamage(dealt, vitalForceByMote: true);
                for (int k = 0; k < 3; k++)
                {
                    var mote = new LifeMote { Position = at + G.RandDir() * 5, Caster = this, VitalForce = dealt * Stats.VitalForceGain / 3f, Size = 0.8f };
                    G.Spawn(mote);
                    NetSync.HeroVisual(mote);
                }
                if (!main.Dead) main.Freeze(Tune.Feel.HitStopCharged * 0.6f);
            }
        }
        float radius = Tune.Vitalist.RuptureRadius * Stats.RuptureRadiusMult;
        foreach (var e in G.Enemies.ToArray())
        {
            if (e == main || e.Dead || !e.CanBeHit) continue;
            var to = e.GlobalPosition - at;
            if (to.Length() > radius + e.HitRadius || !(InSight(at, e) || Spreads(at, e.GlobalPosition, radius + e.HitRadius))) continue;
            var away = to.LengthSquared() > 1 ? to.Normalized() : G.RandDir();
            float dealt = e.Hurt(Tune.Vitalist.RuptureSplash * Stats.RuptureSplashMult * mult, away * 170f, e.GlobalPosition - away * e.HitRadius, DamageKind.Nature);
            if (dealt <= 0) continue;
            OnDealtDamage(dealt, vitalForceByMote: true);
            var splash = new LifeMote { Position = e.GlobalPosition, Caster = this, VitalForce = dealt * Stats.VitalForceGain, Size = 0.55f };
            G.Spawn(splash);
            NetSync.HeroVisual(splash);
        }
        // a burst of crimson from inside it
        G.Fx.Flash(at, 30, LifeColorLight, 0.16f);
        G.Fx.Shockwave(at, radius, new Color(LifeColor, 0.85f), 0.4f);
        G.Fx.Ring(at, radius * 0.85f, new Color(LifeColor, 0.8f), 0.35f);
        G.Fx.Burst(at, LifeColor, 26, 280, 2.6f, 0.55f, 250);
        G.Fx.Burst(at, new Color(0.45f, 0.03f, 0.08f), 14, 170, 3.4f, 0.7f, 400);
        G.Fx.Spark(at, _ruptureDir, true, LifeColorLight);
        G.Fx.AddShake(4);
        G.Main.Kick(_ruptureDir * Tune.Feel.KickFinisher);
        G.Main.Rumble(0.6f, 0.6f, 0.2f);
        G.Sfx.Play("rupture", at, 0, 0.08f);
        _castGlow = 1f;
    }
}
