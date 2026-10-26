using System;
using System.Linq;
using Godot;

namespace DaggerCave;

/// <summary>
/// The world answering a simple kit's element where it lands (<see cref="Touch"/>; <see cref="Tune.ElementWorld"/>):
///  * fire lights gas (a cloud goes up, a gasbag bursts), burns webs, roots and spore clouds away, and melts ice;
///  * frost lays a floe of ice on water (a platform that melts away) and cools lava puddles to nothing;
///  * water douses lava and magma pools and washes acid away;
///  * wind scatters spore clouds and blows gas clouds along;
///  * earth brings down rubble and strikes breakable ledges;
///  * nature grows a vine from the rock above (a climbable rope that withers away);
///  * dark does nothing to the world (its part is the creatures' blindness).
/// Webs, roots, rubble, vines and floes are told to the other games; the rest follow from what each game sees.
/// </summary>
public static class ElementWorld
{
    /// <summary>For the tests: what the world has answered (lit, burnt, floes laid, doused, scattered, broken, vines grown).</summary>
    public static int Lit, Burnt, Floes, Doused, Scattered, Broken, Vines;

    /// <summary>The element lands at <paramref name="at"/>, reaching <paramref name="r"/> px (<paramref name="along"/>: the way it was going,
    /// for wind).</summary>
    public static void Touch(HeroElement el, Vector2 at, float r, Vector2 along = default)
    {
        if (G.Cave == null || el == HeroElement.None) return;
        switch (el)
        {
            case HeroElement.Fire:
                Lit += GasCloud.IgniteAt(at, r);
                foreach (var br in Breakables.All.ToArray())
                {
                    if (br is WebPatch web && GodotObject.IsInstanceValid(web) && web.GlobalPosition.DistanceTo(at) <= web.Radius + r) { web.Strike(at); Burnt++; Flame(web.GlobalPosition); }
                    else if (br is GraspingRoots roots && GodotObject.IsInstanceValid(roots) && roots.HitCenter.DistanceTo(at) <= roots.Radius + r) { roots.Burn(); Burnt++; Flame(roots.HitCenter); }
                }
                foreach (var n in G.World.GetChildren())
                    if (n is SporeCloud sc && !sc.IsQueuedForDeletion() && sc.GlobalPosition.DistanceTo(at) <= sc.Radius + r) { Flame(sc.GlobalPosition); sc.QueueFree(); Burnt++; }
                Breakables.SpellBurst(at, r);
                break;
            case HeroElement.Frost:
                if (Floe(at, r)) Floes++;
                foreach (var n in G.World.GetChildren())
                    if (n is LavaPuddle lp && !lp.IsQueuedForDeletion() && lp.GlobalPosition.DistanceTo(at) <= LavaPuddle.HalfW + r) { Steam(lp.GlobalPosition); lp.QueueFree(); Doused++; }
                break;
            case HeroElement.Water:
                foreach (var n in G.World.GetChildren())
                {
                    if (n is LavaPuddle lp && !lp.IsQueuedForDeletion() && lp.GlobalPosition.DistanceTo(at) <= LavaPuddle.HalfW + r) { Steam(lp.GlobalPosition); lp.QueueFree(); Doused++; }
                    else if (n is MagmaPool mp && !mp.IsQueuedForDeletion() && mp.GlobalPosition.DistanceTo(at) <= mp.Half + r) { Steam(mp.GlobalPosition); mp.QueueFree(); Doused++; }
                    else if (n is AcidPuddle ap && !ap.IsQueuedForDeletion() && ap.GlobalPosition.DistanceTo(at) <= AcidPuddle.HalfW + r) { G.Fx.Splash(ap.GlobalPosition, 0.4f, new Color(Elements.ColorOf(HeroElement.Water), 0.8f)); ap.QueueFree(); Doused++; }
                }
                break;
            case HeroElement.Wind:
            {
                var push = along.LengthSquared() > 0.01f ? along.Normalized() : Vector2.Up;
                foreach (var n in G.World.GetChildren())
                {
                    if (n is SporeCloud sc && !sc.IsQueuedForDeletion() && sc.GlobalPosition.DistanceTo(at) <= sc.Radius + r)
                    {
                        G.Fx.Directional(sc.GlobalPosition, push, 0.6f, new Color(0.75f, 0.55f, 0.95f, 0.6f), 10, 200, 2f, 0.5f, 0, 1);
                        sc.QueueFree();
                        Scattered++;
                    }
                    else if (n is GasCloud gc && !gc.IsQueuedForDeletion() && gc.GlobalPosition.DistanceTo(at) <= gc.Size + r)
                    {
                        gc.GlobalPosition += push * Tune.ElementWorld.GasBlow;
                        Scattered++;
                    }
                }
                break;
            }
            case HeroElement.Earth:
                foreach (var rb in Rubble.All.ToArray())
                {
                    if (!GodotObject.IsInstanceValid(rb) || rb.Cleared) continue;
                    var d = (at - rb.GlobalPosition).Abs() - rb.Size * 0.5f;
                    if (Math.Max(d.X, 0) > r || Math.Max(d.Y, 0) > r) continue;
                    rb.Strike(at);
                    Broken++;
                }
                Breakables.SpellBurst(at, r);
                break;
            case HeroElement.Nature:
                if (Vine(at, r)) Vines++;
                break;
        }
    }

    private static void Flame(Vector2 at)
    {
        G.Fx.Burst(at, new Color(1f, 0.55f, 0.15f, 0.9f), 10, 120, 2f, 0.45f, -60);
        G.Fx.Ember(at, new Color(1f, 0.6f, 0.2f));
    }

    private static void Steam(Vector2 at) => G.Fx.Burst(at, new Color(0.95f, 0.95f, 0.97f, 0.8f), 12, 110, 2.6f, 0.6f, -50);

    /// <summary>Frost near the water's surface: a floe of ice laid on it there (one at a time near the same spot). True if one was laid.</summary>
    private static bool Floe(Vector2 at, float r)
    {
        var cave = G.Cave;
        if (cave.Liquid != Liquid.Water) return false;
        float y = cave.WaterY;
        if (Math.Abs(at.Y - y) > r + Tune.ElementWorld.FloeReach) return false;
        var on = new Vector2(at.X, y);
        // (open water there: not rock, and air above it)
        if (cave.IsSolid(on + new Vector2(0, 6)) || cave.IsSolid(on + new Vector2(0, -12))) return false;
        foreach (var n in G.World.GetChildren())
            if (n is IcePlatform ip && ip.Melt > 0 && !ip.IsQueuedForDeletion() && Math.Abs(ip.GlobalPosition.X - on.X) < ip.HalfW && Math.Abs(ip.GlobalPosition.Y - on.Y) < 8) { ip.Melt = Tune.ElementWorld.FloeSeconds; return false; }
        var floe = new IcePlatform { Position = on - new Vector2(0, 2), HalfW = Tune.ElementWorld.FloeHalf, Melt = Tune.ElementWorld.FloeSeconds };
        G.Spawn(floe);
        NetSync.HeroVisual(floe);
        G.Fx.Burst(on, new Color(0.85f, 0.96f, 1f, 0.9f), 14, 120, 2f, 0.4f, 0);
        G.Sfx.Play("clink", on, -6, 0.1f, 0.8f);
        return true;
    }

    /// <summary>Nature by the rock: a vine let down from the rock above (a climbable rope that withers). True if one grew.</summary>
    private static bool Vine(Vector2 at, float r)
    {
        var cave = G.Cave;
        // (the rock above, within reach; the vine hangs from just under it)
        float up = 0;
        while (up < Tune.ElementWorld.VineReach && !cave.IsSolid(at + new Vector2(0, -up - 4))) up += 4f;
        if (up >= Tune.ElementWorld.VineReach) return false;
        var top = at + new Vector2(0, -up + 2);
        foreach (var rp in Rope.All)
            if (GodotObject.IsInstanceValid(rp) && rp.Holder == null && rp.GlobalPosition.DistanceTo(top) < 24f) return false;
        // (down to the floor below, or as long as a vine grows)
        float down = 0;
        while (down < Tune.ElementWorld.VineLength && !cave.IsSolid(top + new Vector2(0, down + 4))) down += 4f;
        var vine = new Rope { Position = top, Length = Math.Max(40f, down - 6f), Life = Tune.ElementWorld.VineSeconds };
        G.Spawn(vine);
        NetSync.HeroVisual(vine);
        for (int k = 0; k < 8; k++) G.Fx.Ember(top + new Vector2(G.Range(-4, 4), k * 8f), Elements.ColorOf(HeroElement.Nature));
        return true;
    }
}
