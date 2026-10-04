using System;
using System.Collections.Generic;
using Godot;

namespace DaggerCave;

/// <summary>
/// The Shape Shifter. Unshifted it is a poor fighter with a staff. The ability button (Shift) copies
/// the nearest creature in range: the body becomes that creature (its own model, outlined in white),
/// moving as it moves and fighting with its attacks, each its own: an ordinary attack on the attack
/// button and a special on the second ability button. Health and damage stay the Shape Shifter's own.
/// Shift again to drop the form at any time (the ability then recharges).
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

    private float _formAtkCd, _formSpecCd, _formAtkT = -1f, _shiftLock;
    private Vector2 _formAtkAim;
    private bool _formAtkDone;
    private float _formDashT, _formDashDmg;
    private Vector2 _formDashDir;
    private readonly HashSet<Enemy> _formDashHit = new();
    private bool _formLeapLand;
    private int _frenzyLeft;
    private float _frenzyT;
    private float _formSpecT = -1f;

    /// <summary>For the model: the clip of the form's attack under way (null when none) and how far along it is.</summary>
    public string FormClip { get; private set; }
    public float FormClipT { get; private set; }

    public float FormSpecialFrac => Form == null ? 0f : Math.Clamp(_formSpecCd / Math.Max(0.01f, Form.SpecCd), 0f, 1f);
    public bool FormSpecialReady => Form != null && _formSpecCd <= 0;
    public float FormAttackFrac => Form == null ? 0f : Math.Clamp(_formAtkCd / Math.Max(0.01f, Form.Cooldown), 0f, 1f);

    /// <summary>Test aids: press Shift, or the special, once.</summary>
    public bool TestShift() => TryShift();
    public bool TestSpecial(Vector2 aim) => TrySpecial(aim);
    public bool TestAttack() { _formAtkCd = 0; _formAtkT = -1f; return FormPrimary(new Vector2(Facing, 0), false); }

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
        _formAtkCd = 0.25f; _formSpecCd = Math.Min(_formSpecCd, 1.5f); _formAtkT = -1f; _shiftLock = 0.3f;
        _swingT = -1f;
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
        ApplyFormLook();
        var at = GlobalPosition + new Vector2(0, -8);
        G.Fx.Burst(at, new Color(0.9f, 0.93f, 1f, 0.8f), 14, 150, 2f, 0.4f);
        G.Fx.Ring(at, 18, new Color(0.9f, 0.93f, 1f, 0.7f), 0.3f);
        G.Sfx.Play("bubble", GlobalPosition, -5, 0.05f, 1.4f);
        _shiftLock = 0.3f;
        if (cooldown && !IsRemote && !Stats.FluidShift) SpendAbilityCharge();
    }

    /// <summary>Shows the form's model (or the hero's own again): the creature's size, in white ink.</summary>
    private void ApplyFormLook()
    {
        if (Anim == null) return;
        Anim.SetForm(Form?.Set, Form?.Size ?? 1f);
    }

    // ---------------------------------------------------------------- each frame

    private void TickShifter(float dt)
    {
        TickShifterCore(dt);
        if (Anim != null) { Anim.FormClip = FormClip; Anim.FormClipT = FormClipT; }
    }

    private void TickShifterCore(float dt)
    {
        if (_formAtkCd > 0) _formAtkCd -= dt;
        if (_formSpecCd > 0) _formSpecCd -= dt;
        if (_shiftLock > 0) _shiftLock -= dt;
        if (Form == null) { FormClip = null; return; }
        var f = Form;
        // the ordinary attack: wind up, strike once, recover
        FormClip = null;
        if (_formAtkT >= 0)
        {
            _formAtkT += dt;
            if (_formAtkT < f.Wind) { FormClip = f.WindClip; FormClipT = _formAtkT / f.Wind; }
            else
            {
                if (!_formAtkDone) { _formAtkDone = true; FormStrike(); }
                float k = (_formAtkT - f.Wind) / Math.Max(0.05f, f.Strike);
                if (k < 1f) { FormClip = f.StrikeClip; FormClipT = k; }
                else _formAtkT = -1f;
            }
        }
        // a special in progress (a charge or a leap), or its repeated blows
        if (_formDashT > 0)
        {
            _formDashT -= dt;
            Velocity = new Vector2(_formDashDir.X * Tune.Shifter.ChargeSpeed, Velocity.Y);
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
        }
        if (_formLeapLand && IsOnFloor() && Velocity.Y >= 0 && _formSpecT > 0.1f)
        {
            _formLeapLand = false;
            Burst(Form.SpecRange, Form.SpecDmg, 220f, stun: 0.4f);
            G.Fx.Shockwave(GlobalPosition + new Vector2(0, 12), 30, new Color(1, 1, 1, 0.4f), 0.3f);
        }
        if (_formSpecT >= 0) { _formSpecT += dt; if (_formSpecT > 1.6f) _formSpecT = -1f; }
    }

    /// <summary>Damage the form deals, from a base the Shape Shifter's own strength sets.</summary>
    private float FormDamage(float mult) => Tune.Shifter.FormDamage * mult * Stats.DamageMult * Stats.FormDmgMult * G.Range(0.92f, 1.08f);

    private void Blow(Enemy e, float mult, Vector2 knock)
    {
        float dealt = e.Hurt(FormDamage(mult), knock, e.GlobalPosition);
        if (dealt <= 0) { G.Sfx.Play("clink", GlobalPosition, -6); return; }
        OnDealtDamage(dealt);
        if (Stats.FormBleed && !e.Dead) e.Bleed(dealt * 0.5f, 3f);
        e.Freeze(Tune.Feel.HitStopNormal);
        G.Fx.Spark(e.GlobalPosition, knock.Normalized(), mult > 1.5f, new Color(0.95f, 0.97f, 1f));
        G.Main.Kick(knock.Normalized() * Tune.Feel.KickNormal);
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

    // ---------------------------------------------------------------- the ordinary attack

    private bool FormPrimary(Vector2 aim, bool held)
    {
        if (Form == null || _formAtkCd > 0 || _formAtkT >= 0 || _formDashT > 0) return false;
        _formAtkAim = aim.LengthSquared() > 0.01f ? aim.Normalized() : new Vector2(Facing, 0);
        if (Math.Abs(_formAtkAim.X) > 0.15f) Facing = Math.Sign(_formAtkAim.X);
        Anim.Face((int)Facing, instant: true);
        _formAtkT = 0f; _formAtkDone = false;
        _formAtkCd = Form.Cooldown / Stats.AttackSpeed;
        AttacksStarted++;
        G.Sfx.Play(Form.Dmg > 1.4f ? "swing_heavy" : "swing", GlobalPosition, -3, 0.12f, 1.2f - Form.Size * 0.1f);
        return true;
    }

    private void FormStrike()
    {
        var f = Form;
        var dir = new Vector2(Facing, _formAtkAim.Y * 0.6f).Normalized();
        var origin = GlobalPosition + new Vector2(0, -4);
        bool any = false;
        foreach (var e in G.Enemies.ToArray())
        {
            if (!GodotObject.IsInstanceValid(e) || e.Dead) continue;
            var to = e.GlobalPosition - origin;
            float dist = to.Length();
            if (dist > f.Reach + e.HitRadius) continue;
            if (to.Normalized().Dot(dir) < -0.1f && dist > e.HitRadius + 6f) continue;
            Blow(e, f.Dmg, dir * f.Knock);
            any = true;
        }
        foreach (var br in Breakables.All.ToArray())
            if (br.HitSize > 0 && br.HitCenter.DistanceTo(origin + dir * f.Reach * 0.5f) < f.Reach * 0.6f + br.HitSize) br.Strike(origin);
        G.Fx.Directional(origin + dir * f.Reach * 0.6f, dir, 0.6f, new Color(0.95f, 0.97f, 1f, 0.8f), 5, 160, 1.6f, 0.2f, 0, 1);
        if (any) G.Main.Rumble(0.3f, 0.3f, 0.08f);
    }

    // ---------------------------------------------------------------- the special

    private bool TrySpecial(Vector2 aim)
    {
        if (!IsShifter) return false;
        if (Form == null) { SayNo("SHIFT FIRST"); return true; }
        if (_formSpecCd > 0 || _formDashT > 0) return false;
        var f = Form;
        _formSpecCd = f.SpecCd * Stats.FormSpecCdMult;
        float spec = Stats.FormSpecDmgMult;
        if (Stats.SpecEcho && G.Chance(0.35f)) { _formSpecCd = 0; G.Fx.Text(GlobalPosition + new Vector2(0, -46), "ECHO", new Color(0.85f, 0.9f, 1f), 9, 0.6f); }
        _formSpecT = 0f;
        var dir = new Vector2(aim.X != 0 ? Math.Sign(aim.X) : Facing, 0);
        if (dir.X != 0) { Facing = (int)dir.X; Anim.Face((int)Facing, instant: true); }
        G.Fx.Text(GlobalPosition + new Vector2(0, -34), f.SpecialName.ToUpperInvariant(), new Color(0.95f, 0.96f, 1f), 10, 0.8f);
        switch (f.Special)
        {
            case FormSpecial.Slam:
            case FormSpecial.Quake:
                Burst(f.SpecRange, f.SpecDmg * spec, 260f, stun: f.Special == FormSpecial.Quake ? 0.8f : 0.4f);
                G.Fx.Shockwave(GlobalPosition + new Vector2(0, 12), f.SpecRange * 0.5f, new Color(1, 1, 1, 0.5f), 0.35f);
                G.Fx.AddShake(f.Special == FormSpecial.Quake ? 3f : 1.5f);
                G.Sfx.Play("rock", GlobalPosition, -1, 0.1f, 0.8f);
                _formAtkCd = Math.Max(_formAtkCd, 0.4f);
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
                Velocity = new Vector2(dir.X * 300f, -BaseJumpV * 1.35f);
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
