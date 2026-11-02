using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

public partial class Main
{
    private IEnumerator<object> _clSteps;

    /// <summary>`--scenario=chestlook`: a relic chest and a vault's chest beside the hero, shut, then one opened. chestlook_*.png.</summary>
    private void ChestLookScenario()
    {
        if (_clSteps == null) { if (_scT < 0.8f) return; _clSteps = ChestLookRun().GetEnumerator(); }
        if (!_clSteps.MoveNext()) ScEnd();
    }

    private IEnumerable<object> ChestLookRun()
    {
        var p = G.Player;
        foreach (var e in G.Enemies.ToArray()) e.QueueFree();
        IEnumerable<object> Wait(float s) { for (float t = 0; t < s; t += (float)GetProcessDeltaTime()) yield return null; }
        G.Cave.FindFloor(p.GlobalPosition + new Vector2(34, -20), 200, out var f1);
        G.Cave.FindFloor(p.GlobalPosition + new Vector2(-34, -20), 200, out var f2);
        // (close in, to see them)
        _cam.Zoom *= 3.2f;
        var relic = new Chest { Position = f1, Tier = ChestTier.Relic };
        var vault = new Chest { Position = f2, Tier = ChestTier.Relic, Vault = true };
        // (a Relic Collection, a little further off: teal, three gems)
        G.Cave.FindFloor(p.GlobalPosition + new Vector2(80, -20), 200, out var f3);
        var coll = new Chest { Position = f3, Tier = ChestTier.Relic, Collection = true };
        _world.AddChild(relic);
        _world.AddChild(vault);
        _world.AddChild(coll);
        foreach (var _ in Wait(1.0f)) yield return null;
        ScShot("chestlook_shut");
        ScCheck("a relic chest and a vault chest stand by the hero", IsInstanceValid(relic) && IsInstanceValid(vault));
        relic.OpenBy(Net.Me);
        foreach (var _ in Wait(1.2f)) yield return null;
        ScShot("chestlook_open");
    }

    private IEnumerator<object> _slSteps;

    /// <summary>
    /// `--scenario=shrinelook`: four shrines in a row, one of each grade (Uncommon to Legendary), settled, for shrinelook_*.png; then
    /// what a shrine deals matches its flower (its finest card is of its grade exactly), the grades come from the seed the same each
    /// time, and Fortune can make one bloom.
    /// </summary>
    private void ShrineLookScenario()
    {
        if (_slSteps == null) { if (_scT < 0.8f) return; _slSteps = ShrineLookRun().GetEnumerator(); }
        if (!_slSteps.MoveNext()) ScEnd();
    }

    private IEnumerable<object> ShrineLookRun()
    {
        var p = G.Player;
        foreach (var e in G.Enemies.ToArray()) e.QueueFree();
        IEnumerable<object> Wait(float s) { for (float t = 0; t < s; t += (float)GetProcessDeltaTime()) yield return null; }
        var grades = new[] { CardGrade.Uncommon, CardGrade.Rare, CardGrade.Epic, CardGrade.Legendary };
        var shrines = new List<Chest>();
        for (int k = 0; k < 4; k++)
        {
            G.Cave.FindFloor(p.GlobalPosition + new Vector2(-90 + k * 60, -30), 220, out var f);
            var c = new Chest { Position = f, Tier = ChestTier.Wood, Grade = grades[k] };
            _world.AddChild(c);
            shrines.Add(c);
        }
        p.GlobalPosition += new Vector2(0, 0);
        _cam.Zoom *= 1.7f;
        foreach (var _ in Wait(1.0f)) yield return null;
        ScShot("shrinelook_falling");
        foreach (var _ in Wait(4.5f)) yield return null;
        ScShot("shrinelook_landing");
        foreach (var _ in Wait(2.5f)) yield return null;
        ScShot("shrinelook_settled");
        foreach (var _ in Wait(0.35f)) yield return null;
        ScShot("shrinelook_spin");
        // (close up: the plain and the Rare, then the Epic and the Legendary)
        var home = p.GlobalPosition;
        _cam.Zoom *= 2.2f;
        p.GlobalPosition = home + new Vector2(-60, 0);
        foreach (var _ in Wait(1.2f)) yield return null;
        ScShot("shrinelook_close_low");
        p.GlobalPosition = home + new Vector2(60, 0);
        foreach (var _ in Wait(1.2f)) yield return null;
        ScShot("shrinelook_close_high");
        // (turned well round, to show their depth)
        ChestView.TestTurn = 1.0f;
        foreach (var _ in Wait(0.6f)) yield return null;
        ScShot("shrinelook_turned_high");
        p.GlobalPosition = home + new Vector2(-60, 0);
        foreach (var _ in Wait(1.0f)) yield return null;
        ScShot("shrinelook_turned_low");
        ChestView.TestTurn = 0f;
        p.GlobalPosition = home;
        _cam.Zoom /= 2.2f;
        ScCheck($"four shrines, Uncommon to Legendary, settled ({string.Join(", ", shrines.Select(c => $"{c.Grade} lift {c.ShrineLift:0}"))})", shrines.All(c => IsInstanceValid(c) && c.ShrineLift <= 0f));

        // what it deals matches its flower: the finest card is of its grade exactly (many deals, each grade)
        var s = p.Stats;
        int deals = 0, honest = 0;
        var bad = new List<string>();
        foreach (var g in grades)
            for (int n = 0; n < 40; n++)
            {
                var c = new Chest { Position = p.GlobalPosition + new Vector2(0, 400), Tier = ChestTier.Wood, Grade = g };
                _world.AddChild(c);
                c.DealForTest();
                var best = c.Cards.Select(Upgrades.Find).Max(u => u.Grade == CardGrade.None ? CardGrade.Common : u.Grade);
                deals++;
                if (best == c.Grade && c.Grade == g) honest++;
                else if (bad.Count < 6) bad.Add($"{g}: shows {c.Grade}, best {best} ({string.Join(" / ", c.Cards)})");
                c.QueueFree();
            }
        ScCheck($"a shrine's finest card is of its grade exactly, the flower true to its contents ({honest} of {deals}{(bad.Count > 0 ? "; " + string.Join("; ", bad) : "")})", honest == deals);
        int infused = 0;
        for (int n = 0; n < 60; n++)
        {
            var c = new Chest { Position = p.GlobalPosition + new Vector2(0, 400), Tier = ChestTier.Wood, Grade = CardGrade.Legendary };
            _world.AddChild(c);
            c.DealForTest();
            if (c.Cards.Any(id => Infusions.IsInfusion(Upgrades.Find(id)))) infused++;
            c.QueueFree();
        }
        ScCheck($"  a Legendary shrine sometimes holds one of the hero's infusions ({infused} of 60)", !Cards.Grows(s.Hero) || infused > 5);

        // the grade from the seed: the same rolls each time it's seeded the same
        G.Rng = new System.Random(4242); var a = Enumerable.Range(0, 200).Select(_ => Chest.RollShrineGrade(false)).ToList();
        G.Rng = new System.Random(4242); var b = Enumerable.Range(0, 200).Select(_ => Chest.RollShrineGrade(false)).ToList();
        string dist = string.Join(", ", grades.Select(g => $"{g} {a.Count(x => x == g)}"));
        bool boss = Enumerable.Range(0, 200).All(_ => Chest.RollShrineGrade(true) >= CardGrade.Rare);
        ScCheck($"shrine grades come from the seed (the same twice: {a.SequenceEqual(b)}), the finer the rarer ({dist}); a guardian's Rare or better ({boss})",
            a.SequenceEqual(b) && a.Count(x => x == CardGrade.Uncommon) > a.Count(x => x == CardGrade.Rare) && a.Count(x => x == CardGrade.Rare) > a.Count(x => x == CardGrade.Legendary) && a.All(x => x >= CardGrade.Uncommon) && boss);

        // fortune: a lucky looker may make it bloom a grade
        float luck0 = s.Luck;
        s.Luck = 2f;
        int bloomed = 0;
        for (int n = 0; n < 80; n++)
        {
            var c = new Chest { Position = p.GlobalPosition + new Vector2(0, 400), Tier = ChestTier.Wood, Grade = CardGrade.Rare };
            _world.AddChild(c);
            c.DealForTest();
            if (c.Bloomed && c.Grade == CardGrade.Epic) bloomed++;
            c.QueueFree();
        }
        s.Luck = luck0;
        ScCheck($"  with 2 luck a Rare shrine blooms to Epic about half the time ({bloomed} of 80)", bloomed > 20 && bloomed < 60);
        // and the one we watch blooms on screen
        s.Luck = 4f;
        var show = shrines[1];
        show.DealForTest();
        s.Luck = luck0;
        foreach (var _ in Wait(0.8f)) yield return null;
        ScShot("shrinelook_bloomed");
    }
}
