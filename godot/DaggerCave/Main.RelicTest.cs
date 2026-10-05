using System;
using System.Linq;
using Godot;

namespace DaggerCave;

/// <summary>--scenario=relics: difficulty scaling, threat targeting, relics and their chests (see ScenarioTick).</summary>
public partial class Main
{
    private Chest _scWeb, _scMine, _scTheirs, _scHungHigh;
    private float _scFloorY;

    private static bool Near(float a, float b, float eps = 0.001f) => Math.Abs(a - b) < eps;

    /// <summary>Checks that need no staging: the numbers.</summary>
    private void RelicNumbersCheck()
    {
        // ---- difficulty: difficulty x (players x per-player); health takes all of it, damage a fifth of the way
        ScCheck("one player at the defaults: no scaling", Near(RunSettings.HpFor(1, 1f, 1f, false), 1f) && Near(RunSettings.DmgFor(1, 1f, 1f, false), 1f));
        ScCheck("the difficulty slider alone: half and double", Near(RunSettings.HpFor(1, 0.5f, 1f, false), 0.5f) && Near(RunSettings.HpFor(1, 2f, 1f, false), 2f));
        ScCheck("two players, 1.0 each: double health, +20% damage", Near(RunSettings.HpFor(2, 1f, 1f, false), 2f) && Near(RunSettings.DmgFor(2, 1f, 1f, false), 1.2f));
        ScCheck("six players at the top of both sliders: 2 x (6 x 2) = 24", Near(RunSettings.HpFor(6, 2f, 2f, false), 24f) && Near(RunSettings.DmgFor(6, 2f, 2f, false), 1f + 23f * Tune.Difficulty.DamageShare));
        ScCheck("half difficulty softens damage a little", Near(RunSettings.DmgFor(1, 0.5f, 1f, false), 0.9f));
        ScCheck($"Hard Mode alone: x{Tune.Difficulty.HardHp} health, x{Tune.Difficulty.HardDamage} damage",
            Near(RunSettings.HpFor(1, 1f, 1f, true), Tune.Difficulty.HardHp) && Near(RunSettings.DmgFor(1, 1f, 1f, true), Tune.Difficulty.HardDamage));
        ScCheck("Hard Mode multiplies on top of the party", Near(RunSettings.HpFor(2, 1f, 1f, true), 2f * Tune.Difficulty.HardHp));

        // ---- heroes: three to begin with; the dragon awards one a party member played, only to a run that found none
        {
            var kept = Meta.Unlocked.ToList(); int keptRun = Meta.RunUnlocks;
            Meta.Unlocked.Clear(); foreach (var h0 in new[] { HeroKind.Swordsman, HeroKind.Warden, HeroKind.Vitalist }) Meta.Unlocked.Add(h0);
            Meta.RunUnlocks = 0;
            var r0 = new Random(5);
            ScCheck("three heroes to start: the Swordsman, Warden and Vitalist", Meta.IsReallyUnlocked(HeroKind.Swordsman) && Meta.IsReallyUnlocked(HeroKind.Warden) && Meta.IsReallyUnlocked(HeroKind.Vitalist) && !Meta.IsReallyUnlocked(HeroKind.Elementalist) && !Meta.IsReallyUnlocked(HeroKind.Rogue) && !Meta.IsReallyUnlocked(HeroKind.Aegis));
            ScCheck("the dragon awards nobody when the party played only heroes you have", Meta.AwardAfterDragon(new[] { HeroKind.Swordsman, HeroKind.Warden }, r0) == null);
            var won = Meta.AwardAfterDragon(new[] { HeroKind.Swordsman, HeroKind.Rogue, HeroKind.Aegis }, r0);
            ScCheck($"a party that played the Rogue and the Aegis brings one home ({won})", won is HeroKind w1 && (w1 == HeroKind.Rogue || w1 == HeroKind.Aegis) && Meta.IsReallyUnlocked(w1) && Meta.RunUnlocks == 1);
            ScCheck("and only one: a run that has found a hero gets no award", Meta.AwardAfterDragon(new[] { HeroKind.Elementalist }, r0) == null);
            Meta.Unlocked.Clear(); foreach (var h0 in kept) Meta.Unlocked.Add(h0);
            Meta.RunUnlocks = keptRun;
        }

        // ---- threat: the warden draws enemies, the rogue turns them away, relics move it too
        ScCheck($"warden threat {new PlayerStats(HeroKind.Warden).ThreatDist}, rogue {new PlayerStats(HeroKind.Rogue).ThreatDist}, others 1",
            Near(new PlayerStats(HeroKind.Warden).ThreatDist, 0.85f) && Near(new PlayerStats(HeroKind.Rogue).ThreatDist, 1.15f) && Near(new PlayerStats(HeroKind.Swordsman).ThreatDist, 1f));
        var from = new Vector2(0, 0);
        // the warden at 100 px and the swordsman at 95: the warden seems nearer (85 vs 95) and is chosen
        int pick = Enemy.ChooseHero(from, new[] { (new Vector2(100, 0), 0.85f, false), (new Vector2(95, 0), 1f, false) });
        ScCheck($"an enemy picks the warden over a slightly nearer friend ({pick})", pick == 0);
        pick = Enemy.ChooseHero(from, new[] { (new Vector2(100, 0), 1.15f, false), (new Vector2(110, 0), 1f, false) });
        ScCheck($"and the farther-seeming rogue loses to a farther friend ({pick})", pick == 1);
        pick = Enemy.ChooseHero(from, new[] { (new Vector2(50, 0), 1f, true), (new Vector2(400, 0), 1f, false) });
        ScCheck($"a hidden hero is found only when nobody else is ({pick})", pick == 1);
        pick = Enemy.ChooseHero(from, new[] { (new Vector2(50, 0), 1f, true) });
        ScCheck($"...but is found then ({pick})", pick == 0);
        var bait = new PlayerStats(HeroKind.Swordsman); Upgrades.Apply(Upgrades.Get("relic_bait"), bait, null);
        var shroud = new PlayerStats(HeroKind.Swordsman); Upgrades.Apply(Upgrades.Get("relic_shroud"), shroud, null);
        ScCheck($"the Gaudy Charm makes you seem 10% nearer ({bait.ThreatDist}), the Dim Cloak 10% farther ({shroud.ThreatDist})", Near(bait.ThreatDist, 0.9f) && Near(shroud.ThreatDist, 1.1f));

        // ---- the relics: each one is findable, unique, and a card of its own kind
        var ids = Upgrades.Relics.Select(u => u.Id).ToList();
        ScCheck($"{ids.Count} relics, all with their own ids", ids.Distinct().Count() == ids.Count && ids.Count >= 30);
        ScCheck("every relic is a relic card, found by id", Upgrades.Relics.All(u => u.Kind == UpgradeKind.Relic && Upgrades.Find(u.Id) == u));
        foreach (var hero in Enum.GetValues<HeroKind>())
        {
            var st = new PlayerStats(hero);
            var pool = Upgrades.RelicPool(st);
            ScCheck($"{hero}: {pool.Count} relics to be offered, only generic or their own", pool.Count > 16 && pool.All(u => u.For == null || u.For.Contains(hero)));
            ScCheck($"{hero}: has class relics ({pool.Count(u => u.For != null)})", pool.Any(u => u.For != null));
        }

        // ---- a few relics' numbers
        PlayerStats Take(HeroKind h, string id) { var s = new PlayerStats(h); Upgrades.Apply(Upgrades.Get(id), s, null); return s; }
        var leap = Take(HeroKind.Swordsman, "relic_leap");
        ScCheck($"Long Stride: jump x{leap.JumpMult / new PlayerStats(HeroKind.Swordsman).JumpMult:0.00}, air speed x{leap.AirSpeedMult}", Near(leap.AirSpeedMult, 1.2f) && Near(leap.JumpMult / new PlayerStats(HeroKind.Swordsman).JumpMult, 0.8f));
        var anvil = Take(HeroKind.Swordsman, "relic_anvil");
        ScCheck($"Anvil Grip: attack speed x0.8, damage x1.2 ({anvil.AttackSpeed}, {anvil.DamageMult})", Near(anvil.AttackSpeed, 0.8f) && Near(anvil.DamageMult, 1.2f));
        var quick = Take(HeroKind.Swordsman, "relic_quicksilver");
        ScCheck($"Quicksilver Grip: attack speed x1.2, damage x0.8 ({quick.AttackSpeed}, {quick.DamageMult})", Near(quick.AttackSpeed, 1.2f) && Near(quick.DamageMult, 0.8f));
        var amph = Take(HeroKind.Swordsman, "relic_amphibian");
        ScCheck($"Amphibian Charm: swim x1.3, run x0.85 ({amph.SwimSpeed}, {amph.MoveSpeed / new PlayerStats(HeroKind.Swordsman).MoveSpeed:0.00})", Near(amph.SwimSpeed, 1.3f) && Near(amph.MoveSpeed / new PlayerStats(HeroKind.Swordsman).MoveSpeed, 0.85f));
        var lone = Take(HeroKind.Rogue, "relic_r_lone");
        ScCheck($"Lone Blade: throw x2, recall x1.5, one dagger ({lone.ThrowDamageMult}, {lone.RecallDamageMult}, {lone.LoneThrow})", Near(lone.ThrowDamageMult, 2f) && Near(lone.RecallDamageMult, 1.5f) && lone.LoneThrow);
        var hemo = Take(HeroKind.Rogue, "relic_r_hemo");
        ScCheck($"Hemorrhage: recall bleed always ({hemo.RecallBleed}, {hemo.RecallBleedChance})", hemo.RecallBleed && Near(hemo.RecallBleedChance, 1f));
        var wound = Take(HeroKind.Rogue, "relic_r_wound");
        ScCheck("Undying Wound: bleed never stops", wound.RecallBleed && wound.BleedForever);
        var fury = Take(HeroKind.Vitalist, "relic_v_fury"); var siphon = Take(HeroKind.Vitalist, "relic_v_siphon");
        ScCheck($"Bloodlust/Siphon Crystal trade damage for vital force ({fury.DamageMult:0.00}/{fury.VitalForceGain / Tune.Vitalist.VitalForceGain:0.00}, {siphon.DamageMult:0.00}/{siphon.VitalForceGain / Tune.Vitalist.VitalForceGain:0.00})",
            Near(fury.DamageMult, 1.25f) && Near(fury.VitalForceGain / Tune.Vitalist.VitalForceGain, 0.7f) && Near(siphon.DamageMult, 0.75f) && Near(siphon.VitalForceGain / Tune.Vitalist.VitalForceGain, 1.3f));
        var fire = Take(HeroKind.Swordsman, "relic_s_fire");
        ScCheck($"Lingering Fire: a {fire.ChargeDuration} s charge, cooldown x1.6", Near(fire.ChargeDuration, 5f) && Near(fire.ChargeCooldown, Tune.Swordsman.ChargeCooldown * 1.6f));
        var wsiph = Take(HeroKind.Warden, "relic_w_siphon"); var ram = Take(HeroKind.Warden, "relic_w_ram");
        ScCheck($"Warden: shield siphon {wsiph.ShieldSiphon}, juggernaut {ram.Juggernaut}", wsiph.ShieldSiphon > 0 && ram.Juggernaut);
        var blood = Take(HeroKind.Elementalist, "relic_e_blood");
        ScCheck("Blood Channeling is set", blood.BloodCast);
        ScCheck("a relic is for its hero only", !Upgrades.Available(Upgrades.Get("relic_r_lone"), new PlayerStats(HeroKind.Warden)) && Upgrades.Available(Upgrades.Get("relic_r_lone"), new PlayerStats(HeroKind.Rogue)));
        var taken = new PlayerStats(HeroKind.Swordsman); Upgrades.Apply(Upgrades.Get("relic_anvil"), taken, null);
        ScCheck("a relic already carried isn't offered again", !Upgrades.Available(Upgrades.Get("relic_anvil"), taken));

        // ---- party-wide relics: the depth track moves
        RunRelics.Reset();
        ScCheck($"the dragon lives at depth {Biomes.FinalDepth} by default", Biomes.FinalDepth == 10);
        RunRelics.Note(1, "relic_deeper");
        ScCheck($"Deeper Dark: the dragon's depth is {Biomes.FinalDepth}", Biomes.FinalDepth == 11);
        var exits = Biomes.ChooseExits(9, new Random(3));
        ScCheck($"...so the lair is only at depth 11 ({string.Join(",", exits.Select(e => e.biome.Name + ":" + e.depth))})", exits.All(e => e.biome.Id != BiomeId.Lair || e.depth == 11));
        RunRelics.Note(2, "relic_shortcut"); RunRelics.Note(3, "relic_shortcut");
        ScCheck($"two Hasty Descents: depth {Biomes.FinalDepth}, the dragon x{RunRelics.DragonMult:0.0}", Biomes.FinalDepth == 9 && Near(RunRelics.DragonMult, 1.2f));
        RunRelics.Note(2, "relic_treasure"); RunRelics.Note(1, "relic_locksmith");
        ScCheck($"a Hunter's Map: {RunRelics.ChestBonus:0.0} more chests; a Locksmith's Ring: a second vault ({RunRelics.ExtraVault})", Near(RunRelics.ChestBonus, 0.2f) && RunRelics.ExtraVault);
        ScCheck("who carries what is kept by player", RunRelics.Has(2, "relic_treasure") && !RunRelics.Has(1, "relic_treasure"));
        RunRelics.Reset();
        ScCheck("a new run forgets them", Biomes.FinalDepth == 10 && !RunRelics.ExtraVault);

        // ---- chests dealt by tier
        var rng = new Random(11);
        int total = Enum.GetValues<HeroKind>().Length * 60;
        int relicCards = 0, bossRelics = 0, bad = 0;
        foreach (var hero in Enum.GetValues<HeroKind>())
            for (int k = 0; k < 60; k++)
            {
                var st = new PlayerStats(hero);
                var silver = Upgrades.RollChestCards(st, null, rng, Vector2.Zero, relic: true).Select(Upgrades.Find).ToList();
                var gold = Upgrades.RollChestCards(st, null, rng, Vector2.Zero).Select(Upgrades.Find).ToList();
                relicCards += silver.Count(u => u.Kind == UpgradeKind.Relic);
                bossRelics += gold.Count(u => u.Kind == UpgradeKind.Relic);
                if (silver.Count != 3 || gold.Count != 3 || gold.Count(u => u.Kind == UpgradeKind.Class) != 1 || gold.Count(u => u.Kind == UpgradeKind.Generic || u.Kind == UpgradeKind.Conditional) != 2) bad++;
                if (silver.Any(u => u.Kind != UpgradeKind.Relic) || silver.Select(u => u.Id).Distinct().Count() != 3) bad++;
            }
        ScCheck($"relic chests hold three different relics each ({relicCards} in {total} chests), shrines none ({bossRelics}); a shrine deals a class card and two more ({bad} odd)", relicCards == total * 3 && bossRelics == 0 && bad == 0);
    }

    private void RelicsScenario()
    {
        var p = G.Player;
        switch (_scStep)
        {
            case 0:
            {
                if (_scT < 0.5f) return;
                RelicNumbersCheck();
                foreach (var e in G.Enemies.ToArray()) e.QueueFree();
                // a web chest hung just above the floor beside the hero, a pair of owned chests, each in its own spot
                Vector2 Floor(float dx) => G.Cave.FindFloor(p.GlobalPosition + new Vector2(dx, -40), 200, out var f) ? f : p.GlobalPosition + new Vector2(dx, 12);
                var f0 = Floor(0);
                _scFloorY = f0.Y;
                _scWeb = new Chest { Position = f0 + new Vector2(0, -Tune.Relics.WebHangHeight), Hung = true, LandY = f0.Y };
                _world.AddChild(_scWeb);
                _scMine = new Chest { Position = Floor(-90), Tier = ChestTier.Boss, Owner = Net.Me };
                _world.AddChild(_scMine);
                _scTheirs = new Chest { Position = Floor(-160), Tier = ChestTier.Boss, Owner = Net.Me + 77 };
                _world.AddChild(_scTheirs);
                ScCheck($"a web chest hangs {Tune.Relics.WebHangHeight} px up and can't be opened ({_scWeb.Hung}, {Chest.At(_scWeb.GlobalPosition) == null})", _scWeb.Hung && Chest.At(_scWeb.GlobalPosition) == null);
                ScCheck("your own guardian chest opens for you", Chest.At(_scMine.GlobalPosition + new Vector2(0, -_scMine.ShrineLift)) == _scMine);
                ScCheck("someone else's doesn't, until they've left it", Chest.At(_scTheirs.GlobalPosition + new Vector2(0, -_scTheirs.ShrineLift)) == null);
                _scTheirs.Owner = 0;
                ScCheck("(gone to the party, it does)", Chest.At(_scTheirs.GlobalPosition + new Vector2(0, -_scTheirs.ShrineLift)) == _scTheirs);
                // the hero stands under the web and swings up at it
                p.GlobalPosition = f0 + new Vector2(-12, -13);
                p.Velocity = Vector2.Zero;
                _scStep = 1; _scT = 0;
                break;
            }
            case 1:
                if (_scT < 0.3f) return;
                p.GlobalPosition = new Vector2(_scWeb.GlobalPosition.X - 12, _scFloorY - 13);
                _scInput = new PlayerInput { Attack = true, Aim = new Vector2(0.45f, -1f).Normalized() };
                _scStep = 2; _scT = 0;
                break;
            case 2:
                if (_scT > 0.15f) _scInput = default;
                if (_scT < 2.5f && _scWeb.CutT < 0) return;
                ScCheck($"swinging at the web cuts it ({_scWeb.CutT >= 0})", _scWeb.CutT >= 0);
                _scStep = 3; _scT = 0;
                break;
            case 3:
                if (_scT < 2.2f) return;
                ScCheck($"the chest fell to the floor ({_scWeb.GlobalPosition.Y:0.0} vs {_scFloorY:0.0})", Math.Abs(_scWeb.GlobalPosition.Y - _scFloorY) < 1.5f);
                ScCheck($"it settled upright and is openable ({_scWeb.Tilt:0.00}, hung {_scWeb.Hung})", Math.Abs(_scWeb.Tilt) < 0.05f && !_scWeb.Hung && Chest.At(_scWeb.GlobalPosition + new Vector2(-10, -_scWeb.ShrineLift)) == _scWeb);
                ScEnd();
                break;
        }
    }
}
