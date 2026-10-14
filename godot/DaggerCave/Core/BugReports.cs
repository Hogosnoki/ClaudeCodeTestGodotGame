using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Godot;

namespace DaggerCave;

/// <summary>
/// Problem reports from the game: everything worth knowing about where and how something went wrong, put together for the player to
/// send. The report has the player's own words (the only thing asked) and, automatically, the cave's seed (which makes the cave again), the
/// depth and cave kind, the hero, where they stand, a check of whether they are shut in, the map (a crop round them, and the whole of it
/// in the saved file), the build and machine, the look settings and the end of the game's log.
///
/// It goes three places: a file in user://bug_reports/ (the whole report), the clipboard (the same), and the browser, as a GitHub issue
/// on <see cref="Repo"/> already filled in (its title and a short body, kept short so the player's own default browser opens it; the
/// whole report is pasted from the clipboard). The game holds no
/// GitHub password or token: a token inside a game anyone can download is one anyone can take, so the player's own account sends it.
/// </summary>
public static class BugReports
{
    /// <summary>The GitHub repository the reports go to.</summary>
    public const string Repo = "hogosnoki/claudecodetestgodotgame";

    /// <summary>Tests turn this off: nothing opens a browser.</summary>
    public static bool OpenBrowser = true;

    public sealed class Report
    {
        public string Title, IssueBody, Full, Path, Url;
    }

    private static string Safe(Func<string> f)
    {
        try { return f(); } catch (Exception e) { return $"(not available: {e.Message})"; }
    }

    private static string Clock(double s) => $"{(int)(s / 60):0}:{(int)(s % 60):00}";

    /// <summary>The party in a line: how many players and who.</summary>
    private static string PartyLine()
    {
        var party = G.Players.Where(q => q != null && GodotObject.IsInstanceValid(q)).ToList();
        if (party.Count == 0) return "none";
        return $"{party.Count} player{(party.Count == 1 ? "" : "s")}{(Net.Online ? " (online)" : "")}: " + string.Join(", ", party.Select(q => $"{q.Hero}{(q.IsRemote ? " (remote)" : "")} L{q.Level}"));
    }

    /// <summary>What the report says about the run, short: the seed first.</summary>
    public static string Summary()
    {
        var c = G.Cave;
        if (c == null) return "no cave (at the camp)";
        return $"seed {c.Seed} · depth {G.Depth} · {G.Biome?.Name ?? "?"} · {G.Hero}";
    }

    // ---------------------------------------------------------------- the map

    private sealed class MapInfo
    {
        public string Crop, Full;
        public int Reachable, OpenTotal, Cols, Rows;
        public bool StartReached, ExitReached, Shut;
    }

    private static MapInfo Analyse(CaveData cave, Vector2 at)
    {
        int W = cave.W, H = cave.H;
        var open = new bool[W * H];
        int openTotal = 0;
        for (int j = 0; j < H; j++)
            for (int i = 0; i < W; i++)
                if (open[j * W + i] = cave.CellOpen(i, j)) openTotal++;
        int pi = Math.Clamp((int)(at.X / CaveData.Cell), 0, W - 1), pj = Math.Clamp((int)(at.Y / CaveData.Cell), 0, H - 1);
        // (the flood fill starts at the open cell nearest the player, in case they stand in the rock's edge)
        int start = -1;
        for (int r = 0; r < 6 && start < 0; r++)
            for (int dj = -r; dj <= r && start < 0; dj++)
                for (int di = -r; di <= r; di++)
                {
                    int i = pi + di, j = pj + dj;
                    if (i >= 0 && j >= 0 && i < W && j < H && open[j * W + i]) { start = j * W + i; break; }
                }
        var seen = new bool[W * H];
        int reached = 0;
        if (start >= 0)
        {
            var q = new Queue<int>();
            q.Enqueue(start); seen[start] = true;
            while (q.Count > 0)
            {
                int cur = q.Dequeue(); reached++;
                int ci = cur % W, cj = cur / W;
                for (int d = 0; d < 4; d++)
                {
                    int ni = ci + (d == 0 ? 1 : d == 1 ? -1 : 0), nj = cj + (d == 2 ? 1 : d == 3 ? -1 : 0);
                    if (ni < 0 || nj < 0 || ni >= W || nj >= H) continue;
                    int n = nj * W + ni;
                    if (open[n] && !seen[n]) { seen[n] = true; q.Enqueue(n); }
                }
            }
        }
        bool Reached(Vector2 p)
        {
            int i = Math.Clamp((int)(p.X / CaveData.Cell), 0, W - 1), j = Math.Clamp((int)(p.Y / CaveData.Cell), 0, H - 1);
            for (int r = 0; r < 4; r++)
                for (int dj = -r; dj <= r; dj++)
                    for (int di = -r; di <= r; di++)
                    {
                        int a = i + di, b = j + dj;
                        if (a >= 0 && b >= 0 && a < W && b < H && seen[b * W + a]) return true;
                    }
            return false;
        }
        char Glyph(int i, int j)
        {
            if (i == pi && j == pj) return 'P';
            if (cave.StartPos != default && i == (int)(cave.StartPos.X / CaveData.Cell) && j == (int)(cave.StartPos.Y / CaveData.Cell)) return 'S';
            if (!open[j * W + i]) return 'X';
            var p = new Vector2((i + 0.5f) * CaveData.Cell, (j + 0.5f) * CaveData.Cell);
            if (cave.IsLava(p)) return '^';
            if (cave.IsWater(p)) return '~';
            // (open air the player's cell can't reach: ',')
            return seen[j * W + i] ? '.' : ',';
        }
        var info = new MapInfo { Reachable = reached, OpenTotal = openTotal, Cols = W, Rows = H };
        var sb = new StringBuilder();
        for (int j = 0; j < H; j++)
        {
            for (int i = 0; i < W; i++) sb.Append(Glyph(i, j));
            sb.Append('\n');
        }
        info.Full = sb.ToString();
        sb.Clear();
        int x0 = Math.Max(0, pi - 30), x1 = Math.Min(W - 1, pi + 30), y0 = Math.Max(0, pj - 11), y1 = Math.Min(H - 1, pj + 11);
        for (int j = y0; j <= y1; j++)
        {
            for (int i = x0; i <= x1; i++) sb.Append(Glyph(i, j));
            sb.Append('\n');
        }
        info.Crop = sb.ToString();
        info.StartReached = cave.StartPos != default && Reached(cave.StartPos);
        info.ExitReached = cave.Boss != null && Reached(cave.Boss.Floor);
        info.Shut = reached < 400 || (cave.StartPos != default && !info.StartReached);
        return info;
    }

    /// <summary>
    /// Whether the start of a fresh cave is walled in: the open air joined to the start is under 400 cells, or leaves the guardian's room out
    /// of reach (both by the open cells alone: breakable rock and ledges are not counted, so this can be wrong about a cave only a break-out
    /// leaves, which is why it only warns).
    /// </summary>
    public static bool StartShutIn(CaveData cave, out int reached, out int total)
    {
        var m = Analyse(cave, cave.StartPos);
        reached = m.Reachable; total = m.OpenTotal;
        return m.Reachable < 400 || (cave.Boss != null && !m.ExitReached);
    }

    // ---------------------------------------------------------------- the report

    /// <summary>Puts a report together from the player's words, saves it, copies it, and (when asked) opens the browser on the issue.</summary>
    public static Report Make(string complaint, bool send = true)
    {
        var cave = G.Cave;
        var p = G.Player;
        string stamp = Time.GetDatetimeStringFromSystem(false, true).Replace(":", "").Replace("-", "").Replace(" ", "_");
        complaint = (complaint ?? "").Trim();
        if (complaint == "") complaint = "(nothing written)";
        string firstLine = complaint.Split('\n')[0];
        if (firstLine.Length > 70) firstLine = firstLine[..70] + "…";

        var at = p != null && GodotObject.IsInstanceValid(p) ? p.GlobalPosition : (cave?.StartPos ?? Vector2.Zero);
        MapInfo map = null;
        if (cave != null) { try { map = Analyse(cave, at); } catch (Exception e) { GD.PrintErr(e); } }

        string who = Safe(() => $"{G.Hero}{(p != null ? $", {p.Hp:0}/{p.Stats.MaxHp:0} HP, {p.Kills} kills" : "")}");
        string where = cave == null ? "at the camp (no cave)" : $"{G.Biome?.Name} (depth {G.Depth})";
        string cell = cave == null ? "" : $"cell {(int)(at.X / CaveData.Cell)},{(int)(at.Y / CaveData.Cell)} of {cave.W}x{cave.H}";
        string shut = map == null ? "" : map.Shut
            ? $"**SHUT IN?** The air the player's cell touches is {map.Reachable} cells of {map.OpenTotal} open ones; the start {(map.StartReached ? "is" : "is NOT")} reached, the guardian's room {(map.ExitReached ? "is" : "is NOT")} reached."
            : $"Not shut in: {map.Reachable} of {map.OpenTotal} open cells reached from the player's cell; the start {(map.StartReached ? "is" : "is NOT")} reached, the guardian's room {(map.ExitReached ? "is" : "is NOT")} reached.";

        // --- the short report (the issue's body: it has to fit in an address) ---
        var b = new StringBuilder();
        b.AppendLine(complaint);
        b.AppendLine();
        b.AppendLine("| | |");
        b.AppendLine("|---|---|");
        b.AppendLine($"| Seed | `{cave?.Seed.ToString() ?? "-"}` |");
        b.AppendLine($"| Cave | {where} |");
        b.AppendLine($"| Hero | {who} |");
        b.AppendLine($"| Where | {cell} |");
        b.AppendLine($"| Build | `{Net.Build}`{(Net.Online ? " (online)" : "")} |");
        b.AppendLine($"| Session | `{SessionLog.Id}` · playing {Clock(SessionLog.PlaySeconds)} · run {Clock(G.RunTime)} · run {SessionLog.Runs}, {SessionLog.Deaths} deaths |");
        b.AppendLine($"| Party | {PartyLine()} |");
        b.AppendLine();
        if (shut != "") { b.AppendLine(shut); b.AppendLine(); }
        if (map != null)
        {
            b.AppendLine("Map round the player (`X` rock, `.` air they can reach, `,` air they can't, `~` water, `^` lava, `P` player, `S` start):");
            b.AppendLine("```");
            b.Append(map.Crop);
            b.AppendLine("```");
        }
        b.AppendLine("_Reported from the game (the whole map, the settings and the log are in the report file it saved)._");
        string issueBody = b.ToString();

        // --- the whole report (the file, and the clipboard) ---
        var f = new StringBuilder();
        f.AppendLine($"# Dagger Deep problem report — {stamp}");
        f.AppendLine();
        f.Append(issueBody.Replace("_Reported from the game (the whole map, the settings and the log are in the report file it saved)._\n", ""));
        f.AppendLine();
        f.AppendLine("## Session");
        f.AppendLine("```");
        f.AppendLine(SessionLog.Describe());
        f.AppendLine("```");
        f.AppendLine();
        f.AppendLine("## What happened just before (the game's own trail)");
        f.AppendLine("```");
        f.AppendLine(SessionLog.TrailText());
        f.AppendLine("```");
        f.AppendLine();
        f.AppendLine("## Machine");
        f.AppendLine("```");
        f.AppendLine(Safe(() => $"Godot {Engine.GetVersionInfo()["string"]}, {OS.GetName()} {OS.GetVersion()}, {OS.GetProcessorName()}"));
        f.AppendLine(Safe(() => $"GPU {RenderingServer.GetVideoAdapterName()} ({RenderingServer.GetVideoAdapterVendor()}), {DisplayServer.WindowGetSize().X}x{DisplayServer.WindowGetSize().Y}, render scale {GameSettings.RenderScale}"));
        f.AppendLine(Safe(() => $"run {G.RunTime:0}s, state {(G.Main != null ? "in game" : "?")}, enemies {G.Enemies.Count}"));
        f.AppendLine("```");
        if (cave != null)
        {
            f.AppendLine();
            f.AppendLine("## Cave");
            f.AppendLine("```");
            f.AppendLine($"seed {cave.Seed}, {cave.W}x{cave.H} cells, biome {cave.Biome?.Id}, liquid {cave.Liquid}, start {cave.StartPos}, attempts {cave.Attempts}, reachable (generator) {cave.ReachableCells}, trap cells {cave.TrapCells}, fine-reach ok {cave.FineOk} (repairs {cave.FineRepairs}), dry ok {cave.DryOk}");
            f.AppendLine($"rooms {cave.Rooms.Count}, ledges {cave.Ledges.Count}, vault {(cave.Vault != null)}");
            f.AppendLine("```");
        }
        if (map != null)
        {
            f.AppendLine();
            f.AppendLine("## The whole map (`X` rock, `.` air the player can reach, `,` air they can't, `~` water, `^` lava, `P` player, `S` start)");
            f.AppendLine("```");
            f.Append(map.Full);
            f.AppendLine("```");
        }
        f.AppendLine();
        f.AppendLine("## Look settings");
        f.AppendLine("```");
        f.AppendLine(Safe(DebugTuning.Dump));
        f.AppendLine("```");
        f.AppendLine();
        f.AppendLine("## End of the game's log");
        f.AppendLine("```");
        f.AppendLine(Safe(LogTail));
        f.AppendLine("```");
        string full = f.ToString();

        // --- save, copy, open ---
        string path = "";
        try
        {
            DirAccess.MakeDirRecursiveAbsolute("user://bug_reports");
            string rel = $"user://bug_reports/{stamp}_seed{cave?.Seed.ToString() ?? "none"}.md";
            using var file = Godot.FileAccess.Open(rel, Godot.FileAccess.ModeFlags.Write);
            file?.StoreString(full);
            path = ProjectSettings.GlobalizePath(rel);
        }
        catch (Exception e) { GD.PrintErr(e); }
        string title = $"[Report] {firstLine}";
        var rep = new Report { Title = title, IssueBody = issueBody, Full = full, Path = path, Url = IssueUrl(title, issueBody) };
        if (send)
        {
            DisplayServer.ClipboardSet(full);
            if (OpenBrowser) OS.ShellOpen(rep.Url);
        }
        GD.Print($"[report] {title} -> {path}");
        return rep;
    }

    /// <summary>The longest address handed to the system: Windows passes a longer one past the player's own browser to Edge, so the
    /// address stays short and the whole report waits on the clipboard to be pasted in.</summary>
    public const int MaxUrl = 1900;

    /// <summary>The address of a new issue on the repository with its title and as much of the body as fits in <see cref="MaxUrl"/>
    /// (the map crop goes first; then the body is cut, with a note to paste the whole report from the clipboard).</summary>
    private static string IssueUrl(string title, string body)
    {
        string Url(string bd) => $"https://github.com/{Repo}/issues/new?labels=bug-report&title={Uri.EscapeDataString(title)}&body={Uri.EscapeDataString(bd)}";
        const string paste = "\n\n**The whole report is on your clipboard: paste it here (Ctrl+V) before submitting.**\n";
        string u = Url(body);
        if (u.Length <= MaxUrl) return u;
        int a = body.IndexOf("Map round the player", StringComparison.Ordinal);
        int z = body.IndexOf("_Reported from the game", StringComparison.Ordinal);
        if (a >= 0 && z > a) body = body[..a] + body[z..];
        u = Url(body + paste);
        if (u.Length <= MaxUrl) return u;
        // (cut the body down until it fits, keeping the start: the player's words and the seed come first)
        int keep = body.Length;
        while (keep > 0 && Url(body[..keep] + paste).Length > MaxUrl) keep -= Math.Max(20, keep / 10);
        return Url(body[..Math.Max(0, keep)] + paste);
    }

    /// <summary>The last lines of the game's own log file (Godot writes it as it runs), or a note that there is none.</summary>
    private static string LogTail()
    {
        const string path = "user://logs/godot.log";
        if (!Godot.FileAccess.FileExists(path)) return "(no log file)";
        var lines = Godot.FileAccess.GetFileAsString(path).Split('\n');
        return string.Join("\n", lines.Skip(Math.Max(0, lines.Length - 70)));
    }
}
