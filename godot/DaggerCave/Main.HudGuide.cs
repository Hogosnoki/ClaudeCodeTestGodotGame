using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

public partial class Main
{
    private IEnumerator<object> _hgSteps;

    /// <summary>
    /// `--scenario=hudguide [--hero=NAME]`: every piece of the HUD says what it is (its tooltip), and the HUD guide (F1) numbers them all
    /// and lists what each is. hudguide_*.png.
    /// </summary>
    private void HudGuideScenario()
    {
        if (_hgSteps == null) { if (_scT < 0.8f) return; _hgSteps = HudGuideRun().GetEnumerator(); }
        if (!_hgSteps.MoveNext()) ScEnd();
    }

    private IEnumerable<object> HudGuideRun()
    {
        var p = G.Player;
        foreach (var e in G.Enemies.ToArray()) e.QueueFree();
        p.PendingMilestones = 1;
        IEnumerable<object> Wait(float s) { for (float t = 0; t < s; t += (float)GetProcessDeltaTime()) yield return null; }
        foreach (var _ in Wait(0.3f)) yield return null;
        int n = _hud.TipCount;
        ScCheck($"the HUD's pieces each say what they are ({n} marked: {string.Join(", ", _hud.TipTitles.Distinct().Take(14))})", n >= 8);
        ScCheck("the abilities and potions are among them", _hud.TipTitles.Contains("Potions") && _hud.TipTitles.Contains("Health") && _hud.TipTitles.Contains("Dodge") || _hud.TipTitles.Count(t => t.Contains("(support)")) > 0);
        Hud.Guide = true;
        foreach (var _ in Wait(0.3f)) yield return null;
        ScShot("hudguide_open");
        ScCheck("the HUD guide opens over the game", Hud.Guide);
        Hud.Guide = false;
        foreach (var _ in Wait(0.2f)) yield return null;
        ScShot("hudguide_closed");
    }

    private IEnumerator<object> _rfSteps;

    /// <summary>`--scenario=ringfx`: the five kinds of ring side by side (plain, burst, ping, dashed, wave), caught part way. ringfx.png.</summary>
    private void RingFxScenario()
    {
        if (_rfSteps == null) { if (_scT < 0.8f) return; _rfSteps = RingFxRun().GetEnumerator(); }
        if (!_rfSteps.MoveNext()) ScEnd();
    }

    private IEnumerable<object> RingFxRun()
    {
        var p = G.Player;
        foreach (var e in G.Enemies.ToArray()) e.QueueFree();
        IEnumerable<object> Wait(float s) { for (float t = 0; t < s; t += (float)GetProcessDeltaTime()) yield return null; }
        var styles = new[] { FxLayer.RingStyle.Plain, FxLayer.RingStyle.Burst, FxLayer.RingStyle.Ping, FxLayer.RingStyle.Dashed, FxLayer.RingStyle.Wave };
        var cols = new[] { new Color(1f, 1f, 0.8f), new Color(1f, 0.6f, 0.3f), new Color(1f, 0.85f, 0.3f), new Color(0.85f, 0.95f, 1f), new Color(0.5f, 1f, 0.6f) };
        for (int k = 0; k < styles.Length; k++)
            G.Fx.Ring(p.GlobalPosition + new Vector2(-200 + k * 100, -60), 34, new Color(cols[k], 0.95f), 1.2f, styles[k]);
        foreach (var _ in Wait(0.45f)) yield return null;
        ScShot("ringfx");
        ScCheck("five kinds of ring drawn", true);
    }
}
