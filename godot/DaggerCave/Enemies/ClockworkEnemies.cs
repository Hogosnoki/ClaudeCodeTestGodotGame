using System;
using System.Collections.Generic;
using Godot;

namespace DaggerCave;

// ============================================================================ the Clockwork Deep's creatures

/// <summary>
/// A clockwork soldier of riveted brass with a key in its back and a furnace behind a window in its chest. Its heavy overhead swing is slow to
/// come (a long wind-up you can read). It runs on a spring: freshly wound it is quick, and as the spring runs down it slows, swings later and
/// shudders, until it stops altogether to be rewound (a few seconds with its key turning, when a blow lands half again as hard).
/// </summary>
public partial class Automaton : Walker
{
    public override Element Element => Element.Armored;
    public override string HitSound => ArmorBroken ? "hit" : "hit_stone";
    private int _s; // 0 walk, 1 swing wind-up, 2 recover, 3 rewinding
    private float _st, _cd = 1.5f, _spring = 1f;
    public override void NetState(NetIO io) { io.Sync(ref _s); io.Sync(ref _spring); }
    /// <summary>For the model: how tightly wound it is (1 fresh .. 0 run down) and whether it is stopped to be rewound.</summary>
    public float Spring => _spring;
    public bool Rewinding => _s == 3;

    public Automaton() { MaxHp = Tune.Clockwork.Automaton.Hp; BodyRadius = 12; Size = 1.15f; ContactDamage = Tune.Clockwork.Automaton.Contact; XpValue = Tune.Clockwork.Automaton.Xp; KnockResist = 0.4f; }

    protected override void Setup() { DisplayName = "Automaton"; UseSprite("automaton"); }
    protected override Color BloodColor => new(0.75f, 0.6f, 0.3f);

    protected override float TakeHit(float dmg, Vector2 knock, Vector2 hitPos)
    {
        // (stopped to be rewound, it can't turn the blow)
        if (_s == 3) dmg *= Tune.Clockwork.Automaton.Rewound;
        return base.TakeHit(dmg, knock, hitPos);
    }

    protected override void Die()
    {
        if (!Dead)
        {
            G.Fx.Debris(GlobalPosition, new Color(0.7f, 0.5f, 0.2f), 14, 220);
            G.Fx.Debris(GlobalPosition, new Color(0.4f, 0.4f, 0.45f), 10, 180);
            G.Fx.Burst(GlobalPosition, new Color(1f, 0.7f, 0.25f), 12, 200, 2.4f, 0.5f, 160f);
            G.Sfx.Play("clink", GlobalPosition, 0, 0.2f, 0.6f);
        }
        base.Die();
    }

    protected override void Think(float dt)
    {
        _st += dt; _cd -= dt;
        if (Paddle(dt)) return;
        var v = Velocity;
        float drive = 0.45f + 0.75f * _spring;
        switch (_s)
        {
            case 0:
                if (!Awake || DistP > Aggro(400)) { Brake(ref v, dt, 600); break; }
                int dir = DirP;
                Face = dir;
                _spring = Math.Max(0f, _spring - dt / Tune.Clockwork.Automaton.Wind);
                v = Stride(v, Math.Abs(ToP.X) > Tune.Clockwork.Automaton.Reach * 0.7f ? dir : 0, Tune.Clockwork.Automaton.Speed * drive * (Elite ? 1.15f : 1f), dt, 300);
                if (_spring <= 0.02f)
                {
                    _s = 3; _st = 0;
                    G.Sfx.Play("clink", GlobalPosition, -4, 0.1f, 0.5f);
                    break;
                }
                if (_cd <= 0 && Math.Abs(ToP.X) < Tune.Clockwork.Automaton.Reach * Size && Math.Abs(ToP.Y) < 40 && SeesP)
                {
                    _s = 1; _st = 0;
                    Anim.Once("chop_windup", 3, 10f / (Tune.Clockwork.Automaton.Windup / drive * 24f));
                    G.Sfx.Play("goblin", GlobalPosition, -4, 0.1f, 0.5f);
                }
                break;
            case 1:
                Brake(ref v, dt, 1200);
                _spring = Math.Max(0f, _spring - dt / Tune.Clockwork.Automaton.Wind * 1.5f);
                if (_st > Tune.Clockwork.Automaton.Windup / drive)
                {
                    _s = 2; _st = 0; _cd = Tune.Clockwork.Automaton.Cooldown * (Elite ? 0.75f : 1f) / drive;
                    Anim.Once("chop", 3);
                    Swing();
                }
                break;
            case 2:
                Brake(ref v, dt);
                if (_st > 0.7f) _s = 0;
                break;
            default:
                // stopped: the key turns
                Brake(ref v, dt, 1400);
                _spring = Math.Min(1f, _st / Tune.Clockwork.Automaton.Rewind);
                if (G.Chance(0.15f)) G.Fx.Spark(GlobalPosition + new Vector2(-Face * 8, -6) * Size, new Color(1f, 0.8f, 0.4f));
                if (_st > Tune.Clockwork.Automaton.Rewind) { _s = 0; _spring = 1f; _cd = Math.Max(_cd, 0.5f); }
                break;
        }
        Velocity = v;
        ApplyGravity(dt);
    }

    /// <summary>The overhead chop: it lands in front of it, hard.</summary>
    private void Swing()
    {
        G.Sfx.Play("slam", GlobalPosition + new Vector2(Face * 24, 0), -3, 0.1f, 1.1f);
        var at = GlobalPosition + new Vector2(Face * 26f * Size, 10f);
        G.Fx.Debris(at, new Color(0.5f, 0.45f, 0.4f), 4, 100);
        G.Fx.Spark(at, new Color(1f, 0.85f, 0.5f));
        var rel = ToP;
        if (rel.X * Face > -10 && Math.Abs(rel.X) < Tune.Clockwork.Automaton.Reach * Size + 14 && Math.Abs(rel.Y) < 40 * Size)
            P.Hurt(Tune.Clockwork.Automaton.Swing * DmgK * (Elite ? 1.3f : 1f), GlobalPosition, 260, this);
    }

    protected override bool Busy => _s != 0 || InWater || !IsOnFloor();
    public override bool Attacking => _s == 1;
    protected override void OnInterrupted() { if (_s == 1) { _s = 2; _st = 0; } }

    protected override void Animate()
    {
        float avx = Math.Abs(Velocity.X);
        if (_s == 3) Anim.Loop("rewind");
        else Anim.Loop(avx > 8 ? "walk" : "idle", Math.Clamp(avx / 40f, 0.6f, 1.5f));
        Anim.AllowTurns = _s == 0;
    }

    public override void _Draw() => DrawHealthBar();
}

/// <summary>
/// A squat iron boiler on four stubby legs with a nozzle out of its front. It never moves. Its pressure builds (the gauge climbs, it
/// shudders and glows), and at the top it turns on you and blows a jet of scalding steam, then cools.
/// </summary>
public partial class Boiler : Enemy
{
    public override Element Element => Element.Armored;
    public override string HitSound => ArmorBroken ? "hit" : "hit_stone";
    private int _s; // 0 charge, 1 jet, 2 cool
    private float _st, _wait = 0.5f;
    private Vector2 _aim = Vector2.Right;
    public override void NetState(NetIO io) { io.Sync(ref _s); io.Sync(ref _st); }
    /// <summary>For the model: pressure 0..1 (rising in the charge, full in the jet, falling as it cools), and where the nozzle points.</summary>
    public float Pressure => _s == 0 ? Math.Clamp(_st / Tune.Clockwork.Boiler.Charge, 0f, 1f) : _s == 1 ? 1f : Math.Clamp(1f - _st / Tune.Clockwork.Boiler.Cool, 0f, 1f);
    public bool Venting => _s == 1;

    public Boiler() { MaxHp = Tune.Clockwork.Boiler.Hp; BodyRadius = 14; Size = 1.2f; ContactDamage = 7; XpValue = (int)Tune.Clockwork.Boiler.Xp; KnockResist = 1f; }

    protected override void Setup() { DisplayName = "Boiler"; UseSprite("boiler"); }
    protected override Color BloodColor => new(0.8f, 0.7f, 0.4f);
    protected override void Die()
    {
        if (!Dead)
        {
            G.Fx.Debris(GlobalPosition, new Color(0.4f, 0.4f, 0.45f), 16, 240);
            G.Fx.Smoke(GlobalPosition, 6, new Color(0.9f, 0.9f, 0.92f, 0.6f), 30);
            G.Sfx.Play("rock", GlobalPosition, 0, 0.2f, 0.5f);
        }
        base.Die();
    }

    protected override void Think(float dt)
    {
        var v = new Vector2(0, Velocity.Y);
        ApplyGravityTo(ref v, dt);
        Velocity = v;
        bool near = Awake && DistP < Aggro(420);
        if (Math.Abs(ToP.X) > 4) Face = Math.Sign(ToP.X);
        switch (_s)
        {
            case 0:
                if (!near) { _st = Math.Max(0f, _st - dt); break; }
                _st += dt;
                if (G.Chance(0.2f)) G.Fx.Smoke(GlobalPosition + new Vector2(Face * 18 * Size, -10 * Size), 1, new Color(0.9f, 0.9f, 0.92f, 0.4f), 8);
                if (_st >= Tune.Clockwork.Boiler.Charge * (Elite ? 0.75f : 1f) && SeesP)
                {
                    _s = 1; _st = 0;
                    _aim = (P.GlobalPosition + new Vector2(0, -6) - (GlobalPosition + new Vector2(Face * 18 * Size, -6 * Size))).Normalized();
                    G.Sfx.Play("spider", GlobalPosition, 0, 0.1f, 0.4f);
                    G.Spawn(new SteamJet { Position = GlobalPosition + new Vector2(Face * 20 * Size, -6 * Size), Dir = _aim, Length = Tune.Clockwork.Boiler.Length * (Elite ? 1.25f : 1f), Life = Tune.Clockwork.Boiler.Jet, Source = this });
                }
                break;
            case 1:
                _st += dt;
                if (_st > Tune.Clockwork.Boiler.Jet) { _s = 2; _st = 0; }
                break;
            default:
                _st += dt;
                if (_st > Tune.Clockwork.Boiler.Cool) { _s = 0; _st = 0; }
                break;
        }
    }

    private void ApplyGravityTo(ref Vector2 v, float dt) { if (!IsOnFloor()) v.Y = Math.Min(v.Y + Grav * dt, 600f); else v.Y = 0f; }

    protected override bool Busy => _s != 0;
    public override bool Attacking => _s == 1;
    protected override void Animate() => Anim.Loop(_s == 1 ? "vent" : "idle");
    public override void _Draw() => DrawHealthBar();
}

/// <summary>
/// A toothed iron wheel as tall as a knee with a furnace for a hub. It rolls at you across the floor, stops short, revs (sparks flying, the
/// teeth a blur), and drives straight through. After a dash it runs on for a moment, spent.
/// </summary>
public partial class Cogwheel : Walker
{
    public override Element Element => Element.Armored;
    public override string HitSound => ArmorBroken ? "hit" : "hit_stone";
    private int _s; // 0 roll, 1 rev, 2 dash, 3 spent
    private float _st, _cd = 1.2f;
    private int _dashDir = 1;
    private bool _boosted;
    public override void NetState(NetIO io) => io.Sync(ref _s);
    /// <summary>For the model: how fast it is turning, as the rate it covers the ground (and a rev spins it in place).</summary>
    public bool Revving => _s == 1;

    public Cogwheel() { MaxHp = Tune.Clockwork.Cogwheel.Hp; BodyRadius = 9; Size = 1.3f; ContactDamage = Tune.Clockwork.Cogwheel.Contact; XpValue = Tune.Clockwork.Cogwheel.Xp; KnockResist = 0.3f; }

    protected override void Setup() { DisplayName = "Cogwheel"; UseSprite("cogwheel"); }
    protected override Color BloodColor => new(0.8f, 0.6f, 0.25f);
    protected override void Die()
    {
        if (!Dead) { G.Fx.Debris(GlobalPosition, new Color(0.6f, 0.55f, 0.5f), 12, 220); G.Fx.Spark(GlobalPosition, new Color(1f, 0.8f, 0.4f)); G.Sfx.Play("clink", GlobalPosition, 0, 0.2f, 0.7f); }
        base.Die();
    }

    protected override void Think(float dt)
    {
        _st += dt; _cd -= dt;
        if (Paddle(dt)) return;
        var v = Velocity;
        switch (_s)
        {
            case 0:
                Unboost();
                if (!Awake || DistP > Aggro(420)) { Brake(ref v, dt, 600); break; }
                int dir = DirP;
                Face = dir;
                v = Stride(v, Math.Abs(ToP.X) > 50 ? dir : 0, Tune.Clockwork.Cogwheel.Roll * (Elite ? 1.15f : 1f), dt, 300);
                if (_cd <= 0 && DistP < 200 && DistP > 50 && Math.Abs(ToP.Y) < 36 && SeesP)
                {
                    _s = 1; _st = 0; _dashDir = dir;
                    Anim.Once("rev", 3);
                    G.Sfx.Play("goblin", GlobalPosition, -5, 0.1f, 1.8f);
                }
                break;
            case 1:
                Brake(ref v, dt, 1600);
                if (G.Chance(0.5f)) G.Fx.Spark(GlobalPosition + new Vector2(-_dashDir * 8, 8 * Size), new Color(1f, 0.8f, 0.4f));
                if (_st > Tune.Clockwork.Cogwheel.Rev)
                {
                    _s = 2; _st = 0;
                    if (!_boosted) { _boosted = true; ContactDamage *= 1.8f; }
                    G.Sfx.Play("lunge", GlobalPosition, -2, 0.1f, 1.3f);
                }
                break;
            case 2:
                v.X = _dashDir * Tune.Clockwork.Cogwheel.Dash;
                if (G.Chance(0.6f)) G.Fx.Spark(GlobalPosition + new Vector2(-_dashDir * 10, 9 * Size), new Color(1f, 0.8f, 0.4f));
                if (_st > Tune.Clockwork.Cogwheel.DashTime || IsOnWall())
                {
                    _s = 3; _st = 0; _cd = Tune.Clockwork.Cogwheel.Cooldown * (Elite ? 0.7f : 1f);
                    Unboost();
                }
                break;
            default:
                Brake(ref v, dt, 500);
                if (_st > 0.7f) _s = 0;
                break;
        }
        Velocity = v;
        ApplyGravity(dt);
    }

    protected override bool Busy => _s != 0 || InWater || !IsOnFloor();
    public override bool Attacking => _s is 1 or 2;
    protected override bool Striking => _s == 2;
    protected override void OnInterrupted() { if (_s is 1 or 2) { _s = 3; _st = 0; Unboost(); } }
    private void Unboost() { if (_boosted) { _boosted = false; ContactDamage /= 1.8f; } }

    protected override void Animate()
    {
        float avx = Math.Abs(Velocity.X);
        Anim.Loop(avx > 8 ? "roll" : "idle", Math.Clamp(avx / 100f, 0.7f, 2f));
        Anim.AllowTurns = false;
    }

    public override void _Draw() => DrawHealthBar();
}

/// <summary>
/// The guardian of the works: a colossus of brass plate. It fights as the Cavern Colossus does, and from time to time steam erupts in
/// columns across its hall, each shown a moment before by a hissing and a gout of white at the floor.
/// </summary>
public partial class EngineWarden : CavernColossus
{
    private float _vent = 6.5f;
    private readonly List<(float t, Vector2 at)> _pending = new();

    protected override void Setup() { base.Setup(); DisplayName = "Engine-Warden"; }

    protected override void Think(float dt)
    {
        base.Think(dt);
        for (int k = _pending.Count - 1; k >= 0; k--)
        {
            var (t, at) = _pending[k];
            t -= dt;
            if (t > 0f)
            {
                _pending[k] = (t, at);
                if (G.Chance(0.35f)) G.Fx.Smoke(at + new Vector2(G.Range(-8, 8), -2), 1, new Color(0.95f, 0.95f, 0.97f, 0.55f), 9);
                continue;
            }
            _pending.RemoveAt(k);
            G.Sfx.Play("spider", at, 0, 0.1f, 0.4f);
            G.Spawn(new SteamJet { Position = at, Dir = Vector2.Up, Length = 130f, Width = 30f, Life = Tune.Clockwork.Warden.ColumnLife, Source = this });
        }
        _vent -= dt;
        if (_vent > 0f || Busy || Dead || Room == null) return;
        _vent = Tune.Clockwork.Warden.Cooldown * G.Range(0.8f, 1.2f) * (Phase2 ? 0.7f : 1f);
        G.Sfx.Play("roar", GlobalPosition, -3, 0.1f, 0.7f);
        G.Fx.Text(GlobalPosition + new Vector2(0, -60), "PRESSURE", new Color(1f, 0.9f, 0.7f), 13, 1.2f);
        int n = (int)Tune.Clockwork.Warden.Columns + (Phase2 ? 2 : 0);
        for (int k = 0; k < n; k++)
        {
            float x = Room.Center.X + (-0.85f + 1.7f * (k + G.Range(0.2f, 0.8f)) / n) * Room.RxPx;
            if (k == 0) x = P.GlobalPosition.X;
            if (!G.Cave.FindFloor(new Vector2(x, Room.Floor.Y - 60), 140f, out var fl)) continue;
            _pending.Add((Tune.Clockwork.Warden.Telegraph, fl));
        }
    }
}
