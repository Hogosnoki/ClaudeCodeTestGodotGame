using System;
using Godot;

namespace DaggerCave;

/// <summary>
/// The Swordsman's kit: a quick dodge roll on a short cooldown (a swing can be started out of
/// it), the Charged Strike, which empowers the next swing without breaking a combo, and the
/// Heaving Swing: a slow, rooted, two-handed blow on your feet that hits twice as hard (and
/// spends a waiting Charged Strike, for more still).
/// </summary>
public partial class Player
{
    private static float DodgeSpeed => Tune.Hero.DodgeSpeed;
    private static float DodgeTime => Tune.Hero.DodgeTime;

    private float[] _dodgeCd = new float[1];
    private float _dodgeT, _chargeGlowT, _heaveCd, _heaveRootT;
    private Vector2 _dodgeDir;
    private bool _heave, _heaveWanted;

    public bool IsDodging => _dodgeT > 0;
    /// <summary>Planted for a heaving swing: no running, jumping or rolling until it's done.</summary>
    public bool Heaving => _heaveRootT > 0;
    /// <summary>True while the current swing is a heaving swing (for the 3D smear).</summary>
    public bool SwingHeave => _swingT >= 0 && _heave;
    /// <summary>0 = the heaving swing is ready, 1 = just used.</summary>
    public float HeaveCooldownFrac => Math.Clamp(_heaveCd / Math.Max(0.01f, Stats.HeaveCooldown), 0, 1);

    /// <summary>Test harness: every ability ready again.</summary>
    public void ResetAbilityCooldowns()
    {
        _hexCd = _healCd = _drainCd = _ruptureCd = _bashCd = _heaveCd = 0;
        for (int k = 0; k < _dodgeCd.Length; k++) _dodgeCd[k] = 0;
        for (int k = 0; k < _abilityCd.Length; k++) _abilityCd[k] = 0;
    }

    /// <summary>Test harness: the timers that gate the buttons.</summary>
    public string DebugState => $"freeze {_freeze:0.00} swingCd {_swingCd:0.00} swingT {_swingT:0.00} dodgeT {_dodgeT:0.00} atkBuf {_attackBuf:0.00} ablBuf {_abilityBuf:0.00} abl2Buf {_ability2Buf:0.00} ability {AbilityCooldownFrac:0.00} charged {Charged} dashT {_dashT:0.00} bashT {_bashT:0.00} heave {_heaveRootT:0.00} shield {ShieldRaised}";
    public float[] DodgeCooldowns => _dodgeCd;
    /// <summary>A dodge charge's whole recharge, from the roll starting (for the HUD).</summary>
    public float DodgeCooldownTotal => DodgeTime + Tune.Hero.DodgeCooldown * Stats.DodgeCdMult;
    /// <summary>Swings still empowered by the Charged Strike (0 = none).</summary>
    public int Charged { get; private set; }
    /// <summary>0 = the Charged Strike is ready, 1 = just used.</summary>
    public float ChargeCooldownFrac => AbilityCooldownFrac;

    private bool TryDodge(in PlayerInput inp)
    {
        if (_dodgeT > 0 || _airDashT > 0 || Heaving) return false;
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
        if (!IsSwordsman || !AbilityChargeReady || Charged > 0) return false;
        SpendAbilityCharge();
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

    // ---------------------------------------------------------------- heaving swing

    private bool TryHeave(Vector2 aim)
    {
        if (!IsSwordsman || _heaveCd > 0 || Heaving) return false;
        // it needs your feet on the ground (a press just before landing still takes)
        if (InWater || !IsOnFloor()) { _heaveWanted = true; return false; }
        _heaveWanted = false;
        _heaveCd = Stats.HeaveCooldown;
        _dodgeT = 0;
        float dir = Math.Abs(aim.X) > 0.2f ? Math.Sign(aim.X) : Facing;
        Facing = dir;
        StartHeave();
        return true;
    }

    private void StartHeave()
    {
        _heave = true;
        _finisher = false;
        _comboStep = 0;
        _chainLive = false;
        _swingSinceLast = 0;
        // one great arc over the top, from behind your head down to the floor in front
        _swingDir = new Vector2(Facing, -0.25f).Normalized();
        bool charged = _swingCharged = ConsumeCharge();
        float speed = Math.Max(1f, Stats.AttackSpeed);
        _swingArc = Mathf.DegToRad(Tune.Swordsman.HeaveArcDegrees);
        _swingReach = BaseReach * Stats.DaggerReach * Tune.Swordsman.HeaveReach * (charged ? Tune.Swordsman.ChargeReach : 1f);
        _swingDmg = BaseDamage * Stats.DamageMult * Tune.Swordsman.HeaveDamage * (charged ? Tune.Swordsman.ChargeDamage : 1f);
        _swingT = 0;
        _released = false;
        _windup = Tune.Swordsman.HeaveWindup / speed;
        _active = SwingActive * 1.6f / speed;
        _heaveRootT = _windup + _active + Tune.Swordsman.HeaveRecover;
        _swingHits.Clear();
        _brokeThisSwing.Clear();
        _swingHitSomething = false;
        Velocity = new Vector2(0, Velocity.Y);
        Anim.Face((int)Facing, instant: true);
        // the clip's seven wind-up frames last exactly the wind-up
        Anim.Once("heave", 4, 7f / 24f / _windup);
        Anim.Punch(new Vector2(0.9f, 1.12f));
        G.Sfx.Play("gasp", GlobalPosition, -10, 0.05f, 0.55f);
        G.Fx.Dust(GlobalPosition + new Vector2(0, 12), 4, 1.2f);
        if (charged) G.Fx.Ring(GlobalPosition + new Vector2(0, -6), 16, new Color(1f, 0.6f, 0.3f));
    }

    /// <summary>The heaving swing lands on the floor in front: a crack of dust and a jolt.</summary>
    private void HeaveImpact()
    {
        var at = GlobalPosition + new Vector2(Facing * _swingReach * 0.75f, 12);
        if (!G.Cave.IsSolid(at + new Vector2(0, 6))) return;
        G.Fx.Dust(at, 10, 2.2f);
        G.Fx.Debris(at, new Color(0.45f, 0.4f, 0.36f), 6, 200);
        G.Fx.Shockwave(at, 34, new Color(1f, 0.95f, 0.85f, 0.6f), 0.3f);
        G.Fx.AddShake(3.5f);
        G.Sfx.Play("slam", at, -6, 0.05f, 1.3f);
        G.Main.Rumble(0.4f, 0.6f, 0.15f);
    }

    private void TickSwordsman(float dt)
    {
        if (_heaveRootT > 0) _heaveRootT -= dt;
        if (_heaveWanted && _ability2Buf <= 0)
        {
            // the press ran out while you were in the air
            _heaveWanted = false;
            SayNo("FEET ON THE GROUND");
        }
        if (Charged <= 0) return;
        // embers stream off the charged blade
        _chargeGlowT -= dt;
        if (_chargeGlowT > 0) return;
        _chargeGlowT = 0.06f;
        var along = SwingAim != Vector2.Zero ? SwingAim : new Vector2(Facing, -0.2f).Normalized();
        G.Fx.Ember(GlobalPosition + new Vector2(0, -4) + along * G.Range(6, 20) + G.RandDir() * 2, new Color(1f, 0.5f, 0.2f));
    }
}
