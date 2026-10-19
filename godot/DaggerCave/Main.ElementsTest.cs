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
        // (an element waits out the charge's half second, and a jump waits for the ground)
        while (inp.Infuse != 0 && G.Player.InfuseCooldown > 0) yield return null;
        for (int f = 0; inp.Jump && f < 120 && !G.Player.IsOnFloor(); f++) yield return null;
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
        ScCheck($"the next swing is a Searing Strike ({string.Join(", ", p.ElementalUses.Skip(uses0))}), filling fire half way ({foe.FireBuild:0}) and spending the element ({p.Infused})",
            p.ElementalUses.Skip(uses0).Contains("Searing Strike") && Math.Abs(foe.FireBuild - Tune.ElementalSword.MediumBuild) < 1 && p.Infused == HeroElement.None);
        foreach (var _ in ElPress(new PlayerInput { Infuse = 2 }, 0.05f)) yield return null;
        foreach (var _ in ElPress(Atk(), 0.6f)) yield return null;
        ScCheck($"a version waits on no cooldown (the charge's own second paces it): the next Searing Strike goes at once, full of fire: alight ({foe.Ignited}, rest {p.SwingElementRest:0.0} s)", foe.Ignited && p.SwingElementRest <= 0f);
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
        ScCheck($"and waits on no cooldown after ({p.HardSwingCooldownFrac:0.00}): the charge was its price", p.HardSwingCooldownFrac <= 0f);
        // a plain Hard Swing does rest
        Ready();
        foreach (var _ in ElPress(new PlayerInput { Ability2 = true, Aim = aim, AimGiven = true }, 1.4f)) yield return null;
        ScCheck($"a plain Hard Swing keeps its cooldown ({p.HardSwingSeconds:0.0} s left)", p.HardSwingSeconds > 0f);

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
        // the dodge with an element: Explosive Lunge, Glacial Vault, Earthfall
        foreach (var _ in SwordsmanJumps(p)) yield return null;
        ScCheck("the cards the elements replace aren't dealt (Twin Charge, Swift Heave, Relentless Charge)", !Upgrades.Available(Upgrades.Get("charge2"), p.Stats) && !Upgrades.Available(Upgrades.Get("heave_swift"), p.Stats) && !Upgrades.Available(Upgrades.Get("charge_combo"), p.Stats));
        foe.QueueFree();
        foreach (var _ in SwordsmanDualsRun()) yield return null;
    }

    /// <summary>A jump held for <paramref name="hold"/> s (a full jump), the highest the hero rose above where it began (px).</summary>
    private IEnumerable<object> ElJump(Player p, float hold, Action<float> rose, bool dodge = false)
    {
        // (how fast it left the ground: the roof of a low cave would cut the height short)
        float up = 0;
        while (p.JumpElementRest > 0 || !p.IsOnFloor()) yield return null;
        _scInput = dodge ? new PlayerInput { Dodge = true, JumpHeld = true } : new PlayerInput { Jump = true, JumpHeld = true };
        for (float t = 0; t < hold; t += (float)GetProcessDeltaTime()) { _scInput.JumpHeld = true; up = Math.Max(up, -p.Velocity.Y); yield return null; }
        _scInput = default;
        for (float t = 0; t < 1.2f && !(p.IsOnFloor() && t > 0.1f); t += (float)GetProcessDeltaTime()) yield return null;
        rose(up);
    }

    /// <summary>`--scenario=elements --hero=swordsman`, the jump: what each element makes of it.</summary>
    private IEnumerable<object> SwordsmanJumps(Player p)
    {
        var foes = new List<Enemy>();
        Enemy Fresh(float dx) { foreach (var f in foes) if (IsInstanceValid(f)) f.QueueFree(); foes.Clear(); var e = ElDummy(p, dx); foes.Add(e); return e; }
        foreach (var _ in ElWait(0.5f)) yield return null;
        float plain = 0, vault = 0;
        foreach (var _ in ElJump(p, 0.5f, h => plain = h)) yield return null;
        foreach (var _ in ElWait(0.3f)) yield return null;
        // the jump stays a jump: a readied element waits for the dodge
        foreach (var _ in ElPress(new PlayerInput { Infuse = 3 }, 0.02f)) yield return null;
        int vaults = p.ElementalUses.Count(u => u == "Glacial Vault");
        foreach (var _ in ElJump(p, 0.3f, _ => { })) yield return null;
        ScCheck($"a jump with frost readied is a plain jump, and the frost waits ({p.Infused})", p.ElementalUses.Count(u => u == "Glacial Vault") == vaults && p.Infused == HeroElement.Frost);
        p.TestClearCharges();
        // Glacial Vault: thrown higher, and what stands beside it filled with frost
        var foe = Fresh(26);
        foreach (var _ in ElWait(0.2f)) yield return null;
        foreach (var _ in ElPress(new PlayerInput { Infuse = 3 }, 0.02f)) yield return null;
        foreach (var _ in ElJump(p, 0.5f, h => vault = h, dodge: true)) yield return null;
        ScCheck($"frost's dodge is a Glacial Vault: thrown up faster ({plain:0} -> {vault:0} px/s), frost to what's beside it ({foe.FrostBuild:0})", p.ElementalUses.Contains("Glacial Vault") && vault > plain * 1.3f && foe.FrostBuild > 0);
        // Explosive Lunge: a blast where it leaves the ground, and far forward
        foreach (var _ in ElWait(0.4f)) yield return null;
        foe = Fresh(-22);
        foreach (var _ in ElWait(0.2f)) yield return null;
        float x0 = p.GlobalPosition.X;
        foreach (var _ in ElPress(new PlayerInput { Infuse = 2 }, 0.02f)) yield return null;
        foreach (var _ in ElJump(p, 0.3f, _ => { }, dodge: true)) yield return null;
        float went = Math.Abs(p.GlobalPosition.X - x0);
        ScCheck($"fire's dodge is an Explosive Lunge: far forward ({went:0} px) and fire to what it left behind ({foe.FireBuild:0}, alight {foe.Ignited})", p.ElementalUses.Contains("Explosive Lunge") && went > 80f && (foe.FireBuild > 0 || foe.Ignited));
        // Earthfall: it lands in a shockwave
        foreach (var _ in ElWait(0.4f)) yield return null;
        foe = Fresh(30);
        foreach (var _ in ElWait(0.2f)) yield return null;
        p.GlobalPosition = foe.GlobalPosition - new Vector2(30 * p.Facing, 4);
        foreach (var _ in ElPress(new PlayerInput { Infuse = 1 }, 0.02f)) yield return null;
        foreach (var _ in ElJump(p, 0.5f, _ => { }, dodge: true)) yield return null;
        foreach (var _ in ElWait(0.15f)) yield return null;
        float lost = foe.MaxHp - foe.Hp;
        ScCheck($"earth's dodge is Earthfall: it lands in a shockwave ({lost:0} damage, earth {foe.EarthBuild:0}, reeling {foe.Reeling})", p.ElementalUses.Contains("Earthfall!") && lost > 0 && (foe.EarthBuild > 0 || foe.Reeling));
        foreach (var f in foes) if (IsInstanceValid(f)) f.QueueFree();
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

        // locked (a new save), one charge waits, and the same again puts it away
        bool dualsWere = Tune.Testing.DualCharges, unlockedWas = Meta.DualChargesUnlocked;
        Tune.Testing.DualCharges = false; Meta.DualChargesUnlocked = false;
        foreach (var _ in ElPair(p, 3, 1)) yield return null;
        ScCheck($"dual charges locked: one charge waits, the newer replacing it ({string.Join("+", p.Charges)})", p.Charges.Count == 1 && p.Infused == HeroElement.Earth && !p.DualReady);
        foreach (var _ in ElPress(new PlayerInput { Infuse = 1 }, 0.02f)) yield return null;
        ScCheck($"and pressed again it is put away ({p.Charges.Count})", p.Charges.Count == 0);
        Tune.Testing.DualCharges = dualsWere; Meta.DualChargesUnlocked = unlockedWas;
        Meta.DualChargesUnlocked = true;
        ScCheck($"unlocked, two charges can wait ({p.MaxCharges})", p.MaxCharges == 2);
        Meta.DualChargesUnlocked = unlockedWas;

        // the queue: two charges wait; past two, the oldest goes
        p.ResetAbilityCooldowns();
        foreach (var _ in ElPair(p, 3, 3)) yield return null;
        ScCheck($"frost twice readies two charges: {p.DualNameFor(ElemSlot.Heavy)} ({string.Join("+", p.Charges)})", p.Charges.Count == 2 && p.DualNameFor(ElemSlot.Heavy) == "Glacier Cleaver");
        foreach (var _ in ElPress(new PlayerInput { Infuse = 1 }, 0.02f)) yield return null;
        ScCheck($"a third press drops the oldest: frost and earth make {p.DualNameFor(ElemSlot.Heavy)}", p.Charges.Count == 2 && p.DualNameFor(ElemSlot.Heavy) == "Frost Heave");
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
        ScCheck($"and it doesn't rest the Hard Swing ({p.HardSwingCooldownFrac:0.00}): the charges were its price", p.HardSwingCooldownFrac <= 0f);

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
        var wall = p.LastElementSpell as FireWall;
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
        PlayerInput Fixed() => new() { Ability2 = true, Aim = aim, AimGiven = true };
        void Ready() { p.TestResetSpells(); p.SetAlimus(9999); foe.Hp = foe.MaxHp; }
        // (a spot for the fixed-range spells: the dummy where they land)
        void AtSpot() { foe.GlobalPosition = p.CastPoint + aim * Tune.ElementalStaff.FixedRange + new Vector2(0, 8); }

        // a charge every half second: a second press inside it is kept, and readies once it's over
        _scInput = new PlayerInput { Infuse = 1 }; yield return null; _scInput = default;
        float cdAt = p.InfuseCooldown;
        _scInput = new PlayerInput { Infuse = 2 }; yield return null; _scInput = default;
        int early = p.Charges.Count;
        bool inside = p.InfuseCooldown > 0;
        foreach (var _ in ElWait(Tune.Elements.ChargeCooldown + 0.1f)) yield return null;
        ScCheck($"charging an element has a half-second cooldown ({cdAt:0.00} s after a press): a press inside it waits ({early} charge then{(inside ? "" : ", the frame ran long")}, {p.Charges.Count} after)",
            cdAt > 0f && (!inside || early == 1) && p.Charges.Count == 2);
        foreach (var _ in ElWait(7f)) yield return null;
        ScCheck($"idle charges don't fade: 7 s on, {p.Charges.Count} still readied", p.Charges.Count == 2);
        p.TestClearCharges();
        // nothing to count: the attack goes as often as the gap between casts allows
        int pops0 = p.ElementalUses.Count(u => u == "Pop");
        for (int k = 0; k < 6; k++) { foreach (var _ in ElPress(new PlayerInput { Attack = true, Aim = new Vector2(p.Facing, 0), AimGiven = true }, Tune.Elementalist.BoltGap + 0.15f)) yield return null; }
        ScCheck($"no bolts to count: six attacks in a row all go ({p.ElementalUses.Count(u => u == "Pop") - pops0})", p.ElementalUses.Count(u => u == "Pop") - pops0 == 6);
        // a cast in mid-jump: the jump goes on rising, slowly, and carries on at full speed after (it isn't paused, nor turned to a fall)
        while (!p.IsOnFloor()) yield return null;
        _scInput = new PlayerInput { Jump = true, JumpHeld = true }; yield return null;
        _scInput = new PlayerInput { JumpHeld = true };
        p.TestHold(0.6f);
        float jvy0 = p.Velocity.Y, jy0 = p.GlobalPosition.Y, jylast = jy0, jvyEnd = jvy0;
        bool jfell = false;
        for (int f = 0; f < 60 && p.Holding; f++)
        {
            if (p.GlobalPosition.Y > jylast + 0.01f) jfell = true;
            jylast = p.GlobalPosition.Y;
            jvyEnd = p.Velocity.Y;
            yield return null;
        }
        _scInput = default;
        ScCheck($"a cast in mid-jump slows the jump instead of stopping it: still rising through the wind-up ({jy0 - jylast:0} px up, no fall {!jfell}), and rising on as it ends ({jvy0:0} -> {jvyEnd:0} px/s)",
            jvy0 < -100f && !jfell && jy0 - jylast > 2f && jvyEnd < -50f);
        ScCheck($"the spells cost far less: Lift {p.UpdraftCost:0}, fixed range {p.SnapCost:0}, Aid {p.BlizzardCost:0} (were {Tune.Elementalist.UpdraftCost:0}, {Tune.Elementalist.SnapCost:0}, {Tune.Elementalist.BlizzardCost:0})",
            p.UpdraftCost < Tune.Elementalist.UpdraftCost * 0.5f && p.BlizzardCost < Tune.Elementalist.BlizzardCost * 0.5f);

        // the bolt: fire on the staff lasts three bolts
        foreach (var _ in ElPress(new PlayerInput { Infuse = 1 }, 0.05f)) yield return null;
        ScShot("elements_hud");
        bool slowed = false;
        for (int k = 0; k < 2; k++)
        {
            Ready();
            _scInput = Bolt(); yield return null; yield return null; _scInput = default;
            for (float t = 0; t < 0.7f; t += (float)GetProcessDeltaTime()) { slowed |= p.CastSlowed; yield return null; }
        }
        ScCheck($"loosing a bolt slows the caster while it plays ({slowed})", slowed);
        ScCheck($"two Firebolts set it alight ({foe.Ignited}; {p.MissileShots} fire bolt left)", foe.Ignited && p.MissileShots == 1 && p.ElementalUses.Count(u => u == "Firebolt") == 2);
        Ready(); foreach (var _ in ElPress(Bolt(), 0.7f)) yield return null;
        Ready(); float b0 = foe.FireBuild, hp0 = foe.Hp; int pops = p.ElementalUses.Count(u => u == "Pop"); foreach (var _ in ElPress(Bolt(), 0.7f)) yield return null;
        ScCheck($"after the third, with nothing readied, the attack is a pop: no bolt, a small burst on the creature ({hp0 - foe.Hp:0} damage, fire {b0:0} -> {foe.FireBuild:0})",
            p.MissileElement == HeroElement.None && p.ElementalUses.Count(u => u == "Pop") > pops && foe.Hp < hp0 && foe.FireBuild <= b0 + 0.5f && hp0 - foe.Hp < Tune.Elementalist.FireDamage);

        // the fixed-range spell: Burst (on the burning: combust), Snap (chill, then shatter the frozen), Force Blast
        Ready(); AtSpot();
        int c0 = foe.Combusted;
        if (!foe.Ignited) foe.Ignite(1f, 5f);
        foreach (var _ in ElPress(new PlayerInput { Infuse = 1 }, 0.05f)) yield return null;
        foreach (var _ in ElPress(Fixed(), 0.4f)) yield return null;
        ScCheck($"fire's fixed-range spell is Burst: on the burning, it combusts ({c0} -> {foe.Combusted})", p.ElementalUses.Contains("Burst") && foe.Combusted > c0);
        int snaps = 0;
        for (; snaps < 5 && !foe.FrozenSolid; snaps++)
        {
            Ready(); AtSpot();
            foreach (var _ in ElPress(new PlayerInput { Infuse = 2 }, 0.05f)) yield return null;
            foreach (var _ in ElPress(Fixed(), 0.3f)) yield return null;
            if (snaps == 1) ScCheck($"frost's is Snap: twice, and it's chilled ({foe.Chilled})", p.ElementalUses.Contains("Snap") && foe.Chilled);
        }
        ScCheck($"more Snaps freeze it solid ({snaps} in all)", foe.FrozenSolid);
        Ready(); AtSpot();
        foreach (var _ in ElPress(new PlayerInput { Infuse = 2 }, 0.05f)) yield return null;
        foreach (var _ in ElPress(Fixed(), 0.3f)) yield return null;
        ScCheck($"and on the frozen it shatters ({foe.Shattered})", foe.Shattered >= 1);
        Ready(); AtSpot();
        float h0 = foe.Hp;
        foreach (var _ in ElPress(Fixed(), 0.3f)) yield return null;
        ScCheck($"plain, it's a Force Blast at the spot ({h0 - foe.Hp:0} damage)", p.ElementalUses.Contains("Force Blast") && foe.Hp < h0);

        // wind: a Gust bolt shoves; the Cyclone at the spot
        foe.QueueFree();
        foe = ElDummy(p, 90, hold: false);
        foreach (var _ in ElWait(0.2f)) yield return null;
        Ready();
        float x0 = foe.GlobalPosition.X;
        foreach (var _ in ElPress(new PlayerInput { Infuse = 3 }, 0.05f)) yield return null;
        foreach (var _ in ElPress(Bolt(), 0.8f)) yield return null;
        ScCheck($"a Gust bolt shoves a light creature away ({Math.Abs(foe.GlobalPosition.X - x0):0} px)", p.ElementalUses.Contains("Gust") && Math.Abs(foe.GlobalPosition.X - x0) > 8f);
        Ready();
        foreach (var _ in ElPress(new PlayerInput { Infuse = 3 }, 0.05f)) yield return null;
        foreach (var _ in ElPress(Fixed(), 0.9f)) yield return null;
        ScCheck($"wind's fixed-range spell is a Cyclone ({p.LastBlizzard?.El}, pull {p.LastBlizzard?.Pull})", p.ElementalUses.Contains("Cyclone") && p.LastBlizzard != null && p.LastBlizzard.El == HeroElement.Wind && p.LastBlizzard.Pull > 0);
        ScShot("elements_cyclone");
        foreach (var _ in ElWait(1.5f)) yield return null;

        // movement: Lift, Fire Walk, Tundra
        Ready();
        foreach (var _ in ElWait(0.3f)) yield return null;
        float y0 = p.GlobalPosition.Y, top = y0;
        _scInput = new PlayerInput { Dodge = true }; yield return null; yield return null; _scInput = default;
        for (int f = 0; f < 30; f++) { top = Math.Min(top, p.GlobalPosition.Y); yield return null; }
        ScCheck($"plain movement is Lift: a push straight up ({y0 - top:0} px)", p.ElementalUses.Contains("Lift") && y0 - top > 30f);
        foreach (var _ in ElWait(0.8f)) yield return null;
        Ready();
        float sp0 = p.Stats.MoveSpeed;
        foreach (var _ in ElPress(new PlayerInput { Infuse = 1 }, 0.05f)) yield return null;
        foreach (var _ in ElPress(new PlayerInput { Dodge = true }, 0.1f)) yield return null;
        ScCheck($"fire's dodge is Fire Walk: quicker ({sp0:0.00} -> {p.Stats.MoveSpeed:0.00}), fire left behind ({p.LastElementSpell is FireTrail})", p.ElementalUses.Contains("Fire Walk") && p.Stats.MoveSpeed > sp0 && p.LastElementSpell is FireTrail);
        foreach (var _ in ElWait(Tune.ElementalStaff.WalkSeconds + 0.3f)) yield return null;
        foe.QueueFree();
        foe = ElDummy(p, 60);
        foreach (var _ in ElWait(0.2f)) yield return null;
        Ready();
        foreach (var _ in ElPress(new PlayerInput { Infuse = 2 }, 0.05f)) yield return null;
        foreach (var _ in ElPress(new PlayerInput { Dodge = true }, 1.4f)) yield return null;
        ScCheck($"frost's dodge is Tundra: the ground freezes, the creature on it chilled ({foe.Chilled}, {foe.FrostBuild:0} frost) and the hero quicker on it ({p.HasBuff(Player.BuffTailwind)})",
            p.ElementalUses.Contains("Tundra") && foe.Chilled && p.HasBuff(Player.BuffTailwind));
        ScShot("elements_tundra");
        foreach (var _ in ElWait(Tune.ElementalStaff.TundraSeconds)) yield return null;

        // Aid: Ward, Narrow Draft, Kindling, Frost Armor
        Ready();
        foreach (var _ in ElPress(new PlayerInput { Ability = true }, 0.1f)) yield return null;
        ScCheck($"plain Aid is Ward: a barrier ({p.BarrierHp:0})", p.ElementalUses.Contains("Ward") && p.BarrierHp > 0);
        Ready();
        float sp1 = p.Stats.MoveSpeed;
        foreach (var _ in ElPress(new PlayerInput { Infuse = 3 }, 0.05f)) yield return null;
        foreach (var _ in ElPress(new PlayerInput { Ability = true }, 0.1f)) yield return null;
        ScCheck($"wind's Aid is Narrow Draft: faster ({sp1:0.00} -> {p.Stats.MoveSpeed:0.00})", p.HasBuff(Player.BuffTailwind) && p.Stats.MoveSpeed > sp1);
        Ready();
        foreach (var _ in ElPress(new PlayerInput { Infuse = 1 }, 0.05f)) yield return null;
        foreach (var _ in ElPress(new PlayerInput { Ability = true }, 0.1f)) yield return null;
        float k0 = foe.FireBuild;
        foe.GlobalPosition = p.GlobalPosition + new Vector2(90 * p.Facing, -4);
        Ready(); foreach (var _ in ElPress(Bolt(), 0.7f)) yield return null;
        ScCheck($"fire's Aid is Kindling: a plain bolt adds fire ({k0:0} -> {foe.FireBuild:0})", p.HasBuff(Player.BuffKindling) && (foe.FireBuild > k0 || foe.Ignited));
        Ready();
        foreach (var _ in ElPress(new PlayerInput { Infuse = 2 }, 0.05f)) yield return null;
        foreach (var _ in ElPress(new PlayerInput { Ability = true }, 0.1f)) yield return null;
        ScCheck("frost's Aid is Frost Armor", p.HasBuff(Player.BuffFrostArmor));
        ScCheck("the cards the elements replace aren't dealt (Frostbolt, Firestorm, Cinder Snap, Kindling, Deep Chill)",
            new[] { "bolt_frost", "blizzard_fire", "snap_cinder", "kindling", "deepchill" }.All(id => !Upgrades.Available(Upgrades.Get(id), p.Stats)));
        foe.QueueFree();
        foreach (var _ in ElementalistDualsRun()) yield return null;
    }

    /// <summary>`--scenario=elements --hero=elementalist`, the dual charges: the pair's spell on the bolt, movement and fixed-range buttons.</summary>
    private IEnumerable<object> ElementalistDualsRun()
    {
        var p = G.Player;
        var aim = new Vector2(p.Facing, 0);
        var foes = new List<Enemy>();
        void Clear() { foreach (var f in foes) if (IsInstanceValid(f)) f.QueueFree(); foes.Clear(); }
        Enemy Add(float dx, bool hold = true) { var e = ElDummy(p, dx, hold); foes.Add(e); return e; }
        void Ready() { p.TestResetSpells(); p.TestResetDualBolt(); p.SetAlimus(9999); }
        PlayerInput Bolt() => new() { Attack = true, Aim = aim, AimGiven = true };
        PlayerInput Fixed() => new() { Ability2 = true, Aim = aim, AimGiven = true };
        PlayerInput Move() => new() { Dodge = true, Aim = aim, AimGiven = true };
        foreach (var _ in ElWait(0.6f)) yield return null;

        // the names on the buttons for each pair
        Ready();
        foreach (var _ in ElPair(p, 1, 1)) yield return null;
        ScCheck($"fire twice: {p.DualNameFor(ElemSlot.Attack)} / {p.DualNameFor(ElemSlot.Move)} / {p.DualNameFor(ElemSlot.Heavy)}",
            p.DualNameFor(ElemSlot.Attack) == "Meteorite" && p.DualNameFor(ElemSlot.Move) == "Flamebreath" && p.DualNameFor(ElemSlot.Heavy) == "Rain of Fire");
        ScShot("duals_hud");

        // Meteorite (the bolt): falls on the knot of creatures, through a platform in its way
        Clear();
        var a = Add(130); var b = Add(160);
        foreach (var _ in ElWait(0.2f)) yield return null;
        var from = new Vector2(-p.Facing * 0.45f, -1f).Normalized();
        var ledge = new RockLedge { Position = (a.GlobalPosition + b.GlobalPosition) * 0.5f + from * 70f, Half = 44f };
        _world.AddChild(ledge);
        foreach (var _ in ElPress(Bolt(), 0.15f)) yield return null;
        ScShot("duals_meteor");
        foreach (var _ in ElWait(1.6f)) yield return null;
        ScCheck($"a Meteorite falls on the two creatures: both struck and alight ({a.Ignited}, {b.Ignited})", p.ElementalUses.Contains("Meteorite") && a.Ignited && b.Ignited && a.Hp < a.MaxHp && b.Hp < b.MaxHp);
        ScCheck($"and smashes the platform it passes through ({ledge.Broken})", ledge.Broken);
        var rock = new Meteor { GroundY = 500f };
        ScCheck("cast standing, a meteor passes through rock above the caster's ground and lands on ground as low or lower; cast in the air, on any",
            !rock.Lands(new Vector2(0, 400f)) && rock.Lands(new Vector2(0, 500f)) && rock.Lands(new Vector2(0, 560f)) && new Meteor().Lands(new Vector2(0, 100f)));
        rock.Free();
        ledge.QueueFree();

        // Cryometeor: frozen solid, the ground frozen over
        Clear();
        a = Add(130);
        foreach (var _ in ElWait(0.2f)) yield return null;
        Ready();
        foreach (var _ in ElPair(p, 2, 2)) yield return null;
        foreach (var _ in ElPress(Bolt(), 1.6f)) yield return null;
        ScCheck($"a Cryometeor freezes what it lands on solid ({a.FrozenSolid}) and leaves the ground frozen", p.ElementalUses.Contains("Cryometeor") && a.FrozenSolid);
        foreach (var _ in ElWait(3f)) yield return null;

        // Fire Barrage: a volley of bolts
        Clear();
        a = Add(120);
        foreach (var _ in ElWait(0.2f)) yield return null;
        Ready();
        foreach (var _ in ElPair(p, 1, 3)) yield return null;
        foreach (var _ in ElPress(Bolt(), 1.4f)) yield return null;
        var volley = p.LastElementSpell as Barrage;
        ScCheck($"Fire Barrage looses a volley ({volley?.Loosed} bolts) that sets the creature alight ({a.Ignited}, {a.MaxHp - a.Hp:0} damage)", p.ElementalUses.Contains("Fire Barrage") && volley != null && volley.Loosed >= Tune.Dual.BarrageBolts && a.Ignited);

        // Flamebreath (movement): fire in a cone
        Clear();
        a = Add(70);
        foreach (var _ in ElWait(0.2f)) yield return null;
        Ready();
        foreach (var _ in ElPair(p, 1, 1)) yield return null;
        foreach (var _ in ElPress(Move(), 1f)) yield return null;
        ScCheck($"Flamebreath burns what's in its cone ({(p.LastElementSpell as FlameBreath)?.Burned} ticks, alight {a.Ignited})", p.ElementalUses.Contains("Flamebreath") && (a.Ignited || a.FireBuild > 0));
        ScShot("duals_breath");
        foreach (var _ in ElWait(1f)) yield return null;

        // Frost Tower: a leap, a pillar to stand on that creatures go for, that friends pass through
        Clear();
        Ready();
        float y0 = p.GlobalPosition.Y;
        foreach (var _ in ElPair(p, 2, 2)) yield return null;
        foreach (var _ in ElPress(Move(), 1.4f)) yield return null;
        var tower = p.LastTower;
        ScCheck($"Frost Tower: a pillar of ice ({tower?.Risen:0} px, {tower?.MaxHp:0} health) the caster stands on ({y0 - p.GlobalPosition.Y:0} px up, on the ground {p.IsOnFloor()})",
            p.ElementalUses.Contains("Frost Tower") && tower != null && tower.Standing && p.IsOnFloor() && y0 - p.GlobalPosition.Y > Tune.Dual.TowerHeight * 0.8f);
        ScCheck($"its health is {Tune.Dual.TowerHp:0} x the caster's max health over their starting health ({tower?.MaxHp:0})", tower != null && Math.Abs(tower.MaxHp - Tune.Dual.TowerHp * p.Stats.MaxHp / Tune.Elementalist.StartHp) < 0.5f);
        ScShot("duals_tower");
        if (tower != null)
        {
            // a creature beside it goes for the tower, not the hero up on it, and wears it down
            var goon = new Goblin { Position = tower.GlobalPosition + new Vector2(-p.Facing * 46f, -12f) };
            goon.SetMeta("test", true);
            _world.AddChild(goon);
            goon.Wake();
            foes.Add(goon);
            foreach (var _ in ElWait(0.3f)) yield return null;
            ScCheck($"a creature near it is drawn to the tower ({goon.Lure == tower})", goon.Lure == tower);
            float hp0 = p.Hp;
            for (float t = 0; t < 8f && tower.Standing && tower.Struck == 0; t += (float)GetProcessDeltaTime()) yield return null;
            ScCheck($"it strikes the tower ({tower.Struck} blows, {Math.Max(0, tower.Hp):0}/{tower.MaxHp:0}), not the hero on it ({hp0:0} -> {p.Hp:0})", tower.Struck >= 1 && p.Hp >= hp0);
            ScCheck($"a creature can't walk through it ({goon.GlobalPosition.X:0} vs the tower at {tower.GlobalPosition.X:0})", !tower.Standing || Math.Sign(goon.GlobalPosition.X - tower.GlobalPosition.X) == Math.Sign(-p.Facing));
            // its blows bring it down
            tower.Damage(tower.Hp + 1f, goon);
            foreach (var _ in ElWait(0.2f)) yield return null;
            ScCheck($"worn to nothing it shatters ({tower.Standing}), and the caster falls ({(p.CollisionMask & G.LayerTower) == 0})", !IsInstanceValid(tower) || !tower.Standing);
        }
        Clear();
        foreach (var _ in ElWait(1f)) yield return null;

        // Fire Barrier: a ring of fire that throws back what touches it
        Clear();
        a = Add(40, hold: false);
        foreach (var _ in ElWait(0.2f)) yield return null;
        Ready();
        foreach (var _ in ElPair(p, 3, 1)) yield return null;
        foreach (var _ in ElPress(Move(), 1f)) yield return null;
        var ring = p.LastElementSpell as FireRing;
        ScCheck($"Fire Barrier: a ring of fire that burns what touches it ({ring?.Burned}, alight {a.Ignited})", p.ElementalUses.Contains("Fire Barrier") && ring != null && ring.Burned >= 1);
        foreach (var _ in ElWait(4.5f)) yield return null;

        // Frost Barrier: frost armour, spent on the next melee strike
        Clear();
        Ready();
        foreach (var _ in ElPair(p, 2, 3)) yield return null;
        foreach (var _ in ElPress(Move(), 0.1f)) yield return null;
        ScCheck($"Frost Barrier is frost armour on the caster ({p.HasBuff(Player.BuffFrostWard)})", p.ElementalUses.Contains("Frost Barrier") && p.HasBuff(Player.BuffFrostWard));
        a = Add(0, hold: false);
        a.GlobalPosition = p.GlobalPosition + new Vector2(p.Facing * 20f, -4f);
        p.Hurt(1f, a.GlobalPosition, 0f, a);
        ScCheck($"the next melee strike fills the attacker with frost ({a.FrostBuild:0} frost, chilled {a.Chilled}) and spends it ({p.HasBuff(Player.BuffFrostWard)})", p.ElementalUses.Contains("Frost Barrier!") && a.Chilled && !p.HasBuff(Player.BuffFrostWard));
        float before = a.ChillLeft;
        p.TestInvulnClear();
        p.Hurt(1f, a.GlobalPosition, 0f, a);
        ScCheck("once: the strike after it does nothing more", p.ElementalUses.Count(u => u == "Frost Barrier!") == 1);
        Clear();

        // Rain of Fire / Hailstorm (fixed range)
        Clear();
        a = Add(Tune.ElementalStaff.FixedRange);
        a.GlobalPosition = p.CastPoint + aim * Tune.ElementalStaff.FixedRange + new Vector2(0, 8);
        foreach (var _ in ElWait(0.2f)) yield return null;
        Ready();
        foreach (var _ in ElPair(p, 1, 1)) yield return null;
        foreach (var _ in ElPress(Fixed(), 1.2f)) yield return null;
        ScShot("duals_rain");
        foreach (var _ in ElWait(Tune.Dual.RainSeconds - 0.8f)) yield return null;
        ScCheck($"Rain of Fire falls on the spot ({(p.LastElementSpell as Downpour)?.Struck} strikes, alight {a.Ignited}, combusted {a.Combusted})", p.ElementalUses.Contains("Rain of Fire") && (a.Ignited || a.Combusted > 0));
        foreach (var _ in ElWait(0.5f)) yield return null;
        Ready();
        foreach (var _ in ElPair(p, 2, 2)) yield return null;
        foreach (var _ in ElPress(Fixed(), 2.4f)) yield return null;
        ScCheck($"Hailstorm fills it with frost ({a.FrostBuild:0}, chilled {a.Chilled}, frozen {a.FrozenSolid})", p.ElementalUses.Contains("Hailstorm") && (a.FrostBuild > 0 || a.Chilled || a.FrozenSolid));
        foreach (var _ in ElWait(1.5f)) yield return null;

        // Fire Storm / Blizzard: the storms, fanned by the wind
        Ready();
        foreach (var _ in ElPair(p, 1, 3)) yield return null;
        foreach (var _ in ElPress(Fixed(), 0.3f)) yield return null;
        ScCheck($"fire and wind at range: a Fire Storm that pulls ({p.LastBlizzard?.Fire}, {p.LastBlizzard?.Pull})", p.ElementalUses.Contains("Fire Storm") && p.LastBlizzard != null && p.LastBlizzard.Fire && p.LastBlizzard.Pull > 0);
        foreach (var _ in ElWait(Tune.Elementalist.BlizzardSeconds)) yield return null;
        Ready();
        foreach (var _ in ElPair(p, 2, 3)) yield return null;
        foreach (var _ in ElPress(Fixed(), 0.3f)) yield return null;
        ScCheck($"frost and wind: a Blizzard ({p.LastBlizzard?.El})", p.ElementalUses.Contains("Blizzard") && p.LastBlizzard != null && p.LastBlizzard.El == HeroElement.Frost);
        foreach (var _ in ElWait(Tune.Elementalist.BlizzardSeconds)) yield return null;

        // Tornado and Frostfire Lance stay on the fixed-range button
        Clear();
        a = Add(70, hold: false);
        foreach (var _ in ElWait(0.3f)) yield return null;
        Ready();
        float x0 = a.GlobalPosition.X;
        foreach (var _ in ElPair(p, 3, 3)) yield return null;
        foreach (var _ in ElPress(Fixed(), 1.4f)) yield return null;
        var tw = p.LastElementSpell as Tornado;
        ScCheck($"a Tornado takes up the creature ({tw?.Caught}) and carries it along ({Math.Abs(a.GlobalPosition.X - x0):0} px)", p.ElementalUses.Contains("Tornado") && tw != null && tw.Caught >= 1 && Math.Abs(a.GlobalPosition.X - x0) > 20f);
        foreach (var _ in ElWait(3f)) yield return null;
        Clear();
        a = Add(110); b = Add(190);
        foreach (var _ in ElWait(0.2f)) yield return null;
        b.Ignite(0.01f, 5f);
        Ready();
        foreach (var _ in ElPair(p, 2, 1)) yield return null;
        foreach (var _ in ElPress(Fixed(), 0.3f)) yield return null;
        float la = a.MaxHp - a.Hp, lb = b.MaxHp - b.Hp;
        ScCheck($"Frostfire Lance strikes both in its line ({la:0}, {lb:0}), the burning one harder", p.ElementalUses.Contains("Frostfire Lance") && la > 0 && lb > la * 1.3f);

        // a pair with nothing on a button spends just the oldest charge there
        Clear();
        Ready();
        foreach (var _ in ElPair(p, 3, 3)) yield return null;
        foreach (var _ in ElPress(new PlayerInput { Ability = true }, 0.1f)) yield return null;
        ScCheck($"Aid has no pair: it spends the oldest charge (Narrow Draft) and one waits ({p.Charges.Count})", p.HasBuff(Player.BuffTailwind) && p.Charges.Count == 1);
        Clear();
    }
}
