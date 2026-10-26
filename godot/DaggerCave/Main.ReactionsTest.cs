using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

public partial class Main
{
    private IEnumerator<object> _rxSteps;

    /// <summary>
    /// `--scenario=reactions`: the statuses react (<see cref="Tune.Elements"/>). Soaked: frost builds faster, fire slower, a shatter
    /// bursts harder. Entangled: fire faster, frost slower. Blinded: every other element faster. Fire on the chilled (or frost on the
    /// burning): steam. Earth on the soaked: mud. Wind on the burning: wildfire.
    /// </summary>
    private void ReactionsScenario()
    {
        if (_rxSteps == null)
        {
            if (_scT < 0.8f) return;
            _rxSteps = ReactionsRun().GetEnumerator();
        }
        if (!_rxSteps.MoveNext()) ScEnd();
    }

    private IEnumerable<object> ReactionsRun()
    {
        var p = G.Player;
        foreach (var e in G.Enemies.ToArray()) e.QueueFree();
        p.Stats.MaxHp = p.Hp = 5000;
        p.InputOverride = () => default;
        foreach (var _ in ElWait(0.4f)) yield return null;
        var made = new List<Enemy>();
        Enemy Dummy(float dx) { var e = ElDummy(p, dx); made.Add(e); return e; }
        void Clear() { foreach (var e in made) if (IsInstanceValid(e)) e.QueueFree(); made.Clear(); }
        float full = Tune.Elements.Full;

        // soaked: frost faster, fire slower
        var a = Dummy(40);
        a.Build(HeroElement.Water, full);
        a.Build(HeroElement.Frost, 20);
        a.Build(HeroElement.Fire, 20);
        ScCheck($"soaked ({a.Soaked}): 20 frost builds {a.FrostBuild:0} (x{Tune.Elements.SoakFrost}), 20 fire {a.FireBuild:0} (x{Tune.Elements.SoakFire})",
            a.Soaked && Math.Abs(a.FrostBuild - 20 * Tune.Elements.SoakFrost) < 0.5f && Math.Abs(a.FireBuild - 20 * Tune.Elements.SoakFire) < 0.5f);
        Clear(); yield return null;

        // entangled: the opposite
        var b = Dummy(40);
        b.Build(HeroElement.Nature, full);
        b.Build(HeroElement.Fire, 20);
        b.Build(HeroElement.Frost, 20);
        ScCheck($"entangled ({b.Tangled}): 20 fire builds {b.FireBuild:0} (x{Tune.Elements.TangleFire}), 20 frost {b.FrostBuild:0} (x{Tune.Elements.TangleFrost})",
            b.Tangled && Math.Abs(b.FireBuild - 20 * Tune.Elements.TangleFire) < 0.5f && Math.Abs(b.FrostBuild - 20 * Tune.Elements.TangleFrost) < 0.5f);
        Clear(); yield return null;

        // blinded: every other element faster
        var c = Dummy(40);
        c.Build(HeroElement.Dark, full);
        c.Build(HeroElement.Earth, 20);
        ScCheck($"blinded ({c.Blinded}): 20 earth builds {c.EarthBuild:0} (x{Tune.Elements.BlindAmp})", c.Blinded && Math.Abs(c.EarthBuild - 20 * Tune.Elements.BlindAmp) < 0.5f);
        Clear(); yield return null;

        // a soaked shatter bursts harder
        float Shattered(bool soak)
        {
            var d = Dummy(40);
            if (soak) d.Build(HeroElement.Water, full);
            d.FreezeSolid(5f, force: true);
            float hp = d.Hp;
            d.Shatter(1f);
            float dealt = hp - d.Hp;
            Clear();
            return dealt;
        }
        float dry = Shattered(false);
        yield return null;
        float wet = Shattered(true);
        ScCheck($"a shatter deals {dry:0}; soaked, {wet:0} (x{Tune.Elements.SoakShatter})", dry > 0 && Math.Abs(wet - dry * Tune.Elements.SoakShatter) < 1f);
        yield return null;

        // fire on the chilled: steam
        int clouds = SmokeCloud.All.Count;
        var s = Dummy(40);
        s.Chill(0.3f, 4f);
        s.Build(HeroElement.Fire, full);
        foreach (var _ in ElWait(0.1f)) yield return null;
        ScCheck($"fire filling a chilled creature bursts into steam ({s.Steamed}): no chill ({s.Chilled}), no fire ({s.Ignited}), a cloud ({SmokeCloud.All.Count - clouds})",
            s.Steamed == 1 && !s.Chilled && !s.Ignited && SmokeCloud.All.Count > clouds);
        ScShot("reaction_steam");
        Clear();
        var s2 = Dummy(40);
        s2.Ignite(4f, 5f);
        s2.Build(HeroElement.Frost, full);
        ScCheck($"and frost filling a burning one does too ({s2.Steamed}, burning {s2.Ignited}, chilled {s2.Chilled})", s2.Steamed == 1 && !s2.Ignited && !s2.Chilled);
        Clear();
        foreach (var _ in ElWait(0.2f)) yield return null;

        // earth on the soaked: mud
        var m = Dummy(40);
        m.Build(HeroElement.Water, full);
        m.Build(HeroElement.Earth, full);
        ScCheck($"earth filling a soaked creature sinks it in mud ({m.Mudded}): mired ({m.Mired}), no longer soaked ({m.Soaked})", m.Mudded == 1 && m.Mired && !m.Soaked);
        ScShot("reaction_mud");
        Clear(); yield return null;

        // wind on the burning: wildfire
        var w = Dummy(40);
        var near = Dummy(70);
        var far = Dummy(200);
        w.Ignite(4f, 5f);
        w.Shove(new Vector2(p.Facing * 200f, -60f));
        ScCheck($"wind on a burning creature fans the fire ({w.Fanned}) to the one beside it ({near.Ignited}), not the one far off ({far.Ignited})", w.Fanned == 1 && near.Ignited && !far.Ignited);
        ScShot("reaction_wildfire");
        foreach (var _ in ElWait(0.4f)) yield return null;
        Clear();
        p.InputOverride = null;
    }
}
