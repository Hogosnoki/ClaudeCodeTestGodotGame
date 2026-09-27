using System;
using System.Collections.Generic;
using Godot;

namespace DaggerCave;

/// <summary>
/// The Vitalist's kit (a vitality manipulator, with no blade). The attack button casts a drain
/// bolt: a single-target strike at medium range that homes on the creature you aim at and hands
/// back a share of its damage as alimus. The dodge button casts a hex: creatures around you slow
/// down and take more damage for a while. The ability button spends alimus on a heal, shared
/// among everyone nearby who is hurt, by how hurt each of them is.
/// </summary>
public partial class Player
{
    /// <summary>The Vitalist's reserve (like mana): earned by dealing damage, spent on heals.</summary>
    public float Alimus { get; private set; }
    private float _boltCd, _hexCd, _healCd, _castGlow;

    /// <summary>What a heal costs now.</summary>
    public float HealCost => Tune.Vitalist.HealCost * Stats.HealCostMult;
    public float HexCooldownFrac => Math.Clamp(_hexCd / Math.Max(0.01f, Stats.HexCooldown), 0, 1);
    public float HealCooldownFrac => Math.Clamp(_healCd / Tune.Vitalist.HealCooldown, 0, 1);
    /// <summary>For the 3D model: 1 the moment a spell leaves the hands, fading to 0.</summary>
    public float CastGlow => _castGlow;
    /// <summary>For the 3D model: the last spell cast ("bolt", "hex" or "heal").</summary>
    public string LastCast { get; private set; } = "";
    /// <summary>For the 3D model: where the last drain bolt was aimed.</summary>
    public Vector2 CastDir { get; private set; } = Vector2.Right;

    private float BoltRange => Tune.Vitalist.BoltRange * Stats.DaggerReach;
    /// <summary>Where spells leave from: the crystal atop the staff.</summary>
    private Vector2 CastPoint => GlobalPosition + new Vector2(Facing * 7, -12);

    public void GainAlimus(float amount)
    {
        if (!IsVitalist || amount <= 0) return;
        Alimus = Math.Min(Stats.AlimusMax, Alimus + amount);
    }

    /// <summary>Test harness and level changes: set the reserve directly.</summary>
    public void SetAlimus(float value) => Alimus = Math.Clamp(value, 0, Stats.AlimusMax);

    private void TickVitalist(float dt)
    {
        _castGlow = Math.Max(0f, _castGlow - dt * 2.5f);
        if (Alimus > Stats.AlimusMax) Alimus = Stats.AlimusMax;
    }

    private bool CastBolt(Vector2 aim)
    {
        if (_boltCd > 0) return false;
        _boltCd = Tune.Vitalist.BoltCooldown / Math.Max(0.2f, Stats.AttackSpeed);
        aim = aim.LengthSquared() > 0.01f ? aim.Normalized() : new Vector2(Facing, 0);
        var target = FindBoltTarget(aim);
        if (Math.Abs(aim.X) > 0.15f) Facing = Math.Sign(aim.X);
        var from = CastPoint;
        var dir = target != null ? (target.GlobalPosition - from).Normalized() : aim;
        if (Math.Abs(dir.X) > 0.15f) Facing = Math.Sign(dir.X);
        from = CastPoint;
        CastDir = dir;
        Anim.Face((int)Facing, instant: true);
        Anim.Once("cast", 3, 1.6f);
        G.Spawn(new DrainBolt
        {
            Position = from, Dir = dir, Target = target, Range = BoltRange, Leaps = Stats.BoltLeaps,
            Damage = Tune.Vitalist.BoltDamage * Stats.DamageMult * G.Range(0.92f, 1.08f),
        });
        G.Sfx.Play("throw", from, -6, 0.1f, 1.5f);
        G.Sfx.Play("bubble", from, -12, 0.1f, 1.8f);
        G.Fx.Flash(from, 7, new Color(0.55f, 1f, 0.5f), 0.08f);
        _castGlow = 1f;
        LastCast = "bolt";
        return true;
    }

    /// <summary>The creature a bolt should seek: in the cone of your aim, in range and in sight, nearest the line of aim.</summary>
    private Enemy FindBoltTarget(Vector2 aim)
    {
        Enemy best = null;
        float bestScore = float.MaxValue, cone = Mathf.DegToRad(Tune.Vitalist.BoltConeDegrees);
        var origin = CastPoint;
        foreach (var e in G.Enemies)
        {
            if (e.Dead || !e.CanBeHit) continue;
            var to = e.GlobalPosition - origin;
            float d = to.Length();
            if (d > BoltRange + e.HitRadius) continue;
            float ang = Math.Abs(aim.AngleTo(to));
            if (d > 12 && ang > cone + MathF.Atan2(e.HitRadius, d)) continue;
            if (!G.Cave.LineClear(origin, e.GlobalPosition)) continue;
            float score = d * (1f + ang * 1.5f);
            if (score < bestScore) { bestScore = score; best = e; }
        }
        return best;
    }

    private bool TryHex()
    {
        if (!IsVitalist || _hexCd > 0) return false;
        _hexCd = Stats.HexCooldown;
        float r = Tune.Vitalist.HexRadius * Stats.HexRadiusMult;
        var c = GlobalPosition + new Vector2(0, -6);
        foreach (var e in G.Enemies)
        {
            if (e.Dead || e.GlobalPosition.DistanceTo(c) > r + e.HitRadius) continue;
            e.Hex(Tune.Vitalist.HexVulnerability, Tune.Vitalist.HexSlow, Stats.HexSeconds, Stats.HexRot * Stats.DamageMult);
            G.Fx.Burst(e.GlobalPosition, new Color(0.55f, 1f, 0.4f, 0.9f), 6, 60, 1.8f, 0.6f, -60);
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

    /// <summary>
    /// Shares HealAmount among everyone in range who is hurt, by the share of their health each
    /// is missing: with sumP the sum of those shares, each gets amount x (their share / sumP).
    /// </summary>
    private bool TryHeal()
    {
        if (!IsVitalist || _healCd > 0) return false;
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
        if (Alimus < cost) return Refuse("NOT ENOUGH ALIMUS");
        Alimus -= cost;
        _healCd = Tune.Vitalist.HealCooldown;
        float amount = Tune.Vitalist.HealAmount * Stats.HealMult;
        var from = CastPoint;
        foreach (var (p, miss) in hurt)
        {
            p.Heal(amount * miss / sumP);
            var at = p.GlobalPosition + new Vector2(0, -6);
            if (p != this) G.Fx.Beam(from, at, new Color(0.55f, 1f, 0.5f));
            G.Fx.Flash(at, 20, new Color(0.5f, 1f, 0.55f), 0.18f);
            G.Fx.Ring(at, 18, new Color(0.6f, 1f, 0.55f, 0.9f));
            for (int k = 0; k < 10; k++) G.Fx.Ember(p.GlobalPosition + new Vector2(G.Range(-9, 9), G.Range(-4, 12)), new Color(0.55f, 1f, 0.5f));
            p.Anim.Flash(0.4f);
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
        _healCd = 0.5f;
        G.Fx.Text(GlobalPosition + new Vector2(0, -28), why, new Color(0.75f, 0.85f, 0.7f), 9, 0.8f);
        G.Sfx.Play("clink", GlobalPosition, -14, 0.05f, 0.5f);
        return true;
    }
}
