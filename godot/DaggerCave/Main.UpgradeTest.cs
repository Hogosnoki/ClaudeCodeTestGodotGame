using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

public partial class Main
{
    /// <summary>
    /// --upgradetest (headless): the rules of the cards, over many rolls for every hero. A chest
    /// holds one class card and two others and never an alteration; a milestone holds only the
    /// hero's own cards, with an alteration on it while any are left; an ability takes one
    /// alteration; an alteration's upgrades wait for it; Magma Skin waits for the deep; Deep Lungs
    /// and Drowned Lungs exclude each other; a party's chest may hold a friend's class card.
    /// </summary>
    private void RunUpgradeTest()
    {
        bool ok = true;
        void Check(string what, bool cond) { GD.Print($"[upgradetest] {(cond ? "ok  " : "FAIL")} {what}"); ok &= cond; }
        var rng = new Random(1234);
        var heroes = Enum.GetValues<HeroKind>();
        G.Depth = 1;

        // the cards themselves
        Check("Wall Kick is gone from the cards", Upgrades.Find("walljump") == null);
        foreach (var id in new[] { "windrunner", "aegis", "vanguard", "wellspring" })
            Check($"{id} is a class card", Upgrades.Find(id)?.Kind == UpgradeKind.Class);
        foreach (var h in heroes)
        {
            var keys = Upgrades.Abilities(h).Select(a => a.key).ToHashSet();
            var mine = Upgrades.Chest.Where(u => u.For != null && u.For.Contains(h)).ToList();
            Check($"{h}: every class card grows one of its four abilities", mine.All(u => keys.Contains(u.Ability)));
            // (the first is the primary attack, which isn't altered; each of the other three is)
            var abilities = Upgrades.Abilities(h);
            // (the Elementalist's bolts are the exception: Frostbolt alters them)
            bool primaryAltered = h == HeroKind.Elementalist;
            Check($"{h}: {abilities[0].name} {(primaryAltered ? "has" : "has no")} alteration", mine.Any(u => u.Alteration && u.Ability == abilities[0].key) == primaryAltered);
            foreach (var (key, name) in abilities.Skip(1))
                Check($"{h}: {name} has an alteration", mine.Any(u => u.Alteration && u.Ability == key));
        }
        foreach (var u in Upgrades.Chest.Where(u => u.Requires != null && Upgrades.Find(u.Requires).Alteration))
            Check($"{u.Name} grows the same ability as {Upgrades.Get(u.Requires).Name}", u.Ability == Upgrades.Get(u.Requires).Ability && u.Kind == UpgradeKind.Class);

        foreach (var h in heroes)
        {
            // many runs: stats grow card by card as a player's would, and every roll keeps the rules
            int chests = 0, milestones = 0, chestBad = 0, mileBad = 0, noAlteration = 0, twoPerAbility = 0, orphans = 0;
            for (int run = 0; run < 40; run++)
            {
                var s = new PlayerStats(h);
                for (int k = 0; k < 30; k++)
                {
                    bool milestone = k % 4 == 3;
                    var cards = milestone ? Upgrades.RollMilestone(s, rng) : Upgrades.RollChest(s, rng, Vector2.Zero);
                    bool alterationsLeft = Upgrades.Chest.Any(u => u.Alteration && Upgrades.Available(u, s));
                    if (milestone)
                    {
                        milestones++;
                        if (cards.Count != 3 && cards.Count != Upgrades.Chest.Count(u => u.For != null && Upgrades.Available(u, s))) mileBad++;
                        else if (cards.Any(u => u.For == null || !u.For.Contains(h) || !Upgrades.Available(u, s)) || cards.Distinct().Count() != cards.Count) mileBad++;
                        if (alterationsLeft && !cards.Any(u => u.Alteration)) noAlteration++;
                    }
                    else
                    {
                        chests++;
                        int cls = cards.Count(u => u.Kind == UpgradeKind.Class);
                        if (cards.Count != 3 || cards.Any(u => u.Alteration) || cls < 1 && Upgrades.Chest.Any(u => u.Kind == UpgradeKind.Class && Upgrades.Available(u, s))
                            || cards.Count(u => u.For == null) < Math.Min(2, Upgrades.Chest.Count(u => u.For == null && Upgrades.Available(u, s)))
                            || cards.Any(u => !Upgrades.Available(u, s)) || cards.Distinct().Count() != cards.Count) chestBad++;
                    }
                    // take one (the stats only: no hero is needed to count stacks)
                    var take = cards[rng.Next(cards.Count)];
                    s.Stacks[take.Id] = s.StackOf(take.Id) + 1;
                    foreach (var (key, _) in Upgrades.Abilities(h))
                        if (Upgrades.Chest.Count(u => u.Alteration && u.Ability == key && s.StackOf(u.Id) > 0) > 1) twoPerAbility++;
                    foreach (var u in Upgrades.Chest.Where(u => s.StackOf(u.Id) > 0 && u.Requires != null))
                        if (s.StackOf(u.Requires) == 0) orphans++;
                }
            }
            Check($"{h}: {chests} chests each hold a class card and two others, never an alteration ({chestBad} wrong)", chestBad == 0);
            Check($"{h}: {milestones} milestones hold only the hero's own cards ({mileBad} wrong)", mileBad == 0);
            Check($"{h}: every milestone offers an alteration while any are left ({noAlteration} without)", noAlteration == 0);
            Check($"{h}: an ability never takes two alterations ({twoPerAbility})", twoPerAbility == 0);
            Check($"{h}: an alteration's upgrades only come after it ({orphans} early)", orphans == 0);
        }

        // one alteration per ability: the other is locked, and named
        {
            var s = new PlayerStats(HeroKind.Vitalist);
            s.Stacks["hex_burst"] = 1;
            var endless = Upgrades.Get("hex_endless");
            Check("with Blight Burst, Endless Hex isn't offered", !Upgrades.Available(endless, s));
            Check($"and says why ({Upgrades.LockReason(endless, s)})", Upgrades.LockReason(endless, s) == "YOU HAVE BLIGHT BURST");
        }
        // Unyielding Shield keeps Quick Mend and Iron Timing away
        {
            var s = new PlayerStats(HeroKind.Warden);
            s.Stacks["shield_unyielding"] = 1;
            Check("with an Unyielding Shield, no Quick Mend or Iron Timing", !Upgrades.Available(Upgrades.Get("quickmend"), s) && !Upgrades.Available(Upgrades.Get("perfect_soak"), s));
            Check("and Braced is offered", Upgrades.Available(Upgrades.Get("unyielding_more"), s));
        }
        // the lungs
        {
            var s = new PlayerStats(HeroKind.Swordsman);
            s.Stacks["rr_lungs"] = 1;
            Check("with Drowned Lungs, no Deep Lungs", !Upgrades.Available(Upgrades.Get("breath"), s));
            s = new PlayerStats(HeroKind.Swordsman);
            s.Stacks["breath"] = 1;
            Check("with Deep Lungs, no Drowned Lungs", !Upgrades.Available(Upgrades.Get("rr_lungs"), s));
            Check("Drowned Lungs is a side-grade", Upgrades.Get("rr_lungs").Kind == UpgradeKind.SideGrade);
        }
        // Magma Skin waits for the deep
        {
            var s = new PlayerStats(HeroKind.Swordsman);
            var magma = Upgrades.Get("magma");
            G.Depth = Tune.Hero.MagmaSkinFromDepth - 1;
            bool early = Upgrades.Available(magma, s);
            int seen = 0;
            for (int k = 0; k < 400; k++) if (Upgrades.RollChest(s, rng, Vector2.Zero).Contains(magma)) seen++;
            Check($"Magma Skin isn't offered above depth {Tune.Hero.MagmaSkinFromDepth} (available {early}, in {seen} chests)", !early && seen == 0);
            G.Depth = Tune.Hero.MagmaSkinFromDepth;
            seen = 0;
            for (int k = 0; k < 400; k++) if (Upgrades.RollChest(s, rng, Vector2.Zero).Contains(magma)) seen++;
            Check($"from depth {Tune.Hero.MagmaSkinFromDepth} it is (in {seen} of 400 chests), as a conditional card", seen > 0 && magma.Kind == UpgradeKind.Conditional);
            G.Depth = 1;
        }
        // a party's chest: its class card may be a friend's, the other two anyone's
        {
            var s = new PlayerStats(HeroKind.Warden);
            var party = new[] { HeroKind.Warden, HeroKind.Vitalist, HeroKind.Swordsman, HeroKind.Elementalist };
            var whose = new Dictionary<HeroKind, int>();
            int bad = 0;
            for (int k = 0; k < 300; k++)
            {
                var cards = Upgrades.RollChestCards(s, party, rng, Vector2.Zero).Select(Upgrades.Get).ToList();
                var cls = cards.Where(u => u.Kind == UpgradeKind.Class).ToList();
                if (cards.Count != 3 || cls.Count != 1 || cards.Any(u => u.Alteration)) { bad++; continue; }
                foreach (var h in cls[0].For) whose[h] = whose.GetValueOrDefault(h) + 1;
                // a friend's card is locked to them; the rest the dealer can take
                foreach (var u in cards)
                    if ((Upgrades.LockReason(u, s) != null) != (u.For != null && !u.For.Contains(HeroKind.Warden))) bad++;
            }
            Check($"a party's chests hold one class card, for any of them ({string.Join(", ", whose.Select(kv => $"{kv.Key} {kv.Value}"))}; {bad} wrong)",
                bad == 0 && party.All(h => whose.GetValueOrDefault(h) > 0));
        }

        GD.Print(ok ? "[upgradetest] PASS" : "[upgradetest] FAIL");
        SafeQuit.Request(this, ok ? 0 : 1);
    }
}
