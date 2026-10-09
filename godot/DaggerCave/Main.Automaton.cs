using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

public partial class Main
{
    private IEnumerator<object> _autoSteps;

    /// <summary>
    /// `--scenario=automaton` (in the Sulphur Springs, as the Automaton): its make (much health, armour, slow, short in the jump, no lungs),
    /// healing that becomes energy instead and Self-Repair that spends it, the acid water, the toxic air, the gas and the steam doing it no
    /// harm, sinking fast and kicking up through the water, Steam Release (a strike, and aimed down, a lift), the combo's slam, Brace (blows in
    /// front softened and unable to move it, blows behind not) and Over-Pressure (a long shaking build-up, then a boost that wears off).
    /// automaton_*.png.
    /// </summary>
    private void AutomatonScenario()
    {
        if (_scPulse > 0 && (_scPulse -= (float)GetProcessDeltaTime()) <= 0) { _scPulse = 0; _scInput = default; }
        if (_autoSteps == null) { if (_scT < 0.6f) return; _autoSteps = AutomatonRun().GetEnumerator(); }
        if (!_autoSteps.MoveNext()) ScEnd();
    }

    private IEnumerable<object> AutomatonRun()
    {
        var p = G.Player; var cave = G.Cave;
        foreach (var e in G.Enemies.ToArray()) e.QueueFree();
        foreach (var r in cave.Rooms) r.Triggered = true;
        foreach (var v in _world.GetChildren().OfType<GasVent>().ToArray()) v.QueueFree();
        IEnumerable<object> Wait(float s) { for (float t = 0; t < s; t += (float)GetProcessDeltaTime()) yield return null; }
        void Stand(Vector2 at) { p.GlobalPosition = at; p.Velocity = Vector2.Zero; }
        float dt() => (float)GetProcessDeltaTime();

        // ---------------------------------------------------------------- its make
        var s = p.Stats;
        ScCheck($"the Automaton: {s.MaxHp:0} health, {s.DamageReduction:0%} armour, x{s.MoveSpeed:0.00} speed, x{s.JumpMult:0.00} jump", s.Hero == HeroKind.Automaton && s.MaxHp >= 130 && s.DamageReduction > 0.1f && s.MoveSpeed < 0.8f && s.JumpMult < 0.8f);
        ScCheck("it has no lungs, and water can't hurt it", s.Breathless && s.InfiniteBreath && s.Waterproof && s.Sinks && s.SelfRepair);

        // ---------------------------------------------------------------- healing becomes energy; Self-Repair spends it
        p.Hp = 100f; p.TestSetEnergy(10f);
        p.Heal(50f);
        ScCheck($"healing doesn't mend it (hp {p.Hp:0.0}) but becomes a fifth as much energy ({p.Energy:0.0}, want 20)", Math.Abs(p.Hp - 100f) < 0.01f && Math.Abs(p.Energy - 20f) < 0.01f);
        Tap(new PlayerInput { Ability = true });
        foreach (var _ in Wait(0.6f)) yield return null;
        ScCheck($"Self-Repair mends it as it spends energy (hp {p.Hp:0.0}, energy {p.Energy:0.0}, repairing {p.Repairing})", p.Repairing && p.Hp > 100.5f && Math.Abs(p.Hp - 100f + p.Energy - 20f) < 0.05f);
        ScShot("automaton_repair");
        for (float t = 0; t < 5f && p.Repairing; t += dt()) yield return null;
        ScCheck($"until the energy runs out: 20 energy, 20 health (hp {p.Hp:0.0}, energy {p.Energy:0.0})", !p.Repairing && Math.Abs(p.Hp - 120f) < 0.1f && p.Energy < 0.01f);
        p.Hp = s.MaxHp;

        // ---------------------------------------------------------------- the acid water, the toxic air
        Vector2? acidAt = null, toxicAt = null;
        float depth = 0;
        for (float x = 200; x < cave.SizePx.X - 200; x += 16)
        {
            var w = new Vector2(x, cave.WaterY + 26);
            if (!cave.IsSolid(w) && !cave.IsSolid(w + new Vector2(0, -20)) && cave.IsWater(w))
            {
                // (the deepest open water: room to sink in)
                float d = 0;
                while (d < 400 && !cave.IsSolid(w + new Vector2(0, d + 16))) d += 8;
                if (d > depth) { depth = d; acidAt = w; }
            }
            if (toxicAt == null && cave.FindFloor(new Vector2(x, cave.WaterY - 120), 110, out var tf) && cave.IsToxic(tf + new Vector2(0, -14)) && !cave.IsWater(tf + new Vector2(0, -4))) toxicAt = tf;
        }
        ScCheck($"acid water to stand in ({acidAt}, {depth:0} px deep) and poisoned air ({toxicAt})", acidAt != null && toxicAt != null);
        if (acidAt != null)
        {
            foreach (var e in G.Enemies.ToArray()) e.QueueFree();
            Stand(acidAt.Value);
            float hpA = p.Hp, maxSink = 0;
            for (float t = 0; t < 0.7f; t += dt()) { maxSink = Math.Max(maxSink, p.Velocity.Y); yield return null; }
            ScCheck($"it sinks like a stone (falling at {maxSink:0} px/s; others drift down at 22)", maxSink > 120f);
            foreach (var _ in Wait(1.5f)) yield return null;
            ScShot("automaton_underwater");
            ScCheck($"the acid does it no harm ({hpA:0.0} -> {p.Hp:0.0}) and it never runs short of breath ({p.Breath:0.0} of {s.BreathMax:0})", p.Hp >= hpA - 0.01f && p.Breath >= s.BreathMax - 0.01f);
            // kicking up: a press of jump at a time
            float y0 = p.GlobalPosition.Y, minVy = 0;
            for (int k = 0; k < 6; k++)
            {
                Pulse(new PlayerInput { Jump = true, JumpHeld = true });
                for (float t = 0; t < 0.22f; t += dt()) { minVy = Math.Min(minVy, p.Velocity.Y); yield return null; }
            }
            ScCheck($"each press of jump kicks it up (up to {-minVy:0} px/s; it rose {y0 - p.GlobalPosition.Y:0} px)", minVy < -150f && p.GlobalPosition.Y < y0 - 20f);
        }
        if (toxicAt != null)
        {
            foreach (var e in G.Enemies.ToArray()) e.QueueFree();
            Stand(toxicAt.Value + new Vector2(0, -14));
            p.Hp = s.MaxHp;
            float hpT = p.Hp;
            foreach (var _ in Wait(2f)) yield return null;
            ScCheck($"the toxic air is nothing to it (breath {p.Breath:0.0} of {s.BreathMax:0}, hp {hpT:0} -> {p.Hp:0})", p.Breath >= s.BreathMax - 0.01f && p.Hp >= hpT - 0.01f);
        }

        // ---------------------------------------------------------------- a level stretch to work on: gas, steam, its own kit
        if (!FindRun(1, 260, false, out var run)) { ScCheck("a stretch of floor to work on", false); yield break; }
        var stand = run + new Vector2(0, 0);
        foreach (var e in G.Enemies.ToArray()) e.QueueFree();
        Stand(stand);
        foreach (var _ in Wait(0.3f)) yield return null;
        var gas = new GasCloud { Position = p.GlobalPosition + new Vector2(0, -6), Radius = 40, Life = 6f };
        _world.AddChild(gas);
        foreach (var _ in Wait(1.5f)) yield return null;
        ScCheck($"the gas doesn't poison it ({p.Poisoned})", !p.Poisoned);
        gas.QueueFree();
        float hpS = p.Hp;
        var jet = new SteamJet { Position = p.GlobalPosition + new Vector2(-50, -4), Dir = Vector2.Right, Length = 120f, Life = 1.5f };
        _world.AddChild(jet);
        foreach (var _ in Wait(1.6f)) yield return null;
        ScCheck($"steam doesn't scald it ({hpS:0} -> {p.Hp:0})", p.Hp >= hpS - 0.01f);
        Stand(stand);
        foreach (var _ in Wait(0.4f)) yield return null;
        p.Facing = 1;

        // creatures to strike (held still)
        Golem Foe(float dx)
        {
            var at = cave.FindFloor(stand + new Vector2(dx, -30), 120, out var f) ? f : stand + new Vector2(dx, 14);
            var g = new Golem { Position = at + new Vector2(0, -20) };
            g.SetMeta("test", true);
            _world.AddChild(g);
            g.MaxHp = g.Hp = 5000;
            g.Freeze(999f, hold: true);
            return g;
        }
        var near = Foe(40);
        var far = Foe(40 + near.HitRadius + 72);
        foreach (var _ in Wait(0.2f)) yield return null;

        // ---------------------------------------------------------------- the combo: chop, chop, slam
        float nearHp = near.Hp, farHp = far.Hp;
        float farAfterTwo = farHp;
        int a0 = p.AttacksStarted;
        bool thirdHeavy = false, shot = false;
        for (float t = 0; t < 4f && p.AttacksStarted < a0 + 3; t += dt())
        {
            // (pressed again once each stroke has struck: the combo goes on while its strokes land)
            if (!p.IsSwinging || p.SwingCooldownFrac <= 0f) Tap(new PlayerInput { Attack = true, Aim = new Vector2(1, 0) });
            yield return null;
            if (p.AttacksStarted == a0 + 3) { farAfterTwo = far.Hp; thirdHeavy = p.BladeHeavy; _scInput = default; }
        }
        _scInput = default;
        for (float t = 0; t < 0.6f; t += dt()) { if (!shot && t > 0.22f) { shot = true; ScShot("automaton_slam"); } yield return null; }
        ScCheck($"the third stroke is the heavy one ({thirdHeavy})", thirdHeavy);
        foreach (var _ in Wait(0.4f)) yield return null;
        ScCheck($"three strokes land on the creature in front ({nearHp - near.Hp:0} damage)", nearHp - near.Hp > Tune.AutomatonHero.Damage * 1.8f);
        ScCheck($"the two chops don't reach the one farther off ({farHp - farAfterTwo:0}), the slam does ({farAfterTwo - far.Hp:0})", farHp - farAfterTwo < 0.5f && farAfterTwo - far.Hp > 1f);

        // ---------------------------------------------------------------- Steam Release: a strike ahead, and aimed down, a lift
        foreach (var _ in Wait(0.6f)) yield return null;
        nearHp = near.Hp;
        Pulse(new PlayerInput { Ability2 = true, Aim = new Vector2(1, 0) });
        foreach (var _ in Wait(0.15f)) yield return null;
        ScShot("automaton_steam");
        foreach (var _ in Wait(0.3f)) yield return null;
        ScCheck($"the steam strikes what's in front ({nearHp - near.Hp:0.0})", nearHp - near.Hp > Tune.AutomatonHero.SteamDamage * 0.5f);
        p.TestSetEnergy(p.Energy);
        Stand(stand);
        foreach (var _ in Wait(0.4f)) yield return null;
        float yS = p.GlobalPosition.Y, minY = yS;
        Pulse(new PlayerInput { Ability2 = true, Aim = new Vector2(0, 1) });
        for (float t = 0; t < 0.6f; t += dt()) { minY = Math.Min(minY, p.GlobalPosition.Y); yield return null; }
        ScCheck($"aimed at the floor it lifts it {yS - minY:0} px", yS - minY > 20f);
        foreach (var _ in Wait(0.6f)) yield return null;

        // ---------------------------------------------------------------- Brace
        Stand(stand); p.Facing = 1;
        foreach (var _ in Wait(0.3f)) yield return null;
        Pulse(new PlayerInput { Dodge = true });
        foreach (var _ in Wait(0.1f)) yield return null;
        ScCheck($"the dodge button braces it ({p.Bracing})", p.Bracing);
        ScShot("automaton_brace");
        float h0 = p.Hp;
        p.Stats.HurtInvuln = 0f;
        p.Hurt(20f, p.GlobalPosition + new Vector2(30, 0), 400f);
        float frontLoss = h0 - p.Hp, kick = Math.Abs(p.Velocity.X);
        ScCheck($"a blow in front costs {frontLoss:0.0} (want {20 * (1 - s.DamageReduction) * (1 - Tune.AutomatonHero.BraceSoak):0.0}) and doesn't move it ({kick:0} px/s)", Math.Abs(frontLoss - 20 * (1 - s.DamageReduction) * (1 - Tune.AutomatonHero.BraceSoak)) < 0.2f && kick < 1f);
        foreach (var _ in Wait(0.05f)) yield return null;
        h0 = p.Hp;
        p.Hurt(20f, p.GlobalPosition + new Vector2(-30, 0), 400f);
        float backLoss = h0 - p.Hp;
        ScCheck($"a blow from behind isn't softened ({backLoss:0.0})", Math.Abs(backLoss - 20 * (1 - s.DamageReduction)) < 0.2f);
        foreach (var _ in Wait(1.2f)) yield return null;
        // unbraced, a blow shoves it less than it would anyone else
        Stand(stand); p.Facing = 1;
        foreach (var _ in Wait(0.2f)) yield return null;
        p.Hurt(5f, p.GlobalPosition + new Vector2(30, 0), 400f);
        float shove = Math.Abs(p.Velocity.X);
        ScCheck($"unbraced, a blow shoves it at {shove:0} px/s (x{s.KnockTakenMult} of {400 * Tune.Combat.HurtKnockbackMult:0})", shove > 1f && shove < 400 * Tune.Combat.HurtKnockbackMult * 0.6f);
        p.Hp = s.MaxHp;
        foreach (var _ in Wait(0.6f)) yield return null;

        // ---------------------------------------------------------------- Over-Pressure
        Stand(stand);
        foreach (var _ in Wait(0.3f)) yield return null;
        float atk0 = s.AttackSpeed, dmg0 = s.DamageMult, mv0 = s.MoveSpeed, jmp0 = s.JumpMult;
        ScCheck($"its support is Over-Pressure ({p.SupportName})", p.SupportName == "PRESSURE");
        p.TestSupport(new Vector2(1, 0));
        foreach (var _ in Wait(0.5f)) yield return null;
        ScCheck($"it begins to build pressure ({p.Pressurising}), no boost yet (x{s.AttackSpeed / atk0:0.00})", p.Pressurising && !p.Overpressured && Math.Abs(s.AttackSpeed - atk0) < 0.001f);
        foreach (var _ in Wait(2.0f)) yield return null;
        ScShot("automaton_pressure_build");
        for (float t = 0; t < 1.5f && p.Pressurising; t += dt()) yield return null;
        ScCheck($"after about three seconds it's over-pressured ({p.Overpressured}, {p.PressureLeft:0.0}s left)", p.Overpressured && p.PressureLeft > Tune.Support.PressureBuff - 1f);
        ScCheck($"striking x{s.AttackSpeed / atk0:0.00} as fast and x{s.DamageMult / dmg0:0.00} as hard, moving x{s.MoveSpeed / mv0:0.00}, jumping x{s.JumpMult / jmp0:0.00}",
            Math.Abs(s.AttackSpeed / atk0 - Tune.Support.PressureAttack) < 0.01f && Math.Abs(s.DamageMult / dmg0 - Tune.Support.PressureDamage) < 0.01f
            && Math.Abs(s.MoveSpeed / mv0 - Tune.Support.PressureSpeed) < 0.01f && Math.Abs(s.JumpMult / jmp0 - Tune.Support.PressureJump) < 0.01f);
        foreach (var _ in Wait(0.3f)) yield return null;
        ScShot("automaton_pressure_full");
        for (float t = 0; t < Tune.Support.PressureBuff + 1f && p.Overpressured; t += dt()) yield return null;
        ScCheck($"and it wears off, every stat as it was (x{s.AttackSpeed / atk0:0.000}, x{s.DamageMult / dmg0:0.000}, x{s.MoveSpeed / mv0:0.000}, x{s.JumpMult / jmp0:0.000})",
            !p.Overpressured && Math.Abs(s.AttackSpeed - atk0) < 0.001f && Math.Abs(s.DamageMult - dmg0) < 0.001f && Math.Abs(s.MoveSpeed - mv0) < 0.001f && Math.Abs(s.JumpMult - jmp0) < 0.001f);
    }
}
