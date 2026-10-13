using System;
using Godot;

namespace DaggerCave;

/// <summary>
/// Wind-ups. Some casts take a moment to gather before they go off (a heal 0.4 s, a Rupture 0.8 s, the Aegis's shields 0.25 s, the
/// Elementalist's firebolt 0.2 s, frostbolt 0.1 s, Blizzard 0.5 s and Stalag-Might 1 s, the Rogue's recall 0.4 s). The cost is paid and
/// the cooldown started when the button is pressed; then, for the wind-up, the hero is slowed to a tenth (Tune.Hold.MoveShare: walking, swimming and falling alike; a rise or fall in progress is paused, not reset: it carries on when the wind-up ends), with no jumping,
/// no dodging, animated in the cast to show it is under way (the animation lasts as long as the wind-up does);
/// then the cast goes off. A stun, a freeze, a web, a snag or a death cuts it short and the cast is lost.
/// </summary>
public partial class Player
{
    private float _holdT, _holdTotal;
    private Action _holdFire;

    /// <summary>The hero is gathering a cast: slowed to a tenth (the Rupture's seizing counts too).</summary>
    public bool Holding => _holdT > 0f || _ruptureT >= 0f;

    /// <summary>The length of the last wind-up begun (for the tests).</summary>
    public float LastHold { get; private set; }
    /// <summary>For the tests: the farthest a hero may get in a wind-up of <paramref name="seconds"/> while slowed to a tenth (pushing the whole time).</summary>
    public float HoldDriftAllowance(float seconds) => Math.Max(RunSpeed * Stats.MoveSpeed, MaxFall) * Tune.Hold.MoveShare * seconds * 1.3f + 1f;
    /// <summary>The farthest the hero got from where the last wind-up began, while it lasted (for the tests: it should be nothing).</summary>
    public float LastHoldDrift { get; private set; }
    private Vector2 _holdPos;
    private float _holdVy;

    /// <summary>Test aid: the resources the casts spend, full.</summary>
    public void TestFill() { VitalForce = Stats.VitalForceMax; Alimus = Stats.AlimusMax; Fuel = Tune.AutomatonHero.FuelStart; }

    /// <summary>Test aid: lets a wind-up run on by <paramref name="dt"/> (a scenario that drives a cast by hand, outside the physics loop).</summary>
    public void TestHoldTick(float dt) => TickHold(dt);

    /// <summary>For the 3D model and the HUD: 0..1 through the wind-up, or -1 when there is none.</summary>
    public float HoldProgress => _holdT > 0f ? 1f - _holdT / Math.Max(0.01f, _holdTotal) : -1f;

    /// <summary>
    /// Begins a wind-up: for <paramref name="seconds"/> the hero holds still, animated as <paramref name="clip"/>, and then
    /// <paramref name="fire"/> goes off. A wind-up of nothing fires at once.
    /// </summary>
    private void Hold(float seconds, string clip, Action fire, float glow = 1f)
    {
        if (seconds <= 0.001f) { fire(); return; }
        LastHold = seconds;
        LastHoldDrift = 0f;
        _holdPos = GlobalPosition;
        _holdT = _holdTotal = seconds;
        _holdFire = fire;
        // (it slows at once, hanging in the air if you are in it; the rise or fall you were in is kept, and given back when the wind-up ends)
        _holdVy = Velocity.Y;
        Velocity *= Tune.Hold.MoveShare;
        Anim.OnceFor(clip, 3, seconds);
        _castGlow = Math.Max(_castGlow, glow * 0.6f);
    }

    private void CancelHold()
    {
        if (_holdT > 0f) ResumeFromHold();
        _holdT = 0f;
        _holdFire = null;
    }

    /// <summary>The wind-up is over: the rise or fall the hero was in when it began carries on (it is paused, not reset).</summary>
    private void ResumeFromHold()
    {
        if (!IsOnFloor() && !InWater) Velocity = new Vector2(Velocity.X, _holdVy);
    }

    private void TickHold(float dt)
    {
        if (_holdT <= 0f) return;
        if (Dead || Stunned || Frozen || Webbed || _snagT > 0f) { CancelHold(); return; }
        LastHoldDrift = Math.Max(LastHoldDrift, GlobalPosition.DistanceTo(_holdPos));
        _holdT -= dt;
        // (a faint gathering glow while it builds)
        _castGlow = Math.Max(_castGlow, 0.25f + 0.35f * HoldProgress);
        if (_holdT > 0f) return;
        ResumeFromHold();
        var fire = _holdFire;
        _holdFire = null;
        fire?.Invoke();
    }
}
