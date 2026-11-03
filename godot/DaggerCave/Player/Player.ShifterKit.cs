using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

/// <summary>
/// The Shape Shifter's kit, laid out as the others' are (<see cref="Elements.SimpleKit"/>; <see cref="Tune.Shifter"/>):
/// <list type="bullet">
/// <item><b>Elements are the kinds of creature</b>: nature the walkers, wind the fliers, water the swimmers. An element's button takes her into
/// a creature of that kind: the nearest she could copy, or else the last of that kind she was. Pressed again while in that kind, it goes on
/// to the next of that kind she has been this run, and after the last, back to herself. Every creature she copies is hers to take again for
/// the rest of the run.</item>
/// <item><b>Attack</b>: her staff (in the element), or in a form the creature's own attack.</item>
/// <item><b>Molt</b> (the dodge button): she sheds her skin and slips away: a quick dash, untouchable a moment, and for a moment more the
/// creatures lose her.</item>
/// <item><b>Apex Form</b> (the special): the creature she is grows huge for a while: a roar that strikes all about her, harder blows, tougher
/// hide, and its second move back sooner. Out of a form she takes the last one she was, in its apex.</item>
/// <item><b>The second button</b>: in a form, the creature's own second move; primed (just after a switch), the form's own trick, greater.
/// Out of a form, the last form of her element again.</item>
/// </list>
/// </summary>
public partial class Player
{
    private bool ShifterKit => IsShifter && SimpleKit;

    // ================================================================== the creatures she knows

    /// <summary>The forms she has taken this run (any of them hers to take again, by its element's button).</summary>
    public IReadOnlyCollection<string> KnownForms => Stats.KnownForms;

    private void Know(ShiftForm f)
    {
        if (f == null) return;
        Stats.KnownForms.Add(f.Key);
        RememberFor(f.El, f.Key);
    }

    /// <summary>The next form of an element she knows after <paramref name="cur"/> (null: none after it, back to herself).</summary>
    private ShiftForm NextKnown(HeroElement el, ShiftForm cur)
    {
        var known = ShiftForm.All.Where(f => f.El == el && Stats.KnownForms.Contains(f.Key)).ToList();
        int k = cur == null ? -1 : known.IndexOf(cur);
        return k + 1 < known.Count ? known[k + 1] : null;
    }

    /// <summary>An element's button: into a creature of that kind (see the class notes). False when nothing changed.</summary>
    private bool ShifterElement(HeroElement el)
    {
        if (_modeCd > 0) { SayNo($"{Elements.Name(el).ToUpperInvariant()} {_modeCd:0.0}s"); return false; }
        bool same = el == Mode;
        ShiftForm next = null;
        bool back = false;
        var near = ShiftTarget(el);
        var nearForm = near != null ? ShiftForm.For(near) : null;
        if (nearForm != null && nearForm != Form) next = nearForm;
        else if (Form == null || Form.El != el) next = RememberedFor(el);
        else { next = NextKnown(el, Form); back = next == null; }
        if (same && next == null && !back) { SayNo($"NO {Elements.Name(el).ToUpperInvariant()} CREATURE NEAR"); return false; }
        if (!same) SwitchElement(el);
        else { _modeCd = Tune.Testing.NoCooldowns ? 0f : Tune.Elements.SwitchCooldown; _primedT = Tune.Elements.PrimeSeconds; }
        if (next != null)
        {
            if (Form != null) LeaveForm(cooldown: false);
            EnterForm(next);
            Know(next);
            if (nearForm == next) Copied++;
        }
        else if (Form != null && (back || Form.El != el))
        {
            LeaveForm(cooldown: false);
            if (!back) G.Fx.Text(GlobalPosition + new Vector2(0, -46), $"NO {Elements.Name(el).ToUpperInvariant()} CREATURE KNOWN", new Color(0.85f, 0.85f, 0.9f), 9, 0.8f);
        }
        return true;
    }

    /// <summary>For the tests: creatures copied (not taken again from memory).</summary>
    public int Copied { get; private set; }

    // ================================================================== Molt

    private float _moltCd, _moltHideT;
    /// <summary>For the tests: molts made.</summary>
    public int Molts { get; private set; }
    private float MoltRest => Tune.Shifter.MoltCooldown * Stats.DefensiveRestMult;
    public float MoltFrac => Math.Clamp(_moltCd / Math.Max(0.01f, MoltRest), 0f, 1f);
    /// <summary>Creatures have lost her for a moment after a molt.</summary>
    public bool Molting => _moltHideT > 0f;

    private bool TryMolt(in PlayerInput inp)
    {
        if (_moltCd > 0 && !Tune.Testing.NoCooldowns) return false;
        _moltCd = MoltRest;
        Molts++;
        var d = inp.Move.LengthSquared() > 0.04f ? inp.Move.Normalized() : new Vector2(Facing, 0);
        bool flies = Form != null && (Form.Flier || (Form.Swims && InWater));
        if (!flies && IsOnFloor() && d.Y > -0.5f) d = new Vector2(Math.Abs(d.X) > 0.1f ? Math.Sign(d.X) : Facing, 0);
        float t = Tune.Shifter.MoltTime, dist = Tune.Shifter.MoltDistance * Stats.DodgeDistMult;
        var from = GlobalPosition;
        // the husk she leaves: a pale shell where she stood, crumbling
        var body = Possessed ? Ghost.Animator : Anim;
        if (body != null) Afterimage.Spawn(body, new Color(0.88f, 0.86f, 0.78f, 0.9f), 0.6f);
        G.Fx.Burst(from + new Vector2(0, -6), new Color(0.9f, 0.87f, 0.78f, 0.9f), 14, 90, 2f, 0.6f, 80);
        G.Fx.Ring(from + new Vector2(0, -6), 16, new Color(0.92f, 0.9f, 0.82f, 0.8f), 0.3f, FxLayer.RingStyle.Dashed);
        G.Sfx.Play("dodge", GlobalPosition, -3, 0.05f, 1.25f);
        if (Possessed) Ghost.MasterOverride(d * (dist / t) / Tune.Difficulty.EnemyMoveScale, t, gravity: false);
        else
        {
            _dodgeDir = d; _dodgeT = t; _dodgeSlash = false;
            if (Math.Abs(d.X) > 0.2f) Facing = Math.Sign(d.X);
        }
        _iframes = Math.Max(_iframes, Tune.Shifter.MoltUntouchable);
        _moltHideT = Tune.Shifter.MoltHide;
        // (and those that were coming for her look about for her)
        foreach (var e in G.Enemies) if (IsInstanceValid(e) && !e.Dead && e.GlobalPosition.DistanceTo(from) < 220f) e.ForgetTarget();
        return true;
    }

    // ================================================================== Apex Form

    private float _apexT, _apexBase = 1f;
    /// <summary>The creature she is stands in its apex (seconds left).</summary>
    public bool InApex => _apexT > 0f && Form != null;
    public float ApexLeft => Math.Max(0f, _apexT);
    /// <summary>For the tests: apexes begun.</summary>
    public int Apexes { get; private set; }
    /// <summary>While in her apex the creature's second move rests this much (x).</summary>
    public float FormMoveRestMult => InApex ? Tune.Shifter.ApexMoveRest : 1f;

    private bool StartApex()
    {
        if (!IsShifter) return false;
        if (Form == null)
        {
            var f = RememberedFor(Mode) ?? ShiftForm.All.FirstOrDefault(x => Stats.KnownForms.Contains(x.Key));
            if (f == null) { SayNo("NO FORM KNOWN"); return false; }
            EnterForm(f);
        }
        if (!Possessed) return false;
        _apexT = Tune.Shifter.ApexSeconds + Tune.Shifter.ApexExtraSeconds * Stats.SpecialExtra;
        Apexes++;
        var a = Ghost.Animator;
        if (a != null) { _apexBase = a.SizeNow; a.SizeMult = _apexBase * Tune.Shifter.ApexSize; }
        Ghost.RefreshMove2();
        // the roar: everything about her is struck and thrown back
        float r = Tune.Shifter.ApexRoarRadius * SR;
        var at = GlobalPosition;
        foreach (var e in G.Enemies.ToArray())
        {
            if (!IsInstanceValid(e) || e.Dead || !e.CanBeHit) continue;
            var d = e.GlobalPosition - at;
            if (d.Length() > r + e.HitRadius) continue;
            FormBlow(e, Tune.Shifter.FormDamage * Tune.Shifter.ApexRoarDamage * Stats.SpecialDamageMult, (d.LengthSquared() < 1 ? new Vector2(Facing, 0) : d.Normalized()) * 300f);
            BigStruck++;
        }
        var col = Elements.ColorOf(Form.El);
        G.Fx.Ring(at + new Vector2(0, -6), r, new Color(col, 0.9f), 0.45f, FxLayer.RingStyle.Burst);
        G.Fx.Shockwave(at + new Vector2(0, 10), r * 0.7f, new Color(col, 0.5f), 0.4f);
        G.Fx.Flash(at + new Vector2(0, -8), 30, col, 0.25f);
        G.Fx.AddShake(4f);
        G.Main.Rumble(0.5f, 0.4f, 0.3f);
        G.Sfx.Play("roar", GlobalPosition, 0, 0.05f, 0.8f);
        G.Fx.Text(GlobalPosition + new Vector2(0, -50), $"APEX {Form.Name.ToUpperInvariant()}", col, 12, 1f);
        // (her infusions: what the roar leaves about her)
        Infuse(at, r);
        return true;
    }

    private void EndApex()
    {
        if (_apexT <= 0f && _apexBase == 1f) return;
        _apexT = 0f;
        if (Possessed && Ghost.Animator != null) Ghost.Animator.SizeMult = _apexBase;
        _apexBase = 1f;
    }

    /// <summary>A blow of the form landed while in her apex: the milestone forms of Apex Form (Primal Fury: a shockwave from the blow;
    /// Lifeblood: the blow mends her, and a kill keeps the apex going).</summary>
    private void ApexBlowLanded(Enemy e, float dealt)
    {
        if (!InApex) return;
        if (BigForm("a"))
        {
            foreach (var o in G.Enemies.ToArray())
                if (o != e && IsInstanceValid(o) && !o.Dead && o.CanBeHit && o.GlobalPosition.DistanceTo(e.GlobalPosition) < Tune.Growth.FuryRadius + o.HitRadius)
                    o.Hurt(dealt * Tune.Growth.FuryShare, (o.GlobalPosition - e.GlobalPosition).Normalized() * 120f, o.GlobalPosition);
            G.Fx.Ring(e.GlobalPosition, Tune.Growth.FuryRadius, new Color(Elements.ColorOf(Form.El), 0.6f), 0.2f, FxLayer.RingStyle.Burst);
        }
        if (BigForm("b"))
        {
            Heal(dealt * Tune.Growth.LifebloodShare);
            if (e.Dead) _apexT += Tune.Growth.LifebloodKill;
        }
    }

    private void TickShifterKit(float dt)
    {
        if (_moltCd > 0) _moltCd -= dt;
        if (_moltHideT > 0) _moltHideT -= dt;
        if (_apexT > 0)
        {
            _apexT -= dt;
            if (_apexT <= 0 || Form == null) EndApex();
            else if (Possessed && G.Chance(0.25f)) G.Fx.Ember(GlobalPosition + new Vector2(G.Range(-12, 12), G.Range(-20, 4)), Elements.ColorOf(Form.El));
        }
    }

    // ================================================================== the kit's buttons and their names

    private bool ShifterKitPrimary(Vector2 aim, bool held) => Shifted ? FormPrimary(aim, held) : ShifterStaff(aim, held);

    /// <summary>What a button is called now (the HUD's square): the form's attack and move in a form.</summary>
    public string ShifterLabel(KitRole r) => r switch
    {
        KitRole.Primary => Form != null ? Form.Name : "Staff",
        KitRole.Defensive => "Molt",
        KitRole.Special => "Apex Form",
        _ => Form != null ? FormMoveName : "Recall",
    };

    public float ShifterFrac(KitRole r) => r switch
    {
        KitRole.Primary => Form != null ? 0f : SwingCooldownFrac,
        KitRole.Defensive => MoltFrac,
        KitRole.Special => BigFrac,
        _ => Form != null ? FormSpecialFrac : 0f,
    };
}
