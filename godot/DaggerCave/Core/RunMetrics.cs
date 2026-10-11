using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

/// <summary>One hero's record of a run (see <see cref="RunMetrics"/>).</summary>
public sealed class HeroMetrics
{
    public int Peer;
    public string Name = "";
    public HeroKind Hero;
    /// <summary>Attack-button attacks, and how many of them struck something.</summary>
    public int Attacks, Hits;
    public float Dealt, Overkill, Taken, MitigatedSelf;
    public int Kills, EliteKills, BossKills, Deaths;
    public float DealtElites, DealtBosses, TakenElites, TakenBosses;
    /// <summary>Damage by what dealt it (the attack, an ability, over time...).</summary>
    public readonly Dictionary<string, float> DealtBy = new();
    /// <summary>Damage dealt to, kills of and damage taken from each kind of creature.</summary>
    public readonly Dictionary<string, float> DealtTo = new(), TakenFrom = new();
    public readonly Dictionary<string, int> KillsOf = new();
    /// <summary>What other heroes did for this one, by their peer: blows soaked or taken off it, health given (and given past full), revives.</summary>
    public readonly Dictionary<int, float> ProtectedBy = new(), HealedBy = new(), OverhealBy = new();
    public readonly Dictionary<int, int> RevivedBy = new();

    public float HitRate => Attacks == 0 ? 0f : Hits / (float)Attacks;

    public void Write(NetOut w)
    {
        w.Int(Peer).Str(Name).Byte((byte)Hero).Int(Attacks).Int(Hits);
        foreach (var f in new[] { Dealt, Overkill, Taken, MitigatedSelf, DealtElites, DealtBosses, TakenElites, TakenBosses }) w.Float(f);
        w.Int(Kills).Int(EliteKills).Int(BossKills).Int(Deaths);
        static void D<K, V>(NetOut w, Dictionary<K, V> d, Action<K> key, Action<V> val) { w.Int(d.Count); foreach (var (k, v) in d) { key(k); val(v); } }
        D(w, DealtBy, k => w.Str(k), v => w.Float(v));
        D(w, DealtTo, k => w.Str(k), v => w.Float(v));
        D(w, TakenFrom, k => w.Str(k), v => w.Float(v));
        D(w, KillsOf, k => w.Str(k), v => w.Int(v));
        D(w, ProtectedBy, k => w.Int(k), v => w.Float(v));
        D(w, HealedBy, k => w.Int(k), v => w.Float(v));
        D(w, OverhealBy, k => w.Int(k), v => w.Float(v));
        D(w, RevivedBy, k => w.Int(k), v => w.Int(v));
    }

    public static HeroMetrics Read(NetIn r)
    {
        var m = new HeroMetrics { Peer = r.Int(), Name = r.Str(), Hero = (HeroKind)r.Byte(), Attacks = r.Int(), Hits = r.Int() };
        m.Dealt = r.Float(); m.Overkill = r.Float(); m.Taken = r.Float(); m.MitigatedSelf = r.Float();
        m.DealtElites = r.Float(); m.DealtBosses = r.Float(); m.TakenElites = r.Float(); m.TakenBosses = r.Float();
        m.Kills = r.Int(); m.EliteKills = r.Int(); m.BossKills = r.Int(); m.Deaths = r.Int();
        static void D<K, V>(NetIn r, Dictionary<K, V> d, Func<K> key, Func<V> val) { int n = r.Int(); for (int k = 0; k < n; k++) d[key()] = val(); }
        D(r, m.DealtBy, r.Str, r.Float);
        D(r, m.DealtTo, r.Str, r.Float);
        D(r, m.TakenFrom, r.Str, r.Float);
        D(r, m.KillsOf, r.Str, r.Int);
        D(r, m.ProtectedBy, r.Int, r.Float);
        D(r, m.HealedBy, r.Int, r.Float);
        D(r, m.OverhealBy, r.Int, r.Float);
        D(r, m.RevivedBy, r.Int, r.Int);
        return m;
    }
}

/// <summary>
/// How the run is going, hero by hero, for the metrics page (Your Build, and after the party falls). Each game keeps its own hero's
/// record and sends it to the others now and then (so the page can set the heroes side by side); what one hero does for another (a
/// barrier soaking a blow, a heal, a revive) is kept by the one it was done for, under the giver's name, and added up for the giver.
/// </summary>
public static class RunMetrics
{
    public static readonly Dictionary<int, HeroMetrics> By = new();
    private static float _sendT;

    public static HeroMetrics Mine => Get(Net.Me);

    public static HeroMetrics Get(int peer)
    {
        if (!By.TryGetValue(peer, out var m)) By[peer] = m = new HeroMetrics { Peer = peer };
        return m;
    }

    public static void Reset() { By.Clear(); _source = ""; _sourceT = -10f; _attackOpen = false; }

    // ---- what dealt the damage now landing: the last button pressed, for a moment after

    private static string _source = "";
    private static float _sourceT = -10f;
    private static bool _attackOpen;
    private static float Now => Time.GetTicksMsec() / 1000f;
    /// <summary>How long after a press its damage is still put down to it (a bolt in flight, a dagger thrown).</summary>
    private const float SourceWindow = 1.6f;

    /// <summary>This game's hero used something (slot 0 the attack button, 1 and 2 the abilities, 3 the dodge button, 4 the support).</summary>
    public static void Used(Player p, int slot)
    {
        if (p == null || p.IsRemote) return;
        _source = SlotName(p, slot);
        _sourceT = Now;
        if (slot == 0) { Mine.Attacks++; _attackOpen = true; }
    }

    /// <summary>The name a hero's button goes by in the metrics.</summary>
    public static string SlotName(Player p, int slot)
    {
        var s = p.Stats;
        return (slot, s.Hero) switch
        {
            (0, HeroKind.Vitalist) => "Drain",
            (0, HeroKind.Elementalist) => s.Frostbolt ? "Frostbolt" : "Firebolt",
            (0, HeroKind.Rogue) => "Dagger jab",
            (0, HeroKind.Aegis) => "Ward bolt",
            (0, HeroKind.ShapeShifter) => p.Shifted ? "Form attacks" : "Staff",
            (0, HeroKind.Automaton) => "Cleaver",
            (0, _) => "Sword",
            (1, HeroKind.Warden) => "Guarded Charge",
            (1, HeroKind.Vitalist) => "Heal",
            (1, HeroKind.Elementalist) => s.Firestorm ? "Firestorm" : "Blizzard",
            (1, HeroKind.Rogue) => "Dagger throw",
            (1, HeroKind.Aegis) => "Barrier",
            (1, HeroKind.ShapeShifter) => "Shift",
            (1, HeroKind.Automaton) => "Self-Repair",
            (1, _) => "Charged Strike",
            (2, HeroKind.Warden) => "Shield Bash",
            (2, HeroKind.Vitalist) => "Rupture",
            (2, HeroKind.Elementalist) => "Snap",
            (2, HeroKind.Rogue) => "Recall",
            (2, HeroKind.Aegis) => "Burden",
            (2, HeroKind.ShapeShifter) => "Form special",
            (2, HeroKind.Automaton) => "Steam",
            (2, _) => "Heaving Swing",
            (3, HeroKind.Vitalist) => "Hex",
            (3, HeroKind.Elementalist) => "Updraft",
            (3, HeroKind.Rogue) => "Vanish",
            (3, HeroKind.Aegis) => "Bubble",
            (3, HeroKind.Automaton) => "Brace",
            (3, _) => "Dodge",
            _ => Capital(p.SupportName),
        };
    }

    private static string Capital(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..].ToLowerInvariant();

    private static string Kind(Enemy e) => string.IsNullOrEmpty(e.DisplayName) ? e.GetType().Name : e.DisplayName;

    /// <summary>This game's hero dealt a blow (<paramref name="hp0"/>: the creature's health before it). <paramref name="overTime"/>: an affliction's tick.</summary>
    public static void Dealt(Enemy e, float dmg, float hp0, bool overTime = false)
    {
        if (e == null || dmg <= 0) return;
        var m = Mine;
        float real = Math.Min(dmg, Math.Max(0f, hp0));
        m.Dealt += real;
        if (dmg > hp0 && hp0 > 0) m.Overkill += dmg - hp0;
        string src = overTime ? "Over time" : Now - _sourceT <= SourceWindow ? _source : "Other";
        m.DealtBy[src] = m.DealtBy.GetValueOrDefault(src) + real;
        m.DealtTo[Kind(e)] = m.DealtTo.GetValueOrDefault(Kind(e)) + real;
        if (e.IsBoss || e.IsGuardian) m.DealtBosses += real;
        else if (e.Elite) m.DealtElites += real;
        if (!overTime && _attackOpen && Now - _sourceT <= SourceWindow) { m.Hits++; _attackOpen = false; }
    }

    /// <summary>This game's hero slew a creature.</summary>
    public static void Killed(Enemy e)
    {
        if (e == null) return;
        var m = Mine;
        m.Kills++;
        if (e.IsBoss || e.IsGuardian) m.BossKills++;
        else if (e.Elite) m.EliteKills++;
        m.KillsOf[Kind(e)] = m.KillsOf.GetValueOrDefault(Kind(e)) + 1;
    }

    /// <summary>This game's hero took a blow (from a creature, if one is known).</summary>
    public static void Took(float dmg, Enemy from, string what = null)
    {
        if (dmg <= 0) return;
        var m = Mine;
        m.Taken += dmg;
        string k = what ?? (from != null && GodotObject.IsInstanceValid(from) ? Kind(from) : "The cave");
        m.TakenFrom[k] = m.TakenFrom.GetValueOrDefault(k) + dmg;
        if (from != null && GodotObject.IsInstanceValid(from))
        {
            if (from.IsBoss || from.IsGuardian) m.TakenBosses += dmg;
            else if (from.Elite) m.TakenElites += dmg;
        }
    }

    /// <summary>Damage kept off this game's hero: by its own guard (<paramref name="giver"/> = this game), or by a friend's boon.</summary>
    public static void Prevented(float amount, int giver)
    {
        if (amount <= 0) return;
        var m = Mine;
        if (giver == 0 || giver == Net.Me) m.MitigatedSelf += amount;
        else m.ProtectedBy[giver] = m.ProtectedBy.GetValueOrDefault(giver) + amount;
    }

    /// <summary>This game's hero was healed by <paramref name="giver"/>: what it gained, and what ran past full.</summary>
    public static void Healed(int giver, float gained, float over)
    {
        var m = Mine;
        if (giver == 0) giver = Net.Me;
        if (gained > 0) m.HealedBy[giver] = m.HealedBy.GetValueOrDefault(giver) + gained;
        if (over > 0) m.OverhealBy[giver] = m.OverhealBy.GetValueOrDefault(giver) + over;
    }

    /// <summary>A friend's game says a creature's affliction (one this game's hero gave it) took this much.</summary>
    public static void DealtOverTime(float amount)
    {
        if (amount <= 0) return;
        var m = Mine;
        m.Dealt += amount;
        m.DealtBy["Over time"] = m.DealtBy.GetValueOrDefault("Over time") + amount;
    }

    public static void Revived(int giver) { if (giver != 0) Mine.RevivedBy[giver] = Mine.RevivedBy.GetValueOrDefault(giver) + 1; }
    public static void Died() { Mine.Deaths++; Send(); }

    // ---- the totals as the page shows them: what this hero did for everyone (the others' records included)

    public static float MitigationGiven(int peer) => Get(peer).MitigatedSelf + By.Values.Sum(m => m.ProtectedBy.GetValueOrDefault(peer));
    public static float HealingGiven(int peer) => By.Values.Sum(m => m.HealedBy.GetValueOrDefault(peer));
    public static float OverhealGiven(int peer) => By.Values.Sum(m => m.OverhealBy.GetValueOrDefault(peer));
    public static int RevivesGiven(int peer) => By.Values.Sum(m => m.RevivedBy.GetValueOrDefault(peer));

    /// <summary>The heroes to set side by side: this game's first, then the others in the party.</summary>
    public static List<HeroMetrics> Party()
    {
        var me = Mine;
        var p = G.Player;
        if (p != null && GodotObject.IsInstanceValid(p)) me.Hero = p.Stats.Hero;
        me.Name = Net.Online ? (Net.Mine?.Name ?? "You") : "You";
        var list = new List<HeroMetrics> { me };
        foreach (var (peer, m) in By.OrderBy(kv => kv.Key)) if (peer != Net.Me && (Net.Online || m.Attacks + m.Dealt > 0)) list.Add(m);
        return list;
    }

    // ---- online: each game's record to the others, every few seconds and when asked

    public static void Tick(float dt)
    {
        if (!Net.Online || !Net.InRun) return;
        if ((_sendT -= dt) > 0) return;
        _sendT = 4f;
        Send();
    }

    public static void Send()
    {
        if (!Net.Online) return;
        var me = Mine;
        var p = G.Player;
        if (p != null && GodotObject.IsInstanceValid(p)) me.Hero = p.Stats.Hero;
        me.Name = Net.Mine?.Name ?? "Player";
        me.Peer = Net.Me;
        var w = new NetOut(Net.Msg.Metrics);
        me.Write(w);
        Net.SendAll(w, true);
    }

    public static void OnNet(NetIn r)
    {
        var m = HeroMetrics.Read(r);
        if (m.Peer == Net.Me) return;
        By[m.Peer] = m;
    }
}
