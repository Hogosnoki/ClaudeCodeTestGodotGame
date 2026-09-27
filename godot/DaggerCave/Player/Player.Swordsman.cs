using System;
using Godot;

namespace DaggerCave;

/// <summary>
/// The Swordsman's kit: a quick dodge roll on a short cooldown (a swing can be started out of
/// it), and the Charged Strike, which empowers the next swing without breaking a combo.
/// </summary>
public partial class Player
{
    private static float DodgeSpeed => Tune.Hero.DodgeSpeed;
    private static float DodgeTime => Tune.Hero.DodgeTime;

    private float[] _dodgeCd = new float[1];
    private float _dodgeT, _chargeCd, _chargeGlowT;
    private Vector2 _dodgeDir;

    public bool IsDodging => _dodgeT > 0;
    /// <summary>Test harness: every ability ready again.</summary>
    public void ResetAbilityCooldowns()
    {
        _dashCd = _chargeCd = _hexCd = _healCd = _boltCd = 0;
        for (int k = 0; k < _dodgeCd.Length; k++) _dodgeCd[k] = 0;
    }

    /// <summary>Test harness: the timers that gate the buttons.</summary>
    public string DebugState => $"freeze {_freeze:0.00} swingCd {_swingCd:0.00} swingT {_swingT:0.00} dodgeT {_dodgeT:0.00} atkBuf {_attackBuf:0.00} ablBuf {_abilityBuf:0.00} chargeCd {_chargeCd:0.0} charged {Charged} dashT {_dashT:0.00} dashCd {_dashCd:0.00} shield {ShieldRaised}";
    public float[] DodgeCooldowns => _dodgeCd;
    /// <summary>A dodge charge's whole recharge, from the roll starting (for the HUD).</summary>
    public float DodgeCooldownTotal => DodgeTime + Tune.Hero.DodgeCooldown * Stats.DodgeCdMult;
    /// <summary>Swings still empowered by the Charged Strike (0 = none).</summary>
    public int Charged { get; private set; }
    /// <summary>0 = the Charged Strike is ready, 1 = just used.</summary>
    public float ChargeCooldownFrac => Math.Clamp(_chargeCd / Math.Max(0.01f, Stats.ChargeCooldown), 0, 1);

    private bool TryDodge(in PlayerInput inp)
    {
        if (_dodgeT > 0 || _airDashT > 0) return false;
        int idx = -1;
        for (int k = 0; k < _dodgeCd.Length; k++) if (_dodgeCd[k] <= 0) { idx = k; break; }
        if (idx < 0) return false;
        _dodgeCd[idx] = DodgeTime + Tune.Hero.DodgeCooldown * Stats.DodgeCdMult;
        Vector2 d;
        if (InWater) d = inp.Move.LengthSquared() > 0.04f ? inp.Move.Normalized() : new Vector2(Facing, 0);
        else d = new Vector2(Math.Abs(inp.Move.X) > 0.2f ? Math.Sign(inp.Move.X) : Facing, 0);
        _dodgeDir = d; _dodgeT = DodgeTime;
        if (Math.Abs(d.X) > 0.2f) Facing = Math.Sign(d.X);
        Anim.Face((int)Facing, instant: true);
        Anim.Once("dodge", 3, 6f / (DodgeTime * 24f));
        if (Stats.DodgeIFrames) _iframes = DodgeTime + 0.12f;
        G.Sfx.Play("dodge", GlobalPosition, -3);
        if (!InWater && IsOnFloor()) G.Fx.Dust(GlobalPosition + new Vector2(0, 12), 4, 1.4f);
        G.Fx.Directional(GlobalPosition, -d, 0.5f, new Color(0.7f, 0.9f, 1f, 0.6f), 6, 160, 1.5f, 0.2f, 0);
        return true;
    }

    private Vector2 DodgeMotion(Vector2 v, float dt)
    {
        v = _dodgeDir * DodgeSpeed;
        if (Engine.GetPhysicsFrames() % 3 == 0) Afterimage.Spawn(Anim, new Color(0.45f, 0.8f, 1f), 0.2f);
        if (_dodgeT - dt <= 0) v *= 0.45f;
        _dodgeT -= dt;
        return v;
    }

    /// <summary>
    /// Charged Strike: the blade takes on a burning edge and the next swing (two, with Twin
    /// Charge) hits harder and further, and saps what it strikes. Using it is instant: no
    /// animation lock, so it slots into a combo without breaking it.
    /// </summary>
    private bool TryCharge()
    {
        if (!IsSwordsman || _chargeCd > 0 || Charged > 0) return false;
        _chargeCd = Stats.ChargeCooldown;
        Charged = Stats.ChargeSwings;
        // a swing still coiling takes the charge at once
        if (_swingT >= 0 && !_released && !_swingCharged) ChargeCurrentSwing();
        G.Sfx.Play("levelup", GlobalPosition, -8, 0.05f, 0.7f);
        G.Sfx.Play("swing_heavy", GlobalPosition, -10, 0.05f, 0.5f);
        G.Fx.Flash(GlobalPosition + new Vector2(0, -6), 18, new Color(1f, 0.55f, 0.25f));
        G.Fx.Ring(GlobalPosition + new Vector2(0, -4), 16, new Color(1f, 0.6f, 0.3f));
        for (int k = 0; k < 8; k++) G.Fx.Ember(GlobalPosition + new Vector2(Facing * G.Range(4, 16), G.Range(-14, 2)), new Color(1f, 0.55f, 0.2f));
        Anim.Flash(0.35f);
        G.Main.Rumble(0.3f, 0.1f, 0.12f);
        return true;
    }

    /// <summary>Spends one charge on the swing being started (false if none is left).</summary>
    private bool ConsumeCharge()
    {
        if (Charged <= 0) return false;
        Charged--;
        return true;
    }

    /// <summary>The charge caught a swing mid wind-up: it becomes a charged one.</summary>
    private void ChargeCurrentSwing()
    {
        if (!ConsumeCharge()) return;
        _swingCharged = true;
        _swingArc *= 1.15f;
        _swingReach *= Tune.Swordsman.ChargeReach;
        _swingDmg *= Tune.Swordsman.ChargeDamage;
    }

    private void TickSwordsman(float dt)
    {
        if (Charged <= 0) return;
        // embers stream off the charged blade
        _chargeGlowT -= dt;
        if (_chargeGlowT > 0) return;
        _chargeGlowT = 0.06f;
        var along = SwingAim != Vector2.Zero ? SwingAim : new Vector2(Facing, -0.2f).Normalized();
        G.Fx.Ember(GlobalPosition + new Vector2(0, -4) + along * G.Range(6, 20) + G.RandDir() * 2, new Color(1f, 0.5f, 0.2f));
    }
}
