using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Godot;

namespace DaggerCave;

/// <summary>
/// What the game keeps about this session as it goes, for problem reports (see <see cref="BugReports"/>): the player never has to say it.
/// A session id; how long the game has been open and how long was spent actually playing; every run and cave of the session (seed, depth,
/// cave kind, hero, party, when and for how long); a trail of the last things that happened (a cave entered, a death, a pause, an unstick,
/// a menu opened, a player joining...); and, when a report is made, who is in the party right now.
/// </summary>
public static class SessionLog
{
    public static readonly string Id = Guid.NewGuid().ToString("N")[..8];
    private static readonly Stopwatch Open = Stopwatch.StartNew();
    private static readonly DateTime StartedUtc = DateTime.UtcNow;

    /// <summary>Seconds of the session spent in a run (not paused, not at a menu).</summary>
    public static float PlaySeconds;
    public static int Runs, Deaths;

    private sealed class Visit
    {
        public int Seed, Depth, Run;
        public string Biome, Hero;
        public int Players;
        public double AtSeconds;
        public float PlayStart;
    }

    private static readonly List<Visit> Visits = new();
    private static readonly List<(double at, string text)> Trail = new();
    private const int TrailMax = 90;

    private static string Clock(double s) => $"{(int)(s / 60):0}:{(int)(s % 60):00}";

    /// <summary>A line in the trail (the last 90 are kept).</summary>
    public static void Note(string text)
    {
        Trail.Add((Open.Elapsed.TotalSeconds, text));
        if (Trail.Count > TrailMax) Trail.RemoveAt(0);
    }

    /// <summary>Every frame in a run.</summary>
    public static void Tick(float dt) => PlaySeconds += dt;

    public static void RunStarted(string hero, int players)
    {
        Runs++;
        Note($"run {Runs} started as {hero}{(players > 1 ? $" with {players} players" : "")}");
    }

    public static void RunEnded(string how, int depth, float runSeconds)
    {
        if (how == "died") Deaths++;
        Note($"run {Runs} ended: {how} at depth {depth} after {Clock(runSeconds)}");
    }

    public static void LevelEntered(CaveData cave)
    {
        if (cave == null) return;
        Visits.Add(new Visit
        {
            Seed = cave.Seed, Depth = G.Depth, Run = Runs, Biome = cave.Biome?.Name ?? "?", Hero = G.Hero.ToString(),
            Players = Math.Max(1, G.Players.Count), AtSeconds = Open.Elapsed.TotalSeconds, PlayStart = PlaySeconds,
        });
        Note($"entered {cave.Biome?.Name} (depth {G.Depth}), seed {cave.Seed}");
    }

    /// <summary>The session as text: the part of every report.</summary>
    public static string Describe()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"session {Id}, started {StartedUtc:yyyy-MM-dd HH:mm} UTC, open {Clock(Open.Elapsed.TotalSeconds)}, playing {Clock(PlaySeconds)}");
        sb.AppendLine($"runs this session {Runs}, deaths {Deaths}, caves entered {Visits.Count}, run time now {Clock(G.RunTime)} (depth {G.Depth})");
        // --- the party now ---
        var party = G.Players.Where(p => p != null && GodotObject.IsInstanceValid(p)).ToList();
        sb.AppendLine($"players now: {party.Count}{(Net.Online ? $" (online, {(Net.IsHost ? "host" : "guest")})" : " (offline)")}");
        foreach (var p in party)
        {
            string line = $"  {(p.IsRemote ? "remote" : "local ")} {p.Hero}: level {p.Level}, {p.Hp:0}/{p.Stats.MaxHp:0} HP, {p.Kills} kills{(p.Dead ? ", DOWN" : "")}";
            sb.AppendLine(line);
        }
        // --- the caves of the session ---
        if (Visits.Count > 0)
        {
            sb.AppendLine("caves of this session (seed · depth · cave · hero · players · when · how long):");
            for (int k = 0; k < Visits.Count; k++)
            {
                var v = Visits[k];
                double end = k + 1 < Visits.Count ? Visits[k + 1].AtSeconds : Open.Elapsed.TotalSeconds;
                sb.AppendLine($"  run {v.Run}: {v.Seed} · depth {v.Depth} · {v.Biome} · {v.Hero} · {v.Players}p · at {Clock(v.AtSeconds)} · {Clock(end - v.AtSeconds)}");
            }
        }
        sb.AppendLine($"frame rate {Engine.GetFramesPerSecond():0} fps");
        return sb.ToString();
    }

    /// <summary>The trail of recent events, oldest first, each with the session time.</summary>
    public static string TrailText()
    {
        if (Trail.Count == 0) return "(nothing yet)";
        return string.Join("\n", Trail.Select(t => $"{Clock(t.at)}  {t.text}"));
    }
}
