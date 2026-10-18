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
        foreach (var _ in SwordsmanDualsRun()) yield return null;
    }

    /// <summary>Two charges readied (oldest first; past two, the oldest goes): the pair's ability, as the Elementalist's Burst or the Swordsman's Hard Swing will cast it.</summary>
    private IEnumerable<object> ElPair(Player p, int a, int b)
    {
        foreach (var _ in ElWait(0.1f)) yield return null;
        foreach (var _ in ElPress(new PlayerInput { Infuse = a }, 0.02f)) yield return null;
        foreach (var _ in ElPress(new PlayerInput { Infuse = b }, 0.02f)) yield return null;
    }

    /// <summary>`--scenario=elements --hero=swordsman`, the dual charges: each pair's Hard Swing does its own thing where the blade lands.</summary>
    private IEnumerable<object> SwordsmanDualsRun()
    {
        var p = G.Player;
        var aim = new Vector2(p.Facing, 0);
        var foes = new List<Enemy>();
        Enemy Fresh(float dx, bool hold = true) { foreach (var f in foes) if (IsInstanceValid(f)) f.QueueFree(); foes.Clear(); var e = ElDummy(p, dx, hold); foes.Add(e); return e; }
        PlayerInput Hard() => new() { Ability2 = true, Aim = aim, AimGiven = true };
        float reach = Tune.Swordsman.Reach * Tune.Swordsman.HeaveReach;
        foreach (var _ in ElWait(0.6f)) yield return null;

        // the queue: two charges wait; past two, the oldest goes
        p.ResetAbilityCooldowns();
        foreach (var _ in ElPair(p, 3, 3)) yield return null;
        ScCheck($"frost twice readies two charges: {p.DualReadyName} ({string.Join("+", p.Charges)})", p.Charges.Count == 2 && p.DualReadyName == "Glacier Cleaver");
        foreach (var _ in ElPress(new PlayerInput { Infuse = 1 }, 0.02f)) yield return null;
        ScCheck($"a third press drops the oldest: frost and earth make {p.DualReadyName}", p.Charges.Count == 2 && p.DualReadyName == "Frost Heave");
        ScShot("duals_hud");

        // Glacier Cleaver: the vast axe freezes what it cuts, and the head shatters it as it lands
        var foe = Fresh(reach * 1.15f);
        foreach (var _ in ElWait(0.2f)) yield return null;
        p.ResetAbilityCooldowns();
        foreach (var _ in ElPair(p, 3, 3)) yield return null;
        _scInput = Hard(); yield return null; yield return null; _scInput = default;
        foreach (var _ in ElWait(0.25f)) yield return null;
        ScCheck($"Glacier Cleaver: the blade becomes a vast axe of ice ({p.GiantBlade})", p.ElementalUses.Contains("Glacier Cleaver") && p.GiantBlade);
        ScShot("duals_glacier");
        foreach (var _ in ElWait(1.4f)) yield return null;
        float lost = foe.MaxHp - foe.Hp;
        ScCheck($"its blow reaches {reach * 1.15f:0} px, freezes and shatters ({foe.Shattered}) for a great deal ({lost:0})", foe.Shattered >= 1 && lost > Tune.Swordsman.Damage * 6f);
        ScCheck($"and rests long ({p.HardSwingCooldownFrac:0.00} of {Tune.Dual.AxeRest} s)", p.HardSwingCooldownFrac > 0.8f);

        // Frost Heave: ice bursts up under the nearest creature, out of the blade's reach
        foe = Fresh(reach * 2.4f);
        foreach (var _ in ElWait(0.2f)) yield return null;
        p.ResetAbilityCooldowns();
        foreach (var _ in ElPair(p, 1, 3)) yield return null;
        foreach (var _ in ElPress(Hard(), 1.4f)) yield return null;
        lost = foe.MaxHp - foe.Hp;
        ScCheck($"Frost Heave strikes up under a creature {reach * 2.4f:0} px off ({lost:0} damage, chilled {foe.Chilled})", p.ElementalUses.Contains("Frost Heave") && lost > Tune.Swordsman.Damage * 2f && foe.Chilled);

        // Earthsplitter: a fissure runs along the floor and stuns what it passes under
        foe = Fresh(reach * 2.2f);
        foreach (var _ in ElWait(0.2f)) yield return null;
        p.ResetAbilityCooldowns();
        foreach (var _ in ElPair(p, 1, 1)) yield return null;
        foreach (var _ in ElPress(Hard(), 0.9f)) yield return null;
        lost = foe.MaxHp - foe.Hp;
        ScCheck($"Earthsplitter's fissure reaches a creature {reach * 2.2f:0} px off and stuns it ({lost:0} damage, reeling {foe.Reeling})", p.ElementalUses.Contains("Earthsplitter") && lost > Tune.Swordsman.Damage && foe.Reeling);
        foreach (var _ in ElWait(0.6f)) yield return null;

        // Inferno Cleave: a wall of fire that burns creatures and the shots flying into it
        foe = Fresh(reach * 0.75f);
        foreach (var _ in ElWait(0.2f)) yield return null;
        p.ResetAbilityCooldowns();
        foreach (var _ in ElPair(p, 2, 2)) yield return null;
        foreach (var _ in ElPress(Hard(), 0.9f)) yield return null;
        var wall = p.LastDualSpell as FireWall;
        if (wall != null)
        {
            var shot = new EnemyProjectile { Position = wall.GlobalPosition + new Vector2(p.Facing * 90f, -30f), Vel = new Vector2(-p.Facing * 300f, 0), Damage = 1f };
            _world.AddChild(shot);
        }
        foreach (var _ in ElWait(0.8f)) yield return null;
        ScCheck($"Inferno Cleave leaves a wall of fire: the creature in it alight ({foe.Ignited}), a shot into it burnt up ({wall?.Burned})", p.ElementalUses.Contains("Inferno Cleave") && wall != null && foe.Ignited && wall.Burned >= 1);
        ScShot("duals_inferno");
        foreach (var _ in ElWait(4f)) yield return null;

        // Magma Rend: a pool of magma that mires and burns
        foe = Fresh(reach * 0.75f);
        foreach (var _ in ElWait(0.2f)) yield return null;
        p.ResetAbilityCooldowns();
        foreach (var _ in ElPair(p, 2, 1)) yield return null;
        foreach (var _ in ElPress(Hard(), 1.4f)) yield return null;
        ScCheck($"Magma Rend: a pool of magma mires ({foe.Mired}) and burns ({foe.FireBuild:0} fire, alight {foe.Ignited})", p.ElementalUses.Contains("Magma Rend") && foe.Mired && (foe.FireBuild > 0 || foe.Ignited));
        ScShot("duals_magma");
        foreach (var _ in ElWait(5f)) yield return null;

        // Thermal Shock: on a creature already burning, a far harder blow and a cloud of steam
        foe = Fresh(reach * 0.6f);
        foreach (var _ in ElWait(0.2f)) yield return null;
        foe.Ignite(1f, 5f);
        p.ResetAbilityCooldowns();
        int clouds = SmokeCloud.All.Count;
        foreach (var _ in ElPair(p, 3, 2)) yield return null;
        foreach (var _ in ElPress(Hard(), 1.2f)) yield return null;
        lost = foe.MaxHp - foe.Hp;
        ScCheck($"Thermal Shock on a burning creature: a far harder blow ({lost:0}), its fire out ({!foe.Ignited}) and a cloud of steam ({SmokeCloud.All.Count - clouds})",
            p.ElementalUses.Contains("Thermal Shock!") && lost > Tune.Swordsman.Damage * Tune.Dual.ShockDamage * 1.8f && SmokeCloud.All.Count > clouds);
        foreach (var f in foes) if (IsInstanceValid(f)) f.QueueFree();
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
        foreach (var _ in ElementalistDualsRun()) yield return null;
    }

    /// <summary>`--scenario=elements --hero=elementalist`, the dual charges: each pair's Burst is its own spell.</summary>
    private IEnumerable<object> ElementalistDualsRun()
    {
        var p = G.Player;
        var aim = new Vector2(p.Facing, 0);
        var foes = new List<Enemy>();
        void Clear() { foreach (var f in foes) if (IsInstanceValid(f)) f.QueueFree(); foes.Clear(); }
        Enemy Add(float dx, bool hold = true) { var e = ElDummy(p, dx, hold); foes.Add(e); return e; }
        void Ready() { p.TestResetSpells(); p.SetAlimus(9999); }
        PlayerInput Burst() => new() { Ability2 = true, Aim = aim, AimGiven = true };
        foreach (var _ in ElWait(0.6f)) yield return null;

        // Meteor: falls on the knot of creatures, through a platform in its way
        Clear();
        var a = Add(130); var b = Add(160);
        foreach (var _ in ElWait(0.2f)) yield return null;
        var from = new Vector2(-p.Facing * 0.45f, -1f).Normalized();
        var ledge = new RockLedge { Position = (a.GlobalPosition + b.GlobalPosition) * 0.5f + from * 70f, Half = 44f };
        _world.AddChild(ledge);
        Ready();
        foreach (var _ in ElPair(p, 1, 1)) yield return null;
        ScCheck($"fire twice readies {p.DualReadyName}", p.DualReadyName == "Meteor");
        foreach (var _ in ElPress(Burst(), 0.15f)) yield return null;
        ScShot("duals_meteor");
        foreach (var _ in ElWait(1.6f)) yield return null;
        ScCheck($"a Meteor falls on the two creatures: both struck and alight ({a.Ignited}, {b.Ignited}; {a.MaxHp - a.Hp:0}, {b.MaxHp - b.Hp:0} damage)", p.ElementalUses.Contains("Meteor") && a.Ignited && b.Ignited && a.Hp < a.MaxHp && b.Hp < b.MaxHp);
        ScCheck($"and smashes the platform it passes through ({ledge.Broken})", ledge.Broken);
        ledge.QueueFree();

        // Frozen Orb: a spiral of shards, then a burst
        Clear();
        a = Add(110);
        foreach (var _ in ElWait(0.2f)) yield return null;
        Ready();
        foreach (var _ in ElPair(p, 2, 2)) yield return null;
        foreach (var _ in ElPress(Burst(), 0.6f)) yield return null;
        var orb = p.LastDualSpell as FrozenOrb;
        ScCheck($"Frozen Orb drifts out loosing shards ({orb?.Shards})", p.ElementalUses.Contains("Frozen Orb") && orb != null && orb.Shards > 3);
        ScShot("duals_orb");
        foreach (var _ in ElWait(3f)) yield return null;
        ScCheck($"its shards and burst fill the creature with frost (chilled {a.Chilled}, frozen {a.FrozenSolid}, shattered {a.Shattered})", a.Chilled || a.FrozenSolid || a.Shattered > 0);

        // Tornado: carries a creature along
        Clear();
        a = Add(70, hold: false);
        foreach (var _ in ElWait(0.3f)) yield return null;
        Ready();
        float x0 = a.GlobalPosition.X;
        foreach (var _ in ElPair(p, 3, 3)) yield return null;
        foreach (var _ in ElPress(Burst(), 1.4f)) yield return null;
        var tw = p.LastDualSpell as Tornado;
        ScCheck($"a Tornado takes up the creature ({tw?.Caught}) and carries it along ({Math.Abs(a.GlobalPosition.X - x0):0} px)", p.ElementalUses.Contains("Tornado") && tw != null && tw.Caught >= 1 && Math.Abs(a.GlobalPosition.X - x0) > 20f);
        ScShot("duals_tornado");
        foreach (var _ in ElWait(3f)) yield return null;

        // Frostfire Lance: one beam through a line; the burning one takes more
        Clear();
        a = Add(110); b = Add(190);
        foreach (var _ in ElWait(0.2f)) yield return null;
        b.Ignite(0.01f, 5f);
        Ready();
        foreach (var _ in ElPair(p, 2, 1)) yield return null;
        foreach (var _ in ElPress(Burst(), 0.3f)) yield return null;
        float la = a.MaxHp - a.Hp, lb = b.MaxHp - b.Hp;
        ScCheck($"Frostfire Lance strikes both in its line ({la:0}, {lb:0}), the burning one harder", p.ElementalUses.Contains("Frostfire Lance") && la > 0 && lb > la * 1.3f);

        // Wildfire: the wind carries fire from one to the next
        Clear();
        a = Add(100); b = Add(150);
        foreach (var _ in ElWait(0.2f)) yield return null;
        Ready();
        foreach (var _ in ElPair(p, 1, 3)) yield return null;
        foreach (var _ in ElPress(Burst(), 2.2f)) yield return null;
        ScCheck($"Wildfire lights one and carries it to the other ({a.Ignited}, {b.Ignited})", p.ElementalUses.Contains("Wildfire") && a.Ignited && b.Ignited);
        foreach (var _ in ElWait(3f)) yield return null;

        // Whiteout: a gale that drives back, fills with frost and turns shots round
        Clear();
        a = Add(80, hold: false);
        foreach (var _ in ElWait(0.3f)) yield return null;
        Ready();
        x0 = a.GlobalPosition.X;
        float d0 = Math.Abs(x0 - p.GlobalPosition.X);
        foreach (var _ in ElPair(p, 3, 2)) yield return null;
        foreach (var _ in ElPress(Burst(), 0.2f)) yield return null;
        var gale = p.LastDualSpell as Whiteout;
        var shot = new EnemyProjectile { Position = p.CastPoint + aim * 120f + new Vector2(0, -20), Vel = -aim * 200f, Damage = 1f };
        _world.AddChild(shot);
        foreach (var _ in ElWait(1.2f)) yield return null;
        float d1 = Math.Abs(a.GlobalPosition.X - p.GlobalPosition.X);
        ScCheck($"Whiteout drives the creature back ({d0:0} -> {d1:0} px), fills it with frost ({a.FrostBuild:0}, chilled {a.Chilled}) and turns a shot round ({gale?.Turned})",
            p.ElementalUses.Contains("Whiteout") && d1 > d0 + 10f && (a.FrostBuild > 0 || a.Chilled) && gale != null && gale.Turned >= 1);
        Clear();
    }
}
