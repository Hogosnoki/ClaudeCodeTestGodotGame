using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

public partial class Main
{
    private IEnumerator<object> _meetSteps;

    /// <summary>
    /// `--scenario=meetings`: the sounds of one thing meeting another (Audio/MaterialSounds.cs). Every strike on every body is made and is
    /// its own (no two alike); a sword's blow on a golem is steel on stone and on a scorpion steel on shell, a fireball's on the golem is fire
    /// on stone; a bear's claws on the Swordsman are a blade on flesh, and a thrown rock that misses clacks on the cave's stone.
    /// </summary>
    private void MeetingsScenario()
    {
        if (_meetSteps == null) { if (_scT < 0.8f) return; _meetSteps = MeetingsRun().GetEnumerator(); }
        if (!_meetSteps.MoveNext()) ScEnd();
    }

    /// <summary>A sound's fingerprint: its loudness over five slices of time, and how bright it is (zero crossings a second) in each.</summary>
    private static float[] Fingerprint(float[] b)
    {
        var f = new float[10];
        int n = b.Length / 5;
        for (int k = 0; k < 5; k++)
        {
            double e = 0; int zc = 0;
            for (int i = k * n + 1; i < (k + 1) * n; i++) { e += b[i] * b[i]; if ((b[i] >= 0) != (b[i - 1] >= 0)) zc++; }
            f[k] = (float)Math.Sqrt(e / n);
            f[5 + k] = zc / (float)n;
        }
        float peak = Math.Max(1e-6f, f.Take(5).Max());
        for (int k = 0; k < 5; k++) f[k] /= peak;
        return f;
    }

    private IEnumerable<object> MeetingsRun()
    {
        var p = G.Player;
        foreach (var e in G.Enemies.ToArray()) { G.Enemies.Remove(e); e.QueueFree(); }
        p.Stats.MaxHp = p.Hp = 5000;
        foreach (var _ in ElWait(0.3f)) yield return null;

        // ---- every pair made, each its own
        var prints = new Dictionary<(Strike, Body), float[]>();
        int empty = 0;
        foreach (Strike s in Enum.GetValues<Strike>())
            foreach (Body b in Enum.GetValues<Body>())
            {
                var buf = MaterialSounds.Build(s, b, ElementSounds.StableHash(MaterialSounds.Name(s, b)));
                if (buf.All(x => Math.Abs(x) < 1e-4f)) empty++;
                prints[(s, b)] = Fingerprint(buf);
            }
        float Dist((Strike, Body) a, (Strike, Body) c) => prints[a].Zip(prints[c], (x, y) => Math.Abs(x - y)).Sum();
        var keys = prints.Keys.ToList();
        float closest = float.MaxValue; string closestPair = "";
        for (int i = 0; i < keys.Count; i++)
            for (int j = i + 1; j < keys.Count; j++)
            {
                float d = Dist(keys[i], keys[j]);
                if (d < closest) { closest = d; closestPair = $"{keys[i]} / {keys[j]}"; }
            }
        ScCheck($"every strike on every body has a sound of its own ({keys.Count} made, {empty} silent; the closest two are {closestPair}, {closest:0.000} apart)", empty == 0 && closest > 0.02f);
        ScCheck($"  a sword on stone, on a shell and on flesh are three different sounds ({Dist((Strike.Blade, Body.Stone), (Strike.Blade, Body.Chitin)):0.00}, {Dist((Strike.Blade, Body.Stone), (Strike.Blade, Body.Flesh)):0.00} apart); fire on stone isn't a sword on stone ({Dist((Strike.Fire, Body.Stone), (Strike.Blade, Body.Stone)):0.00})",
            Dist((Strike.Blade, Body.Stone), (Strike.Blade, Body.Chitin)) > 0.15f && Dist((Strike.Blade, Body.Stone), (Strike.Blade, Body.Flesh)) > 0.15f && Dist((Strike.Fire, Body.Stone), (Strike.Blade, Body.Stone)) > 0.15f);
        float secs = G.Sfx.MeetingSeconds(Strike.Blade, Body.Stone);
        ScCheck($"  the sound bank makes one when asked ({secs:0.00} s)", secs > 0.1f);

        // ---- in the game
        string Heard(Action act) { G.Sfx.LastMeeting = ""; act(); return G.Sfx.LastMeeting; }
        var golem = new Golem { Position = p.GlobalPosition + new Vector2(60, -20) };
        var scorp = new Scorpion { Position = p.GlobalPosition + new Vector2(-60, -20) };
        foreach (var e in new Enemy[] { golem, scorp }) { e.SetMeta("test", true); _world.AddChild(e); e.Freeze(99f, hold: true); }
        foreach (var _ in ElWait(0.3f)) yield return null;
        string swordOnGolem = Heard(() => golem.Hurt(5f, Vector2.Zero, golem.GlobalPosition));
        string fireOnGolem = Heard(() => golem.Hurt(5f, Vector2.Zero, golem.GlobalPosition, DamageKind.Fire));
        string swordOnShell = Heard(() => scorp.Hurt(5f, Vector2.Zero, scorp.GlobalPosition));
        ScCheck($"the {p.Stats.Hero}'s blow on a golem is {swordOnGolem} (blade on stone), fire on it {fireOnGolem} (fire on stone), the blow on a scorpion {swordOnShell} (blade on shell)",
            swordOnGolem == MaterialSounds.Name(Strike.Blade, Body.Stone) && fireOnGolem == MaterialSounds.Name(Strike.Fire, Body.Stone) && swordOnShell == MaterialSounds.Name(Strike.Blade, Body.Chitin));
        var bear = new Bear { Position = p.GlobalPosition + new Vector2(0, -40) };
        bear.SetMeta("test", true); _world.AddChild(bear); bear.Freeze(99f, hold: true);
        foreach (var _ in ElWait(0.3f)) yield return null;
        p.Stats.HurtInvuln = 0f;
        string clawOnHero = Heard(() => p.Hurt(5f, bear.GlobalPosition, 100f, bear));
        ScCheck($"a bear's claws on the {p.Stats.Hero} are {clawOnHero} (blade on {p.BodyMaterial})", clawOnHero == MaterialSounds.Name(Strike.Blade, p.BodyMaterial));
        foreach (var e in new Enemy[] { golem, scorp, bear }) if (IsInstanceValid(e)) { G.Enemies.Remove(e); e.QueueFree(); }
    }
}
