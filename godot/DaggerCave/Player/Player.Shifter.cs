using System;
using System.Collections.Generic;
using Godot;

namespace DaggerCave;

/// <summary>
/// The Shape Shifter. Unshifted it is a poor fighter with a staff. The ability button (Shift) copies
/// the nearest creature in range, and then the Shape Shifter IS that creature: a real one of its kind
/// (see <see cref="Ghost"/>) runs its own behaviour, with the controller in place of its hunger. The
/// stick sets where it heads (as a point out in that direction, where it would have sought a hero),
/// the attack button sets its intent to attack, and it goes through all of that creature's wind-up,
/// strike and recovery, with its own animations, speed, jump and blow. Its blows land on the
/// creatures of the cave. Health stays the Shape Shifter's own, and strength (upgrades) applies to the
/// damage the creature's blow deals. The second button is the creature's own second move where it has
/// one (the bear's charge); otherwise a trick of the Shape Shifter's own. Shift again to drop the form
/// (the ability then recharges).
/// </summary>
public partial class Player
{
    public bool IsShifter => Stats.Hero == HeroKind.ShapeShifter;
    /// <summary>The creature this hero is now (null: itself). Online copies show their game's.</summary>
    public ShiftForm Form { get; private set; }
    public bool Shifted => Form != null;
    public float FormMove => Form?.Move ?? 1f;
    public float FormJump => Form?.Jump ?? 1f;
    public bool FormFlier => Form?.Flier ?? false;
    public float FormArmor => Form?.Armor ?? 0f;

    /// <summary>
    /// The creature that is the body of this hero while shifted: a real creature of that kind (not one of the
    /// cave's), driven by the controller. This hero's own body follows it, and its model is the one you see.
    /// Null when not shifted (and on online copies, which show the form by what their game sends).
    /// </summary>
    public Enemy Ghost { get; private set; }

    private float _formAtkCd, _formSpecCd, _formAtkT = -1f, _shiftLock;
    private float _formDashT, _formDashDmg;
    private Vector2 _formDashDir;
    private readonly HashSet<Enemy> _formDashHit = new();
    private bool _formLeapLand;
    private int _frenzyLeft;
    private float _frenzyT;
    private float _formSpecT = -1f;

    /// <summary>For the model of an online copy: the clip of the form's attack under way (null when none) and how far along it is.</summary>
    public string FormClip { get; private set; }
    public float FormClipT { get; private set; }

    private bool NativeSpecial => Ghost != null && GodotObject.IsInstanceValid(Ghost) && Ghost.MasterSpecialIntentIndex >= 0;
    public float FormSpecialFrac => Form == null ? 0f : NativeSpecial ? Ghost.MasterSpecialFrac : Math.Clamp(_formSpecCd / Math.Max(0.01f, Form.SpecCd), 0f, 1f);
    public bool FormSpecialReady => Form != null && (NativeSpecial ? Ghost.MasterSpecialReady : _formSpecCd <= 0);
    public float FormAttackFrac => 0f;

    /// <summary>Test aids: press Shift, or the special, once.</summary>
    public bool TestShift() => TryShift();
    public bool TestSpecial(Vector2 aim) => TrySpecial(aim);
    public bool TestAttack() { Ghost?.MasterAttack(new Vector2(Facing, 0)); return Ghost != null; }

    // ---------------------------------------------------------------- shifting

    /// <summary>The creature a Shift would copy now: the nearest one in range that can be.</summary>
    public Enemy ShiftTarget()
    {
        Enemy best = null; float bd = Tune.Shifter.CopyRange * Stats.ShiftRangeMult;
        foreach (var e in G.Enemies)
        {
            if (!GodotObject.IsInstanceValid(e) || e.Dead) continue;
            if (ShiftForm.For(e) == null) continue;
            float d = e.GlobalPosition.DistanceTo(GlobalPosition);
            if (d < bd) { bd = d; best = e; }
        }
        return best;
    }

    /// <summary>The ability button: become the nearest creature, or drop the form you have.</summary>
    private bool TryShift()
    {
        if (!IsShifter || _shiftLock > 0) return false;
        if (Form != null) { LeaveForm(cooldown: true); return true; }
        if (!AbilityChargeReady) return false;
        var e = ShiftTarget();
        if (e == null)
        {
            // (anything near that can't be copied says so)
            bool near = false;
            foreach (var o in G.Enemies) if (GodotObject.IsInstanceValid(o) && !o.Dead && o.GlobalPosition.DistanceTo(GlobalPosition) < Tune.Shifter.CopyRange * Stats.ShiftRangeMult) near = true;
            SayNo(near ? "CAN'T COPY THAT" : "NOTHING TO COPY");
            _shiftLock = 0.4f;
            return true;
        }
        EnterForm(ShiftForm.For(e));
        return true;
    }

    public void EnterForm(ShiftForm f)
    {
        if (f == null) return;
        Form = f;
        _formAtkCd = 0f; _formSpecCd = Math.Min(_formSpecCd, 1.5f); _formAtkT = -1f; _shiftLock = 0.3f;
        _swingT = -1f;
        // (this game's hero becomes a creature of its kind; a copy of a friend's hero just shows the model)
        if (!IsRemote) SpawnGhost(f);
        ApplyFormLook();
        var at = GlobalPosition + new Vector2(0, -8);
        G.Fx.Burst(at, new Color(0.9f, 0.93f, 1f, 0.9f), 22, 190, 2.4f, 0.5f);
        G.Fx.Ring(at, 22, new Color(0.9f, 0.93f, 1f, 0.9f), 0.4f);
        G.Sfx.Play("bubble", GlobalPosition, -2, 0.05f, 0.7f);
        G.Fx.Text(GlobalPosition + new Vector2(0, -34), f.Name.ToUpperInvariant(), new Color(0.92f, 0.95f, 1f), 11, 0.9f);
        Anim?.Flash(0.8f);
        if (Stats.ShiftHeals && !IsRemote) Heal(Stats.MaxHp * 0.1f);
    }

    public void LeaveForm(bool cooldown)
    {
        if (Form == null) return;
        Form = null;
        _formAtkT = -1f; _formDashT = 0; _frenzyLeft = 0; _formSpecT = -1f; _formLeapLand = false;
        FormClip = null;
        DropGhost();
        ApplyFormLook();
        var at = GlobalPosition + new Vector2(0, -8);
        G.Fx.Burst(at, new Color(0.9f, 0.93f, 1f, 0.8f), 14, 150, 2f, 0.4f);
        G.Fx.Ring(at, 18, new Color(0.9f, 0.93f, 1f, 0.7f), 0.3f);
        G.Sfx.Play("bubble", GlobalPosition, -5, 0.05f, 1.4f);
        _shiftLock = 0.3f;
        if (cooldown && !IsRemote && !Stats.FluidShift) SpendAbilityCharge();
    }

    /// <summary>The creature a form is, made fresh (as it would stand in the cave).</summary>
    private static Enemy MakeCreature(ShiftForm f) => f.Key switch
    {
        "goblin" => new Goblin(),
        "skeleton" => new Skeleton(),
        "rat" => new Rat(),
        "bat" => new Bat(),
        // (a spider walks the ground, rather than hanging from the ceiling)
        "spider" => new Spider { Grounded = true },
        "frog" => new Frog(),
        "scorpion" => new Scorpion(),
        "bear" => new Bear(),
        "golem" => new Golem(),
        "hornet" => new Hornet(),
        "sporeling" => new Sporeling(),
        "crab" => new Crab(),
        "fish" => new Fish(),
        _ => null,
    };

    private void SpawnGhost(ShiftForm f)
    {
        DropGhost();
        var g = MakeCreature(f);
        if (g == null) return;
        g.Master = this;
        g.Position = GlobalPosition;
        // (it takes its turn before the hero does each frame, so the hero follows where it is now)
        g.ProcessPriority = -5;
        G.World.AddChild(g);
        g.Wake();
        g.FaceToward(Facing);
        Ghost = g;
    }

    /// <summary>The creature goes (the form dropped, or the hero fell): the hero stands where it stood.</summary>
    private void DropGhost()
    {
        var g = Ghost;
        Ghost = null;
        if (g == null || !GodotObject.IsInstanceValid(g)) return;
        GlobalPosition = g.GlobalPosition;
        Velocity = Vector2.Zero;
        // (its model goes with it, at once: the hero's own is shown again)
        g.Visible = false;
        g.QueueFree();
    }

    /// <summary>Shows the form: the hero's own model gives way to the creature (this game: the driven creature's; a copy: the model on its own animator).</summary>
    private void ApplyFormLook()
    {
        if (Anim == null) return;
        if (IsRemote) { Anim.SetForm(Form?.Set, Form?.Size ?? 1f); return; }
        Anim.SetForm(null);
        Anim.Visible = Form == null;
    }

    // ---------------------------------------------------------------- the creature's body

    /// <summary>
    /// Each frame while shifted: the controller's push, jump and attack go to the creature as intents, and the
    /// hero's own body is wherever the creature is (its speed is the creature's).
    /// </summary>
    private Vector2 PossessedStep(PlayerInput inp)
    {
        var g = Ghost;
        g.MasterAim = inp.Move;
        if (inp.Jump) g.MasterJump();
        if (Math.Abs(inp.Move.X) > 0.2f) Facing = Math.Sign(inp.Move.X);
        return g.Velocity * Tune.Difficulty.EnemyMoveScale;
    }

    private float _suffT;
    /// <summary>A fish out of water: drowning in the air. <paramref name="share"/> of the hero's health a second (taken every half second).</summary>
    public void Suffocate(float share, float dt)
    {
        if (Dead || (_suffT -= dt) > 0) return;
        _suffT = 0.5f;
        TakeRawDamage(Stats.MaxHp * share * 0.5f, "drown");
    }

    /// <summary>Whether this hero is a creature this very frame (the creature stands in the world).</summary>
    private bool Possessed => Ghost != null && GodotObject.IsInstanceValid(Ghost);

    // ---------------------------------------------------------------- each frame

    private void TickShifter(float dt)
    {
        TickShifterCore(dt);
        if (Anim != null) { Anim.FormClip = FormClip; Anim.FormClipT = FormClipT; }
    }

    private void TickShifterCore(float dt)
    {
        if (_formSpecCd > 0) _formSpecCd -= dt;
        if (_shiftLock > 0) _shiftLock -= dt;
        FormClip = null;
        if (Form == null) return;
        var f = Form;
        // (what the other games see of the creature's attack: its wind-up, and its blow)
        if (Possessed)
        {
            if (Ghost.Attacking) { FormClip = f.WindClip; FormClipT = 0.5f; }
            else if (Ghost.Animator != null && Ghost.Animator.OnceActive && Ghost.Animator.OnceName == f.StrikeClip) { FormClip = f.StrikeClip; FormClipT = 0.5f; }
        }
        // a special in progress (a charge or a leap), or its repeated blows
        if (_formDashT > 0)
        {
            _formDashT -= dt;
            foreach (var e in G.Enemies.ToArray())
            {
                if (!GodotObject.IsInstanceValid(e) || e.Dead || _formDashHit.Contains(e)) continue;
                if (e.GlobalPosition.DistanceTo(GlobalPosition) > e.HitRadius + 16f) continue;
                _formDashHit.Add(e);
                Blow(e, _formDashDmg, _formDashDir * 320f);
            }
            FormClip = f.StrikeClip; FormClipT = 0.5f;
        }
        if (_frenzyLeft > 0 && (_frenzyT -= dt) <= 0)
        {
            _frenzyT = 0.1f; _frenzyLeft--;
            Burst(Form.SpecRange, Form.SpecDmg, 90f, stun: 0f);
            FormClip = f.StrikeClip; FormClipT = (_frenzyLeft % 2) * 0.4f + 0.2f;
            if (Possessed && _frenzyLeft % 2 == 0) Ghost.Animator?.Once(f.StrikeClip, 3);
        }
        if (_formLeapLand && Possessed && Ghost.OnGround && Ghost.Velocity.Y >= 0 && _formSpecT > 0.1f)
        {
            _formLeapLand = false;
            Burst(Form.SpecRange, Form.SpecDmg, 220f, stun: 0.4f);
            G.Fx.Shockwave(GlobalPosition + new Vector2(0, 12), 30, new Color(1, 1, 1, 0.4f), 0.3f);
        }
        if (_formSpecT >= 0) { _formSpecT += dt; if (_formSpecT > 1.6f) _formSpecT = -1f; }
    }

    /// <summary>Damage the form's tricks deal, from a base the Shape Shifter's own strength sets.</summary>
    private float FormDamage(float mult) => Tune.Shifter.FormDamage * mult * Stats.DamageMult * Stats.FormDmgMult * G.Range(0.92f, 1.08f);

    /// <summary>A blow of the Shape Shifter's own trick (a special).</summary>
    private void Blow(Enemy e, float mult, Vector2 knock) => Strike(e, FormDamage(mult), knock, mult > 1.5f);

    /// <summary>
    /// A blow of the creature's own: the damage that kind of creature's blow deals (<paramref name="baseDamage"/>, as in its
    /// tuning), through the Shape Shifter's own strength. Called by the creature, when its attack lands.
    /// </summary>
    public void FormBlow(Enemy e, float baseDamage, Vector2 knock) =>
        Strike(e, baseDamage * Stats.DamageMult * Stats.FormDmgMult * G.Range(0.92f, 1.08f), knock, baseDamage > Tune.Shifter.FormDamage * 1.5f);

    private void Strike(Enemy e, float dmg, Vector2 knock, bool heavy)
    {
        float dealt = e.Hurt(dmg, knock, e.GlobalPosition);
        if (dealt <= 0) { G.Sfx.Play("clink", GlobalPosition, -6); return; }
        OnDealtDamage(dealt);
        if (Stats.FormBleed && !e.Dead) e.Bleed(dealt * 0.5f, 3f);
        e.HitStop(Tune.Feel.HitStopNormal);
        // (the creature that struck holds still for the same beat)
        if (Possessed) Ghost.Freeze(e.Dead ? Tune.Feel.HitStopNormal * Tune.Feel.HitStopKillMult : Tune.Feel.HitStopNormal, hold: true);
        G.Fx.Spark(e.GlobalPosition, knock.LengthSquared() > 1 ? knock.Normalized() : new Vector2(Facing, 0), heavy, new Color(0.95f, 0.97f, 1f));
        G.Main.Kick((knock.LengthSquared() > 1 ? knock.Normalized() : new Vector2(Facing, 0)) * Tune.Feel.KickNormal);
    }

    /// <summary>A burst round the hero: every creature within range takes the damage, knocked outward (and stunned, if asked).</summary>
    private void Burst(float range, float mult, float knock, float stun)
    {
        var at = GlobalPosition;
        G.Fx.Ring(at + new Vector2(0, -6), range, new Color(0.92f, 0.95f, 1f, 0.8f), 0.25f);
        foreach (var e in G.Enemies.ToArray())
        {
            if (!GodotObject.IsInstanceValid(e) || e.Dead) continue;
            var d = e.GlobalPosition - at;
            if (d.Length() > range + e.HitRadius) continue;
            Blow(e, mult, (d.LengthSquared() < 1 ? new Vector2(Facing, 0) : d.Normalized()) * knock);
            if (stun > 0 && !e.Dead) e.Interrupt(d.LengthSquared() < 1 ? Vector2.Zero : d.Normalized() * 60f, stun, "STUNNED");
        }
        foreach (var br in Breakables.All.ToArray())
            if (br.HitSize > 0 && br.HitCenter.DistanceTo(at) < range + br.HitSize) br.Strike(at);
    }

    // ---------------------------------------------------------------- the attack button

    /// <summary>
    /// The attack button while shifted: it is the creature's intent to attack, nothing more. The creature
    /// attacks the moment it can (its own cooldown), through its own wind-up, strike and recovery.
    /// </summary>
    private bool FormPrimary(Vector2 aim, bool held)
    {
        if (!Possessed) return false;
        Ghost.MasterAttack(aim);
        return true;
    }

    // ---------------------------------------------------------------- the second button

    private bool TrySpecial(Vector2 aim)
    {
        if (!IsShifter) return false;
        if (Form == null) { SayNo("SHIFT FIRST"); return true; }
        // a creature with a second move of its own does that one, through its own wind-up
        if (NativeSpecial)
        {
            if (!Ghost.MasterSpecialReady) return false;
            Ghost.MasterSpecial(aim);
            return true;
        }
        if (_formSpecCd > 0 || _formDashT > 0) return false;
        var f = Form;
        _formSpecCd = f.SpecCd * Stats.FormSpecCdMult;
        float spec = Stats.FormSpecDmgMult;
        if (Stats.SpecEcho && G.Chance(0.35f)) { _formSpecCd = 0; G.Fx.Text(GlobalPosition + new Vector2(0, -46), "ECHO", new Color(0.85f, 0.9f, 1f), 9, 0.6f); }
        _formSpecT = 0f;
        var dir = new Vector2(aim.X != 0 ? Math.Sign(aim.X) : Facing, 0);
        if (dir.X != 0) { Facing = (int)dir.X; Ghost?.FaceToward(dir.X); }
        // (the creature's own blow animation, for a trick that has none of its own)
        Ghost?.Animator?.Once(f.StrikeClip, 3);
        G.Fx.Text(GlobalPosition + new Vector2(0, -34), f.SpecialName.ToUpperInvariant(), new Color(0.95f, 0.96f, 1f), 10, 0.8f);
        switch (f.Special)
        {
            case FormSpecial.Slam:
            case FormSpecial.Splash:
            case FormSpecial.Quake:
                Burst(f.SpecRange, f.SpecDmg * spec, 260f, stun: f.Special == FormSpecial.Quake ? 0.8f : 0.4f);
                G.Fx.Shockwave(GlobalPosition + new Vector2(0, 12), f.SpecRange * 0.5f, new Color(1, 1, 1, 0.5f), 0.35f);
                G.Fx.AddShake(f.Special == FormSpecial.Quake ? 3f : 1.5f);
                G.Sfx.Play("rock", GlobalPosition, -1, 0.1f, 0.8f);
                break;
            case FormSpecial.Spin:
                Burst(f.SpecRange, f.SpecDmg * spec, 200f, stun: 0.2f);
                _iframes = Math.Max(_iframes, 0.35f);
                G.Sfx.Play("swing_heavy", GlobalPosition, -2, 0.1f, 1.1f);
                break;
            case FormSpecial.Frenzy:
                _frenzyLeft = 6; _frenzyT = 0f;
                break;
            case FormSpecial.Screech:
                Burst(f.SpecRange, f.SpecDmg * spec, 60f, stun: 0.1f);
                foreach (var e in G.Enemies.ToArray())
                    if (GodotObject.IsInstanceValid(e) && !e.Dead && e.GlobalPosition.DistanceTo(GlobalPosition) < f.SpecRange + e.HitRadius) e.Weaken(0.6f, 5f);
                G.Sfx.Play("roar", GlobalPosition, -3, 0.1f, 1.6f);
                break;
            case FormSpecial.Spit:
                G.Spawn(new SwordWave { Position = CastPoint, Dir = dir, Damage = FormDamage(f.SpecDmg * spec), Range = f.SpecRange, Speed = 420f });
                G.Sfx.Play("swing", GlobalPosition, -2, 0.1f, 1.5f);
                break;
            case FormSpecial.Leap:
                // (flung up and forward by the creature's own body, then it lands on the creatures below)
                Ghost?.MasterOverride(new Vector2(dir.X * 300f, -BaseJumpV * 1.35f), 1.2f, gravity: true);
                _formLeapLand = true;
                G.Sfx.Play("jump", GlobalPosition, -3, 0.1f, 0.7f);
                break;
            case FormSpecial.Lash:
                Burst(f.SpecRange, f.SpecDmg * spec, 180f, stun: 0.3f);
                foreach (var e in G.Enemies.ToArray())
                    if (GodotObject.IsInstanceValid(e) && !e.Dead && e.GlobalPosition.DistanceTo(GlobalPosition) < f.SpecRange + e.HitRadius) e.Weaken(0.75f, 4f);
                break;
            case FormSpecial.Charge:
            case FormSpecial.Dive:
                _formDashT = f.Special == FormSpecial.Charge ? f.SpecRange / Tune.Shifter.ChargeSpeed : f.SpecRange / Tune.Shifter.ChargeSpeed * 0.8f;
                _formDashDir = dir; _formDashDmg = f.SpecDmg * spec; _formDashHit.Clear();
                _iframes = Math.Max(_iframes, _formDashT);
                Ghost?.MasterOverride(new Vector2(dir.X * Tune.Shifter.ChargeSpeed / Tune.Difficulty.EnemyMoveScale, 0f), _formDashT, gravity: !f.Flier);
                G.Sfx.Play("swing_heavy", GlobalPosition, -1, 0.1f, 0.7f);
                break;
            case FormSpecial.Spores:
                Burst(f.SpecRange, f.SpecDmg * spec, 40f, stun: 0f);
                foreach (var e in G.Enemies.ToArray())
                    if (GodotObject.IsInstanceValid(e) && !e.Dead && e.GlobalPosition.DistanceTo(GlobalPosition) < f.SpecRange + e.HitRadius) e.Weaken(0.5f, 6f);
                G.Fx.Burst(GlobalPosition, new Color(0.7f, 1f, 0.6f, 0.7f), 26, 90, 2.2f, 0.9f);
                break;
            case FormSpecial.Grip:
                Burst(f.SpecRange, f.SpecDmg * spec, 80f, stun: 1.0f);
                break;
        }
        return true;
    }
}
