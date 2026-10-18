using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;

namespace DaggerCave;

public partial class Main
{
    private IEnumerator<object> _gridSteps;

    /// <summary>
    /// `--scenario=grid`: the Ember Grid in a run. Materials found in place of what a hero couldn't use (a potion on a full belt, a heart
    /// at full health, a key with no room, gold on a full purse); a side-grade the loadout brings taken back by a milestone's "Back to..."
    /// card, with the upgrades grown on it (grid_revert.png); milestones never dealing side-grades; and the hexes that act in a run: the
    /// potion's Shared Draught and Volatile Brew, the vault's extra cards, Discerning's choice of relics and salvaging a relic for dust.
    /// </summary>
    private void GridScenario()
    {
        if (_gridSteps == null) { if (_scT < 0.8f) return; _gridSteps = GridRun().GetEnumerator(); }
        if (!_gridSteps.MoveNext()) ScEnd();
    }

    private IEnumerable<object> GridRun()
    {
        var p = G.Player;
        foreach (var e in G.Enemies.ToArray()) e.QueueFree();
        IEnumerable<object> Wait(float s) { for (float t = 0; t < s; t += (float)GetProcessDeltaTime()) yield return null; }
        HexGrid.Owned.Clear(); HexGrid.Mats.Clear(); GridFinds.Reset();
        float reagent = Tune.Grid.Reagent, essence = Tune.Grid.Essence, tumbler = Tune.Grid.Tumbler, seal = Tune.Grid.Seal, revert = Tune.Grid.RevertChance;
        Tune.Grid.Reagent = Tune.Grid.Essence = Tune.Grid.Tumbler = Tune.Grid.Seal = Tune.Grid.RevertChance = 1f;
        foreach (var _ in Wait(0.3f)) yield return null;

        // ---------------------------------------------------------------- materials in place of what can't be used
        p.Potions = p.MaxPotions;
        G.Spawn(new PotionPickup { Position = p.GlobalPosition });
        foreach (var _ in Wait(0.3f)) yield return null;
        ScCheck($"a potion on a full belt is a Reagent ({HexGrid.Mat("reagent")}, belt {p.Potions}/{p.MaxPotions})", HexGrid.Mat("reagent") == 1 && p.Potions == p.MaxPotions);
        ScShot("grid_reagent");
        p.Hp = p.Stats.MaxHp;
        G.Spawn(new HeartPickup { Position = p.GlobalPosition });
        foreach (var _ in Wait(0.3f)) yield return null;
        string ess = HexGrid.EssenceKey(p.Hero);
        ScCheck($"a heart at full health is the hero's own essence ({HexGrid.MatName(ess)}: {HexGrid.Mat(ess)})", HexGrid.Mat(ess) == 1);
        p.Keys = p.MaxKeys;
        G.Spawn(new KeyPickup { Position = p.GlobalPosition });
        foreach (var _ in Wait(0.3f)) yield return null;
        ScCheck($"a key with no room is a Tumbler ({HexGrid.Mat("tumbler")})", HexGrid.Mat("tumbler") == 1 && p.Keys == p.MaxKeys);
        bool merchant = Meta.MerchantUnlocked; Meta.MerchantUnlocked = true;
        p.Gold = 10; p.GainGold(1);
        ScCheck("gold on a light purse is just gold", HexGrid.Mat("seal") == 0);
        p.Gold = Tune.Grid.SealPurse; p.GainGold(1);
        ScCheck($"gold on a full purse ({Tune.Grid.SealPurse}+) is a Guild Seal too ({HexGrid.Mat("seal")})", HexGrid.Mat("seal") == 1);
        p.GainGold(1);
        ScCheck($"but not again within {Tune.Grid.SealGap} s", HexGrid.Mat("seal") == 1);
        Meta.MerchantUnlocked = merchant;
        ScCheck($"the run's summary names them ({GridFinds.Line().TrimStart(',', ' ')})", GridFinds.Line().Contains("Reagent") && GridFinds.Line().Contains("Tumbler"));
        p.Potions = 0; p.Keys = 0; p.Gold = 0;

        // ---------------------------------------------------------------- side-grades: brought in the loadout, taken back in the run
        var s = p.Stats;
        var alt = Upgrades.Chest.Where(u => u.Alteration && u.For != null && u.For.Contains(s.Hero)).OrderByDescending(u => Upgrades.GrowsOn(u.Id).Count).FirstOrDefault();
        ScCheck($"the {s.Hero} has a side-grade to bring ({alt?.Name})", alt != null);
        if (alt != null)
        {
            var fields = typeof(PlayerStats).GetFields(BindingFlags.Public | BindingFlags.Instance).Where(f => f.FieldType == typeof(float) || f.FieldType == typeof(int) || f.FieldType == typeof(bool)).ToArray();
            var before = fields.Select(f => f.GetValue(s)).ToArray();
            Upgrades.ApplyLoadoutCard(alt, s, p);
            var grown = Upgrades.GrowsOn(alt.Id).FirstOrDefault(u => Upgrades.Available(u, s));
            if (grown != null) Upgrades.Apply(grown, s, p);
            // (and something else taken meanwhile, which stays)
            s.DamageMult *= 1.1f;
            var milestone = Upgrades.RollMilestone(s, new Random(4));
            var back = milestone.FirstOrDefault(u => u.RevertOf == alt.Id);
            ScCheck($"a milestone offers '{back?.Name}' ({string.Join(", ", milestone.Select(u => u.Name))})", back != null);
            ScCheck("and never a side-grade", Enumerable.Range(0, 200).All(k => Upgrades.RollMilestone(s, new Random(k)).All(u => !u.Alteration)));
            if (back != null)
            {
                _upgradeMenu.Open(milestone.Append(Upgrades.Skip).ToList(), "MILESTONE!", null, UiArt.Surface.Night, UiArt.Rarity.Rare);
                foreach (var _ in Wait(0.5f)) yield return null;
                ScShot("grid_revert");
                _upgradeMenu.Visible = false;
                Upgrades.Apply(back, s, p);
                s.DamageMult /= 1.1f;
                var after = fields.Select(f => f.GetValue(s)).ToArray();
                var differ = fields.Where((f, k) => !(Equals(before[k], after[k]) || before[k] is float a && after[k] is float b && Math.Abs(a - b) < 1e-3f * Math.Max(1f, Math.Abs(a)))).Select(f => f.Name).ToList();
                ScCheck($"'{back.Name}' takes {alt.Name} off{(grown != null ? $" and {grown.Name} with it" : "")}, as if never taken ({(differ.Count == 0 ? "every stat as it was" : string.Join(", ", differ) + " differ")})",
                    s.StackOf(alt.Id) == 0 && (grown == null || s.StackOf(grown.Id) == 0) && differ.Count == 0);
                ScCheck("and it isn't offered again", !Upgrades.RollMilestone(s, new Random(4)).Any(u => u.RevertOf != null));
                ScCheck("the side-grade can be dealt... nowhere in the run (only the loadout brings it)", !Upgrades.RollChest(s, new Random(1), p.GlobalPosition).Any(u => u.Alteration));
            }
        }

        // ---------------------------------------------------------------- the potion's hexes
        HexGrid.Owned.Add("pot_volatile");
        var g = new Goblin { Position = p.GlobalPosition + new Vector2(30 * p.Facing, 0) };
        _world.AddChild(g);
        foreach (var _ in Wait(0.3f)) yield return null;
        float ghp = g.Hp;
        p.Hp = p.Stats.MaxHp * 0.5f; p.Potions = 1;
        float hp0 = p.Hp;
        p.DrinkPotion();
        ScCheck($"Volatile Brew: drinking bursts on the creatures about ({ghp:0} -> {g.Hp:0}) and heals 15% less ({p.Hp - hp0:0.0} at once)", g.Hp < ghp && Math.Abs((p.Hp - hp0) - p.Stats.MaxHp * 0.15f * 0.85f * p.Stats.PotionPower * p.Stats.HealingTakenMult * Tune.Hero.HealingMult) < 1f);
        g.QueueFree();
        HexGrid.Owned.Remove("pot_volatile");

        // ---------------------------------------------------------------- vaults, relic chests, the merchant
        var rng = new Random(7);
        int plainVault = Upgrades.RollVaultCards(s, null, rng).Length;
        foreach (var id in new[] { "key_root", "key_rich", "key_master" }) HexGrid.Owned.Add(id);
        var vault = Upgrades.RollVaultCards(s, null, rng).Select(Upgrades.Get).ToList();
        ScCheck($"Locksmith, Rich Vaults and Master Key: a vault holds {vault.Count} (was {plainVault}), a relic among them", vault.Count == plainVault + 3 && vault.Count(u => u.Relic) == 1 && vault.Distinct().Count() == vault.Count);
        var relic1 = Upgrades.RollChestCards(s, null, rng, p.GlobalPosition, relic: true);
        HexGrid.Owned.Add("rel_choice1"); HexGrid.Owned.Add("rel_choice2");
        var relic3 = Upgrades.RollChestCards(s, null, rng, p.GlobalPosition, relic: true);
        ScCheck($"Discerning: a relic chest holds a choice of 3 relics (was {relic1.Length})", relic1.Length == 1 && relic3.Length == 3 && relic3.All(id => Upgrades.Get(id).Relic) && relic3.Distinct().Count() == 3);
        var chest = new Chest { Position = p.GlobalPosition, Tier = ChestTier.Relic, Cards = relic3 };
        var choices = relic3.Select(Upgrades.Get).ToList(); var locks = choices.Select(_ => (string)null).ToList();
        AddSalvage(chest, choices, locks);
        ScCheck("a relic chest may be salvaged instead", choices.Contains(Upgrades.Salvage));
        chest.Free();
        int dust = HexGrid.Mat("dust");
        HexGrid.Owned.Add("rel_root");
        _lookingChest = null; _state = State.Choosing;
        OnUpgradePicked(Upgrades.Salvage);
        ScCheck($"salvaging gives Relic Dust, two with Antiquarian ({dust} -> {HexGrid.Mat("dust")})", HexGrid.Mat("dust") == dust + 2);
        var card = Upgrades.Chest.First(u => u.Kind == UpgradeKind.Class && u.For != null && u.For.Contains(s.Hero));
        int price = PriceOf(card);
        HexGrid.Owned.Add("shop_favor");
        ScCheck($"Merchant's Favor: his cards cost 20% more ({price} -> {PriceOf(card)})", Math.Abs(PriceOf(card) - price * Tune.Grid.FavorCardMult) <= 1f);

        HexGrid.Owned.Clear(); HexGrid.Mats.Clear();
        Tune.Grid.Reagent = reagent; Tune.Grid.Essence = essence; Tune.Grid.Tumbler = tumbler; Tune.Grid.Seal = seal; Tune.Grid.RevertChance = revert;
    }
}
