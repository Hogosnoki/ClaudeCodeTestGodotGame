using System;
using System.Linq;
using Godot;

namespace DaggerCave;

/// <summary>
/// The Rogue's kit. Two daggers: the attack button jabs with them (quick, one creature at a time,
/// now and then a critical strike twice as hard; half as fast with one thrown); the ability button
/// throws one, and it sticks in the creature it meets (a miss comes back by itself, and with both
/// out, both come home); the second ability recalls them, each tearing back out through its
/// creature and yanking it toward you. The dodge button vanishes: creatures lose you, and you move
/// half again as fast, until you strike or are struck. Innate: sliding down walls and kicking off
/// them. Alterations: Ricochet, Smoke Bomb and Tether.
/// </summary>
public partial class Player
{
    private bool IsRogue => Stats.Hero == HeroKind.Rogue;

    private readonly ThrownDagger[] _thrown = new ThrownDagger[2];
    private float _vanishT, _throwCd;
    private bool _surpriseReady;
    private ThrownDagger _tetherTo;
    private float _tetherT;
    private bool _tetherSurprise, _swingSurprise;

    /// <summary>Daggers in hand (0-2).</summary>
    public int DaggersInHand => (_thrown[0] == null ? 1 : 0) + (_thrown[1] == null ? 1 : 0);
    /// <summary>For the 3D model: whether dagger <paramref name="k"/> is in hand (0 right, 1 left); a copy goes by its game's flags.</summary>
    public bool DaggerInHand(int k) => IsRemote ? (_netFlags & (k == 0 ? HfDagger0Out : HfDagger1Out)) == 0 : _thrown[k] == null;
    /// <summary>Daggers out of the hand now (for the tests and the HUD).</summary>
    public ThrownDagger ThrownDaggerAt(int k) => _thrown[k];
    /// <summary>Hidden from creatures: vanished, or inside a cloud of smoke (a copy goes by its game's flags).</summary>
    public new bool Hidden => IsRemote ? (_netFlags & HfHidden) != 0 : _vanishT > 0 || (SmokeCloud.All.Count > 0 && SmokeCloud.Covers(GlobalPosition));
    /// <summary>Vanished (the Rogue alone, not smoke): half again as fast.</summary>
    public bool Vanished => _vanishT > 0;
    public float VanishLeft => _vanishT;
    /// <summary>How long a vanishing lasts (Deep Shadows adds to it).</summary>
    public float VanishTotal => Tune.Rogue.VanishSeconds + Stats.VanishBonus;
    /// <summary>Critical strikes, backstabs and surprise attacks landed (for the tests).</summary>
    public int Crits { get; private set; }
    public int Backstabs { get; private set; }
    public int Surprises { get; private set; }
    /// <summary>The last smoke cloud thrown down (for the tests).</summary>
    public SmokeCloud LastSmoke { get; private set; }
    /// <summary>Being pulled along a tether to a dagger.</summary>
    public bool Tethering => _tetherTo != null;

    private void TickRogue(float dt)
    {
        _throwCd -= dt;
        // the shadows wear off
        if (_vanishT > 0 && (_vanishT -= dt) <= 0) Unvanish(false);
        for (int k = 0; k < 2; k++)
            if (_thrown[k] != null && !IsInstanceValid(_thrown[k])) _thrown[k] = null;
        // both out and neither still in the air: both come home by themselves
        if (_thrown[0] != null && _thrown[1] != null && _tetherTo == null
            && _thrown[0].State == ThrownDagger.Phase.Stuck && _thrown[1].State == ThrownDagger.Phase.Stuck)
        {
            _thrown[0].ComeBack();
            _thrown[1].ComeBack();
        }
    }

    // ---------------------------------------------------------------- the strike bonuses

    /// <summary>
    /// An attack begins (a jab, a throw, a recall): it brings the Rogue out of the shadows at once,
    /// and says whether it's the first attack out of them (Surprise Attack: the blow it lands
    /// counts four times over, however long it takes to land).
    /// </summary>
    private bool StrikeFromShadows()
    {
        bool surprise = _surpriseReady && Stats.SurpriseAttack && Hidden;
        _surpriseReady = false;
        Reveal(false);
        return surprise;
    }

    /// <summary>
    /// What a Rogue's blow (a jab, a thrown dagger, a recall) is multiplied by against this
    /// creature: a critical strike, a stab from behind (Backstab), and the first strike out of the
    /// shadows (Surprise Attack, decided as the attack began).
    /// </summary>
    private float RogueStrikeMult(Enemy e, Vector2 from, bool surprise, out bool crit, bool sureCrit = false)
    {
        float mult = 1f;
        crit = sureCrit || G.Chance(Stats.CritChance);
        if (crit) { mult *= Tune.Rogue.CritMult + Stats.CritMultBonus; Crits++; }
        if (Stats.Backstab && e.FacingAwayFrom(from)) { mult *= Tune.Rogue.BackstabMult; Backstabs++; }
        if (surprise)
        {
            mult *= Tune.Rogue.SurpriseMult;
            Surprises++;
            G.Fx.Text(e.GlobalPosition + new Vector2(0, -e.HitRadius - 16), "SURPRISE", new Color(1f, 0.85f, 0.35f), 11, 0.8f);
        }
        return mult;
    }

    /// <summary>A critical strike's show: a gold flash and a word over the creature.</summary>
    private static void CritFx(Enemy e, Vector2 at)
    {
        G.Fx.Flash(at, 12, new Color(1f, 0.88f, 0.4f), 0.1f);
        G.Fx.Text(e.GlobalPosition + new Vector2(0, -e.HitRadius - 10), "CRIT", new Color(1f, 0.85f, 0.35f), 10, 0.6f);
    }

    /// <summary>Attacking out of the shadows (or being struck) ends a vanishing.</summary>
    private void Reveal(bool struck)
    {
        if (_vanishT > 0) Unvanish(struck);
    }

    private void Unvanish(bool struck)
    {
        _vanishT = 0;
        _surpriseReady = false;
        G.Fx.Smoke(GlobalPosition, 3, new Color(0.25f, 0.25f, 0.3f, 0.6f), 30f);
        if (struck) G.Fx.Text(GlobalPosition + new Vector2(0, -28), "SEEN", new Color(0.9f, 0.85f, 0.8f), 9, 0.6f);
    }

    // ---------------------------------------------------------------- the throw

    private bool TryThrow(Vector2 aim)
    {
        if (!IsRogue || _throwCd > 0 || DaggersInHand == 0 || _tetherTo != null) return false;
        aim = aim.LengthSquared() > 0.01f ? aim.Normalized() : new Vector2(Facing, 0);
        var target = FindSpellTarget(aim, Tune.Rogue.ThrowRange, Tune.Rogue.ThrowConeDegrees);
        var from = GlobalPosition + new Vector2(0, -6);
        var dir = target != null ? (target.GlobalPosition - from).Normalized() : aim;
        bool both = Stats.TwinThrow && DaggersInHand == 2;
        bool surprise = StrikeFromShadows();
        _throwCd = 0.18f;
        if (Math.Abs(dir.X) > 0.15f) Facing = Math.Sign(dir.X);
        Anim.Face((int)Facing, instant: true);
        Anim.Once("throw", 3, 1.8f);
        AttacksStarted++;
        if (both)
        {
            // a pair, fanned a little apart (the first of them to land is the surprise)
            Launch(0, dir.Rotated(-0.09f), from, surprise);
            Launch(1, dir.Rotated(0.09f), from, surprise);
        }
        // (the off hand throws first: its dagger is the one the throw flings)
        else Launch(_thrown[1] == null ? 1 : 0, dir, from, surprise);
        G.Sfx.Play("throw", from, -4, 0.1f, 1.3f);
        return true;
    }

    private void Launch(int k, Vector2 dir, Vector2 from, bool surprise)
    {
        var d = new ThrownDagger
        {
            Position = from + dir * 6f, Dir = dir, Index = k, Thrower = this,
            Ricochet = Stats.Ricochet, Bounces = Stats.RicochetBounces,
            Damage = Tune.Rogue.ThrowDamage * Stats.DamageMult * Stats.ThrowDamageMult, FromShadows = surprise,
        };
        _thrown[k] = d;
        G.Spawn(d);
        NetSync.HeroVisual(d);
    }

    /// <summary>A dagger of this hero's met a creature: the blow, a nudge, and whatever bonuses it earns.</summary>
    public void DaggerStruck(ThrownDagger d, Enemy e, Vector2 at)
    {
        if (!IsInstanceValid(e) || e.Dead) return;
        bool surprise = d.FromShadows;
        // (one surprise per attack: a pair thrown together share it)
        foreach (var t in _thrown) if (t != null && IsInstanceValid(t)) t.FromShadows = false;
        d.FromShadows = false;
        float mult = RogueStrikeMult(e, GlobalPosition, surprise, out bool crit);
        float dealt = e.Hurt(d.Damage * mult, d.Dir * Tune.Rogue.ThrowNudge, at);
        if (dealt <= 0) return;
        OnDealtDamage(dealt);
        if (crit) CritFx(e, at);
        if (!e.Dead) e.Freeze(Tune.Rogue.HitStop * 2f);
        G.Sfx.Play("hit", at, -4, 0.1f, 1.3f);
    }

    /// <summary>A dagger comes back to the hand.</summary>
    public void CatchDagger(ThrownDagger d)
    {
        for (int k = 0; k < 2; k++) if (_thrown[k] == d) _thrown[k] = null;
        if (_tetherTo == d) _tetherTo = null;
        G.Sfx.Play("clink", GlobalPosition, -12, 0.1f, 1.7f);
        G.Fx.Glint(GlobalPosition + new Vector2(Facing * 6, -6), new Color(1f, 0.95f, 0.85f), 5);
    }

    // ---------------------------------------------------------------- recall

    private bool TryRecall()
    {
        if (!IsRogue || _tetherTo != null) return false;
        var out_ = _thrown.Where(d => d != null && IsInstanceValid(d) && d.State != ThrownDagger.Phase.Returning).ToList();
        if (out_.Count == 0) { SayNo("BOTH DAGGERS IN HAND"); return true; }
        Anim.Once("throw", 3, 2.4f);
        NetSync.HeroRecall(this);
        bool surprise = StrikeFromShadows();
        // Tether: you go to the dagger instead (the first one stuck in a creature)
        if (Stats.Tether && out_.FirstOrDefault(d => d.State == ThrownDagger.Phase.Stuck) is ThrownDagger anchor)
        {
            _tetherTo = anchor;
            _tetherT = 0;
            _tetherSurprise = surprise;
            foreach (var d in out_) if (d != anchor) d.ComeBack();
            G.Fx.Beam(GlobalPosition + new Vector2(0, -6), anchor.GlobalPosition, new Color(0.85f, 0.9f, 1f, 0.8f));
            G.Sfx.Play("dodge", GlobalPosition, -4, 0.05f, 1.4f);
            return true;
        }
        foreach (var d in out_)
        {
            // a dagger in a creature tears back out through it, yanking it toward you
            if (d.State == ThrownDagger.Phase.Stuck && d.StuckIn is Enemy e && IsInstanceValid(e) && !e.Dead)
            {
                RecallStrike(e, d.GlobalPosition, surprise);
                surprise = false;
            }
            d.ComeBack();
        }
        return true;
    }

    private void RecallStrike(Enemy e, Vector2 at, bool surprise, bool sureCrit = false)
    {
        var toMe = (GlobalPosition - e.GlobalPosition).Normalized();
        float mult = RogueStrikeMult(e, GlobalPosition, surprise, out bool crit, sureCrit);
        float dealt = e.Hurt(Tune.Rogue.RecallDamage * Stats.DamageMult * Stats.RecallDamageMult * mult, toMe * Tune.Rogue.RecallYank, at);
        if (dealt > 0) OnDealtDamage(dealt);
        if (crit) CritFx(e, at);
        G.Fx.Spark(at, toMe, true, new Color(1f, 0.9f, 0.75f));
        G.Fx.Burst(at, e.BloodTint, 8, 150, 2f, 0.35f, 200);
        G.Sfx.Play("hit", at, -2, 0.1f, 0.9f);
    }

    /// <summary>Tether: flying along the line to the dagger; arriving strikes its creature and takes the dagger back.</summary>
    private Vector2 TetherMotion(Vector2 v, float dt)
    {
        var d = _tetherTo;
        _tetherT += dt;
        if (d == null || !IsInstanceValid(d) || d.State == ThrownDagger.Phase.Returning || _tetherT > 1.2f) { _tetherTo = null; return v * 0.3f; }
        var to = d.GlobalPosition - (GlobalPosition + new Vector2(0, -4));
        if (to.Length() < 16f)
        {
            // arrived: the dagger comes out of the creature into your hand as you land on it
            if (d.StuckIn is Enemy e && IsInstanceValid(e) && !e.Dead) RecallStrike(e, d.GlobalPosition, _tetherSurprise, sureCrit: Stats.Pounce);
            _tetherTo = null;
            _tetherSurprise = false;
            d.ComeBack();
            return new Vector2(-Math.Sign(to.X == 0 ? Facing : to.X) * 60f, -120f);
        }
        if (Math.Abs(to.X) > 2) Facing = Math.Sign(to.X);
        if (Engine.GetPhysicsFrames() % 3 == 0) Afterimage.Spawn(Anim, new Color(0.8f, 0.85f, 1f), 0.18f);
        return to.Normalized() * Tune.Rogue.TetherSpeed;
    }

    // ---------------------------------------------------------------- vanish

    private bool TryVanish()
    {
        if (!IsRogue || !AbilityChargeReady) return false;
        SpendAbilityCharge();
        _surpriseReady = Stats.SurpriseAttack;
        var foot = GlobalPosition + new Vector2(0, 8);
        if (Stats.SmokeBomb)
        {
            // Smoke Bomb: a cloud for everyone in it, instead of vanishing alone
            var cloud = new SmokeCloud
            {
                Position = foot - new Vector2(0, 14),
                Radius = Tune.Rogue.SmokeRadius * Stats.SmokeWideMult,
                Life = Tune.Rogue.SmokeSeconds + Stats.VanishBonus,
            };
            G.Spawn(cloud);
            NetSync.HeroVisual(cloud);
            LastSmoke = cloud;
            G.Fx.Burst(foot, new Color(0.4f, 0.4f, 0.44f, 0.8f), 16, 160, 3f, 0.6f, -20);
            G.Sfx.Play("rock", foot, -4, 0.1f, 0.6f);
        }
        else
        {
            _vanishT = VanishTotal;
            G.Fx.Smoke(GlobalPosition, 6, new Color(0.2f, 0.2f, 0.25f, 0.7f), 50f);
            G.Sfx.Play("dodge", GlobalPosition, -6, 0.05f, 0.5f);
        }
        Anim.Once("dodge", 2, 1.4f);
        G.Main.Rumble(0.15f, 0.05f, 0.1f);
        return true;
    }
}
