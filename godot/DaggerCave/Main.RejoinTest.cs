using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

/// <summary>
/// tools/rejointest.sh: getting back into a run (Main.Rejoin.cs), with three copies of the game on one machine.
///   --nettest=rjhost  hosts and starts a run.
///   --nettest=rjjoin  joins, grows its hero (levels, cards, a relic, gold, a wound), then its connection drops as if the network went:
///                     it must get straight back in by itself, never leaving the level for the title, as the very same hero. Then it
///                     dies outright (as a crash would), its hero written down.
///   --nettest=rjback  a fresh copy, joining with the same address: it must be put back into the run as that same hero (played back
///                     from what was written down).
/// </summary>
public partial class Main
{
    private string _rjSig = "";
    private bool _rjSawTitle, _rjSawTrying;
    private int _rjPeer;

    private void BeginRejoinTest()
    {
        GetTree().Paused = true;
        _state = State.Title;
        _hud.Visible = false;
        GameSettings.PlayerName = _netTest == "rjhost" ? "Hosty" : "Joiner";
        if (_netTest != "rjback") ForgetDropped(); // (nothing left over from an earlier test)
        GD.Print($"[nettest] {_netTest}: {(_netTest == "rjhost" ? "hosting" : $"joining {_netAddr}")} as the {G.Hero} (build {Net.Build})");
        if (_netTest == "rjhost") { if (!Net.Host()) NtFail($"hosting opens port {Net.Port} ({Net.Status})"); }
        else Net.Join(_netAddr);
        if (_netTest == "rjback") GD.Print($"[nettest] rjback: a hero kept on disk for {RejoinCode ?? "-"} (the {RejoinHero?.ToString() ?? "-"}); asking with token {Net.RejoinToken}");
    }

    /// <summary>What a hero is, to compare before and after (hero, level, xp, gold, keys, potions, picks, health, max health, damage, cards).</summary>
    private static string RjSig(Player p)
    {
        var cards = string.Join(",", p.Stats.Stacks.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}x{kv.Value}"));
        return string.Join("|", p.Stats.Hero, p.Level, p.Xp, p.Gold, p.Keys, p.Potions, p.PendingPicks, $"{p.Hp:0}", $"{p.Stats.MaxHp:0}", $"{p.Stats.DamageMult:0.000}", cards);
    }

    /// <summary>The same hero (health, max health and damage within 2%: a hero played back may round a little differently).</summary>
    private static bool RjSame(string a, string b, out string diff)
    {
        var x = a.Split('|'); var y = b.Split('|');
        diff = "";
        if (x.Length != y.Length) { diff = "shape"; return false; }
        string[] names = { "hero", "level", "xp", "gold", "keys", "potions", "picks", "hp", "maxhp", "dmg", "cards" };
        for (int k = 0; k < x.Length; k++)
        {
            bool near = k is 7 or 8 or 9;
            bool ok = near ? Math.Abs(float.Parse(x[k]) - float.Parse(y[k])) <= Math.Max(1f, Math.Abs(float.Parse(x[k])) * 0.02f) : x[k] == y[k];
            if (!ok) diff += $"{names[k]} {x[k]} vs {y[k]}; ";
        }
        return diff == "";
    }

    private void RejoinTestTick()
    {
        if (Net.IsHost && Net.InRun)
            foreach (var e in G.Enemies.ToArray())
                if (!e.Puppet && !e.IsQueuedForDeletion()) e.QueueFree();
        if (_netTest == "rjhost") RjHostStep();
        else if (_netTest == "rjjoin") RjJoinStep();
        else RjBackStep();
    }

    private void RjHostStep()
    {
        switch (_ntPhase)
        {
            case 0:
                if (NtGot("ready", out _)) _ntReady = true;
                if (_ntReady && Net.Count == 2 && Net.AllPicked)
                {
                    NtCheck("a friend joined the lobby", true);
                    HostStartRun();
                    G.Player.Stats.MaxHp = G.Player.Hp = 5000;
                    NtNext();
                }
                else if (_ntPhaseT > 60) NtFail("a friend joins the lobby");
                break;
            case 1:
                // (the friend drops and comes back by itself; then dies outright; a fresh copy of theirs comes back)
                if (NtGot("sig", out var sig)) { _rjSig = sig; GD.Print($"[nettest] rjhost: the friend's hero before it went: {sig}"); }
                if (NtGot("back-in", out var r)) NtCheck($"the friend dropped and came back into the run by itself ({r}; {G.Players.Count} heroes here)", true);
                if (NtGot("crashing", out _)) NtNext();
                else if (_ntPhaseT > 90) NtFail("the friend drops, comes back, then goes");
                break;
            case 2:
            {
                // (the fresh copy is in: tell it what the hero was)
                var friend = OtherHero();
                if (NtGot("in", out _)) { NtSay("sig " + _rjSig); }
                if (NtGot("done", out var how))
                {
                    NtCheck($"the fresh copy came back as the same hero ({how}); {G.Players.Count} heroes here, {Net.Count} players", G.Players.Count == 2 && friend != null);
                    _ntFinished = true;
                    NtNext();
                }
                else if (_ntPhaseT > 90) NtFail("a fresh copy of the friend's game comes back into the run");
                break;
            }
            case 3:
                if (_ntPhaseT > 3f || !Net.Online || Net.Count < 2) NtEnd();
                break;
        }
    }

    private void RjJoinStep()
    {
        var p = G.Player;
        if (_ntPhase >= 2 && (_state != State.Playing || _onlineMenu.Visible)) _rjSawTitle = true;
        switch (_ntPhase)
        {
            case 0:
                if (Net.Mine is { Picked: true }) { NtSay("ready"); NtNext(); }
                else if (_ntPhaseT > 60) NtFail($"join the host's lobby ({Net.Status})");
                break;
            case 1:
                if (Net.InRun && _state == State.Playing && _ntPhaseT > 1f && p != null)
                {
                    // (a hero with a history: levels, two of its cards, a relic, gold, keys, a wound)
                    p.GainLevels(3);
                    foreach (var u in Cards.Ability.Where(u => u.For != null && u.For.Contains(p.Stats.Hero)).Take(2)) Upgrades.Apply(u, p.Stats, p);
                    var relic = Upgrades.Relics.First(u => u.For == null && u.Rarity == RelicRarity.Common && !u.Id.Contains("deeper") && !u.Id.Contains("shortcut"));
                    Upgrades.Apply(relic, p.Stats, p);
                    p.Gold = 77; p.Keys = 1; p.Potions = 2;
                    p.Hp = MathF.Round(p.Stats.MaxHp * 0.6f);
                    p.Choosing = true;
                    _rjSig = RjSig(p);
                    GD.Print($"[nettest] rjjoin: my hero now: {_rjSig}");
                    NtNext();
                }
                else if (_ntPhaseT > 60) NtFail("the host starts the run");
                break;
            case 2:
                // (a moment for it to be written down, then the network goes)
                if (_ntPhaseT < 5f) break;
                _rjPeer = Net.Me;
                GD.Print("[nettest] rjjoin: the connection drops");
                Net.TestDrop();
                NtNext();
                break;
            case 3:
                _rjSawTrying |= _reconnecting;
                if (Net.InRun && !_reconnecting && G.Player != null && Net.Me != _rjPeer && _ntPhaseT > 1f)
                {
                    p = G.Player;
                    p.Choosing = true;
                    var now = RjSig(p);
                    NtCheck($"the drop went quietly: it stayed in the level trying to get back in ({_rjSawTrying}), and was back in by itself, never shown the title or the online menu ({!_rjSawTitle})", _rjSawTrying && !_rjSawTitle);
                    NtCheck($"as the very same hero ({now})", RjSame(_rjSig, now, out var diff) || Fail(diff));
                    NtSay($"back-in {now}");
                    NtNext();
                }
                else if (_ntPhaseT > 20) NtFail($"get back in by itself within 20 s (reconnecting {_reconnecting}, in run {Net.InRun}, {Net.Status})");
                break;
            case 4:
                if (_ntPhaseT < 5f) break;
                // (written down, then gone as a crash would go)
                RememberHero();
                NtSay("sig " + RjSig(G.Player));
                NtSay("crashing");
                NtNext();
                break;
            case 5:
                if (_ntPhaseT < 1f) break;
                GD.Print(_ntOk ? "[nettest] rjjoin PASS (and now it dies outright)" : "[nettest] rjjoin FAIL");
                OS.Kill(OS.GetProcessId());
                break;
        }
    }

    private bool Fail(string why) { GD.Print($"[nettest]      ({why})"); return false; }

    private void RjBackStep()
    {
        switch (_ntPhase)
        {
            case 0:
                if (Net.InRun && _state == State.Playing && G.Player != null && _ntPhaseT > 1f)
                {
                    G.Player.Choosing = true;
                    NtSay("in");
                    NtNext();
                }
                else if (_ntPhaseT > 60) NtFail($"a fresh copy joins the run ({Net.Status}, in run {Net.InRun})");
                break;
            case 1:
                if (NtGot("sig", out var before))
                {
                    var now = RjSig(G.Player);
                    bool same = RjSame(before, now, out var diff);
                    NtCheck($"put back into the run as the same hero, played back from disk (was {before}; now {now})", same || Fail(diff));
                    NtSay($"done {(same ? "same" : "different: " + diff)}");
                    _ntFinished = true;
                    NtNext();
                }
                else if (_ntPhaseT > 30) NtFail("the host says what the hero was");
                break;
            case 2:
                if (_ntPhaseT > 1.5f) { Net.Leave(); NtEnd(); }
                break;
        }
    }
}
