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
            clean &= roll.Count == 3 && roll.All(u => Cards.Ability.Contains(u) || Cards.Shared.Contains(u));
            mine &= roll.All(u => u.For == null || u.For.Contains(hero));
            ownSeen += roll.Count(u => u.For != null);
        }
        ScCheck($"  three cards, each the {hero}'s own or anyone's, never the old kits' ({clean}, {mine}; {ownSeen} of 180 the hero's own)", clean && mine && ownSeen >= 90);
        // (every card can be shown: its label and its text)
        var badLabel = new List<string>();
        foreach (var u in Cards.All)
        {
            try { if (string.IsNullOrEmpty(UpgradeMenu.KindLabel(u, hero)) || string.IsNullOrEmpty(Upgrades.DescFor(u, s))) badLabel.Add(u.Id); }
            catch (Exception) { badLabel.Add(u.Id); }
        }
        ScCheck($"  every new card can be shown, its label and text ({Cards.All.Count() - badLabel.Count} of {Cards.All.Count()}{(badLabel.Count > 0 ? "; not " + string.Join(", ", badLabel) : "")})", badLabel.Count == 0);
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
            foreach (var _ in ElWait(2.2f)) yield return null;
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
        float restBefore = p.BigCooldown;
        p.OnKill(knot[0]);
        ScCheck($"Bloodrush: a kill cuts the rest ({restBefore:0.00} -> {p.BigCooldown:0.00} s)", p.BigCooldown < restBefore - Tune.Growth.KillRestCut * 0.9f);

        foreach (var e in knot) if (IsInstanceValid(e)) e.QueueFree();
        p.InputOverride = null;
    }
}
