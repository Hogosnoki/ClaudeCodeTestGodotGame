using System;
using System.Linq;
using Godot;

namespace DaggerCave;

/// <summary>
/// The elemental Shape Shifter (nature, wind, water; <see cref="Tune.ElementalShifter"/>). The element is the kind of creature she becomes:
/// nature the creatures that walk (goblin, skeleton, rat, spider, scorpion, bear, golem, sporeling), wind those that fly (bat, hornet),
/// water those of the water (fish, crab, frog) (<see cref="ShiftForm.El"/>). Shift copies the nearest creature of her mode's kind; with none
/// near, she takes the form of that kind she remembers; with none of those either but another kind near, she copies it and her mode follows
/// its element. The dodge button, in a form, remembers it for its element (one for each), and switching the mode while shifted takes her
/// into the remembered form of the new element (or back to herself). A form's blows carry its element (nature or water build-up, a shove
/// for wind), and the attribute on top rides on them too; overcharged, the creature's special is its greater one (its blows harder, and
/// a full meter of the element). Out of a form, her staff carries the mode's element.
/// </summary>
public partial class Player
{
    /// <summary>The form she remembers for an element (null: none yet).</summary>
    public ShiftForm RememberedFor(HeroElement el)
    {
        string key = el switch { HeroElement.Wind => Stats.RememberedWind, HeroElement.Water => Stats.RememberedWater, _ => Stats.RememberedNature };
        return key == null ? null : Array.Find(ShiftForm.All, f => f.Key == key);
    }

    private void RememberFor(HeroElement el, string key)
    {
        switch (el)
        {
            case HeroElement.Wind: Stats.RememberedWind = key; break;
            case HeroElement.Water: Stats.RememberedWater = key; break;
            default: Stats.RememberedNature = key; break;
        }
    }

    /// <summary>What an element makes of a creature's name (a Venom Rat, a Storm Bat, a Tide Crab).</summary>
    public static string ShapeName(ShiftForm f) => f == null ? "" : $"{(f.El == HeroElement.Wind ? "Storm" : f.El == HeroElement.Water ? "Tide" : "Venom")} {f.Name}";

    /// <summary>The nearest creature in range she could copy, of a kind (<see cref="HeroElement.None"/>: any).</summary>
    public Enemy ShiftTarget(HeroElement kind)
    {
        Enemy best = null; float bd = Tune.Shifter.CopyRange * Stats.ShiftRangeMult;
        foreach (var e in G.Enemies)
        {
            if (!GodotObject.IsInstanceValid(e) || e.Dead) continue;
            var f = ShiftForm.For(e);
            if (f == null || (kind != HeroElement.None && f.El != kind)) continue;
            float d = e.GlobalPosition.DistanceTo(GlobalPosition);
            if (d < bd) { bd = d; best = e; }
        }
        return best;
    }

    /// <summary>The ability button: drop the form; or copy the mode's kind, take its remembered form, or copy another kind (the mode follows).</summary>
    private bool ShifterShift()
    {
        if (!IsShifter || _shiftLock > 0) return false;
        if (Form != null) { LeaveForm(cooldown: true); return true; }
        if (!AbilityChargeReady && !Tune.Testing.NoCooldowns) return false;
        var mode = Mode;
        if (ShiftTarget(mode) is Enemy e) { EnterForm(ShiftForm.For(e)); return true; }
        if (RememberedFor(mode) is ShiftForm rem)
        {
            EnterForm(rem);
            Recalled++;
            G.Fx.Text(GlobalPosition + new Vector2(0, -46), "RECALLED", new Color(0.8f, 0.95f, 1f), 9, 0.7f);
            return true;
        }
        if (ShiftTarget(HeroElement.None) is Enemy other)
        {
            // (none of the mode's kind near, nor remembered: she takes what there is, and the mode follows its element)
            var f = ShiftForm.For(other);
            ShifterSetMode(f.El);
            EnterForm(f);
            return true;
        }
        SayNo($"NO {Elements.Name(mode).ToUpperInvariant()} CREATURE NEAR");
        _shiftLock = 0.4f;
        return true;
    }

    /// <summary>The dodge button in a form: remembered for its element.</summary>
    private bool ShifterRemember()
    {
        if (!IsShifter || Form == null) return false;
        if (_rememberCd > 0) return true;
        _rememberCd = Tune.Shifter.RememberCooldown;
        var f = Form;
        if (RememberedFor(f.El) == f) { G.Fx.Text(GlobalPosition + new Vector2(0, -34), "ALREADY REMEMBERED", new Color(0.8f, 0.85f, 0.95f), 9, 0.6f); return true; }
        RememberFor(f.El, f.Key);
        Stats.RememberedForm = f.Key;
        Remembered++;
        var col = Elements.ColorOf(f.El);
        G.Fx.Ring(GlobalPosition + new Vector2(0, -8), 20, new Color(col, 0.9f), 0.5f, FxLayer.RingStyle.Wave);
        G.Fx.Text(GlobalPosition + new Vector2(0, -34), $"REMEMBERED ({Elements.Name(f.El).ToUpperInvariant()}): {f.Name.ToUpperInvariant()}", col, 11, 1f);
        G.Sfx.Play("levelup", GlobalPosition, -10, 0.05f, 1.5f);
        return true;
    }

    /// <summary>The mode switched while shifted: into the remembered form of the new element, or back to herself.</summary>
    private void ShifterModeSwitched(HeroElement el)
    {
        if (Form == null || Form.El == el) return;
        var rem = RememberedFor(el);
        LeaveForm(cooldown: false);
        if (rem != null) { EnterForm(rem); return; }
        G.Fx.Text(GlobalPosition + new Vector2(0, -46), $"NO {Elements.Name(el).ToUpperInvariant()} FORM REMEMBERED", new Color(0.85f, 0.85f, 0.9f), 9, 0.8f);
    }

    /// <summary>The second button out of a form: the remembered form of the mode's element.</summary>
    private bool ShifterRecall()
    {
        var f = RememberedFor(Mode);
        if (f == null || _shiftLock > 0) { SayNo(f == null ? $"NO {Elements.Name(Mode).ToUpperInvariant()} FORM REMEMBERED" : "NOT YET"); return true; }
        if (!AbilityChargeReady && !Tune.Testing.NoCooldowns) return false;
        EnterForm(f);
        Recalled++;
        G.Fx.Text(GlobalPosition + new Vector2(0, -46), "RECALLED", new Color(0.8f, 0.95f, 1f), 9, 0.7f);
        return true;
    }

    // ================================================================== the element on her blows

    /// <summary>A greater special under way: its blows land harder and fill the form's element (seconds left).</summary>
    private float _greaterT;
    /// <summary>For the tests: greater specials begun.</summary>
    public int GreaterSpecials { get; private set; }

    /// <summary>The overcharge spent on the creature's special: its greater one.</summary>
    private void StartGreater()
    {
        SpendPair();
        _greaterT = Tune.ElementalShifter.GreaterSeconds;
        GreaterSpecials++;
        var f = Form;
        var col = Elements.ColorOf(f.El);
        G.Fx.Ring(GlobalPosition + new Vector2(0, -8), 28, new Color(col, 0.95f), 0.4f, FxLayer.RingStyle.Burst);
        G.Fx.Flash(GlobalPosition + new Vector2(0, -8), 22, col, 0.2f);
        _lastEl = f.El;
        Used(f.GreaterName, col);
    }

    /// <summary>The damage of a form's blow, elemental: harder while a greater special is under way.</summary>
    private float ShapeBlowMult => Elemental && Form != null && _greaterT > 0 ? Tune.ElementalShifter.GreaterDamage : 1f;

    /// <summary>A form's blow landed on <paramref name="e"/>: its element (and the attribute on top).</summary>
    private void ShapeBlowLanded(Enemy e, Vector2 knock)
    {
        if (!Elemental || Form == null || e.Dead) return;
        float p = Stats.DamageMult;
        bool greater = _greaterT > 0;
        switch (Form.El)
        {
            case HeroElement.Wind:
                var dir = knock.LengthSquared() > 1 ? knock.Normalized() : new Vector2(Facing, 0);
                e.Shove(dir * (greater ? Tune.ElementalShifter.GreaterShove : Tune.ElementalShifter.WindShove) + new Vector2(0, -50f));
                break;
            default:
                e.Build(Form.El, greater ? Tune.Elements.Full : Tune.ElementalShifter.BlowBuild, p, this);
                break;
        }
        if (!e.Dead) AttributeHit(e, GlobalPosition);
    }

    private void TickShifterElements(float dt)
    {
        if (_greaterT > 0) _greaterT -= dt;
    }

    // ================================================================== her staff, out of a form

    /// <summary>The attack out of a form: the staff, carrying the mode's element.</summary>
    private bool ShifterStaff(Vector2 aim, bool held)
    {
        if (!TrySwing(aim, held)) return false;
        var el = Mode;
        if (el == HeroElement.Wind) _swingReach *= Tune.ElementalShifter.GaleReach;
        else { _swingEl = el; _swingBuild = Tune.ElementalShifter.StaffBuild; }
        _lastEl = el;
        Used(Elements.SingleName(Stats.Hero, ElemSlot.Attack, el));
        return true;
    }

    /// <summary>For the HUD: what a button would do now (the form's special, greater if overcharged; or the shift).</summary>
    private string ShifterNameFor(ElemSlot slot)
    {
        var f = Form;
        return slot switch
        {
            ElemSlot.Heavy => f == null ? "Recall" : Overcharged ? f.GreaterName : f.SpecialName,
            ElemSlot.Move => f != null ? ShapeName(f) : Elements.SingleName(Stats.Hero, ElemSlot.Move, Mode),
            _ => f != null ? f.Name : Elements.SingleName(Stats.Hero, ElemSlot.Attack, Mode),
        };
    }

    /// <summary>The mode set outright (a creature of another kind copied): nothing on top, no cooldown.</summary>
    private void ShifterSetMode(HeroElement el)
    {
        if (Mode == el) return;
        TestMode(el);
        G.Fx.Text(GlobalPosition + new Vector2(0, -58), $"{Elements.Name(el).ToUpperInvariant()} MODE", Elements.ColorOf(el), 10, 0.8f);
    }
}
