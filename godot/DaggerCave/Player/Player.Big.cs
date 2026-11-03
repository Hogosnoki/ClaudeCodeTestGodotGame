using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

/// <summary>
/// The great specials (<see cref="Tune.Big"/>): each hero's special button is one great, dramatic stroke on a long
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
    /// <summary>Strokes ready (a Twin Stroke holds two): one comes back each time the rest runs out.</summary>
    private int _bigHeld = 1;
    private float _whirlT, _whirlTick, _blossomT, _blossomTick, _gbashT, _judgeT;
    private int _whirlHits, _whirlPulses, _blossomVolleys, _blossomMax;
    private Vector2 _judgeAt, _gbashDir;
    // (what a stroke leaves going on after it: a garden, a sanctum, and blows still to come)
    private float _gardenT, _gardenTick, _sanctumT, _sanctumTick;
    private Vector2 _gardenAt, _sanctumAt;
    private readonly List<(float t, Action a)> _bigLater = new();
    /// <summary>For the tests: specials loosed, and the creatures their blows struck.</summary>
    public int BigUses { get; private set; }
    public int BigStruck { get; private set; }
    public bool Whirling => _whirlT > 0f;
    public bool Blossoming => _blossomT > 0f;
    public float BigCooldown => _bigHeld > 0 ? 0f : Math.Max(0f, _bigCd);
    public float BigFrac => _bigHeld > 0 ? 0f : Math.Clamp(_bigCd / Math.Max(0.01f, _bigCdTotal), 0f, 1f);
    /// <summary>Strokes ready now.</summary>
    public int BigHeld => _bigHeld;
    /// <summary>The last meteor called (the tests watch it fall).</summary>
    public SkyStone LastMeteor { get; private set; }
    /// <summary>The hero's special has taken this form (a milestone's: "a" or "b"; "" its own).</summary>
    private bool BigForm(string f) => Stats.SpecialForm == f;
    private float SR => Stats.SpecialRadiusMult;

    private float BigRest => Stats.Hero switch
    {
        HeroKind.Swordsman => Tune.Big.WhirlRest,
        HeroKind.Elementalist => Tune.Big.MeteorRest * (BigForm("b") ? Tune.Growth.CataclysmRest : 1f),
        HeroKind.Warden => Tune.Big.BashRest,
        HeroKind.Vitalist => Tune.Big.BloomRest,
        HeroKind.Aegis => Tune.Big.JudgeRest,
        HeroKind.Rogue => Tune.Big.BlossomRest,
        _ => 10f,
    } * Stats.SpecialRestMult;

    /// <summary>A Twin Stroke: one more stroke held, ready now.</summary>
    public void GainSpecialCharge() => _bigHeld = Math.Min(Stats.SpecialCharges, _bigHeld + 1);

    /// <summary>The special's rest is cut short (a kill, the other ability with Momentum).</summary>
    public void CutSpecialRest(float seconds)
    {
        if (_bigCd > 0f && seconds > 0f) _bigCd = Math.Max(0.001f, _bigCd - seconds);
    }

    /// <summary>The special button: the hero's great stroke, if one is ready.</summary>
    private bool BigSpecial(Vector2 aim)
    {
        if (_bigHeld <= 0 && !Tune.Testing.NoCooldowns) return false;
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
        if (Tune.Testing.NoCooldowns) _bigCd = 0.25f;
        else
        {
            _bigHeld--;
            if (_bigCd <= 0f) _bigCd = _bigCdTotal;
        }
        BigUses++;
        SimpleUsed(KitRole.Special);
        return true;
    }

    private void TickBig(float dt)
    {
        if (_bigCd > 0f && (_bigCd -= dt) <= 0f && !Tune.Testing.NoCooldowns)
        {
            // (a stroke comes back; if more are owed, the next starts resting)
            _bigHeld = Math.Min(Stats.SpecialCharges, _bigHeld + 1);
            if (_bigHeld < Stats.SpecialCharges) _bigCd += _bigCdTotal;
        }
        if (_whirlT > 0f) TickWhirl(dt);
        if (_blossomT > 0f) TickBlossom(dt);
        if (_gbashT > 0f && (_gbashT -= dt) <= 0f) GreatBashLands(1f, false);
        if (_judgeT > 0f && (_judgeT -= dt) <= 0f) JudgementLands(_judgeAt, 1f, 1f, true);
        if (_gardenT > 0f) TickGarden(dt);
        if (_sanctumT > 0f) TickSanctum(dt);
        for (int k = _bigLater.Count - 1; k >= 0; k--)
        {
            var (t, a) = _bigLater[k];
            if ((t -= dt) > 0f) { _bigLater[k] = (t, a); continue; }
            _bigLater.RemoveAt(k);
            if (!Dead) a();
        }
    }

    /// <summary>A blow of a special (or, <paramref name="special"/> false, of another great blow): damage, a shove, and its share of the touch.</summary>
    private void BigHit(Enemy e, float dmg, Vector2 push, Vector2 from, float touch, DamageKind kind = DamageKind.Physical, bool special = true)
    {
        if (e.Dead || !e.CanBeHit) return;
        bool shatter = e.FrozenSolid;
        float d = e.Hurt(dmg * Stats.DamageMult * (special ? Stats.SpecialDamageMult : 1f), push, e.GlobalPosition - (e.GlobalPosition - from).Normalized() * e.HitRadius, kind);
        if (d > 0f) { OnDealtDamage(d); BigStruck++; }
        if (shatter && !e.Dead) ShatterFrozen(e);
        if (!e.Dead && touch > 0f) SimpleTouch(e, KitRole.Special, from, touch);
    }

    /// <summary>The zones made by infusions, newest last (for tests).</summary>
    public readonly List<Blizzard> InfusedZones = new();

    /// <summary>
    /// The special's infusions (Player/Infusions.cs): each leaves its element on the ground where the special landed, a zone that keeps
    /// on striking, building its element, setting alight, freezing or drawing in (an Ascended one wider, longer, harder).
    /// </summary>
    private void Infuse(Vector2 at, float reach)
    {
        if (!Cards.Grows(Stats.Hero)) return;
        // (on the ground beneath, if there is ground near: a crater, not a cloud)
        bool grounded = G.Cave.FindFloor(at, reach * 1.2f, out var floor);
        foreach (var (sp, n) in Infusions.Of(Stats))
        {
            bool up = n >= 2;
            float r = reach * sp.Radius * (up ? Tune.Infusion.AscendRadius : 1f);
            float secs = Tune.Infusion.Seconds * sp.Seconds * (up ? Tune.Infusion.AscendSeconds : 1f);
            float hard = up ? Tune.Infusion.AscendDamage : 1f;
            var spot = grounded ? floor - new Vector2(0, r * 0.45f) : at;
            var zone = new Blizzard
            {
                Position = spot, Radius = r, Seconds = secs, Ticks = Math.Max(2, (int)MathF.Round(secs * Tune.Infusion.TicksPerSecond)),
                Fire = sp.El == HeroElement.Fire, El = sp.El, Elemental = true, Infusion = true,
                Damage = Tune.Infusion.TickDamage * sp.Damage * hard * Stats.DamageMult * Stats.SpecialDamageMult,
                Build = Tune.Infusion.TickBuild * sp.Build * hard, Pull = sp.Pull,
                IgniteChance = sp.Ignite, IgniteDps = Tune.Infusion.IgniteDps * hard * Stats.DamageMult, IgniteSeconds = Tune.Infusion.IgniteSeconds,
                FreezeChance = sp.Freeze, Caster = this,
            };
            G.Spawn(zone);
            NetSync.HeroVisual(zone);
            InfusedZones.Add(zone);
            if (InfusedZones.Count > 16) InfusedZones.RemoveAt(0);
            var col = Elements.ColorOf(sp.El);
            ElementWorld.Touch(sp.El, spot, r, Vector2.Down);
            G.Fx.Ring(spot, r, new Color(col, 0.9f), 0.5f, FxLayer.RingStyle.Burst);
            G.Fx.Burst(spot, new Color(col.Lerp(Colors.White, 0.3f), 0.9f), 18, 220, 2.6f, 0.5f, sp.El == HeroElement.Fire ? -120 : 200);
            G.Fx.Text(spot + new Vector2(0, -r - 10), (up ? "ASCENDED " : "") + sp.Name.ToUpperInvariant(), col, 10, 0.9f);
        }
    }

    private static IEnumerable<Enemy> FoesNear(Vector2 at, float r) => G.Enemies.ToArray()
        .Where(e => !e.Dead && e.CanBeHit && e.GlobalPosition.DistanceTo(at) <= r + e.HitRadius && G.Cave.LineClear(at, e.GlobalPosition));

    // ---------------------------------------------------------------- the Swordsman's Whirlwind (Cyclone; Blade Storm)

    private float WhirlRadius => Tune.Big.WhirlRadius * SR * (BigForm("a") ? Tune.Growth.CycloneRadius : 1f);

    private bool StartWhirl()
    {
        CutSwing();
        CutDodge();
        _whirlPulses = Tune.Big.WhirlPulses + Tune.Growth.WhirlExtraPulses * Stats.SpecialExtra;
        _whirlT = Tune.Big.WhirlTime / Tune.Big.WhirlPulses * _whirlPulses;
        _whirlTick = 0f;
        _whirlHits = 0;
        _swingT = -1;
        G.Sfx.Play("swing_heavy", GlobalPosition, -2, 0.05f, 0.8f);
        G.Fx.Ring(GlobalPosition + new Vector2(0, -4), WhirlRadius, new Color(Elements.ColorOf(Mode), 0.6f), 0.3f, FxLayer.RingStyle.Sweep);
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
        // (the blade held straight out, level, the body sweeping round behind it: one turn a stroke)
        float stroke = Tune.Big.WhirlTime / Tune.Big.WhirlPulses;
        if (!Anim.Once("whirl", 3, (8f / 24f) / Math.Max(0.05f, stroke))) Anim.Once(_whirlHits % 2 == 0 ? "slash_a_fwd" : "slash_b_fwd", 3, 3f);
        var c = GlobalPosition + new Vector2(0, -4);
        var col = Elements.ColorOf(Mode);
        float r = WhirlRadius;
        foreach (var e in FoesNear(c, r))
        {
            var away = (e.GlobalPosition - c).LengthSquared() > 1f ? (e.GlobalPosition - c).Normalized() : Vector2.Up;
            // (round and round: each stroke flings them a little along the spin, and outward; a Cyclone draws them in)
            var spin = new Vector2(-away.Y, away.X) * Facing;
            var push = BigForm("a") ? (-away * 0.9f + spin * 0.6f) * 130f : (away + spin * 0.6f) * 120f;
            BigHit(e, Tune.Big.WhirlDamage, push, c, 1f / _whirlPulses);
        }
        // (a Blade Storm: every other stroke flings a crescent of steel out each way)
        if (BigForm("b") && _whirlHits % 2 == 0)
            foreach (float side in new[] { 1f, -1f })
                G.Spawn(new SwordWave { Position = c, Dir = new Vector2(side, 0), Damage = Tune.Big.WhirlDamage * Tune.Growth.BladeStormWave * Stats.DamageMult * Stats.SpecialDamageMult, Range = Tune.Growth.BladeStormRange });
        // (a level sweep: a flat ring of light all round at the height of the blade, and glints along a flat ellipse)
        G.Fx.Ring(c, r * 0.9f, new Color(col.Lerp(Colors.White, 0.5f), 0.85f), 0.2f, FxLayer.RingStyle.Sweep);
        for (int k = 0; k < 10; k++)
        {
            float a = k / 10f * Mathf.Tau + _whirlHits;
            var p = c + new Vector2(MathF.Cos(a), MathF.Sin(a) * 0.16f) * r * 0.85f;
            G.Fx.Glint(p, col.Lerp(Colors.White, 0.4f), 5);
        }
        G.Sfx.Play("swing", c, -6, 0.15f, 1.1f + 0.05f * _whirlHits);
        if (_whirlHits >= _whirlPulses) { _whirlT = 0f; Infuse(c, r); }
    }

    // ---------------------------------------------------------------- the Elementalist's Meteor (Meteor Shower; Cataclysm)

    private bool CallMeteor(Vector2 aim)
    {
        if (Math.Abs(aim.X) > 0.15f) Facing = Math.Sign(aim.X);
        Anim.Face((int)Facing, instant: true);
        var spot = FixedSpot(aim);
        // (it lands on what's under the spot: the floor beneath, if there is one near)
        if (G.Cave.FindFloor(spot, 160f, out var floor)) spot = floor + new Vector2(0, -6);
        var el = Mode;
        var col = Elements.ColorOf(el);
        // (a Cataclysm is one vast stone; a Shower, several; each "again" one more, smaller)
        bool shower = BigForm("a"), cata = BigForm("b");
        float r = Tune.Big.MeteorRadius * SR * (cata ? Tune.Growth.CataclysmRadius : 1f);
        float power = cata ? Tune.Growth.CataclysmDamage : 1f;
        int stones = (shower ? Tune.Growth.ShowerStones : 1) + Stats.SpecialExtra;
        for (int i = 0; i < stones; i++)
        {
            bool main = i == 0 && !shower;
            var at = spot;
            if (i > 0 || shower)
            {
                // (the others fall about the spot, to either side)
                float off = (shower ? (i - (stones - 1) * 0.5f) * Tune.Growth.ShowerSpread : (i % 2 == 1 ? 1f : -1f) * (40f + 30f * i));
                at = spot + new Vector2(off, -30f);
                if (G.Cave.FindFloor(at, 200f, out var f2)) at = f2 + new Vector2(0, -6);
            }
            float sr = main ? r : r * (shower ? Tune.Growth.ShowerRadius : Tune.Growth.ExtraStoneRadius);
            float sp = main ? power : (shower ? Tune.Growth.ShowerDamage : Tune.Growth.ExtraStoneDamage);
            var from = at + new Vector2(-Facing * Tune.Big.MeteorDrift, -Tune.Big.MeteorHeight);
            var stone = new SkyStone { Position = from, Target = at, El = el, Caster = this, Seconds = Tune.Big.MeteorFall + 0.12f * i, Radius = sr, Power = sp };
            G.Spawn(stone);
            if (i == 0) LastMeteor = stone;
            // (each spot is marked as it comes)
            G.Fx.Ring(at, sr, new Color(col, 0.6f), stone.Seconds, FxLayer.RingStyle.Dashed);
            G.Fx.Converge(at, sr * 0.7f, new Color(col, 0.7f), 10, stone.Seconds);
        }
        Anim.Once("rupture", 3, 1.2f);
        _castGlow = 1f;
        G.Sfx.Play("roar", spot, -10, 0.1f, 0.5f);
        G.Main.Rumble(0.3f, 0.2f, 0.25f);
        return true;
    }

    /// <summary>A meteor strikes: a wide burst, harder at its heart; it leaves its element burning on the spot.</summary>
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
            BigHit(e, Tune.Big.MeteorDamage * m.Power * fall, (away + Vector2.Up * 0.8f).Normalized() * 260f, spot, Math.Min(1f, m.Power));
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
        Infuse(spot, r);
        float big = Math.Clamp(r / Tune.Big.MeteorRadius, 0.5f, 2f);
        G.Fx.Explosion(spot, col, 2.2f * big);
        G.Fx.Shockwave(spot + new Vector2(0, 4), r * 1.2f, new Color(col, 0.9f));
        G.Fx.Ring(spot, r, new Color(col.Lerp(Colors.White, 0.4f), 0.95f), 0.4f, FxLayer.RingStyle.Burst);
        G.Fx.Burst(spot, col, (int)(40 * big), 260, 3f, 0.7f, 300f);
        G.Fx.Dust(spot + new Vector2(0, 6), 10, 1.6f);
        G.Fx.ScreenFlash(col, 0.25f * big);
        G.Fx.AddShake(9f * big);
        G.Sfx.Play("slam", spot, 2, 0.05f, 0.6f / big);
        G.Sfx.Play("rock", spot, -2, 0.1f, 0.5f);
        G.Main.Rumble(0.8f, 0.6f, 0.35f);
    }

    // ---------------------------------------------------------------- the Warden's Great Bash (Shockwave; Bulwark)

    private bool StartGreatBash(Vector2 aim)
    {
        if (!IsWarden) return false;
        CutShieldMoves();
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

    /// <summary>The shove lands (<paramref name="echo"/>: an aftershock, softer, with no step on).</summary>
    private void GreatBashLands(float power, bool echo)
    {
        var d = _gbashDir;
        var origin = GlobalPosition + new Vector2(0, -3);
        float r = Tune.Big.BashRadius * SR * (echo ? 0.85f : 1f);
        float cos = MathF.Cos(Mathf.DegToRad(Tune.Big.BashArc));
        // (the shove carries the Warden a step on with it)
        if (!echo) Velocity = new Vector2(d.X * Tune.Big.BashLunge, Velocity.Y);
        foreach (var e in FoesNear(origin, r))
        {
            var to = e.GlobalPosition - origin;
            if (to.Length() > 12f && to.Normalized().Dot(d) < cos) continue;
            var push = (to.Normalized() + d).Normalized() * Tune.Big.BashPush * (echo ? 0.6f : 1f);
            BigHit(e, Tune.Big.BashDamage * power, push, origin, echo ? 0.5f : 1f);
            if (!e.Dead) e.Interrupt(push, Tune.Big.BashStun * (echo ? 0.5f : 1f) * (e.Elite || e.IsGuardian ? 0.5f : 1f), "STUNNED");
        }
        if (!echo)
        {
            // (an infusion fills the ground before the Warden)
            Infuse(origin + d * r * 0.55f, r);
            foreach (var pr in G.Main.EnemyProjectiles.ToArray())
                if (!pr.IsQueuedForDeletion() && (pr.GlobalPosition - origin).Length() < r && (pr.GlobalPosition - origin).Normalized().Dot(d) > cos) pr.Reflect(d, Stats.DamageMult);
            // (each "again": an aftershock on the heels of it)
            for (int k = 0; k < Stats.SpecialExtra; k++) _bigLater.Add((Tune.Growth.AftershockGap * (k + 1), () => GreatBashLands(Tune.Growth.AftershockDamage, true)));
            // (a Shockwave runs on along the ground; a Bulwark wards the Warden and those beside him)
            if (BigForm("a"))
                for (int k = 0; k < Tune.Growth.ShockwaveSteps; k++)
                {
                    float dist = Tune.Growth.ShockwaveStart + Tune.Growth.ShockwaveStep * k;
                    var at = origin + d * dist;
                    _bigLater.Add((0.06f * (k + 1), () => ShockwaveAt(at, d)));
                }
            if (BigForm("b"))
            {
                GiveBarrier(Stats.MaxHp * Tune.Growth.BulwarkSelf, Tune.Growth.BulwarkSeconds);
                foreach (var h in HeroesNear(Tune.Growth.BulwarkRange)) if (h != this) h.GiveBarrier(h.Stats.MaxHp * Tune.Growth.BulwarkAllies, Tune.Growth.BulwarkSeconds);
                G.Fx.Ring(GlobalPosition, Tune.Growth.BulwarkRange * 0.6f, new Color(0.85f, 0.9f, 1f, 0.8f), 0.4f, FxLayer.RingStyle.Wave);
            }
        }
        var front = origin + d * 22f;
        G.Fx.Shockwave(front + new Vector2(0, 14), r, new Color(0.95f, 0.9f, 0.7f, echo ? 0.6f : 0.9f));
        for (int k = 0; k < (echo ? 1 : 3); k++) G.Fx.Directional(front, d, 0.9f, new Color(0.9f, 0.85f, 0.7f, 0.8f), 14, 320, 2.4f, 0.4f, 0, 0);
        G.Fx.Dust(GlobalPosition + new Vector2(d.X * 20f, 12), 8, 1.4f);
        G.Fx.Ring(front, r * 0.6f, new Color(1f, 0.95f, 0.75f, 0.9f), 0.3f, FxLayer.RingStyle.Wave);
        G.Fx.AddShake(echo ? 4f : 7f);
        G.Sfx.Play("slam", front, echo ? -4 : 0, 0.05f, echo ? 0.9f : 0.75f);
        G.Main.Rumble(0.7f, 0.5f, 0.25f);
    }

    /// <summary>One step of a Shockwave running along the ground.</summary>
    private void ShockwaveAt(Vector2 at, Vector2 d)
    {
        if (G.Cave.FindFloor(at + new Vector2(0, -20f), 80f, out var f)) at = f + new Vector2(0, -8f);
        else if (G.Cave.IsSolid(at)) return;
        foreach (var e in FoesNear(at, Tune.Growth.ShockwaveRadius))
        {
            var push = (d + Vector2.Up * 0.5f).Normalized() * 200f;
            BigHit(e, Tune.Big.BashDamage * Tune.Growth.ShockwaveDamage, push, at, 0.3f);
            if (!e.Dead) e.Interrupt(push, Tune.Growth.ShockwaveStun, "STUNNED");
        }
        G.Fx.Dust(at + new Vector2(0, 6), 6, 1.2f);
        G.Fx.Debris(at, new Color(0.5f, 0.45f, 0.4f), 4, 140);
        G.Fx.Ring(at, Tune.Growth.ShockwaveRadius, new Color(0.95f, 0.9f, 0.7f, 0.6f), 0.18f, FxLayer.RingStyle.Burst);
        G.Sfx.Play("rock", at, -10, 0.15f, 0.9f);
    }

    // ---------------------------------------------------------------- the Vitalist's Bloom (Overgrowth; Heartbloom)

    private bool Bloom()
    {
        if (!IsVitalist) return false;
        BloomPulse(1f);
        for (int k = 0; k < Stats.SpecialExtra; k++) _bigLater.Add((Tune.Growth.BloomPulseGap * (k + 1), () => BloomPulse(Tune.Growth.BloomPulseDamage)));
        if (BigForm("a")) { _gardenT = Tune.Growth.GardenSeconds; _gardenTick = 0f; _gardenAt = GlobalPosition + new Vector2(0, -6); }
        Anim.Once("hex", 3, 1.2f);
        _castGlow = 1f;
        return true;
    }

    private void BloomPulse(float power)
    {
        var c = GlobalPosition + new Vector2(0, -6);
        float r = Tune.Big.BloomRadius * SR;
        var col = Elements.ColorOf(Mode);
        int struck = 0;
        foreach (var e in FoesNear(c, r))
        {
            var away = (e.GlobalPosition - c).LengthSquared() > 1f ? (e.GlobalPosition - c).Normalized() : Vector2.Up;
            BigHit(e, Tune.Big.BloomDamage * power, away * 160f + new Vector2(0, -60f), c, 0f, DamageKind.Nature);
            if (!e.Dead) e.Seep(Mode, Tune.Elements.HexSeep * Tune.Big.BloomSeep * power, Stats.HexSeconds, Stats.DamageMult, this);
            struck++;
        }
        // (a Heartbloom mends twice as much, and more for every creature it tore at)
        float heal = Tune.Big.BloomHeal * power * (BigForm("b") ? Tune.Growth.HeartbloomHeal : 1f) + (BigForm("b") ? Tune.Growth.HeartbloomPerFoe * Math.Min(struck, 10) : 0f);
        foreach (var h in HeroesNear(r)) h.Heal(h.Stats.MaxHp * heal);
        ElementWorld.Touch(Mode, c, r);
        if (power >= 1f) Infuse(c, r);
        G.Fx.Converge(c, r * 0.6f, new Color(LifeColor, 0.8f), 12, 0.2f);
        G.Fx.Shockwave(c + new Vector2(0, 16), r, new Color(LifeColor, 0.85f));
        G.Fx.Ring(c, r, new Color(col, 0.9f), 0.45f, FxLayer.RingStyle.Burst);
        G.Fx.Ring(c, r * 0.6f, new Color(LifeColorLight, 0.8f), 0.35f, FxLayer.RingStyle.Wave);
        G.Fx.Burst(c, LifeColor, (int)(36 * power), 240, 2.6f, 0.6f, -40f);
        G.Fx.Burst(c, col, (int)(20 * power), 180, 2.2f, 0.5f, 0f);
        G.Fx.Flash(c, r * 0.5f, LifeColorLight, 0.25f);
        G.Fx.AddShake(5f * power);
        G.Sfx.Play("heal", c, 0, 0.05f, 0.7f);
        G.Sfx.Play("drain", c, -2, 0.05f, 0.6f);
        G.Main.Rumble(0.5f, 0.4f, 0.2f);
    }

    /// <summary>An Overgrowth's garden: friends in it mend, creatures in it take her element.</summary>
    private void TickGarden(float dt)
    {
        _gardenT -= dt;
        if ((_gardenTick -= dt) > 0f) return;
        _gardenTick = 0.5f;
        float r = Tune.Growth.GardenRadius * SR;
        foreach (var h in G.Players.ToArray())
            if (h != null && IsInstanceValid(h) && !h.Dead && h.GlobalPosition.DistanceTo(_gardenAt) <= r) h.Heal(h.Stats.MaxHp * Tune.Growth.GardenHeal);
        foreach (var e in FoesNear(_gardenAt, r)) e.Seep(Mode, Tune.Elements.HexSeep * 0.5f, 1f, Stats.DamageMult, this);
        G.Fx.Ring(_gardenAt, r, new Color(LifeColor, 0.45f), 0.5f, FxLayer.RingStyle.Dashed);
        G.Fx.Burst(_gardenAt + new Vector2(G.Range(-r * 0.6f, r * 0.6f), 8f), new Color(LifeColorLight, 0.8f), 4, 50, 2f, 0.6f, -60f);
    }

    // ---------------------------------------------------------------- the Aegis's Judgement (Sanctum; Chain of Light)

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
        G.Fx.Ring(spot, Tune.Big.JudgeRadius * SR, new Color(gold, 0.6f), Tune.Big.JudgeWindup, FxLayer.RingStyle.Dashed);
        G.Fx.Beam(spot + new Vector2(0, -420f), spot, new Color(gold, 0.25f));
        Anim.Once("heal", 3, 1.4f);
        _castGlow = 1f;
        G.Sfx.Play("levelup", spot, -8, 0.05f, 0.7f);
        return true;
    }

    /// <summary>A pillar of light falls on <paramref name="spot"/> (<paramref name="main"/>: the stroke's own, which wards and calls the rest).</summary>
    private void JudgementLands(Vector2 spot, float power, float size, bool main)
    {
        float r = Tune.Big.JudgeRadius * SR * size;
        var gold = new Color(1f, 0.9f, 0.5f);
        Infuse(spot, r);
        foreach (var e in FoesNear(spot, r))
        {
            var away = (e.GlobalPosition - spot).LengthSquared() > 1f ? (e.GlobalPosition - spot).Normalized() : Vector2.Up;
            BigHit(e, Tune.Big.JudgeDamage * power, away * 180f + new Vector2(0, -80f), spot, main ? 1f : 0.5f);
            if (!e.Dead) e.Weaken(Tune.Big.JudgeWeaken, Tune.Big.JudgeWeakenSeconds);
        }
        if (main)
        {
            // (the friends near the Aegis, or near where it fell, are warded)
            foreach (var h in G.Players.ToArray())
            {
                if (h == null || !IsInstanceValid(h) || h.Dead) continue;
                if (h.GlobalPosition.DistanceTo(spot) > Tune.Big.JudgeWardRange && h.GlobalPosition.DistanceTo(GlobalPosition) > Tune.Big.JudgeWardRange) continue;
                h.GiveBarrier(h.Stats.MaxHp * Tune.Big.JudgeBarrier, Tune.Big.JudgeBarrierSeconds);
            }
            // (each "again", and a Chain of Light's links: more pillars, on the creatures nearest that it didn't strike)
            int more = Stats.SpecialExtra + (BigForm("b") ? Tune.Growth.ChainPillars : 0);
            var marks = G.Enemies.Where(e => !e.Dead && e.CanBeHit && e.GlobalPosition.DistanceTo(spot) > r && e.GlobalPosition.DistanceTo(spot) < Tune.Growth.ChainRange)
                .OrderBy(e => e.GlobalPosition.DistanceSquaredTo(spot)).Take(more).ToList();
            for (int k = 0; k < marks.Count; k++)
            {
                var at = marks[k].GlobalPosition;
                if (G.Cave.FindFloor(at, 80f, out var f)) at = f + new Vector2(0, -6);
                _bigLater.Add((Tune.Growth.ChainGap * (k + 1), () => JudgementLands(at, Tune.Growth.ChainDamage, Tune.Growth.ChainSize, false)));
            }
            // (an "again" with no creature left outside the light falls on the same spot once more)
            for (int k = marks.Count; k < Stats.SpecialExtra; k++)
            {
                var at = spot + new Vector2(G.Range(-12f, 12f), 0f);
                _bigLater.Add((Tune.Growth.ChainGap * (k + 1), () => JudgementLands(at, Tune.Growth.ChainDamage, Tune.Growth.ChainSize, false)));
            }
            if (BigForm("a")) { _sanctumT = Tune.Growth.SanctumSeconds; _sanctumTick = 0.5f; _sanctumAt = spot; }
        }
        for (int k = 0; k < (main ? 3 : 1); k++) G.Fx.Beam(spot + new Vector2(G.Range(-10, 10), -480f), spot, new Color(gold, 0.9f));
        G.Fx.Explosion(spot, gold, 1.6f * size);
        G.Fx.Shockwave(spot + new Vector2(0, 6), r * 1.1f, new Color(gold, 0.9f));
        G.Fx.Ring(spot, r, new Color(1f, 0.97f, 0.85f, 0.95f), 0.35f, FxLayer.RingStyle.Burst);
        G.Fx.Burst(spot, gold, (int)(30 * size), 220, 2.6f, 0.6f, -60f);
        if (main) G.Fx.ScreenFlash(gold, 0.2f);
        G.Fx.AddShake(main ? 7f : 3f);
        G.Sfx.Play("slam", spot, main ? 0 : -6, 0.05f, 0.9f);
        G.Sfx.Play("levelup", spot, -4, 0.05f, 0.6f);
        G.Main.Rumble(0.6f, 0.4f, 0.25f);
    }

    /// <summary>A Sanctum: the light stays where it fell, searing what stands in it.</summary>
    private void TickSanctum(float dt)
    {
        _sanctumT -= dt;
        if ((_sanctumTick -= dt) > 0f) return;
        _sanctumTick = 0.5f;
        float r = Tune.Big.JudgeRadius * SR;
        var gold = new Color(1f, 0.9f, 0.5f);
        foreach (var e in FoesNear(_sanctumAt, r)) BigHit(e, Tune.Big.JudgeDamage * Tune.Growth.SanctumDamage, Vector2.Zero, _sanctumAt, 0.15f);
        G.Fx.Beam(_sanctumAt + new Vector2(G.Range(-r * 0.5f, r * 0.5f), -420f), _sanctumAt, new Color(gold, 0.5f));
        G.Fx.Ring(_sanctumAt, r, new Color(gold, 0.5f), 0.4f, FxLayer.RingStyle.Dashed);
    }

    // ---------------------------------------------------------------- the Rogue's Death Blossom (Shadow Marks; Whirling Steel)

    private bool StartBlossom()
    {
        if (!IsRogue) return false;
        CutTether();
        _blossomMax = Tune.Big.BlossomVolleys + Stats.SpecialExtra + (BigForm("b") ? Tune.Growth.SteelVolleys : 0);
        float tick = Tune.Big.BlossomTime / Tune.Big.BlossomVolleys;
        _blossomT = tick * _blossomMax;
        _blossomTick = 0f;
        _blossomVolleys = 0;
        _iframes = Math.Max(_iframes, _blossomT + 0.1f);
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
        int n = Tune.Big.BlossomKnives + (BigForm("b") ? Tune.Growth.SteelKnives : 0);
        float range = Tune.Big.BlossomRange * SR;
        float turn = _blossomVolleys * Mathf.Pi / n;
        // (the knives find the creatures about: one at each, nearest first, the rest flung out all around)
        var marks = FoesNear(c, range).OrderBy(e => e.GlobalPosition.DistanceSquaredTo(c)).Take(n).ToList();
        for (int k = 0; k < n; k++)
        {
            float a = k / (float)n * Mathf.Tau + turn;
            var dir = k < marks.Count ? (marks[k].GlobalPosition - c).Normalized().Rotated(G.Range(-0.04f, 0.04f)) : new Vector2(MathF.Cos(a), MathF.Sin(a));
            // (a knife thrown at a creature in sight flies to it; one flung out flies until it meets rock)
            Vector2 end;
            if (k < marks.Count) end = marks[k].GlobalPosition + dir * marks[k].HitRadius;
            else G.Cave.Raycast(c, dir, range, out end, 2f);
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
                BigHit(hit, Tune.Big.BlossomDamage, dir * 60f, c, 1f / (_blossomMax * 2f));
                // (Shadow Marks: what a knife finds is laid open to every blow for a while)
                if (BigForm("a") && !hit.Dead) hit.Expose(Tune.Growth.MarkVulnerable, Tune.Growth.MarkSeconds);
                G.Fx.Spark(hit.GlobalPosition, -dir, true, col.Lerp(Colors.White, 0.5f));
            }
        }
        G.Sfx.Play("throw", c, -6, 0.15f, 1.2f + 0.08f * _blossomVolleys);
        if (_blossomVolleys >= _blossomMax) { _blossomT = 0f; Infuse(c, range * 0.6f); }
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
    /// <summary>Its blow, x the meteor's own (a Cataclysm's more, a shower's stones less).</summary>
    public float Power = 1f;
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
