using System;
using Godot;

namespace DaggerCave;

/// <summary>
/// The rampage: kills in quick succession are counted (each within <see cref="Tune.Hordes.RampageWindow"/> s of the last), the count
/// shown over the hero from the third on, and at 5, 10, 20, 35 and every 25 after that the streak is called out and, from 10, paid:
/// a few seconds of haste, then of fury, then of both. It is the reward for wading into a horde and not letting up, and it ends the
/// moment you do (or are killed), so the hero feels strongest exactly where they are most exposed.
/// </summary>
public partial class Player
{
    /// <summary>The streak now, and the longest of this run.</summary>
    public int Streak { get; private set; }
    public int BestStreak { get; private set; }
    private float _streakT;

    private static readonly (int At, string Name)[] RampageSteps =
    {
        (5, "KILLING SPREE"), (10, "RAMPAGE"), (20, "SLAUGHTER"), (35, "UNSTOPPABLE"),
    };

    private void RampageKill()
    {
        if (IsRemote || Tune.Testing.NoHordes) return;
        Streak++;
        _streakT = Tune.Hordes.RampageWindow;
        BestStreak = Math.Max(BestStreak, Streak);
        var over = GlobalPosition + new Vector2(0, -34);
        string name = null;
        foreach (var (at, n) in RampageSteps) if (Streak == at) name = n;
        if (name == null && Streak > 35 && (Streak - 35) % 25 == 0) name = "LEGENDARY";
        if (name == null)
        {
            if (Streak >= 3) G.Fx.Text(over, $"x{Streak}", new Color(1f, 0.82f, 0.4f, 0.9f), 9, 0.5f, -16f);
            return;
        }
        var col = Streak >= 20 ? new Color(1f, 0.35f, 0.2f) : Streak >= 10 ? new Color(1f, 0.6f, 0.25f) : new Color(1f, 0.85f, 0.4f);
        G.Fx.Text(over + new Vector2(0, -8), $"{name}  x{Streak}", col, Streak >= 20 ? 15 : 13, 1.3f, -14f);
        G.Fx.Ring(GlobalPosition, 30f + Math.Min(40f, Streak), new Color(col, 0.8f), 0.35f, FxLayer.RingStyle.Burst);
        G.Fx.AddShake(Streak >= 20 ? 3f : 1.5f);
        G.Sfx.Play("roar", GlobalPosition, -8, 0.05f, 1.2f + 0.1f * Math.Min(5, Streak / 10));
        float secs = Tune.Hordes.RampageSeconds;
        if (Streak >= 10 && Streak < 20) GiveBuff(BuffHaste, secs);
        else if (Streak >= 20 && Streak < 35) GiveBuff(BuffFury, secs);
        else if (Streak >= 35) { GiveBuff(BuffHaste, secs + 2f); GiveBuff(BuffFury, secs + 2f); }
    }

    private void TickRampage(float dt)
    {
        if (Streak == 0) return;
        if ((_streakT -= dt) > 0f && !Dead) return;
        if (Streak >= 5) G.Fx.Text(GlobalPosition + new Vector2(0, -34), $"streak {Streak}", new Color(0.8f, 0.8f, 0.8f, 0.8f), 9, 0.9f, -10f);
        Streak = 0;
    }
}
