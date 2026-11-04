using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

public partial class Main
{
    private IEnumerator<object> _vpSteps;

    /// <summary>
    /// `--scenario=vitalpace --hero=vitalist` (the drain's pace, the rupture's cost, the Bloom's blow): held on one creature the drain
    /// comes as often as it always did (every <c>DrainRetarget</c> s); on two it takes them in turn, twice as often, each no more often
    /// than before; with Many Mouths among four it takes two at a time, the pairs alternating; the rupture costs a heal's worth and
    /// recharges as a heal does; the Bloom strikes each creature for <c>Tune.Big.BloomDamage</c>.
    /// </summary>
    private void VitalPaceScenario()
    {
        if (_vpSteps == null) { if (_scT < 0.8f) return; _vpSteps = VitalPaceRun().GetEnumerator(); }
        if (!_vpSteps.MoveNext()) ScEnd();
    }

    private IEnumerable<object> VitalPaceRun()
    {
        var p = G.Player;
        foreach (var e in G.Enemies.ToArray()) e.QueueFree();
        p.Stats.MaxHp = p.Hp = 5000;
        p.InputOverride = () => { var i = _scInput; _scInput.Dodge = _scInput.Ability = _scInput.Ability2 = _scInput.Attack = false; return i; };
        IEnumerable<object> Wait(float s) { for (float t = 0; t < s; t += (float)GetProcessDeltaTime()) yield return null; }
        foreach (var _ in Wait(0.5f)) yield return null;
        int dir = G.Cave.IsSolid(p.GlobalPosition + new Vector2(p.Facing * 120, -10)) ? -(int)p.Facing : (int)p.Facing;
        Enemy Dummy(float dx)
        {
            var e = new Goblin { Position = p.GlobalPosition + new Vector2(dir * dx, -4) };
            e.SetMeta("test", true);
            _world.AddChild(e);
            e.Wake();
            e.Hp = e.MaxHp = 1000000f;
            e.Pin(999f);
            return e;
        }
        var log = new List<Enemy>();
        var casts = new List<List<Enemy>>();
        int started = 0;
        p.TestDrained = e => { log.Add(e); if (p.AttacksStarted != started) { started = p.AttacksStarted; casts.Add(new List<Enemy>()); } casts[^1].Add(e); };
        const float hold = 3f;
        float gap = Tune.Vitalist.DrainRetarget / Math.Max(0.2f, p.Stats.AttackSpeed);
        IEnumerable<object> Hold()
        {
            log.Clear(); casts.Clear(); started = p.AttacksStarted;
            _scInput = new PlayerInput { AttackHeld = true, Aim = new Vector2(dir, 0.1f).Normalized(), AimGiven = true };
            foreach (var _ in Wait(hold)) yield return null;
            _scInput = default;
            foreach (var _ in Wait(0.8f)) yield return null;
        }

        // ---- one creature: as often as ever
        var a = Dummy(90);
        foreach (var _ in Wait(0.3f)) yield return null;
        foreach (var _ in Hold()) yield return null;
        int one = log.Count;
        float want = hold / gap;
        ScCheck($"on one creature the drain comes every {gap:0.###} s as it always did ({one} in {hold:0} s, want about {want:0.#})", one >= want - 1.2f && one <= want + 1.2f);

        // ---- two: in turn, twice as often, each no more often than before
        var b = Dummy(115);
        foreach (var _ in Wait(0.3f)) yield return null;
        foreach (var _ in Hold()) yield return null;
        int two = log.Count, onA = log.Count(e => e == a), onB = log.Count(e => e == b);
        bool turns = Enumerable.Range(1, Math.Max(0, log.Count - 1)).All(k => log[k] != log[k - 1]);
        ScCheck($"on two it takes them in turn, twice as often ({two} in {hold:0} s; {onA} and {onB}; alternating {turns})", two >= 2 * one - 2 && turns && Math.Abs(onA - onB) <= 1);
        ScCheck($"and neither is drained more often than one alone was ({onA}, {onB} vs {one})", onA <= one + 1 && onB <= one + 1);

        // ---- Many Mouths among four: two at a time, the pairs alternating
        Upgrades.Apply(Upgrades.Get("mouths"), p.Stats, p);
        var c = Dummy(140); var d = Dummy(165);
        foreach (var _ in Wait(0.3f)) yield return null;
        foreach (var _ in Hold()) yield return null;
        bool pairs = casts.Count >= 2 && casts.All(x => x.Count == 2);
        bool apart = Enumerable.Range(1, Math.Max(0, casts.Count - 1)).All(k => !casts[k].Intersect(casts[k - 1]).Any());
        var counts = new[] { a, b, c, d }.Select(e => log.Count(x => x == e)).ToArray();
        ScCheck($"with Many Mouths among four it takes two at a time ({casts.Count} casts: {string.Join(" ", casts.Select(x => x.Count))})", pairs && casts.Count >= 2 * one - 2);
        ScCheck($"and the pairs alternate (no creature in two casts in a row: {apart}; each {string.Join("/", counts)})", apart && counts.Max() - counts.Min() <= 1);
        foreach (var e in new[] { b, c, d }) e.QueueFree();

        // ---- the rupture: a heal's worth, a heal's recharge
        p.TestDrained = null;
        p.ResetAbilityCooldowns();
        p.SetVitalForce(p.Stats.VitalForceMax);
        float vf = p.VitalForce;
        foreach (var _ in Wait(0.2f)) yield return null;
        _scInput = new PlayerInput { Ability2 = true, Aim = new Vector2(dir, 0.1f).Normalized(), AimGiven = true };
        foreach (var _ in Wait(0.2f)) yield return null;
        ScCheck($"the rupture costs a heal's worth ({vf:0} -> {p.VitalForce:0}; heal {p.HealCost:0}, rupture {p.RuptureCost:0})", Math.Abs(p.RuptureCost - p.HealCost) < 0.01f && Math.Abs(vf - p.VitalForce - p.RuptureCost) < 1.5f);
        ScCheck($"and recharges as a heal does ({Tune.Vitalist.RuptureCooldown:0.#} s, heal {p.AbilityRecharge:0.#} s)", Math.Abs(Tune.Vitalist.RuptureCooldown - Tune.Vitalist.HealCooldown) < 0.01f && p.RuptureCooldownFrac > 0.8f);
        foreach (var _ in Wait(1.5f)) yield return null;

        // ---- the Bloom: a lighter blow to every creature near
        var e1 = Dummy(40); var e2 = Dummy(70);
        foreach (var _ in Wait(0.3f)) yield return null;
        float h1 = e1.Hp, h2 = e2.Hp;
        int bigs = p.BigUses;
        _scInput = new PlayerInput { Ability = true, Aim = new Vector2(dir, 0), AimGiven = true };
        foreach (var _ in Wait(0.3f)) yield return null;
        float bw = Tune.Big.BloomDamage * p.Stats.DamageMult * p.Stats.SpecialDamageMult;
        float d1 = h1 - e1.Hp, d2 = h2 - e2.Hp;
        ScCheck($"the Bloom strikes each creature near for about {bw:0} ({d1:0}, {d2:0}; bloomed {p.BigUses - bigs})", d1 > bw * 0.85f && d1 < bw * 1.4f && d2 > bw * 0.85f && d2 < bw * 1.4f);
        p.InputOverride = null;
    }
}
