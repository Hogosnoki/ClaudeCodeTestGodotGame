using System;
using Godot;

namespace DaggerCave;

/// <summary>
/// Creatures that defend themselves (<see cref="Tune.EnemyDefense"/>). The quick ones (<see cref="Agile"/>) dodge a blow now and
/// then, hopping out of its way; the strong ones (<see cref="Guards"/>) block what comes at them from the front with a guard that
/// holds so much before it breaks and leaves them reeling. Neither works while the creature is attacking, stunned, frozen, held
/// or asleep, so a hero who strikes as it commits, from behind, or before it has noticed, gets through. The heroes' answers to it
/// are boons (Player/Boons.cs): blows that pierce a guard, wear it down faster, or come back twice as hard after being stopped;
/// foes that dodge less, and a strike after a dodge that can't be dodged.
/// </summary>
public partial class Enemy
{
    /// <summary>Quick on its feet: it dodges blows now and then.</summary>
    public virtual bool Agile => false;
    /// <summary>Strong and steady: it blocks blows from the front with a guard that breaks after so much.</summary>
    public virtual bool Guards => false;

    private float _dodgeCd, _dodgeT, _guard = -1f, _sinceBlock, _guardBrokenT;
    private bool _guardCracked;

    /// <summary>Its guard now and at full (0 for a creature that doesn't block), and whether it's broken (for the tests).</summary>
    public float GuardLeft => Guards ? Math.Max(0f, _guard < 0f ? GuardMax : _guard) : 0f;
    public float GuardMax => Guards ? MaxHp * (Elite || IsGuardian ? Tune.EnemyDefense.GuardShareBig : Tune.EnemyDefense.GuardShare) : 0f;
    public bool GuardBroken => _guardBrokenT > 0f;
    /// <summary>Blows it has dodged and blocked (for the tests and the run's numbers).</summary>
    public int Dodges { get; private set; }
    public int Blocks { get; private set; }

    /// <summary>Free to defend itself: awake, not dead, not attacking, stunned, frozen solid or held.</summary>
    private bool FreeToDefend => Awake && !Dead && !Puppet && !IsBoss && _pin <= 0f && Stun <= 0f && !FrozenSolid && !Attacking;

    /// <summary>
    /// A hero's blow about to land (the host's own creature, or offline): a quick creature may dodge it (nothing lands), a strong
    /// one may block it with its guard (what the guard holds is taken off). Returns what's left of the blow.
    /// </summary>
    private float Defend(float dmg, Vector2 knock, Vector2 hitPos, DamageKind kind)
    {
        if (!Tune.EnemyDefense.On || kind == DamageKind.Raw || dmg <= 0f || Dead) return dmg;
        if (!Agile && !Guards) return dmg;
        var hero = StrikerHero();
        var st = hero?.Stats;
        double now = Time.GetTicksMsec() / 1000.0;
        // (still in the dodge it began: everything in the window misses)
        if (_dodgeT > 0f) return 0f;
        if (Agile && FreeToDefend && _dodgeCd <= 0f && !(hero != null && hero.UndodgeableUntil > now))
        {
            float chance = Tune.EnemyDefense.DodgeChance * (1f - Math.Clamp(st?.DodgeDenied ?? 0f, 0f, 1f));
            if (G.Chance(chance)) { Dodge(hero, knock); return 0f; }
        }
        if (Guards && FreeToDefend && !GuardBroken)
        {
            // only what comes at it from the side it faces
            var from = hero != null ? hero.GlobalPosition : GlobalPosition - knock;
            if (FacingAwayFrom(from)) return dmg;
            if (_guard < 0f) _guard = GuardMax;
            if (_guard <= 0f) return dmg;
            float pierce = Math.Clamp(st?.BlockPierce ?? 0f, 0f, 1f);
            float crush = Math.Max(1f, st?.GuardCrush ?? 1f);
            // the guard stops the blow (all of it, less what pierces it) as far as it holds; a crushing blow wears it faster
            float stop = Math.Min(dmg * (1f - pierce), _guard / crush);
            _guard -= stop * crush;
            _sinceBlock = 0f;
            Blocks++;
            Blocked(hero, stop, now);
            if (_guard <= 0.001f) BreakGuard(knock);
            else if (!_guardCracked && _guard < GuardMax * Tune.EnemyDefense.GuardCrackShare)
            {
                _guardCracked = true;
                G.Fx.Text(HeadPoint(10f), "GUARD CRACKING", new Color(1f, 0.75f, 0.45f), 10, 0.8f);
            }
            return dmg - stop;
        }
        return dmg;
    }

    /// <summary>A dodge: it hops back from the blow, untouchable for a moment; the hero who swung may answer it (a boon).</summary>
    private void Dodge(Player hero, Vector2 knock)
    {
        Dodges++;
        _dodgeT = Tune.EnemyDefense.DodgeWindow;
        _dodgeCd = Tune.EnemyDefense.DodgeCooldown;
        float away = knock.X != 0f ? Math.Sign(knock.X) : hero != null ? Math.Sign(GlobalPosition.X - hero.GlobalPosition.X) : -Face;
        if (away == 0f) away = -Face;
        if (!ManualMove) KnockVel = new Vector2(away * Tune.EnemyDefense.DodgeHop, -Tune.EnemyDefense.DodgeLift);
        G.Fx.Text(HeadPoint(10f), "DODGE", new Color(0.75f, 0.95f, 1f), 10, 0.6f);
        G.Fx.Directional(GlobalPosition, new Vector2(-away, 0), 0.4f, new Color(0.85f, 0.95f, 1f, 0.55f), 6, 120, 1.6f, 0.25f, 0, 0);
        G.Fx.Dust(GlobalPosition + new Vector2(0, HitRadius * 0.8f), 3, 1f);
        G.Sfx.Play("throw", GlobalPosition, -10, 0.15f, 1.5f);
        if (hero != null && hero.Stats.DodgedNextMult > 0f)
        {
            // Second Swing and its kin: the next strike can't be dodged, and lands harder
            hero.UndodgeableUntil = Time.GetTicksMsec() / 1000.0 + Tune.EnemyDefense.AnswerSeconds;
            hero.ArmNextHit(Math.Max(hero.NextHitMult, 1f + hero.Stats.DodgedNextMult), Tune.EnemyDefense.AnswerSeconds);
        }
    }

    /// <summary>A block: the guard rings and holds; the hero who struck may answer it (a boon).</summary>
    private void Blocked(Player hero, float stopped, double now)
    {
        G.Fx.Text(HeadPoint(6f), "BLOCKED", new Color(0.7f, 0.85f, 1f), 10, 0.5f);
        G.Fx.Spark(GlobalPosition + new Vector2(Face * HitRadius * 0.6f, -HitRadius * 0.3f), new Color(0.8f, 0.9f, 1f));
        G.Fx.Ring(GlobalPosition + new Vector2(Face * HitRadius * 0.5f, -HitRadius * 0.3f), HitRadius * 0.6f, new Color(0.75f, 0.88f, 1f, 0.7f), 0.2f, FxLayer.RingStyle.Ping);
        G.Sfx.Play("clink", GlobalPosition, -4, 0.1f, 1.1f);
        if (hero != null && hero.Stats.BlockedNextMult > 0f && stopped > 0f)
            hero.ArmNextHit(Math.Max(hero.NextHitMult, 1f + hero.Stats.BlockedNextMult), Tune.EnemyDefense.AnswerSeconds);
    }

    /// <summary>The guard gives way: the creature reels, its attack broken off, and the guard comes back only after a rest.</summary>
    private void BreakGuard(Vector2 knock)
    {
        _guard = 0f;
        _guardBrokenT = Tune.EnemyDefense.GuardBreakRest;
        G.Fx.Text(HeadPoint(14f), "GUARD BROKEN", new Color(1f, 0.6f, 0.35f), 12, 1f);
        G.Fx.Burst(GlobalPosition, new Color(0.85f, 0.9f, 1f), 12, 170, 2.4f, 0.4f);
        G.Sfx.Play("rock", GlobalPosition, -2, 0.1f, 1.4f);
        Interrupt(new Vector2(Math.Sign(knock.X) * 120f, -40f), Tune.EnemyDefense.GuardBreakStun, "GUARD BROKEN");
    }

    /// <summary>Each frame: the dodge's cooldown, the guard coming back.</summary>
    private void TickDefense(float dt)
    {
        if (_dodgeT > 0f) _dodgeT -= dt;
        if (_dodgeCd > 0f) _dodgeCd -= dt;
        if (!Guards) return;
        if (_guard < 0f) _guard = GuardMax;
        if (_guardBrokenT > 0f)
        {
            _guardBrokenT -= dt;
            if (_guardBrokenT <= 0f) { _guard = GuardMax * 0.5f; _guardCracked = false; }
            return;
        }
        _sinceBlock += dt;
        if (_sinceBlock >= Tune.EnemyDefense.GuardRegenDelay && _guard < GuardMax)
        {
            _guard = Math.Min(GuardMax, _guard + GuardMax * Tune.EnemyDefense.GuardRegenRate * dt);
            if (_guard >= GuardMax * Tune.EnemyDefense.GuardCrackShare) _guardCracked = false;
        }
    }
}

// ---- who dodges, and who blocks

public partial class Rat { public override bool Agile => true; }
public partial class Bat { public override bool Agile => true; }
public partial class Spider { public override bool Agile => true; }
public partial class Goblin { public override bool Agile => true; }
public partial class Hornet { public override bool Agile => true; }
public partial class Mite { public override bool Agile => true; }
public partial class AcidNewt { public override bool Agile => true; }
public partial class FrostWraith { public override bool Agile => true; }
public partial class Eel { public override bool Agile => true; }
public partial class Shardling { public override bool Agile => true; }

public partial class Bear { public override bool Guards => true; }
public partial class Golem { public override bool Guards => true; }
public partial class Skeleton { public override bool Guards => true; }
public partial class Automaton { public override bool Guards => true; }
public partial class Crab { public override bool Guards => true; }
public partial class Scorpion { public override bool Guards => true; }
public partial class Boiler { public override bool Guards => true; }
