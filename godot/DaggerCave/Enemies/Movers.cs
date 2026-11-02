using System;
using System.Linq;
using Godot;

namespace DaggerCave;

// ============================================================================ creatures that hold other heights than the floor
// (Tune.Movers; generators send them, Generator.TryMover)

/// <summary>
/// A mite: a glowing midge the size of a thumb, one of a swarm. A swarm flies as one cloud: each mite keeps to its fellows (drawn to the
/// middle of the swarm, matching their flight, never quite touching), and the cloud circles its hero's head, now and then one darting in
/// to bite. Alone, a mite is nothing (a few points of health); a swarm is a cloud to sweep with one wide blow.
/// </summary>
public partial class Mite : Enemy
{
    /// <summary>The swarm it flies with (the mites released together).</summary>
    public int Flock;
    private static int _nextFlock;
    /// <summary>A new swarm's number.</summary>
    public static int NewFlock() => ++_nextFlock;

    private float _phase, _dartT, _dartCd;
    private Vector2 _dartDir;
    /// <summary>For the tests: darts in to bite.</summary>
    public int Darts { get; private set; }
    public override void NetState(NetIO io) => io.Sync(ref Flock);

    protected override bool UsesGravity => false;
    protected override bool Striking => _dartT > 0f;

    public Mite() { MaxHp = Tune.Movers.MiteHp; BodyRadius = 3.5f; Size = 1.5f; ContactDamage = Tune.Movers.MiteBite; XpValue = 1; }

    protected override void Setup()
    {
        DisplayName = "Mite";
        UseSprite("mite");
        MotionMode = MotionModeEnum.Floating;
        _phase = G.Range(0f, Mathf.Tau);
        _dartCd = G.Range(1.2f, 3.5f);
    }

    protected override Color BloodColor => new(0.95f, 0.85f, 0.35f);
    public override string HitSound => "hit_wood";

    protected override void Think(float dt)
    {
        var me = GlobalPosition;
        if (!Awake)
        {
            Velocity = Velocity.MoveToward(new Vector2(MathF.Sin(T * 3f + _phase) * 25f, MathF.Cos(T * 2.3f + _phase) * 18f), 300f * dt);
            return;
        }
        // the swarm: the middle of the fellows near, their flight, and a little room from each
        Vector2 sum = Vector2.Zero, vel = Vector2.Zero, apart = Vector2.Zero;
        int n = 0;
        foreach (var e in G.Enemies)
        {
            if (e == this || e is not Mite m || m.Dead || m.Flock != Flock) continue;
            var d = me - m.GlobalPosition;
            float dist2 = d.LengthSquared();
            if (dist2 > Tune.Movers.MiteFlockRange * Tune.Movers.MiteFlockRange) continue;
            sum += m.GlobalPosition; vel += m.Velocity; n++;
            if (dist2 < 196f && dist2 > 0.01f) apart += d / dist2 * 14f;
        }
        var to = ToP;
        // (each circles its hero's head on its own loop, all of it above their shoulders)
        var loop = new Vector2(MathF.Cos(_phase + T * 1.7f), MathF.Sin(_phase * 1.3f + T * 2.2f) * 0.45f) * Tune.Movers.MiteOrbit;
        var seek = (to + new Vector2(0, -Tune.Movers.MiteOrbit * 0.45f - 18f) + loop).LimitLength(Tune.Movers.MiteSpeed) * 1.6f;
        var want = seek;
        if (n > 0) want += (sum / n - me) * 1.1f + (vel / n) * 0.3f;
        want += apart * 40f;
        want = want.LimitLength(Tune.Movers.MiteSpeed);
        // now and then, one darts in to bite
        _dartCd -= dt;
        if (_dartT > 0f)
        {
            _dartT -= dt;
            Velocity = _dartDir * Tune.Movers.MiteDart;
            // (a dart that meets rock glances off it, back up into the air)
            if (IsOnFloor() || IsOnWall() || IsOnCeiling()) { _dartT = 0f; Velocity = new Vector2(Velocity.X * 0.3f, -140f); }
        }
        else
        {
            if (_dartCd <= 0f && DistP < Tune.Movers.MiteDartRange && SeesP)
            {
                _dartT = 0.32f;
                Darts++;
                _dartDir = to.LengthSquared() > 1f ? to.Normalized() : Vector2.Down;
                _dartCd = G.Range(Tune.Movers.MiteDartEvery * 0.7f, Tune.Movers.MiteDartEvery * 1.3f);
            }
            Velocity = Velocity.MoveToward(want, 700f * dt);
        }
        if (me.Y > G.Cave.WaterY - 12) Velocity = new Vector2(Velocity.X, Math.Min(Velocity.Y, -60f));
        if (Math.Abs(Velocity.X) > 6f) Face = Math.Sign(Velocity.X);
    }

    protected override void Animate() => Anim.Loop(_dartT > 0f ? "dive" : "fly");
    public override void _Draw() { }
}

/// <summary>
/// A delver: a blind, shovel-snouted thing like a mole the length of a hound, with digging claws. It swims through rock as through water:
/// you see only a ridge of dust running along the floor toward you. It comes up beside you, beyond your guard (cracks, a puff of dust: a
/// moment to step away), bursts out of the ground throwing what stands there into the air, bites at you for a while, and goes back down.
/// It can only be struck while it's up.
/// </summary>
public partial class Delver : Enemy
{
    private int _s; // 0 under the ground, 1 breaking through, 2 up, 3 going down
    private float _st, _bite, _biteT, _dustT, _side;
    private Vector2 _spot;
    public override void NetState(NetIO io) => io.Sync(ref _s);

    /// <summary>For the tests: times it broke through.</summary>
    public int Eruptions { get; private set; }
    public bool Buried => _s == 0 || _s == 1;

    protected override bool UsesGravity => false;
    public override bool CanBeHit => !Buried;
    protected override bool Striking => _biteT > 0f || (_s == 2 && _st < 0.25f);
    public override bool Attacking => _s == 1 || Striking;
    protected override bool Busy => _s != 2;

    public Delver() { MaxHp = Tune.Movers.DelverHp; BodyRadius = 8f; Size = 1.4f; ContactDamage = Tune.Movers.DelverBite; XpValue = 5; KnockResist = 1f; }

    protected override void Setup()
    {
        DisplayName = "Delver";
        UseSprite("delver");
        MotionMode = MotionModeEnum.Floating;
        ManualMove = true;
        CollisionMask = 0;
        Anim.Visible = false;
        _st = G.Range(0f, 0.5f);
    }

    protected override Color BloodColor => new(0.55f, 0.25f, 0.2f);

    /// <summary>Where it will come up: on the floor beside its hero, on the side away from where it comes from.</summary>
    private bool SpotNear(out Vector2 spot)
    {
        var h = P.GlobalPosition;
        // (the side is chosen once a run under the ground begins: the far side from where it set off)
        if (_side == 0) { _side = Math.Sign(h.X - GlobalPosition.X); if (_side == 0) _side = 1; }
        float side = _side;
        foreach (float dx in new[] { side * Tune.Movers.DelverBeside, -side * Tune.Movers.DelverBeside, side * Tune.Movers.DelverBeside * 2f })
        {
            if (G.Cave.FindFloor(h + new Vector2(dx, -12f), 140f, out var floor) && !G.Cave.IsWater(floor + new Vector2(0, -6)) && !G.Cave.IsSolid(floor + new Vector2(0, -14)))
            { spot = floor; return true; }
        }
        spot = default;
        return false;
    }

    protected override void Think(float dt)
    {
        _st += dt;
        _biteT -= dt;
        Velocity = Vector2.Zero;
        switch (_s)
        {
            case 0:
            {
                // under the ground: it runs toward the spot beside its hero, a ridge of dust along the floor above it
                if (!Awake) return;
                bool has = SpotNear(out var spot);
                var goal = has ? spot + new Vector2(0, Tune.Movers.DelverDepth) : P.GlobalPosition + new Vector2(0, 60f);
                var d = goal - GlobalPosition;
                float step = Tune.Movers.DelverSpeed * dt;
                GlobalPosition += d.Length() <= step ? d : d.Normalized() * step;
                if (Math.Abs(d.X) > 2f) Face = Math.Sign(d.X);
                if ((_dustT -= dt) <= 0f)
                {
                    _dustT = 0.12f;
                    if (G.Cave.FindFloor(GlobalPosition + new Vector2(0, -90f), 130f, out var above))
                        G.Fx.Burst(above + new Vector2(0, -2), new Color(0.55f, 0.47f, 0.38f, 0.8f), 2, 50, 1.6f, 0.4f, 200);
                }
                if (has && d.Length() < 6f && _st > Tune.Movers.DelverUnder)
                {
                    _s = 1; _st = 0; _spot = spot; _side = 0;
                    G.Sfx.Play("rock", spot, -10, 0.1f, 0.6f);
                }
                break;
            }
            case 1:
                // breaking through: cracks and dust where it will come up
                GlobalPosition = _spot + new Vector2(0, Tune.Movers.DelverDepth * (1f - _st / Tune.Movers.DelverWarn));
                if (G.Chance(0.5f)) G.Fx.Burst(_spot + new Vector2(G.Range(-12, 12), -2), new Color(0.6f, 0.52f, 0.42f, 0.85f), 1, 70, 1.8f, 0.45f, 160);
                if (_st > Tune.Movers.DelverWarn) Erupt();
                break;
            case 2:
            {
                // up: it turns to its hero and bites when they're close
                var to = ToP;
                if (Math.Abs(to.X) > 3f) Face = Math.Sign(to.X);
                if ((_bite -= dt) <= 0f && to.Length() < Tune.Movers.DelverReach && Math.Abs(to.Y) < 30f)
                {
                    _bite = Tune.Movers.DelverBiteEvery;
                    _biteT = 0.25f;
                    GlobalPosition += new Vector2(Face * 6f, 0);
                    Anim.Once("bite", 3, 1.6f);
                }
                if (_st > Tune.Movers.DelverUp) { _s = 3; _st = 0; G.Sfx.Play("rock", GlobalPosition, -12, 0.1f, 0.7f); }
                break;
            }
            default:
                // going down
                GlobalPosition += new Vector2(0, 60f * dt);
                if (G.Chance(0.4f)) G.Fx.Burst(_spot + new Vector2(G.Range(-8, 8), -2), new Color(0.6f, 0.52f, 0.42f, 0.8f), 1, 50, 1.6f, 0.4f, 180);
                if (_st > 0.4f) { _s = 0; _st = 0; CollisionMask = 0; }
                break;
        }
    }

    /// <summary>It bursts out of the ground: what stands over the spot is thrown up and hurt.</summary>
    private void Erupt()
    {
        _s = 2; _st = 0;
        Eruptions++;
        GlobalPosition = _spot + new Vector2(0, -BodyRadius * Size + 2f);
        CollisionMask = G.LayerTerrain;
        foreach (var h in G.Players)
        {
            if (h == null || h.Dead || h.GlobalPosition.DistanceTo(_spot + new Vector2(0, -10)) > Tune.Movers.DelverBurst) continue;
            h.Hurt(Tune.Movers.DelverBurstDamage * DmgK, _spot + new Vector2(0, 10), 260f, this);
        }
        G.Fx.Burst(_spot, new Color(0.55f, 0.47f, 0.38f), 18, 180, 2.4f, 0.5f, 300);
        G.Fx.Debris(_spot, new Color(0.45f, 0.38f, 0.3f), 6, 160);
        G.Fx.Ring(_spot, Tune.Movers.DelverBurst, new Color(0.7f, 0.6f, 0.45f, 0.8f), 0.25f, FxLayer.RingStyle.Burst);
        G.Fx.AddShake(2.5f);
        G.Sfx.Play("rock", _spot, -2, 0.1f, 0.8f);
        Anim.Once("erupt", 3, 1.4f);
    }

    protected override void Animate()
    {
        Anim.Visible = !Buried;
        if (_s == 2 && _biteT <= 0f && _st > 0.4f) Anim.Loop("idle");
    }
    protected override void PuppetAnimate() { if (Anim != null) Anim.Visible = !Buried; }

    public override void _Draw() { if (!Buried) DrawHealthBar(); }
}

/// <summary>
/// A bomber: a bloated, leathery flying beetle with a pale sac slung under it. It holds high over the fight, just under the roof, sliding to
/// and fro over its hero, and lets fall a seed-pod that bursts where it lands (its shadow marks the spot as it falls). Climb to it, or strike
/// it from below.
/// </summary>
public partial class Bomber : Enemy
{
    private float _cd, _phase, _droppedT;
    public override void NetState(NetIO io) => io.Sync(ref _droppedT);
    /// <summary>For the tests: pods let fall.</summary>
    public int Drops { get; private set; }

    protected override bool UsesGravity => false;
    public override bool Attacking => _cd < 0.45f && _cd > 0f;

    public Bomber() { MaxHp = Tune.Movers.BomberHp; BodyRadius = 8f; Size = 1.9f; ContactDamage = 0f; XpValue = 5; KnockResist = 0.3f; }

    protected override void Setup()
    {
        DisplayName = "Bomber";
        UseSprite("bomber");
        MotionMode = MotionModeEnum.Floating;
        _phase = G.Range(0f, Mathf.Tau);
        _cd = G.Range(1.5f, Tune.Movers.BomberEvery);
    }

    protected override Color BloodColor => new(0.55f, 0.65f, 0.25f);

    /// <summary>The height it holds: well over its hero, but under the roof.</summary>
    private float HoldY()
    {
        var h = P.GlobalPosition;
        float y = h.Y - Tune.Movers.BomberHeight;
        G.Cave.Raycast(h + new Vector2(0, -10), Vector2.Up, Tune.Movers.BomberHeight + 40f, out var roof, 4f);
        return Math.Max(y, roof.Y + Tune.Movers.BomberUnderRoof);
    }

    protected override void Think(float dt)
    {
        _cd -= dt; _droppedT -= dt;
        var me = GlobalPosition;
        if (!Awake)
        {
            Velocity = Velocity.MoveToward(new Vector2(MathF.Sin(T + _phase) * 20f, MathF.Sin(T * 1.7f + _phase) * 10f), 200f * dt);
            return;
        }
        var h = P.GlobalPosition;
        // to and fro over its hero, high up
        var goal = new Vector2(h.X + MathF.Sin(T * Tune.Movers.BomberSway + _phase) * Tune.Movers.BomberSpan, HoldY() + MathF.Sin(T * 2.1f + _phase) * 8f);
        var want = (goal - me) * 2f;
        want = want.LimitLength(Tune.Movers.BomberSpeed);
        Velocity = Velocity.MoveToward(want, 260f * dt);
        if (Math.Abs(Velocity.X) > 8f) Face = Math.Sign(Velocity.X);
        // over its hero, with a clear drop: the pod goes
        if (_cd <= 0f && Math.Abs(h.X - me.X) < Tune.Movers.BomberDropWithin && h.Y > me.Y + 40f && G.Cave.LineClear(me, h))
        {
            _cd = Tune.Movers.BomberEvery * G.Range(0.85f, 1.15f);
            Drop();
        }
    }

    private void Drop()
    {
        var from = GlobalPosition + new Vector2(0, 10f);
        var pod = new EnemyProjectile
        {
            Position = from, Vel = new Vector2(Velocity.X * 0.4f, 30f), Grav = Tune.Movers.PodGravity, Radius = 6f,
            Damage = Tune.Movers.PodDamage * DmgK, Kind = "pod", Source = this, Life = 5f,
        };
        G.Spawn(pod);
        Drops++;
        _droppedT = 0.4f;
        // (where it will burst: marked as it falls)
        if (G.Cave.FindFloor(from, 600f, out var floor))
        {
            float fall = MathF.Sqrt(2f * Math.Max(1f, floor.Y - from.Y) / Tune.Movers.PodGravity);
            G.Fx.Ring(floor + new Vector2(pod.Vel.X * fall, -2f), Tune.Movers.PodRadius, new Color(0.75f, 0.85f, 0.3f, 0.7f), fall, FxLayer.RingStyle.Dashed);
        }
        G.Sfx.Play("throw", from, -8, 0.1f, 0.6f);
    }

    protected override void Animate() => Anim.Loop(_droppedT > 0f ? "drop" : "fly");
    public override void _Draw() => DrawHealthBar();
}
