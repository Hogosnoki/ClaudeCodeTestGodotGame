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
        _world.AddChild(relic);
        _world.AddChild(vault);
        foreach (var _ in Wait(1.0f)) yield return null;
        ScShot("chestlook_shut");
        ScCheck("a relic chest and a vault chest stand by the hero", IsInstanceValid(relic) && IsInstanceValid(vault));
        relic.OpenBy(Net.Me);
        foreach (var _ in Wait(1.2f)) yield return null;
        ScShot("chestlook_open");
    }
}
