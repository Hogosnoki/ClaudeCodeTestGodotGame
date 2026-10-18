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
    private static void ApplyTraced(Upgrade u, PlayerStats s, Player p) => s.RelicTraces[u.Id] = Traced(u, s, p);

    /// <summary>Applies a card, writing down what it changed.</summary>
    private static RelicTrace Traced(Upgrade u, PlayerStats s, Player p)
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
        return trace;
    }

    /// <summary>The relics that change the world or the party's chests (a Hunter's Map, a Locksmith's Ring...): kept once taken, never traded.</summary>
    public static bool Tradeable(string id, PlayerStats s) => s.RelicTraces.ContainsKey(id) && TradeableKind(id);
    public static bool TradeableKind(string id) => id is not ("relic_deeper" or "relic_shortcut" or "relic_locksmith" or "relic_treasure" or "relic_kidding");

    /// <summary>A relic this hero carries is given up: what it changed is put back, its listeners are taken off, and it is no longer theirs.</summary>
    public static bool RemoveRelic(string id, PlayerStats s, Player p)
    {
        if (!s.RelicTraces.TryGetValue(id, out var trace)) return false;
        Undo(trace, s);
        s.RelicTraces.Remove(id);
        s.Stacks.Remove(id);
        if (p != null)
        {
            p.Hp = Math.Min(p.Hp, s.MaxHp);
            if (!p.IsRemote) RunRelics.Forget(id);
        }
        return true;
    }

    /// <summary>Puts back what a traced card changed (see the class's summary).</summary>
    private static void Undo(RelicTrace trace, PlayerStats s)
    {
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
    }

    // ================================================================== side-grades taken back

    /// <summary>The cards that grow on a side-grade (their Requires leads back to it), for each side-grade.</summary>
    private static Dictionary<string, List<Upgrade>> _growsOn;
    public static List<Upgrade> GrowsOn(string altId)
    {
        if (_growsOn == null)
        {
            _growsOn = new();
            foreach (var a in Chest.Where(u => u.Alteration))
            {
                var set = new List<Upgrade>();
                var ids = new HashSet<string> { a.Id };
                for (bool more = true; more;)
                {
                    more = false;
                    foreach (var u in Chest)
                        if (u.Requires != null && ids.Contains(u.Requires) && ids.Add(u.Id)) { set.Add(u); more = true; }
                }
                _growsOn[a.Id] = set;
            }
        }
        return _growsOn.TryGetValue(altId, out var l) ? l : new List<Upgrade>();
    }

    /// <summary>A side-grade or a card growing on one: kept traced, so a "Back to..." card can take it off again.</summary>
    private static bool IsSideGradeCard(Upgrade u) => u.Alteration || (u.Requires != null && Chest.Any(a => a.Alteration && GrowsOn(a.Id).Contains(u)));

    private static void ApplyCard(Upgrade u, PlayerStats s, Player p)
    {
        if (!s.CardTraces.TryGetValue(u.Id, out var list)) s.CardTraces[u.Id] = list = new List<RelicTrace>();
        list.Add(Traced(u, s, p));
    }

    /// <summary>A side-grade the loadout brings, applied as a run begins (traced: it can be taken back in the run).</summary>
    public static void ApplyLoadoutCard(Upgrade u, PlayerStats s, Player p)
    {
        ApplyCard(u, s, p);
        s.Stacks[u.Id] = s.StackOf(u.Id) + 1;
    }

    /// <summary>
    /// The "Back to..." cards: one for each side-grade, offered at milestones while the hero brings that side-grade. Taking it puts the
    /// ability back as it first was, and the upgrades that grew on the side-grade go with it.
    /// </summary>
    public static readonly List<Upgrade> Reverts = new();

    private static void BuildReverts()
    {
        foreach (var a in Chest.Where(u => u.Alteration))
        {
            var alt = a;
            string title = AbilityTitle(alt.Ability, alt.For?[0] ?? HeroKind.Swordsman);
            string grown = GrowsOn(alt.Id).Count > 0 ? $" The upgrades grown on {alt.Name} go with it." : "";
            Reverts.Add(new Upgrade
            {
                Id = "rv_" + alt.Id, Name = $"Back to {title}", Desc = $"Undo {alt.Name}: your {title} works as it first did.{grown}",
                Icon = alt.Icon, For = alt.For, Ability = alt.Ability, RevertOf = alt.Id, Weight = 1f,
                Apply = (s, p) => RemoveSideGrade(alt.Id, s, p),
            });
        }
    }

    /// <summary>Takes a side-grade off a hero (and every card that grew on it), as if never taken.</summary>
    public static bool RemoveSideGrade(string altId, PlayerStats s, Player p)
    {
        if (s.StackOf(altId) == 0) return false;
        foreach (var u in GrowsOn(altId).AsEnumerable().Reverse().Append(Find(altId)))
        {
            if (s.CardTraces.TryGetValue(u.Id, out var list))
            {
                for (int k = list.Count - 1; k >= 0; k--) Undo(list[k], s);
                s.CardTraces.Remove(u.Id);
            }
            s.Stacks.Remove(u.Id);
        }
        if (p != null)
        {
            p.Hp = Math.Min(p.Hp, s.MaxHp);
            p.SyncCharges();
        }
        return true;
    }
}
