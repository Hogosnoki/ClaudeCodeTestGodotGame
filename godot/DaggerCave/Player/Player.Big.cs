using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

/// <summary>
/// The Gauntlet's specials (<see cref="Tune.Big"/>): with the Gauntlet on, each hero's special button is one great, dramatic stroke on a long
/// rest, made to break a horde open.
/// <list type="bullet">
/// <item>Swordsman, <b>Whirlwind</b>: they spin, striking all about them again and again.</item>
/// <item>Elementalist, <b>Meteor</b> (Megacryometeor in frost, Thunderstone in wind): a stone called down from above onto the spot along
/// the aim. It bursts wide and leaves its element burning there.</item>
/// <item>Warden, <b>Great Bash</b>: a vast shove of the shield. It flattens and stuns all in a wide arc ahead.</item>
/// <item>Vitalist, <b>Bloom</b>: life erupts from her. It tears at every creature near and seeps her element into them, and mends every
/// friend near.</item>
/// <item>Aegis, <b>Judgement</b>: a pillar of light slams onto the spot. It weakens what it strikes and wards the friends near.</item>
/// <item>Rogue, <b>Death Blossom</b>: they leap and spin in the air, untouchable, flinging knives at every creature near and all around.</item>
/// </list>
/// Every blow carries the element's touch as a special's (shared among a spell's strikes). The Warden's Charge moves to the other button.
/// </summary>
public partial class Player
{
    private float _bigCd, _bigCdTotal = 1f;
    private float _whirlT, _whirlTick, _blossomT, _blossomTick, _gbashT, _judgeT;
    private int _whirlHits, _blossomVolleys;
    private Vector2 _judgeAt, _gbashDir;
    /// <summary>For the tests: specials loosed, and the creatures their blows struck.</summary>
    public int BigUses { get; private set; }
    public int BigStruck { get; private set; }
    public bool Whirling => _whirlT > 0f;
    public bool Blossoming => _blossomT > 0f;
    public float BigCooldown => Math.Max(0f, _bigCd);
    public float BigFrac => Math.Clamp(_bigCd / Math.Max(0.01f, _bigCdTotal), 0f, 1f);
    /// <summary>The last meteor called (the tests watch it fall).</summary>
    public SkyStone LastMeteor { get; private set; }

    private float BigRest => Stats.Hero switch
    {
        HeroKind.Swordsman => Tune.Big.WhirlRest,
        HeroKind.Elementalist => Tune.Big.MeteorRest,
        HeroKind.Warden => Tune.Big.BashRest,
        HeroKind.Vitalist => Tune.Big.BloomRest,
        HeroKind.Aegis => Tune.Big.JudgeRest,
        HeroKind.Rogue => Tune.Big.BlossomRest,
        _ => 10f,
    } * Stats.AbilityCdMult;

    /// <summary>The special button, in the Gauntlet: the hero's great stroke, if it's rested.</summary>
    private bool BigSpecial(Vector2 aim)
    {
        if (_bigCd > 0f && !Tune.Testing.NoCooldowns) return false;
        if (_whirlT > 0f || _blossomT > 0f || _gbashT > 0f || _judgeT > 0f) return false;
        aim = aim.LengthSquared() > 0.01f ? aim.Normalized() : new Vector2(Facing, 0);
        bool ok = Stats.Hero switch
        {
            HeroKind.Swordsman => StartWhirl(),
            HeroKind.Elementalist => CallMeteor(aim),
            HeroKind.Warden => StartGreatBash(aim),
            HeroKind.Vitalist => Bloom(),
            HeroKind.Aegis => StartJudgement(aim),
            HeroKind.Rogue => StartBlossom(),
            _ => false,
        };
        if (!ok) return false;
        _bigCdTotal = BigRest;
        _bigCd = Tune.Testing.NoCooldowns ? 0.25f : _bigCdTotal;
        BigUses++;
        SimpleUsed(KitRole.Special);
        return true;
    }

    private void TickBig(float dt)
    {
        _bigCd -= dt;
        if (_whirlT > 0f) TickWhirl(dt);
        if (_blossomT > 0f) TickBlossom(dt);
        if (_gbashT > 0f && (_gbashT -= dt) <= 0f) GreatBashLands();
        if (_judgeT > 0f && (_judgeT -= dt) <= 0f) JudgementLands();
    }

    /// <summary>A blow of a special: damage, a shove, and its share of the special's touch.</summary>
    private void BigHit(Enemy e, float dmg, Vector2 push, Vector2 from, float touch, DamageKind kind = DamageKind.Physical)
    {
        if (e.Dead || !e.CanBeHit) return;
        bool shatter = e.FrozenSolid;
        float d = e.Hurt(dmg * Stats.DamageMult, push, e.GlobalPosition - (e.GlobalPosition - from).Normalized() * e.HitRadius, kind);
        if (d > 0f) { OnDealtDamage(d); BigStruck++; }
        if (shatter && !e.Dead) ShatterFrozen(e);
        if (!e.Dead && touch > 0f) SimpleTouch(e, KitRole.Special, from, touch);
    }

    private static IEnumerable<Enemy> FoesNear(Vector2 at, float r) => G.Enemies.ToArray()
        .Where(e => !e.Dead && e.CanBeHit && e.GlobalPosition.DistanceTo(at) <= r + e.HitRadius && G.Cave.LineClear(at, e.GlobalPosition));

    // ---------------------------------------------------------------- the Swordsman's Whirlwind

    private bool StartWhirl()
    {
        if (Heaving || _dodgeT > 0f) return false;
        _whirlT = Tune.Big.WhirlTime;
        _whirlTick = 0f;
        _whirlHits = 0;
        _swingT = -1;
        G.Sfx.Play("swing_heavy", GlobalPosition, -2, 0.05f, 0.8f);
        G.Fx.Ring(GlobalPosition, Tune.Big.WhirlRadius, new Color(Elements.ColorOf(Mode), 0.6f), 0.3f, FxLayer.RingStyle.Dashed);
        G.Main.Rumble(0.3f, 0.2f, 0.2f);
        return true;
    }

    private void TickWhirl(float dt)
    {
        _whirlT -= dt;
        if ((_whirlTick -= dt) > 0f) return;
        _whirlTick = Tune.Big.WhirlTime / Tune.Big.WhirlPulses;
        _whirlHits++;
        // (the blade comes round: the body turns with it, each stroke the other way, a slash each time)
        Facing = -Facing;
        Anim.Face((int)Facing, instant: true);
        Anim.Once(_whirlHits % 2 == 0 ? "slash_a_fwd" : "slash_b_fwd", 3, 3f);
        var c = GlobalPosition + new Vector2(0, -4);
        var col = Elements.ColorOf(Mode);
        foreach (var e in FoesNear(c, Tune.Big.WhirlRadius))
        {
            var away = (e.GlobalPosition - c).LengthSquared() > 1f ? (e.GlobalPosition - c).Normalized() : Vector2.Up;
            // (round and round: each stroke flings them a little along the spin, and outward)
            var spin = new Vector2(-away.Y, away.X) * Facing;
            BigHit(e, Tune.Big.WhirlDamage, (away + spin * 0.6f) * 120f, c, 1f / Tune.Big.WhirlPulses);
        }
        G.Fx.Ring(c, Tune.Big.WhirlRadius * 0.9f, new Color(col.Lerp(Colors.White, 0.5f), 0.8f), 0.16f, FxLayer.RingStyle.Burst);
        for (int k = 0; k < 10; k++)
        {
            float a = k / 10f * Mathf.Tau + _whirlHits;
            var p = c + new Vector2(MathF.Cos(a), MathF.Sin(a) * 0.55f) * Tune.Big.WhirlRadius * 0.8f;
            G.Fx.Glint(p, col.Lerp(Colors.White, 0.4f), 5);
        }
        G.Sfx.Play("swing", c, -6, 0.15f, 1.1f + 0.05f * _whirlHits);
        if (_whirlHits >= Tune.Big.WhirlPulses) _whirlT = 0f;
    }

    // ---------------------------------------------------------------- the Elementalist's Meteor

    private bool CallMeteor(Vector2 aim)
    {
        if (Math.Abs(aim.X) > 0.15f) Facing = Math.Sign(aim.X);
        Anim.Face((int)Facing, instant: true);
        var spot = FixedSpot(aim);
        // (it lands on what's under the spot: the floor beneath, if there is one near)
        if (G.Cave.FindFloor(spot, 160f, out var floor)) spot = floor + new Vector2(0, -6);
        var el = Mode;
        var col = Elements.ColorOf(el);
        // (from high above, a little behind the caster, falling on a slant)
        var from = spot + new Vector2(-Facing * Tune.Big.MeteorDrift, -Tune.Big.MeteorHeight);
        LastMeteor = new SkyStone { Position = from, Target = spot, El = el, Caster = this, Seconds = Tune.Big.MeteorFall, Radius = Tune.Big.MeteorRadius };
        G.Spawn(LastMeteor);
        // (the spot is marked as it comes)
        G.Fx.Ring(spot, Tune.Big.MeteorRadius, new Color(col, 0.6f), Tune.Big.MeteorFall, FxLayer.RingStyle.Dashed);
        G.Fx.Converge(spot, Tune.Big.MeteorRadius * 0.7f, new Color(col, 0.7f), 10, Tune.Big.MeteorFall);
        Anim.Once("rupture", 3, 1.2f);
        _castGlow = 1f;
        G.Sfx.Play("roar", spot, -10, 0.1f, 0.5f);
        G.Main.Rumble(0.3f, 0.2f, 0.25f);
        return true;
    }

    /// <summary>The meteor strikes: a wide burst, harder at its heart; it leaves its element burning on the spot.</summary>
    public void MeteorLanded(SkyStone m)
    {
        var spot = m.Target;
        var el = m.El;
        var col = Elements.ColorOf(el);
        float r = m.Radius;
        var struck = FoesNear(spot, r).ToList();
        foreach (var e in struck)
        {
            float d = e.GlobalPosition.DistanceTo(spot);
            float fall = Mathf.Lerp(1f, Tune.Big.MeteorEdge, Math.Clamp(d / r, 0f, 1f));
            var away = d > 1f ? (e.GlobalPosition - spot).Normalized() : Vector2.Up;
            BigHit(e, Tune.Big.MeteorDamage * fall, (away + Vector2.Up * 0.8f).Normalized() * 260f, spot, 1f);
        }
        ElementWorld.Touch(el, spot, r, Vector2.Down);
        Breakables.SpellBurst(spot, r * 0.6f);
        var zone = new Blizzard
        {
            Position = spot, Radius = r * 0.75f, Seconds = Tune.Elements.ZoneSeconds, Ticks = Tune.Elements.ZoneTicks,
            Fire = el == HeroElement.Fire, El = el, Elemental = true, Damage = 0f, FreezeChance = 0f, IgniteChance = 0f,
            Build = el == HeroElement.Wind ? 0f : Tune.Elements.ZoneBuild, Pull = el == HeroElement.Wind ? Tune.Elements.ZonePull : 0f, Caster = this,
        };
        G.Spawn(zone);
        NetSync.HeroVisual(zone);
        LastBlizzard = zone;
        G.Fx.Explosion(spot, col, 2.2f);
        G.Fx.Shockwave(spot + new Vector2(0, 4), r * 1.2f, new Color(col, 0.9f));
        G.Fx.Ring(spot, r, new Color(col.Lerp(Colors.White, 0.4f), 0.95f), 0.4f, FxLayer.RingStyle.Burst);
        G.Fx.Burst(spot, col, 40, 260, 3f, 0.7f, 300f);
        G.Fx.Dust(spot + new Vector2(0, 6), 10, 1.6f);
        G.Fx.ScreenFlash(col, 0.25f);
        G.Fx.AddShake(9f);
        G.Sfx.Play("slam", spot, 2, 0.05f, 0.6f);
        G.Sfx.Play("rock", spot, -2, 0.1f, 0.5f);
        G.Main.Rumble(0.8f, 0.6f, 0.35f);
    }

    // ---------------------------------------------------------------- the Warden's Great Bash

    private bool StartGreatBash(Vector2 aim)
    {
        if (!IsWarden || _bashT > 0 || _dashT > 0) return false;
        var d = new Vector2(Math.Abs(aim.X) > 0.2f ? Math.Sign(aim.X) : Facing, 0);
        _gbashDir = d;
        Facing = d.X;
        _gbashT = Tune.Big.BashWindup;
        _swingT = -1;
        ShieldRaised = true;
        ShieldDir = d;
        Anim.Face((int)Facing, instant: true);
        Anim.Once("bash", 3, 0.9f);
        G.Fx.Converge(GlobalPosition + d * 14f, 30f, new Color(0.95f, 0.9f, 0.7f, 0.8f), 8, Tune.Big.BashWindup);
        G.Sfx.Play("dodge", GlobalPosition, -4, 0.05f, 0.45f);
        return true;
    }

    private void GreatBashLands()
    {
        var d = _gbashDir;
        var origin = GlobalPosition + new Vector2(0, -3);
        float r = Tune.Big.BashRadius;
        float cos = MathF.Cos(Mathf.DegToRad(Tune.Big.BashArc));
        // (the shove carries the Warden a step on with it)
        Velocity = new Vector2(d.X * Tune.Big.BashLunge, Velocity.Y);
        foreach (var e in FoesNear(origin, r))
        {
            var to = e.GlobalPosition - origin;
            if (to.Length() > 12f && to.Normalized().Dot(d) < cos) continue;
            var push = (to.Normalized() + d).Normalized() * Tune.Big.BashPush;
            BigHit(e, Tune.Big.BashDamage, push, origin, 1f);
            if (!e.Dead) e.Interrupt(push, Tune.Big.BashStun * (e.Elite || e.IsGuardian ? 0.5f : 1f), "STUNNED");
        }
        foreach (var pr in G.Main.EnemyProjectiles.ToArray())
            if (!pr.IsQueuedForDeletion() && (pr.GlobalPosition - origin).Length() < r && (pr.GlobalPosition - origin).Normalized().Dot(d) > cos) pr.Reflect(d, Stats.DamageMult);
        var front = origin + d * 22f;
        G.Fx.Shockwave(front + new Vector2(0, 14), r, new Color(0.95f, 0.9f, 0.7f, 0.9f));
        for (int k = 0; k < 3; k++) G.Fx.Directional(front, d, 0.9f, new Color(0.9f, 0.85f, 0.7f, 0.8f), 14, 320, 2.4f, 0.4f, 0, 0);
        G.Fx.Dust(GlobalPosition + new Vector2(d.X * 20f, 12), 8, 1.4f);
        G.Fx.Ring(front, r * 0.6f, new Color(1f, 0.95f, 0.75f, 0.9f), 0.3f, FxLayer.RingStyle.Wave);
        G.Fx.AddShake(7f);
        G.Sfx.Play("slam", front, 0, 0.05f, 0.75f);
        G.Main.Rumble(0.7f, 0.5f, 0.25f);
    }

    // ---------------------------------------------------------------- the Vitalist's Bloom

    private bool Bloom()
    {
        if (!IsVitalist) return false;
        var c = GlobalPosition + new Vector2(0, -6);
        float r = Tune.Big.BloomRadius;
        var col = Elements.ColorOf(Mode);
        foreach (var e in FoesNear(c, r))
        {
            var away = (e.GlobalPosition - c).LengthSquared() > 1f ? (e.GlobalPosition - c).Normalized() : Vector2.Up;
            BigHit(e, Tune.Big.BloomDamage, away * 160f + new Vector2(0, -60f), c, 0f, DamageKind.Nature);
            if (!e.Dead) e.Seep(Mode, Tune.Elements.HexSeep * Tune.Big.BloomSeep, Stats.HexSeconds, Stats.DamageMult, this);
        }
        foreach (var h in HeroesNear(r)) h.Heal(h.Stats.MaxHp * Tune.Big.BloomHeal);
        ElementWorld.Touch(Mode, c, r);
        G.Fx.Converge(c, r * 0.6f, new Color(LifeColor, 0.8f), 12, 0.2f);
        G.Fx.Shockwave(c + new Vector2(0, 16), r, new Color(LifeColor, 0.85f));
        G.Fx.Ring(c, r, new Color(col, 0.9f), 0.45f, FxLayer.RingStyle.Burst);
        G.Fx.Ring(c, r * 0.6f, new Color(LifeColorLight, 0.8f), 0.35f, FxLayer.RingStyle.Wave);
        G.Fx.Burst(c, LifeColor, 36, 240, 2.6f, 0.6f, -40f);
        G.Fx.Burst(c, col, 20, 180, 2.2f, 0.5f, 0f);
        G.Fx.Flash(c, r * 0.5f, LifeColorLight, 0.25f);
        G.Fx.AddShake(5f);
        Anim.Once("hex", 3, 1.2f);
        _castGlow = 1f;
        G.Sfx.Play("heal", c, 0, 0.05f, 0.7f);
        G.Sfx.Play("drain", c, -2, 0.05f, 0.6f);
        G.Main.Rumble(0.5f, 0.4f, 0.2f);
        return true;
    }

    // ---------------------------------------------------------------- the Aegis's Judgement

    private bool StartJudgement(Vector2 aim)
    {
        if (!IsAegis) return false;
        if (Math.Abs(aim.X) > 0.15f) Facing = Math.Sign(aim.X);
        Anim.Face((int)Facing, instant: true);
        var spot = FixedSpot(aim);
        if (G.Cave.FindFloor(spot, 160f, out var floor)) spot = floor + new Vector2(0, -6);
        _judgeAt = spot;
        _judgeT = Tune.Big.JudgeWindup;
        var gold = new Color(1f, 0.9f, 0.5f);
        G.Fx.Ring(spot, Tune.Big.JudgeRadius, new Color(gold, 0.6f), Tune.Big.JudgeWindup, FxLayer.RingStyle.Dashed);
        G.Fx.Beam(spot + new Vector2(0, -420f), spot, new Color(gold, 0.25f));
        Anim.Once("heal", 3, 1.4f);
        _castGlow = 1f;
        G.Sfx.Play("levelup", spot, -8, 0.05f, 0.7f);
        return true;
    }

    private void JudgementLands()
    {
        var spot = _judgeAt;
        float r = Tune.Big.JudgeRadius;
        var gold = new Color(1f, 0.9f, 0.5f);
        foreach (var e in FoesNear(spot, r))
        {
            var away = (e.GlobalPosition - spot).LengthSquared() > 1f ? (e.GlobalPosition - spot).Normalized() : Vector2.Up;
            BigHit(e, Tune.Big.JudgeDamage, away * 180f + new Vector2(0, -80f), spot, 1f);
            if (!e.Dead) e.Weaken(Tune.Big.JudgeWeaken, Tune.Big.JudgeWeakenSeconds);
        }
        // (the friends near the Aegis, or near where it fell, are warded)
        foreach (var h in G.Players.ToArray())
        {
            if (h == null || !IsInstanceValid(h) || h.Dead) continue;
            if (h.GlobalPosition.DistanceTo(spot) > Tune.Big.JudgeWardRange && h.GlobalPosition.DistanceTo(GlobalPosition) > Tune.Big.JudgeWardRange) continue;
            h.GiveBarrier(h.Stats.MaxHp * Tune.Big.JudgeBarrier, Tune.Big.JudgeBarrierSeconds);
        }
        for (int k = 0; k < 3; k++) G.Fx.Beam(spot + new Vector2(G.Range(-10, 10), -480f), spot, new Color(gold, 0.9f));
        G.Fx.Explosion(spot, gold, 1.6f);
        G.Fx.Shockwave(spot + new Vector2(0, 6), r * 1.1f, new Color(gold, 0.9f));
        G.Fx.Ring(spot, r, new Color(1f, 0.97f, 0.85f, 0.95f), 0.35f, FxLayer.RingStyle.Burst);
        G.Fx.Burst(spot, gold, 30, 220, 2.6f, 0.6f, -60f);
        G.Fx.ScreenFlash(gold, 0.2f);
        G.Fx.AddShake(7f);
        G.Sfx.Play("slam", spot, 0, 0.05f, 0.9f);
        G.Sfx.Play("levelup", spot, -4, 0.05f, 0.6f);
        G.Main.Rumble(0.6f, 0.4f, 0.25f);
    }

    // ---------------------------------------------------------------- the Rogue's Death Blossom

    private bool StartBlossom()
    {
        if (!IsRogue || _tetherTo != null) return false;
        _blossomT = Tune.Big.BlossomTime;
        _blossomTick = 0f;
        _blossomVolleys = 0;
        _iframes = Math.Max(_iframes, Tune.Big.BlossomTime + 0.1f);
        _swingT = -1;
        // (up into the air)
        Velocity = new Vector2(Velocity.X * 0.3f, -BaseJumpV * Tune.Big.BlossomLeap);
        _jumpCutDone = true;
        Anim.Once("dodge", 3, 1.4f);
        G.Sfx.Play("throw", GlobalPosition, -2, 0.1f, 0.8f);
        G.Fx.Ring(GlobalPosition, 18, new Color(Elements.ColorOf(Mode), 0.8f), 0.25f, FxLayer.RingStyle.Burst);
        return true;
    }

    private void TickBlossom(float dt)
    {
        _blossomT -= dt;
        // (hanging at the top of the leap while the knives fly)
        if (Velocity.Y > 0f) Velocity = new Vector2(Velocity.X, Math.Min(Velocity.Y, 40f));
        if ((_blossomTick -= dt) > 0f) return;
        _blossomTick = Tune.Big.BlossomTime / Tune.Big.BlossomVolleys;
        _blossomVolleys++;
        Facing = -Facing;
        Anim.Face((int)Facing, instant: true);
        Anim.Once("throw", 3, 3f);
        var c = GlobalPosition + new Vector2(0, -4);
        var col = Elements.ColorOf(Mode);
        int n = Tune.Big.BlossomKnives;
        float turn = _blossomVolleys * Mathf.Pi / n;
        // (the knives find the creatures about: one at each, nearest first, the rest flung out all around)
        var marks = FoesNear(c, Tune.Big.BlossomRange).OrderBy(e => e.GlobalPosition.DistanceSquaredTo(c)).Take(n).ToList();
        for (int k = 0; k < n; k++)
        {
            float a = k / (float)n * Mathf.Tau + turn;
            var dir = k < marks.Count ? (marks[k].GlobalPosition - c).Normalized().Rotated(G.Range(-0.04f, 0.04f)) : new Vector2(MathF.Cos(a), MathF.Sin(a));
            // (a knife thrown at a creature in sight flies to it; one flung out flies until it meets rock)
            Vector2 end;
            if (k < marks.Count) end = marks[k].GlobalPosition + dir * marks[k].HitRadius;
            else G.Cave.Raycast(c, dir, Tune.Big.BlossomRange, out end, 2f);
            // (a knife thrown at a creature threads past the others to it)
            Enemy hit = k < marks.Count ? marks[k] : null; float best = float.MaxValue;
            if (hit == null) foreach (var e in G.Enemies)
            {
                if (e.Dead || !e.CanBeHit) continue;
                var p = Geometry2D.GetClosestPointToSegment(e.GlobalPosition, c, end);
                if (p.DistanceTo(e.GlobalPosition) > e.HitRadius + 3f) continue;
                float dd = p.DistanceSquaredTo(c);
                if (dd < best) { best = dd; hit = e; }
            }
            var to = hit != null ? hit.GlobalPosition : end;
            G.Fx.Beam(c + dir * 6f, to, new Color(col.Lerp(Colors.White, 0.6f), 0.7f));
            if (hit != null)
            {
                BigHit(hit, Tune.Big.BlossomDamage, dir * 60f, c, 1f / (Tune.Big.BlossomVolleys * 2f));
                G.Fx.Spark(hit.GlobalPosition, -dir, true, col.Lerp(Colors.White, 0.5f));
            }
        }
        G.Sfx.Play("throw", c, -6, 0.15f, 1.2f + 0.08f * _blossomVolleys);
        if (_blossomVolleys >= Tune.Big.BlossomVolleys) _blossomT = 0f;
    }
}

/// <summary>
/// The Elementalist's meteor (<see cref="Player.MeteorLanded"/>): a stone of her element, called down from high above onto a spot. It falls on a
/// slant over <see cref="Seconds"/>, trailing its element, and bursts where it lands. The caster's game deals the blow; the fall is seen in all.
/// </summary>
public partial class SkyStone : Node2D
{
    public Vector2 Target;
    public HeroElement El;
    public Player Caster;
    public float Seconds = 0.55f, Radius = 90f;
    private Vector2 _from;
    private float _t;
    public bool Landed { get; private set; }
    public float Progress => Math.Clamp(_t / Math.Max(0.01f, Seconds), 0f, 1f);

    public override void _Ready() { _from = GlobalPosition; ZIndex = 3; }

    public override void _PhysicsProcess(double delta)
    {
        _t += (float)delta;
        float k = Progress;
        // (it gathers speed as it falls)
        GlobalPosition = _from.Lerp(Target, k * k);
        var col = Elements.ColorOf(El);
        if (G.Chance(0.8f)) G.Fx.Burst(GlobalPosition, new Color(col, 0.8f), 2, 40, 2.2f, 0.35f, -20f);
        if (k < 1f) return;
        Landed = true;
        if (Caster != null && IsInstanceValid(Caster)) Caster.MeteorLanded(this);
        QueueFree();
    }
}
