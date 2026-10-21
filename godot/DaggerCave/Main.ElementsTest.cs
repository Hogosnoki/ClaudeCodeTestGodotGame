using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

public partial class Main
{
    private IEnumerator<object> _elSteps;

    /// <summary>
    /// `--scenario=elements --hero=swordsman|elementalist|warden`: the elemental kits (Core/Elements.cs). An element readied with its button
    /// changes the next attack into its version; build-ups fill and take hold (alight, combust; chilled, frozen, shatter; stunned,
    /// sapped; shoved); each version rests its own time; the cards the elements replace aren't dealt; the HUD (elements_hud.png).
    /// </summary>
    private void ElementsScenario()
    {
        if (_elSteps == null)
        {
            // (the real rules, cooldowns and all; the runs turn the testing switch on where they test it)
            Tune.Testing.NoCooldowns = false;
            if (_scT < 0.8f) return;
            _elSteps = (G.Player.Stats.Hero switch { HeroKind.Elementalist => ElementalistRun(), HeroKind.Warden => WardenElementsRun(), HeroKind.Vitalist => VitalistElementsRun(), HeroKind.Aegis => AegisElementsRun(), HeroKind.Rogue => RogueElementsRun(), HeroKind.ShapeShifter => ShifterElementsRun(), _ => SwordsmanElementsRun() }).GetEnumerator();
        }
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
        if (p.Mode != Elements.Of(p.Stats.Hero)[slot - 1]) foreach (var _ in ElMode(slot, 0f)) yield return null;
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

        // the mode controls (the same for every elemental hero)
        foreach (var _ in ModeControlsRun(p)) yield return null;

        // fire mode (his own): every swing a Searing Strike (40 fire), three to set it alight, three more to combust
        int uses0 = p.ElementalUses.Count;
        foreach (var _ in ElMode(2, 0.05f)) yield return null;
        ScCheck($"in fire mode ({p.Mode})", p.Mode == HeroElement.Fire);
        ScShot("elements_hud");
        foreach (var _ in ElWait(1.2f)) yield return null;
        ScShot("elements_glow");
        foreach (var _ in ElPress(Atk(), 0.6f)) yield return null;
        ScCheck($"a swing is a Searing Strike ({string.Join(", ", p.ElementalUses.Skip(uses0))}), filling {Tune.ElementalSword.SearBuild:0} fire ({foe.FireBuild:0}), and the mode stays ({p.Mode})",
            p.ElementalUses.Skip(uses0).Contains("Searing Strike") && Math.Abs(foe.FireBuild - Tune.ElementalSword.SearBuild) < 1 && p.Mode == HeroElement.Fire);
        for (int k = 0; k < 2; k++) foreach (var _ in ElPress(Atk(), 0.6f)) yield return null;
        ScCheck($"two more and it's full of fire: alight ({foe.Ignited})", foe.Ignited);
        for (int k = 0; k < 3; k++) foreach (var _ in ElStrike(p, 2, Atk)) yield return null;
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
        foreach (var _ in ElMode(1, 0.05f)) yield return null;
        Near();
        foreach (var _ in ElPress(new PlayerInput { Ability2 = true, Aim = aim, AimGiven = true }, 1.4f)) yield return null;
        ScCheck($"earth's Hard Swing is ThunderCrush: it shatters the frozen ({foe.Shattered}) and its full earth stuns ({foe.Reeling})", p.ElementalUses.Contains("ThunderCrush") && foe.Shattered >= 1);
        ScCheck($"and it rests the Hard Swing ({p.HardSwingSeconds:0.0} s left): in a mode, every button keeps a cooldown", p.HardSwingSeconds > 0f);

        // frost's Hard Swing is the Ice Axe: raised behind the head while the axe head grows out of the blade, then down in front only
        foreach (var _ in ElWait(1.2f)) yield return null;
        Ready();
        Near();
        var behind = ElDummy(p, -16);
        float bh0 = behind.Hp;
        foreach (var _ in ElMode(3, 0.05f)) yield return null;
        Near();
        _scInput = new PlayerInput { Ability2 = true, Aim = aim, AimGiven = true }; yield return null; _scInput = default;
        // (watched frame by frame through the wind-up)
        int axeKind = p.AxeKind; float grow0 = p.AxeGrow, grow1 = 0f, wound = 0f; bool untouched = true, shot = false;
        while (p.AxeKind > 0 && p.AxeStage < 1f && wound < 3f)
        {
            grow1 = Math.Max(grow1, p.AxeGrow);
            untouched &= foe.Hp >= foe.MaxHp;
            if (!shot && p.AxeGrow > 0.6f) { shot = true; ScShot("elements_iceaxe"); }
            wound += (float)GetProcessDeltaTime();
            yield return null;
        }
        ScCheck($"the Ice Axe winds up long ({wound:0.00} s), its head growing out of the blade (kind {axeKind}, grown {grow0:0.00} -> {grow1:0.00}), nothing struck meanwhile ({untouched})",
            p.ElementalUses.Contains("Ice Axe") && axeKind == 1 && grow0 < 0.3f && grow1 > 0.9f && wound > 0.6f && untouched);
        foreach (var _ in ElWait(1.2f)) yield return null;
        ScCheck($"it comes down in front ({foe.MaxHp - foe.Hp:0} damage, frost {foe.FrostBuild:0}) and not behind ({bh0 - behind.Hp:0})", foe.Hp < foe.MaxHp && behind.Hp >= bh0 && p.AxeKind == 0);
        behind.QueueFree();

        // earth on the swing: the Heaving Swing
        foreach (var _ in ElWait(1.2f)) yield return null;
        Ready();
        float e0 = foe.EarthBuild;
        foreach (var _ in ElMode(1, 0.05f)) yield return null;
        foreach (var _ in ElPress(Atk(), 1.2f)) yield return null;
        ScCheck($"earth on the swing is the Heaving Swing ({foe.EarthBuild:0} earth)", p.ElementalUses.Contains("Heaving Swing") && foe.EarthBuild > e0);

        // Empower Hilt: frost on every blow; earth saps
        Ready();
        foreach (var _ in ElMode(3, 0.05f)) yield return null;
        foreach (var _ in ElPress(new PlayerInput { Ability = true, Aim = aim, AimGiven = true }, 0.1f)) yield return null;
        float f0 = foe.FrostBuild;
        foreach (var _ in ElPress(Atk(), 0.6f)) yield return null;
        ScCheck($"a frost hilt: swings carry frost ({f0:0} -> {foe.FrostBuild:0}, {p.HiltLeft:0.0} s left)", p.HiltElement == HeroElement.Frost && foe.FrostBuild > f0);
        Ready();
        foreach (var _ in ElMode(1, 0.05f)) yield return null;
        foreach (var _ in ElPress(new PlayerInput { Ability = true, Aim = aim, AimGiven = true }, 0.1f)) yield return null;
        foreach (var _ in ElPress(Atk(), 0.6f)) yield return null;
        ScCheck($"an earth hilt saps what it strikes ({foe.Sapped})", foe.Sapped);
        // the dodge with an element: Explosive Lunge, Glacial Vault, Earthfall
        foreach (var _ in SwordsmanJumps(p)) yield return null;
        ScCheck("the cards the elements replace aren't dealt (Twin Charge, Swift Heave, Relentless Charge)", !Upgrades.Available(Upgrades.Get("charge2"), p.Stats) && !Upgrades.Available(Upgrades.Get("heave_swift"), p.Stats) && !Upgrades.Available(Upgrades.Get("charge_combo"), p.Stats));
        foe.QueueFree();
        foreach (var _ in SwordsmanDualsRun()) yield return null;
        foreach (var _ in PairCoverageRun(p)) yield return null;
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
        // the jump stays a jump: the mode's leap is the dodge's
        foreach (var _ in ElMode(3, 0.02f)) yield return null;
        int vaults = p.ElementalUses.Count(u => u == "Glacial Vault");
        foreach (var _ in ElJump(p, 0.3f, _ => { })) yield return null;
        ScCheck($"a jump in frost mode is a plain jump ({p.Mode})", p.ElementalUses.Count(u => u == "Glacial Vault") == vaults && p.Mode == HeroElement.Frost);
        // Glacial Vault: a spring back off a burst of ice (frost to what's beside it), leaving the hero's likeness in ice where it stood
        var foe = Fresh(26);
        foreach (var _ in ElWait(0.2f)) yield return null;
        foreach (var _ in ElMode(3, 0.02f)) yield return null;
        float vx0 = p.GlobalPosition.X, face = p.Facing;
        foreach (var _ in ElJump(p, 0.5f, h => vault = h, dodge: true)) yield return null;
        float sprang = (vx0 - p.GlobalPosition.X) * face;
        var decoy = p.LastDecoy;
        ScCheck($"frost's dodge is a Glacial Vault: a spring back ({sprang:0} px, still facing on {p.Facing == face}), frost to what's beside it ({foe.FrostBuild:0}), and an ice decoy left standing ({decoy?.Standing})",
            p.ElementalUses.Contains("Glacial Vault") && sprang > 30f && foe.FrostBuild > 0 && decoy != null && IsInstanceValid(decoy) && decoy.Standing);
        if (decoy != null && IsInstanceValid(decoy))
        {
            // while it stands, another vault leaves no other
            foreach (var _ in ElMode(3, 0.02f)) yield return null;
            foreach (var _ in ElJump(p, 0.3f, _ => { }, dodge: true)) yield return null;
            int decoys = G.World.GetChildren().OfType<IceDecoy>().Count(d => d.Standing);
            ScCheck($"a second vault while the likeness stands leaves no other ({decoys} standing, the same one {p.LastDecoy == decoy})", decoys == 1 && p.LastDecoy == decoy && p.ElementalUses.Count(u => u == "Glacial Vault") >= 2);
            // creatures go for it: a blow meant for the hero lands on it, and a second breaks it
            bool lured = foe.Lure == decoy;
            float taken = p.Hurt(10f, foe.GlobalPosition, 0f, foe);
            ScCheck($"the decoy draws the creature beside it ({lured}) and takes the blow meant for the hero ({taken:0} taken, {decoy.Struck} blow on it)", lured && taken <= 0f && decoy.Struck == 1 && decoy.Standing);
            decoy.Damage(1f, foe);
            foreach (var _ in ElWait(0.1f)) yield return null;
            ScCheck($"a second blow shatters it ({(IsInstanceValid(decoy) ? decoy.Standing.ToString() : "gone")})", !IsInstanceValid(decoy) || !decoy.Standing);
        }
        // Explosive Lunge: a blast where it leaves the ground, and far forward
        foreach (var _ in ElWait(0.4f)) yield return null;
        foe = Fresh(-22);
        foreach (var _ in ElWait(0.2f)) yield return null;
        float x0 = p.GlobalPosition.X;
        foreach (var _ in ElMode(2, 0.02f)) yield return null;
        foreach (var _ in ElJump(p, 0.3f, _ => { }, dodge: true)) yield return null;
        float went = Math.Abs(p.GlobalPosition.X - x0);
        ScCheck($"fire's dodge is an Explosive Lunge: far forward ({went:0} px) and fire to what it left behind ({foe.FireBuild:0}, alight {foe.Ignited})", p.ElementalUses.Contains("Explosive Lunge") && went > 80f && (foe.FireBuild > 0 || foe.Ignited));
        // and on the way he swings at the creatures he comes to
        foreach (var _ in ElWait(0.3f)) yield return null;
        var ahead = Fresh(55);
        foreach (var _ in ElWait(0.2f)) yield return null;
        foreach (var _ in ElMode(2, 0.02f)) yield return null;
        foreach (var _ in ElJump(p, 0.3f, _ => { }, dodge: true)) yield return null;
        ScCheck($"on the way he swings at the creature he meets ({p.LungeStrikes} struck, {ahead.MaxHp - ahead.Hp:0} damage)", p.LungeStrikes >= 1 && ahead.Hp < ahead.MaxHp);
        // Earthfall: it lands in a shockwave
        foreach (var _ in ElWait(0.4f)) yield return null;
        foe = Fresh(30);
        foreach (var _ in ElWait(0.2f)) yield return null;
        p.GlobalPosition = foe.GlobalPosition - new Vector2(30 * p.Facing, 4);
        foreach (var _ in ElMode(1, 0.02f)) yield return null;
        foreach (var _ in ElJump(p, 0.5f, _ => { }, dodge: true)) yield return null;
        foreach (var _ in ElWait(0.15f)) yield return null;
        float lost = foe.MaxHp - foe.Hp;
        ScCheck($"earth's dodge is Earthfall: it lands in a shockwave ({lost:0} damage, earth {foe.EarthBuild:0}, reeling {foe.Reeling})", p.ElementalUses.Contains("Earthfall!") && lost > 0 && (foe.EarthBuild > 0 || foe.Reeling));
        // Earthfall goes in the air too, without its jump: he comes straight down; a vault goes only from the ground
        foreach (var _ in ElWait(0.4f)) yield return null;
        while (!p.IsOnFloor() || p.JumpElementRest > 0) yield return null;
        foreach (var _ in ElMode(1, 0.02f)) yield return null;
        int falls = p.ElementalUses.Count(u => u == "Earthfall"), lands = p.ElementalUses.Count(u => u == "Earthfall!");
        _scInput = new PlayerInput { Jump = true, JumpHeld = true }; yield return null;
        _scInput = new PlayerInput { JumpHeld = true };
        foreach (var _ in ElWait(0.12f)) yield return null;
        bool aloft = !p.IsOnFloor();
        _scInput = new PlayerInput { Dodge = true }; yield return null;
        _scInput = default;
        float vyAfter = p.Velocity.Y;
        for (float t = 0; t < 1.5f && !p.IsOnFloor(); t += (float)GetProcessDeltaTime()) yield return null;
        yield return null;
        ScCheck($"in the air earth's dodge is still Earthfall (aloft {aloft}), with no jump ({vyAfter:0} px/s), and it lands in its shockwave",
            aloft && p.ElementalUses.Count(u => u == "Earthfall") == falls + 1 && vyAfter >= 0f && p.ElementalUses.Count(u => u == "Earthfall!") == lands + 1);
        while (p.JumpElementRest > 0) yield return null;
        foreach (var _ in ElMode(3, 0.02f)) yield return null;
        int vaulted = p.ElementalUses.Count(u => u == "Glacial Vault");
        _scInput = new PlayerInput { Jump = true, JumpHeld = true }; yield return null;
        _scInput = new PlayerInput { JumpHeld = true };
        foreach (var _ in ElWait(0.12f)) yield return null;
        _scInput = new PlayerInput { Dodge = true }; yield return null;
        _scInput = default;
        foreach (var _ in ElWait(0.1f)) yield return null;
        ScCheck($"but in the air frost's dodge is nothing: the vault goes only from the ground ({p.ElementalUses.Count(u => u == "Glacial Vault") - vaulted} vaults, no Dash Slash: {p.DashSlashes})", p.ElementalUses.Count(u => u == "Glacial Vault") == vaulted && p.DashSlashes == 0);
        foreach (var _ in ElWait(0.6f)) yield return null;
        foreach (var f in foes) if (IsInstanceValid(f)) f.QueueFree();
    }

    /// <summary>The mode of element <paramref name="a"/>, overcharged (<paramref name="b"/> the same): the greater version waits on the buttons
    /// that have one. (A different <paramref name="b"/> rides on top as the attribute.)</summary>
    private IEnumerable<object> ElPair(Player p, int a, int b)
    {
        foreach (var _ in ElWait(0.1f)) yield return null;
        foreach (var _ in ElMode(a, 0.02f)) yield return null;
        if (a == b) p.TestOvercharge();
        else p.TestAttribute(Elements.Of(p.Stats.Hero)[b - 1]);
    }

    /// <summary>
    /// The mode controls, pressed as a player would (the same for every elemental hero): it starts in its own element's mode; another
    /// element goes on top as the attribute (one slot: a third replaces it); its own element on top is the overcharge (another replaces that
    /// too); the attribute's element pressed again switches the mode, at most once every few seconds; a spent overcharge comes back after a
    /// while; locked (a new save), there's no overcharge; the attribute's element rides on every blow.
    /// </summary>
    private IEnumerable<object> ModeControlsRun(Player p)
    {
        var els = Elements.Of(p.Stats.Hero);
        var sig = Elements.Signature(p.Stats.Hero);
        int si = Array.IndexOf(els, sig) + 1, o1 = si % 3 + 1, o2 = (si + 1) % 3 + 1;
        HeroElement E(int slot) => els[slot - 1];
        IEnumerable<object> Press(int slot)
        {
            while (p.InfuseCooldown > 0) yield return null;
            _scInput = new PlayerInput { Infuse = slot }; yield return null; yield return null;
            _scInput = default; yield return null;
        }
        p.TestMode(HeroElement.None);
        p.TestResetModeCooldowns();
        ScCheck($"it starts in {Elements.Name(sig).ToLowerInvariant()} mode, its own, nothing on top ({p.Mode}, {p.Top})", p.Mode == sig && p.Top == HeroElement.None);
        foreach (var _ in Press(o1)) yield return null;
        ScCheck($"another element goes on top as the attribute ({p.Mode} + {p.Attribute})", p.Mode == sig && p.Attribute == E(o1));
        foreach (var _ in Press(o2)) yield return null;
        ScCheck($"a third replaces it: one slot on top ({p.Mode} + {p.Attribute})", p.Mode == sig && p.Attribute == E(o2));
        foreach (var _ in Press(si)) yield return null;
        ScCheck($"its own element on top is the overcharge, replacing the attribute (overcharged {p.Overcharged}, attribute {p.Attribute})", p.Overcharged && p.Attribute == HeroElement.None);
        ScShot("modes_overcharged");
        foreach (var _ in Press(o1)) yield return null;
        ScCheck($"another replaces the overcharge (overcharged {p.Overcharged}, {p.Mode} + {p.Attribute})", !p.Overcharged && p.Attribute == E(o1));
        foreach (var _ in Press(o1)) yield return null;
        ScCheck($"the attribute's element again switches the mode to it ({p.Mode}, on top {p.Top}, {p.ModeCooldown:0.0} s before another switch)", p.Mode == E(o1) && p.Top == HeroElement.None && p.ModeCooldown > 0);
        ScShot("modes_switched");
        foreach (var _ in Press(si)) yield return null;
        foreach (var _ in Press(si)) yield return null;
        ScCheck($"a switch back at once waits on the mode's cooldown ({p.Mode} + {p.Attribute})", p.Mode == E(o1) && p.Attribute == sig);
        foreach (var _ in ElWait(p.ModeCooldown + 0.1f)) yield return null;
        foreach (var _ in Press(si)) yield return null;
        ScCheck($"after it, the switch goes ({p.Mode})", p.Mode == sig);
        foreach (var _ in Press(si)) yield return null;
        p.TestSpendOvercharge();
        foreach (var _ in Press(si)) yield return null;
        ScCheck($"a spent overcharge comes back only after a while ({p.OverchargeSeconds(sig):0} s; overcharged {p.Overcharged})", !p.Overcharged && p.OverchargeSeconds(sig) > 0);
        p.TestResetModeCooldowns();
        bool dualsWere = Tune.Testing.DualCharges, unlockedWas = Meta.DualChargesUnlocked;
        Tune.Testing.DualCharges = false; Meta.DualChargesUnlocked = false;
        foreach (var _ in Press(si)) yield return null;
        ScCheck($"locked (a new save), there's no overcharge ({p.Overcharged})", !p.Overcharged);
        Tune.Testing.DualCharges = dualsWere; Meta.DualChargesUnlocked = unlockedWas;
        // the attribute rides on the blows: its element's build-up (an element that builds: not wind)
        var foe = ElDummy(p, 40);
        foreach (var _ in ElWait(0.1f)) yield return null;
        var rider = els.First(x => x != sig && x != HeroElement.Wind);
        p.TestAttribute(rider);
        p.AttributeHit(foe, p.GlobalPosition);
        float built = rider switch { HeroElement.Fire => foe.FireBuild, HeroElement.Frost => foe.FrostBuild, HeroElement.Nature => foe.NatureBuild, HeroElement.Water => foe.WaterBuild, _ => foe.EarthBuild };
        ScCheck($"{Elements.Name(rider).ToLowerInvariant()} on top adds its build-up to a blow ({built:0})", built > 0);
        foe.QueueFree();
        // with cooldowns off for testing, the switch and the overcharge come back at once
        Tune.Testing.NoCooldowns = true;
        p.TestMode(sig); p.TestResetModeCooldowns();
        foreach (var _ in Press(o1)) yield return null;
        foreach (var _ in Press(o1)) yield return null;
        foreach (var _ in Press(si)) yield return null;
        foreach (var _ in Press(si)) yield return null;
        bool backAtOnce = p.Mode == sig;
        foreach (var _ in Press(si)) yield return null;
        p.TestSpendOvercharge();
        foreach (var _ in Press(si)) yield return null;
        ScCheck($"with cooldowns off for testing, a switch back goes at once ({backAtOnce}) and a spent overcharge is there again ({p.Overcharged})", backAtOnce && p.Overcharged);
        Tune.Testing.NoCooldowns = false;
        // an attribute on top stays: the mix waits on the buttons as long as it's there
        p.TestMode(sig); p.TestResetModeCooldowns();
        p.TestAttribute(E(o1));
        string mix = p.NameFor(ElemSlot.Attack);
        // (the Shape Shifter has no mixes: her element is the kind of creature, and the attribute rides on its blows)
        if (p.Stats.Hero == HeroKind.ShapeShifter) ScCheck($"with {Elements.Name(E(o1)).ToLowerInvariant()} on top it stays there, to ride on her blows ({p.Attribute})", p.Attribute == E(o1));
        else ScCheck($"with {Elements.Name(E(o1)).ToLowerInvariant()} on top, the attack is the mix: {mix}", mix == Elements.DualName(p.Stats.Hero, ElemSlot.Attack, sig, E(o1)) && mix != "");
        p.TestMode(sig);
        p.TestResetModeCooldowns();
    }

    /// <summary>
    /// Every pair on every elemental button, used once each (the same element twice: the mode overcharged; two different: the mode with
    /// the other on top as its attribute): each has a version, and each goes off.
    /// </summary>
    private IEnumerable<object> PairCoverageRun(Player p)
    {
        foreach (var e in G.Enemies.ToArray()) e.QueueFree();
        var els = Elements.Of(p.Stats.Hero);
        var start = p.GlobalPosition;
        var aim = new Vector2(p.Facing, 0);
        var slots = p.Stats.Hero switch
        {
            HeroKind.Warden => new[] { ElemSlot.Attack, ElemSlot.Move, ElemSlot.Heavy },
            HeroKind.Vitalist or HeroKind.Aegis or HeroKind.Rogue => new[] { ElemSlot.Attack, ElemSlot.Move, ElemSlot.Aid },
            _ => new[] { ElemSlot.Attack, ElemSlot.Move, ElemSlot.Heavy, ElemSlot.Aid },
        };
        // (something to drain, for the Vitalist: a drain at nothing is a plain wisp)
        var target = p.Stats.Hero == HeroKind.Vitalist ? ElDummy(p, 60) : null;
        var missing = new List<string>();
        int fired = 0, all = 0;
        foreach (var slot in slots)
            for (int i = 0; i < els.Length; i++)
                for (int j = i; j < els.Length; j++)
                {
                    all++;
                    string name = Elements.DualName(p.Stats.Hero, slot, els[i], els[j]);
                    if (name == "") { missing.Add($"{slot} {els[i]}+{els[j]}: none"); continue; }
                    // (from where it began, on its feet, every cooldown cleared)
                    for (int f = 0; f < 200 && (p.Heaving || !p.IsOnFloor() || p.IsShieldDashing); f++) yield return null;
                    p.GlobalPosition = start; p.Velocity = Vector2.Zero;
                    p.ResetAbilityCooldowns();
                    if (p.Stats.Hero == HeroKind.Elementalist) { p.TestResetSpells(); p.TestResetDualBolt(); p.SetAlimus(9999); }
                    if (p.Stats.Hero == HeroKind.Warden) p.RefillShield();
                    if (p.Stats.Hero == HeroKind.Vitalist) { p.Stats.VitalForceMax = Math.Max(p.Stats.VitalForceMax, 9999); p.SetVitalForce(9999); }
                    if (p.Stats.Hero == HeroKind.Aegis) { p.TestResetCooldowns(); p.TestClearBubble(); }
                    if (p.Stats.Hero == HeroKind.Rogue) p.TestCatchDaggers();
                    p.TestMode(els[i]);
                    p.TestResetModeCooldowns();
                    if (i == j) p.TestOvercharge(); else p.TestAttribute(els[j]);
                    foreach (var _ in ElWait(0.15f)) yield return null;
                    int had = p.ElementalUses.Count(u => u == name);
                    if (slot == ElemSlot.Heavy && p.Stats.Hero == HeroKind.Warden)
                    {
                        for (int f = 0; f < 4; f++) { _scInput = new PlayerInput { GuardHeld = true, GuardAim = aim }; yield return null; }
                        _scInput = default;
                    }
                    else
                    {
                        var press = slot switch
                        {
                            ElemSlot.Attack => new PlayerInput { Attack = true, Aim = aim, AimGiven = true },
                            ElemSlot.Move => p.Stats.Hero == HeroKind.Warden ? new PlayerInput { Ability = true, Aim = aim, AimGiven = true } : new PlayerInput { Dodge = true, Aim = aim, AimGiven = true },
                            ElemSlot.Heavy => new PlayerInput { Ability2 = true, Aim = aim, AimGiven = true },
                            _ => new PlayerInput { Ability = true, Aim = aim, AimGiven = true },
                        };
                        foreach (var _ in ElPress(press, 0.1f)) yield return null;
                    }
                    if (p.ElementalUses.Count(u => u == name) > had) fired++; else missing.Add($"{slot} {els[i]}+{els[j]} ({name}) didn't go");
                    foreach (var _ in ElWait(slot == ElemSlot.Heavy && p.Stats.Hero == HeroKind.Swordsman ? 2.2f : 0.6f)) yield return null;
                }
        if (target != null && IsInstanceValid(target)) target.QueueFree();
        ScCheck($"every pair has a version on every elemental button, and each goes off ({fired}/{all}){(missing.Count > 0 ? ": " + string.Join("; ", missing) : "")}", fired == all);
        p.GlobalPosition = start; p.Velocity = Vector2.Zero;
        p.TestMode(Elements.Signature(p.Stats.Hero));
        p.TestResetModeCooldowns();
        p.ResetAbilityCooldowns();
    }

    /// <summary>Straight into the mode of element <paramref name="slot"/> (1-3), nothing on top, its cooldowns cleared.</summary>
    private IEnumerable<object> ElMode(int slot, float after = 0.1f)
    {
        var p = G.Player;
        p.TestMode(Elements.Of(p.Stats.Hero)[slot - 1]);
        p.TestResetModeCooldowns();
        foreach (var _ in ElWait(after)) yield return null;
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

        // overcharge: the mode's own element pressed again makes its Hard Swing the great cleave
        p.ResetAbilityCooldowns();
        foreach (var _ in ElPair(p, 3, 3)) yield return null;
        ScCheck($"frost mode, overcharged: the Hard Swing is the {p.DualNameFor(ElemSlot.Heavy)} ({string.Join("+", p.Charges)})", p.Overcharged && p.DualNameFor(ElemSlot.Heavy) == "Glacier Cleaver");
        ScShot("duals_hud");

        // Glacier Cleaver: the vast axe freezes what it cuts, and the head shatters it as it lands
        var foe = Fresh(reach * 0.95f);
        foreach (var _ in ElWait(0.2f)) yield return null;
        p.ResetAbilityCooldowns();
        foreach (var _ in ElPair(p, 3, 3)) yield return null;
        _scInput = Hard(); yield return null; yield return null; _scInput = default;
        foreach (var _ in ElWait(0.25f)) yield return null;
        ScCheck($"Glacier Cleaver: the blade becomes a vast axe of ice ({p.GiantBlade}, kind {p.AxeKind})", p.ElementalUses.Contains("Glacier Cleaver") && p.GiantBlade && p.AxeKind == 2);
        foreach (var _ in ElWait(0.7f)) yield return null;
        ScShot("duals_glacier");
        foreach (var _ in ElWait(1.2f)) yield return null;
        float lost = foe.MaxHp - foe.Hp;
        ScCheck($"its blow reaches {reach * 0.95f:0} px, freezes and shatters ({foe.Shattered}) for a great deal ({lost:0})", foe.Shattered >= 1 && lost > Tune.Swordsman.Damage * 6f);
        ScCheck($"the overcharge is spent ({p.Overcharged}) and frost's comes back in {p.OverchargeSeconds(HeroElement.Frost):0} s", !p.Overcharged && p.OverchargeSeconds(HeroElement.Frost) > 0f);

        // Earthsplitter: a fissure runs along the floor and stuns what it passes under
        foe = Fresh(reach * 2.2f);
        foreach (var _ in ElWait(0.2f)) yield return null;
        p.ResetAbilityCooldowns();
        foreach (var _ in ElPair(p, 1, 1)) yield return null;
        foreach (var _ in ElPress(Hard(), 0.9f)) yield return null;
        lost = foe.MaxHp - foe.Hp;
        ScCheck($"Earthsplitter gathers a long while: nothing yet 0.9 s in ({lost:0} damage)", p.ElementalUses.Contains("Earthsplitter") && lost <= 0f);
        ScShot("duals_splitter_windup");
        foreach (var _ in ElWait(1.6f)) yield return null;
        lost = foe.MaxHp - foe.Hp;
        ScCheck($"then its fissure reaches a creature {reach * 2.2f:0} px off and stuns it ({lost:0} damage, reeling {foe.Reeling})", lost > Tune.Swordsman.Damage && foe.Reeling);
        foreach (var _ in ElWait(0.6f)) yield return null;

        // Breaking Quake: the blade driven into the floor, cracks of magma tearing out either way: 100 fire to what stands over them
        foe = Fresh(reach * 2f);
        var behind = ElDummy(p, -reach * 1.5f);
        foes.Add(behind);
        foreach (var _ in ElWait(0.2f)) yield return null;
        p.ResetAbilityCooldowns();
        foreach (var _ in ElPair(p, 1, 2)) yield return null;
        foreach (var _ in ElPress(Hard(), 1.6f)) yield return null;
        ScShot("duals_quake");
        foreach (var _ in ElWait(0.3f)) yield return null;
        var cracks = p.LastElementSpell as MagmaCracks;
        ScCheck($"Breaking Quake: cracks of magma tear out either way ({cracks?.Struck} struck): both creatures alight ({foe.Ignited}, {behind.Ignited})", p.ElementalUses.Contains("Breaking Quake") && cracks != null && foe.Ignited && behind.Ignited);
        foreach (var _ in ElWait(1.6f)) yield return null;
        ScCheck($"and the cracks close moments after ({(cracks != null && IsInstanceValid(cracks) ? "open" : "closed")})", cracks == null || !IsInstanceValid(cracks));

        // Thermal Shock: whichever of fire and frost it holds more, full; the burning burst
        foe = Fresh(reach * 0.8f);
        foe.Build(HeroElement.Fire, 60f);
        foe.Build(HeroElement.Frost, 20f);
        foreach (var _ in ElWait(0.2f)) yield return null;
        p.ResetAbilityCooldowns();
        foreach (var _ in ElPair(p, 2, 3)) yield return null;
        int burst0 = foe.Combusted;
        foreach (var _ in ElPress(Hard(), 1.4f)) yield return null;
        ScCheck($"Thermal Shock on a creature fuller of fire: alight, and it bursts at once ({burst0} -> {foe.Combusted}), its frost untouched ({foe.FrostBuild:0})", p.ElementalUses.Contains("Thermal Shock") && foe.Combusted > burst0 && foe.FrostBuild < 25f);

        // Steam Strike: the swing adds whichever of fire and frost it holds more of
        foe = Fresh(reach * 0.7f);
        foe.Build(HeroElement.Frost, 30f);
        foreach (var _ in ElWait(0.2f)) yield return null;
        p.ResetAbilityCooldowns();
        foreach (var _ in ElPair(p, 2, 3)) yield return null;
        foreach (var _ in ElPress(new PlayerInput { Attack = true, Aim = aim, AimGiven = true }, 0.6f)) yield return null;
        ScCheck($"Steam Strike on a creature fuller of frost adds frost ({foe.FrostBuild:0}), not fire ({foe.FireBuild:0})", p.ElementalUses.Contains("Steam Strike") && foe.FrostBuild > 60f && foe.FireBuild <= 0f);

        // the movement's greater leap and mixes
        foreach (var _ in SwordsmanLeapDuals(p)) yield return null;

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

        foreach (var f in foes) if (IsInstanceValid(f)) f.QueueFree();
    }

    /// <summary>`--scenario=elements --hero=swordsman`: the dodge with something on top: Comet, Blazing Charge, Stalagmite Vault.</summary>
    private IEnumerable<object> SwordsmanLeapDuals(Player p)
    {
        var foes = new List<Enemy>();
        Enemy Fresh(float dx) { foreach (var f in foes) if (IsInstanceValid(f)) f.QueueFree(); foes.Clear(); var e = ElDummy(p, dx); foes.Add(e); return e; }
        IEnumerable<object> Dodge() { while (p.JumpElementRest > 0 || !p.IsOnFloor()) yield return null; _scInput = new PlayerInput { Dodge = true, JumpHeld = true }; yield return null; _scInput = default; }
        foreach (var _ in ElWait(0.4f)) yield return null;

        // Blazing Charge (earth and fire): a charge forward in a sphere of fire, crashing into each creature: 100 fire
        var foe = Fresh(50);
        foreach (var _ in ElWait(0.2f)) yield return null;
        foreach (var _ in ElPair(p, 1, 2)) yield return null;
        foreach (var _ in Dodge()) yield return null;
        bool shell = false;
        for (float t = 0; t < 0.3f; t += (float)GetProcessDeltaTime()) { shell |= G.World.GetChildren().OfType<BlazeShell>().Any(); yield return null; }
        ScShot("duals_blaze");
        foreach (var _ in ElWait(0.5f)) yield return null;
        ScCheck($"earth and fire: Blazing Charge crashes into the creature in its way ({p.BlazeHits} hit, {foe.MaxHp - foe.Hp:0} damage, alight {foe.Ignited}) in a sphere of fire ({shell})",
            p.ElementalUses.Contains("Blazing Charge") && p.BlazeHits >= 1 && foe.Hp < foe.MaxHp && foe.Ignited && shell);
        ScCheck($"the mix spends the attribute: back to the mode ({p.Mode}, on top {p.Top})", p.Top == HeroElement.None && p.Mode == HeroElement.Earth);

        // Comet (earth and frost): a leap forward, all blue-white; landing, a burst of frost (100 frost) and crystals of ice
        foreach (var _ in ElWait(0.4f)) yield return null;
        foe = Fresh(90);
        foreach (var _ in ElWait(0.2f)) yield return null;
        foreach (var _ in ElPair(p, 1, 3)) yield return null;
        foreach (var _ in Dodge()) yield return null;
        bool glow = false;
        for (float t = 0; t < 1.6f && !p.ElementalUses.Contains("Comet!"); t += (float)GetProcessDeltaTime()) { glow |= p.CometOn && p.Anim.FlashAmount > 0.5f; yield return null; }
        foreach (var _ in ElWait(0.1f)) yield return null;
        ScShot("duals_comet");
        var crystals = p.LastCrystals;
        ScCheck($"earth and frost: Comet flies blue-white ({glow}) and comes down in a burst of frost ({foe.MaxHp - foe.Hp:0} damage, chilled {foe.Chilled}), crystals bursting out of the floor ({crystals?.Spikes.Count})",
            p.ElementalUses.Contains("Comet!") && glow && foe.Hp < foe.MaxHp && (foe.Chilled || foe.FrozenSolid) && crystals != null && crystals.Burst && crystals.Spikes.Count >= 5);
        foreach (var _ in ElWait(1.8f)) yield return null;
        ScCheck($"and the crystals melt away after a moment ({(crystals != null && IsInstanceValid(crystals) ? "standing" : "gone")})", crystals == null || !IsInstanceValid(crystals));

        // Stalagmite Vault (frost overcharged): a leap back, stalagmites of ice bursting up from the ground he passes over
        foreach (var _ in ElWait(0.3f)) yield return null;
        foe = Fresh(-45);
        foreach (var _ in ElWait(0.2f)) yield return null;
        foreach (var _ in ElPair(p, 3, 3)) yield return null;
        int decoys0 = G.World.GetChildren().OfType<IceDecoy>().Count();
        foreach (var _ in Dodge()) yield return null;
        foreach (var _ in ElWait(0.6f)) yield return null;
        ScShot("duals_stalagmite");
        var spikes = p.LastCrystals;
        ScCheck($"frost overcharged: Stalagmite Vault leaps back and raises stalagmites of ice where he passes ({spikes?.Spikes.Count} risen, {spikes?.Struck} struck, frost {foe.FrostBuild:0})",
            p.ElementalUses.Contains("Stalagmite Vault") && spikes != null && !spikes.Burst && spikes.Spikes.Count >= 3 && spikes.Struck >= 1 && (foe.FrostBuild > 0 || foe.Chilled));
        ScCheck($"and leaves no likeness ({G.World.GetChildren().OfType<IceDecoy>().Count() - decoys0} new)", G.World.GetChildren().OfType<IceDecoy>().Count() == decoys0);
        foreach (var _ in ElWait(1.5f)) yield return null;
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

        // the mode controls (the same for every elemental hero)
        foreach (var _ in ModeControlsRun(p)) yield return null;
        // nothing to count: the attack goes as often as the gap between casts allows, every bolt the mode's
        foreach (var _ in ElMode(1, 0.05f)) yield return null;
        int fb0 = p.ElementalUses.Count(u => u == "Firebolt");
        for (int k = 0; k < 6; k++) { p.TestResetBolt(); foreach (var _ in ElPress(new PlayerInput { Attack = true, Aim = new Vector2(p.Facing, 0), AimGiven = true }, Tune.ElementalStaff.ShotGap + 0.15f)) yield return null; }
        ScCheck($"no bolts to count: six attacks in a row in fire mode are six Firebolts ({p.ElementalUses.Count(u => u == "Firebolt") - fb0})", p.ElementalUses.Count(u => u == "Firebolt") - fb0 == 6);
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

        // the bolt: in fire mode, every one a Firebolt
        foreach (var _ in ElMode(1, 0.05f)) yield return null;
        int fbs = p.ElementalUses.Count(u => u == "Firebolt");
        foe.Quench();
        ScShot("elements_hud");
        foreach (var _ in ElWait(1.2f)) yield return null;
        ScShot("elements_glow");
        bool slowed = false;
        for (int k = 0; k < 2; k++)
        {
            Ready();
            _scInput = Bolt(); yield return null; yield return null; _scInput = default;
            for (float t = 0; t < 0.7f; t += (float)GetProcessDeltaTime()) { slowed |= p.CastSlowed; yield return null; }
        }
        ScCheck($"loosing a bolt slows the caster while it plays ({slowed})", slowed);
        ScCheck($"two Firebolts set it alight ({foe.Ignited}), and the mode stays ({p.Mode})", foe.Ignited && p.ElementalUses.Count(u => u == "Firebolt") - fbs == 2 && p.Mode == HeroElement.Fire);

        // the fixed-range spell: Burst (on the burning: combust), Snap (chill, then shatter the frozen), Force Blast
        Ready(); AtSpot();
        int c0 = foe.Combusted;
        if (!foe.Ignited) foe.Ignite(1f, 5f);
        foreach (var _ in ElMode(1, 0.05f)) yield return null;
        var far = ElDummy(p, -150);
        far.Ignite(1f, 5f);
        foreach (var _ in ElPress(Fixed(), 0.4f)) yield return null;
        ScCheck($"fire's fixed-range spell is Burst: on the burning, it combusts ({c0} -> {foe.Combusted}), and so does a burning creature far from the spot ({far.Combusted})", p.ElementalUses.Contains("Burst") && foe.Combusted > c0 && far.Combusted > 0);
        int snaps = 0;
        for (; snaps < 5 && !foe.FrozenSolid; snaps++)
        {
            Ready(); AtSpot();
            foreach (var _ in ElMode(2, 0.05f)) yield return null;
            foreach (var _ in ElPress(Fixed(), 0.3f)) yield return null;
            if (snaps == 1) ScCheck($"frost's is Snap: twice, and it's chilled ({foe.Chilled})", p.ElementalUses.Contains("Snap") && foe.Chilled);
        }
        ScCheck($"more Snaps freeze it solid ({snaps} in all)", foe.FrozenSolid);
        Ready(); AtSpot();
        foreach (var _ in ElMode(2, 0.05f)) yield return null;
        foreach (var _ in ElPress(Fixed(), 0.3f)) yield return null;
        ScCheck($"and on the frozen it shatters ({foe.Shattered})", foe.Shattered >= 1);
        // the frozen anywhere in view shatter, not only at the spot
        Ready(); AtSpot();
        far.Hp = far.MaxHp;
        far.FreezeSolid(5f, force: true);
        foreach (var _ in ElMode(2, 0.05f)) yield return null;
        foreach (var _ in ElPress(Fixed(), 0.3f)) yield return null;
        ScCheck($"Snap shatters a frozen creature far from its spot too ({far.Shattered})", far.Shattered >= 1);
        far.QueueFree();

        // wind: a Gust bolt shoves; the Cyclone at the spot
        foe.QueueFree();
        foe = ElDummy(p, 90, hold: false);
        foreach (var _ in ElWait(0.2f)) yield return null;
        Ready();
        float x0 = foe.GlobalPosition.X;
        foreach (var _ in ElMode(3, 0.05f)) yield return null;
        foreach (var _ in ElPress(Bolt(), 0.8f)) yield return null;
        ScCheck($"a Gust bolt shoves a light creature away ({Math.Abs(foe.GlobalPosition.X - x0):0} px)", p.ElementalUses.Contains("Gust") && Math.Abs(foe.GlobalPosition.X - x0) > 8f);
        Ready();
        foreach (var _ in ElMode(3, 0.05f)) yield return null;
        foreach (var _ in ElPress(Fixed(), 0.9f)) yield return null;
        ScCheck($"wind's fixed-range spell is a Cyclone ({p.LastBlizzard?.El}, pull {p.LastBlizzard?.Pull})", p.ElementalUses.Contains("Cyclone") && p.LastBlizzard != null && p.LastBlizzard.El == HeroElement.Wind && p.LastBlizzard.Pull > 0);
        ScShot("elements_cyclone");
        foreach (var _ in ElWait(1.5f)) yield return null;

        // movement: Fire Walk, then Lift while it rests; Tundra
        Ready();
        float sp0 = p.Stats.MoveSpeed;
        foreach (var _ in ElMode(1, 0.05f)) yield return null;
        foreach (var _ in ElPress(new PlayerInput { Dodge = true }, 0.1f)) yield return null;
        ScCheck($"fire's dodge is Fire Walk: quicker ({sp0:0.00} -> {p.Stats.MoveSpeed:0.00}), fire left behind ({p.LastElementSpell is FireTrail})", p.ElementalUses.Contains("Fire Walk") && p.Stats.MoveSpeed > sp0 && p.LastElementSpell is FireTrail);
        while (!p.IsOnFloor()) yield return null;
        float y0 = p.GlobalPosition.Y, top = y0;
        _scInput = new PlayerInput { Dodge = true }; yield return null; yield return null; _scInput = default;
        for (int f = 0; f < 30; f++) { top = Math.Min(top, p.GlobalPosition.Y); yield return null; }
        ScCheck($"while Fire Walk rests, the dodge is Lift: a push straight up ({y0 - top:0} px)", p.ElementalUses.Contains("Lift") && y0 - top > 30f);
        foreach (var _ in ElWait(Tune.ElementalStaff.WalkSeconds + 0.3f)) yield return null;
        foe.QueueFree();
        foe = ElDummy(p, 60);
        foreach (var _ in ElWait(0.2f)) yield return null;
        Ready();
        foreach (var _ in ElMode(2, 0.05f)) yield return null;
        foreach (var _ in ElPress(new PlayerInput { Dodge = true }, 1.4f)) yield return null;
        ScCheck($"frost's dodge is Tundra: the ground freezes, the creature on it chilled ({foe.Chilled}, {foe.FrostBuild:0} frost) and the hero quicker on it ({p.HasBuff(Player.BuffTailwind)})",
            p.ElementalUses.Contains("Tundra") && foe.Chilled && p.HasBuff(Player.BuffTailwind));
        ScShot("elements_tundra");
        foreach (var _ in ElWait(Tune.ElementalStaff.TundraSeconds)) yield return null;

        // Aid: Narrow Draft, Kindling, Frost Armor
        Ready();
        float sp1 = p.Stats.MoveSpeed;
        foreach (var _ in ElMode(3, 0.05f)) yield return null;
        foreach (var _ in ElPress(new PlayerInput { Ability = true }, 0.1f)) yield return null;
        ScCheck($"wind's Aid is Narrow Draft: faster ({sp1:0.00} -> {p.Stats.MoveSpeed:0.00})", p.HasBuff(Player.BuffTailwind) && p.Stats.MoveSpeed > sp1);
        foreach (var _ in ElWait(Tune.ElementalStaff.TailwindSeconds + 0.3f)) yield return null;
        ScCheck($"and it lasts about {Tune.ElementalStaff.TailwindSeconds:0} s ({p.HasBuff(Player.BuffTailwind)} after)", !p.HasBuff(Player.BuffTailwind));
        Ready();
        foreach (var _ in ElMode(1, 0.05f)) yield return null;
        foreach (var _ in ElPress(new PlayerInput { Ability = true }, 0.1f)) yield return null;
        float k0 = foe.FireBuild;
        foe.GlobalPosition = p.GlobalPosition + new Vector2(90 * p.Facing, -4);
        Ready(); foreach (var _ in ElPress(Bolt(), 0.7f)) yield return null;
        ScCheck($"fire's Aid is Kindling ({k0:0} -> {foe.FireBuild:0} fire)", p.HasBuff(Player.BuffKindling) && (foe.FireBuild > k0 || foe.Ignited));
        Ready();
        foreach (var _ in ElMode(2, 0.05f)) yield return null;
        foreach (var _ in ElPress(new PlayerInput { Ability = true }, 0.1f)) yield return null;
        ScCheck("frost's Aid is Frost Armor", p.HasBuff(Player.BuffFrostArmor));
        ScCheck("the cards the elements replace aren't dealt (Frostbolt, Firestorm, Cinder Snap, Kindling, Deep Chill)",
            new[] { "bolt_frost", "blizzard_fire", "snap_cinder", "kindling", "deepchill" }.All(id => !Upgrades.Available(Upgrades.Get(id), p.Stats)));
        foe.QueueFree();
        foreach (var _ in ElementalistDualsRun()) yield return null;
        foreach (var _ in PairCoverageRun(p)) yield return null;
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

        // Strong Gust: a little whirlwind races along the floor, shoving the creatures it meets back and nicking them
        Clear();
        a = Add(50, hold: false);
        var b2 = Add(80, hold: false);
        foreach (var _ in ElWait(0.2f)) yield return null;
        Ready();
        foreach (var _ in ElPair(p, 3, 3)) yield return null;
        float ga0 = a.GlobalPosition.X, gb0 = b2.GlobalPosition.X, gface = p.Facing;
        foreach (var _ in ElPress(Bolt(), 0.8f)) yield return null;
        var gust = p.LastElementSpell as StrongGust;
        float pa = (a.GlobalPosition.X - ga0) * gface, pb = (b2.GlobalPosition.X - gb0) * gface;
        ScCheck($"Strong Gust drives both creatures back ({pa:0} px, {pb:0} px) and nicks them ({a.MaxHp - a.Hp:0}, {b2.MaxHp - b2.Hp:0})",
            p.ElementalUses.Contains("Strong Gust") && gust != null && pa > 20f && pb > 20f && a.Hp < a.MaxHp && b2.Hp < b2.MaxHp);

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

        // Tornado on the fixed-range button
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
        // Aid's greater wind version: the draft, and the overcharge is spent on it
        Clear();
        Ready();
        foreach (var _ in ElPair(p, 3, 3)) yield return null;
        foreach (var _ in ElPress(new PlayerInput { Ability = true }, 0.1f)) yield return null;
        ScCheck($"overcharged, Aid is wind's Gale Draft: the draft, and the overcharge is spent ({p.Overcharged})", p.ElementalUses.Contains("Gale Draft") && p.HasBuff(Player.BuffTailwind) && !p.Overcharged);
        Clear();
    }

    /// <summary>`--scenario=elements --hero=warden`: the Warden's attack, block and dash, plain, by each element and by each pair.</summary>
    private IEnumerable<object> WardenElementsRun()
    {
        var p = G.Player;
        foreach (var e in G.Enemies.ToArray()) e.QueueFree();
        p.Stats.MaxHp = p.Hp = 5000;
        p.InputOverride = () => { var i = _scInput; _scInput.Infuse = 0; _scInput.Attack = _scInput.Ability = _scInput.Ability2 = _scInput.Dodge = false; return i; };
        foreach (var _ in ElWait(0.5f)) yield return null;
        var start = p.GlobalPosition;
        var aim = new Vector2(p.Facing, 0);
        var foes = new List<Enemy>();
        void Clear() { foreach (var f in foes) if (IsInstanceValid(f)) f.QueueFree(); foes.Clear(); }
        Enemy Add(float dx, bool hold = true) { var e = ElDummy(p, dx, hold); foes.Add(e); return e; }
        void Home() { p.GlobalPosition = start; p.Velocity = Vector2.Zero; p.RefillShield(); p.ResetAbilityCooldowns(); p.TestResetGuards(); }
        PlayerInput Atk() => new() { Attack = true, Aim = aim, AimGiven = true };
        PlayerInput Dash() => new() { Ability = true, Aim = aim, AimGiven = true };
        // the mode of element a (and overcharged with b the same)
        IEnumerable<object> Ready(int a, int b = 0)
        {
            foreach (var _ in ElMode(a, 0.02f)) yield return null;
            if (b == a) p.TestOvercharge();
        }
        // the shield up toward the creature, for a few frames
        IEnumerable<object> Raise() { for (int f = 0; f < 4; f++) { _scInput = new PlayerInput { GuardHeld = true, GuardAim = aim }; yield return null; } }
        IEnumerable<object> Lower() { _scInput = default; foreach (var _ in ElWait(0.3f)) yield return null; }

        ScCheck($"the Warden fights with elements: {string.Join(", ", Elements.Of(p.Stats.Hero))}", p.Elemental && Elements.Of(p.Stats.Hero).SequenceEqual(new[] { HeroElement.Earth, HeroElement.Frost, HeroElement.Wind }));
        foreach (var _ in ElPress(new PlayerInput { Ability2 = true, Aim = aim, AimGiven = true }, 0.1f)) yield return null;
        ScCheck($"he has no Shield Bash ({p.IsShieldBashing}), and its cards aren't dealt", !p.IsShieldBashing && !Upgrades.Available(Upgrades.Get("bash_deflect"), p.Stats));

        // the mode controls (the same for every elemental hero)
        foreach (var _ in ModeControlsRun(p)) yield return null;

        // ---- the attack: each mode, then each overcharged
        var foe = Add(22);
        foreach (var _ in ElWait(0.3f)) yield return null;
        foreach (var _ in Ready(1)) yield return null;
        float h0 = foe.Hp;
        foreach (var _ in ElPress(Atk(), 0.8f)) yield return null;
        float stoneDmg = h0 - foe.Hp;
        ScCheck($"earth mode's cut: Stonecutter ({foe.EarthBuild:0} earth, {stoneDmg:0} damage)", p.ElementalUses.Contains("Stonecutter") && foe.EarthBuild > 1);
        foreach (var _ in Ready(2)) yield return null;
        h0 = foe.Hp;
        foreach (var _ in ElPress(Atk(), 0.8f)) yield return null;
        float rimeDmg = h0 - foe.Hp;
        ScCheck($"frost mode's cut: Rime Edge ({foe.FrostBuild:0} frost, {rimeDmg:0} damage)", p.ElementalUses.Contains("Rime Edge") && foe.FrostBuild > 1);
        foreach (var _ in Ready(3)) yield return null;
        h0 = foe.Hp;
        foreach (var _ in ElPress(Atk(), 0.8f)) yield return null;
        float galeDmg = h0 - foe.Hp;
        // (the paces, swung at nothing: a blow that lands refunds the swing's cooldown)
        Clear();
        float stoneCd = 0, rimeCd = 0, galeCd = 0;
        for (int m = 1; m <= 3; m++)
        {
            foreach (var _ in Ready(m)) yield return null;
            foreach (var _ in ElWait(0.8f)) yield return null;
            _scInput = Atk(); yield return null; _scInput = default;
            float cd = p.SwingCooldownLeft;
            if (m == 1) stoneCd = cd; else if (m == 2) rimeCd = cd; else galeCd = cd;
        }
        ScCheck($"each mode swings at its own pace and weight: earth slow and heavy ({stoneCd:0.00} s, {stoneDmg:0}), frost between ({rimeCd:0.00} s, {rimeDmg:0}), wind quick and light ({galeCd:0.00} s, {galeDmg:0})",
            stoneCd > rimeCd && rimeCd > galeCd && stoneDmg > rimeDmg && rimeDmg > galeDmg && galeDmg > 0);
        Clear(); Home();
        foe = Add(22, hold: false);
        foreach (var _ in ElWait(0.2f)) yield return null;
        float fx0 = foe.GlobalPosition.X;
        foreach (var _ in Ready(3)) yield return null;
        foreach (var _ in ElPress(Atk(), 0.4f)) yield return null;
        ScCheck($"wind mode's cut: Gale Edge throws it back ({(foe.GlobalPosition.X - fx0) * p.Facing:0} px)", p.ElementalUses.Contains("Gale Edge") && (foe.GlobalPosition.X - fx0) * p.Facing > 10f);

        // the pairs on the attack
        Clear(); Home();
        foe = Add(22);
        foreach (var _ in ElWait(0.2f)) yield return null;
        foreach (var _ in Ready(1, 1)) yield return null;
        float hp0 = foe.Hp;
        foreach (var _ in ElPress(Atk(), 0.5f)) yield return null;
        ScCheck($"earth and earth: Boulder Strike, a crushing blow ({hp0 - foe.Hp:0}) that stuns ({foe.Reeling})", p.ElementalUses.Contains("Boulder Strike") && hp0 - foe.Hp > Tune.Warden.Damage * 1.8f);
        Clear(); Home();
        foe = Add(130);
        foreach (var _ in ElWait(0.2f)) yield return null;
        foreach (var _ in Ready(2, 2)) yield return null;
        foreach (var _ in ElPress(Atk(), 0.8f)) yield return null;
        ScCheck($"frost and frost: Icicle Lance, ice thrown on to a creature 130 px off ({foe.MaxHp - foe.Hp:0} damage, chilled {foe.Chilled})", p.ElementalUses.Contains("Icicle Lance") && foe.Hp < foe.MaxHp && (foe.Chilled || foe.FrozenSolid));
        Clear(); Home();
        foe = Add(-20);
        foreach (var _ in ElWait(0.2f)) yield return null;
        foreach (var _ in Ready(3, 3)) yield return null;
        foreach (var _ in ElPress(Atk(), 0.5f)) yield return null;
        ScCheck($"wind and wind: Cyclone Slash, a cut all round strikes behind ({foe.MaxHp - foe.Hp:0})", p.ElementalUses.Contains("Cyclone Slash") && foe.Hp < foe.MaxHp);
        // ---- the block: the mode's own guard as the shield goes up
        float wait = p.Stats.HurtInvuln + 0.1f;
        // earth: the Earth Shield, the ground heaved up in front for as long as the block is held: a wall, nothing through, nothing moving him
        Clear(); Home();
        foreach (var _ in ElWait(0.3f)) yield return null;
        foreach (var _ in Ready(1)) yield return null;
        IEnumerable<object> Hold(float s, PlayerInput extra = default)
        {
            for (float t = 0; t < s; t += (float)GetProcessDeltaTime()) { var i = extra; i.GuardHeld = true; i.GuardAim = aim; _scInput = i; yield return null; }
        }
        foreach (var _ in Hold(0.4f)) yield return null;
        var mound = G.World.GetChildren().OfType<EarthMound>().FirstOrDefault();
        foe = Add(70, hold: false);
        float sh0 = p.ShieldHp, hpa = p.Hp;
        p.Hurt(30f, foe.GlobalPosition, 300f, foe);
        p.Hurt(30f, p.GlobalPosition + new Vector2(-30f * p.Facing, 0), 300f);
        float mx0 = p.GlobalPosition.X, f0 = p.Facing;
        // (held, with a push away and a leap: he stays; the creature coming on meets the wall; measured along the way he faced as it rose)
        float closest = 999f;
        for (float t = 0; t < 2f; t += (float)GetProcessDeltaTime())
        {
            _scInput = new PlayerInput { GuardHeld = true, GuardAim = aim, Move = new Vector2(-p.Facing, 0), Jump = t < 0.4f };
            if (IsInstanceValid(foe)) closest = Math.Min(closest, (foe.GlobalPosition.X - p.GlobalPosition.X) * f0);
            yield return null;
        }
        float wallAt = mound != null && IsInstanceValid(mound) ? (mound.GlobalPosition.X - p.GlobalPosition.X) * f0 : 0f;
        ScCheck($"earth: held, the Earth Shield heaves the ground up in front ({p.EarthShieldUp}, {(mound != null ? "mound" : "none")}) and stands while it's held (2 s on: {mound != null && IsInstanceValid(mound) && !mound.Sinking})",
            p.ElementalUses.Contains("Earth Shield") && p.EarthShieldUp && mound != null && IsInstanceValid(mound) && !mound.Sinking);
        ScCheck($"behind it nothing gets through, from either side ({hpa - p.Hp:0}), the shield isn't worn ({sh0 - p.ShieldHp:0})", p.Hp >= hpa && p.ShieldHp >= sh0);
        ScCheck($"he can't move or leap ({Math.Abs(p.GlobalPosition.X - mx0):0} px), and he kneels behind it", Math.Abs(p.GlobalPosition.X - mx0) < 2f && p.IsOnFloor());
        ScCheck($"it's a wall: the creature coming on stays beyond it (closest {closest:0} px, the wall at {wallAt:0} px)", closest > wallAt);
        ScShot("warden_earthshield");
        _scInput = default;
        foreach (var _ in ElWait(Tune.ElementalWarden.MoundSink + 0.3f)) yield return null;
        ScCheck($"let go, it sinks back ({p.EarthShieldUp}, gone {mound == null || !IsInstanceValid(mound)}) and rests ({p.ModeGuardRest:0.0} s)", !p.EarthShieldUp && (mound == null || !IsInstanceValid(mound)) && p.ModeGuardRest > 0);
        foreach (var _ in Raise()) yield return null;
        ScCheck($"raised again while it rests, the shield is only the shield ({p.EarthShieldUp})", !p.EarthShieldUp);
        foreach (var _ in Lower()) yield return null;
        // frost: the Frost Shield soaks 90% and shatters once it has soaked 30
        Clear(); Home();
        foe = Add(40);
        foreach (var _ in ElWait(0.3f)) yield return null;
        foreach (var _ in Ready(2)) yield return null;
        foreach (var _ in Raise()) yield return null;
        foreach (var _ in Lower()) yield return null;
        hpa = p.Hp;
        p.Hurt(10f, foe.GlobalPosition, 0f, foe);
        float took = hpa - p.Hp;
        ScCheck($"frost: the Frost Shield ({p.ShellElement}) stays after the shield is lowered and soaks most of a blow ({took:0.0} of 10 through, {p.ShellSoaked:0} soaked)", p.ShellElement == HeroElement.Frost && took < 2.5f && p.ShellSoaked > 5f);
        ScShot("warden_frostshield");
        float foeHp = foe.Hp;
        for (int k = 0; k < 4 && p.ShellElement == HeroElement.Frost; k++) { foreach (var _ in ElWait(wait)) yield return null; p.Hurt(10f, foe.GlobalPosition, 0f, foe); }
        ScCheck($"once it has soaked {Tune.ElementalWarden.FrostShellHold:0} it shatters ({p.ShellsShattered}): the creature about him is filled with frost ({foe.FrostBuild:0}, chilled {foe.Chilled}) and struck ({foeHp - foe.Hp:0})",
            p.ShellElement == HeroElement.None && p.ShellsShattered > 0 && (foe.Chilled || foe.FrozenSolid || foe.FrostBuild > 1) && foe.Hp < foeHp);
        // wind: the Wind Barrier soaks 20% and breaks once it has soaked 40
        Clear(); Home();
        foreach (var _ in Ready(3)) yield return null;
        foreach (var _ in Raise()) yield return null;
        foreach (var _ in Lower()) yield return null;
        hpa = p.Hp;
        p.Hurt(10f, p.GlobalPosition + new Vector2(30f * p.Facing, 0), 0f);
        took = hpa - p.Hp;
        ScCheck($"wind: the Wind Barrier ({p.ShellElement}) soaks a little of a blow ({took:0.0} of 10 through)", p.ShellElement == HeroElement.Wind && took > 5f && took < 9.9f);
        ScShot("warden_windbarrier");
        int blows = 1;
        for (int k = 0; k < 8 && p.ShellElement == HeroElement.Wind; k++) { foreach (var _ in ElWait(wait)) yield return null; p.Hurt(50f, p.GlobalPosition + new Vector2(30f * p.Facing, 0), 0f); blows++; }
        ScCheck($"it breaks once it has soaked {Tune.ElementalWarden.WindShellHold:0} ({blows} blows)", p.ShellElement == HeroElement.None && blows >= 4);
        // the pairs on the block
        Clear(); Home();
        foe = Add(-22);
        foreach (var _ in ElWait(0.2f)) yield return null;
        foreach (var _ in Ready(1, 1)) yield return null;
        foreach (var _ in Raise()) yield return null;
        float hpb = p.Hp;
        p.Hurt(30f, foe.GlobalPosition, 0f, foe);
        foreach (var _ in ElWait(0.1f)) yield return null;
        ScCheck($"earth and earth: Bastion ({p.GuardName}) guards all round: a blow from behind is stopped ({hpb - p.Hp:0} through)", p.GuardName == "Bastion" && p.Hp >= hpb);
        foreach (var _ in Lower()) yield return null;
        Clear(); Home();
        foe = Add(22);
        foreach (var _ in ElWait(0.2f)) yield return null;
        foreach (var _ in Ready(2, 2)) yield return null;
        foreach (var _ in Raise()) yield return null;
        p.Hurt(10f, foe.GlobalPosition, 0f, foe);
        foreach (var _ in ElWait(0.2f)) yield return null;
        var wall = p.LastTower;
        ScCheck($"frost and frost: Glacial Bulwark raises a wall of ice ({wall?.Standing}) that takes the blow meant for him ({wall?.Struck} on it)", p.ElementalUses.Contains("Glacial Bulwark") && wall != null && IsInstanceValid(wall) && wall.Standing && wall.Struck > 0);
        ScShot("warden_bulwark");
        if (wall != null && IsInstanceValid(wall)) wall.QueueFree();
        foreach (var _ in ElWait(0.3f)) yield return null;
        p.Hurt(10f, foe.GlobalPosition, 0f, foe);
        foreach (var _ in ElWait(0.2f)) yield return null;
        ScCheck($"with the wall gone, a creature striking the shield is frozen solid ({foe.FrozenSolid})", foe.FrozenSolid);
        foreach (var _ in Lower()) yield return null;
        Clear(); Home();
        foe = Add(30, hold: false);
        foreach (var _ in ElWait(0.2f)) yield return null;
        foreach (var _ in Ready(3, 3)) yield return null;
        float ex0 = Math.Abs(foe.GlobalPosition.X - p.GlobalPosition.X);
        for (int f = 0; f < 30; f++) { _scInput = new PlayerInput { GuardHeld = true, GuardAim = aim }; yield return null; }
        float ex1 = Math.Abs(foe.GlobalPosition.X - p.GlobalPosition.X);
        ScCheck($"wind and wind: Eye of the Storm pushes a creature out ({ex0:0} -> {ex1:0} px)", p.ElementalUses.Contains("Eye of the Storm") && ex1 > ex0 + 10f);
        foreach (var _ in Lower()) yield return null;
        // ---- the dash: each element, then each pair (every elemental charge runs through creatures)
        Clear(); Home();
        foreach (var _ in ElWait(0.2f)) yield return null;
        foreach (var _ in Ready(1)) yield return null;
        float x0 = p.GlobalPosition.X;
        foreach (var _ in ElPress(Dash(), 0.6f)) yield return null;
        float plainRun = Math.Abs(p.GlobalPosition.X - x0);
        Home();
        Home();
        foe = Add(50);
        foreach (var _ in ElWait(0.2f)) yield return null;
        foreach (var _ in Ready(1)) yield return null;
        foreach (var _ in ElPress(Dash(), 0.8f)) yield return null;
        ScCheck($"earth on the dash: Rockslide Charge ploughs through ({foe.MaxHp - foe.Hp:0} damage, earth {foe.EarthBuild:0}, reeling {foe.Reeling})", p.ElementalUses.Contains("Rockslide Charge") && foe.Hp < foe.MaxHp);
        Clear(); Home();
        foe = Add(50);
        foreach (var _ in ElWait(0.2f)) yield return null;
        foreach (var _ in Ready(2)) yield return null;
        foreach (var _ in ElPress(Dash(), 0.8f)) yield return null;
        ScCheck($"frost: Frozen Tundra fills what it passes with frost ({foe.FrostBuild:0}) and freezes the ground ({p.LastElementSpell is TundraField})", p.ElementalUses.Contains("Frozen Tundra") && (foe.FrostBuild > 1 || foe.Chilled) && p.LastElementSpell is TundraField);
        Clear(); Home();
        foreach (var _ in ElWait(0.2f)) yield return null;
        foreach (var _ in Ready(3)) yield return null;
        x0 = p.GlobalPosition.X;
        foreach (var _ in ElPress(Dash(), 0.9f)) yield return null;
        float galeRun = Math.Abs(p.GlobalPosition.X - x0);
        ScCheck($"wind: the Gale Charge runs farther than earth's charge ({plainRun:0} -> {galeRun:0} px)", p.ElementalUses.Contains("Gale Charge") && galeRun > plainRun * 1.3f);
        // the pairs on the dash
        Clear(); Home();
        foe = Add(50);
        foreach (var _ in ElWait(0.2f)) yield return null;
        foreach (var _ in Ready(1, 1)) yield return null;
        foreach (var _ in ElPress(Dash(), 0.8f)) yield return null;
        ScCheck($"earth and earth: Avalanche, the quake and a fissure on ahead ({p.LastElementSpell is Fissure})", p.ElementalUses.Contains("Avalanche") && p.LastElementSpell is Fissure && foe.Hp < foe.MaxHp);
        Clear(); Home();
        foe = Add(50);
        foreach (var _ in ElWait(0.2f)) yield return null;
        foreach (var _ in Ready(2, 2)) yield return null;
        foreach (var _ in ElPress(Dash(), 0.8f)) yield return null;
        ScCheck($"frost and frost: Glacier Charge freezes what it passes solid ({foe.FrozenSolid})", p.ElementalUses.Contains("Glacier Charge") && foe.FrozenSolid);
        Clear(); Home();
        foe = Add(40, hold: false);
        foreach (var _ in ElWait(0.2f)) yield return null;
        foreach (var _ in Ready(3, 3)) yield return null;
        float tx0 = foe.GlobalPosition.X;
        foreach (var _ in ElPress(Dash(), 1.2f)) yield return null;
        ScCheck($"wind and wind: the Tempest Charge carries a creature along ({(foe.GlobalPosition.X - tx0) * p.Facing:0} px)", p.ElementalUses.Contains("Tempest Charge") && (foe.GlobalPosition.X - tx0) * p.Facing > 60f);
        Clear(); Home();
        foreach (var _ in ElWait(0.3f)) yield return null;
        foreach (var _ in Ready(2)) yield return null;
        foreach (var _ in ElWait(1.2f)) yield return null;
        ScShot("warden_hud");
        foreach (var _ in PairCoverageRun(p)) yield return null;
    }
}
