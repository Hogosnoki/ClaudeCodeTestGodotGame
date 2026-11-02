using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

/// <summary>The great moves a boss can have beside its own (see <see cref="Enemy"/>'s BossMoves part).</summary>
public enum BMove { Leap, Charge, Guard, Barrage, Nova, Summon, Blink, Shockline }

/// <summary>
/// Every boss's repertoire (<see cref="Tune.BossMoves"/>): beside what its kind always does (and its second attack), a guardian, a
/// colossus or the dragon has three or four great moves, each with its own wind-up, warning shape (<see cref="FxLayer.Tell"/>), pose and
/// name called out over it:
/// <list type="bullet">
/// <item><b>Leap Slam</b> (moving): crosshairs and a ring where it will land; it bounds there and the ground bursts.</item>
/// <item><b>Charge</b> (moving): chevrons across the room; it thunders along them, and if it meets a wall it reels, open to blows.</item>
/// <item><b>Guard</b>: a turning ring of light; for a while it takes a fraction of every blow, then answers what it took with a riposte.</item>
/// <item><b>Barrage</b>: a fan of aim lines; three volleys of shots.</item>
/// <item><b>Nova</b>: spines growing all round; two rings of shots.</item>
/// <item><b>Summon</b>: rings where its brood will rise; two or three lesser kin join the fight (never more than four).</item>
/// <item><b>Blink</b> (moving): crosshairs behind the hero; it vanishes, appears there, and strikes.</item>
/// <item><b>Shockline</b>: chevrons both ways along the ground; it slams, and a wave runs out each way.</item>
/// </list>
/// Below 40% health it is enraged: everything it does comes quicker, and its great moves more often.
/// </summary>
public partial class Enemy
{
    private sealed class BossKit
    {
        public BMove[] Moves;
        public Color Col;
        public string Shot = "rock";
        /// <summary>What its Summon calls (null: its own kind).</summary>
        public Func<Enemy> Brood;
    }

    /// <summary>Its repertoire: by what it is (only a guardian, a colossus or the dragon has one).</summary>
    private BossKit KitOf()
    {
        if (!IsGuardian && !IsBoss) return null;
        return this switch
        {
            Dragon => new BossKit { Moves = new[] { BMove.Nova, BMove.Barrage, BMove.Guard }, Col = new(1f, 0.5f, 0.15f), Shot = "fire" },
            VentColossus => new BossKit { Moves = new[] { BMove.Shockline, BMove.Nova, BMove.Guard, BMove.Barrage }, Col = new(0.85f, 0.9f, 0.25f), Shot = "acid" },
            EngineWarden => new BossKit { Moves = new[] { BMove.Shockline, BMove.Nova, BMove.Guard, BMove.Barrage }, Col = new(1f, 0.7f, 0.3f), Shot = "fire" },
            CavernColossus => new BossKit { Moves = new[] { BMove.Shockline, BMove.Nova, BMove.Guard, BMove.Barrage }, Col = new(0.85f, 0.75f, 0.6f), Shot = "rock" },
            Spider => new BossKit { Moves = new[] { BMove.Leap, BMove.Nova, BMove.Summon, BMove.Blink }, Col = new(0.92f, 0.92f, 1f), Shot = "spit", Brood = () => new Spider() },
            Goblin => new BossKit { Moves = new[] { BMove.Charge, BMove.Barrage, BMove.Summon, BMove.Guard }, Col = new(1f, 0.6f, 0.3f), Shot = "rock", Brood = () => new Goblin() },
            Bear => new BossKit { Moves = new[] { BMove.Leap, BMove.Charge, BMove.Guard, BMove.Shockline }, Col = new(1f, 0.55f, 0.3f) },
            Scorpion => new BossKit { Moves = new[] { BMove.Barrage, BMove.Summon, BMove.Blink, BMove.Guard }, Col = new(0.55f, 0.95f, 0.3f), Shot = "spit", Brood = () => new Scorpion() },
            Skeleton => new BossKit { Moves = new[] { BMove.Blink, BMove.Barrage, BMove.Summon, BMove.Guard }, Col = new(0.75f, 0.8f, 1f), Shot = "rock", Brood = () => new Skeleton() },
            Golem => new BossKit { Moves = new[] { BMove.Leap, BMove.Shockline, BMove.Guard, BMove.Nova }, Col = new(0.9f, 0.75f, 0.55f), Shot = "rock" },
            Bat => new BossKit { Moves = new[] { BMove.Charge, BMove.Nova, BMove.Summon, BMove.Blink }, Col = new(0.75f, 0.6f, 1f), Shot = "crystal", Brood = () => new Bat() },
            _ => new BossKit { Moves = new[] { BMove.Leap, BMove.Barrage, BMove.Guard, BMove.Nova }, Col = new(1f, 0.6f, 0.4f) },
        };
    }

    private BossKit _bmKit;
    private bool _bmKitMade;
    private BossKit Kit { get { if (!_bmKitMade) { _bmKit = KitOf(); _bmKitMade = true; } return _bmKit; } }
    /// <summary>Its great moves (tests, the build page): empty for anything but a boss.</summary>
    public IReadOnlyList<BMove> BossMoves => Kit?.Moves ?? Array.Empty<BMove>();

    private BMove _bm, _bmLast = (BMove)(-1);
    private int _bmPhase; // 0 resting, 1 winding up, 2 acting, 3 recovering
    private float _bmT, _bmCd = -1f, _bmGuardT, _bmAbsorbed, _bmGuardText;
    private Vector2 _bmAim, _bmDir;
    private readonly HashSet<Player> _bmStruck = new();
    private int _bmVolleys;
    private readonly List<Enemy> _bmBrood = new();
    /// <summary>Below <see cref="Tune.BossMoves.EnrageAt"/> of its health: quicker in all it does.</summary>
    public bool Enraged { get; private set; }
    /// <summary>For the tests: great moves begun, by kind; whether one is under way; and its guard.</summary>
    public readonly Dictionary<BMove, int> BossMoveUses = new();
    public bool InBossMove => _bmPhase != 0;
    public int BossMovePhase => _bmPhase;
    public bool Guarding => _bmGuardT > 0f;
    public int BroodAlive => _bmBrood.Count(e => IsInstanceValid(e) && !e.Dead);
    /// <summary>For the tests: this great move, now.</summary>
    public void BossMoveNow(BMove m) { Awake = true; if (_bmPhase == 0) BossBegin(m); }

    /// <summary>
    /// The great moves, each frame, before its own behaviour (and its second attack): true while one is under way. A boss picks one now
    /// and then when it is free and its hero near and in sight, never the same twice running.
    /// </summary>
    private bool BossTick(float dt)
    {
        var kit = Kit;
        if (kit == null || Tune.Testing.NoSecondary) return false;
        if (!Enraged && Hp < MaxHp * Tune.BossMoves.EnrageAt && Hp > 0)
        {
            Enraged = true;
            G.Fx.Text(HeadPoint(18f), "ENRAGED", new Color(1f, 0.3f, 0.2f), 16, 1.4f);
            G.Fx.Ring(GlobalPosition, HitRadius * 2.5f, new Color(1f, 0.3f, 0.2f), 0.5f, FxLayer.RingStyle.Burst);
            G.Fx.AddShake(6f);
            G.Sfx.Play("roar", GlobalPosition, 0, 0, 0.8f);
        }
        if (_bmGuardT > 0f && _bmPhase == 0) _bmGuardT = 0f;
        if (_bmPhase == 0)
        {
            if (_bmCd < 0f) _bmCd = Tune.BossMoves.FirstAfter;
            if (Awake) _bmCd -= dt;
            if (_bmCd > 0f || !Awake || Attacking || Striking || P == null || P.Dead) return false;
            float d = TrueDistP;
            if (d > Tune.BossMoves.Reach || !SeesP) return false;
            // (a leap or a charge wants room; the rest go anywhere)
            var choices = kit.Moves.Where(m => m != _bmLast && !(m is BMove.Leap or BMove.Charge && d < 70f) && !(m == BMove.Summon && BroodAlive >= Tune.BossMoves.BroodMax)).ToArray();
            if (choices.Length == 0) choices = kit.Moves.Where(m => m != BMove.Summon || BroodAlive < Tune.BossMoves.BroodMax).ToArray();
            if (choices.Length == 0) return false;
            BossBegin(choices[G.Rng.Next(choices.Length)]);
        }
        _bmT += dt;
        switch (_bmPhase)
        {
            case 1: BossWindup(dt); break;
            case 2: BossAct(dt); break;
            case 3: BossRecover(dt); break;
        }
        if (Dead) return true;
        if (ManualMove && _bmPhase != 0) MoveAndSlide();
        return true;
    }

    private float BossWindupTime => _bm switch
    {
        BMove.Guard => 0.45f, BMove.Blink => 0.7f, BMove.Leap => 0.85f, BMove.Charge => 0.9f, BMove.Summon => 1f, _ => 0.8f,
    } * (Enraged ? 0.8f : 1f);

    private static string BossMoveName(BMove m) => m switch
    {
        BMove.Leap => "LEAP SLAM", BMove.Charge => "CHARGE", BMove.Guard => "GUARD", BMove.Barrage => "BARRAGE", BMove.Nova => "NOVA",
        BMove.Summon => "SUMMON", BMove.Blink => "BLINK", _ => "SHOCKWAVE",
    };

    private void BossBegin(BMove m)
    {
        var kit = Kit;
        if (kit == null) return;
        // (its second attack, if one was winding up, gives way)
        Move2Interrupt();
        _bm = m; _bmLast = m; _bmPhase = 1; _bmT = 0f; _bmStruck.Clear(); _bmVolleys = 0;
        BossMoveUses[m] = BossMoveUses.GetValueOrDefault(m) + 1;
        _bmAim = P?.GlobalPosition ?? GlobalPosition;
        _bmDir = (_bmAim - GlobalPosition).Normalized();
        if (_bmDir == Vector2.Zero) _bmDir = new Vector2(Face, 0);
        if (Math.Abs(_bmDir.X) > 0.05f) Face = Math.Sign(_bmDir.X);
        float w = BossWindupTime;
        var c = kit.Col;
        switch (m)
        {
            case BMove.Leap:
                G.Fx.Tell(FxLayer.TellKind.Reticle, GlobalPosition, _bmAim, 26f, w + 0.75f, c);
                G.Fx.Tell(FxLayer.TellKind.Ground, GlobalPosition, _bmAim + new Vector2(0, 10), Tune.BossMoves.SlamRadius, w + 0.75f, c);
                break;
            case BMove.Charge:
            {
                var dir = UsesGravity && !InWater ? new Vector2(Math.Sign(_bmDir.X == 0 ? Face : _bmDir.X), 0) : _bmDir;
                _bmDir = dir;
                G.Fx.Tell(FxLayer.TellKind.Charge, GlobalPosition, GlobalPosition + dir * 320f, 0, w, c);
                break;
            }
            case BMove.Guard: G.Fx.Tell(FxLayer.TellKind.Whirl, GlobalPosition, GlobalPosition, HitRadius * 1.5f + 10f, w + Tune.BossMoves.GuardSeconds, new Color(0.6f, 0.85f, 1f)); break;
            case BMove.Barrage: G.Fx.Tell(FxLayer.TellKind.Aim, BossMouth, _bmAim, 0.2f, w, c); break;
            case BMove.Nova: G.Fx.Tell(FxLayer.TellKind.Radial, GlobalPosition, GlobalPosition, HitRadius * 2.4f + 12f, w, c); break;
            case BMove.Summon:
                for (int k = -1; k <= 1; k += 2) G.Fx.Tell(FxLayer.TellKind.Ground, GlobalPosition, BroodSpot(k), 22f, w, c);
                break;
            case BMove.Blink:
                _bmAim = BlinkSpot();
                G.Fx.Tell(FxLayer.TellKind.Reticle, GlobalPosition, _bmAim, 24f, w + 0.3f, c);
                break;
            case BMove.Shockline:
                G.Fx.Tell(FxLayer.TellKind.Charge, GlobalPosition, GlobalPosition + new Vector2(260f, 0), 0, w, c);
                G.Fx.Tell(FxLayer.TellKind.Charge, GlobalPosition, GlobalPosition + new Vector2(-260f, 0), 0, w, c);
                break;
        }
        G.Fx.Text(HeadPoint(14f), BossMoveName(m), new Color(c, 1f), 13, w + 0.4f, -12f);
        G.Sfx.Play(m is BMove.Summon or BMove.Guard ? "roar" : "swing_heavy", GlobalPosition, -3, 0, m == BMove.Summon ? 0.7f : 0.9f);
    }

    private Vector2 BossMouth => GlobalPosition + new Vector2(Face * HitRadius * 0.7f, -HitRadius * 0.4f);
    private Vector2 BossFeet => GlobalPosition + new Vector2(0, HitRadius * 0.75f);

    /// <summary>Beside the boss, on the floor if it walks: where one of its brood will rise.</summary>
    private Vector2 BroodSpot(int side)
    {
        var at = GlobalPosition + new Vector2(side * (HitRadius + 34f), -10f);
        if (G.Cave.IsSolid(at)) at = GlobalPosition + new Vector2(0, -HitRadius - 16f);
        return at;
    }

    /// <summary>Behind the hero (on the far side from the boss), where there's room; else beside them.</summary>
    private Vector2 BlinkSpot()
    {
        if (P == null) return GlobalPosition;
        var hero = P.GlobalPosition;
        float side = Math.Sign(hero.X - GlobalPosition.X); if (side == 0) side = 1;
        float gap = HitRadius + 26f;
        foreach (float dx in new[] { gap * side, -gap * side, (gap + 40f) * side })
        {
            var at = hero + new Vector2(dx, -10f);
            if (UsesGravity && G.Cave.FindFloor(at + new Vector2(0, -20f), 120f, out var fl)) at = fl - new Vector2(0, HitRadius * 0.8f);
            if (!G.Cave.IsSolid(at) && !G.Cave.IsSolid(at + new Vector2(0, -HitRadius)) && G.Cave.LineClear(hero, at)) return at;
        }
        return hero + new Vector2(0, -HitRadius - 30f);
    }

    private void BossWindup(float dt)
    {
        float w = BossWindupTime, p = Math.Min(1f, _bmT / w);
        Hold(dt);
        if (Anim != null)
        {
            float shake = (p > 0.6f ? (p - 0.6f) * 2.5f : 0f) * MathF.Sin(_bmT * 60f);
            switch (_bm)
            {
                case BMove.Leap: Anim.Scale = new Vector2(1f + 0.2f * p, 1f - 0.25f * p); Anim.Position = new Vector2(0, 4f * p); break;
                case BMove.Charge: Anim.Scale = new Vector2(1f + 0.15f * p, 1f - 0.1f * p); Anim.Rotation = -Face * 0.25f * p; Anim.Position = new Vector2(-Face * 5f * p + shake * 2f, 0); break;
                case BMove.Guard: Anim.Scale = new Vector2(1f - 0.08f * p, 1f + 0.06f * p); Anim.Rotation = Face * 0.1f * p; break;
                case BMove.Barrage: Anim.Scale = new Vector2(1f - 0.08f * p, 1f + 0.14f * p); Anim.Rotation = -Face * 0.2f * p; Anim.Position = new Vector2(0, -4f * p); break;
                case BMove.Nova: Anim.Scale = Vector2.One * (1f + 0.22f * p); Anim.Position = new Vector2(shake * 3f, 0); break;
                case BMove.Summon: Anim.Scale = new Vector2(1f - 0.06f * p, 1f + 0.18f * p); Anim.Rotation = MathF.Sin(_bmT * 18f) * 0.12f * p; break;
                case BMove.Blink: Anim.Scale = Vector2.One * (1f - 0.6f * p); break;
                case BMove.Shockline: Anim.Scale = new Vector2(1f - 0.1f * p, 1f + 0.22f * p); Anim.Position = new Vector2(0, -6f * p); break;
            }
            Anim.Flash(0.2f + 0.4f * p * (0.5f + 0.5f * MathF.Sin(_bmT * 22f)));
        }
        if (_bmT < w) return;
        _bmPhase = 2; _bmT = 0f;
        BossRelease();
    }

    private float BossDamage(float mult) => ContactDamage * DmgK * mult;

    private void BossRelease()
    {
        var kit = Kit;
        var c = kit.Col;
        switch (_bm)
        {
            case BMove.Leap:
            {
                float flight = 0.7f, g = 700f * MoveScale;
                var d = _bmAim - GlobalPosition;
                Velocity = UsesGravity && !InWater
                    ? new Vector2(d.X / flight / MoveScale, (d.Y - 0.5f * g * flight * flight) / flight / MoveScale)
                    : d / flight / MoveScale;
                G.Sfx.Play("jump", GlobalPosition, -2, 0, 0.6f);
                break;
            }
            case BMove.Charge:
                Velocity = _bmDir * Tune.BossMoves.ChargeSpeed;
                G.Sfx.Play("roar", GlobalPosition, -4, 0, 1.1f);
                break;
            case BMove.Guard:
                _bmGuardT = Tune.BossMoves.GuardSeconds;
                _bmAbsorbed = 0f;
                G.Fx.Ring(GlobalPosition, HitRadius * 1.6f, new Color(0.6f, 0.85f, 1f), 0.4f, FxLayer.RingStyle.Dashed);
                G.Sfx.Play("clink", GlobalPosition, -2, 0, 0.6f);
                break;
            case BMove.Barrage:
                BossVolley();
                break;
            case BMove.Nova:
                BossRing(14, 0f);
                break;
            case BMove.Summon:
            {
                int n = Enraged ? 3 : 2;
                for (int k = 0; k < n && BroodAlive < Tune.BossMoves.BroodMax; k++)
                {
                    var e = kit.Brood?.Invoke() ?? (Enemy)Activator.CreateInstance(GetType());
                    e.Minion = true;
                    e.Position = BroodSpot(k % 2 == 0 ? -1 : 1) + new Vector2(0, -6f * k);
                    e.Engage();
                    G.World.AddChild(e);
                    e.MaxHp *= Tune.BossMoves.BroodHp;
                    e.Hp = e.MaxHp;
                    e.XpValue = Math.Max(1, e.XpValue / 4);
                    _bmBrood.Add(e);
                    G.Fx.Smoke(e.Position, 8, new Color(c, 0.6f), 16f);
                    G.Fx.Ring(e.Position, 20, c, 0.3f, FxLayer.RingStyle.Burst);
                }
                break;
            }
            case BMove.Blink:
                G.Fx.Smoke(GlobalPosition, 12, new Color(c, 0.7f), 20f);
                G.Fx.Flash(GlobalPosition, 26, c);
                GlobalPosition = _bmAim;
                Velocity = Vector2.Zero;
                G.Fx.Smoke(GlobalPosition, 12, new Color(c, 0.7f), 20f);
                G.Fx.Flash(GlobalPosition, 30, c);
                G.Sfx.Play("airdash", GlobalPosition, -2, 0, 0.7f);
                if (P != null && Math.Abs(P.GlobalPosition.X - GlobalPosition.X) > 2f) Face = Math.Sign(P.GlobalPosition.X - GlobalPosition.X);
                if (Anim != null) Anim.Scale = new Vector2(1.2f, 0.85f);
                break;
            case BMove.Shockline:
                foreach (float dir in new[] { -1f, 1f })
                    G.Spawn(new Shockwave { Position = BossFeet + new Vector2(dir * HitRadius, -4f), Dir = dir, Speed = 300f, Damage = BossDamage(1.3f), Life = 1.1f, Size = 1.4f, Source = this });
                G.Fx.Shockwave(BossFeet, 60f, c, 0.4f);
                G.Fx.Dust(BossFeet, 16, 1.8f);
                G.Fx.AddShake(6f);
                G.Sfx.Play("slam", GlobalPosition, 0, 0, 0.7f);
                if (Anim != null) Anim.Scale = new Vector2(1.35f, 0.72f);
                break;
        }
    }

    /// <summary>A fan of shots at the hero.</summary>
    private void BossVolley()
    {
        var kit = Kit;
        var from = BossMouth;
        var to = (P?.GlobalPosition ?? _bmAim) + new Vector2(0, -6f) - from;
        float speed = Tune.BossMoves.ShotSpeed, grav = kit.Shot == "rock" ? 220f : 40f;
        float flight = to.Length() / speed;
        var aim = (to - new Vector2(0, 0.5f * grav * flight * flight)).Normalized();
        for (int k = 0; k < 5; k++)
            G.Spawn(new EnemyProjectile { Position = from, Vel = aim.Rotated((k - 2) * 0.18f) * speed, Grav = grav, Damage = BossDamage(0.8f), Kind = kit.Shot, Radius = 5f, Life = 2.4f, Source = this });
        G.Fx.Flash(from, 18, kit.Col);
        G.Sfx.Play("throw", GlobalPosition, -4, 0, 0.9f);
        _bmVolleys++;
    }

    /// <summary>A ring of shots all round.</summary>
    private void BossRing(int n, float turn)
    {
        var kit = Kit;
        for (int k = 0; k < n; k++)
        {
            float a = turn + k * MathF.Tau / n;
            G.Spawn(new EnemyProjectile { Position = GlobalPosition, Vel = new Vector2(MathF.Cos(a), MathF.Sin(a)) * Tune.BossMoves.ShotSpeed * 0.8f, Grav = 0f, Damage = BossDamage(0.8f), Kind = kit.Shot == "rock" ? "crystal" : kit.Shot, Radius = 4.5f, Life = 1.8f, Source = this });
        }
        G.Fx.Ring(GlobalPosition, HitRadius * 2f, kit.Col, 0.4f, FxLayer.RingStyle.Burst);
        G.Sfx.Play("spike", GlobalPosition, -2, 0, 0.8f);
        _bmVolleys++;
    }

    private void BossAct(float dt)
    {
        var kit = Kit;
        switch (_bm)
        {
            case BMove.Leap:
            {
                if (UsesGravity && !InWater) Velocity = new Vector2(Velocity.X, Math.Min(Velocity.Y + 700f * dt, 600f));
                if (Anim != null) { Anim.Scale = new Vector2(0.88f, 1.18f); Anim.Position = Vector2.Zero; }
                bool landed = UsesGravity && !InWater ? _bmT > 0.25f && IsOnFloor() : _bmT > 0.7f;
                if (landed || _bmT > 1.6f)
                {
                    BossArea(BossFeet, Tune.BossMoves.SlamRadius, BossDamage(1.8f), 360f, true);
                    G.Fx.Shockwave(BossFeet, Tune.BossMoves.SlamRadius, kit.Col, 0.5f);
                    G.Fx.Dust(BossFeet, 20, 2f);
                    G.Fx.AddShake(9f);
                    G.Sfx.Play("slam", GlobalPosition, 0, 0, 0.6f);
                    if (Anim != null) Anim.Scale = new Vector2(1.4f, 0.7f);
                    BossEnd();
                }
                break;
            }
            case BMove.Charge:
            {
                Velocity = UsesGravity && !InWater ? new Vector2(_bmDir.X * Tune.BossMoves.ChargeSpeed, Math.Min(Velocity.Y + 700f * dt, 400f)) : _bmDir * Tune.BossMoves.ChargeSpeed;
                if (Anim != null) { Anim.Scale = new Vector2(1.25f, 0.85f); Anim.Rotation = Face * 0.15f; Anim.Position = Vector2.Zero; }
                if (G.Chance(0.7f)) G.Fx.Dust(BossFeet, 2, 0.8f);
                foreach (var h in G.Players)
                {
                    if (h == null || h.Dead || _bmStruck.Contains(h) || h.GlobalPosition.DistanceTo(GlobalPosition) > HitRadius + 14f) continue;
                    _bmStruck.Add(h);
                    h.Hurt(BossDamage(1.6f), GlobalPosition, 380f, this);
                }
                if (IsOnWall() && _bmT > 0.12f)
                {
                    // (it meets the wall and reels: a moment to strike back)
                    G.Fx.Shockwave(GlobalPosition + new Vector2(Face * HitRadius, 0), 40f, kit.Col, 0.35f);
                    G.Fx.Text(HeadPoint(10f), "STAGGERED", new Color(1f, 0.9f, 0.5f), 12, 1f);
                    G.Fx.AddShake(7f);
                    G.Sfx.Play("slam", GlobalPosition, -2, 0, 0.8f);
                    Stun = Math.Max(Stun, Tune.BossMoves.WallStagger);
                    BossEnd();
                }
                else if (_bmT > 1.1f) BossEnd();
                break;
            }
            case BMove.Guard:
            {
                Hold(dt);
                _bmGuardT -= dt;
                if (Anim != null) { Anim.Scale = new Vector2(0.92f, 1.06f); Anim.Flash(0.3f + 0.2f * MathF.Sin(_bmT * 10f)); }
                if (_bmGuardT <= 0f)
                {
                    _bmGuardT = 0f;
                    // (what it took, it answers)
                    if (_bmAbsorbed > 0f)
                    {
                        G.Fx.Text(HeadPoint(12f), "RIPOSTE", new Color(0.7f, 0.9f, 1f), 14, 1f);
                        BossRing(10, 0.3f);
                        BossArea(GlobalPosition, 70f, BossDamage(1.2f) + _bmAbsorbed * 0.1f, 320f, false);
                    }
                    BossEnd();
                }
                break;
            }
            case BMove.Barrage:
                Hold(dt);
                if (Anim != null) { Anim.Scale = new Vector2(1.08f, 0.94f); Anim.Rotation = Face * 0.12f; }
                if (_bmVolleys < 3 && _bmT > _bmVolleys * 0.32f) BossVolley();
                if (_bmT > 0.95f) BossEnd();
                break;
            case BMove.Nova:
                Hold(dt);
                if (Anim != null) Anim.Scale = Vector2.One * (1.2f - 0.2f * Math.Min(1f, _bmT / 0.3f));
                if (_bmVolleys < 2 && _bmT > 0.35f) BossRing(14, MathF.PI / 14f);
                if (_bmT > 0.6f) BossEnd();
                break;
            case BMove.Summon:
                Hold(dt);
                if (_bmT > 0.4f) BossEnd();
                break;
            case BMove.Blink:
            {
                Hold(dt);
                if (Anim != null) Anim.Scale = Anim.Scale.Lerp(Vector2.One, Math.Min(1f, dt * 10f));
                // (a beat to see it, then the blow)
                if (_bmT > 0.28f && _bmVolleys == 0)
                {
                    _bmVolleys = 1;
                    BossArea(GlobalPosition, HitRadius + 44f, BossDamage(1.4f), 300f, false);
                    G.Fx.Swoosh(GlobalPosition, 40f, Face > 0 ? 0f : MathF.PI, kit.Col);
                    G.Sfx.Play("swing_heavy", GlobalPosition, -2);
                }
                if (_bmT > 0.5f) BossEnd();
                break;
            }
            case BMove.Shockline:
                Hold(dt);
                if (_bmT > 0.35f) BossEnd();
                break;
        }
    }

    private void BossRecover(float dt)
    {
        Hold(dt);
        float p = Math.Min(1f, _bmT / 0.45f);
        if (Anim != null)
        {
            Anim.Scale = Anim.Scale.Lerp(Vector2.One, p);
            Anim.Rotation = Mathf.Lerp(Anim.Rotation, 0f, p);
            Anim.Position = Anim.Position.Lerp(Vector2.Zero, p);
        }
        if (_bmT < 0.45f) return;
        _bmPhase = 0; _bmT = 0f;
        _bmCd = Tune.BossMoves.Gap * G.Range(0.8f, 1.25f) * (Enraged ? Tune.BossMoves.EnragedGap : 1f);
        if (Anim != null) { Anim.Scale = Vector2.One; Anim.Rotation = 0f; Anim.Position = Vector2.Zero; }
    }

    private void BossEnd() { _bmPhase = 3; _bmT = 0f; }

    /// <summary>Every hero within reach of a point takes the blow (a slam's reach is flat along the ground).</summary>
    private void BossArea(Vector2 at, float radius, float dmg, float knock, bool flat)
    {
        foreach (var h in G.Players)
        {
            if (h == null || h.Dead) continue;
            var d = h.GlobalPosition - at;
            if (d.X * d.X + d.Y * d.Y * (flat ? 2.2f : 1f) > radius * radius) continue;
            h.Hurt(dmg, at, knock, this);
        }
    }

    /// <summary>A blow on a guarding boss: it takes a fraction (and remembers the rest, for its riposte).</summary>
    private float BossGuardBlow(float dmg)
    {
        if (_bmGuardT <= 0f || dmg <= 0f) return dmg;
        float taken = dmg * Tune.BossMoves.GuardTaken;
        _bmAbsorbed += dmg - taken;
        if ((_bmGuardText -= 0.016f) <= 0f || _bmGuardText > 0.4f)
        {
            _bmGuardText = 0.35f;
            G.Fx.Text(HeadPoint(6f), "GUARDED", new Color(0.65f, 0.85f, 1f), 10, 0.5f);
            G.Fx.Spark(GlobalPosition + new Vector2(0, -HitRadius * 0.3f), new Color(0.75f, 0.9f, 1f));
            G.Sfx.Play("clink", GlobalPosition, -6, 0.1f, 1.2f);
        }
        return taken;
    }
}
