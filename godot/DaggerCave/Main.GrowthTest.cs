using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

public partial class Main
{
    private IEnumerator<object> _grSteps;

    /// <summary>
    /// `--scenario=growth --hero=swordsman|warden|elementalist|vitalist|aegis|rogue`, how the heroes grow (Player/Cards.cs): a level brings a
    /// pick of three cards (the hero's own and anyone's, never the old kits' trees); a card makes the special strike harder; "Again" strikes
    /// more; a milestone offers the special's two forms (one taken, they're gone) and the form works; a Twin Stroke holds two strokes;
    /// Bloodrush hastens the special on a kill; a chest's class card is one of the hero's level cards.
    /// </summary>
    private void GrowthScenario()
    {
        if (_grSteps == null) { if (_scT < 0.8f) return; _grSteps = GrowthRun().GetEnumerator(); }
        if (!_grSteps.MoveNext()) ScEnd();
    }

    private IEnumerable<object> GrowthRun()
    {
        var p = G.Player;
        var s = p.Stats;
        var hero = s.Hero;
        foreach (var e in G.Enemies.ToArray()) { G.Enemies.Remove(e); e.QueueFree(); }
        _genSpots.Clear();
        s.MaxHp = p.Hp = 5000;
        p.InputOverride = () => { var i = _scInput; _scInput.Attack = _scInput.Ability = _scInput.Ability2 = _scInput.Dodge = _scInput.Jump = false; return i; };
        foreach (var _ in ElWait(0.5f)) yield return null;
        var home = p.GlobalPosition;
        var aim = new Vector2(p.Facing, 0);
        var rng = new Random(7);

        // ---- a level brings a pick
        int picks0 = p.PendingPicks, miles0 = p.PendingMilestones, lv0 = p.Level;
        p.AddXp(p.XpToNext - p.Xp);
        ScCheck($"a level up ({lv0} -> {p.Level}) brings a pick of cards ({p.PendingPicks - picks0} pending)", p.Level == lv0 + 1 && p.PendingPicks == picks0 + 1);
        bool clean = true, mine = true;
        int ownSeen = 0;
        for (int k = 0; k < 60; k++)
        {
            var roll = Cards.RollLevel(s, rng);
            clean &= roll.Count == 3 && roll.All(u => Cards.Ability.Contains(u) || Cards.Shared.Contains(u) || Boons.IsBoon(u)) && roll.Distinct().Count() == 3;
            mine &= roll.All(u => u.For == null || u.For.Contains(hero));
            ownSeen += roll.Count(u => u.For != null);
        }
        ScCheck($"  three different cards, each the {hero}'s own, a boon or anyone's, never the old kits' ({clean}, {mine}; {ownSeen} of 180 the hero's own)", clean && mine && ownSeen >= 70);
        // ---- boons: graded like loot
        var gradeCount = new Dictionary<CardGrade, int>();
        int boonsSeen = 0;
        for (int k = 0; k < 2000; k++)
            foreach (var u in Cards.RollLevel(s, rng).Where(Boons.IsBoon)) { boonsSeen++; gradeCount[u.Grade] = gradeCount.GetValueOrDefault(u.Grade) + 1; }
        string dist = string.Join(", ", Enum.GetValues<CardGrade>().Skip(1).Select(g => $"{g} {gradeCount.GetValueOrDefault(g) * 100f / Math.Max(1, boonsSeen):0.0}%"));
        ScCheck($"  boons come in every grade, the higher the rarer ({boonsSeen} dealt: {dist})",
            gradeCount.GetValueOrDefault(CardGrade.Common) > gradeCount.GetValueOrDefault(CardGrade.Uncommon) && gradeCount.GetValueOrDefault(CardGrade.Uncommon) > gradeCount.GetValueOrDefault(CardGrade.Rare)
            && gradeCount.GetValueOrDefault(CardGrade.Rare) > gradeCount.GetValueOrDefault(CardGrade.Epic) && gradeCount.GetValueOrDefault(CardGrade.Epic) > gradeCount.GetValueOrDefault(CardGrade.Legendary) && gradeCount.GetValueOrDefault(CardGrade.Legendary) > 0);
        bool epicMile = Enumerable.Range(0, 40).All(_ => Cards.RollMilestone(s, rng).Any(u => u.Grade >= CardGrade.Epic));
        ScCheck($"  every milestone deals something of Epic grade or better ({epicMile})", epicMile);
        {
            // (each boon does what it says: a copy of the hero's stats, before and after)
            var t0 = new PlayerStats(hero);
            var t1 = new PlayerStats(hero);
            foreach (var id in new[] { "b_def_5", "b_atk_5", "b_spd_5", "b_jmp_5", "b_crit_5", "b_ward_5", "b_leap_5", "b_rate_5" }) Upgrades.Get(id).Apply(t1, null);
            ScCheck($"  Legendary boons: damage taken x{t1.DamageTakenMult / t0.DamageTakenMult:0.00}, damage x{t1.DamageMult / t0.DamageMult:0.00}, speed x{t1.MoveSpeed / t0.MoveSpeed:0.00}, jump x{t1.JumpMult / t0.JumpMult:0.00}, crit +{(t1.GenCritChance - t0.GenCritChance) * 100f:0}%, ward {t1.NegateChance * 100f:0}%, air jumps +{t1.ExtraAirJumps}, attack rate x{t1.AttackSpeed / t0.AttackSpeed:0.00}",
                t1.DamageTakenMult < t0.DamageTakenMult * 0.8f && t1.DamageMult > t0.DamageMult * 1.25f && t1.MoveSpeed > t0.MoveSpeed * 1.15f && t1.JumpMult > t0.JumpMult * 1.4f
                && t1.GenCritChance > t0.GenCritChance + 0.1f && t1.NegateChance > 0.1f && t1.ExtraAirJumps == 2 && t1.AttackSpeed > t0.AttackSpeed * 1.15f);
            int jumps0 = p.AirJumpsMax;
            float hp0 = p.Hp, ward0 = s.NegateChance;
            s.NegateChance = 1f; s.ExtraAirJumps++;
            float took = p.Hurt(50f, p.GlobalPosition + new Vector2(20, 0));
            ScCheck($"  a ward shrugs a blow off ({took:0} taken, {hp0 - p.Hp:0} lost); an extra air jump counts ({jumps0} -> {p.AirJumpsMax})", took == 0f && p.Hp == hp0 && p.AirJumpsMax == jumps0 + 1);
            s.NegateChance = ward0; s.ExtraAirJumps--;
        }
        // (every grade side by side, for the picture)
        _upgradeMenu.Open(new[] { "b_def_1", "b_hp_2", "b_crit_3", "b_haste_4", "b_atk_5" }.Select(Upgrades.Get).ToList(), "EVERY GRADE", null, UiArt.Surface.Arcane, UiArt.Rarity.Uncommon);
        foreach (var _ in ElWait(0.6f)) yield return null;
        ScShot($"growth_{hero.ToString().ToLowerInvariant()}_grades");
        _upgradeMenu.Visible = false;
        // (every card can be shown: its label and its text)
        var badLabel = new List<string>();
        foreach (var u in Cards.All)
        {
            try { if (string.IsNullOrEmpty(UpgradeMenu.KindLabel(u, hero)) || string.IsNullOrEmpty(Upgrades.DescFor(u, s))) badLabel.Add(u.Id); }
            catch (Exception) { badLabel.Add(u.Id); }
        }
        // (everything a card's face is drawn from, for every card and every hero, with and without the hero's stats)
        var badFace = new List<string>();
        foreach (var u in Upgrades.All.Concat(Cards.All).Distinct())
            foreach (var h in Enum.GetValues<HeroKind>())
            {
                var hs = new PlayerStats(h);
                foreach (var st in new[] { hs, null })
                {
                    try
                    {
                        _ = UiArt.RarityOf(u); _ = UiArt.RoleOf(u); _ = UiArt.CardSurface(u); _ = UpgradeMenu.KindColor(u.Kind); _ = UpgradeMenu.CategoryColor(u.Icon);
                        _ = Icons.CategoryFor(u); _ = UiArt.RarityName(UiArt.RarityOf(u)); _ = UiArt.RoleName(UiArt.RoleOf(u));
                        if (string.IsNullOrWhiteSpace(UpgradeMenu.KindLabel(u, st?.Hero ?? h)) || string.IsNullOrWhiteSpace(u.Name) || string.IsNullOrWhiteSpace(Upgrades.DescFor(u, st)))
                            badFace.Add($"{u.Id}/{h}{(st == null ? "/nostats" : "")}: empty");
                    }
                    catch (Exception ex) { badFace.Add($"{u.Id}/{h}{(st == null ? "/nostats" : "")}: {ex.GetType().Name} {ex.Message}"); }
                }
            }
        var (bl, bn, bw) = UpgradeMenu.CardWords(new Upgrade { Id = "x_broken_card" }, s, UiArt.Rarity.Common);
        ScCheck($"  even a broken card is never blank ('{bl}' / '{bn}' / '{bw}')", bl != "" && bn != "" && bw != "");
        ScCheck($"  every card's face can be drawn for every hero ({badFace.Count} problems{(badFace.Count > 0 ? ": " + string.Join("; ", badFace.Take(12)) : "")})", badFace.Count == 0);
        ScCheck($"  every new card can be shown, its label and text ({Cards.All.Count() - badLabel.Count} of {Cards.All.Count()}{(badLabel.Count > 0 ? "; not " + string.Join(", ", badLabel) : "")})", badLabel.Count == 0);
        // (the real menus, as a level's pick and a milestone deal them: pictures to look at)
        for (int k = 0; k < 5; k++)
        {
            bool mile = k >= 3;
            var deal = mile ? Cards.RollMilestone(s, rng) : Cards.RollLevel(s, rng);
            deal.Add(Upgrades.Skip);
            _upgradeMenu.Open(deal, mile ? "MILESTONE!" : $"LEVEL {p.Level}: CHOOSE ONE", null, UiArt.Surface.Night, UiArt.Rarity.Rare);
            foreach (var _ in ElWait(0.4f)) yield return null;
            GD.Print($"[scenario] menu {k}: {string.Join(" | ", deal.Select(u => $"{u.Id} '{u.Name}' [{UpgradeMenu.KindLabel(u, hero)}] {Upgrades.DescFor(u, s).Length} chars"))}");
            ScShot($"growth_{hero.ToString().ToLowerInvariant()}_menu{k}");
        }
        _upgradeMenu.Visible = false;
        var chestRoll = Upgrades.RollChest(s, rng, home);
        ScCheck($"  a chest's class card is one of the hero's level cards ({string.Join(", ", chestRoll.Select(u => u.Name))})", chestRoll.Any(u => Cards.Ability.Contains(u)) && !chestRoll.Any(u => u.For != null && !Cards.Ability.Contains(u)));

        // ---- the special, grown by cards (a knot of creatures in its way, the damage dealt read each time)
        float reach = hero switch
        {
            HeroKind.Elementalist or HeroKind.Aegis => Math.Min(Tune.ElementalStaff.FixedRange - 30f, 150f),
            HeroKind.Warden => 60f,
            HeroKind.Rogue => 50f,
            _ => 30f,
        };
        List<Enemy> knot = null;
        IEnumerable<object> Knot()
        {
            if (knot != null) foreach (var e in knot) if (IsInstanceValid(e)) e.QueueFree();
            p.GlobalPosition = home; p.Velocity = Vector2.Zero;
            p.ResetAbilityCooldowns();
            foreach (var _ in ElWait(0.3f)) yield return null;
            float[] offs = hero is HeroKind.Elementalist or HeroKind.Aegis ? new[] { 0f, -24f, 24f, -44f, 44f } : new[] { 0f, 20f, 40f, -20f, -40f };
            knot = offs.Select(o => ElDummy(p, hero == HeroKind.Warden ? 30f + Math.Abs(o) * 1.6f : reach + o)).ToList();
            foreach (var _ in ElWait(0.3f)) yield return null;
        }
        float dealt = 0f;
        IEnumerable<object> Stroke()
        {
            float before = knot.Sum(e => e.Hp);
            _scInput = new PlayerInput { Ability = true, Aim = aim, AimGiven = true }; yield return null; yield return null; _scInput = default;
            // (a picture of the special in the middle of it, the first time)
            foreach (var _ in ElWait(0.3f)) yield return null;
            ScShot($"growth_{hero.ToString().ToLowerInvariant()}_special");
            foreach (var _ in ElWait(1.9f)) yield return null;
            dealt = before - knot.Sum(e => e.Hp);
        }
        foreach (var _ in Knot()) yield return null;
        int uses0 = p.BigUses;
        foreach (var _ in Stroke()) yield return null;
        float plain = dealt;
        ScCheck($"the special as it is: {plain:0} dealt to the knot ({p.BigUses - uses0} use)", plain > 0f && p.BigUses == uses0 + 1);
        var hard = Cards.Ability.First(u => u.For.Contains(hero) && u.Id.EndsWith("_hard"));
        Upgrades.Apply(hard, s, p);
        foreach (var _ in Knot()) yield return null;
        foreach (var _ in Stroke()) yield return null;
        ScCheck($"{hard.Name}: it strikes harder ({dealt:0}, x{dealt / Math.Max(1f, plain):0.00}, want about {Tune.Growth.HardStep:0.00})", dealt > plain * 1.12f);
        float hardDealt = dealt;
        var again = Cards.Ability.First(u => u.For.Contains(hero) && u.Id.EndsWith("_again"));
        Upgrades.Apply(again, s, p);
        foreach (var _ in Knot()) yield return null;
        foreach (var _ in Stroke()) yield return null;
        ScCheck($"{again.Name}: {again.Desc} ({dealt:0} dealt, from {hardDealt:0})", dealt > hardDealt * 1.1f);

        // ---- a milestone: the special's two forms; one taken, the other is gone
        var mroll = Cards.RollMilestone(s, rng);
        var forms = mroll.Where(u => u.Tags.Contains("form")).ToList();
        ScCheck($"a milestone offers the special's two forms ({string.Join(" / ", forms.Select(u => u.Name))}) and one more ({string.Join(", ", mroll.Except(forms).Select(u => u.Name))})", forms.Count == 2 && mroll.Count == 3);
        var form = forms.FirstOrDefault();
        if (form != null)
        {
            Upgrades.Apply(form, s, p);
            var after = Cards.RollMilestone(s, rng);
            ScCheck($"  {form.Name} taken: the next milestone has no form in it ({string.Join(", ", after.Select(u => u.Name))})", !after.Any(u => u.Tags.Contains("form")) && after.Count == 3);
            int stones0 = _world.GetChildren().OfType<SkyStone>().Count();
            foreach (var _ in Knot()) yield return null;
            int stonesSeen = 0;
            float before = knot.Sum(e => e.Hp);
            _scInput = new PlayerInput { Ability = true, Aim = aim, AimGiven = true }; yield return null; yield return null; _scInput = default;
            for (float t = 0; t < 2.4f; t += (float)GetProcessDeltaTime())
            {
                stonesSeen = Math.Max(stonesSeen, _world.GetChildren().OfType<SkyStone>().Count());
                yield return null;
            }
            dealt = before - knot.Sum(e => e.Hp);
            ScShot($"growth_{hero.ToString().ToLowerInvariant()}_form");
            string extra = hero == HeroKind.Elementalist ? $", {stonesSeen} stones in the air at once" : "";
            ScCheck($"  {form.Name} in play: it strikes the knot ({dealt:0} dealt{extra})", dealt > 0f && (hero != HeroKind.Elementalist || stonesSeen >= Tune.Growth.ShowerStones));
        }

        // ---- infusions: Legendary cards that change the special (it leaves its element behind, a zone that keeps on working)
        var infs = Infusions.All.Where(u => u.For.Contains(hero)).ToList();
        int infSeen = 0;
        for (int k = 0; k < 60; k++) if (Cards.RollMilestone(s, rng).Any(Infusions.IsInfusion)) infSeen++;
        ScCheck($"infusions: two for the {hero} ({string.Join(", ", infs.Select(u => u.Name))}), Legendary, dealt by milestones now and then ({infSeen} of 60)",
            infs.Count == 2 && infs.All(u => u.Grade == CardGrade.Legendary) && infSeen >= 6 && infSeen <= 40);
        foreach (var u in infs) Upgrades.Apply(u, s, p);
        foreach (var _ in Knot()) yield return null;
        int zones0 = p.InfusedZones.Count;
        float[] built0 = new float[8];
        foreach (var e in knot) for (int k = 0; k < 8; k++) built0[k] += e.BuiltTotal[k];
        foreach (var _ in Stroke()) yield return null;
        var made = p.InfusedZones.Skip(zones0).Where(IsInstanceValid).ToList();
        float hpMid = knot.Where(IsInstanceValid).Sum(e => e.Hp);
        foreach (var _ in ElWait(1.5f)) yield return null;
        ScShot($"growth_{hero.ToString().ToLowerInvariant()}_infused");
        float lingering = hpMid - knot.Where(IsInstanceValid).Sum(e => e.Hp);
        var els = made.Select(z => z.El).Distinct().ToList();
        float builtNew = 0f;
        foreach (var e in knot) foreach (var el in els) builtNew += e.BuiltTotal[(int)el] - built0[(int)el];
        int strikes = made.Where(IsInstanceValid).Sum(z => z.Struck);
        ScCheck($"  both taken: the special leaves {made.Count} zones ({string.Join(", ", made.Select(z => $"{z.El} r{z.Radius:0} {z.Seconds:0}s"))}), still at work after it ({lingering:0} more dealt, {builtNew:0} built, {strikes} strikes)",
            made.Count >= 2 && made.All(z => z.Infusion) && strikes > 0 && (lingering > 0f || builtNew > 0f));
        float r1 = made.FirstOrDefault(z => z.El == made[0].El)?.Radius ?? 0f;
        var firstEl = made.Count > 0 ? made[0].El : HeroElement.None;
        var first = infs.FirstOrDefault(u => Infusions.SpecOf(u).El == firstEl) ?? infs[0];
        Upgrades.Apply(first, s, p);
        foreach (var _ in Knot()) yield return null;
        int zones1 = p.InfusedZones.Count;
        foreach (var _ in Stroke()) yield return null;
        var asc = p.InfusedZones.Skip(zones1).Where(IsInstanceValid).FirstOrDefault(z => made.Count > 0 && z.El == made[0].El);
        ScCheck($"  {first.Name} taken again: Ascended, wider ({r1:0} -> {asc?.Radius ?? 0:0}) and longer", asc != null && asc.Radius > r1 * 1.15f);
        foreach (var z in p.InfusedZones.Where(IsInstanceValid)) z.QueueFree();

        // ---- a Twin Stroke: two strokes back to back, then it rests
        var twin = Cards.Milestones.First(u => u.Id == "m_twin");
        Upgrades.Apply(twin, s, p);
        foreach (var _ in Knot()) yield return null;
        int u0 = p.BigUses;
        foreach (var _ in Stroke()) yield return null;
        foreach (var _ in Stroke()) yield return null;
        int two = p.BigUses - u0;
        foreach (var _ in Stroke()) yield return null;
        ScCheck($"Twin Stroke: two strokes back to back ({two}), then a third waits on the rest ({p.BigUses - u0 - two}; {p.BigCooldown:0.0} s)", two == 2 && p.BigUses - u0 == 2);

        // ---- Bloodrush: a kill hastens the special
        var rush = Cards.Milestones.First(u => u.Id == "m_bloodrush");
        Upgrades.Apply(rush, s, p);
        // (a short rest may have run out while the last stroke played: spend what's held so a rest is running)
        for (int k = 0; k < 3 && p.BigCooldown <= Tune.Growth.KillRestCut; k++)
        {
            _scInput = new PlayerInput { Ability = true, Aim = aim, AimGiven = true }; yield return null; yield return null; _scInput = default;
            foreach (var _ in ElWait(0.3f)) yield return null;
        }
        float restBefore = p.BigCooldown;
        p.OnKill(knot[0]);
        ScCheck($"Bloodrush: a kill cuts the rest ({restBefore:0.00} -> {p.BigCooldown:0.00} s)", p.BigCooldown < restBefore - Tune.Growth.KillRestCut * 0.9f);

        foreach (var e in knot) if (IsInstanceValid(e)) e.QueueFree();
        p.InputOverride = null;
    }
}
