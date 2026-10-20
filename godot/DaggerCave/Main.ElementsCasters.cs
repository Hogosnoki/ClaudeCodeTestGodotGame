using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

/// <summary>
/// `--scenario=elements --hero=vitalist|aegis`: the elemental Vitalist (nature, wind, water) and Aegis (wind, earth, frost)
/// (<c>Player/Player.VitalistElements.cs</c>, <c>Player/Player.AegisElements.cs</c>): the mode controls, the new elements on creatures
/// (entangled, soaked), each mode's versions on the buttons as a player would press them, and every pair on every button.
/// </summary>
public partial class Main
{
    private IEnumerable<object> VitalistElementsRun()
    {
        var p = G.Player;
        foreach (var e in G.Enemies.ToArray()) e.QueueFree();
        p.Stats.MaxHp = p.Hp = 5000;
        p.Stats.VitalForceMax = 9999; p.SetVitalForce(9999);
        p.InputOverride = () => { var i = _scInput; _scInput.Infuse = 0; _scInput.Attack = _scInput.Ability = _scInput.Ability2 = _scInput.Dodge = false; return i; };
        foreach (var _ in ElWait(0.5f)) yield return null;
        var start = p.GlobalPosition;
        var aim = new Vector2(p.Facing, 0);
        var foes = new List<Enemy>();
        void Clear() { foreach (var f in foes) if (IsInstanceValid(f)) f.QueueFree(); foes.Clear(); }
        Enemy Add(float dx, bool hold = true) { var e = ElDummy(p, dx, hold); foes.Add(e); return e; }
        void Home() { p.GlobalPosition = start; p.Velocity = Vector2.Zero; p.ResetAbilityCooldowns(); p.SetVitalForce(9999); }
        PlayerInput Atk() => new() { Attack = true, Aim = aim, AimGiven = true };
        PlayerInput Hex() => new() { Dodge = true, Aim = aim, AimGiven = true };
        PlayerInput Heal() => new() { Ability = true, Aim = aim, AimGiven = true };
        IEnumerable<object> Ready(int a, int b = 0)
        {
            foreach (var _ in ElMode(a, 0.02f)) yield return null;
            if (b == a) p.TestOvercharge();
            else if (b > 0) p.TestAttribute(Elements.Of(p.Stats.Hero)[b - 1]);
        }

        ScCheck($"the Vitalist fights with elements: {string.Join(", ", Elements.Of(p.Stats.Hero))}, starting in nature mode",
            p.Elemental && Elements.Of(p.Stats.Hero).SequenceEqual(new[] { HeroElement.Nature, HeroElement.Wind, HeroElement.Water }) && Elements.Signature(p.Stats.Hero) == HeroElement.Nature);
        foreach (var _ in ModeControlsRun(p)) yield return null;

        // ---- the new elements on a creature: nature entangles, water soaks (a creature free to act: a held one doesn't bleed)
        var foe = Add(60, hold: false);
        foreach (var _ in ElWait(0.2f)) yield return null;
        foe.Build(HeroElement.Nature, Tune.Elements.Full, 1f, p);
        foreach (var _ in ElWait(0.3f)) yield return null;
        float bled = foe.MaxHp - foe.Hp;
        ScCheck($"a full meter of nature entangles a creature: mired ({foe.Mired}) and bleeding sap ({bled:0} so far)", foe.Entangled == 1 && foe.Mired && bled > 0);
        foe.Build(HeroElement.Water, Tune.Elements.Full, 1f, p);
        foe.Build(HeroElement.Frost, 25f, 1f, p);
        ScCheck($"a full meter of water soaks it ({foe.Soaked}), and frost builds twice as fast in it (25 -> {foe.FrostBuild:0})", foe.Soaked && foe.FrostBuild >= 49f);
        Clear();

        // ---- the drain: each mode
        Home();
        foe = Add(60);
        foreach (var _ in ElWait(0.3f)) yield return null;
        foreach (var _ in Ready(1)) yield return null;
        foreach (var _ in ElPress(Atk(), 0.5f)) yield return null;
        ScCheck($"nature mode's drain: Thorn Drain ({foe.NatureBuild:0} nature)", p.ElementalUses.Contains("Thorn Drain") && foe.NatureBuild > 1);
        Clear(); Home();
        foe = Add(Tune.Vitalist.DrainRange * 1.2f);
        foreach (var _ in ElWait(0.3f)) yield return null;
        foreach (var _ in Ready(2)) yield return null;
        foreach (var _ in ElPress(Atk(), 0.5f)) yield return null;
        ScCheck($"wind mode's drain: Wisp Drain reaches a creature beyond the drain's own reach ({foe.MaxHp - foe.Hp:0} drained)", p.ElementalUses.Contains("Wisp Drain") && foe.Hp < foe.MaxHp);
        Clear(); Home();
        foe = Add(60);
        foreach (var _ in ElWait(0.3f)) yield return null;
        foreach (var _ in Ready(3)) yield return null;
        p.Hp = p.Stats.MaxHp - 500;
        float hp0 = p.Hp;
        foreach (var _ in ElPress(Atk(), 0.5f)) yield return null;
        ScCheck($"water mode's drain: Tide Drain ({foe.WaterBuild:0} water), mending her ({p.Hp - hp0:0.0})", p.ElementalUses.Contains("Tide Drain") && foe.WaterBuild > 1 && p.Hp > hp0);
        // the pairs on the drain
        Clear(); Home();
        foe = Add(60);
        var beside = Add(100);
        foreach (var _ in ElWait(0.3f)) yield return null;
        foreach (var _ in Ready(1, 1)) yield return null;
        foreach (var _ in ElPress(Atk(), 0.6f)) yield return null;
        ScCheck($"nature overcharged: Bramble Lash entangles it ({foe.Entangled}) and lashes the one beside it too ({beside.MaxHp - beside.Hp:0})", p.ElementalUses.Contains("Bramble Lash") && foe.Entangled > 0 && beside.Hp < beside.MaxHp);
        Clear(); Home();
        foe = Add(120, hold: false);
        foreach (var _ in ElWait(0.3f)) yield return null;
        float d0 = Math.Abs(foe.GlobalPosition.X - p.GlobalPosition.X);
        foreach (var _ in Ready(3, 3)) yield return null;
        foreach (var _ in ElPress(Atk(), 0.6f)) yield return null;
        float d1 = Math.Abs(foe.GlobalPosition.X - p.GlobalPosition.X);
        ScCheck($"water overcharged: Riptide soaks it ({foe.Soaked}) and hauls it toward her ({d0:0} -> {d1:0} px)", p.ElementalUses.Contains("Riptide") && foe.Soaked && d1 < d0 - 10f);
        ScShot("vitalist_riptide");

        // ---- the hex: each mode, and pairs
        Clear(); Home();
        foe = Add(40);
        foreach (var _ in ElWait(0.3f)) yield return null;
        foreach (var _ in Ready(1)) yield return null;
        foreach (var _ in ElPress(Hex(), 0.3f)) yield return null;
        ScCheck($"nature mode's hex: Overgrowth fills what's about her with nature ({foe.NatureBuild:0})", p.ElementalUses.Contains("Overgrowth") && foe.NatureBuild > 1);
        ScShot("vitalist_overgrowth");
        Clear(); Home();
        foe = Add(60, hold: false);
        foreach (var _ in ElWait(0.3f)) yield return null;
        d0 = Math.Abs(foe.GlobalPosition.X - p.GlobalPosition.X);
        foreach (var _ in Ready(2)) yield return null;
        foreach (var _ in ElPress(Hex(), 0.4f)) yield return null;
        d1 = Math.Abs(foe.GlobalPosition.X - p.GlobalPosition.X);
        ScCheck($"wind mode's hex: Withering Gale throws it out ({d0:0} -> {d1:0} px)", p.ElementalUses.Contains("Withering Gale") && d1 > d0 + 10f);
        Clear(); Home();
        foe = Add(150, hold: false);
        foreach (var _ in ElWait(0.3f)) yield return null;
        d0 = Math.Abs(foe.GlobalPosition.X - p.GlobalPosition.X);
        foreach (var _ in Ready(2, 2)) yield return null;
        foreach (var _ in ElPress(Hex(), 0.4f)) yield return null;
        d1 = Math.Abs(foe.GlobalPosition.X - p.GlobalPosition.X);
        ScCheck($"wind overcharged: Eye of the Tempest hauls a creature far off in toward her ({d0:0} -> {d1:0} px)", p.ElementalUses.Contains("Eye of the Tempest") && d1 < d0 - 10f);
        Clear(); Home();
        foe = Add(40);
        foreach (var _ in ElWait(0.3f)) yield return null;
        foreach (var _ in Ready(3, 3)) yield return null;
        foreach (var _ in ElPress(Hex(), 0.3f)) yield return null;
        bool pool = G.World.GetChildren().OfType<HealingPool>().Any();
        ScCheck($"water overcharged: Maelstrom soaks what's about her ({foe.Soaked}) and wells up a healing pool ({pool})", p.ElementalUses.Contains("Maelstrom") && foe.Soaked && pool);
        ScShot("vitalist_maelstrom");

        // ---- the heal: each mode
        Clear(); Home();
        foreach (var _ in Ready(1)) yield return null;
        p.Hp = p.Stats.MaxHp * 0.5f;
        hp0 = p.Hp;
        foreach (var _ in ElPress(Heal(), 0.7f)) yield return null;
        float now = p.Hp - hp0;
        foreach (var _ in ElWait(1.5f)) yield return null;
        ScCheck($"nature mode's heal: Regrowth mends some now ({now:0.0}) and more grows back after ({p.Hp - hp0 - now:0.0} more)", p.ElementalUses.Contains("Regrowth") && now > 0 && p.Hp - hp0 > now + 0.5f);
        Home();
        foreach (var _ in Ready(2)) yield return null;
        foreach (var _ in ElPress(Heal(), 0.7f)) yield return null;
        ScCheck($"wind mode's heal: Breath of Life, even with no one hurt, and a wind at her back ({p.HasBuff(Player.BuffTailwind)})", p.ElementalUses.Contains("Breath of Life") && p.HasBuff(Player.BuffTailwind));
        Home();
        foreach (var _ in Ready(3)) yield return null;
        p.GivePoison(200f, 10f);
        bool was = p.Poisoned;
        foreach (var _ in ElPress(Heal(), 0.7f)) yield return null;
        ScCheck($"water mode's heal: Cleansing Wave washes the poison off (poisoned {was} -> {p.Poisoned})", p.ElementalUses.Contains("Cleansing Wave") && was && !p.Poisoned);
        Home();
        p.TestClearBarrier();
        foreach (var _ in Ready(3, 3)) yield return null;
        float bar0 = p.BarrierHp;
        foreach (var _ in ElPress(Heal(), 0.7f)) yield return null;
        ScCheck($"water overcharged: Wellspring puts a barrier of water on her ({bar0:0} -> {p.BarrierHp:0})", p.ElementalUses.Contains("Wellspring") && p.BarrierHp > bar0);
        ScShot("vitalist_hud");
        Clear();
        foreach (var _ in PairCoverageRun(p)) yield return null;
    }

    private IEnumerable<object> AegisElementsRun()
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
        void Home() { p.GlobalPosition = start; p.Velocity = Vector2.Zero; p.ResetAbilityCooldowns(); p.TestResetCooldowns(); p.TestClearBubble(); }
        PlayerInput Shot() => new() { Attack = true, Aim = aim, AimGiven = true };
        PlayerInput Dome() => new() { Dodge = true, Aim = aim, AimGiven = true };
        PlayerInput Ward() => new() { Ability = true, Aim = aim, AimGiven = true };
        IEnumerable<object> Ready(int a, int b = 0)
        {
            foreach (var _ in ElMode(a, 0.02f)) yield return null;
            if (b == a) p.TestOvercharge();
            else if (b > 0) p.TestAttribute(Elements.Of(p.Stats.Hero)[b - 1]);
        }

        ScCheck($"the Aegis fights with elements: {string.Join(", ", Elements.Of(p.Stats.Hero))}, starting in wind mode",
            p.Elemental && Elements.Of(p.Stats.Hero).SequenceEqual(new[] { HeroElement.Wind, HeroElement.Earth, HeroElement.Frost }) && Elements.Signature(p.Stats.Hero) == HeroElement.Wind);
        foreach (var _ in ModeControlsRun(p)) yield return null;

        // ---- the bubble shot: each mode
        Home();
        var foe = Add(90, hold: false);
        foreach (var _ in ElWait(0.3f)) yield return null;
        foreach (var _ in Ready(1)) yield return null;
        // (it comes on toward her as it would; the burst throws it back: the fastest it went away from her)
        float away = 0f, lastX = foe.GlobalPosition.X;
        _scInput = Shot(); yield return null; _scInput = default;
        for (float t = 0; t < 0.8f; t += (float)GetProcessDeltaTime())
        {
            float dt = Math.Max(0.001f, (float)GetProcessDeltaTime());
            away = Math.Max(away, (foe.GlobalPosition.X - lastX) * p.Facing / dt);
            lastX = foe.GlobalPosition.X;
            yield return null;
        }
        ScCheck($"wind mode's shot: Gale Bubble's burst throws the creature back ({away:0} px/s away from her at the fastest)", p.ElementalUses.Contains("Gale Bubble") && away > 80f);
        float d0, d1;
        Clear(); Home();
        foe = Add(90);
        foreach (var _ in ElWait(0.3f)) yield return null;
        foreach (var _ in Ready(2)) yield return null;
        float h0 = foe.Hp;
        foreach (var _ in ElPress(Shot(), 0.8f)) yield return null;
        float stone = h0 - foe.Hp;
        ScCheck($"earth mode's shot: Stone Bubble, heavier ({stone:0} damage, {foe.EarthBuild:0} earth)", p.ElementalUses.Contains("Stone Bubble") && foe.EarthBuild > 1 && stone > Tune.Aegis.BoltDamage);
        foreach (var _ in Ready(3)) yield return null;
        p.TestResetCooldowns();
        foreach (var _ in ElPress(Shot(), 0.8f)) yield return null;
        ScCheck($"frost mode's shot: Rime Bubble ({foe.FrostBuild:0} frost)", p.ElementalUses.Contains("Rime Bubble") && foe.FrostBuild > 1);
        Clear(); Home();
        foreach (var _ in Ready(1, 1)) yield return null;
        _scInput = Shot(); yield return null; _scInput = default;
        int volley = 0;
        var seen = new List<int>();
        for (int f = 0; f < 6; f++)
        {
            int n = G.World.GetChildren().OfType<ElementBolt>().Count(b => b.Version == "Bubble Volley");
            seen.Add(n); volley = Math.Max(volley, n);
            yield return null;
        }
        ScCheck($"wind overcharged: Bubble Volley, three shots fanned ({volley}; frame by frame {string.Join(",", seen)}; {p.LastShotCount} loosed)", p.ElementalUses.Contains("Bubble Volley") && volley >= 3);
        foreach (var _ in ElWait(0.6f)) yield return null;
        ScShot("aegis_volley");
        Clear(); Home();
        foe = Add(90);
        foreach (var _ in ElWait(0.3f)) yield return null;
        foreach (var _ in Ready(3, 3)) yield return null;
        foreach (var _ in ElPress(Shot(), 0.8f)) yield return null;
        ScCheck($"frost overcharged: Frost Orb chills what it bursts on (chilled {foe.Chilled})", p.ElementalUses.Contains("Frost Orb") && (foe.Chilled || foe.FrozenSolid));

        // ---- the barrier: each mode
        Clear(); Home();
        foreach (var _ in Ready(1)) yield return null;
        foreach (var _ in ElPress(Ward(), 0.6f)) yield return null;
        float windWard = p.BarrierHp;
        ScCheck($"wind mode's barrier: Wind Ward ({windWard:0}) and a wind at her back ({p.HasBuff(Player.BuffTailwind)})", p.ElementalUses.Contains("Wind Ward") && windWard > 0 && p.HasBuff(Player.BuffTailwind));
        p.TestClearBarrier();
        Home();
        foreach (var _ in Ready(2)) yield return null;
        foreach (var _ in ElPress(Ward(), 0.6f)) yield return null;
        ScCheck($"earth mode's barrier: Stone Skin, stronger ({p.BarrierHp:0} against {windWard:0})", p.ElementalUses.Contains("Stone Skin") && p.BarrierHp > windWard * 1.2f);
        p.TestClearBarrier();
        Home();
        foreach (var _ in Ready(3)) yield return null;
        foreach (var _ in ElPress(Ward(), 0.6f)) yield return null;
        ScCheck($"frost mode's barrier: Frost Ward, with frost armour ({p.HasBuff(Player.BuffFrostArmor)})", p.ElementalUses.Contains("Frost Ward") && p.HasBuff(Player.BuffFrostArmor));
        ScShot("aegis_ward");

        // ---- the bubble field: each mode
        Clear(); Home();
        foe = Add(50, hold: false);
        foreach (var _ in ElWait(0.3f)) yield return null;
        d0 = Math.Abs(foe.GlobalPosition.X - p.GlobalPosition.X);
        foreach (var _ in Ready(1)) yield return null;
        foreach (var _ in ElPress(Dome(), 0.7f)) yield return null;
        d1 = Math.Abs(foe.GlobalPosition.X - p.GlobalPosition.X);
        float airCap = p.BubbleHp;
        ScCheck($"wind mode's field: Air Dome wraps her ({airCap:0}) and throws the creature out ({d0:0} -> {d1:0} px)", p.ElementalUses.Contains("Air Dome") && airCap > 0 && d1 > d0 + 10f);
        ScShot("aegis_airdome");
        Clear(); Home();
        p.TestClearBubble();
        foreach (var _ in Ready(2)) yield return null;
        foreach (var _ in ElPress(Dome(), 0.7f)) yield return null;
        ScCheck($"earth mode's field: Stone Dome, stronger bubbles ({p.BubbleHp:0} against {airCap:0})", p.ElementalUses.Contains("Stone Dome") && p.BubbleHp > airCap * 1.2f);
        Home();
        foe = Add(50);
        foreach (var _ in ElWait(0.3f)) yield return null;
        foreach (var _ in Ready(3)) yield return null;
        foreach (var _ in ElPress(Dome(), 0.7f)) yield return null;
        ScCheck($"frost mode's field: Ice Dome fills the creature near with frost ({foe.FrostBuild:0})", p.ElementalUses.Contains("Ice Dome") && foe.FrostBuild > 1);
        ScShot("aegis_hud");
        Clear(); Home();
        foreach (var _ in PairCoverageRun(p)) yield return null;
    }
}
