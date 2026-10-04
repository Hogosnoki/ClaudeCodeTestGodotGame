using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

/// <summary>
/// Every hero's support ability (the support button): one that does something for the others.
/// Swordsman: Battle Shout (everyone near hits harder). Warden: Taunt (every engaged creature turns on
/// her). Vitalist: Health Tap (health for vital force). Elementalist: Stalag-Might (the earth seizes a
/// creature). Rogue: Expose (a creature takes more from everyone). Aegis: Mending Mark (whoever next
/// strikes the marked creature is healed). Shape Shifter: Pack Howl (everyone near runs and strikes faster).
/// </summary>
public partial class Player
{
    /// <summary>Test aid: act as another hero for the support button.</summary>
    public HeroKind? TestHero;
    private HeroKind SupportHero => TestHero ?? Stats.Hero;
    private float _supportCd, _supportBuf;
    private Vector2 _supportAim;
    private float _tauntLeft;

    /// <summary>Seconds the support ability takes to come back.</summary>
    public float SupportRecharge => SupportHero switch
    {
        HeroKind.Swordsman => Tune.Support.ShoutCooldown,
        HeroKind.Warden => Tune.Support.TauntCooldown,
        HeroKind.Vitalist => Tune.Support.TapCooldown,
        HeroKind.Elementalist => Tune.Support.StalagCooldown,
        HeroKind.Rogue => Tune.Support.ExposeCooldown,
        HeroKind.Aegis => Tune.Support.MarkCooldown,
        _ => Tune.Support.HowlCooldown,
    } * Stats.AbilityCdMult;

    public string SupportName => SupportHero switch
    {
        HeroKind.Swordsman => "SHOUT",
        HeroKind.Warden => "TAUNT",
        HeroKind.Vitalist => "TAP",
        HeroKind.Elementalist => "STALAG",
        HeroKind.Rogue => "EXPOSE",
        HeroKind.Aegis => "MARK",
        _ => "HOWL",
    };

    public float SupportCooldownFrac => Math.Clamp(_supportCd / Math.Max(0.01f, SupportRecharge), 0f, 1f);
    public bool SupportReady => _supportCd <= 0;
    public bool Taunting => IsRemote ? (_netFlags & HfTaunt) != 0 : _tauntLeft > 0;
    public float TauntLeft => _tauntLeft;
    /// <summary>How far off the creatures count this hero to be: the hero's own reckoning, or next to nothing while taunting.</summary>
    public float EffectiveThreat => Taunting ? 0.001f : (Stats?.ThreatDist ?? 1f);

    private void TickSupport(float dt)
    {
        if (_supportCd > 0) _supportCd -= dt;
        _supportBuf -= dt;
        if (_tauntLeft > 0)
        {
            _tauntLeft -= dt;
            if (G.Chance(dt * 6f)) G.Fx.Ring(GlobalPosition + new Vector2(0, -8), 18, new Color(1f, 0.55f, 0.3f, 0.6f), 0.4f);
        }
        TickBuffs(dt);
    }

    /// <summary>Test aids: press the support button once with it ready; let buffs and a taunt run out.</summary>
    public bool TestSupport(Vector2 aim) { _supportCd = 0; return Support(aim); }
    public void TestExpire() { TickBuffs(999f); _tauntLeft = 0; }

    /// <summary>The support button.</summary>
    private bool Support(Vector2 aim)
    {
        if (_snagT > 0 || _supportCd > 0) return false;
        bool used = SupportHero switch
        {
            HeroKind.Swordsman => TryShout(),
            HeroKind.Warden => TryTaunt(),
            HeroKind.Vitalist => TryHealthTap(),
            HeroKind.Elementalist => TryStalag(aim),
            HeroKind.Rogue => TryExpose(aim),
            HeroKind.Aegis => TryMendMark(aim),
            _ => TryHowl(),
        };
        if (used) _supportCd = SupportRecharge;
        return used;
    }

    /// <summary>Every living hero within range of this one (this one too).</summary>
    private List<Player> HeroesNear(float range)
    {
        var list = new List<Player>();
        foreach (var h in G.Players)
            if (h != null && IsInstanceValid(h) && !h.Dead && (h == this || h.GlobalPosition.DistanceTo(GlobalPosition) <= range)) list.Add(h);
        return list;
    }

    // ---------------------------------------------------------------- timed buffs

    public const int BuffShout = 1, BuffHowl = 2;
    private readonly Dictionary<int, float> _buffs = new();
    public bool HasBuff(int kind) => _buffs.ContainsKey(kind);
    public float BuffLeft(int kind) => _buffs.TryGetValue(kind, out var t) ? t : 0f;

    /// <summary>A friend's call (or your own): for <paramref name="seconds"/>, hit harder (Shout) or move and strike faster (Howl).</summary>
    public void GiveBuff(int kind, float seconds)
    {
        if (Dead) return;
        if (IsRemote) { NetSync.BoonRemote(this, NetSync.Boon.Buff, seconds, 0, 0, kind); return; }
        if (!_buffs.ContainsKey(kind)) ApplyBuff(kind, +1);
        _buffs[kind] = Math.Max(BuffLeft(kind), seconds);
        G.Fx.Ring(GlobalPosition + new Vector2(0, -6), 20, kind == BuffShout ? new Color(1f, 0.6f, 0.3f, 0.8f) : new Color(0.85f, 0.95f, 1f, 0.8f), 0.4f);
    }

    private void ApplyBuff(int kind, int dir)
    {
        float m(float x) => dir > 0 ? x : 1f / x;
        if (kind == BuffShout) Stats.DamageMult *= m(Tune.Support.ShoutDamage);
        else if (kind == BuffHowl) { Stats.MoveSpeed *= m(Tune.Support.HowlMove); Stats.AttackSpeed *= m(Tune.Support.HowlAttack); }
    }

    private void TickBuffs(float dt)
    {
        if (_buffs.Count == 0) return;
        foreach (var kind in _buffs.Keys.ToList())
        {
            if ((_buffs[kind] -= dt) > 0) continue;
            _buffs.Remove(kind);
            ApplyBuff(kind, -1);
        }
    }

    // ---------------------------------------------------------------- the seven

    private bool TryShout()
    {
        foreach (var h in HeroesNear(Tune.Support.ShoutRange)) h.GiveBuff(BuffShout, Tune.Support.ShoutSeconds);
        var at = GlobalPosition + new Vector2(0, -8);
        G.Fx.Ring(at, Tune.Support.ShoutRange * 0.6f, new Color(1f, 0.6f, 0.3f, 0.7f), 0.5f);
        G.Fx.Shockwave(GlobalPosition + new Vector2(0, 12), 40, new Color(1f, 0.8f, 0.5f, 0.5f), 0.4f);
        G.Fx.Text(GlobalPosition + new Vector2(0, -34), "BATTLE SHOUT", new Color(1f, 0.75f, 0.4f), 12, 1f);
        G.Sfx.Play("roar", GlobalPosition, -2, 0.05f, 1.2f);
        Anim?.Once("cast", 3, 1.4f);
        return true;
    }

    private bool TryHowl()
    {
        foreach (var h in HeroesNear(Tune.Support.HowlRange)) h.GiveBuff(BuffHowl, Tune.Support.HowlSeconds);
        var at = GlobalPosition + new Vector2(0, -8);
        G.Fx.Ring(at, Tune.Support.HowlRange * 0.6f, new Color(0.85f, 0.95f, 1f, 0.7f), 0.5f);
        G.Fx.Text(GlobalPosition + new Vector2(0, -34), "PACK HOWL", new Color(0.9f, 0.95f, 1f), 12, 1f);
        G.Sfx.Play("roar", GlobalPosition, -2, 0.05f, 1.5f);
        return true;
    }

    private bool TryTaunt()
    {
        _tauntLeft = Tune.Support.TauntSeconds;
        var at = GlobalPosition + new Vector2(0, -8);
        G.Fx.Ring(at, 60, new Color(1f, 0.5f, 0.3f, 0.8f), 0.5f);
        G.Fx.Shockwave(GlobalPosition + new Vector2(0, 12), 50, new Color(1f, 0.6f, 0.4f, 0.5f), 0.4f);
        G.Fx.Text(GlobalPosition + new Vector2(0, -34), "TAUNT", new Color(1f, 0.6f, 0.35f), 12, 1f);
        G.Sfx.Play("roar", GlobalPosition, -1, 0.05f, 0.7f);
        // every creature near looks for its mark again, at once
        foreach (var e in G.Enemies) if (IsInstanceValid(e)) e.ForgetTarget();
        return true;
    }

    private bool TryHealthTap()
    {
        float cost = Stats.MaxHp * Tune.Support.TapCost;
        if (Hp <= cost + 1f) { SayNo("TOO WEAK"); _supportCd = 0.5f; return false; }
        Hp -= cost;
        GainVitalForce(Stats.VitalForceMax * Tune.Support.TapGain);
        var at = GlobalPosition + new Vector2(0, -8);
        G.Fx.Burst(at, new Color(0.8f, 0.1f, 0.15f), 14, 140, 2.2f, 0.4f, 60);
        G.Fx.Converge(at, 34, LifeColorLight, 12, 0.35f);
        G.Fx.Text(GlobalPosition + new Vector2(0, -22), "-" + Math.Max(1, (int)Math.Ceiling(cost)), new Color(0.85f, 0.15f, 0.2f), 13);
        G.Fx.Text(GlobalPosition + new Vector2(0, -36), "HEALTH TAP", LifeColorLight, 11, 0.9f);
        Anim?.Flash(0.6f);
        G.Sfx.Play("drain", GlobalPosition, -4, 0.05f, 0.8f);
        return true;
    }

    private bool TryStalag(Vector2 aim)
    {
        var foe = FindSpellTarget(aim.LengthSquared() > 0.01f ? aim.Normalized() : new Vector2(Facing, 0), Tune.Support.StalagRange, 40f);
        if (foe == null) { SayNo("NOTHING TO SEIZE"); _supportCd = 0.5f; return false; }
        var at = foe.GlobalPosition + new Vector2(0, foe.HitRadius * 0.6f);
        foe.Hurt(Tune.Support.StalagDamage * Stats.DamageMult, Vector2.Zero, at, DamageKind.Physical);
        if (!foe.Dead) foe.Interrupt(Vector2.Zero, Tune.Support.StalagSeconds, "ROOTED");
        // spikes of rock thrust up under it
        for (int k = -2; k <= 2; k++)
        {
            var p = at + new Vector2(k * 7, 0);
            G.Fx.Directional(p, Vector2.Up, 0.2f, new Color(0.55f, 0.47f, 0.38f), 4, 200, 2.4f, 0.4f, 300, 1);
        }
        G.Fx.Debris(at, new Color(0.5f, 0.43f, 0.35f), 16, 220);
        G.Fx.Shockwave(at + new Vector2(0, 6), 26, new Color(0.8f, 0.7f, 0.5f, 0.5f), 0.3f);
        G.Fx.Text(GlobalPosition + new Vector2(0, -34), "STALAG-MIGHT!", new Color(0.85f, 0.75f, 0.55f), 11, 0.9f);
        G.Sfx.Play("rock", at, -1, 0.1f, 0.8f);
        CastPose("hex", "stalag");
        return true;
    }

    private bool TryExpose(Vector2 aim)
    {
        var foe = FindSpellTarget(aim.LengthSquared() > 0.01f ? aim.Normalized() : new Vector2(Facing, 0), Tune.Support.ExposeRange, 40f);
        if (foe == null) { SayNo("NOTHING TO MARK"); _supportCd = 0.5f; return false; }
        foe.Hex(Tune.Support.ExposeVuln, 1f, Tune.Support.ExposeSeconds);
        G.Fx.Ring(foe.GlobalPosition, foe.HitRadius + 10, new Color(1f, 0.85f, 0.3f, 0.9f), 0.4f);
        G.Fx.Text(foe.HeadPoint(10f), "EXPOSED", new Color(1f, 0.85f, 0.35f), 11, 0.9f);
        G.Sfx.Play("throw", GlobalPosition, -6, 0.1f, 1.3f);
        return true;
    }

    private bool TryMendMark(Vector2 aim)
    {
        var foe = FindSpellTarget(aim.LengthSquared() > 0.01f ? aim.Normalized() : new Vector2(Facing, 0), Tune.Support.MarkRange, 40f);
        if (foe == null) { SayNo("NOTHING TO MARK"); _supportCd = 0.5f; return false; }
        foe.GiveHealMark(Tune.Support.MarkHeal * Stats.WardMult, Tune.Support.MarkSeconds);
        G.Fx.Beam(CastPoint, foe.GlobalPosition, new Color(0.6f, 0.95f, 0.9f, 0.9f));
        G.Fx.Ring(foe.GlobalPosition, foe.HitRadius + 10, new Color(0.6f, 0.95f, 0.9f, 0.9f), 0.4f);
        G.Fx.Text(foe.HeadPoint(10f), "MENDING MARK", new Color(0.65f, 1f, 0.92f), 10, 0.9f);
        G.Sfx.Play("bubble", GlobalPosition, -4, 0.05f, 1.2f);
        CastPose("hex", "ward");
        return true;
    }
}
