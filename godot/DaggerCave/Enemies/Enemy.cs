using System;
using System.Collections.Generic;
using Godot;

namespace DaggerCave;

/// <summary>
/// Shared enemy plumbing: health, hit flash, knockback stun, contact damage, sleeping when far
/// from the player, XP/heart drops on death, elite (mini-boss) scaling, and draw helpers.
/// Subclasses implement <see cref="Think"/> (set Velocity or move manually) and <see cref="_Draw"/>.
/// </summary>
public abstract partial class Enemy : CharacterBody2D
{
    public const float Grav = 1200f;

    public string DisplayName = "Enemy";
    public float MaxHp = 20, Hp;
    public float BodyRadius = 9f;
    public float ContactDamage = 8f;
    public int XpValue = 3;
    public float KnockResist;
    public bool Elite, IsBoss;
    public float Size = 1f;
    public bool Dead;
    public Action<Enemy> OnDeath;

    // ---- online play
    /// <summary>Online: a copy of the host's creature, shown from what the host sends. It thinks
    /// nothing and deals nothing here; blows that land on it are sent to the host.</summary>
    public bool Puppet;
    /// <summary>Online: this creature's id in every game.</summary>
    public int NetId;
    /// <summary>Online (host): the player whose blow is landing right now (credited with a kill).</summary>
    public int LastAttacker;
    /// <summary>Online: its name as the host has it (variants and elites).</summary>
    public string NetDisplayName;
    private bool _goneSent;

    // ---- biome variants and guardians
    /// <summary>Put before the creature's name ("Frost ", "Ember "...).</summary>
    public string NamePrefix = "";
    /// <summary>A guardian's title, shown on its banner and health bar.</summary>
    public string Title = "";
    /// <summary>The exit guardian of a level (its death opens the exits).</summary>
    public bool IsGuardian;
    /// <summary>Colour wash over the sprite for biome variants.</summary>
    public Color? Tint;
    /// <summary>Extra damage multiplier (variants, guardians).</summary>
    public float DmgMult = 1f;
    /// <summary>What every hit this creature deals is multiplied by: difficulty curve times its own
    /// multiplier (and less while weakened by a charged strike).</summary>
    protected float DmgK => G.DepthDmg * DmgMult * (_weakT > 0 ? _weakMult : 1f);

    protected float T, HurtFlash, Stun;
    protected SpriteAnimator Anim;
    /// <summary>This creature's animator (and through it, its 3D model).</summary>
    public SpriteAnimator Animator => Anim;
    protected Vector2 KnockVel;
    protected float Face = 1;
    protected bool Awake;
    /// <summary>When true, the subclass positions itself in Think and the base skips MoveAndSlide.</summary>
    protected bool ManualMove;
    protected bool ContactActive = true;
    protected virtual bool UsesGravity => true;
    /// <summary>Walks and falls (rather than flying or swimming): it can be stranded on a ledge.</summary>
    public bool Walks => UsesGravity;
    public virtual bool CanBeHit => true;
    public virtual float HitRadius => BodyRadius * Size;

    /// <summary>The hero it's after: the nearest one still standing (online, looked at again every half second).</summary>
    protected Player P => Target;
    private Player _target;
    private float _targetT;
    public Player Target
    {
        get
        {
            if (!Net.Online || G.Players.Count <= 1) return G.Player;
            if (_target != null && IsInstanceValid(_target) && _target.IsInsideTree() && !_target.Dead && _targetT > 0) return _target;
            _targetT = 0.5f;
            Player best = null;
            float bd = float.MaxValue;
            foreach (var h in G.Players)
            {
                if (h == null || !IsInstanceValid(h) || h.Dead) continue;
                float d = h.GlobalPosition.DistanceSquaredTo(GlobalPosition);
                if (d < bd) { bd = d; best = h; }
            }
            _target = best ?? G.Player;
            return _target;
        }
    }
    protected Vector2 ToP => P.GlobalPosition - GlobalPosition;
    protected float DistP => ToP.Length();
    protected bool SeesP => G.Cave.LineClear(GlobalPosition, P.GlobalPosition);
    protected bool InWater => G.Cave.IsWater(GlobalPosition);

    public override void _Ready()
    {
        CollisionLayer = G.LayerEnemy;
        CollisionMask = G.LayerTerrain;
        FloorMaxAngle = Mathf.DegToRad(Tune.Cave.WalkableSlopeDegrees + 2);
        FloorSnapLength = 6f;
        ZIndex = 0;
        // Health is fixed at spawn from the difficulty curve (and how many are playing); damage
        // and tempo follow it live. (A copy has the host's numbers already.)
        if (!Puppet)
        {
            MaxHp *= G.DepthHp * NetSync.HpScale;
            Hp = MaxHp;
        }
        AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = BodyRadius * Size * 0.9f } });
        G.Enemies.Add(this);
        Face = G.Chance(0.5f) ? 1 : -1;
        Setup();
        DisplayName = Puppet && NetDisplayName != null ? NetDisplayName : (Elite && !IsGuardian && !IsBoss ? "Elite " : "") + NamePrefix + DisplayName;
        if (Tint is Color tint && Anim != null) Anim.Sprite.SelfModulate = tint;
        if (Net.IsHost && !Puppet) NetSync.EnemySpawned(this);
    }

    public override void _ExitTree()
    {
        G.Enemies.Remove(this);
        if (!_goneSent && Net.IsHost && !Puppet) { _goneSent = true; NetSync.EnemyGone(this, false); }
        if (Puppet) return;
        BrainFlush(terminal: false);
    }

    protected virtual void Setup() { }

    /// <summary>Attaches this creature's sprite sheet (call from Setup).</summary>
    protected void UseSprite(string set)
    {
        Anim = SpriteAnimator.Create(set, Size);
        Anim.Facing = (int)Face;
        Anim.FootOffset = BodyRadius * Size;
        AddChild(Anim);
    }

    /// <summary>Picks clips for the current state; runs after Think every physics frame.</summary>
    protected virtual void Animate() { }

    /// <summary>Turns this enemy into a mini-boss. Call before adding to the tree.</summary>
    public virtual void MakeElite()
    {
        Elite = true;
        Size *= Tune.Elite.SizeMult;
        MaxHp *= Tune.Elite.HpMult;
        ContactDamage *= Tune.Elite.DamageMult;
        XpValue = (int)(XpValue * Tune.Elite.XpMult);
        KnockResist = Math.Max(KnockResist, Tune.Elite.MinKnockResist);
    }

    protected abstract void Think(float dt);

    /// <summary>
    /// True for enemies spawned as an "entrance" (they arrive from off-screen and come looking for
    /// the player rather than waiting to be found).
    /// </summary>
    public bool Hunting;

    /// <summary>Makes this enemy seek the player from out of view. Call before adding to the tree.</summary>
    public virtual void Engage() { Awake = true; Hunting = true; }

    /// <summary>Starts it awake (guardians), without the hunting behaviour.</summary>
    public void Wake() => Awake = true;

    /// <summary>How far away this enemy notices the player (much further when hunting).</summary>
    protected float Aggro(float range) => Hunting ? 1200f : range;

    public override void _PhysicsProcess(double delta)
    {
        if (Puppet) { PuppetTick((float)delta); return; }
        // (what it does, the other games see: its effects and sounds are sent)
        NetSync.Scope++;
        try { Simulate(delta); }
        finally { NetSync.Scope--; }
    }

    private void Simulate(double delta)
    {
        if (Dead) return;
        _targetT -= (float)delta;
        var p = P;
        if (p == null) return;
        float dist = DistP;
        if (Hunting && !IsBoss && dist > 1700) { QueueFree(); return; } // wandered off-stage
        if (!IsBoss && dist > 1500) return; // asleep
        if (!Awake && dist < 420) Awake = true;
        // (what ails it goes on through a hit-stop: a burning creature keeps burning)
        if (!TickAfflictions((float)delta)) return;
        // hit-stop: this creature alone holds still for a beat (with a little shudder)
        if (_freeze > 0)
        {
            _freeze -= (float)delta;
            if (Anim != null)
            {
                Anim.TimeMult = 0;
                Anim.Position = _freeze > 0 ? new Vector2(G.Range(-1.5f, 1.5f), G.Range(-0.8f, 0.8f)) : Vector2.Zero;
            }
            QueueRedraw();
            return;
        }
        // frozen solid (the Elementalist's frost): it holds its pose in the ice, doing nothing,
        // until it thaws or is shattered
        if (_iceT > 0)
        {
            _iceT -= (float)delta;
            if (Anim != null) { Anim.TimeMult = 0; Anim.Position = Vector2.Zero; }
            Velocity = new Vector2(0, UsesGravity && !InWater ? Math.Min(Velocity.Y + 700f * (float)delta, 400f) : 0);
            MoveAndSlide();
            if (_iceT <= 0) Thawed();
            QueueRedraw();
            return;
        }
        // Enemies run on their own clock, sped up by the difficulty curve: movement, cooldowns
        // and animations all scale together (a hex, or a chill, slows the whole clock down).
        float tempo = G.Tempo * (_hexT > 0 ? _hexSlow : 1f) * (_chillT > 0 ? _chillSlow : 1f);
        float dt = (float)delta * tempo;
        T += dt; HurtFlash -= (float)delta;
        if (_primeT > 0) _primeT -= dt;
        TickSlot(dt);
        if (_bleedT > 0)
        {
            _bleedT -= dt;
            Hp -= _bleedDps * dt;
            if (G.Chance(0.15f)) G.Fx.Burst(GlobalPosition, BloodColor, 1, 30, 1.8f, 0.4f, 200);
            if (Hp <= 0) { Die(); return; }
        }
        BrainAccount(dt);

        if (Stun > 0)
        {
            Stun -= dt;
            if (_dazed) { if (Stun > 0) DazeFx(dt); else _dazed = false; }
            var v = KnockVel;
            KnockVel *= 1f / (1f + 8f * dt);
            if (UsesGravity && !InWater) v.Y += 200;
            Velocity = v;
            MoveAndSlide();
        }
        else
        {
            BrainDecide(dt);
            Think(dt);
            if (!ManualMove)
            {
                // tempo speeds the creature up; MoveScale shrinks every move it makes (speed,
                // gravity and jump height together, with the same timing)
                float k = tempo * MoveScale;
                Velocity *= k;
                MoveAndSlide();
                Velocity /= k;
            }
        }
        if (Anim != null)
        {
            Anim.TimeMult = tempo;
            Animate();
            Anim.Face((int)Face);
            if (!ManualMove) Anim.Motion(Velocity * MoveScale);
        }

        // Touching an enemy only hurts during a body attack (a swoop, dart, lunge, drop, charge),
        // and then only once per attack. Idle bodies do PassiveContactMult of that (0 by default).
        bool striking = Striking;
        if (striking && !_wasStriking) _strikeLanded = false;
        _wasStriking = striking;
        float touch = striking ? (_strikeLanded ? 0 : ContactDamage) : ContactDamage * Tune.Combat.PassiveContactMult;
        if (ContactActive && touch > 0)
        {
            // any hero it touches (online, the others' too: their games take the blow)
            foreach (var h in G.Players)
            {
                if (h == null || h.Dead || h.GlobalPosition.DistanceTo(GlobalPosition) >= HitRadius + 7) continue;
                // a strike that lands, or that the shield stops, is spent
                if ((h.Hurt(touch * DmgK, GlobalPosition, source: this) > 0 || h.LastHitBlocked) && striking) { _strikeLanded = true; break; }
            }
        }
        QueueRedraw();
    }

    private float _freeze, _bleedT, _bleedDps;

    /// <summary>How far enemies move relative to their numbers (Tune.Difficulty.EnemyMoveScale).</summary>
    protected static float MoveScale => Tune.Difficulty.EnemyMoveScale;

    /// <summary>Damage over time (the swordsman's Rending Edge). Stacks by topping up.</summary>
    public void Bleed(float total, float seconds)
    {
        if (Dead || total <= 0) return;
        if (Puppet) { NetSync.EffectPuppet(this, NetSync.Effect.Bleed, total, seconds); return; }
        float left = _bleedT > 0 ? _bleedDps * _bleedT : 0;
        _bleedT = seconds;
        _bleedDps = (left + total) / seconds;
    }

    /// <summary>
    /// Freezes just this creature for a hit-stop. A creature winding up or attacking isn't held
    /// by a hit-stop: its attack plays out exactly as telegraphed, so it can be read and blocked.
    /// <paramref name="hold"/> freezes it all the same (a rupture seizing it, the blow it just landed).
    /// </summary>
    public void Freeze(float seconds, bool hold = false)
    {
        if (Dead || (!hold && AttackingNow)) return;
        _freeze = Math.Max(_freeze, seconds);
        if (Puppet) NetSync.EffectPuppet(this, NetSync.Effect.Freeze, seconds, hold ? 1 : 0);
    }
    /// <summary>Seconds of hit-stop left.</summary>
    public float FreezeLeft => _freeze;

    /// <summary>True while a body attack is under way: touching the player then hurts (once).</summary>
    protected virtual bool Striking => false;
    private bool _wasStriking, _strikeLanded;

    // ================================================================== breaking attacks off
    /// <summary>
    /// True while an attack is under way: its wind-up or its blow. Only such a creature stops the
    /// Warden's Guarded Charge (walking up to you, however menacingly, doesn't count).
    /// </summary>
    public virtual bool Attacking => Striking;

    /// <summary>Bosses shrug interruptions off (the blow itself is still stopped).</summary>
    public virtual bool Interruptible => !IsBoss;

    /// <summary>
    /// Breaks off the attack under way (a perfect block, the Guarded Charge): the creature reels
    /// for <paramref name="stagger"/> seconds and its attack starts over from scratch.
    /// </summary>
    public void Interrupt(Vector2 push, float stagger, string label = "BROKEN")
    {
        if (Dead) return;
        if (Puppet) { NetSync.EffectPuppet(this, NetSync.Effect.Interrupt, stagger, v: push, label: label); return; }
        _strikeLanded = true; // whatever blow was coming is spent
        if (!Interruptible) { Recoil(push.X); return; }
        OnInterrupted();
        Intent = 0;
        _slotT = 0; // its attack slot goes to someone else
        Stun = Math.Max(Stun, stagger);
        // a long stun leaves it seeing stars
        if (stagger >= 1f) _dazed = true;
        KnockVel = ManualMove ? Vector2.Zero : push * (1f - KnockResist * 0.7f);
        if (Anim != null)
        {
            Anim.CancelOnce();
            Anim.Once("hurt", 5);
            Anim.Flash(0.45f);
        }
        G.Fx.Text(GlobalPosition + new Vector2(0, -HitRadius - 14), label, new Color(0.75f, 0.9f, 1f), 9, 0.7f);
    }

    private bool _dazed;
    private float _dazeFxT;

    /// <summary>Stars wheeling around the head of a stunned creature.</summary>
    private void DazeFx(float dt)
    {
        _dazeFxT -= dt;
        if (_dazeFxT > 0) return;
        _dazeFxT = 0.07f;
        float a = T * 7f;
        float r = Math.Max(8f, HitRadius * 0.8f);
        var head = GlobalPosition + new Vector2(0, -HitRadius - 6);
        for (int k = 0; k < 2; k++)
        {
            float ak = a + k * MathF.PI;
            G.Fx.Glint(head + new Vector2(MathF.Cos(ak) * r, MathF.Sin(ak) * r * 0.3f), new Color(1f, 0.92f, 0.45f), 5f);
        }
    }

    /// <summary>Resets this creature's attack state after an interruption (override per creature).</summary>
    protected virtual void OnInterrupted() { }

    /// <summary>Knocked back or reeling from a broken attack.</summary>
    public bool Reeling => Stun > 0;

    // ================================================================== afflictions
    private float _weakT, _weakMult = 1f;                  // deals less damage (the Swordsman's charged strike)
    private float _hexT, _hexSlow = 1f, _hexVuln = 1f;     // slowed, and hurt more (the Vitalist's hex)
    private float _moteT;
    public bool Weakened => _weakT > 0;
    public bool Hexed => _hexT > 0;

    /// <summary>For <paramref name="seconds"/>, every hit this creature deals is multiplied by <paramref name="dmgMult"/>.</summary>
    public void Weaken(float dmgMult, float seconds)
    {
        if (Dead) return;
        if (Puppet) { _weakT = Math.Max(_weakT, 0.3f); NetSync.EffectPuppet(this, NetSync.Effect.Weaken, dmgMult, seconds); return; }
        _weakMult = _weakT > 0 ? Math.Min(_weakMult, dmgMult) : dmgMult;
        _weakT = Math.Max(_weakT, seconds);
    }

    /// <summary>For <paramref name="seconds"/>, it moves and acts at <paramref name="slow"/> speed,
    /// takes <paramref name="vulnerability"/> times the damage, and (Withering Hex) rots away
    /// <paramref name="rotDps"/> health a second.</summary>
    public void Hex(float vulnerability, float slow, float seconds, float rotDps = 0f)
    {
        if (Dead) return;
        if (Puppet) { _hexT = Math.Max(_hexT, 0.3f); NetSync.EffectPuppet(this, NetSync.Effect.Hex, vulnerability, slow, seconds, rotDps); return; }
        if (rotDps > 0) _hexBy = NetSync.Striker;
        _hexVuln = _hexT > 0 ? Math.Max(_hexVuln, vulnerability) : vulnerability;
        _hexSlow = _hexT > 0 ? Math.Min(_hexSlow, slow) : slow;
        _hexRot = _hexT > 0 ? Math.Max(_hexRot, rotDps) : rotDps;
        _hexT = Math.Max(_hexT, seconds);
    }
    private float _hexRot;
    /// <summary>Online: whose hex is rotting it (their hero is credited with the damage).</summary>
    private int _hexBy;

    /// <summary>Ticks the afflictions; false if it rotted (or burned) to death.</summary>
    private bool TickAfflictions(float dt)
    {
        if (_weakT > 0) _weakT -= dt;
        if (_hexT > 0) _hexT -= dt;
        if (_chillT > 0) _chillT -= dt;
        if (_hexT > 0 && _hexRot > 0 && CanBeHit)
        {
            float before = Hp;
            Hp -= _hexRot * dt;
            NetSync.CreditDealt(_hexBy, before - Math.Max(0, Hp));
            if (Hp <= 0) { LastAttacker = _hexBy; Die(); return false; }
        }
        if (_burnT > 0)
        {
            _burnT -= dt;
            if (CanBeHit)
            {
                float before = Hp;
                Hp -= _burnDps * dt;
                NetSync.CreditDealt(_burnBy, before - Math.Max(0, Hp));
                if (Hp <= 0) { LastAttacker = _burnBy; Die(); return false; }
            }
        }
        if (_weakT <= 0 && _hexT <= 0 && _burnT <= 0 && _chillT <= 0) return true;
        AfflictionFx(dt);
        return true;
    }

    /// <summary>A few motes drifting off whatever ails it (flames off a burning one, frost off a chilled one).</summary>
    private void AfflictionFx(float dt)
    {
        _moteT -= dt;
        if (_moteT > 0) return;
        _moteT = G.Range(0.08f, 0.16f);
        var at = GlobalPosition + new Vector2(G.Range(-1f, 1f) * HitRadius, G.Range(-1f, 0.4f) * HitRadius);
        if (_hexT > 0) G.Fx.Burst(at, new Color(0.6f, 0.95f, 0.45f, 0.8f), 1, 22, 1.5f, 0.7f, -40);
        if (_weakT > 0) G.Fx.Burst(at, new Color(1f, 0.35f, 0.25f, 0.7f), 1, 18, 1.3f, 0.6f, -30);
        if (_burnT > 0)
        {
            G.Fx.Ember(at, G.Chance(0.5f) ? new Color(1f, 0.55f, 0.15f) : new Color(1f, 0.82f, 0.35f));
            if (G.Chance(0.3f)) G.Fx.Smoke(at + new Vector2(0, -4), 1, new Color(0.2f, 0.17f, 0.15f, 0.45f), 30f);
        }
        if (_chillT > 0) G.Fx.Burst(at, new Color(0.75f, 0.92f, 1f, 0.8f), 1, 16, 1.4f, 0.7f, 30);
    }

    /// <summary>The colour an affliction washes over the creature (for the 3D model), alpha = strength.</summary>
    public Color AfflictionAura =>
        _iceT > 0 ? new Color(0.72f, 0.9f, 1f, 1f)
        : _burnT > 0 ? new Color(1f, 0.48f, 0.12f, 0.75f)
        : _chillT > 0 ? new Color(0.5f, 0.78f, 1f, 0.6f)
        : _hexT > 0 ? new Color(0.45f, 1f, 0.35f, 0.8f)
        : _weakT > 0 ? new Color(1f, 0.25f, 0.15f, 0.6f)
        : new Color(0, 0, 0, 0);

    // ---- the Elementalist's afflictions: burning, chilled, frozen solid
    private float _burnT, _burnDps, _chillT, _chillSlow = 1f, _iceT;
    /// <summary>Online: whose fire is burning it (their hero is credited with the damage).</summary>
    private int _burnBy;

    /// <summary>Set alight (a firebolt, a Firestorm).</summary>
    public bool Ignited => _burnT > 0;
    /// <summary>Slowed by frost (a frostbolt).</summary>
    public bool Chilled => _chillT > 0;
    /// <summary>Frozen solid in a block of ice: it can do nothing, and a snap shatters it.</summary>
    public bool FrozenSolid => _iceT > 0;
    /// <summary>Only a regular creature can be frozen solid (never a mini-boss, a guardian or a boss).</summary>
    public bool CanFreezeSolid => !Elite && !IsGuardian && !IsBoss;

    /// <summary>Sets it alight: it burns for <paramref name="dps"/> a second for <paramref name="seconds"/> (the hotter fire stays).</summary>
    public void Ignite(float dps, float seconds)
    {
        if (Dead || dps <= 0) return;
        if (Puppet) { _burnT = Math.Max(_burnT, 0.3f); NetSync.EffectPuppet(this, NetSync.Effect.Ignite, dps, seconds); return; }
        bool fresh = _burnT <= 0;
        _burnBy = NetSync.Striker;
        _burnDps = fresh ? dps : Math.Max(_burnDps, dps);
        _burnT = Math.Max(_burnT, seconds);
        if (fresh)
        {
            G.Fx.Flash(GlobalPosition, HitRadius + 6, new Color(1f, 0.55f, 0.15f), 0.12f);
            G.Fx.Text(GlobalPosition + new Vector2(0, -HitRadius - 12), "ALIGHT", new Color(1f, 0.6f, 0.2f), 9, 0.6f);
            G.Sfx.Play("lava", GlobalPosition, -12, 0.1f, 1.5f);
        }
    }

    /// <summary>Chills it: it moves and acts <paramref name="slow"/> slower (0.3 = 30%) for <paramref name="seconds"/>.</summary>
    public void Chill(float slow, float seconds)
    {
        if (Dead) return;
        if (Puppet) { _chillT = Math.Max(_chillT, 0.3f); NetSync.EffectPuppet(this, NetSync.Effect.Chill, slow, seconds); return; }
        float k = Math.Clamp(1f - slow, 0.1f, 1f);
        _chillSlow = _chillT > 0 ? Math.Min(_chillSlow, k) : k;
        _chillT = Math.Max(_chillT, seconds);
    }

    /// <summary>
    /// Freezes a regular creature solid for <paramref name="seconds"/>. Like every blow, it never
    /// catches a creature winding up or in the middle of an attack (the attack plays out as
    /// telegraphed). True if it froze.
    /// </summary>
    public bool FreezeSolid(float seconds)
    {
        if (Dead || !CanFreezeSolid || AttackingNow) return false;
        if (Puppet) { _iceT = Math.Max(_iceT, 0.3f); NetSync.EffectPuppet(this, NetSync.Effect.Frost, seconds); return true; }
        bool fresh = _iceT <= 0;
        _iceT = Math.Max(_iceT, seconds);
        if (fresh)
        {
            G.Fx.Ring(GlobalPosition, HitRadius + 10, new Color(0.8f, 0.95f, 1f), 0.3f);
            G.Fx.Burst(GlobalPosition, new Color(0.85f, 0.96f, 1f), 10, 90, 2f, 0.4f, 60);
            G.Fx.Text(GlobalPosition + new Vector2(0, -HitRadius - 12), "FROZEN", new Color(0.75f, 0.92f, 1f), 9, 0.7f);
            G.Sfx.Play("clink", GlobalPosition, -6, 0.05f, 0.7f);
        }
        return true;
    }

    /// <summary>Shatters the ice (a snap): it's free again at once.</summary>
    public void Thaw()
    {
        if (Dead) return;
        if (Puppet) { _iceT = 0; NetSync.EffectPuppet(this, NetSync.Effect.Thaw, 0); return; }
        if (_iceT > 0) { _iceT = 0; Thawed(); }
    }

    /// <summary>Puts its fire out (a cinder snap spends it).</summary>
    public void Quench()
    {
        if (Dead) return;
        if (Puppet) { _burnT = 0; NetSync.EffectPuppet(this, NetSync.Effect.Quench, 0); return; }
        _burnT = 0;
    }

    /// <summary>The ice gives way: a spray of frost, and it moves again.</summary>
    private void Thawed()
    {
        _iceT = 0;
        G.Fx.Burst(GlobalPosition, new Color(0.8f, 0.94f, 1f, 0.9f), 8, 110, 2f, 0.35f, 200);
        if (Anim != null) Anim.TimeMult = 1;
    }

    /// <summary>A small horizontal bounce back after landing a hit on the player.</summary>
    public void Recoil(float dirX)
    {
        if (ManualMove || IsBoss || dirX == 0) return;
        Velocity = new Vector2(Math.Sign(dirX) * Tune.Combat.StrikeRecoil * (1f - KnockResist * 0.7f), Velocity.Y);
    }

    protected void ApplyGravity(float dt, float mult = 1f)
    {
        var v = Velocity;
        v.Y = Math.Min(v.Y + Grav * mult * dt, 700);
        Velocity = v;
    }

    /// <summary>Returns the damage actually dealt (0 if immune). Online, a blow on a copy goes to the host.</summary>
    public float Hurt(float dmg, Vector2 knock, Vector2 hitPos)
    {
        if (Dead || !CanBeHit) return 0;
        if (Puppet) return PuppetHurt(dmg, knock, hitPos);
        // (online, the kill goes to whoever struck last: another game's hero, or this one's)
        LastAttacker = NetSync.Striker;
        NetSync.Scope++;
        try { return TakeHit(dmg, knock, hitPos); }
        finally { NetSync.Scope--; }
    }

    /// <summary>A blow lands (the host's own creature, or offline): subclasses add their own reactions.</summary>
    protected virtual float TakeHit(float dmg, Vector2 knock, Vector2 hitPos)
    {
        if (Dead || !CanBeHit) return 0;
        Awake = true;
        if (_hexT > 0) dmg *= _hexVuln;
        Hp -= dmg;
        HurtFlash = 0.12f;
        // a creature winding up or attacking keeps its pose and its timing: the blow flashes and
        // squashes it, but only a shield bash, a guarded charge or a perfect block breaks it off
        bool midAttack = Attacking;
        if (Anim != null)
        {
            Anim.Flash(1f);
            if (Hp > 0 && !midAttack) Anim.Once("hurt", 4);
            // squash-and-stretch punch away from the blow
            var baseScale = Vector2.One;
            Anim.Scale = new Vector2(1.25f, 0.8f);
            var tw = Anim.CreateTween();
            tw.TweenProperty(Anim, "scale", baseScale, 0.18f).SetTrans(Tween.TransitionType.Elastic).SetEase(Tween.EaseType.Out);
        }
        var k = knock * (1f - KnockResist);
        if (k.Length() > 60 && !ManualMove && !midAttack) { Stun = 0.2f; KnockVel = k; }
        else if (!ManualMove && knock.X != 0) Recoil(knock.X); // even without Heavy Pommel, a hit nudges it back
        bool big = dmg >= 15;
        G.Fx.Text(hitPos + new Vector2(0, -10), Mathf.RoundToInt(dmg).ToString(), big ? new Color(1f, 0.85f, 0.3f) : Colors.White, big ? 13 : 11);
        G.Fx.Directional(hitPos, knock.LengthSquared() > 1 ? knock.Normalized() : Vector2.Up, 0.8f, BloodColor, 7, 200, 2f, 0.35f, 300);
        G.Sfx.Play("hit", GlobalPosition, 0, 0.12f);
        OnHurt();
        if (Hp <= 0) Die();
        return dmg;
    }

    protected virtual void OnHurt() { }
    protected virtual Vector2 DeathDrift => Vector2.Zero;
    protected virtual Color BloodColor => new(0.75f, 0.1f, 0.12f);

    protected virtual void Die()
    {
        if (Dead) return;
        NetSync.Scope++;
        try { DieHere(); }
        finally { NetSync.Scope--; }
    }

    private void DieHere()
    {
        Dead = true;
        G.Sfx.Play("enemy_die", GlobalPosition, 0, 0.15f, Elite ? 0.7f : 1f);
        G.Fx.Burst(GlobalPosition, BloodColor, Elite ? 36 : 16, Elite ? 260 : 170, 2.8f, 0.6f);
        G.Fx.Ring(GlobalPosition, HitRadius + 6, new Color(1, 1, 1, 0.6f));
        int xp = (int)MathF.Round(XpValue * (Elite ? Meta.EliteXpMult : 1f));
        int orbs = Math.Clamp(xp / 2, 1, 12);
        int per = Math.Max(1, xp / orbs);
        for (int k = 0; k < orbs; k++)
            G.Spawn(new XpOrb { Value = per, Position = GlobalPosition, Vel = G.RandDir() * G.Range(60, 180) + new Vector2(0, -60) });
        // healing is scarce: rare from regular kills, likelier from mini-bosses; potions rarer still
        if (G.Chance(IsBoss || IsGuardian ? 1f : Elite ? Tune.Drops.HeartChanceElite : Tune.Drops.HeartChance)) G.Spawn(new HeartPickup { Position = GlobalPosition });
        if (G.Chance(Meta.PotionDropChance(P))) G.Spawn(new PotionPickup { Position = GlobalPosition + new Vector2(6, -4) });
        if (Elite || IsBoss) G.Fx.Explosion(GlobalPosition, BloodColor, IsBoss ? 1.6f : 1f);
        else G.Fx.Pop(GlobalPosition, BloodColor, HitRadius);
        // the kill goes to whoever landed the blow (online, maybe another game's hero)
        if (!Net.Online || LastAttacker == 0 || LastAttacker == Net.Me) G.Player?.OnKill();
        if (Elite && !IsBoss) G.Main.SlowMo(Tune.Feel.EliteKillSlowMo, Tune.Feel.EliteKillSlowMoScale);
        if (IsBoss) G.Main.SlowMo(Tune.Feel.BossKillSlowMo, Tune.Feel.BossKillSlowMoScale);
        BrainFlush(terminal: true);
        if (Net.IsHost && !_goneSent) { _goneSent = true; NetSync.EnemyGone(this, true); }
        OnDeath?.Invoke(this);
        Anim?.PlayDeathAndFree("death", Elite ? 1.2f : 0.5f, DeathDrift);
        QueueFree();
    }

    // ================================================================== online copies

    private readonly NetInterp _net = new();
    private string _netAnim = "";
    private int _netFrame, _netFacing = 1;
    private float _netSpeed = 1f;
    private ushort _netFlags;
    private const ushort NfReeling = 1, NfFrozen = 2, NfDazed = 4, NfHexed = 8, NfWeak = 16, NfFlash = 32, NfFloor = 64, NfAttacking = 128,
                         NfIgnited = 256, NfChilled = 512, NfIced = 1024;

    /// <summary>On the ground (a copy goes by what the host says).</summary>
    public bool OnGround => Puppet ? (_netFlags & NfFloor) != 0 : IsOnFloor();
    /// <summary>Mid-attack (a copy goes by what the host says).</summary>
    public bool AttackingNow => Puppet ? (_netFlags & NfAttacking) != 0 : Attacking;
    /// <summary>How much harder a blow lands on a copy right now (a hex), for the striker's own sums.</summary>
    public float PuppetVulnerability => (_netFlags & NfHexed) != 0 || _hexT > 0 ? Tune.Vitalist.HexVulnerability : 1f;

    /// <summary>The host's creature, as its copies need it.</summary>
    public void WriteNet(NetOut w)
    {
        w.Vec(GlobalPosition);
        w.HVec(Velocity);
        w.SByte((sbyte)(Face < 0 ? -1 : 1));
        var spr = Anim?.Sprite;
        w.Str(spr != null ? (string)spr.Animation : "");
        w.Byte((byte)Math.Clamp(spr?.Frame ?? 0, 0, 255));
        w.Half(spr != null ? spr.SpeedScale * Math.Max(0f, Anim.TimeMult) : 1f);
        w.Half(Math.Clamp(Hp / Math.Max(1f, MaxHp), 0f, 1f));
        ushort f = 0;
        if (Stun > 0) f |= NfReeling;
        if (_freeze > 0) f |= NfFrozen;
        if (_dazed && Stun > 0) f |= NfDazed;
        if (_hexT > 0) f |= NfHexed;
        if (_weakT > 0) f |= NfWeak;
        if (HurtFlash > 0) f |= NfFlash;
        if (IsOnFloor()) f |= NfFloor;
        if (Attacking) f |= NfAttacking;
        if (_burnT > 0) f |= NfIgnited;
        if (_chillT > 0) f |= NfChilled;
        if (_iceT > 0) f |= NfIced;
        w.UShort(f);
    }

    /// <summary>An update for a copy (or, for one this game doesn't have, read past it).</summary>
    public static void ReadNet(NetIn r, Enemy e, double now)
    {
        var pos = r.Vec();
        var vel = r.HVec();
        int face = r.SByte();
        string anim = r.Str();
        int frame = r.Byte();
        float speed = r.Half();
        float hp = r.Half();
        ushort flags = r.UShort();
        if (e == null) return;
        e._net.Push(now, pos, vel);
        e._netFacing = face;
        e._netAnim = anim;
        e._netFrame = frame;
        e._netSpeed = speed;
        bool flashNow = (flags & NfFlash) != 0 && (e._netFlags & NfFlash) == 0;
        e._netFlags = flags;
        e.Hp = hp * e.MaxHp;
        e.Stun = (flags & NfReeling) != 0 ? 0.1f : 0f;
        e._dazed = (flags & NfDazed) != 0;
        e._hexT = (flags & NfHexed) != 0 ? Math.Max(e._hexT, 0.15f) : e._hexT;
        e._weakT = (flags & NfWeak) != 0 ? Math.Max(e._weakT, 0.15f) : e._weakT;
        // (the host's word on the Elementalist's afflictions: a copy only shows them, and a snap
        // looks for them here)
        e._burnT = (flags & NfIgnited) != 0 ? Math.Max(e._burnT, 0.15f) : e._burnT;
        e._chillT = (flags & NfChilled) != 0 ? Math.Max(e._chillT, 0.15f) : e._chillT;
        e._iceT = (flags & NfIced) != 0 ? Math.Max(e._iceT, 0.15f) : e._iceT;
        if (flashNow) e.Anim?.Flash(0.8f);
    }

    /// <summary>A copy between updates: smoothed movement, the host's animation, its afflictions.</summary>
    private void PuppetTick(float dt)
    {
        if (Dead) return;
        T += dt;
        HurtFlash -= dt;
        if (_freeze > 0) _freeze -= dt;
        if (_hexT > 0) _hexT -= dt;
        if (_weakT > 0) _weakT -= dt;
        if (_burnT > 0) _burnT -= dt;
        if (_chillT > 0) _chillT -= dt;
        if (_iceT > 0) _iceT -= dt;
        if (_burnT > 0 || _chillT > 0) AfflictionFx(dt);
        if (_net.Sample(NetSync.Now - NetSync.InterpDelay, out var pos, out var vel))
        {
            GlobalPosition = pos;
            Velocity = vel;
        }
        Face = _netFacing;
        if (Anim != null)
        {
            bool shudder = _freeze > 0 || (_netFlags & NfFrozen) != 0;
            Anim.TimeMult = 1f;
            Anim.Position = shudder ? new Vector2(G.Range(-1.5f, 1.5f), G.Range(-0.8f, 0.8f)) : Vector2.Zero;
            if (_netAnim != "") Anim.Mirror(_netAnim, _netFrame, shudder ? 0f : _netSpeed, _netFacing);
            Anim.Motion(Velocity * MoveScale);
        }
        if (_dazed && Stun > 0) DazeFx(dt);
        QueueRedraw();
    }

    /// <summary>A blow from this game's hero on a copy: shown at once, and sent to the host to apply.</summary>
    private float PuppetHurt(float dmg, Vector2 knock, Vector2 hitPos)
    {
        HurtFlash = 0.12f;
        if (Anim != null)
        {
            Anim.Flash(1f);
            Anim.Scale = new Vector2(1.25f, 0.8f);
            var tw = Anim.CreateTween();
            tw.TweenProperty(Anim, "scale", Vector2.One, 0.18f).SetTrans(Tween.TransitionType.Elastic).SetEase(Tween.EaseType.Out);
        }
        return NetSync.HitPuppet(this, dmg, knock, hitPos);
    }

    /// <summary>The host's creature died (or was put away): the copy goes too, dying the same way.</summary>
    public void PuppetGone(bool died)
    {
        if (Dead) return;
        Dead = true;
        if (died) Anim?.PlayDeathAndFree("death", Elite ? 1.2f : 0.5f, DeathDrift);
        QueueFree();
    }

    /// <summary>
    /// A creature's own extra state its copies need to look right (a frog's tongue, a spider's
    /// thread, whether a skeleton is in pieces). One method both writes and reads it.
    /// </summary>
    public virtual void NetState(NetIO io) { }

    // ---- drawing helpers ----

    /// <summary>Sets a transform that flips by facing and scales by size (and optional extra rotation).</summary>
    protected void Begin(float rot = 0, float sx = 1, float sy = 1)
        => DrawSetTransform(Vector2.Zero, rot, new Vector2(Face * Size * sx, Size * sy));

    protected void End() => DrawSetTransform(Vector2.Zero, 0, Vector2.One);


    protected void DrawHealthBar()
    {
        DrawBrainLabel();
        if (!Elite || IsBoss || IsGuardian || Hp >= MaxHp) return;
        float w = 30 * Size * 0.6f;
        var pos = new Vector2(-w / 2, -HitRadius - 12);
        DrawRect(new Rect2(pos, new Vector2(w, 4)), new Color(0, 0, 0, 0.7f));
        DrawRect(new Rect2(pos, new Vector2(w * Math.Max(0, Hp / MaxHp), 4)), new Color(0.9f, 0.2f, 0.25f));
    }

    // ================================================================== neural brain
    // Each creature type exposes a small set of high-level moves (Actions). Every frame the
    // subclass's Think carries out the current Intent. Who sets the Intent:
    //  * scripted: Teacher() -- the original hand-written AI -- every frame;
    //  * brain:    the type's shared network, every DecisionInterval seconds, while not Busy.
    // Rewards (damage dealt to the player, damage taken, time alive) are collected between
    // decisions and fed back to the network while training.

    /// <summary>Inputs every creature feeds its brain, before the one-hot of its previous move.</summary>
    public const int BaseInputs = 32;

    /// <summary>Shared brain name (see BrainLocks), or null for creatures with no brain.</summary>
    protected virtual string BrainName => null;
    /// <summary>Names of the moves this creature can choose between; index = action id. 0 should be a safe "do nothing much".</summary>
    protected virtual string[] Actions => null;
    /// <summary>What the original scripted AI would do right now.</summary>
    protected virtual int Teacher() => 0;
    /// <summary>Whether a move is possible now (impossible ones are masked out of the choice).</summary>
    protected virtual bool CanAct(int a) => true;
    /// <summary>True mid-attack, airborne, etc.: no new decisions until it's free again.</summary>
    protected virtual bool Busy => false;
    /// <summary>0..1: how ready its main attack is.</summary>
    protected virtual float AttackReady => 1f;

    /// <summary>The move being carried out now.</summary>
    protected int Intent;
    /// <summary>Clears a one-shot move (an attack) once it has started, so it isn't repeated.</summary>
    protected void Consume()
    {
        // an attack under way keeps its slot until it has played out (see TickSlot)
        if (IsAttack(Intent)) _slotT = Math.Max(_slotT, Tune.Combat.SlotHold);
        Intent = 0;
    }

    // ---- attack etiquette
    /// <summary>Which of this creature's moves are attacks (they obey the first-attack delay and the attack slots).</summary>
    protected virtual bool IsAttack(int a) => false;
    private float _primeT;   // first-attack countdown
    private bool _primed;    // has wanted to attack at least once
    private float _slotT;    // > 0 while this creature holds one of the attack slots

    // Attack slots: of the creatures fighting the player and ready to strike, only a share
    // (Tune.Combat.AttackerShare, rounded up) may be attacking at once, so a crowd hits harder
    // than a lone creature but never all together. Counted once per physics frame.
    private static ulong _slotFrame = ulong.MaxValue;
    // per hero this frame: [slots allowed, slots taken] (online, each hero has its own crowd)
    private static readonly Dictionary<Player, int[]> _slots = new();

    private static int[] CountSlots(Player p)
    {
        ulong frame = Engine.GetPhysicsFrames();
        if (frame != _slotFrame) { _slotFrame = frame; _slots.Clear(); }
        if (p == null) return new[] { 1, 0 };
        if (_slots.TryGetValue(p, out var c)) return c;
        int ready = 0, taken = 0;
        float r2 = Tune.Combat.SlotRange * Tune.Combat.SlotRange;
        foreach (var e in G.Enemies)
        {
            if (e.Dead || e.Puppet || !e.Awake || e.IsBoss || e.IsGuardian || e.GlobalPosition.DistanceSquaredTo(p.GlobalPosition) > r2) continue;
            if (e._slotT > 0) { taken++; ready++; }
            else if (e.AttackReady >= 0.99f) ready++;
        }
        c = new[] { Math.Max(1, (int)MathF.Ceiling(ready * Tune.Combat.AttackerShare - 1e-3f)), taken };
        _slots[p] = c;
        return c;
    }

    /// <summary>
    /// Holds back an attack when (1) this is the first time the creature wants to attack: it waits
    /// FirstAttackDelay first, so nothing strikes the moment it drops into view; or (2) every attack
    /// slot near the player is taken. Bosses and guardians, and creatures far off, never wait.
    /// </summary>
    private void GateAttack()
    {
        if (!IsAttack(Intent)) return;
        if (!_primed) { _primed = true; _primeT = Tune.Combat.FirstAttackDelay; }
        if (_primeT > 0) { Intent = 0; return; }
        if (IsBoss || IsGuardian || _slotT > 0 || DistP > Tune.Combat.SlotRange) return;
        var slots = CountSlots(P);
        if (slots[1] >= slots[0]) { Intent = 0; return; }
        // reserve a slot now (others deciding this frame see it taken); Consume holds it longer
        slots[1]++;
        _slotT = Tune.Combat.SlotReserve;
    }

    /// <summary>A slot drains only while the creature is free again, so it is held for the whole attack.</summary>
    private float _slotBusy;
    private void TickSlot(float dt)
    {
        if (_slotT <= 0) { _slotBusy = 0; return; }
        if (!Busy) { _slotT -= dt; _slotBusy = 0; }
        // caught up in something long (stunned, hanging on a thread, burrowed) rather than an
        // attack: let the slot go, so it can't keep everyone else waiting
        else if ((_slotBusy += dt) > Tune.Combat.SlotMaxBusy && !Attacking) _slotT = 0;
    }

    public Brain Brain => _brain;
    public bool BrainDriven { get; private set; }
    public string IntentName => Actions != null && Intent >= 0 && Intent < Actions.Length ? Actions[Intent] : "";

    private Brain _brain;
    private float _decideT = -1, _hpSeen = float.NaN;
    private float _dealtTrace, _takenTrace, _alive;
    private float[] _probs;
    // the decision awaiting its outcome
    private bool _pending, _pendingLearn;
    private Brain.Transition _pendingT;
    private float _pendingReward, _pendingTime, _pendingDealt, _pendingTaken;

    private bool HasBrain => Actions != null && BrainName != null && Tune.Brains.Enabled;

    /// <summary>Called by Player.Hurt (directly or through this creature's projectiles).</summary>
    public void CreditDamage(float dmg)
    {
        if (!HasBrain || dmg <= 0) return;
        float frac = dmg / Math.Max(1f, P?.Stats.MaxHp ?? 60f);
        _pendingReward += Tune.Brains.DamageDealtReward * frac * 10f;
        _pendingDealt += dmg;
        _dealtTrace += frac * 5f;
    }

    private void BrainAccount(float dt)
    {
        if (!HasBrain) return;
        float hp = Math.Max(0, Hp);
        if (float.IsNaN(_hpSeen)) _hpSeen = hp;
        if (hp < _hpSeen)
        {
            float frac = (_hpSeen - hp) / Math.Max(1f, MaxHp);
            _pendingReward -= Tune.Brains.DamageTakenPenalty * frac;
            _pendingTaken += frac;
            _takenTrace += frac * 3f;
        }
        _hpSeen = hp;
        float decay = MathF.Exp(-dt / 1.5f);
        _dealtTrace *= decay; _takenTrace *= decay;
        if (!Awake) return;
        _alive += dt;
        if (_pending) { _pendingReward -= Tune.Brains.TimePenaltyPerSec * dt; _pendingTime += dt; }
    }

    private void BrainDecide(float dt)
    {
        if (!HasBrain) { BrainDriven = false; return; }
        _brain ??= Brains.Get(BrainName, BaseInputs + Actions.Length, Actions.Length);
        if (!Brains.Drives(_brain) || !Awake)
        {
            BrainDriven = false;
            Intent = Teacher();
            GateAttack();
            return;
        }
        BrainDriven = true;
        _decideT -= dt;
        if (_decideT > 0 || Busy || !Brains.TakeDecisionSlot()) return;
        _decideT = Tune.Brains.DecisionInterval + G.Range(-1f, 1f) * Tune.Brains.DecisionJitter;

        int n = Actions.Length;
        var x = new float[BaseInputs + n];
        FillFeatures(x);
        var mask = new bool[n];
        bool any = false;
        for (int a = 0; a < n; a++) { mask[a] = CanAct(a); any |= mask[a]; }
        if (!any) mask[0] = true;

        bool learn = Brains.Learns(_brain);
        float value = 0;
        if (_pending && _pendingLearn && learn)
            Commit(_brain.Value(x));

        int teacher = Teacher();
        if (teacher < 0 || teacher >= n || !mask[teacher]) teacher = -1;
        _probs ??= new float[n];
        int action;
        bool forced = false;
        if (learn && teacher >= 0 && G.Chance(Math.Min(0.9f, _brain.TeacherWeight)))
        {
            action = teacher; forced = true;
        }
        else action = _brain.Choose(x, mask, learn, out value, _probs);
        Intent = action;
        GateAttack();

        _pending = true;
        _pendingLearn = learn;
        _pendingT = new Brain.Transition { X = x, Mask = mask, Action = action, Teacher = teacher, Forced = forced };
        _pendingReward = 0; _pendingTime = 0; _pendingDealt = 0; _pendingTaken = 0;
        if (learn) { _brain.Experience++; _brain.Dirty = true; }
    }

    /// <summary>Closes the pending decision: target = reward + discounted value of where it led.</summary>
    private void Commit(float nextValue)
    {
        float steps = Math.Max(1f, _pendingTime / Math.Max(0.01f, Tune.Brains.DecisionInterval));
        _pendingT.Target = _pendingReward + MathF.Pow(Tune.Brains.Gamma, steps) * nextValue;
        _brain.Remember(_pendingT);
        _brain.NoteStats(_pendingReward, _pendingDealt, _pendingTaken);
        _pending = false;
    }

    /// <summary>On death (terminal: no future) or on leaving the level (bootstrap from the last state).</summary>
    private void BrainFlush(bool terminal)
    {
        if (!_pending || !_pendingLearn || _brain == null) { _pending = false; return; }
        if (terminal) BrainAccount(0); // count the killing blow
        Commit(terminal ? 0f : _brain.Value(_pendingT.X));
    }

    private void FillFeatures(float[] x)
    {
        var p = P;
        var to = ToP;
        float dist = Math.Max(1f, to.Length());
        var pos = GlobalPosition;
        var cave = G.Cave;
        int sx = to.X >= 0 ? 1 : -1;
        int i = 0;
        x[i++] = to.X / dist;                                   // direction to the player (angle substitute)
        x[i++] = to.Y / dist;
        x[i++] = Math.Min(dist / 500f, 2f);                     // distance
        x[i++] = MathF.Exp(-dist / 80f);                        // closeness (sharp near the player)
        x[i++] = MathF.Tanh(to.X / 150f);                       // precise offsets at close range
        x[i++] = MathF.Tanh(to.Y / 150f);
        x[i++] = Math.Clamp(p.Velocity.X / 300f, -2, 2);        // player motion
        x[i++] = Math.Clamp(p.Velocity.Y / 300f, -2, 2);
        x[i++] = Math.Clamp(Velocity.X / 300f, -2, 2);          // own motion
        x[i++] = Math.Clamp(Velocity.Y / 300f, -2, 2);
        x[i++] = Hp / Math.Max(1f, MaxHp);
        x[i++] = p.Hp / Math.Max(1f, p.Stats.MaxHp);
        x[i++] = SeesP ? 1 : 0;
        x[i++] = IsOnFloor() ? 1 : 0;
        x[i++] = InWater ? 1 : 0;
        x[i++] = p.InWater ? 1 : 0;
        x[i++] = p.OnGround ? 1 : 0;
        x[i++] = p.IsSwinging ? 1 : 0;                          // danger: the dagger is out
        x[i++] = p.Guarding ? 1 : 0;                           // dodging, invulnerable, or shield up
        x[i++] = p.Facing * -sx;                                // +1 = the player is facing me
        x[i++] = p.SecondaryReady ? 1 : 0;                      // a Charged Strike / Guarded Charge / heal could be coming
        x[i++] = cave.IsSolid(pos + new Vector2(sx * 20, 0)) ? 1 : 0;   // wall between us
        x[i++] = !cave.IsSolid(pos + new Vector2(sx * 16, 30)) && !cave.IsSolid(pos + new Vector2(sx * 16, 60)) ? 1 : 0; // gap toward the player
        x[i++] = cave.IsSolid(pos + new Vector2(0, -40)) ? 1 : 0;     // low ceiling
        // nearest ally, and how crowded it is
        Enemy near = null; float nd = float.MaxValue; int allies = 0;
        foreach (var e in G.Enemies)
        {
            if (e == this || e.Dead) continue;
            float d = e.GlobalPosition.DistanceSquaredTo(pos);
            if (d < 200 * 200) allies++;
            if (d < nd) { nd = d; near = e; }
        }
        var toA = near != null ? near.GlobalPosition - pos : new Vector2(400, 0);
        x[i++] = MathF.Tanh(toA.X / 200f);
        x[i++] = MathF.Tanh(toA.Y / 200f);
        x[i++] = Math.Min(allies / 5f, 1.5f);
        x[i++] = MathF.Tanh(_alive / 20f);                      // how long it has been fighting
        x[i++] = Math.Min(_takenTrace, 2f);                     // recently hurt
        x[i++] = Math.Min(_dealtTrace, 2f);                     // recently landed a hit
        x[i++] = G.Tempo - 1f;                                  // difficulty
        x[i++] = AttackReady;
        // one-hot of the previous move
        for (int a = 0; a < Actions.Length; a++) x[BaseInputs + a] = a == Intent ? 1 : 0;
        if (i != BaseInputs) throw new InvalidOperationException($"feature count {i} != {BaseInputs}");
    }

    private void DrawBrainLabel()
    {
        if (!Brains.Training || !BrainDriven || !Brains.ShowLabels) return;
        var font = ThemeDB.FallbackFont;
        string txt = IntentName + (Brains.IsLocked(BrainName) ? " *" : "");
        var col = Brains.IsLocked(BrainName) ? new Color(0.6f, 0.8f, 1f, 0.9f) : new Color(1f, 0.9f, 0.4f, 0.9f);
        var at = new Vector2(-40, -HitRadius - 16);
        DrawStringOutline(font, at, txt, HorizontalAlignment.Center, 80, 7, 2, new Color(0, 0, 0, 0.8f));
        DrawString(font, at, txt, HorizontalAlignment.Center, 80, 7, col);
    }
}
