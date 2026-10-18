using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

public partial class Main
{
    private IEnumerator<object> _elSteps;

    /// <summary>
    /// `--scenario=elements --hero=swordsman|elementalist`: the elemental kits (Core/Elements.cs). An element readied with its button
    /// changes the next attack into its version; build-ups fill and take hold (alight, combust; chilled, frozen, shatter; stunned,
    /// sapped; shoved); each version rests its own time; the cards the elements replace aren't dealt; the HUD (elements_hud.png).
    /// </summary>
    private void ElementsScenario()
    {
        if (_elSteps == null) { if (_scT < 0.8f) return; _elSteps = (G.Player.Stats.Hero == HeroKind.Elementalist ? ElementalistRun() : SwordsmanElementsRun()).GetEnumerator(); }
        if (!_elSteps.MoveNext()) ScEnd();
    }

    private IEnumerable<object> ElWait(float s) { for (float t = 0; t < s; t += (float)GetProcessDeltaTime()) yield return null; }

    /// <summary>A creature that stands still and can take it all (a goblin: freezable, stunnable).</summary>
    private Enemy ElDummy(Player p, float dx, bool hold = true)
    {
        var foe = new Goblin { Position = p.GlobalPosition + new Vector2(dx * p.Facing, -4) };
        foe.SetMeta("test", true);
        _world.AddChild(foe);
        foe.Wake();
        foe.Hp = foe.MaxHp = 1000000f;
        if (hold) foe.Freeze(9999f, hold: true);
        return foe;
    }

    /// <summary>Presses for one frame what <paramref name="inp"/> holds, then lets go.</summary>
    private IEnumerable<object> ElPress(PlayerInput inp, float after = 0.1f)
    {
        _scInput = inp;
        yield return null; yield return null;
        _scInput = default;
        foreach (var _ in ElWait(after)) yield return null;
    }

    /// <summary>The Swordsman's elemental swing as soon as the element is free to go on one: ready it, swing, until the blow is used.</summary>
    private IEnumerable<object> ElStrike(Player p, int slot, Func<PlayerInput> atk)
    {
        while (p.SwingElementRest > 0) yield return null;
        int n = p.ElementalUses.Count;
        if (p.Infused != Elements.Of(p.Stats.Hero)[slot - 1]) foreach (var _ in ElPress(new PlayerInput { Infuse = slot }, 0f)) yield return null;
        for (int f = 0; f < 60 && p.ElementalUses.Count == n; f++) { _scInput = atk(); yield return null; }
        _scInput = default;
        foreach (var _ in ElWait(0.25f)) yield return null;
    }

    private IEnumerable<object> SwordsmanElementsRun()
    {
        var p = G.Player;
        foreach (var e in G.Enemies.ToArray()) e.QueueFree();
        p.Stats.MaxHp = p.Hp = 5000;
        p.InputOverride = () => { var i = _scInput; _scInput.Infuse = 0; _scInput.Attack = _scInput.Ability = _scInput.Ability2 = _scInput.Dodge = false; return i; };
        foreach (var _ in ElWait(0.5f)) yield return null;
        ScCheck($"the Swordsman fights with elements: {string.Join(", ", Elements.Of(p.Stats.Hero))} on {Controls.Name("infuse_1")}/{Controls.Name("infuse_2")}/{Controls.Name("infuse_3")}", p.Elemental && Elements.Of(p.Stats.Hero).Length == 3);
        var foe = ElDummy(p, 26);
        foreach (var _ in ElWait(0.3f)) yield return null;
        var aim = new Vector2(p.Facing, 0);
        // (each blow from beside it: blows knock it back)
        PlayerInput Atk() { Near(); return new() { Attack = true, Aim = aim, AimGiven = true }; }
        void Near() { p.GlobalPosition = foe.GlobalPosition - new Vector2(18 * p.Facing, 0); p.Velocity = Vector2.Zero; }
        void Ready() { p.ResetAbilityCooldowns(); foe.Hp = foe.MaxHp; }

        // a plain swing: no build-up
        foreach (var _ in ElPress(Atk(), 0.6f)) yield return null;
        ScCheck($"a plain swing lands and builds nothing (hp {foe.Hp:0}, fire {foe.FireBuild:0})", foe.Hp < foe.MaxHp && foe.FireBuild < 1 && foe.FrostBuild < 1);

        // fire: Pyre Strike, twice to set it alight, twice more to combust
        int uses0 = p.ElementalUses.Count;
        foreach (var _ in ElPress(new PlayerInput { Infuse = 2 }, 0.05f)) yield return null;
        ScCheck($"fire is readied ({p.Infused})", p.Infused == HeroElement.Fire);
        ScShot("elements_hud");
        foreach (var _ in ElPress(Atk(), 0.6f)) yield return null;
        ScCheck($"the next swing is a Pyre Strike ({string.Join(", ", p.ElementalUses.Skip(uses0))}), filling fire half way ({foe.FireBuild:0}) and spending the element ({p.Infused})",
            p.ElementalUses.Skip(uses0).Contains("Pyre Strike") && Math.Abs(foe.FireBuild - Tune.ElementalSword.MediumBuild) < 1 && p.Infused == HeroElement.None);
        foreach (var _ in ElPress(new PlayerInput { Infuse = 2 }, 0.05f)) yield return null;
        foreach (var _ in ElPress(Atk(), 0.6f)) yield return null;
        ScCheck($"the element rests a moment: the next swing is plain and fire waits ({p.Infused}, rest {p.SwingElementRest:0.0} s)", p.Infused == HeroElement.Fire && !foe.Ignited);
        foreach (var _ in ElWait(Tune.ElementalSword.SwingRestFire)) yield return null;
        foreach (var _ in ElPress(Atk(), 0.6f)) yield return null;
        ScCheck($"full of fire: alight ({foe.Ignited})", foe.Ignited);
        for (int k = 0; k < 2; k++) foreach (var _ in ElStrike(p, 2, Atk)) yield return null;
        ScCheck($"full again while alight: it combusts ({foe.Combusted})", foe.Combusted >= 1);
        ScShot("elements_combust");

        // frost: Razor Ice, chilled, then frozen; ThunderCrush shatters it and stuns
        for (int k = 0; k < 4; k++)
        {
            foreach (var _ in ElStrike(p, 3, Atk)) yield return null;
            if (k == 1) ScCheck($"two Razor Ice: chilled ({foe.Chilled})", foe.Chilled);
        }
        ScCheck($"two more: frozen solid ({foe.FrozenSolid})", foe.FrozenSolid);
        Ready();
        foreach (var _ in ElPress(new PlayerInput { Infuse = 1 }, 0.05f)) yield return null;
        Near();
        foreach (var _ in ElPress(new PlayerInput { Ability2 = true, Aim = aim, AimGiven = true }, 1.4f)) yield return null;
        ScCheck($"earth's Hard Swing is ThunderCrush: it shatters the frozen ({foe.Shattered}) and its full earth stuns ({foe.Reeling})", p.ElementalUses.Contains("ThunderCrush") && foe.Shattered >= 1);
        ScCheck($"and rests a long while ({p.HardSwingCooldownFrac:0.00} of {Tune.ElementalSword.CrushRest} s left)", p.HardSwingCooldownFrac > 0.8f);

        // earth on the swing: the Heaving Swing
        foreach (var _ in ElWait(1.2f)) yield return null;
        Ready();
        float e0 = foe.EarthBuild;
        foreach (var _ in ElPress(new PlayerInput { Infuse = 1 }, 0.05f)) yield return null;
        foreach (var _ in ElPress(Atk(), 1.2f)) yield return null;
        ScCheck($"earth on the swing is the Heaving Swing ({foe.EarthBuild:0} earth)", p.ElementalUses.Contains("Heaving Swing") && foe.EarthBuild > e0);

        // Empower Hilt: frost on every blow; earth saps
        Ready();
        foreach (var _ in ElPress(new PlayerInput { Infuse = 3 }, 0.05f)) yield return null;
        foreach (var _ in ElPress(new PlayerInput { Ability = true, Aim = aim, AimGiven = true }, 0.1f)) yield return null;
        float f0 = foe.FrostBuild;
        foreach (var _ in ElPress(Atk(), 0.6f)) yield return null;
        ScCheck($"a frost hilt: plain swings carry frost ({f0:0} -> {foe.FrostBuild:0}, {p.HiltLeft:0.0} s left)", p.HiltElement == HeroElement.Frost && foe.FrostBuild > f0);
        Ready();
        foreach (var _ in ElPress(new PlayerInput { Infuse = 1 }, 0.05f)) yield return null;
        foreach (var _ in ElPress(new PlayerInput { Ability = true, Aim = aim, AimGiven = true }, 0.1f)) yield return null;
        foreach (var _ in ElPress(Atk(), 0.6f)) yield return null;
        ScCheck($"an earth hilt saps what it strikes ({foe.Sapped})", foe.Sapped);
        ScCheck("the cards the elements replace aren't dealt (Twin Charge, Swift Heave, Relentless Charge)", !Upgrades.Available(Upgrades.Get("charge2"), p.Stats) && !Upgrades.Available(Upgrades.Get("heave_swift"), p.Stats) && !Upgrades.Available(Upgrades.Get("charge_combo"), p.Stats));
        foe.QueueFree();
    }

    private IEnumerable<object> ElementalistRun()
    {
        var p = G.Player;
        foreach (var e in G.Enemies.ToArray()) e.QueueFree();
        p.Stats.MaxHp = p.Hp = 5000;
        p.Stats.AlimusMax = 9999; p.SetAlimus(9999);
        p.InputOverride = () => { var i = _scInput; _scInput.Infuse = 0; _scInput.Attack = _scInput.Ability = _scInput.Ability2 = _scInput.Dodge = false; return i; };
        foreach (var _ in ElWait(0.5f)) yield return null;
        ScCheck($"the Elementalist fights with elements: {string.Join(", ", Elements.Of(p.Stats.Hero))}", p.Elemental && Elements.Of(p.Stats.Hero).Length == 3);
        var foe = ElDummy(p, 90);
        foreach (var _ in ElWait(0.3f)) yield return null;
        var aim = new Vector2(p.Facing, 0);
        PlayerInput Bolt() => new() { Attack = true, Aim = aim, AimGiven = true };
        void Ready() { p.TestResetSpells(); p.SetAlimus(9999); foe.Hp = foe.MaxHp; }

        // fire on the staff: three Firebolts, then plain
        foreach (var _ in ElPress(new PlayerInput { Infuse = 1 }, 0.05f)) yield return null;
        ScShot("elements_hud");
        for (int k = 0; k < 2; k++) { Ready(); foreach (var _ in ElPress(Bolt(), 0.7f)) yield return null; }
        ScCheck($"two Firebolts set it alight ({foe.Ignited}; {p.MissileShots} fire bolt left)", foe.Ignited && p.MissileShots == 1 && p.ElementalUses.Count(u => u == "Firebolt") == 2);
        Ready(); foreach (var _ in ElPress(Bolt(), 0.7f)) yield return null;
        Ready(); float b0 = foe.FireBuild; foreach (var _ in ElPress(Bolt(), 0.7f)) yield return null;
        ScCheck($"after the third the bolts are plain ({p.MissileElement}; fire {b0:0} -> {foe.FireBuild:0})", p.MissileElement == HeroElement.None && foe.FireBuild <= b0 + 0.5f);

        // Detonate: alight already, it combusts
        Ready();
        int c0 = foe.Combusted;
        foreach (var _ in ElPress(new PlayerInput { Infuse = 1 }, 0.05f)) yield return null;
        if (!foe.Ignited) foe.Ignite(1f, 5f);
        foreach (var _ in ElPress(new PlayerInput { Ability2 = true, Aim = aim, AimGiven = true }, 0.4f)) yield return null;
        ScCheck($"fire's Burst is Detonate: alight already, it combusts ({c0} -> {foe.Combusted})", p.ElementalUses.Contains("Detonate") && foe.Combusted > c0);

        // frost: Shatter freezes with two bursts' worth, then breaks the ice
        Ready();
        foreach (var _ in ElPress(new PlayerInput { Infuse = 2 }, 0.05f)) yield return null;
        foreach (var _ in ElPress(new PlayerInput { Ability2 = true, Aim = aim, AimGiven = true }, 0.3f)) yield return null;
        ScCheck($"frost's Burst chills ({foe.Chilled})", foe.Chilled);
        Ready();
        foreach (var _ in ElPress(new PlayerInput { Infuse = 2 }, 0.05f)) yield return null;
        foreach (var _ in ElPress(new PlayerInput { Ability2 = true, Aim = aim, AimGiven = true }, 0.3f)) yield return null;
        ScCheck($"and again freezes solid ({foe.FrozenSolid})", foe.FrozenSolid);
        Ready();
        foreach (var _ in ElPress(new PlayerInput { Infuse = 2 }, 0.05f)) yield return null;
        foreach (var _ in ElPress(new PlayerInput { Ability2 = true, Aim = aim, AimGiven = true }, 0.3f)) yield return null;
        ScCheck($"and on the frozen it shatters ({foe.Shattered})", foe.Shattered >= 1);

        // wind: a Gust bolt shoves it (a creature free to move)
        foe.QueueFree();
        foe = ElDummy(p, 90, hold: false);
        foreach (var _ in ElWait(0.2f)) yield return null;
        Ready();
        float x0 = foe.GlobalPosition.X;
        foreach (var _ in ElPress(new PlayerInput { Infuse = 3 }, 0.05f)) yield return null;
        foreach (var _ in ElPress(Bolt(), 0.8f)) yield return null;
        ScCheck($"a Gust bolt shoves a light creature away ({Math.Abs(foe.GlobalPosition.X - x0):0} px)", p.ElementalUses.Contains("Gust") && Math.Abs(foe.GlobalPosition.X - x0) > 8f);

        // storms by element
        Ready();
        foreach (var _ in ElPress(new PlayerInput { Infuse = 3 }, 0.05f)) yield return null;
        foreach (var _ in ElPress(new PlayerInput { Ability = true, Aim = aim, AimGiven = true }, 0.9f)) yield return null;
        ScCheck($"wind's Extended is a Cyclone ({p.LastBlizzard?.El}, pull {p.LastBlizzard?.Pull})", p.ElementalUses.Contains("Cyclone") && p.LastBlizzard != null && p.LastBlizzard.El == HeroElement.Wind && p.LastBlizzard.Pull > 0);
        ScShot("elements_cyclone");
        foreach (var _ in ElWait(1.5f)) yield return null;

        // Aid: Narrow Draft, Kindling, Frost Armor
        Ready();
        float sp0 = p.Stats.MoveSpeed;
        foreach (var _ in ElPress(new PlayerInput { Infuse = 3 }, 0.05f)) yield return null;
        foreach (var _ in ElPress(new PlayerInput { Dodge = true }, 0.1f)) yield return null;
        ScCheck($"wind's Aid is Narrow Draft: faster ({sp0:0.00} -> {p.Stats.MoveSpeed:0.00})", p.HasBuff(Player.BuffTailwind) && p.Stats.MoveSpeed > sp0);
        Ready();
        foreach (var _ in ElPress(new PlayerInput { Infuse = 1 }, 0.05f)) yield return null;
        foreach (var _ in ElPress(new PlayerInput { Dodge = true }, 0.1f)) yield return null;
        float k0 = foe.FireBuild;
        Ready(); foreach (var _ in ElPress(Bolt(), 0.7f)) yield return null;
        ScCheck($"fire's Aid is Kindling: a plain bolt adds fire ({k0:0} -> {foe.FireBuild:0})", p.HasBuff(Player.BuffKindling) && (foe.FireBuild > k0 || foe.Ignited));
        Ready();
        foreach (var _ in ElPress(new PlayerInput { Infuse = 2 }, 0.05f)) yield return null;
        foreach (var _ in ElPress(new PlayerInput { Dodge = true }, 0.1f)) yield return null;
        ScCheck("frost's Aid is Frost Armor", p.HasBuff(Player.BuffFrostArmor));
        ScCheck("the cards the elements replace aren't dealt (Frostbolt, Firestorm, Cinder Snap, Kindling, Deep Chill)",
            new[] { "bolt_frost", "blizzard_fire", "snap_cinder", "kindling", "deepchill" }.All(id => !Upgrades.Available(Upgrades.Get(id), p.Stats)));
        foe.QueueFree();
    }
}
