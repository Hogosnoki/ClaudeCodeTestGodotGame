using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

public partial class Main
{
    private IEnumerator<object> _sspSteps;

    /// <summary>
    /// `--scenario=shifterspecials --hero=shifter`: the Transformation (a form's blows land harder than the creature's own would), the rat's Gnaw (bites
    /// straight ahead only), the bat's Latch (hangs on biting, lets go, mends), the scorpion's Venom Spray (a cone that poisons) and the
    /// spider's Envenomate (one creature, a powerful venom). Each against creatures held still beside the hero.
    /// </summary>
    private void ShifterSpecialsScenario()
    {
        if (_sspSteps == null) { if (_scT < 0.6f) return; _sspSteps = ShifterSpecialsRun().GetEnumerator(); }
        if (!_sspSteps.MoveNext()) ScEnd();
    }

    private IEnumerable<object> ShifterSpecialsRun()
    {
        var p = G.Player;
        foreach (var e in G.Enemies.ToArray()) e.QueueFree();
        p.Stats.MaxHp = 5000; p.Hp = 5000;
        p.InputOverride = () => { var i = _scInput; _scInput.Jump = false; _scInput.Attack = false; _scInput.Ability2 = false; return i; };
        // (well away from the cave mouth, whose daylight would wash the effects out)
        bool found = FindRun(1, 140, false, out var stand, 420f);
        ScCheck($"a floor to try them on ({stand.Round()})", found);
        if (!found) yield break;
        var held = new List<Enemy>();
        Enemy Foe(Vector2 off)
        {
            var e = new Goblin { Position = stand + off };
            e.SetMeta("test", true);
            _world.AddChild(e);
            e.MaxHp = e.Hp = 100000;
            held.Add(e);
            return e;
        }
        void Hold() { foreach (var e in held) if (GodotObject.IsInstanceValid(e) && !e.Dead) e.Freeze(99f, hold: true); }
        void Clear() { foreach (var e in held) if (GodotObject.IsInstanceValid(e)) e.QueueFree(); held.Clear(); }
        IEnumerable<object> Become(string key)
        {
            if (p.Shifted) p.LeaveForm(false);
            p.GlobalPosition = stand; p.Velocity = Vector2.Zero; p.Facing = 1;
            p.EnterForm(ShiftForm.All.First(f => f.Key == key));
            // (settled, and its special ready: a Shift leaves it at least a moment to come back)
            for (float t = 0; t < 4f && (t < 0.7f || !p.FormSpecialReady); t += (float)GetProcessDeltaTime()) { Hold(); yield return null; }
        }
        IEnumerable<object> Wait(float s) { for (float t = 0; t < s; t += (float)GetProcessDeltaTime()) { Hold(); yield return null; } }

        // ---------------------------------------------------------------- the Transformation
        foreach (var _ in Become("goblin")) yield return null;
        ScCheck($"in a form the Shape Shifter has the Transformation (x{p.TransformMult:0.00} damage)", Math.Abs(p.TransformMult - (1f + Tune.Shifter.TransformDamage)) < 0.001f);
        float bonus = Tune.Shifter.TransformDamage;
        float Swing(Enemy foe)
        {
            float hp0 = foe.Hp;
            p.FormBlow(foe, 10f, Vector2.Zero);
            return hp0 - foe.Hp;
        }
        var dummy = Foe(new Vector2(30, 0));
        foreach (var _ in Wait(0.3f)) yield return null;
        float with = 0, without = 0;
        for (int k = 0; k < 8; k++) with += Swing(dummy);
        Tune.Shifter.TransformDamage = 0f;
        for (int k = 0; k < 8; k++) without += Swing(dummy);
        Tune.Shifter.TransformDamage = bonus;
        ScCheck($"and a blow of the creature's own lands harder for it ({with / 8f:0.0} against {without / 8f:0.0} without it: x{with / Math.Max(1f, without):0.00})",
            with / Math.Max(1f, without) > 1f + bonus * 0.8f && with / Math.Max(1f, without) < 1f + bonus * 1.2f);
        Clear();

        // ---------------------------------------------------------------- the rat's Gnaw: straight ahead only
        foreach (var _ in Become("rat")) yield return null;
        var ahead = Foe(new Vector2(26, 0));
        var behind = Foe(new Vector2(-26, 0));
        foreach (var _ in Wait(0.3f)) yield return null;
        float ha = ahead.Hp, hb = behind.Hp; int bites0 = p.GnawBites;
        _scInput = new PlayerInput { Ability2 = true, Aim = new Vector2(1, 0), AimGiven = true };
        for (float t = 0; t < 1.2f; t += (float)GetProcessDeltaTime())
        {
            Hold();
            if (t > 0.18f && t < 0.18f + (float)GetProcessDeltaTime() * 1.5f) ScShot("form_rat_gnaw");
            yield return null;
        }
        float rate = Tune.Shifter.FormDamage * ShiftForm.All.First(f => f.Key == "rat").SpecDmg * p.Stats.DamageMult * p.Stats.FormDmgMult * p.Stats.FormSpecDmgMult * p.TransformMult;
        ScCheck($"the rat gnaws at what is ahead ({p.GnawBites - bites0} bites, {ha - ahead.Hp:0} damage, about {rate:0.0} a bite)",
            p.GnawBites - bites0 >= Tune.Shifter.GnawBites - 1 && ha - ahead.Hp > rate * (Tune.Shifter.GnawBites - 1) * 0.85f && ha - ahead.Hp < rate * Tune.Shifter.GnawBites * 1.15f);
        ScCheck($"and not at what is behind it ({hb - behind.Hp:0} damage)", hb - behind.Hp < 0.01f);
        Clear();

        // ---------------------------------------------------------------- the bat's Latch
        foreach (var _ in Become("bat")) yield return null;
        var prey = Foe(new Vector2(90, -10));
        foreach (var _ in Wait(0.3f)) yield return null;
        p.Hp = p.Stats.MaxHp * 0.5f;
        float hp0 = prey.Hp;
        _scInput = new PlayerInput { Ability2 = true, Aim = new Vector2(1, 0), AimGiven = true };
        bool latched = false, shot = false;
        for (float t = 0; t < 1.2f && !latched; t += (float)GetProcessDeltaTime()) { Hold(); latched = p.LatchedOn == prey; yield return null; }
        ScCheck($"the bat flies at the creature and latches on ({latched})", latched);
        for (float t = 0; t < 2.5f && p.LatchedOn != null; t += (float)GetProcessDeltaTime())
        {
            Hold();
            if (!shot && t > 0.4f) { shot = true; ScShot("form_bat_latch"); }
            yield return null;
        }
        float bitten = hp0 - prey.Hp;
        float want = Tune.Shifter.FormDamage * ShiftForm.All.First(f => f.Key == "bat").SpecDmg * p.Stats.DamageMult * p.Stats.FormDmgMult * p.Stats.FormSpecDmgMult * p.TransformMult;
        ScCheck($"bites it hard while it hangs on ({bitten:0} of about {want:0}), then lets go ({p.LatchedOn == null})", p.LatchedOn == null && bitten > want * 0.8f && bitten < want * 1.2f);
        float healAt = p.Hp;
        foreach (var _ in Wait(2f)) yield return null;
        ScCheck($"and the blood mends the hero over the next seconds ({healAt:0} -> {p.Hp:0}, mending {p.Mended})", p.Hp > healAt + p.Stats.MaxHp * Tune.Shifter.LatchHealShare * 0.25f);
        Clear();
        p.Hp = p.Stats.MaxHp;

        // ---------------------------------------------------------------- the scorpion's Venom Spray
        foreach (var _ in Become("scorpion")) yield return null;
        var front = Foe(new Vector2(60, 0));
        var back = Foe(new Vector2(-50, 0));
        foreach (var _ in Wait(0.3f)) yield return null;
        float hf = front.Hp, hk = back.Hp;
        _scInput = new PlayerInput { Ability2 = true, Aim = new Vector2(1, 0), AimGiven = true };
        foreach (var _ in Wait(0.12f)) yield return null;
        ScShot("form_scorpion_spray");
        foreach (var _ in Wait(0.3f)) yield return null;
        float hit = hf - front.Hp;
        ScCheck($"the scorpion sprays venom ahead: the creature there is poisoned ({front.Envenomed}, {front.VenomLeft:0} to come) and the one behind is not ({back.Envenomed}, {hk - back.Hp:0} damage)",
            front.Envenomed && front.VenomLeft > 10f && !back.Envenomed && hk - back.Hp < 0.01f);
        foreach (var _ in Wait(2f)) yield return null;
        ScCheck($"and the poison works on ({hit:0} from the spray, {hf - front.Hp:0} two seconds on)", hf - front.Hp > hit + 5f);
        Clear();

        // ---------------------------------------------------------------- the spider's Envenomate
        foreach (var _ in Become("spider")) yield return null;
        var mark = Foe(new Vector2(100, 0));
        foreach (var _ in Wait(0.3f)) yield return null;
        int env0 = p.Envenomings;
        _scInput = new PlayerInput { Ability2 = true, Aim = new Vector2(1, 0), AimGiven = true };
        shot = false;
        for (float t = 0; t < 1.5f && p.Envenomings == env0; t += (float)GetProcessDeltaTime())
        {
            Hold();
            if (!shot && t > 0.1f) { shot = true; ScShot("form_spider_pounce"); }
            yield return null;
        }
        float dose = Tune.Shifter.FormDamage * Tune.Shifter.EnvenomTotal * p.Stats.DamageMult * p.Stats.FormDmgMult * p.Stats.FormSpecDmgMult * p.TransformMult;
        ScCheck($"the spider pounces on its mark and bites ({p.Envenomings - env0}): a powerful venom ({mark.VenomLeft:0} of about {dose:0} to come)",
            p.Envenomings > env0 && mark.Envenomed && mark.VenomLeft > dose * 0.75f);
        ScShot("form_spider_bite");
        Clear();
        if (p.Shifted) p.LeaveForm(false);
    }
}
