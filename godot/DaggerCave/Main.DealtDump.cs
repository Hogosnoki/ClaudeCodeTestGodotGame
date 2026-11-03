using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Godot;

namespace DaggerCave;

/// <summary>
/// `--dumpdealt=PATH`: for each hero that grows by the level cards, every card that hero can be dealt today (its own level cards, the
/// milestones, the shared cards, boons, infusions, chest fillers, bargains, relics, corrupted cards), with its text, as Markdown; and quits.
/// A card that waits on another (Requires/When) is listed too, marked. For reviewing the cards against the kits as they are.
/// </summary>
public partial class Main
{
    private void RunDealtDump(string path)
    {
        var sb = new StringBuilder();
        foreach (var h in new[] { HeroKind.Swordsman, HeroKind.Warden, HeroKind.Elementalist, HeroKind.Vitalist, HeroKind.Aegis, HeroKind.Rogue, HeroKind.ShapeShifter, HeroKind.Automaton })
        {
            var s = new PlayerStats(h);
            bool Can(Upgrade u)
            {
                if (Upgrades.Available(u, s)) return true;
                // (one that waits on another card: offered later)
                if (u.Requires == null && u.When == null) return false;
                var (req, when) = (u.Requires, u.When);
                u.Requires = null; u.When = null;
                bool ok = Upgrades.Available(u, s);
                u.Requires = req; u.When = when;
                return ok;
            }
            string Line(Upgrade u) => $"- `{u.Id}` **{u.Name}**{(u.MaxStacks > 1 && u.MaxStacks < 50 ? $" ×{u.MaxStacks}" : "")}{(u.Requires != null || u.When != null ? " *(later)*" : "")}{(u.Grade != CardGrade.None ? $" [{u.Grade}]" : "")}: {u.Desc}";
            sb.AppendLine($"# {h}{(Cards.Grows(h) ? "" : " (own kit)")}\n");
            void Sec(string title, IEnumerable<Upgrade> list)
            {
                var l = list.Where(Can).ToList();
                if (l.Count == 0) return;
                sb.AppendLine($"## {title} ({l.Count})\n");
                foreach (var u in l) sb.AppendLine(Line(u));
                sb.AppendLine();
            }
            Sec("Level cards: own", Cards.Grows(h) ? Cards.Ability : Upgrades.Chest.Where(u => u.Kind == UpgradeKind.Class));
            Sec("Level cards: shared", Cards.Shared);
            Sec("Milestones", Cards.Grows(h) ? Cards.Milestones : Upgrades.Chest.Where(u => u.Technique || u.Alteration));
            Sec("Boons", Boons.All);
            Sec("Infusions", Infusions.All);
            Sec("Chest fillers", Upgrades.Chest.Where(Upgrades.IsChestFiller));
            Sec("Bargains", Upgrades.Chest.Where(u => u.Kind == UpgradeKind.RiskReward).Concat(Upgrades.Bargains));
            Sec("Relics", Upgrades.Relics);
            Sec("Corrupted", Upgrades.Corrupted);
        }
        var f = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        f.StoreString(sb.ToString());
        f.Close();
        GD.Print($"[dumpdealt] wrote {path}");
        SafeQuit.Request(this, 0);
    }
}
