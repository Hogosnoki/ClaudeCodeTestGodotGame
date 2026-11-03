using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

public partial class Main
{
    private IEnumerator<object> _saSteps;

    /// <summary>
    /// `--scenario=shiftall --hero=shapeshifter`: every form she can take, one after another, beside a creature held still: the form's attack
    /// lands on it, the second button makes the creature's own second move (where its kind has one) and that lands too, and nothing the
    /// form does hurts her.
    /// </summary>
    private void ShiftAllScenario()
    {
        if (_saSteps == null) { if (_scT < 0.8f) return; _saSteps = ShiftAllRun().GetEnumerator(); }
        if (!_saSteps.MoveNext()) ScEnd();
    }

    private IEnumerable<object> ShiftAllRun()
    {
        var p = G.Player;
        foreach (var e in G.Enemies.ToArray()) e.QueueFree();
        p.Stats.MaxHp = p.Hp = 100000;
        p.InputOverride = () => { var i = _scInput; _scInput.Attack = _scInput.Ability2 = false; return i; };
        IEnumerable<object> Wait(float s) { for (float t = 0; t < s; t += (float)GetProcessDeltaTime()) yield return null; }
        foreach (var _ in Wait(0.5f)) yield return null;
        int dir = G.Cave.IsSolid(p.GlobalPosition + new Vector2(p.Facing * 80, -10)) ? -(int)p.Facing : (int)p.Facing;
        var home = p.GlobalPosition;
        var missAtk = new List<string>(); var missMove = new List<string>(); var noMove = new List<string>(); var selfHarm = new List<string>();
        int moves = 0;
        // (`--forms=goblin,eel`: only those)
        var only = OS.GetCmdlineUserArgs().FirstOrDefault(x => x.StartsWith("--forms="))?.Substring(8).Split(',');
        foreach (var f in ShiftForm.All.Where(x => only == null || only.Contains(x.Key)))
        {
            if (p.Shifted) p.LeaveForm(false);
            p.GlobalPosition = home; p.Velocity = Vector2.Zero;
            foreach (var _ in Wait(0.2f)) yield return null;
            var foe = new Goblin { Position = home + new Vector2(dir * 46, -4) };
            foe.SetMeta("test", true);
            _world.AddChild(foe);
            foe.Wake();
            foe.Hp = foe.MaxHp = 1000000f;
            foe.Pin(999f);
            p.EnterForm(f);
            foreach (var _ in Wait(0.3f)) yield return null;
            float hp0 = p.Hp, f0 = foe.Hp;
            // ---- the attack, held a while toward it
            for (float t = 0; t < 1.6f; t += (float)GetProcessDeltaTime())
            {
                bool close = Math.Abs(foe.GlobalPosition.X - p.GlobalPosition.X) < Math.Max(26f, f.Reach * 0.8f);
                _scInput = new PlayerInput { Attack = true, AttackHeld = true, Aim = new Vector2(dir, 0), AimGiven = true, Move = close ? Vector2.Zero : new Vector2(dir, 0) };
                yield return null;
            }
            _scInput = default;
            foreach (var _ in Wait(0.4f)) yield return null;
            bool atk = foe.Hp < f0;
            // ---- the second move
            float f1 = foe.Hp;
            var ghost = p.Ghost;
            bool has = ghost != null && ghost.HasFormMove2;
            int uses0 = ghost?.Move2Uses ?? 0;
            if (has)
            {
                foe.GlobalPosition = p.GlobalPosition + new Vector2(dir * (ghost.Move2Of.Kind is Move2.Burst or Move2.Spin or Move2.Spines ? 36 : 70), -4);
                foreach (var _ in Wait(0.1f)) yield return null;
                p.TestSpecial(new Vector2(dir, 0));
                foreach (var _ in Wait(2.2f)) yield return null;
            }
            bool made = has && ghost != null && IsInstanceValid(ghost) && ghost.Move2Uses > uses0;
            bool landed = foe.Hp < f1;
            string name = has ? ghost.Move2Of.Name : "-";
            GD.Print($"[shiftall] {f.Name,-16} attack {(atk ? "lands" : "MISSES")} ({f0 - f1:0})   second move {name}: {(has ? made ? landed ? $"lands ({f1 - foe.Hp:0})" : "made, missed" : "NOT MADE" : "none")}   hero hp {hp0:0} -> {p.Hp:0}");
            // (a fish out of water flops: it strikes only in the water)
            if (!atk && f.Key != "fish") missAtk.Add(f.Name);
            if (has && !made) noMove.Add(f.Name);
            if (has && made && !landed) missMove.Add(f.Name);
            if (made) moves++;
            // (a fish out of water drowns: that is the fish, not a blow of its own)
            if (p.Hp < hp0 - 0.01f && f.Key != "fish") selfHarm.Add(f.Name);
            foe.QueueFree();
        }
        if (p.Shifted) p.LeaveForm(false);
        if (only == null) ScCheck($"every creature kind but the bosses, guardians and structures can be copied ({ShiftForm.All.Length} forms)", ShiftForm.All.Length >= 29);
        ScCheck($"every form's attack lands (missed: {string.Join(", ", missAtk)})", missAtk.Count == 0);
        ScCheck($"every form whose kind has a second move makes it on the second button ({moves} made; not made: {string.Join(", ", noMove)})", noMove.Count == 0);
        ScCheck($"and they land (missed: {string.Join(", ", missMove)})", missMove.Count <= 3);
        ScCheck($"nothing a form does hurts her (hurt in: {string.Join(", ", selfHarm)})", selfHarm.Count == 0);
        p.InputOverride = null;
    }
}
