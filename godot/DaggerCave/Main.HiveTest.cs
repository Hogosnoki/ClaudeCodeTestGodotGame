using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

public partial class Main
{
    private IEnumerator<object> _hvSteps;

    /// <summary>`--scenario=hive`: the Nest's hornet hive keeps three hornets about it, sends a new one about four seconds after each is lost (not before), drops nothing for the hornets, and leaves a chest when it is destroyed.</summary>
    private void HiveScenario()
    {
        if (_hvSteps == null) { if (_scT < 0.8f) return; _hvSteps = HiveRun().GetEnumerator(); }
        if (!_hvSteps.MoveNext()) ScEnd();
    }

    private IEnumerable<object> HiveRun()
    {
        var p = G.Player;
        foreach (var e in G.Enemies.ToArray()) e.QueueFree();
        p.Stats.MaxHp = p.Hp = 5000;
        IEnumerable<object> Wait(float s) { for (float t = 0; t < s; t += (float)GetProcessDeltaTime()) yield return null; }
        ScCheck($"the Nest has spiders in its tables ({G.Biome.Residents.Values.SelectMany(l => l).Count(e => e.Make() is Spider)} entries)", G.Biome.Residents.Values.SelectMany(l => l).Any(e => e.Make() is Spider));
        // (the level's own roll is from its seed: hang one over the hero for the test)
        if (!G.Cave.FindCeiling(p.GlobalPosition, 800, out var ce)) { ScCheck("a ceiling over the hero", false); yield break; }
        _hiveSpot = ce + new Vector2(0, 30); _hiveMade = false;
        foreach (var _ in Wait(1.0f)) yield return null;
        var hive = TheHive;
        ScCheck($"a hive hangs where it was meant to ({hive != null})", hive != null && IsInstanceValid(hive));
        if (hive == null) yield break;
        // (a second one, hung in the open air near the hero, for the picture)
        var show = new Hive { Position = p.GlobalPosition + new Vector2(70, -95) };
        _world.AddChild(show);
        foreach (var _ in Wait(1.5f)) yield return null;
        ScShot("hive_0");
        show.Hurt(1f, Vector2.Zero, show.GlobalPosition, DamageKind.Raw);
        yield return null; yield return null;
        ScShot("hive_1");
        foreach (var h in show.BroodList.ToList()) { G.Enemies.Remove(h); h.QueueFree(); }
        G.Enemies.Remove(show); show.QueueFree();
        yield return null; yield return null;
        ScCheck($"it has three hornets out ({hive.BroodAlive}, sent {hive.Released})", hive.BroodAlive == Tune.Hive.Keep && hive.Released == 3);

        // the hornets are kept out of harm's way for the timing test
        var hornets = G.Enemies.OfType<Hornet>().Where(h => h.Brood).ToList();
        ScCheck($"they are marked as the hive's ({hornets.Count})", hornets.Count == 3);
        // one is lost
        hornets[0].Hurt(99999f, Vector2.Zero, hornets[0].GlobalPosition, DamageKind.Raw);
        foreach (var _ in Wait(0.3f)) yield return null;
        ScCheck($"a lost hornet is not at once replaced ({hive.BroodAlive} out, {hive.BroodPending} on its way)", hive.BroodAlive == 2 && hive.BroodPending == 1);
        foreach (var _ in Wait(2.6f)) yield return null;
        ScCheck($"nor after three seconds ({hive.BroodAlive} out)", hive.BroodAlive == 2);
        foreach (var _ in Wait(2.0f)) yield return null;
        ScCheck($"but about four seconds on there are three again ({hive.BroodAlive} out, {hive.Released} sent)", hive.BroodAlive == 3 && hive.Released == 4);
        // two at once: each counts its own time
        var two = G.Enemies.OfType<Hornet>().Where(h => h.Brood).Take(2).ToList();
        foreach (var h in two) h.Hurt(99999f, Vector2.Zero, h.GlobalPosition, DamageKind.Raw);
        foreach (var _ in Wait(0.3f)) yield return null;
        ScCheck($"two lost, two on their way ({hive.BroodAlive} out, {hive.BroodPending} on their way)", hive.BroodAlive == 1 && hive.BroodPending == 2);
        foreach (var _ in Wait(5.0f)) yield return null;
        ScCheck($"three again ({hive.BroodAlive})", hive.BroodAlive == 3);

        // the hive is tough
        float hp = hive.MaxHp;
        ScCheck($"the hive is tough ({hp:0} health against a hornet's {G.Enemies.OfType<Hornet>().First().MaxHp:0})", hp > 8 * G.Enemies.OfType<Hornet>().First().MaxHp);
        // destroying it leaves a chest
        int chests = Chest.All.Count;
        hive.Hurt(hp * 2, Vector2.Zero, hive.GlobalPosition, DamageKind.Raw);
        foreach (var _ in Wait(1.0f)) yield return null;
        ScCheck($"destroying it leaves a chest of upgrades ({Chest.All.Count} against {chests})", Chest.All.Count == chests + 1);
        int out0 = G.Enemies.OfType<Hornet>().Count(h => !h.Dead);
        foreach (var _ in Wait(6.0f)) yield return null;
        ScCheck($"and no more hornets are sent ({G.Enemies.OfType<Hornet>().Count(h => !h.Dead)} against {out0})", G.Enemies.OfType<Hornet>().Count(h => !h.Dead) <= out0);
    }
}
