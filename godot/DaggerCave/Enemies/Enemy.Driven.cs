using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;

namespace DaggerCave;

/// <summary>
/// A creature driven by a Shape Shifter. The Shape Shifter's form is not an imitation: it is a real
/// creature of that kind, running its own behaviour (its footwork, wind-ups, strikes and
/// animations), with the controller in place of its hunger. Pressing a direction sets a point out in
/// that direction for it to seek (where it would have sought the hero); the attack button sets its
/// intent to attack, and it goes through all of that creature's wind-up before the blow lands. Its
/// blows land on the creatures of the cave, never on the hero. It stands in no list of the cave's
/// creatures (no one fights it); the Shape Shifter's own body follows it.
/// </summary>
public abstract partial class Enemy
{
    /// <summary>The Shape Shifter this creature is the body of (null: an ordinary creature of the cave).</summary>
    public bool MidAction => Busy;
    public Player Master;
    /// <summary>Driven by a Shape Shifter.</summary>
    public bool Driven => Master != null;

    /// <summary>The controller's push (-1..1 each way, up negative); zero: not pushed.</summary>
    public Vector2 MasterAim;
    /// <summary>How far out the point it seeks lies (px).</summary>
    public const float MasterReach = 320f;

    private Vector2 _masterDir = new(1, 0);
    private Vector2 _attackAim;
    private float _attackWantT, _specialWantT, _jumpWantT;

    /// <summary>The way it last was told to go or strike (to the way it faces, before that).</summary>
    public Vector2 MasterDir => MasterAim.LengthSquared() > 0.0625f ? MasterAim.Normalized() : _attackWantT > 0 && _attackAim.LengthSquared() > 0.01f ? _attackAim.Normalized() : _masterDir;

    /// <summary>The place it seeks: out along the push (or the aim of an attack asked for), or nowhere (its own place) when not pushed.</summary>
    public Vector2 MasterPoint
    {
        get
        {
            if (_attackWantT > 0 && _attackAim.LengthSquared() > 0.01f) return GlobalPosition + _attackAim.Normalized() * MasterReach;
            if (MasterAim.LengthSquared() > 0.0625f) return GlobalPosition + MasterAim.Normalized() * MasterReach;
            return GlobalPosition;
        }
    }

    /// <summary>The controller is pushed some way.</summary>
    protected bool MasterPushed => MasterAim.LengthSquared() > 0.0625f;

    /// <summary>The attack button: it will attack the moment it can (asked again each frame while held). <paramref name="aim"/>: the way to strike (zero: the way it faces).</summary>
    public void MasterAttack(Vector2 aim)
    {
        _attackWantT = 0.3f;
        _attackAim = aim;
    }

    /// <summary>The jump button.</summary>
    public void MasterJump() => _jumpWantT = 0.12f;

    /// <summary>The special button, for a creature with a move of its own for it (see <see cref="MasterSpecialIntent"/>).</summary>
    public void MasterSpecial(Vector2 aim)
    {
        _specialWantT = 0.3f;
        _attackAim = aim;
    }

    /// <summary>Whether the second move this creature has for the special button is ready (false: it has none).</summary>
    public bool MasterSpecialReady => MasterSpecialIntent >= 0 && CanAct(MasterSpecialIntent);
    /// <summary>The index of that move (-1: this creature has none).</summary>
    public int MasterSpecialIntentIndex => MasterSpecialIntent;
    /// <summary>How far through its recovery that move is: 0 ready, 1 just used.</summary>
    public virtual float MasterSpecialFrac => 0f;
    /// <summary>The creature's own second move (an index of its Actions), or -1 for none.</summary>
    protected virtual int MasterSpecialIntent => -1;

    /// <summary>What the creature does at the controller's asking: <paramref name="moving"/> (pushed some way) and <paramref name="attack"/> (the attack button): the index of one of its moves (0 is standing).</summary>
    protected virtual int MasterIntent(bool moving, bool attack) => 0;

    /// <summary>Whether the creature is being asked to use its special move.</summary>
    protected bool SpecialWanted => _specialWantT > 0;
    /// <summary>The jump button is down (a creature with a jump of its own, such as a frog's hop, answers it itself).</summary>
    protected bool JumpWanted => _jumpWantT > 0;
    /// <summary>The jump asked for is taken.</summary>
    protected void ClearJump() => _jumpWantT = 0;
    /// <summary>How fast this kind of creature leaves the ground in a jump (px/s, before the creatures' move scale).</summary>
    protected virtual float JumpSpeed => 360f;
    /// <summary>The creature answers the jump button itself (its own kind of leap) instead of the plain spring.</summary>
    protected virtual bool OwnJump => false;

    /// <summary>The creature's next move now (called each frame in place of its own scripted choice).</summary>
    private int MasterIntentNow()
    {
        if (MasterAim.LengthSquared() > 0.0625f) _masterDir = MasterAim.Normalized();
        else if (_attackWantT > 0 && _attackAim.LengthSquared() > 0.01f) _masterDir = _attackAim.Normalized();
        if (_specialWantT > 0 && MasterSpecialIntent >= 0 && CanAct(MasterSpecialIntent)) return MasterSpecialIntent;
        return MasterIntent(MasterAim.LengthSquared() > 0.0625f, _attackWantT > 0);
    }

    // ---- what the driven creature does after its own thinking each frame
    private void DrivenAfterThink(float dt)
    {
        _attackWantT -= dt; _specialWantT -= dt; _jumpWantT -= dt;
        // (an attack under way is the attack taken: the ask is spent)
        if (Attacking) { _attackWantT = 0; _specialWantT = 0; }
        if (MasterAim.LengthSquared() > 0.0625f) _masterDir = MasterAim.Normalized();
        // the jump button: a creature that walks springs up from the ground (a flier already goes where it points)
        if (_jumpWantT > 0 && !OwnJump && Walks && IsOnFloor() && !Busy && !InWater)
        {
            _jumpWantT = 0;
            Velocity = new Vector2(Velocity.X, -JumpSpeed);
            G.Sfx.Play("jump", GlobalPosition, -8, 0.1f, 0.8f);
        }
        else if (_jumpWantT > 0 && !IsOnFloor()) _jumpWantT = 0;
        // a charge or leap the Shape Shifter's special ordered: it goes on by itself, no thinking
        if (_overrideT > 0)
        {
            _overrideT -= dt;
            Velocity = new Vector2(_overrideVel.X, _overrideGravity ? Math.Min(Velocity.Y + Grav * dt, 560f) : _overrideVel.Y);
        }
    }

    private float _overrideT;
    private Vector2 _overrideVel;
    private bool _overrideGravity;
    /// <summary>The creature is flung along (the Shape Shifter's own special: a charge, a leap): it holds this speed for the time, unthinking.</summary>
    public void MasterOverride(Vector2 vel, float seconds, bool gravity)
    {
        _overrideT = seconds; _overrideVel = vel; _overrideGravity = gravity;
        Velocity = vel;
    }
    public bool Overridden => _overrideT > 0;

    // ---- its blows
    /// <summary>
    /// The creature's blow, when driven: it lands on every hostile creature (and breakable thing) for which
    /// <paramref name="inReach"/> holds (given where it lies from this creature, and how big it is), for the damage
    /// this kind of creature's blow deals (<paramref name="damage"/>, as in its tuning, before the depth's scaling:
    /// the Shape Shifter's own strength is applied to it). Returns how many creatures it struck.
    /// </summary>
    protected int StrikeFoes(Func<Vector2, float, bool> inReach, float damage, float knock = 200f)
    {
        if (Master == null) return 0;
        int n = 0;
        foreach (var e in G.Enemies.ToArray())
        {
            if (!IsInstanceValid(e) || e.Dead || !e.CanBeHit) continue;
            var rel = e.GlobalPosition - GlobalPosition;
            if (!inReach(rel, e.HitRadius)) continue;
            var dir = rel.LengthSquared() < 1f ? new Vector2(Face, 0) : rel.Normalized();
            Master.FormBlow(e, damage, dir * knock);
            n++;
        }
        foreach (var br in Breakables.All.ToArray())
            if (br.HitSize > 0 && inReach(br.HitCenter - GlobalPosition, br.HitSize)) br.Strike(GlobalPosition);
        return n;
    }

    // ---- creatures with no driving of their own
    private static readonly Dictionary<Type, bool> _ownDriving = new();
    /// <summary>
    /// This kind answers the controller itself (its own <see cref="MasterIntent"/>: footwork, wind-ups and blows of its own). The others are
    /// driven by <see cref="DrivenGeneric"/>: the stick moves the body (it walks and jumps, flies or swims as its form says), the attack
    /// button gives the form's blow (or shot), and the second button its own second move (Enemy.Secondary.cs) all the same.
    /// </summary>
    public bool OwnDriving
    {
        get
        {
            var t = GetType();
            if (!_ownDriving.TryGetValue(t, out bool own))
            {
                var m = t.GetMethod("MasterIntent", BindingFlags.Instance | BindingFlags.NonPublic);
                own = m != null && m.DeclaringType != typeof(Enemy);
                _ownDriving[t] = own;
            }
            return own;
        }
    }

    private float _gAtkT = -1f, _gAtkCd;
    private bool _gStruck;
    private Vector2 _gAtkDir = new(1, 0);
    /// <summary>A generically driven creature's blow under way (its wind-up and its strike).</summary>
    public bool GenericAttacking => _gAtkT >= 0f;
    /// <summary>For the tests: blows and shots a generically driven creature has made.</summary>
    public int GenericAttacks { get; private set; }

    /// <summary>
    /// A creature driven generically, each frame in place of its own thinking: the body goes where the stick points (at the form's speed,
    /// a run like the hero's x its Move), jumps on the jump button, and the attack button winds up and strikes with the form's blow.
    /// </summary>
    private void DrivenGeneric(float dt)
    {
        var f = Master?.Form;
        if (f == null) return;
        float speed = Tune.Hero.RunSpeed * f.Move * Master.Stats.MoveSpeed * (1f + Master.Stats.FormSpeedAdd) / MoveScale;
        bool free = f.Flier || (f.Swims && InWater);
        var v = Velocity;
        bool winding = _gAtkT >= 0f && _gAtkT < f.Wind;
        float push = winding ? 0.25f : 1f;
        if (free) v = v.MoveToward(MasterAim * speed * push, 1500f * dt);
        else
        {
            v.X = Mathf.MoveToward(v.X, MasterAim.X * speed * push, 1800f * dt);
            v.Y = Math.Min(v.Y + 700f * dt, 420f);
            if (JumpWanted && IsOnFloor() && !winding) { v.Y = -JumpSpeed * f.Jump; ClearJump(); G.Sfx.Play("jump", GlobalPosition, -8, 0.1f, 0.8f); }
        }
        if (Math.Abs(MasterAim.X) > 0.2f && _gAtkT < 0f) Face = Math.Sign(MasterAim.X);
        // the attack button: the form's blow, through its wind-up
        _gAtkCd -= dt;
        if (_gAtkT < 0f && _attackWantT > 0f && _gAtkCd <= 0f)
        {
            _gAtkT = 0f; _gStruck = false; _attackWantT = 0f;
            _gAtkDir = MasterDir;
            if (Math.Abs(_gAtkDir.X) > 0.1f) Face = Math.Sign(_gAtkDir.X);
        }
        if (_gAtkT >= 0f)
        {
            _gAtkT += dt;
            float p = Math.Min(1f, _gAtkT / Math.Max(0.01f, f.Wind));
            if (Anim != null && !_gStruck) { Anim.Scale = new Vector2(1f + 0.14f * p, 1f - 0.1f * p); Anim.Rotation = -Face * 0.22f * p; Anim.Flash(0.2f * p); }
            if (!_gStruck && _gAtkT >= f.Wind)
            {
                _gStruck = true;
                GenericAttacks++;
                GenericStrike(f);
                if (Anim != null) { Anim.Scale = new Vector2(1.25f, 0.85f); Anim.Rotation = Face * 0.2f; if (f.StrikeClip != "strike") Anim.Once(f.StrikeClip, 3); }
            }
            if (_gAtkT >= f.Wind + f.Strike)
            {
                _gAtkT = -1f;
                _gAtkCd = f.Cooldown * Tune.Shifter.GenericRest / Math.Max(0.2f, Master.Stats.AttackSpeed);
                if (Anim != null) { Anim.Scale = Vector2.One; Anim.Rotation = 0f; }
            }
        }
        Velocity = v;
        if (ManualMove) { Velocity *= MoveScale; MoveAndSlide(); Velocity /= MoveScale; }
    }

    /// <summary>The form's blow lands: on what is in reach before it, or (a ranged form) as a shot at what the aim points to.</summary>
    private void GenericStrike(ShiftForm f)
    {
        float dmg = Tune.Shifter.GenericBlow * f.Dmg;
        if (f.Ranged)
        {
            var from = GlobalPosition + new Vector2(Face * HitRadius * 0.6f, -HitRadius * 0.3f);
            var foe = DrivenFoe(_gAtkDir, 260f);
            var to = (foe != null ? foe.GlobalPosition : from + _gAtkDir * 200f) - from;
            G.Spawn(new EnemyProjectile { Position = from, Vel = to.Normalized() * 340f, Grav = 40f, Damage = dmg, Kind = f.Shot, Radius = 5f, Life = 1.6f, Source = this });
            G.Fx.Flash(from, 12, new Color(0.9f, 0.95f, 0.6f));
            G.Sfx.Play("spider", GlobalPosition, -6, 0.1f, 1.2f);
            return;
        }
        float reach = f.Reach;
        var dir = _gAtkDir;
        StrikeFoes((rel, r) => rel.Length() < reach + r && rel.Dot(dir.LengthSquared() > 0.01f ? dir : new Vector2(Face, 0)) > -8f, dmg, f.Knock);
        G.Fx.Swoosh(GlobalPosition + new Vector2(Face * reach * 0.4f, -4f), Face, reach * 0.6f, new Color(0.95f, 0.97f, 1f, 0.8f));
        G.Sfx.Play("swing", GlobalPosition, -6, 0.1f, 1.1f);
    }

    /// <summary>The creature a driven creature's move would go for: the nearest within <paramref name="range"/>, those the way it aims first.</summary>
    public Enemy DrivenFoe(Vector2 dir, float range)
    {
        Enemy best = null; float bs = float.MaxValue;
        foreach (var e in G.Enemies)
        {
            if (!IsInstanceValid(e) || e.Dead || !e.CanBeHit || e == this) continue;
            var rel = e.GlobalPosition - GlobalPosition;
            float d = rel.Length();
            if (d > range + e.HitRadius) continue;
            if (d > 1f && dir.LengthSquared() > 0.01f && rel.Dot(dir) / d < 0.2f) continue;
            if (!G.Cave.LineClear(GlobalPosition + new Vector2(0, -6), e.GlobalPosition)) continue;
            if (d < bs) { bs = d; best = e; }
        }
        return best;
    }

    private readonly HashSet<Enemy> _drivenHit = new();
    private bool _drivenWasStriking;

    /// <summary>Touching the creatures of the cave during a body attack (a swoop, a dive, a lunge, a charge): each is struck once per attack.</summary>
    private void DrivenContact(bool striking)
    {
        if (striking && !_drivenWasStriking) _drivenHit.Clear();
        _drivenWasStriking = striking;
        if (!striking) return;
        foreach (var e in G.Enemies.ToArray())
        {
            if (!IsInstanceValid(e) || e.Dead || !e.CanBeHit || _drivenHit.Contains(e)) continue;
            var rel = e.GlobalPosition - GlobalPosition;
            if (rel.Length() >= HitRadius + e.HitRadius + 4f) continue;
            _drivenHit.Add(e);
            var dir = rel.LengthSquared() < 1f ? new Vector2(Face, 0) : rel.Normalized();
            Master.FormBlow(e, ContactDamage, dir * 160f);
        }
    }
}
