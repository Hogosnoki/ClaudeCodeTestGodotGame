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
        FloorMaxAngle = Mathf.DegToRad(50);
        FloorSnapLength = 6f;
        ZIndex = 0;
        // Health is fixed at spawn from the difficulty curve; damage and tempo follow it live.
        MaxHp *= G.DepthHp;
        Hp = MaxHp;
        AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = BodyRadius * Size * 0.9f } });
        G.Enemies.Add(this);
        Face = G.Chance(0.5f) ? 1 : -1;
        Setup();
    }

    public override void _ExitTree() => G.Enemies.Remove(this);

    protected virtual void Setup() { }

    /// <summary>Attaches this creature's sprite sheet (call from Setup).</summary>
    protected void UseSprite(string set)
    {
        Anim = SpriteAnimator.Create(set, Size);
        Anim.Facing = (int)Face;
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
        DisplayName = "Elite " + DisplayName;
    }

    protected abstract void Think(float dt);

    /// <summary>
    /// True for enemies spawned as an "entrance" (they arrive from off-screen and come looking for
    /// the player rather than waiting to be found).
    /// </summary>
    public bool Hunting;

    /// <summary>Makes this enemy seek the player from out of view. Call before adding to the tree.</summary>
    public virtual void Engage() { Awake = true; Hunting = true; }

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
        // Enemies run on their own clock, sped up by the difficulty curve: movement, cooldowns
        // and animations all scale together.
        float tempo = G.Tempo;
        float dt = (float)delta * tempo;
        T += dt; HurtFlash -= (float)delta;

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
            Think(dt);
            if (!ManualMove)
            {
                Velocity *= tempo;
                MoveAndSlide();
                Velocity /= tempo;
            }
        }
        if (Anim != null)
        {
            Anim.TimeMult = tempo;
            Animate();
            Anim.Face((int)Face);
        }

        if (ContactActive && ContactDamage > 0 && !p.Dead && dist < HitRadius + 7)
            p.Hurt(ContactDamage * G.DepthDmg, GlobalPosition);
        QueueRedraw();
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
        int orbs = Math.Clamp(XpValue / 2, 1, 12);
        int per = Math.Max(1, XpValue / orbs);
        for (int k = 0; k < orbs; k++)
            G.Spawn(new XpOrb { Value = per, Position = GlobalPosition, Vel = G.RandDir() * G.Range(60, 180) + new Vector2(0, -60) });
        // healing is scarce: rare from regular kills, likely (not certain) from mini-bosses
        if (G.Chance(IsBoss ? 1f : Elite ? Tune.Drops.HeartChanceElite : Tune.Drops.HeartChance)) G.Spawn(new HeartPickup { Position = GlobalPosition });
        P?.OnKill();
        if (Elite && !IsBoss) G.Main.SlowMo(Tune.Feel.EliteKillSlowMo, Tune.Feel.EliteKillSlowMoScale);
        if (IsBoss) G.Main.SlowMo(Tune.Feel.BossKillSlowMo, Tune.Feel.BossKillSlowMoScale);
        OnDeath?.Invoke(this);
        Anim?.PlayDeathAndFree("death", Elite ? 1.2f : 0.5f, DeathDrift);
        QueueFree();
    }

    // ---- drawing helpers ----

    /// <summary>Sets a transform that flips by facing and scales by size (and optional extra rotation).</summary>
    protected void Begin(float rot = 0, float sx = 1, float sy = 1)
        => DrawSetTransform(Vector2.Zero, rot, new Vector2(Face * Size * sx, Size * sy));

    protected void End() => DrawSetTransform(Vector2.Zero, 0, Vector2.One);

    protected Color Tint(Color c) => HurtFlash > 0 ? new Color(1, 1, 1, c.A) : c;

    protected void DrawHealthBar()
    {
        if (!Elite || IsBoss || Hp >= MaxHp) return;
        float w = 30 * Size * 0.6f;
        var pos = new Vector2(-w / 2, -HitRadius - 12);
        DrawRect(new Rect2(pos, new Vector2(w, 4)), new Color(0, 0, 0, 0.7f));
        DrawRect(new Rect2(pos, new Vector2(w * Math.Max(0, Hp / MaxHp), 4)), new Color(0.9f, 0.2f, 0.25f));
    }
}
