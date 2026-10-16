using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Godot;

namespace DaggerCave;

/// <summary>`--dumpcards=PATH`: writes every card the game holds (upgrades, side-grades, techniques, bargains, relics, corrupted cards), the class perks
/// and the meta trees, from the game's own data, as Markdown, and quits. For balance reviews.</summary>
public partial class Main
{
    private void RunCardDump(string path)
    {
        var sb = new StringBuilder();
        string Stacks(Upgrade u) => u.MaxStacks > 1 && u.MaxStacks < 100 ? $" ×{u.MaxStacks}" : "";
        string Line(Upgrade u) => $"- **{u.Name}**{Stacks(u)}{(u.Tier == UpgradeTier.Ability ? " *(rare)*" : "")}{(u.Multi ? " *(party)*" : "")}: {u.Desc.Replace("{ability}", "ability")}";
        var heroes = Enum.GetValues<HeroKind>();
        sb.AppendLine("## Hero upgrades and side-grades (by ability)\n");
        sb.AppendLine("Class upgrades come in chests (one per chest, for your hero) and milestones; side-grades (alterations) change how an ability works, one per ability, from milestones or the loadout.\n");
        foreach (var h in heroes)
        {
            sb.AppendLine($"### {h}\n");
            foreach (var (key, name) in Upgrades.Abilities(h))
            {
                var cls = Upgrades.Chest.Where(u => u.For != null && u.For.Contains(h) && u.Ability == key && !u.Alteration && u.Kind != UpgradeKind.RiskReward).ToList();
                var alt = Upgrades.Chest.Where(u => u.For != null && u.For.Contains(h) && u.Ability == key && u.Alteration).ToList();
                if (cls.Count + alt.Count == 0) continue;
                sb.AppendLine($"**{name}**");
                foreach (var u in cls) sb.AppendLine(Line(u));
                foreach (var u in alt) sb.AppendLine(Line(u).Replace("- **", "- ◆ side-grade **"));
                sb.AppendLine();
            }
            var other = Upgrades.Chest.Where(u => u.For != null && u.For.Contains(h) && !Upgrades.Abilities(h).Any(a => a.key == u.Ability) && u.Kind is UpgradeKind.Class or UpgradeKind.Alteration).ToList();
            if (other.Count > 0) { sb.AppendLine("**Other**"); foreach (var u in other) sb.AppendLine(Line(u)); sb.AppendLine(); }
        }
        void Section(string title, string note, IEnumerable<Upgrade> list)
        {
            var l = list.ToList();
            if (l.Count == 0) return;
            sb.AppendLine($"## {title} ({l.Count})\n");
            if (note != "") sb.AppendLine(note + "\n");
            foreach (var u in l) sb.AppendLine(Line(u) + (u.For != null ? $" *[{string.Join("/", u.For)}]*" : ""));
            sb.AppendLine();
        }
        Section("Generic upgrades", "Anyone's; the two other cards in a chest.", Upgrades.Chest.Where(u => u.Kind == UpgradeKind.Generic && u.For == null));
        Section("Conditional upgrades", "Generic, offered once something else is true.", Upgrades.Chest.Where(u => u.Kind == UpgradeKind.Conditional));
        Section("Techniques", "A way of getting about, unlocked once (milestones); their upgrades wait for them.", Upgrades.Chest.Where(u => u.Kind == UpgradeKind.Technique));
        Section("Bargains (risk-rewards)", "Something given, something taken; each once (vaults, chests).", Upgrades.Chest.Where(u => u.Kind == UpgradeKind.RiskReward));
        foreach (var r in Enum.GetValues<RelicRarity>())
            Section($"Relics: {r}", r == RelicRarity.Common ? "Relic chests (three to choose from), hoards, the merchant. Rarity sets how often each is dealt (common 1.2, uncommon 1, rare 0.6, legendary 0.3)." : "", Upgrades.Relics.Where(u => u.Rarity == r && !u.Id.StartsWith("rr_")));
        Section("Corrupted cards", "The Null's corrupted chests.", Upgrades.Corrupted);
        sb.Append(HexGrid.Dump());
        var f = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        f.StoreString(sb.ToString());
        f.Close();
        GD.Print($"[dumpcards] wrote {path}: {Upgrades.Chest.Count} chest cards, {Upgrades.Relics.Count} relics, {HexGrid.Nodes.Count} grid hexes");
        SafeQuit.Request(this, 0);
    }
}
