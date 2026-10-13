using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

public partial class Main
{
    private IEnumerator<object> _cardSteps;

    /// <summary>`--scenario=cards --hero=swordsman`: the whole collection hangs together (ids, kinds, rarity, every card applies), and the new upgrades and relics do what they say.</summary>
    private void CardsScenario()
    {
        if (_cardSteps == null) { if (_scT < 0.8f) return; _cardSteps = CardsRun().GetEnumerator(); }
        if (!_cardSteps.MoveNext()) ScEnd();
    }

    private IEnumerable<object> CardsRun()
    {
        var p = G.Player;
        foreach (var e in G.Enemies.ToArray()) e.QueueFree();
        p.Stats.MaxHp = p.Hp = 5000;
        IEnumerable<object> Wait(float s) { for (float t = 0; t < s; t += (float)GetProcessDeltaTime()) yield return null; }
        foreach (var _ in Wait(0.8f)) yield return null;

        // ---------------------------------------------------------------- the collection
        var all = Upgrades.All.ToList();
        ScCheck($"{all.Count} cards, every id its own", all.Select(u => u.Id).Distinct().Count() == all.Count);
        var generic = Upgrades.Chest.Where(u => u.Kind is UpgradeKind.Generic or UpgradeKind.Conditional).ToList();
        ScCheck($"{generic.Count} generic and conditional upgrades (it was 14)", generic.Count >= 40);
        ScCheck($"{Upgrades.Relics.Count} relics, all relic cards with a rarity", Upgrades.Relics.Count >= 45 && Upgrades.Relics.All(u => u.Kind == UpgradeKind.Relic));
        ScCheck($"rarities: {string.Join(", ", Enum.GetValues<RelicRarity>().Select(r => $"{Upgrades.Relics.Count(u => u.Rarity == r)} {r}"))}", Enum.GetValues<RelicRarity>().All(r => Upgrades.Relics.Any(u => u.Rarity == r)));
        var barg = Upgrades.Chest.Where(u => u.Kind == UpgradeKind.RiskReward).ToList();
        ScCheck($"{barg.Count} bargains (the stat swaps that were relics), none a relic", barg.Count >= 30 && barg.All(u => !u.Relic));
        ScCheck($"techniques: {string.Join(", ", Upgrades.Chest.Where(u => u.Technique).Select(u => u.Name))}", Upgrades.Chest.Count(u => u.Technique) >= 3 && Upgrades.Chest.Where(u => u.Technique).All(u => u.Kind == UpgradeKind.Technique));
        ScCheck("a technique is not dealt as chest filler, but milestones offer them", !Upgrades.IsChestFiller(Upgrades.Get("djump")));
        foreach (var hero in Enum.GetValues<HeroKind>())
        {
            var st = new PlayerStats(hero);
            ScCheck($"{hero}: class relics ({Upgrades.RelicPool(st).Count(u => u.For != null)}), {Upgrades.RelicPool(st).Count} in all", Upgrades.RelicPool(st).Any(u => u.For != null));
        }
        // every card applies (to a hero of its own), and describes itself
        int bad = 0; string badId = "";
        foreach (var u in all.Where(u => u.Icon != "skip"))
        {
            var hero = u.For != null ? u.For[0] : HeroKind.Swordsman;
            try { var st = new PlayerStats(hero); Upgrades.Apply(u, st, p); }
            catch (Exception ex) { bad++; badId += u.Id + " (" + ex.GetType().Name + ") "; }
            if (string.IsNullOrWhiteSpace(u.Name) || string.IsNullOrWhiteSpace(u.Desc)) { bad++; badId += u.Id + " (no text) "; }
        }
        p.Stats.MaxHp = p.Hp = 5000;
        ScCheck($"every card applies and has a name and a text ({bad} bad {badId})", bad == 0);
        ScCheck("relic weights follow their rarity (a legendary the least, a common the most)", Upgrades.Get("relic_deathward").Weight < Upgrades.Get("relic_struck_fire").Weight && Upgrades.Get("relic_struck_fire").Weight < Upgrades.Get("relic_trophy").Weight);

        // ---------------------------------------------------------------- doing what they say
        Enemy Dummy(bool elite = false, float dx = 40f, float hp = 1000000f)
        {
            var g = new Goblin { Position = p.GlobalPosition + new Vector2(dx * p.Facing, 0), Elite = elite };
            g.SetMeta("test", true);
            _world.AddChild(g);
            g.Wake(); g.Freeze(120f, hold: true);
            g.MaxHp = g.Hp = hp;
            return g;
        }
        float Hit(Enemy e, float dmg = 100f, DamageKind k = DamageKind.Physical) { float h = e.Hp; e.Hurt(dmg, Vector2.Zero, e.GlobalPosition, k); return h - e.Hp; }
        var foe = Dummy();
        yield return null;
        float plain = Hit(foe); foe.Hp = foe.MaxHp;
        var st0 = p.Stats;

        st0.GenCritChance = 1f;
        float crit = Hit(foe); foe.Hp = foe.MaxHp; st0.GenCritChance = 0f;
        ScCheck($"Keen Eye: a certain crit hits half as hard again ({plain:0} -> {crit:0})", Math.Abs(crit / plain - 1.5f) < 0.02f);
        st0.VsFreshMult = 2f;
        float fresh = Hit(foe); foe.Hp = foe.MaxHp; st0.VsFreshMult = 1f;
        ScCheck($"First Blood: a creature at full health takes more ({fresh:0} against {plain:0})", Math.Abs(fresh / plain - 2f) < 0.02f);
        st0.VsStatusMult = 2f;
        foe.Ignite(0.001f, 30f);
        float stat = Hit(foe); foe.Hp = foe.MaxHp; st0.VsStatusMult = 1f;
        ScCheck($"Opportunist: a burning creature takes more ({stat:0} against {plain:0})", Math.Abs(stat / plain - 2f) < 0.02f);
        var big = Dummy(elite: true, dx: 70f);
        yield return null;
        float bigPlain = Hit(big); big.Hp = big.MaxHp;
        st0.VsBigMult = 2f;
        float bigHit = Hit(big); st0.VsBigMult = 1f;
        ScCheck($"Giant Slayer: an elite takes more ({bigHit:0} against {bigPlain:0})", Math.Abs(bigHit / bigPlain - 2f) < 0.02f);
        big.QueueFree();

        // taking
        st0.ThornsShare = 0.5f;
        float f0 = foe.Hp; p.ClearInvuln(); p.Hurt(40f, foe.GlobalPosition, 0f, foe);
        float back = f0 - foe.Hp; st0.ThornsShare = 0f; foe.Hp = foe.MaxHp;
        ScCheck($"Barbed Hide: a blow falls back on what struck ({back:0.0} of a 40 blow at half)", back > 6f && back < 22f);
        st0.BlowCap = 0.1f;
        float hp1 = p.Hp; p.ClearInvuln(); p.Hurt(2000f, foe.GlobalPosition, 0f, foe);
        float took = hp1 - p.Hp; st0.BlowCap = 0f; p.Hp = p.Stats.MaxHp;
        ScCheck($"Hard Case: no blow takes more than the share ({took:0} of {p.Stats.MaxHp:0}, at most {p.Stats.MaxHp * 0.1f:0})", took <= p.Stats.MaxHp * 0.1f + 1f && took > 1f);
        st0.DeathWard = true;
        p.Hp = 5f; p.ClearInvuln(); p.Hurt(9999f, foe.GlobalPosition, 0f, foe);
        ScCheck($"Death Ward: a killing blow leaves one health ({p.Hp:0.0}, dead {p.Dead})", !p.Dead && p.Hp <= 1.5f);
        st0.DeathWard = false; p.Hp = p.Stats.MaxHp; p.ClearInvuln();
        foreach (var _ in Wait(0.2f)) yield return null;
        st0.RegenShare = 0.5f; p.Hp = 100f;
        foreach (var _ in Wait(6f)) yield return null;
        ScCheck($"Recuperation: mending after a few quiet seconds (100 -> {p.Hp:0})", p.Hp > 400f);
        st0.RegenShare = 0f; p.Hp = p.Stats.MaxHp;

        // the relics, by the events they hear (certain where it is a chance)
        var cThen = (Tune.Relics.ChainChance, Tune.Relics.BountyChance);
        Tune.Relics.ChainChance = 1f; Tune.Relics.BountyChance = 1f;
        foreach (var id in new[] { "relic_soul", "relic_hunger", "relic_bounty", "relic_wither", "relic_chain", "relic_spill", "relic_mark", "relic_overflow", "relic_riposte", "relic_retribution", "relic_scavenger", "relic_levelsurge", "relic_lucky", "relic_midas" })
            Upgrades.Apply(Upgrades.Get(id), p.Stats, p);
        foe.QueueFree(); foreach (var e in G.Enemies.ToArray()) if (IsInstanceValid(e)) e.QueueFree();
        yield return null;
        // a victim with a neighbour: a kill bursts (Soul Lantern), spills (Spillover), arcs (Chain), spreads fire (Wither), drops coins (Bounty)
        var victim = Dummy(dx: 40f, hp: 20f);
        var neighbour = Dummy(dx: 70f);
        yield return null;
        victim.Ignite(0.001f, 30f);
        int gold0 = p.Gold; var coins0 = _world.GetChildren().OfType<CoinPickup>().Count();
        float nb0 = neighbour.Hp;
        victim.Hurt(100000f, Vector2.Zero, victim.GlobalPosition, DamageKind.Physical);
        yield return null;
        ScCheck($"the kill: the victim fell ({victim.Dead}), and its neighbour took harm from the burst, the spill and the arc ({nb0 - neighbour.Hp:0.0}; soul {p.Tally("soul")}, spill {p.Tally("spill")}, chain {p.Tally("chain")})", victim.Dead && nb0 - neighbour.Hp > 10f && p.Tally("soul") >= 1 && p.Tally("spill") >= 1);
        ScCheck($"Wither: the burning victim set its neighbour alight ({neighbour.Ignited}; {p.Tally("wither")})", neighbour.Ignited);
        ScCheck($"Bounty Hunter: coins dropped ({_world.GetChildren().OfType<CoinPickup>().Count() - coins0})", _world.GetChildren().OfType<CoinPickup>().Count() > coins0);
        // a hit: marks (Tracking Mark), and arms nothing; overflow at full health
        Hit(neighbour, 10f); neighbour.Hp = neighbour.MaxHp;
        float marked = neighbour.MarkedUntil;
        ScCheck($"Tracking Mark: the creature hit is marked ({marked > Time.GetTicksMsec() / 1000.0})", marked > Time.GetTicksMsec() / 1000.0);
        ScCheck($"Overflow: a hit at full health put up a barrier ({p.Tally("overflow")}, barrier {p.BarrierHp:0})", p.Tally("overflow") >= 1 && p.BarrierHp > 0f);
        // dodge, hurt, level, chest
        p.Stats.On.RaiseDodge(p);
        ScCheck($"Riposte: the dodge button arms the next blow ({p.NextHitMult:0.0}x)", p.NextHitUntil > Time.GetTicksMsec() / 1000.0 && Math.Abs(p.NextHitMult - 1.5f) < 0.01f);
        neighbour.Hp = neighbour.MaxHp = 1000000f; neighbour.MarkedUntil = 0;
        float armed = Hit(neighbour); neighbour.Hp = neighbour.MaxHp;
        float unarmed = Hit(neighbour); neighbour.Hp = neighbour.MaxHp;
        ScCheck($"and that blow lands harder, once ({armed:0} then {unarmed:0})", armed > unarmed * 1.3f);
        p.Hp = p.Stats.MaxHp * 0.5f; int g1 = p.Gold;
        p.Stats.On.RaiseChest(p);
        ScCheck($"Scavenger: a chest mends and pays ({p.Gold - g1} gold, health {p.Hp:0})", p.Gold == g1 + (int)MathF.Round(5 * p.Stats.GoldMult) && p.Hp > p.Stats.MaxHp * 0.65f);
        var near = Dummy(dx: 40f);
        yield return null;
        float n1 = near.Hp;
        p.Stats.On.RaiseLevel(p);
        ScCheck($"Level Surge: a level gained blasts the creatures about ({n1 - near.Hp:0.0})", n1 - near.Hp > 20f);
        ScCheck($"Midas Mark and Lucky Coin are in effect (gold step {p.Stats.GoldDamageStep}, gold x{p.Stats.GoldMult:0.0})", p.Stats.GoldDamageStep > 0f && p.Stats.GoldMult >= 1.5f);
        Tune.Relics.ChainChance = cThen.Item1; Tune.Relics.BountyChance = cThen.Item2;
    }
}
