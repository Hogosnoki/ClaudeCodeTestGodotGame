using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

/// <summary>The kinds of second attack a creature can have (see <see cref="Enemy"/>'s Secondary part).</summary>
public enum Move2 { None, Lunge, Spit, Volley, Slam, Burst, Spin, Pounce, Dive, Spines, Kamikaze, Shatter }

/// <summary>A creature's second attack: what it is, how long it winds up, how far it reaches, how hard and how often.</summary>
public sealed class Move2Spec
{
    public Move2 Kind;
    public string Name = "";
    /// <summary>Wind-up (s), rest between uses (s), the range it's used from (px), damage (x the creature's contact damage), reach or radius (px), speed (px/s).</summary>
    public float Windup = 0.6f, Cooldown = 7f, MinRange, MaxRange = 150f, Damage = 1.3f, Radius = 48f, Speed = 320f;
    /// <summary>Shots in a volley or a ring of spines; what they are (an <see cref="EnemyProjectile"/> kind).</summary>
    public int Count = 1;
    public string Shot = "rock";
    public Color Col = new(0.8f, 0.5f, 1f);
    public string Sound = "swing";
    /// <summary>How long it must have been awake before it's first used (s); a finale: the creature is spent by it (it dies).</summary>
    public float After;
    public bool Finale => Kind is Move2.Kamikaze or Move2.Shatter;
}

/// <summary>
/// Every creature's second attack (<see cref="Tune.Secondary"/>): beside what it always did, each kind has one more move with its own
/// wind-up, its own warning shape (drawn by <see cref="FxLayer.Tell"/>, not the primary blow's "!" and squash) and its own pose: a rat
/// leans back and leaps at you, a bear rises and slams the ground, a scorpion rears and spits venom, a hornet looses a fan of stingers,
/// a crab spins with its claws out, a frog pounces on where you stand, an urchin bristles and fires spines all round... It is used only
/// when the creature is free (not mid-attack, not stunned), from its own range, and then rests.
/// </summary>
public partial class Enemy
{
    private static readonly Dictionary<Type, Move2Spec> _move2 = new()
    {
        // ---- the land
        [typeof(Rat)] = new() { Kind = Move2.Lunge, Name = "Leaping Bite", Windup = 0.45f, Cooldown = 6f, MinRange = 40, MaxRange = 140, Damage = 1.4f, Speed = 360, Col = new(0.95f, 0.75f, 0.5f), Sound = "lunge" },
        [typeof(Bear)] = new() { Kind = Move2.Slam, Name = "Ground Slam", Windup = 0.9f, Cooldown = 8f, MaxRange = 110, Damage = 1.6f, Radius = 70, Col = new(1f, 0.6f, 0.3f), Sound = "roar" },
        [typeof(Scorpion)] = new() { Kind = Move2.Spit, Name = "Venom Spit", Windup = 0.7f, Cooldown = 6.5f, MinRange = 60, MaxRange = 260, Damage = 1.1f, Speed = 300, Shot = "spit", Col = new(0.55f, 0.95f, 0.3f), Sound = "spider" },
        [typeof(Hornet)] = new() { Kind = Move2.Volley, Name = "Stinger Fan", Windup = 0.6f, Cooldown = 7f, MinRange = 50, MaxRange = 240, Damage = 0.7f, Speed = 330, Count = 3, Shot = "spit", Col = new(1f, 0.9f, 0.3f), Sound = "buzz" },
        [typeof(Skeleton)] = new() { Kind = Move2.Spit, Name = "Bone Throw", Windup = 0.6f, Cooldown = 6f, MinRange = 70, MaxRange = 260, Damage = 1f, Speed = 320, Shot = "rock", Col = new(0.92f, 0.9f, 0.82f), Sound = "throw" },
        [typeof(Sporeling)] = new() { Kind = Move2.Burst, Name = "Spore Burst", Windup = 0.8f, Cooldown = 7.5f, MaxRange = 70, Damage = 1.2f, Radius = 56, Col = new(0.75f, 0.95f, 0.45f), Sound = "gasp" },
        // (an ice spirit keeps away and throws ice: after a while it shatters into homing icicles, so it can't hold a fight up)
        [typeof(FrostWraith)] = new() { Kind = Move2.Shatter, Name = "Shatter", Windup = 1.2f, Cooldown = 1f, MaxRange = 520, Damage = 0.9f, Speed = 170, Shot = "icicle", Col = new(0.7f, 0.95f, 1f), Sound = "clink", After = Tune.Secondary.WraithAfter },
        [typeof(Shardling)] = new() { Kind = Move2.Volley, Name = "Shard Fan", Windup = 0.65f, Cooldown = 7f, MinRange = 50, MaxRange = 230, Damage = 0.8f, Speed = 300, Count = 3, Shot = "crystal", Col = new(0.65f, 0.85f, 1f), Sound = "clink" },
        [typeof(Bat)] = new() { Kind = Move2.Dive, Name = "Plummet", Windup = 0.5f, Cooldown = 6f, MinRange = 40, MaxRange = 200, Damage = 1.3f, Speed = 400, Col = new(0.8f, 0.6f, 1f), Sound = "bat" },
        [typeof(Frog)] = new() { Kind = Move2.Pounce, Name = "Pounce", Windup = 0.55f, Cooldown = 6.5f, MinRange = 50, MaxRange = 190, Damage = 1.3f, Radius = 22, Col = new(0.55f, 0.95f, 0.5f), Sound = "frog" },
        [typeof(Goblin)] = new() { Kind = Move2.Spit, Name = "Rock Toss", Windup = 0.65f, Cooldown = 6f, MinRange = 70, MaxRange = 260, Damage = 1f, Speed = 300, Shot = "rock", Col = new(0.85f, 0.75f, 0.55f), Sound = "goblin" },
        [typeof(Spider)] = new() { Kind = Move2.Pounce, Name = "Ambush Leap", Windup = 0.5f, Cooldown = 7f, MinRange = 50, MaxRange = 200, Damage = 1.3f, Radius = 22, Col = new(0.85f, 0.85f, 0.95f), Sound = "spider" },
        [typeof(LavaMonster)] = new() { Kind = Move2.Burst, Name = "Eruption", Windup = 0.9f, Cooldown = 8f, MaxRange = 90, Damage = 1.5f, Radius = 66, Col = new(1f, 0.5f, 0.15f), Sound = "lava" },
        [typeof(Golem)] = new() { Kind = Move2.Slam, Name = "Quake Stomp", Windup = 1f, Cooldown = 8.5f, MaxRange = 120, Damage = 1.7f, Radius = 80, Col = new(0.85f, 0.75f, 0.6f), Sound = "slam" },
        // ---- the water
        [typeof(Fish)] = new() { Kind = Move2.Lunge, Name = "Dart", Windup = 0.4f, Cooldown = 6f, MinRange = 30, MaxRange = 150, Damage = 1.3f, Speed = 380, Col = new(0.5f, 0.85f, 1f), Sound = "splash" },
        [typeof(Urchin)] = new() { Kind = Move2.Spines, Name = "Spine Volley", Windup = 0.8f, Cooldown = 7f, MaxRange = 180, Damage = 0.8f, Speed = 240, Count = 8, Shot = "crystal", Col = new(0.85f, 0.55f, 0.95f), Sound = "spike" },
        [typeof(Eel)] = new() { Kind = Move2.Lunge, Name = "Coil Strike", Windup = 0.5f, Cooldown = 6.5f, MinRange = 30, MaxRange = 170, Damage = 1.5f, Speed = 420, Col = new(0.6f, 0.95f, 0.85f), Sound = "eel" },
        [typeof(Crab)] = new() { Kind = Move2.Spin, Name = "Claw Whirl", Windup = 0.6f, Cooldown = 7f, MaxRange = 70, Damage = 1.1f, Radius = 40, Col = new(1f, 0.55f, 0.45f), Sound = "swing" },
        // ---- the movers
        [typeof(Mite)] = new() { Kind = Move2.Lunge, Name = "Nip", Windup = 0.35f, Cooldown = 5f, MinRange = 20, MaxRange = 110, Damage = 1.2f, Speed = 340, Col = new(0.9f, 0.85f, 0.6f), Sound = "buzz" },
        [typeof(Delver)] = new() { Kind = Move2.Burst, Name = "Upheaval", Windup = 0.8f, Cooldown = 7.5f, MaxRange = 80, Damage = 1.4f, Radius = 60, Col = new(0.75f, 0.6f, 0.45f), Sound = "rock" },
        // (a bomber keeps out of reach: after a while it dives at a hero and blows itself up)
        [typeof(Bomber)] = new() { Kind = Move2.Kamikaze, Name = "Kamikaze", Windup = 0.9f, Cooldown = 1f, MaxRange = 420, Damage = 2.2f, Radius = Tune.Secondary.KamikazeRadius, Speed = 380, Col = new(1f, 0.45f, 0.25f), Sound = "buzz", After = Tune.Secondary.BomberAfter },
        // ---- the elementals
        [typeof(EarthElemental)] = new() { Kind = Move2.Slam, Name = "Rockfall Stomp", Windup = 0.95f, Cooldown = 8f, MaxRange = 120, Damage = 1.6f, Radius = 78, Col = new(0.8f, 0.65f, 0.45f), Sound = "slam" },
        [typeof(FrostElemental)] = new() { Kind = Move2.Spin, Name = "Blizzard Spin", Windup = 0.7f, Cooldown = 7.5f, MaxRange = 80, Damage = 1.2f, Radius = 50, Col = new(0.7f, 0.95f, 1f), Sound = "swing_heavy" },
        [typeof(NatureElemental)] = new() { Kind = Move2.Spines, Name = "Thorn Ring", Windup = 0.75f, Cooldown = 7f, MaxRange = 170, Damage = 0.8f, Speed = 250, Count = 10, Shot = "spit", Col = new(0.5f, 0.95f, 0.4f), Sound = "spike" },
        [typeof(FireElemental)] = new() { Kind = Move2.Lunge, Name = "Flame Rush", Windup = 0.55f, Cooldown = 6.5f, MinRange = 40, MaxRange = 180, Damage = 1.4f, Speed = 400, Col = new(1f, 0.55f, 0.2f), Sound = "lava" },
        [typeof(WaterElemental)] = new() { Kind = Move2.Burst, Name = "Tidal Burst", Windup = 0.8f, Cooldown = 7.5f, MaxRange = 90, Damage = 1.3f, Radius = 64, Col = new(0.45f, 0.75f, 1f), Sound = "splash" },
        // ---- the clockwork and the springs
        [typeof(Automaton)] = new() { Kind = Move2.Lunge, Name = "Piston Charge", Windup = 0.6f, Cooldown = 7f, MinRange = 40, MaxRange = 180, Damage = 1.5f, Speed = 380, Col = new(0.95f, 0.8f, 0.45f), Sound = "slam" },
        [typeof(Boiler)] = new() { Kind = Move2.Burst, Name = "Steam Vent", Windup = 0.85f, Cooldown = 8f, MaxRange = 90, Damage = 1.3f, Radius = 66, Col = new(0.9f, 0.9f, 0.95f), Sound = "gasp" },
        [typeof(Cogwheel)] = new() { Kind = Move2.Lunge, Name = "Runaway Roll", Windup = 0.55f, Cooldown = 6.5f, MinRange = 40, MaxRange = 200, Damage = 1.4f, Speed = 420, Col = new(0.85f, 0.75f, 0.5f), Sound = "clink" },
        [typeof(Gasbag)] = new() { Kind = Move2.Spit, Name = "Gas Glob", Windup = 0.75f, Cooldown = 7f, MinRange = 40, MaxRange = 240, Damage = 1f, Speed = 260, Shot = "acid", Col = new(0.8f, 0.9f, 0.35f), Sound = "gasp" },
        [typeof(AcidNewt)] = new() { Kind = Move2.Spit, Name = "Acid Spit", Windup = 0.6f, Cooldown = 6f, MinRange = 60, MaxRange = 240, Damage = 1.1f, Speed = 300, Shot = "acid", Col = new(0.8f, 0.95f, 0.2f), Sound = "spider" },
    };

    /// <summary>This creature's second attack (null: it has none: structures, bosses with their own repertoire).</summary>
    public Move2Spec Move2Of => _move2.TryGetValue(GetType(), out var m) ? m : null;

    /// <summary>The kinds that have a second attack (tests).</summary>
    public static IEnumerable<Type> Move2Kinds => _move2.Keys;

    private int _m2Phase; // 0 resting, 1 winding up, 2 striking, 3 recovering
    // (driven by a Shape Shifter: what it goes for, and the creatures its move has struck)
    private Enemy _m2Foe;
    private readonly HashSet<Enemy> _m2Struck = new();
    private float _m2T, _m2Cd = -1f, _m2Spin, _m2Awake;
    private Vector2 _m2Aim, _m2Dir;
    private bool _m2Hit, _m2Left;
    /// <summary>For the tests: second attacks begun and landed (any hero struck by one).</summary>
    public int Move2Uses, Move2Hits;
    /// <summary>For the tests: winding up its second attack, or in the middle of it.</summary>
    public bool InMove2 => _m2Phase != 0;
    /// <summary>For the tests: 1 winding up, 2 striking, 3 recovering; and how far into the phase (s).</summary>
    public int Move2Phase => _m2Phase;
    public float Move2PhaseT => _m2T;
    /// <summary>For the tests: awake, and its second attack ready now.</summary>
    public void Move2Ready() { Awake = true; _m2Cd = 0.01f; _m2Awake = Math.Max(_m2Awake, Move2Of?.After ?? 0f); }

    /// <summary>
    /// The second attack, each frame (before the creature's own Think): true while it is under way, so the creature's own behaviour
    /// waits. It starts only when the creature is free and its hero in range, and then rests.
    /// </summary>
    /// <summary>
    /// Driven by a Shape Shifter (Enemy.Driven.cs): the second button asks for its second move, toward the creature nearest the aim (or out
    /// along it). The same wind-up, warning shape, pose and blow as the creature's own, but they land on the creatures of the cave. False
    /// while it rests, mid-move, or with no second move a form could make (a finale spends the body).
    /// </summary>
    public bool MasterMove2(Vector2 aim)
    {
        var m = Move2Of;
        if (Master == null || m == null || m.Finale || _m2Phase != 0 || _m2Cd > 0f || Stun > 0) return false;
        var dir = aim.LengthSquared() > 0.04f ? aim.Normalized() : new Vector2(Face, 0);
        _m2Foe = DrivenFoe(dir, m.MaxRange);
        var at = _m2Foe != null ? _m2Foe.GlobalPosition : GlobalPosition + dir * Math.Max(m.MinRange + 20f, Math.Min(m.MaxRange * 0.6f, 160f));
        _m2Struck.Clear();
        Move2Begin(m, at);
        return true;
    }

    /// <summary>A driven creature's second move: 0 ready .. 1 just used (and whether it has one at all).</summary>
    public float Move2Frac => Move2Of is { } m && _m2Cd > 0f ? Math.Clamp(_m2Cd / Move2Rest(m), 0f, 1f) : 0f;
    public bool HasFormMove2 => Move2Of is { Finale: false };
    public bool Move2ReadyNow => HasFormMove2 && _m2Phase == 0 && _m2Cd <= 0f;

    private float Move2Rest(Move2Spec m) => m.Cooldown * (Master != null ? Master.Stats.FormSpecCdMult * Master.Stats.OtherRestMult * Master.FormMoveRestMult : 1f);

    /// <summary>Its second move is ready again at once (a Shape Shifter's Apex Form).</summary>
    public void RefreshMove2() { if (_m2Phase == 0) _m2Cd = 0f; }

    /// <summary>The driven creature's second move, each frame: resting until asked; under way, it plays out (true: its own behaviour waits).</summary>
    private bool Move2DrivenTick(float dt)
    {
        var m = Move2Of;
        if (m == null) return false;
        if (_m2Phase == 0) { if (_m2Cd > 0f) _m2Cd -= dt; return false; }
        _m2T += dt;
        switch (_m2Phase)
        {
            case 1: Move2Windup(m, dt); break;
            case 2: Move2Strike(m, dt); break;
            case 3: Move2Recover(m, dt); break;
        }
        if (ManualMove && _m2Phase != 0) { Velocity *= MoveScale; MoveAndSlide(); Velocity /= MoveScale; }
        return true;
    }

    /// <summary>Where the move goes: its hero; driven, the creature it went for (or the spot it was aimed at).</summary>
    private Vector2 Move2Target => Master != null ? (_m2Foe != null && IsInstanceValid(_m2Foe) && !_m2Foe.Dead ? _m2Foe.GlobalPosition : _m2Aim) : P != null ? P.GlobalPosition : _m2Aim;
    /// <summary>Its blow's damage: driven, the kind's own (the Shape Shifter's strength is put on it), never less than a form's.</summary>
    private float Move2Damage(Move2Spec m) => Master != null ? Math.Max(ContactDamage, Tune.Shifter.FormDamage * 0.6f) * m.Damage * Master.Stats.FormSpecDmgMult : ContactDamage * DmgK * m.Damage;

    private bool Move2Tick(float dt)
    {
        var m = Move2Of;
        if (m == null || Tune.Testing.NoSecondary || IsBoss) return false;
        if (_m2Cd < 0f) _m2Cd = G.Range(1.5f, m.Cooldown); // (they don't all start at once)
        if (Awake) _m2Awake += dt;
        if (_m2Phase == 0)
        {
            _m2Cd -= dt;
            if (_m2Awake < m.After) return false;
            if (_m2Cd > 0f || !Awake || Attacking || Striking || P == null || P.Dead) return false;
            float d = TrueDistP;
            // (a burst is only worth it with the hero inside it)
            float reach = m.Kind == Move2.Burst ? Math.Min(m.MaxRange, m.Radius * 0.85f) : m.MaxRange;
            if (d < m.MinRange || d > reach || (!SeesP && m.Kind != Move2.Shatter)) return false;
            if (UsesGravity && !InWater && m.Kind is Move2.Lunge or Move2.Slam or Move2.Pounce && !IsOnFloor()) return false;
            Move2Begin(m, P.GlobalPosition);
        }
        _m2T += dt;
        switch (_m2Phase)
        {
            case 1: Move2Windup(m, dt); break;
            case 2: Move2Strike(m, dt); break;
            case 3: Move2Recover(m, dt); break;
        }
        if (Dead) return true;
        if (ManualMove && _m2Phase != 0) { MoveAndSlide(); }
        return true;
    }

    private void Move2Begin(Move2Spec m, Vector2 aimAt)
    {
        _m2Phase = 1; _m2T = 0f; _m2Hit = false; _m2Left = false; _m2Spin = 0f;
        Move2Uses++;
        _m2Aim = aimAt;
        _m2Dir = (_m2Aim - GlobalPosition).Normalized();
        if (_m2Dir == Vector2.Zero) _m2Dir = new Vector2(Face, 0);
        if (Math.Abs(_m2Dir.X) > 0.05f) Face = Math.Sign(_m2Dir.X);
        float w = Move2Windup(m);
        var head = HeadPoint(2f);
        switch (m.Kind)
        {
            case Move2.Lunge: G.Fx.Tell(FxLayer.TellKind.Charge, GlobalPosition, GlobalPosition + _m2Dir * MathF.Min(m.MaxRange, m.Speed * 0.38f), 0, w, m.Col); break;
            case Move2.Dive: G.Fx.Tell(FxLayer.TellKind.Charge, GlobalPosition, _m2Aim, 0, w, m.Col); break;
            case Move2.Spit: G.Fx.Tell(FxLayer.TellKind.Aim, MouthPoint, _m2Aim, 0, w, m.Col); break;
            case Move2.Volley: G.Fx.Tell(FxLayer.TellKind.Aim, MouthPoint, _m2Aim, 0.26f, w, m.Col); break;
            case Move2.Slam: G.Fx.Tell(FxLayer.TellKind.Ground, GlobalPosition, FeetPoint, m.Radius, w + 0.45f, m.Col); break;
            case Move2.Burst: G.Fx.Tell(FxLayer.TellKind.Ground, GlobalPosition, GlobalPosition, m.Radius, w, m.Col); break;
            case Move2.Spin: G.Fx.Tell(FxLayer.TellKind.Whirl, GlobalPosition, GlobalPosition, m.Radius, w, m.Col); break;
            case Move2.Pounce: G.Fx.Tell(FxLayer.TellKind.Reticle, GlobalPosition, _m2Aim, m.Radius, w + 0.5f, m.Col); break;
            case Move2.Kamikaze: G.Fx.Tell(FxLayer.TellKind.Charge, GlobalPosition, _m2Aim, 0, w, m.Col); G.Fx.Tell(FxLayer.TellKind.Reticle, GlobalPosition, _m2Aim, m.Radius * 0.35f, w + 0.6f, m.Col); break;
            case Move2.Shatter: G.Fx.Tell(FxLayer.TellKind.Radial, GlobalPosition, GlobalPosition, Math.Max(30f, HitRadius * 2.6f), w, m.Col); break;
            case Move2.Spines: G.Fx.Tell(FxLayer.TellKind.Radial, GlobalPosition, GlobalPosition, Math.Max(24f, HitRadius * 2.2f), w, m.Col); break;
        }
        // (its name, small and in its colour: what's coming)
        G.Fx.Text(head + new Vector2(0, -6), m.Name.ToUpperInvariant(), new Color(m.Col, 0.95f), 8, w + 0.2f, -10f);
        G.Sfx.Play(m.Sound, GlobalPosition, -6, 0, 0.8f);
    }

    /// <summary>The wind-up's length: an elite's is a little quicker.</summary>
    private float Move2Windup(Move2Spec m) => m.Windup * (Elite ? 0.85f : 1f);
    private Vector2 MouthPoint => GlobalPosition + new Vector2(Face * HitRadius * 0.6f, -HitRadius * 0.3f);
    private Vector2 FeetPoint => GlobalPosition + new Vector2(0, HitRadius * 0.7f);

    /// <summary>Holds still (falling if it walks) and takes its pose, each kind its own.</summary>
    private void Move2Windup(Move2Spec m, float dt)
    {
        float w = Move2Windup(m), p = Math.Min(1f, _m2T / w);
        Hold(dt);
        if (Anim != null)
        {
            float shake = (p > 0.6f ? (p - 0.6f) * 2.5f : 0f) * MathF.Sin(_m2T * 70f);
            switch (m.Kind)
            {
                case Move2.Kamikaze:
                    // (rocks back and forth, glowing hotter)
                    Anim.Scale = Vector2.One * (1f + 0.15f * p);
                    Anim.Rotation = MathF.Sin(_m2T * 30f) * 0.25f * p;
                    Anim.Position = new Vector2(0, -4f * p);
                    Anim.Flash(0.4f + 0.6f * p);
                    break;
                case Move2.Shatter:
                    // (swells, cracking, flashing faster and faster)
                    Anim.Scale = Vector2.One * (1f + 0.35f * p);
                    Anim.Position = new Vector2(shake * 3f, 0);
                    Anim.Flash(0.5f + 0.5f * MathF.Abs(MathF.Sin(_m2T * (10f + 30f * p))));
                    if (G.Chance(0.3f)) G.Fx.Glint(GlobalPosition + G.RandDir() * HitRadius, m.Col, 5);
                    break;
                case Move2.Lunge: case Move2.Dive:
                    // (leans back, coiled)
                    Anim.Scale = new Vector2(1f + 0.18f * p, 1f - 0.14f * p);
                    Anim.Rotation = -Face * 0.3f * p;
                    Anim.Position = new Vector2(-Face * 3f * p, 0);
                    break;
                case Move2.Spit: case Move2.Volley:
                    // (rears up, drawing breath)
                    Anim.Scale = new Vector2(1f - 0.1f * p, 1f + 0.16f * p);
                    Anim.Rotation = -Face * 0.18f * p;
                    Anim.Position = new Vector2(-Face * 2f * p, -3f * p);
                    break;
                case Move2.Slam:
                    // (rises tall)
                    Anim.Scale = new Vector2(1f - 0.12f * p, 1f + 0.24f * p);
                    Anim.Position = new Vector2(0, -5f * p);
                    break;
                case Move2.Burst: case Move2.Spines:
                    // (swells, trembling)
                    Anim.Scale = Vector2.One * (1f + 0.28f * p);
                    Anim.Position = new Vector2(shake * 2f, 0);
                    break;
                case Move2.Spin:
                    // (winds round the other way, then shudders)
                    Anim.Rotation = -Face * 0.9f * p;
                    Anim.Position = new Vector2(shake * 2.5f, 0);
                    break;
                case Move2.Pounce:
                    // (crouches low)
                    Anim.Scale = new Vector2(1f + 0.22f * p, 1f - 0.28f * p);
                    Anim.Position = new Vector2(0, 3f * p);
                    break;
            }
            Anim.Flash(0.15f + 0.35f * p * (0.5f + 0.5f * MathF.Sin(_m2T * 24f)));
        }
        if (_m2T < w) return;
        _m2Phase = 2; _m2T = 0f;
        Move2Release(m);
    }

    /// <summary>The moment it strikes: off it goes, or out the shots fly, or the ground shakes.</summary>
    private void Move2Release(Move2Spec m)
    {
        float dmg = Move2Damage(m);
        switch (m.Kind)
        {
            case Move2.Lunge:
                Velocity = UsesGravity && !InWater ? new Vector2(_m2Dir.X * m.Speed, -90f) : _m2Dir * m.Speed;
                G.Sfx.Play("lunge", GlobalPosition, -4, 0, 1.1f);
                break;
            case Move2.Dive:
                _m2Dir = (Move2Target - GlobalPosition).Normalized();
                Velocity = _m2Dir * m.Speed;
                G.Sfx.Play("airdash", GlobalPosition, -4);
                break;
            case Move2.Spit: case Move2.Volley:
            {
                var from = MouthPoint;
                var to = Move2Target + new Vector2(0, -6) - from;
                float grav = m.Shot == "rock" ? 260f : 60f;
                // (thrown high enough that the fall brings it onto the hero)
                float flight = to.Length() / m.Speed;
                var aim = (to - new Vector2(0, 0.5f * grav * flight * flight)).Normalized();
                int n = Math.Max(1, m.Count);
                for (int k = 0; k < n; k++)
                {
                    float a = n == 1 ? 0f : (k - (n - 1) / 2f) * 0.26f;
                    G.Spawn(new EnemyProjectile { Position = from, Vel = aim.Rotated(a) * m.Speed, Grav = grav, Damage = dmg, Kind = m.Shot, Radius = 4.5f, Life = 2.2f, Source = this });
                }
                G.Fx.Flash(from, 14, m.Col);
                G.Sfx.Play(m.Shot == "rock" ? "throw" : "spider", GlobalPosition, -4, 0, 1.2f);
                break;
            }
            case Move2.Spines:
            {
                int n = Math.Max(4, m.Count);
                for (int k = 0; k < n; k++)
                {
                    float a = k * MathF.Tau / n + G.Range(-0.05f, 0.05f);
                    G.Spawn(new EnemyProjectile { Position = GlobalPosition, Vel = new Vector2(MathF.Cos(a), MathF.Sin(a)) * m.Speed, Grav = 0f, Damage = dmg, Kind = m.Shot, Radius = 3.5f, Life = 1.4f, Source = this });
                }
                G.Fx.Ring(GlobalPosition, HitRadius * 1.6f, m.Col, 0.3f, FxLayer.RingStyle.Burst);
                G.Sfx.Play("spike", GlobalPosition, -3);
                break;
            }
            case Move2.Slam:
                // (up it goes; the blow lands when it comes down)
                Velocity = new Vector2(_m2Dir.X * 70f, UsesGravity && !InWater ? -300f : 0f);
                G.Sfx.Play("jump", GlobalPosition, -6, 0, 0.7f);
                break;
            case Move2.Burst:
                Move2Area(GlobalPosition, m.Radius, dmg, 280f, flat: false);
                G.Fx.Ring(GlobalPosition, m.Radius, m.Col, 0.4f, FxLayer.RingStyle.Burst);
                G.Fx.Smoke(GlobalPosition, 10, new Color(m.Col, 0.6f), m.Radius * 0.6f);
                G.Sfx.Play(m.Sound, GlobalPosition, -2, 0, 0.9f);
                break;
            case Move2.Spin:
                G.Sfx.Play("swing_heavy", GlobalPosition, -4, 0, 1.2f);
                break;
            case Move2.Kamikaze:
                _m2Dir = (Move2Target - GlobalPosition).Normalized();
                Velocity = _m2Dir * m.Speed;
                G.Sfx.Play("airdash", GlobalPosition, -2, 0, 0.8f);
                break;
            case Move2.Shatter:
            {
                int n = Tune.Secondary.Icicles;
                for (int k = 0; k < n; k++)
                {
                    float a = k * MathF.Tau / n + G.Range(-0.1f, 0.1f);
                    G.Spawn(new EnemyProjectile { Position = GlobalPosition, Vel = new Vector2(MathF.Cos(a), MathF.Sin(a)) * m.Speed, Grav = 0f, Damage = dmg, Kind = "icicle", Radius = 4f, Life = 3.2f, Source = this });
                }
                G.Fx.Ring(GlobalPosition, HitRadius * 2.4f, m.Col, 0.4f, FxLayer.RingStyle.Burst);
                G.Fx.Burst(GlobalPosition, m.Col, 18, 180, 2.4f, 0.5f, 100);
                G.Fx.Flash(GlobalPosition, 40, new Color(0.85f, 0.97f, 1f), 0.2f);
                G.Sfx.Play("clink", GlobalPosition, 0, 0, 0.7f);
                G.Sfx.Play("explode", GlobalPosition, -10, 0, 1.6f);
                Die();
                break;
            }
            case Move2.Pounce:
            {
                // (a leap that comes down on where you stood)
                float flight = 0.55f;
                var d = _m2Aim - GlobalPosition;
                float g = 700f * MoveScale;
                Velocity = new Vector2(d.X / flight / MoveScale, (d.Y - 0.5f * g * flight * flight) / flight / MoveScale);
                G.Sfx.Play("jump", GlobalPosition, -4);
                break;
            }
        }
    }

    private void Move2Strike(Move2Spec m, float dt)
    {
        float dmg = Move2Damage(m);
        switch (m.Kind)
        {
            case Move2.Lunge: case Move2.Dive:
            {
                if (UsesGravity && !InWater && m.Kind == Move2.Lunge) Velocity = new Vector2(Velocity.X, Math.Min(Velocity.Y + 700f * dt, 400f));
                if (Anim != null) { Anim.Scale = new Vector2(1.28f, 0.82f); Anim.Rotation = Face * 0.25f; Anim.Position = Vector2.Zero; }
                if (G.Chance(0.6f)) G.Fx.Trail(GlobalPosition, m.Col);
                if (!_m2Hit) Move2Touch(dmg, 260f);
                if (_m2T > (m.Kind == Move2.Dive ? 0.5f : 0.36f) || (IsOnWall() && _m2T > 0.1f)) Move2End();
                break;
            }
            case Move2.Spit: case Move2.Volley: case Move2.Spines:
                Hold(dt);
                if (Anim != null) { Anim.Scale = new Vector2(1.12f, 0.92f); Anim.Rotation = Face * 0.15f; }
                if (_m2T > 0.18f) Move2End();
                break;
            case Move2.Burst:
                Hold(dt);
                if (Anim != null) Anim.Scale = Vector2.One * (1.3f - 0.3f * Math.Min(1f, _m2T / 0.2f));
                if (_m2T > 0.22f) Move2End();
                break;
            case Move2.Slam:
            {
                if (UsesGravity && !InWater) Velocity = new Vector2(Velocity.X, Math.Min(Velocity.Y + 900f * dt, 520f));
                if (Anim != null) { Anim.Scale = new Vector2(0.9f, 1.15f); Anim.Position = Vector2.Zero; }
                bool landed = (!UsesGravity || InWater) ? _m2T > 0.25f : (_m2T > 0.15f && IsOnFloor());
                if (landed || _m2T > 1.2f)
                {
                    Move2Area(FeetPoint, m.Radius, dmg, 340f);
                    G.Fx.Shockwave(FeetPoint, m.Radius, m.Col, 0.45f);
                    G.Fx.Dust(FeetPoint, 14, 1.6f);
                    G.Fx.Shake = Math.Max(G.Fx.Shake, 5f);
                    G.Sfx.Play("slam", GlobalPosition, -2, 0, 0.85f);
                    if (Anim != null) Anim.Scale = new Vector2(1.35f, 0.7f);
                    Move2End();
                }
                break;
            }
            case Move2.Spin:
            {
                // (spins on, claws out, edging toward the hero)
                _m2Spin += dt * 26f;
                if (Anim != null) { Anim.Rotation = _m2Spin; Anim.Position = Vector2.Zero; }
                var to = Move2Target - GlobalPosition;
                Velocity = UsesGravity && !InWater ? new Vector2(Math.Sign(to.X) * 70f, Math.Min(Velocity.Y + 700f * dt, 400f)) : to.Normalized() * 60f;
                if (G.Chance(0.5f)) G.Fx.Swoosh(GlobalPosition, m.Radius, _m2Spin, m.Col);
                if (Master != null) Move2FoesNear(GlobalPosition, m.Radius + 6f, dmg, 240f, flat: false);
                else if (!_m2Hit) foreach (var h in G.Players) if (h != null && !h.Dead && h.GlobalPosition.DistanceTo(GlobalPosition) < m.Radius + 6f) { Move2Blow(h, dmg, 240f); break; }
                if (_m2T > 0.75f) Move2End();
                break;
            }
            case Move2.Kamikaze:
            {
                // (it bends a little after its hero, and goes up in flames on whatever it meets)
                if (P != null)
                {
                    var want = (P.GlobalPosition - GlobalPosition).Normalized();
                    _m2Dir = _m2Dir.Lerp(want, Math.Min(1f, 2.2f * dt)).Normalized();
                    Velocity = _m2Dir * m.Speed;
                }
                if (Anim != null) { Anim.Rotation = _m2Dir.Angle() * 0.3f; Anim.Flash(1f); }
                if (G.Chance(0.8f)) G.Fx.Ember(GlobalPosition, m.Col);
                bool touching = G.Players.Any(h => h != null && !h.Dead && h.GlobalPosition.DistanceTo(GlobalPosition) < Math.Max(HitRadius + 12f, m.Radius * 0.4f));
                // (it doesn't blow up on the ceiling it leaves from: only after a moment does the rock it meets set it off)
                bool rock = _m2T > 0.3f && (IsOnWall() || IsOnFloor() || IsOnCeiling() || G.Cave.IsSolid(GlobalPosition + _m2Dir * (HitRadius + 4f)));
                if (touching || rock || _m2T > 1.6f)
                {
                    Move2Area(GlobalPosition, m.Radius, dmg, 320f, flat: false);
                    G.Fx.Explosion(GlobalPosition, m.Col, 1.1f);
                    G.Fx.Ring(GlobalPosition, m.Radius, m.Col, 0.3f, FxLayer.RingStyle.Burst);
                    G.Fx.AddShake(4f);
                    G.Sfx.Play("explode", GlobalPosition, -2, 0.1f, 1f);
                    Die();
                }
                break;
            }
            case Move2.Pounce:
            {
                if (UsesGravity && !InWater) Velocity = new Vector2(Velocity.X, Math.Min(Velocity.Y + 700f * dt, 500f));
                if (Anim != null) { Anim.Scale = new Vector2(0.86f, 1.18f); Anim.Rotation = Face * 0.2f; Anim.Position = Vector2.Zero; }
                if (!_m2Hit) Move2Touch(dmg, 240f);
                bool landed = _m2T > 0.2f && (IsOnFloor() || !UsesGravity || InWater);
                if (landed || _m2T > 1.1f)
                {
                    if (!_m2Hit) Move2Area(FeetPoint, m.Radius, dmg * 0.8f, 220f);
                    G.Fx.Dust(FeetPoint, 8, 1f);
                    Move2End();
                }
                break;
            }
        }
    }

    private void Move2Recover(Move2Spec m, float dt)
    {
        Hold(dt);
        float p = Math.Min(1f, _m2T / 0.35f);
        if (Anim != null)
        {
            Anim.Scale = Anim.Scale.Lerp(Vector2.One, p);
            Anim.Rotation = Mathf.Lerp(Anim.Rotation % MathF.Tau, 0f, p);
            Anim.Position = Anim.Position.Lerp(Vector2.Zero, p);
        }
        if (_m2T < 0.35f) return;
        Move2Reset();
    }

    private void Move2End() { _m2Phase = 3; _m2T = 0f; }

    private void Move2Reset()
    {
        var m = Move2Of;
        _m2Phase = 0; _m2T = 0f;
        _m2Cd = Master != null && m != null ? Move2Rest(m) : (m?.Cooldown ?? 7f) * G.Range(0.8f, 1.2f) * (Elite ? 0.75f : 1f);
        _m2Foe = null;
        if (Anim != null) { Anim.Scale = Vector2.One; Anim.Rotation = 0f; Anim.Position = Vector2.Zero; }
    }

    /// <summary>A hit, stun or freeze cuts the second attack short (it rests a little before trying again).</summary>
    protected void Move2Interrupt()
    {
        if (_m2Phase == 0) return;
        Move2Reset();
        _m2Cd *= 0.5f;
    }

    /// <summary>Stands its ground: slows to a stop, and falls if it walks.</summary>
    private void Hold(float dt)
    {
        var v = Velocity;
        v.X = Mathf.MoveToward(v.X, 0, 900f * dt);
        if (UsesGravity && !InWater) v.Y = Math.Min(v.Y + 700f * dt, 400f); else v.Y = Mathf.MoveToward(v.Y, 0, 600f * dt);
        Velocity = v;
    }

    /// <summary>Its body strikes whatever hero it reaches (once).</summary>
    private void Move2Touch(float dmg, float knock)
    {
        if (Master != null) { Move2FoesNear(GlobalPosition, HitRadius + 10f, dmg, knock, flat: false); return; }
        foreach (var h in G.Players)
        {
            if (h == null || h.Dead || h.GlobalPosition.DistanceTo(GlobalPosition) >= HitRadius + 10f) continue;
            Move2Blow(h, dmg, knock);
            return;
        }
    }

    /// <summary>Every hero within reach of a point takes the blow.</summary>
    private void Move2Area(Vector2 at, float radius, float dmg, float knock, bool flat = true)
    {
        if (Master != null) { Move2FoesNear(at, radius, dmg, knock, flat); return; }
        foreach (var h in G.Players)
        {
            if (h == null || h.Dead) continue;
            var d = h.GlobalPosition - at;
            // (the ground's blow is flat: it reaches less far up than along)
            if (d.X * d.X + d.Y * d.Y * (flat ? 2.2f : 1f) > radius * radius) continue;
            Move2Blow(h, dmg, knock);
        }
    }

    /// <summary>Driven: every creature of the cave within reach of a point takes the move's blow, once each a move.</summary>
    private void Move2FoesNear(Vector2 at, float radius, float dmg, float knock, bool flat)
    {
        if (Master == null) return;
        foreach (var e in G.Enemies.ToArray())
        {
            if (!IsInstanceValid(e) || e.Dead || !e.CanBeHit || _m2Struck.Contains(e)) continue;
            var d = e.GlobalPosition - at;
            float rr = radius + e.HitRadius;
            if (d.X * d.X + d.Y * d.Y * (flat ? 2.2f : 1f) > rr * rr) continue;
            _m2Struck.Add(e);
            _m2Hit = true; Move2Hits++;
            Master.FormBlow(e, dmg, (d.LengthSquared() < 1f ? new Vector2(Face, 0) : d.Normalized()) * knock);
        }
    }

    private void Move2Blow(Player h, float dmg, float knock)
    {
        if (h.Hurt(dmg, GlobalPosition, knock, this) > 0 || h.LastHitBlocked) { _m2Hit = true; Move2Hits++; }
    }
}
