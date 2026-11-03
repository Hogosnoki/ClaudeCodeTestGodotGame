using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

public partial class Main
{
    private IEnumerator<object> _skSteps;

    /// <summary>
    /// `--scenario=shifterkit --hero=shifter` (her own kit): an element's button copies the creature of that kind beside her; pressed again it
    /// takes her back to herself; another element with no creature of its kind switches the element only; the first element's button again
    /// takes the creature she knows from memory; primed by that switch, the second button makes the form's own trick (greater), then the
    /// creature's own second move; Molt slips her away and the creatures lose her; Apex Form grows the creature, its roar strikes, and it
    /// shrinks back when it ends.
    /// </summary>
    private void ShifterKitScenario()
    {
        if (_skSteps == null) { if (_scT < 0.8f) return; _skSteps = ShifterKitRun().GetEnumerator(); }
        if (!_skSteps.MoveNext()) ScEnd();
    }

    private IEnumerable<object> ShifterKitRun()
    {
        var p = G.Player;
        foreach (var e in G.Enemies.ToArray()) e.QueueFree();
        p.Stats.MaxHp = p.Hp = 5000;
        p.InputOverride = () => { var i = _scInput; _scInput.Dodge = _scInput.Ability = _scInput.Ability2 = _scInput.Attack = false; return i; };
        IEnumerable<object> Wait(float s) { for (float t = 0; t < s; t += (float)GetProcessDeltaTime()) yield return null; }
        foreach (var _ in Wait(0.5f)) yield return null;
        ScCheck($"she has the kit the others have ({p.SimpleKit}; {p.KitLabel(KitRole.Defensive)}, {p.KitLabel(KitRole.Special)})", p.SimpleKit && p.KitLabel(KitRole.Defensive) == "Molt" && p.KitLabel(KitRole.Special) == "Apex Form");
        var els = Elements.Of(HeroKind.ShapeShifter);
        int Slot(HeroElement el) => Array.IndexOf(els, el);
        int dir = G.Cave.IsSolid(p.GlobalPosition + new Vector2(p.Facing * 80, -10)) ? -(int)p.Facing : (int)p.Facing;
        Enemy Dummy(Enemy e, float dx)
        {
            e.Position = p.GlobalPosition + new Vector2(dir * dx, -4);
            e.SetMeta("test", true);
            _world.AddChild(e);
            e.Wake();
            e.Hp = e.MaxHp = 1000000f;
            e.Pin(999f);
            return e;
        }
        // ---- nature's button beside a goblin: she becomes it
        var gob = Dummy(new Goblin(), 60);
        foreach (var _ in Wait(0.3f)) yield return null;
        p.TryInfuse(Slot(HeroElement.Nature));
        foreach (var _ in Wait(0.2f)) yield return null;
        ScCheck($"nature's button beside a goblin makes her one ({p.Form?.Name ?? "herself"}; copied {p.Copied}, knows {string.Join(",", p.KnownForms)})", p.Form?.Key == "goblin" && p.Copied == 1 && p.KnownForms.Contains("goblin"));
        // ---- pressed again: back to herself
        foreach (var _ in Wait(Tune.Elements.SwitchCooldown + 0.2f)) yield return null;
        p.TryInfuse(Slot(HeroElement.Nature));
        foreach (var _ in Wait(0.2f)) yield return null;
        ScCheck($"pressed again (no other walker known) it takes her back to herself ({p.Form?.Name ?? "herself"})", !p.Shifted);
        gob.QueueFree();
        // ---- wind's button, with no flier near or known: the element only
        foreach (var _ in Wait(Tune.Elements.SwitchCooldown + 0.2f)) yield return null;
        p.TryInfuse(Slot(HeroElement.Wind));
        foreach (var _ in Wait(0.2f)) yield return null;
        ScCheck($"wind's button with no flier near or known switches the element only ({p.Mode}, {p.Form?.Name ?? "herself"})", p.Mode == HeroElement.Wind && !p.Shifted);
        // ---- nature's again: the goblin from memory, and primed by the switch, the form's own trick
        var foe = Dummy(new Goblin(), 200);
        foreach (var _ in Wait(Tune.Elements.SwitchCooldown + 0.2f)) yield return null;
        p.TryInfuse(Slot(HeroElement.Nature));
        foreach (var _ in Wait(0.2f)) yield return null;
        ScCheck($"nature's button with no walker near takes the goblin she knows ({p.Form?.Name ?? "herself"}; copied still {p.Copied})", p.Form?.Key == "goblin" && p.Copied == 1);
        foe.GlobalPosition = p.GlobalPosition + new Vector2(dir * 40, -4);
        int greater0 = p.GreaterSpecials;
        _scInput = new PlayerInput { Ability2 = true, Aim = new Vector2(dir, 0), AimGiven = true };
        foreach (var _ in Wait(0.3f)) yield return null;
        ScCheck($"primed by the switch, the second button is the form's own trick, greater ({p.GreaterSpecials - greater0})", p.GreaterSpecials > greater0);
        foreach (var _ in Wait(1.5f)) yield return null;
        int moves0 = p.FormMoves;
        foe.GlobalPosition = p.GlobalPosition + new Vector2(dir * 70, -4);
        _scInput = new PlayerInput { Ability2 = true, Aim = new Vector2(dir, 0), AimGiven = true };
        foreach (var _ in Wait(0.3f)) yield return null;
        ScCheck($"after that, the creature's own second move ({p.Ghost?.Move2Of?.Name}; {p.FormMoves - moves0})", p.FormMoves > moves0);
        foreach (var _ in Wait(1.5f)) yield return null;
        // ---- Molt
        var at = p.GlobalPosition;
        _scInput = new PlayerInput { Dodge = true, Move = new Vector2(-dir, 0) };
        yield return null; yield return null;
        bool hid = p.Molting && p.Hidden;
        _scInput = default;
        foreach (var _ in Wait(0.4f)) yield return null;
        float moved = p.GlobalPosition.DistanceTo(at);
        ScCheck($"Molt slips her away ({moved:0} px) and the creatures lose her ({hid}; molts {p.Molts})", p.Molts == 1 && hid && moved > 50f);
        // ---- Apex Form
        foe.GlobalPosition = p.GlobalPosition + new Vector2(dir * 50, -4);
        foreach (var _ in Wait(0.2f)) yield return null;
        float size0 = p.Ghost?.Animator?.SizeNow ?? 1f, h0 = foe.Hp;
        _scInput = new PlayerInput { Ability = true, Aim = new Vector2(dir, 0), AimGiven = true };
        foreach (var _ in Wait(0.3f)) yield return null;
        float size1 = p.Ghost?.Animator?.SizeNow ?? 1f;
        ScCheck($"Apex Form: the creature grows ({size0:0.00} -> {size1:0.00}), its roar strikes ({h0 - foe.Hp:0}), in its apex ({p.InApex}, {p.ApexLeft:0.0} s)", p.InApex && size1 > size0 * 1.2f && foe.Hp < h0);
        foreach (var _ in Wait(Tune.Shifter.ApexSeconds + 0.5f)) yield return null;
        ScCheck($"and when it ends it shrinks back ({p.Ghost?.Animator?.SizeNow ?? 0:0.00}; apex {p.InApex})", !p.InApex && Math.Abs((p.Ghost?.Animator?.SizeNow ?? size0) - size0) < 0.01f);
        foe.QueueFree();
        p.InputOverride = null;
    }
}
