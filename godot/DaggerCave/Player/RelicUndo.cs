using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;

namespace DaggerCave;

/// <summary>
/// A relic given back (the merchant's trading stand): what a relic did when it was taken is written down then (every plain number, switch
/// and name it changed on the hero's stats, and every listener it added to their events), so it can be taken off again later without
/// rebuilding the hero. A number something else has changed since is put back by the same step reversed: as a factor where it was one, else
/// by the difference.
/// </summary>
public static partial class Upgrades
{
    private static readonly FieldInfo[] StatFields = typeof(PlayerStats).GetFields(BindingFlags.Public | BindingFlags.Instance)
        .Where(f => !f.IsInitOnly && (f.FieldType == typeof(float) || f.FieldType == typeof(int) || f.FieldType == typeof(bool) || f.FieldType == typeof(string))).ToArray();
    private static readonly FieldInfo[] EventFields = typeof(Triggers).GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
        .Where(f => typeof(Delegate).IsAssignableFrom(f.FieldType)).ToArray();

    /// <summary>What one relic changed when it was taken.</summary>
    public sealed class RelicTrace
    {
        public readonly List<(FieldInfo Field, object Before, object After)> Stats = new();
        public readonly List<(FieldInfo Field, Delegate[] Added)> Listeners = new();
    }

    private static Delegate[] Calls(Triggers t, FieldInfo f) => (f.GetValue(t) as Delegate)?.GetInvocationList() ?? Array.Empty<Delegate>();

    /// <summary>Applies a relic, writing down what it changed.</summary>
    private static void ApplyTraced(Upgrade u, PlayerStats s, Player p)
    {
        var before = StatFields.Select(f => f.GetValue(s)).ToArray();
        var calls = EventFields.Select(f => Calls(s.On, f)).ToArray();
        u.Apply(s, p);
        var trace = new RelicTrace();
        for (int k = 0; k < StatFields.Length; k++)
        {
            var after = StatFields[k].GetValue(s);
            if (!Equals(after, before[k])) trace.Stats.Add((StatFields[k], before[k], after));
        }
        for (int k = 0; k < EventFields.Length; k++)
        {
            var added = Calls(s.On, EventFields[k]).Except(calls[k]).ToArray();
            if (added.Length > 0) trace.Listeners.Add((EventFields[k], added));
        }
        s.RelicTraces[u.Id] = trace;
    }

    /// <summary>The relics that change the world or the party's chests (a Hunter's Map, a Locksmith's Ring...): kept once taken, never traded.</summary>
    public static bool Tradeable(string id, PlayerStats s) => s.RelicTraces.ContainsKey(id) && TradeableKind(id);
    public static bool TradeableKind(string id) => id is not ("relic_deeper" or "relic_shortcut" or "relic_locksmith" or "relic_treasure" or "relic_kidding");

    /// <summary>A relic this hero carries is given up: what it changed is put back, its listeners are taken off, and it is no longer theirs.</summary>
    public static bool RemoveRelic(string id, PlayerStats s, Player p)
    {
        if (!s.RelicTraces.TryGetValue(id, out var trace)) return false;
        foreach (var (f, before, after) in trace.Stats)
        {
            var now = f.GetValue(s);
            if (Equals(now, after)) { f.SetValue(s, before); continue; }
            switch (now)
            {
                case float v:
                {
                    float b = (float)before, a = (float)after;
                    // (as a factor where it was one, else by the difference)
                    f.SetValue(s, a != 0f && b != 0f ? v * (b / a) : v - (a - b));
                    break;
                }
                case int v: f.SetValue(s, v - ((int)after - (int)before)); break;
                default: f.SetValue(s, before); break;
            }
        }
        foreach (var (f, added) in trace.Listeners)
        {
            var cur = f.GetValue(s.On) as Delegate;
            foreach (var d in added) cur = Delegate.Remove(cur, d);
            f.SetValue(s.On, cur);
        }
        s.RelicTraces.Remove(id);
        s.Stacks.Remove(id);
        if (p != null)
        {
            p.Hp = Math.Min(p.Hp, s.MaxHp);
            if (!p.IsRemote) RunRelics.Forget(id);
        }
        return true;
    }
}
