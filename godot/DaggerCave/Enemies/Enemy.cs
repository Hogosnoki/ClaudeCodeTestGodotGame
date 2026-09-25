using System;
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
    /// <summary>What every hit this creature deals is multiplied by: difficulty curve times its own multiplier.</summary>
    protected float DmgK => G.DepthDmg * DmgMult;

    protected float T, HurtFlash, Stun;
    protected SpriteAnimator Anim;
    protected Vector2 KnockVel;
    protected float Face = 1;
    protected bool Awake;
    /// <summary>When true, the subclass positions itself in Think and the base skips MoveAndSlide.</summary>
    protected bool ManualMove;
    protected bool ContactActive = true;
    protected virtual bool UsesGravity => true;
    public virtual bool CanBeHit => true;
    public virtual float HitRadius => BodyRadius * Size;

    protected Player P => G.Player;
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
        // Health is fixed at spawn from the difficulty curve; damage and tempo follow it live.
        MaxHp *= G.DepthHp;
        Hp = MaxHp;
        AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = BodyRadius * Size * 0.9f } });
        G.Enemies.Add(this);
        Face = G.Chance(0.5f) ? 1 : -1;
        Setup();
        DisplayName = (Elite && !IsGuardian && !IsBoss ? "Elite " : "") + NamePrefix + DisplayName;
        if (Tint is Color tint && Anim != null) Anim.Sprite.SelfModulate = tint;
    }

    public override void _ExitTree()
    {
        G.Enemies.Remove(this);
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
        if (Dead) return;
        var p = P;
        if (p == null) return;
        float dist = DistP;
        if (Hunting && !IsBoss && dist > 1700) { QueueFree(); return; } // wandered off-stage
        if (!IsBoss && dist > 1500) return; // asleep
        if (!Awake && dist < 420) Awake = true;
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
        // Enemies run on their own clock, sped up by the difficulty curve: movement, cooldowns
        // and animations all scale together.
        float tempo = G.Tempo;
        float dt = (float)delta * tempo;
        T += dt; HurtFlash -= (float)delta;
        if (_primeT > 0) _primeT -= dt;
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
        if (ContactActive && touch > 0 && !p.Dead && dist < HitRadius + 7)
        {
            // a strike that lands, or that the shield stops, is spent
            if ((p.Hurt(touch * DmgK, GlobalPosition, source: this) > 0 || p.LastHitBlocked) && striking) _strikeLanded = true;
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
        float left = _bleedT > 0 ? _bleedDps * _bleedT : 0;
        _bleedT = seconds;
        _bleedDps = (left + total) / seconds;
    }

    /// <summary>Freezes just this creature for a hit-stop.</summary>
    public void Freeze(float seconds) { if (!Dead) _freeze = Math.Max(_freeze, seconds); }

    /// <summary>True while a body attack is under way: touching the player then hurts (once).</summary>
    protected virtual bool Striking => false;
    private bool _wasStriking, _strikeLanded;

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

    /// <summary>Returns the damage actually dealt (0 if immune).</summary>
    public virtual float Hurt(float dmg, Vector2 knock, Vector2 hitPos)
    {
        if (Dead || !CanBeHit) return 0;
        Awake = true;
        Hp -= dmg;
        HurtFlash = 0.12f;
        if (Anim != null)
        {
            Anim.Flash(1f);
            if (Hp > 0) Anim.Once("hurt", 4);
            // squash-and-stretch punch away from the blow
            var baseScale = Vector2.One;
            Anim.Scale = new Vector2(1.25f, 0.8f);
            var tw = Anim.CreateTween();
            tw.TweenProperty(Anim, "scale", baseScale, 0.18f).SetTrans(Tween.TransitionType.Elastic).SetEase(Tween.EaseType.Out);
        }
        var k = knock * (1f - KnockResist);
        if (k.Length() > 60 && !ManualMove) { Stun = 0.2f; KnockVel = k; }
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
        P?.OnKill();
        if (Elite && !IsBoss) G.Main.SlowMo(Tune.Feel.EliteKillSlowMo, Tune.Feel.EliteKillSlowMoScale);
        if (IsBoss) G.Main.SlowMo(Tune.Feel.BossKillSlowMo, Tune.Feel.BossKillSlowMoScale);
        BrainFlush(terminal: true);
        OnDeath?.Invoke(this);
        Anim?.PlayDeathAndFree("death", Elite ? 1.2f : 0.5f, DeathDrift);
        QueueFree();
    }

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
        if (IsAttack(Intent)) _lastAttackStart = G.RunTime; // this creature has the floor for a moment
        Intent = 0;
    }

    // ---- attack etiquette
    /// <summary>Which of this creature's moves are attacks (they obey the first-attack delay and take turns).</summary>
    protected virtual bool IsAttack(int a) => false;
    private float _primeT;                       // first-attack countdown
    private bool _primed;                        // has wanted to attack at least once
    private static float _lastAttackStart = -99; // shared: when any nearby creature last started an attack

    /// <summary>
    /// Holds back an attack when (1) this is the first time the creature wants to attack: it waits
    /// FirstAttackDelay first, so nothing strikes the moment it drops into view; or (2) another
    /// creature near the player started an attack less than AttackStagger ago: they take turns.
    /// </summary>
    private void GateAttack()
    {
        if (!IsAttack(Intent)) return;
        if (!_primed) { _primed = true; _primeT = Tune.Combat.FirstAttackDelay; }
        bool waiting = _primeT > 0;
        bool turn = IsBoss || G.RunTime - _lastAttackStart >= Tune.Combat.AttackStagger || DistP > 600;
        if (waiting || !turn) Intent = 0;
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
        x[i++] = p.IsOnFloor() ? 1 : 0;
        x[i++] = p.IsSwinging ? 1 : 0;                          // danger: the dagger is out
        x[i++] = p.Guarding ? 1 : 0;                           // dodging, invulnerable, or shield up
        x[i++] = p.Facing * -sx;                                // +1 = the player is facing me
        x[i++] = p.SecondaryReady ? 1 : 0;                      // a throw / barrier could be coming
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
