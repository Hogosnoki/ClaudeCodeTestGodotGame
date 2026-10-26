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

        // creatures that refuse a status by their nature
        var fe = new FireElemental { Position = p.GlobalPosition + new Vector2(40 * p.Facing, -10) };
        fe.SetMeta("test", true);
        _world.AddChild(fe);
        fe.Wake();
        fe.Hp = fe.MaxHp = 1000000f;
        fe.Freeze(9999f, hold: true);
        made.Add(fe);
        fe.Build(HeroElement.Fire, full);
        fe.Build(HeroElement.Water, 20);
        ScCheck($"a fire elemental refuses fire ({fe.Refused} refused, alight {fe.Ignited}, {fe.BuiltTotal[(int)HeroElement.Fire]:0} built) but takes water ({fe.WaterBuild:0})",
            fe.Refused >= 1 && !fe.Ignited && fe.BuiltTotal[(int)HeroElement.Fire] < 0.5f && fe.WaterBuild > 19f);
        var bat = new Bat { Position = p.GlobalPosition + new Vector2(90 * p.Facing, -40) };
        bat.SetMeta("test", true);
        _world.AddChild(bat);
        bat.Wake();
        bat.Hp = bat.MaxHp = 1000000f;
        bat.Freeze(9999f, hold: true);
        made.Add(bat);
        bat.Build(HeroElement.Nature, 40);
        bat.Build(HeroElement.Frost, 20);
        ScCheck($"a bat, flying, refuses nature (no roots reach it: {bat.BuiltTotal[(int)HeroElement.Nature]:0}) but takes frost ({bat.FrostBuild:0})", bat.BuiltTotal[(int)HeroElement.Nature] < 0.5f && bat.FrostBuild > 19f);
        var golem = new Golem { Position = p.GlobalPosition + new Vector2(140 * p.Facing, -10) };
        golem.SetMeta("test", true);
        _world.AddChild(golem);
        golem.Wake();
        golem.Hp = golem.MaxHp = 1000000f;
        golem.Freeze(9999f, hold: true);
        made.Add(golem);
        golem.Build(HeroElement.Earth, 40);
        float wantG = 40 * Tune.Elements.ArmoredEarth * (golem.Elite ? Tune.Elements.EliteTake : 1f);
        ScCheck($"an armoured golem takes earth at half ({golem.EarthBuild:0} of 40, want {wantG:0})", Math.Abs(golem.EarthBuild - wantG) < 0.5f);
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

        // ---- the world answers the element (ElementWorld)
        var here = p.GlobalPosition + new Vector2(60 * p.Facing, -10);
        T Put<T>(T n) where T : Node2D { _world.AddChild(n); return n; }
        var web = Put(new WebPatch { Position = here });
        var roots = Put(new GraspingRoots { Position = here + new Vector2(0, 10) });
        var spores = Put(new SporeCloud { Position = here + new Vector2(0, -10), Life = 30f });
        yield return null;
        int burnt = ElementWorld.Burnt;
        ElementWorld.Touch(HeroElement.Fire, here, 30f);
        foreach (var _ in ElWait(0.2f)) yield return null;
        ScCheck($"fire burns away a web ({!IsInstanceValid(web) || web.IsQueuedForDeletion()}), roots ({!IsInstanceValid(roots) || roots.IsQueuedForDeletion()}) and a spore cloud ({!IsInstanceValid(spores) || spores.IsQueuedForDeletion()}) ({ElementWorld.Burnt - burnt})",
            ElementWorld.Burnt - burnt >= 3 && (!IsInstanceValid(web) || web.IsQueuedForDeletion()) && (!IsInstanceValid(roots) || roots.IsQueuedForDeletion()) && (!IsInstanceValid(spores) || spores.IsQueuedForDeletion()));
        var gas = Put(new GasCloud { Position = here, Life = 30f });
        foreach (var _ in ElWait(0.6f)) yield return null;
        var gasAt = gas.GlobalPosition;
        ElementWorld.Touch(HeroElement.Wind, here, 30f, new Vector2(p.Facing, 0));
        ScCheck($"wind blows a gas cloud along ({gas.GlobalPosition.X - gasAt.X:0} px)", Math.Abs(gas.GlobalPosition.X - gasAt.X) > 40f);
        int lit = ElementWorld.Lit;
        ElementWorld.Touch(HeroElement.Fire, gas.GlobalPosition, 30f);
        ScCheck($"fire lights it ({gas.Lit})", gas.Lit && ElementWorld.Lit > lit);
        foreach (var _ in ElWait(1.2f)) yield return null;
        var lava = Put(new LavaPuddle { Position = here + new Vector2(0, 10) });
        var acid = Put(new AcidPuddle { Position = here + new Vector2(20, 10), Life = 30f });
        yield return null;
        ElementWorld.Touch(HeroElement.Water, here + new Vector2(10, 10), 30f);
        ScCheck($"water douses lava ({lava.IsQueuedForDeletion()}) and washes acid away ({acid.IsQueuedForDeletion()})", lava.IsQueuedForDeletion() && acid.IsQueuedForDeletion());
        int vines = ElementWorld.Vines, ropes = Rope.All.Count;
        // (somewhere near with rock overhead, within a vine's reach)
        var under = p.GlobalPosition;
        for (float dx = 0; dx < 1200; dx += 24)
        {
            bool found = false;
            foreach (var sx in new[] { dx, -dx })
            {
                var at = p.GlobalPosition + new Vector2(sx, -20);
                if (G.Cave.IsSolid(at)) continue;
                for (float up = 0; up < Tune.ElementWorld.VineReach - 10; up += 8)
                    if (G.Cave.IsSolid(at + new Vector2(0, -up))) { under = at; found = true; break; }
                if (found) break;
            }
            if (found) break;
        }
        ElementWorld.Touch(HeroElement.Nature, under, 20f);
        yield return null;
        ScCheck($"nature lets a vine down from the rock above ({ElementWorld.Vines - vines}, ropes {ropes} -> {Rope.All.Count})", ElementWorld.Vines > vines && Rope.All.Count > ropes);
        ScShot("world_vine");
        // (frost on water: only where there is water)
        var cave = G.Cave;
        Vector2? open = null;
        if (cave.Liquid == Liquid.Water)
            for (float x = p.GlobalPosition.X - 1500; x < p.GlobalPosition.X + 1500 && open == null; x += 16)
            {
                var at = new Vector2(x, cave.WaterY);
                if (!cave.IsSolid(at + new Vector2(0, 6)) && !cave.IsSolid(at + new Vector2(0, -12)) && !cave.IsSolid(at + new Vector2(30, 6)) && !cave.IsSolid(at + new Vector2(-30, 6))) open = at;
            }
        if (open is Vector2 w0)
        {
            int floes = ElementWorld.Floes;
            ElementWorld.Touch(HeroElement.Frost, w0 + new Vector2(0, -10), 20f);
            yield return null;
            var floe = G.World.GetChildren().OfType<IcePlatform>().FirstOrDefault(ip => ip.Melt > 0);
            ScCheck($"frost lays a floe of ice on the water ({ElementWorld.Floes - floes}, melting in {(floe != null ? floe.Melt : 0):0.0} s)", ElementWorld.Floes > floes && floe != null);
            if (floe != null) floe.Melt = 0.05f;
            foreach (var _ in ElWait(0.3f)) yield return null;
            ScCheck($"and it melts away ({floe == null || !IsInstanceValid(floe) || floe.IsQueuedForDeletion()})", floe == null || !IsInstanceValid(floe) || floe.IsQueuedForDeletion());
        }
        else GD.Print("[scenario] (no open water in this cave: the floe is checked in the river)");
        p.InputOverride = null;
    }
}
