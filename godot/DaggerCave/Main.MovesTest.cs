using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

public partial class Main
{
    private IEnumerator<object> _m2Steps;

    /// <summary>
    /// `--scenario=moves`: every creature's second attack (Enemies/Enemy.Secondary.cs). Each kind that has one is set down beside a
    /// sturdy hero with its move ready: it must wind up (its warning shape drawn), strike, land on the hero and recover, then go back
    /// to its own ways. Pictures of each kind of move mid wind-up (moves_&lt;kind&gt;).
    /// </summary>
    private void MovesScenario()
    {
        if (_m2Steps == null) { if (_scT < 0.8f) return; _m2Steps = MovesRun().GetEnumerator(); }
        if (!_m2Steps.MoveNext()) ScEnd();
    }

    private IEnumerable<object> MovesRun()
    {
        var p = G.Player;
        foreach (var e in G.Enemies.ToArray()) { G.Enemies.Remove(e); e.QueueFree(); }
        _genSpots.Clear();
        p.Stats.MaxHp = p.Hp = 50000;
        p.InputOverride = () => default;
        foreach (var _ in ElWait(0.5f)) yield return null;
        var home = p.GlobalPosition;
        var shotKinds = new HashSet<Move2>();
        var bad = new List<string>();
        int good = 0, skipped = 0;
        // (--only=Bear,Scorpion: just those, for a quick look)
        var only = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--only="))?[7..].Split(',').ToHashSet();
        foreach (var type in Enemy.Move2Kinds)
        {
            if (only != null && !only.Contains(type.Name)) { skipped++; continue; }
            // (fish and eels live in water: the entrance has none to set them in)
            if (type == typeof(Fish) || type == typeof(Eel)) { skipped++; continue; }
            p.GlobalPosition = home; p.Velocity = Vector2.Zero; p.Hp = p.Stats.MaxHp;
            var e = (Enemy)Activator.CreateInstance(type);
            var m = e.Move2Of;
            float reach = m.Kind == Move2.Burst ? Math.Min(m.MaxRange, m.Radius * 0.85f) : m.MaxRange;
            float dist = Math.Clamp((m.MinRange + reach) * 0.5f, 30f, 150f);
            bool flies = type == typeof(Gasbag) || type == typeof(Bat) || type == typeof(Hornet) || type == typeof(FrostWraith) || type == typeof(Bomber) || type == typeof(Gasbag) || type == typeof(Mite);
            var at = home + new Vector2(dist, flies ? -50f : -20f);
            if (!flies && G.Cave.FindFloor(at + new Vector2(0, -30), 200, out var fl)) at = fl - new Vector2(0, 14);
            e.Position = at;
            e.Engage();
            _world.AddChild(e);
            foreach (var _ in ElWait(0.25f)) yield return null;
            if (!IsInstanceValid(e)) { bad.Add($"{type.Name}: gone at once"); continue; }
            e.Move2Ready();
            bool wound = false, struck = false, shot = false;
            float hp0 = p.Hp;
            float retry = 1f;
            for (float t = 0; t < 8f; t += (float)GetProcessDeltaTime())
            {
                if (!IsInstanceValid(e) || e.Dead) { if (wound) struck = true; break; }
                // (one busy with its own attack, or not yet in range, is asked again)
                if (!wound && (retry -= (float)GetProcessDeltaTime()) <= 0f) { retry = 1f; e.Move2Ready(); }
                if (e.Move2Phase == 1) wound = true;
                if (e.Move2Phase >= 2) struck = true;
                if (!shot && e.Move2Phase == 1 && e.Move2PhaseT > m.Windup * 0.6f && shotKinds.Add(m.Kind)) { ScShot($"moves_{m.Kind.ToString().ToLowerInvariant()}"); shot = true; }
                if (struck && e.Move2Phase == 0) { foreach (var _ in ElWait(0.6f)) yield return null; break; }
                yield return null;
            }
            // (a shot's blow is the shot's: what the hero lost says whether it landed)
            bool shoots = m.Kind is Move2.Spit or Move2.Volley or Move2.Spines or Move2.Shatter;
            if (m.Finale) foreach (var _ in ElWait(1.5f)) yield return null; // (icicles take a moment to come round)
            bool spent = !IsInstanceValid(e) || e.Dead;
            bool landed = e.Move2Hits > 0 || (shoots && p.Hp < hp0);
            // (a finale spends the creature; anything else goes back to its own ways)
            bool back = m.Finale ? spent : IsInstanceValid(e) && !e.InMove2;
            string line = $"{type.Name}: {m.Name} ({m.Kind}) wound up {wound}, struck {struck}, landed {landed} ({hp0 - p.Hp:0} taken), {(m.Finale ? "spent" : "recovered")} {back}";
            GD.Print("[scenario]   " + line);
            // (a shot at a hero standing still lands; a lunge or a pounce may sail over: struck is enough for those)
            if (wound && struck && back && (landed || m.Kind is Move2.Lunge or Move2.Pounce or Move2.Dive)) good++; else bad.Add(line);
            if (IsInstanceValid(e)) { G.Enemies.Remove(e); e.QueueFree(); }
            foreach (var proj in EnemyProjectiles.ToArray()) if (IsInstanceValid(proj)) proj.QueueFree();
            foreach (var _ in ElWait(0.2f)) yield return null;
        }
        int total = Enemy.Move2Kinds.Count() - skipped;
        ScCheck($"every creature with a second attack winds it up, strikes and recovers ({good} of {total}; fish and eels need water){(bad.Count > 0 ? ": " + string.Join(" | ", bad) : "")}", good == total);
        if (only == null) ScCheck($"each kind of move was seen mid wind-up ({string.Join(", ", shotKinds)})", shotKinds.Count >= 9);
        p.InputOverride = null;
    }
}
