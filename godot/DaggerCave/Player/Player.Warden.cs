using System;
using Godot;

namespace DaggerCave;

/// <summary>
/// The Warden's kit. The shield is raised by holding the dodge button, or just by pushing the
/// right stick (so you can run one way and guard the other). It stops most of each blow (the
/// rest gets through as chip damage, without a flinch) and loses strength for what it stops;
/// normal blocks don't stop an attack, but a perfect block (raised just before the hit) stops
/// all of it and breaks the attack off. Healing also mends the shield. The ability is the
/// shield dash: a guarded charge that stops at the first projectile or attacking creature in
/// its way and breaks that attack off, passing straight by creatures that aren't attacking. The
/// second ability is the shield bash: a short shove that stuns what it meets (breaking off its
/// attack) at the cost of a dent in the shield.
/// </summary>
public partial class Player
{
    public float ShieldHp { get; private set; } = Tune.Warden.ShieldHp;
    public bool ShieldRaised { get; private set; }
    public bool ShieldBroken => _shieldBrokenT > 0;
    public float ShieldBrokenLeft => _shieldBrokenT;
    public Vector2 ShieldDir { get; private set; } = Vector2.Right;
    public float ShieldArc => Mathf.DegToRad(Tune.Warden.ShieldArcDegrees) * Stats.ShieldArcMult;
    private float _shieldBrokenT, _shieldRegenWait, _shieldUpT, _shieldFlash, _blockGrace;

    private float _dashT;
    private Vector2 _dashDir = Vector2.Right;
    public bool IsShieldDashing => IsRemote ? (_netFlags & HfDash) != 0 : _dashT > 0;
    /// <summary>0 = the shield dash is ready, 1 = just used.</summary>
    public float DashCooldownFrac => AbilityCooldownFrac;

    private float _bashT, _bashCd;
    private bool _bashHit;
    private Vector2 _bashDir = Vector2.Right;
    public bool IsShieldBashing => _bashT > 0;
    /// <summary>0 = the shield bash is ready, 1 = just used.</summary>
    public float BashCooldownFrac => Math.Clamp(_bashCd / Math.Max(0.01f, Stats.BashCooldown), 0, 1);

    /// <summary>Test harness: a fresh, full shield.</summary>
    public void RefillShield() { ShieldHp = Stats.ShieldMax; _shieldBrokenT = 0; }

    /// <summary>Healing mends the shield too; a broken shield is usable again at once.</summary>
    private void MendShield(float amount)
    {
        if (amount <= 0) return;
        if (_shieldBrokenT > 0)
        {
            _shieldBrokenT = 0;
            G.Fx?.Text(GlobalPosition + new Vector2(0, -30), "SHIELD MENDED", new Color(0.6f, 0.85f, 1f), 9, 0.9f);
        }
        ShieldHp = Math.Min(Stats.ShieldMax, ShieldHp + amount);
    }

    private void UpdateShield(PlayerInput inp, float dt)
    {
        // regeneration: at zero after a break for ShieldBreakTime, then back at the normal rate
        if (_shieldBrokenT > 0) _shieldBrokenT -= dt;
        else if (_shieldRegenWait > 0) _shieldRegenWait -= dt * (Stats.QuickMend ? 4f : 1f);
        else ShieldHp = Math.Min(Stats.ShieldMax, ShieldHp + Stats.ShieldRegen * dt);
        _shieldFlash -= dt; _blockGrace -= dt;

        bool dashing = _dashT > 0, bashing = _bashT > 0;
        bool want = inp.GuardHeld || inp.Dodge || inp.StickGuard;
        bool was = ShieldRaised;
        ShieldRaised = dashing || (bashing && !ShieldBroken) || (want && !ShieldBroken && ShieldHp > 0);
        if (ShieldRaised && !was) _shieldUpT = 0;
        _shieldUpT += dt;
        // aim: right stick / mouse when given, otherwise the way you face
        var aim = dashing ? _dashDir : bashing ? _bashDir : inp.GuardAim.LengthSquared() > 0.01f ? inp.GuardAim.Normalized() : new Vector2(Facing, 0);
        ShieldDir = aim;
        // held with the button, the shield turns you to face it; held with the right stick it
        // can point behind you while you run
        if (ShieldRaised && !dashing && !bashing && !inp.StickGuard && Math.Abs(aim.X) > 0.2f) Facing = Math.Sign(aim.X);
    }

    public struct Block
    {
        public bool Blocked, Perfect;
        /// <summary>Damage that gets through the shield.</summary>
        public float Through;
    }

    /// <summary>
    /// Whether the raised shield covers a blow arriving from <paramref name="from"/>. It stops
    /// Stats.BlockShare of it (all of it on a perfect block, or mid-dash) and loses ShieldCost of
    /// what it stopped; if that's more than it has left, it gives way and the rest comes through.
    /// A perfect block breaks off the attack of the <paramref name="melee"/> attacker.
    /// </summary>
    public Block TryBlock(Vector2 from, float dmg, Enemy melee = null)
    {
        var b = new Block { Through = dmg };
        if (!IsWarden || !ShieldRaised) return b;
        var to = from - GlobalPosition;
        // an attacker pressed right up against you is judged by which side it's on
        if (to.Length() < 14) to = new Vector2(to.X == 0 ? ShieldDir.X : Math.Sign(to.X), 0);
        if (to.LengthSquared() < 0.01f) to = ShieldDir;
        if (Math.Abs(ShieldDir.AngleTo(to)) > ShieldArc * 0.5f + 0.2f) return b;
        b.Blocked = true;
        // melee blows landing on the shield in the same instant count once
        if (melee != null && _blockGrace > 0) { b.Through = 0; return b; }
        if (melee != null) _blockGrace = 0.2f;
        bool dashing = _dashT > 0;
        b.Perfect = dashing || _shieldUpT <= Tune.Warden.PerfectWindow;
        float stopped = b.Perfect ? dmg : dmg * Stats.BlockShare;
        b.Through = dmg - stopped;
        float cost = dashing ? 0f : stopped * Tune.Warden.ShieldCost * (b.Perfect && Stats.PerfectSoak ? Tune.Warden.PerfectSoakMult : 1f);
        if (cost > ShieldHp)
        {
            b.Through += stopped * (cost - ShieldHp) / cost;
            ShieldHp = 0;
        }
        else ShieldHp -= cost;
        _shieldRegenWait = Tune.Warden.ShieldRegenDelay;
        _shieldFlash = b.Perfect ? 0.25f : 0.12f;
        var at = GlobalPosition + ShieldDir * 16;
        G.Fx.Spark(at, ShieldDir, b.Perfect, b.Perfect ? new Color(1f, 1f, 0.8f) : new Color(0.55f, 0.8f, 1f));
        G.Sfx.Play("clink", at, b.Perfect ? 0 : -3, 0.1f, b.Perfect ? 1.3f : 0.9f);
        G.Main.Rumble(0.3f, 0.2f, 0.08f);
        Freeze(b.Perfect ? 0.08f : 0.04f);
        if (b.Perfect && !dashing) G.Fx.Text(GlobalPosition + new Vector2(0, -26), "PERFECT", new Color(1f, 0.95f, 0.6f), 10, 0.6f);
        if (melee != null && GodotObject.IsInstanceValid(melee) && !melee.Dead)
        {
            var away = (melee.GlobalPosition - GlobalPosition).Normalized();
            // a perfect block ends the attack; an ordinary one only takes the sting out of it
            if (b.Perfect) melee.Interrupt(away * 200f, Tune.Warden.PerfectStagger);
            if (Stats.ShieldThorns) melee.Hurt(dmg * 0.4f * Stats.DamageMult, away * 120f, melee.GlobalPosition - away * melee.HitRadius);
        }
        if (ShieldHp <= 0 && !dashing) BreakShield(at);
        return b;
    }

    private void BreakShield(Vector2 at)
    {
        ShieldHp = 0;
        _shieldBrokenT = Stats.ShieldBreakTime;
        ShieldRaised = false;
        G.Sfx.Play("rock", at, 0, 0.1f, 1.4f);
        G.Fx.Burst(at, new Color(0.55f, 0.8f, 1f), 22, 180, 2.5f, 0.5f);
        G.Fx.Text(GlobalPosition + new Vector2(0, -26), "SHIELD BROKEN", new Color(0.6f, 0.8f, 1f), 10, 1f);
    }

    /// <summary>What gets through the shield: armour applies, but there's no flinch, knockback or hit-stop.</summary>
    private float ApplyChip(float through, Enemy source)
    {
        if (through <= 0.01f) return 0;
        float dmg = through * (1f - Stats.DamageReduction) * Stats.DamageTakenMult;
        if (source != null && GodotObject.IsInstanceValid(source)) source.CreditDamage(dmg);
        TakeRawDamage(dmg, "chip");
        _invuln = Math.Max(_invuln, 0.1f);
        return dmg;
    }

    /// <summary>Projectiles meet the shield first; a perfect block with Riposte Guard sends them back.</summary>
    public bool TryBlockProjectile(EnemyProjectile pr)
    {
        var b = TryBlock(pr.GlobalPosition - pr.Vel.Normalized() * 10, pr.Damage);
        if (!b.Blocked) return false;
        if (b.Perfect && Stats.PerfectReflect) pr.Reflect(ShieldDir, Stats.DamageMult);
        else pr.Deflect();
        LastHitBlocked = true;
        ApplyChip(b.Through, pr.Source);
        return true;
    }

    // ---------------------------------------------------------------- shield dash

    private bool TryShieldDash(Vector2 aim)
    {
        if (!IsWarden || !AbilityChargeReady || _dashT > 0) return false;
        var d = aim.LengthSquared() > 0.01f ? aim.Normalized() : new Vector2(Facing, 0);
        // on your feet it's a charge along the ground (unless you aim well upward)
        if (!InWater && IsOnFloor() && d.Y > -0.5f) d = new Vector2(Math.Abs(d.X) > 0.1f ? Math.Sign(d.X) : Facing, 0);
        _dashDir = d;
        _dashT = Stats.DashTime;
        SpendAbilityCharge();
        if (Math.Abs(d.X) > 0.1f) Facing = Math.Sign(d.X);
        _swingT = -1; // a swing still under way gives way to the charge
        _shieldUpT = 0;
        ShieldRaised = true;
        ShieldDir = d;
        Anim.Face((int)Facing, instant: true);
        Anim.Once("bash", 3, 6f / (Stats.DashTime * 24f));
        G.Sfx.Play("dodge", GlobalPosition, -2, 0.05f, 0.75f);
        G.Sfx.Play("clink", GlobalPosition, -10, 0.05f, 0.6f);
        if (!InWater && IsOnFloor()) G.Fx.Dust(GlobalPosition + new Vector2(0, 12), 5, 1.6f);
        G.Fx.Directional(GlobalPosition, -d, 0.6f, new Color(0.6f, 0.85f, 1f, 0.7f), 8, 180, 1.6f, 0.25f, 0);
        G.Main.Rumble(0.25f, 0.1f, 0.1f);
        return true;
    }

    private Vector2 DashMotion(Vector2 v, float dt)
    {
        if (DashCollide()) return Velocity; // stopped against something (with a rebound)
        v = _dashDir * Tune.Warden.DashSpeed;
        if (Engine.GetPhysicsFrames() % 2 == 0) Afterimage.Spawn(Anim, new Color(0.45f, 0.7f, 1f), 0.22f);
        _dashT -= dt;
        if (_dashT <= 0) v *= 0.35f;
        return v;
    }

    /// <summary>The charge meets the first projectile, shockwave or attacking creature ahead of it.</summary>
    private bool DashCollide()
    {
        var front = GlobalPosition + new Vector2(0, -3) + _dashDir * 12;
        foreach (var pr in G.Main.EnemyProjectiles.ToArray())
        {
            if (pr.GlobalPosition.DistanceTo(front) > pr.Radius + 13) continue;
            var at = pr.GlobalPosition;
            if (Stats.PerfectReflect) pr.Reflect(_dashDir, Stats.DamageMult); else pr.Deflect();
            DashImpact(at, null);
            return true;
        }
        foreach (var n in G.World.GetChildren())
        {
            if (n is not Shockwave sw || sw.GlobalPosition.DistanceTo(front) > 18 * sw.Size) continue;
            var at = sw.GlobalPosition;
            sw.Break();
            DashImpact(at, null);
            return true;
        }
        foreach (var e in G.Enemies.ToArray())
        {
            if (e.Dead || !e.AttackingNow) continue;
            if (e.GlobalPosition.DistanceTo(front) > e.HitRadius + 10) continue;
            DashImpact(e.GlobalPosition - (e.GlobalPosition - front).Normalized() * e.HitRadius, e);
            return true;
        }
        return false;
    }

    /// <summary>The charge stops dead against it: the attack is broken off, with a shield bash.</summary>
    private void DashImpact(Vector2 at, Enemy e)
    {
        if (e != null)
        {
            e.Interrupt(_dashDir * Tune.Warden.DashPush, Tune.Warden.DashStagger);
            float dealt = e.Hurt(Tune.Warden.DashDamage * Stats.DamageMult * Stats.DashDamageMult, _dashDir * 160f, at);
            if (dealt > 0) OnDealtDamage(dealt);
            if (!e.Dead) e.Freeze(Tune.Feel.HitStopDash);
            if (Stats.DashMend) { MendShield(12f); Heal(4f); }
        }
        G.Fx.Spark(at, _dashDir, true, new Color(0.7f, 0.9f, 1f));
        G.Fx.Ring(at, 14, new Color(0.6f, 0.85f, 1f, 0.9f));
        G.Sfx.Play("clink", at, 0, 0.1f, 0.7f);
        G.Sfx.Play("slam", at, -10, 0.1f, 1.4f);
        G.Main.Kick(_dashDir * Tune.Feel.KickFinisher);
        G.Fx.AddShake(3);
        G.Main.Rumble(0.5f, 0.5f, 0.15f);
        Freeze(Tune.Feel.HitStopDash);
        _invuln = Math.Max(_invuln, 0.25f);
        _dashT = 0;
        _shieldUpT = 0; // still braced: a blow that follows at once is a perfect block too
        Velocity = new Vector2(-_dashDir.X * 110f, Math.Min(Velocity.Y, 0f) * 0.2f);
    }

    // ---------------------------------------------------------------- shield bash

    private bool TryShieldBash(Vector2 aim)
    {
        if (!IsWarden || _bashCd > 0 || _bashT > 0) return false;
        if (ShieldBroken || ShieldHp <= 0) { _bashCd = 0.5f; SayNo("SHIELD BROKEN"); return true; }
        var d = aim.LengthSquared() > 0.01f ? aim.Normalized() : new Vector2(Facing, 0);
        // on your feet it's a shove straight ahead; in the air or the water, wherever you aim
        if (!InWater && IsOnFloor()) d = new Vector2(Math.Abs(d.X) > 0.2f ? Math.Sign(d.X) : Facing, 0);
        _bashDir = d;
        _bashT = Tune.Warden.BashTime;
        _bashCd = Stats.BashCooldown;
        _bashHit = false;
        if (Math.Abs(d.X) > 0.1f) Facing = Math.Sign(d.X);
        _swingT = -1; // a swing still under way gives way to the shove
        _shieldUpT = 0;
        ShieldRaised = true;
        ShieldDir = d;
        Anim.Face((int)Facing, instant: true);
        Anim.Once("shove", 3, 8f / 24f / (Tune.Warden.BashTime * 1.8f));
        G.Sfx.Play("dodge", GlobalPosition, -4, 0.05f, 0.6f);
        if (!InWater && IsOnFloor()) G.Fx.Dust(GlobalPosition + new Vector2(Facing * -4, 12), 3, 1f);
        return true;
    }

    /// <summary>The shove's own motion: a short burst forward, fading (it stops dead on impact).</summary>
    private float BashMotion(float vx)
    {
        if (_bashHit) return vx;
        float k = Math.Clamp(_bashT / Tune.Warden.BashTime, 0, 1);
        return _bashDir.X * Tune.Warden.BashLunge * k;
    }

    private void TickBash(float dt)
    {
        if (_bashT <= 0) return;
        _bashT -= dt;
        if (_bashHit) return;
        if (InWater || !IsOnFloor()) Velocity = Velocity.Lerp(_bashDir * Tune.Warden.BashLunge * 0.6f, 0.3f);
        var front = GlobalPosition + new Vector2(0, -3) + _bashDir * 10;
        // the shield meets projectiles on the way, as it would held up
        foreach (var pr in G.Main.EnemyProjectiles.ToArray())
        {
            if (pr.GlobalPosition.DistanceTo(front) > pr.Radius + 14) continue;
            if (Stats.PerfectReflect) pr.Reflect(_bashDir, Stats.DamageMult); else pr.Deflect();
        }
        Enemy best = null;
        float bestD = float.MaxValue;
        foreach (var e in G.Enemies)
        {
            if (e.Dead || !e.CanBeHit) continue;
            float d = e.GlobalPosition.DistanceTo(front) - e.HitRadius;
            if (d > Tune.Warden.BashReach || d >= bestD) continue;
            if (!G.Cave.LineClear(GlobalPosition + new Vector2(0, -3), e.GlobalPosition)) continue;
            best = e; bestD = d;
        }
        if (best != null) BashImpact(best);
    }

    /// <summary>The shove lands: damage, a stun that breaks off whatever it was doing, and a dent in the shield.</summary>
    private void BashImpact(Enemy e)
    {
        _bashHit = true;
        _bashT = Math.Min(_bashT, 0.1f);
        var at = e.GlobalPosition - (e.GlobalPosition - GlobalPosition).Normalized() * e.HitRadius;
        // (the blow first, then the stun: a hit's own short reel mustn't cut the stun short)
        float dealt = e.Hurt(Tune.Warden.BashDamage * Stats.DamageMult, _bashDir * Tune.Warden.BashPush, at);
        if (dealt > 0) OnDealtDamage(dealt);
        if (!e.Dead)
        {
            float stun = Tune.Warden.BashStun * (e.Elite || e.IsGuardian ? 0.5f : 1f);
            e.Interrupt(_bashDir * Tune.Warden.BashPush, stun, "STUNNED");
            e.Freeze(Tune.Feel.HitStopDash);
        }
        // the shield takes the blow too
        ShieldHp -= Tune.Warden.BashShieldCost;
        _shieldRegenWait = Tune.Warden.ShieldRegenDelay;
        _shieldFlash = 0.25f;
        G.Fx.Spark(at, _bashDir, true, new Color(1f, 0.95f, 0.75f));
        G.Fx.Ring(at, 16, new Color(0.7f, 0.9f, 1f, 0.9f));
        G.Fx.Shockwave(at, 26, new Color(1f, 1f, 1f, 0.7f), 0.25f);
        for (int k = 0; k < 5; k++) G.Fx.Glint(at + G.RandDir() * G.Range(4, 14), new Color(1f, 0.9f, 0.5f), 6);
        G.Sfx.Play("clink", at, 2, 0.05f, 0.55f);
        G.Sfx.Play("slam", at, -4, 0.05f, 1.6f);
        G.Main.Kick(_bashDir * Tune.Feel.KickFinisher);
        G.Fx.AddShake(3.5f);
        G.Main.Rumble(0.6f, 0.7f, 0.18f);
        Freeze(Tune.Feel.HitStopDash);
        Velocity = new Vector2(-_bashDir.X * 70f, Math.Min(Velocity.Y, 0f));
        if (ShieldHp <= 0) BreakShield(at);
    }

    // ---------------------------------------------------------------- drawing

    /// <summary>The Warden's guard (for the 3D stage): the shield's arc of light.</summary>
    public struct Guard
    {
        public bool Shield, Dash;
        public float Angle, Half, Strength;
        public Color Col;
    }

    public Guard GetGuard()
    {
        var g = new Guard();
        if (Dead || !IsWarden || !ShieldRaised) return g;
        g.Shield = true;
        g.Dash = IsShieldDashing;
        g.Strength = g.Dash ? 1f : Math.Clamp(ShieldHp / Math.Max(1f, Stats.ShieldMax), 0, 1);
        bool perfectWindow = IsRemote ? (_netFlags & HfPerfect) != 0 : _shieldUpT <= Tune.Warden.PerfectWindow;
        g.Col = g.Dash || perfectWindow || _shieldFlash > 0 ? new Color(0.85f, 0.95f, 1f) : new Color(0.35f, 0.65f, 1f);
        g.Angle = ShieldDir.Angle();
        g.Half = ShieldArc * 0.5f;
        return g;
    }

    private void DrawGuard()
    {
        var g = GetGuard();
        if (!g.Shield) return;
        // a narrow arc of blue light in front of the shield hand
        var c = new Vector2(0, -3);
        const float r = 17;
        DrawArc(c, r, g.Angle - g.Half, g.Angle + g.Half, 16, new Color(g.Col, 0.25f + 0.3f * g.Strength), 6f);
        DrawArc(c, r + 1.5f, g.Angle - g.Half, g.Angle + g.Half, 16, new Color(g.Col, 0.55f + 0.45f * g.Strength), 1.6f);
    }
}
