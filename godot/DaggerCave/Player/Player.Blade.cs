using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

/// <summary>
/// The blade, shared by the Swordsman (medium sword), the Warden (shortsword) and the Rogue (its
/// daggers): a swing aimed anywhere around you in three beats (wind-up, sweep, follow-through),
/// combos that chain when strikes land, a finisher, and the hit-stop that freezes both fighters on
/// the impact. The Rogue's jabs are quick and strike one creature each, with no combo, but can be
/// critical.
/// </summary>
public partial class Player
{
    private static float ComboWindow => Tune.Hero.ComboWindow;
    // (the Rogue jabs half as fast with one of its daggers thrown)
    private float SwingCooldownBase => IsWarden ? Tune.Warden.SwingCooldown : IsRogue ? Tune.Rogue.SwingCooldown * (DaggersInHand < 2 ? Tune.Rogue.OneDaggerSlow : 1f) : Tune.Swordsman.SwingCooldown;
    private float SwingActive => IsWarden ? Tune.Warden.SwingTime : IsRogue ? Tune.Rogue.SwingTime : Tune.Swordsman.SwingTime;
    private float SwingWindup => IsWarden ? Tune.Warden.SwingWindup : IsRogue ? Tune.Rogue.SwingWindup : Tune.Swordsman.SwingWindup;
    private float BaseReach => IsWarden ? Tune.Warden.Reach : IsRogue ? Tune.Rogue.Reach : Tune.Swordsman.Reach;
    private float BaseDamage => IsWarden ? Tune.Warden.Damage : IsRogue ? Tune.Rogue.Damage : Tune.Swordsman.Damage;
    private float BaseKnock => IsWarden ? Tune.Warden.Knockback : IsRogue ? Tune.Rogue.Knockback : Tune.Swordsman.Knockback;
    private float LungeSpeed => IsWarden ? Tune.Warden.Lunge : IsRogue ? Tune.Rogue.Lunge : Tune.Swordsman.Lunge;

    // this swing's beats (scaled by attack speed): wind-up, then the sweep (the hitbox), then follow-through
    private float _windup, _active;
    private bool _released;
    private float _swingCd, _swingT = -1, _swingSinceLast = 9, _lungeT, _lungeDir, _waveCd;
    private Vector2 _swingDir;
    private int _comboStep;
    private bool _swingHitSomething, _chainLive, _finisher, _swingCharged;
    private readonly HashSet<Enemy> _swingHits = new();
    private readonly HashSet<IBreakable> _brokeThisSwing = new();
    private float _swingArc, _swingReach, _swingDmg;

    public bool IsSwinging => _swingT >= 0;
    /// <summary>Attacks started this run (swings, drains): for the tests.</summary>
    public int AttacksStarted { get; private set; }
    /// <summary>The current swing's aim (zero when not swinging); the 3D body sweeps the blade through it.</summary>
    public Vector2 SwingAim => _swingT >= 0 ? _swingDir : Vector2.Zero;
    /// <summary>True while the current swing carries a charged strike.</summary>
    public bool SwingCharged => _swingT >= 0 && _swingCharged;
    public float SwingCooldownFrac => Math.Clamp(_swingCd / (SwingCooldownBase / Stats.AttackSpeed), 0, 1);

    /// <summary>Starts a swing if the blade is ready (a dodge in progress turns into it, when
    /// the attack is pressed: holding it down doesn't cut every roll short).</summary>
    private bool TrySwing(Vector2 aim, bool held = false)
    {
        if (_swingCd > 0 || (held && _dodgeT > 0)) return false;
        // swinging out of a roll: the roll ends but its speed carries into the strike
        if (_dodgeT > 0) _dodgeT = 0;
        StartSwing(aim);
        return true;
    }

    private void StartSwing(Vector2 aim, bool counter = false)
    {
        aim = aim.Normalized();
        _heave = false;
        _counter = counter;
        AttacksStarted++;
        // Combo: a strike that lands refunds the swing cooldown, up to ComboResets times in a row.
        // A swing made without a refund (or after a pause) starts a new chain.
        _comboStep = _chainLive && _swingSinceLast < SwingCooldownBase / Stats.AttackSpeed + ComboWindow ? _comboStep + 1 : 0;
        _chainLive = false;
        _swingSinceLast = 0;
        _swingDir = aim;
        if (Math.Abs(aim.X) > 0.15f) Facing = Math.Sign(aim.X);
        // Finisher: the last strike of a full chain of three or more hits much harder
        bool finisher = _finisher = Stats.ThirdCombo && _comboStep >= 2 && _comboStep == Stats.ComboResets;
        // Charged Strike: this swing carries the charge (never breaking the combo; with
        // Relentless Charge, the whole combo carries it)
        bool charged = _swingCharged = ChargeForSwing();
        _swingArc = Mathf.DegToRad(finisher ? Tune.Hero.FinisherArcDegrees : IsRogue ? 90f : Tune.Hero.SwingArcDegrees) * (charged ? 1.15f : 1f);
        _swingReach = BaseReach * Stats.DaggerReach * (finisher ? Tune.Hero.FinisherReachMult : 1f) * (charged ? ChargeReachMult : 1f);
        _swingDmg = BaseDamage * Stats.DamageMult * Stats.PrimaryDamageMult * (finisher ? Tune.Hero.FinisherDamageMult : 1f) * (charged ? ChargeDmgMult : 1f);
        _swingT = 0;
        _released = false;
        float speed = Math.Max(1f, Stats.AttackSpeed);
        _windup = SwingWindup / speed * (finisher ? 1.5f : 1f);
        _active = SwingActive / speed;
        _swingCd = SwingCooldownBase / Stats.AttackSpeed;
        _swingHits.Clear();
        _brokeThisSwing.Clear();
        _swingHitSomething = false;
        // coil for the wind-up
        Anim.Punch(new Vector2(1.08f, 0.9f));
        // body animation: combo letter + the nearest of five aim directions in front of the player
        var local = new Vector2(aim.X * Facing, aim.Y);
        float la = MathF.Atan2(local.Y, Math.Max(local.X, -0.2f));
        string dir = la < -1.18f ? "up" : la < -0.39f ? "upfwd" : la < 0.39f ? "fwd" : la < 1.18f ? "downfwd" : "down";
        // (the Rogue's jabs alternate hands and strokes, one after another)
        string letter = finisher ? "c" : (IsRogue ? AttacksStarted : _comboStep) % 2 == 0 ? "a" : "b";
        Anim.Face((int)Facing, instant: true);
        // clip frames: wind-up (2, or 3 for the finisher), woosh (2), follow-through (the rest).
        // Play it so the wind-up frames last exactly the wind-up time; the woosh then lands with the hitbox.
        float windFrames = finisher ? 3 : 2;
        Anim.Once($"slash_{letter}_{dir}", 3, windFrames / 24f / _windup);
        NetSync.HeroSwing(this, _swingDir, _swingArc, _swingReach, _windup, _active, _comboStep, finisher, charged, false);
    }

    /// <summary>The moment the blade comes around: sound, lunge, crescent wave, stretch.</summary>
    private void ReleaseSwing()
    {
        _released = true;
        var aim = _swingDir;
        G.Sfx.Play(_finisher || _swingCharged || _heave ? "swing_heavy" : "swing", GlobalPosition, _heave ? 1 : -2, 0.12f, _heave ? 0.7f : 1f + _comboStep * 0.08f);
        // the sword carries you forward a little (horizontal strikes, on your feet; never a heave)
        if (LungeSpeed > 0 && !InWater && !_heave && Math.Abs(aim.X) > 0.35f) { _lungeT = 0.12f; _lungeDir = Math.Sign(aim.X); }
        if (_heave) Afterimage.Spawn(Anim, _swingCharged ? new Color(1f, 0.55f, 0.3f) : new Color(0.8f, 0.9f, 1f), 0.2f);
        Anim.Punch(new Vector2(1.22f, 0.86f));
        if (Stats.CrescentWave && _waveCd <= 0)
        {
            _waveCd = Tune.Swordsman.WaveCooldown;
            var wave = new SwordWave
            {
                Position = GlobalPosition + new Vector2(0, -3) + aim * (_swingReach * 0.6f),
                Dir = aim,
                Damage = _swingDmg * Tune.Swordsman.WaveDamage,
                Range = Tune.Swordsman.WaveRange,
            };
            G.Spawn(wave);
            NetSync.HeroVisual(wave);
        }
        // Storm Edge: a charged swing looses a full-strength wave of its own
        if (_swingCharged && Stats.ChargeWave)
        {
            var wave = new SwordWave
            {
                Position = GlobalPosition + new Vector2(0, -3) + aim * (_swingReach * 0.6f),
                Dir = aim,
                Damage = _swingDmg,
                Range = Tune.Swordsman.WaveRange * 1.3f,
            };
            G.Spawn(wave);
            NetSync.HeroVisual(wave);
        }
    }

    /// <summary>Time into the sweep (negative during the wind-up).</summary>
    private float SweepT => _swingT - _windup;

    private void UpdateSwing(float dt)
    {
        _swingT += dt;
        if (!_released && SweepT >= 0) ReleaseSwing();
        if (SweepT >= 0 && SweepT <= _active)
        {
            var origin = GlobalPosition + new Vector2(0, -3);
            // (the Rogue's jab takes the nearest creature only)
            var foes = IsRogue ? G.Enemies.OrderBy(e => e.GlobalPosition.DistanceSquaredTo(origin)).ToArray() : G.Enemies.ToArray();
            foreach (var e in foes)
            {
                if (IsRogue && _swingHitSomething) break;
                if (e.Dead || _swingHits.Contains(e)) continue;
                var to = e.GlobalPosition - origin;
                float dist = to.Length();
                if (dist - e.HitRadius > _swingReach + 4) continue;
                float tol = dist > 1 ? MathF.Atan2(e.HitRadius, dist) : MathF.PI;
                if (dist > 12 && Math.Abs(_swingDir.AngleTo(to)) > _swingArc * 0.5f + tol) continue;
                if (!G.Cave.LineClear(origin, e.GlobalPosition - to.Normalized() * Math.Min(dist, e.HitRadius))) continue;
                _swingHits.Add(e);
                OnSwingHit(e, to);
            }
            foreach (var b in Breakables.All.ToArray())
            {
                var to = b.HitCenter - origin;
                float reach = _swingReach + b.HitSize * 0.6f;
                if (to.Length() > reach || _brokeThisSwing.Contains(b)) continue;
                if (to.Length() > 14 && Math.Abs(_swingDir.AngleTo(to)) > _swingArc * 0.5f + 0.4f) continue;
                _brokeThisSwing.Add(b);
                b.Strike(origin);
            }
            foreach (var pr in G.Main.EnemyProjectiles.ToArray())
            {
                var to = pr.GlobalPosition - origin;
                if (to.Length() > _swingReach + 8 || Math.Abs(_swingDir.AngleTo(to)) > _swingArc * 0.6f) continue;
                pr.Deflect();
            }
        }
        // a heave ends in the floor in front of you
        if (_heave && !_heaveLanded && SweepT >= _active * 0.8f) { _heaveLanded = true; HeaveImpact(); }
        if (SweepT < 0) _heaveLanded = false;
        // follow-through: the rest of the clip (3-4 frames at the clip's speed), a held beat
        float follow = _heave ? Tune.Swordsman.HeaveRecover : _windup * 1.6f;
        if (SweepT > _active + follow) _swingT = -1;
    }

    private bool _heaveLanded;

    private void OnSwingHit(Enemy e, Vector2 to)
    {
        var dir = (_swingDir + to.Normalized()).Normalized();
        var kbTable = Tune.Hero.KnockbackByLevel;
        float kb = BaseKnock + kbTable[Math.Clamp(Stats.KnockbackLevel, 0, kbTable.Length - 1)];
        bool finisher = _finisher || _heave, charged = _swingCharged;
        if (finisher) kb += Tune.Hero.FinisherExtraKnockback;
        if (charged) kb += Tune.Hero.FinisherExtraKnockback * 0.6f;
        if (_heave) kb = Math.Max(kb, Tune.Swordsman.HeaveKnockback);
        var hitPos = e.GlobalPosition - to.Normalized() * e.HitRadius;
        float dmg = _swingDmg * G.Range(0.9f, 1.1f);
        if (Stats.Execute && e.Hp < e.MaxHp * Tune.Swordsman.ExecuteBelow) dmg *= 1f + Tune.Swordsman.ExecuteBonus;
        bool crit = false;
        if (IsRogue)
        {
            // (a jab is exact: no spread) a critical strike, a stab in the back, a strike from the shadows
            dmg = _swingDmg * RogueStrikeMult(e, GlobalPosition, out crit);
            Reveal(false);
        }
        float dealt = e.Hurt(dmg, dir * kb, hitPos);
        if (dealt > 0 && Stats.BleedShare > 0 && !e.Dead) e.Bleed(dealt * Stats.BleedShare, Tune.Swordsman.BleedSeconds);
        if (dealt <= 0)
        {
            G.Sfx.Play("clink", GlobalPosition, -6);
            G.Fx.Spark(hitPos, -dir, false, new Color(1f, 0.9f, 0.6f));
            return;
        }
        OnDealtDamage(dealt);
        if (crit) CritFx(e, hitPos);
        // a charged strike saps whatever it cuts: it hits back softer for a while
        if (charged && !e.Dead) e.Weaken(ChargeWeaken, Tune.Swordsman.WeakenSeconds);
        bool killed = e.Dead;
        bool heavy = finisher || charged;
        float stop = charged ? Tune.Feel.HitStopCharged : finisher ? Tune.Feel.HitStopFinisher : killed ? Tune.Feel.HitStopKill : Tune.Feel.HitStopNormal;
        // (a jab barely pauses: the Rogue's rhythm is speed)
        if (IsRogue) stop = crit ? Tune.Rogue.HitStop * 2.5f : Tune.Rogue.HitStop;
        if (!killed) e.Freeze(stop);
        // impact: sparks, freeze-frame, a camera nudge in the direction of the blow, rumble
        var sparkCol = charged ? new Color(1f, 0.55f, 0.3f) : finisher ? new Color(1f, 0.85f, 0.4f) : Colors.White;
        G.Fx.Spark(hitPos, dir, heavy || killed, sparkCol);
        G.Main.Kick(dir * (heavy ? Tune.Feel.KickFinisher : killed ? Tune.Feel.KickKill : Tune.Feel.KickNormal));
        G.Main.Rumble(heavy ? 0.6f : 0.35f, heavy ? 0.7f : 0.15f, heavy ? 0.16f : 0.08f);
        if (heavy || killed) G.Fx.AddShake(heavy ? Tune.Feel.ShakeFinisher : Tune.Feel.ShakeKill);
        if (heavy) Afterimage.Spawn(Anim, sparkCol, 0.18f);
        if (charged) G.Fx.Burst(hitPos, new Color(1f, 0.45f, 0.2f), 10, 200, 2.2f, 0.45f);
        if (!_swingHitSomething)
        {
            _swingHitSomething = true;
            // the combo stays live: the next swing may follow at once (a press during the
            // hit-stop is kept and fires the moment it ends). No combo from behind a raised shield,
            // nor out of a heave, nor out of a counter (unless Flowing Counter).
            if (!IsRogue && _comboStep < Stats.ComboResets && !ShieldRaised && !_heave && (!_counter || Stats.CounterCombo)) { _swingCd = 0f; _chainLive = true; }
            // mutual bounce: you rebound slightly from what you hit (sideways only)
            // (not the Rogue: its jabs come too fast to bounce off each one)
            if (Math.Abs(to.X) > 2 && !IsRogue) Velocity = new Vector2(Velocity.X - Math.Sign(to.X) * Tune.Combat.StrikeRecoil, Velocity.Y);
            Freeze(stop);
            // Pogo: downward aerial strikes bounce the player up.
            if (Stats.Pogo && !IsOnFloor() && !InWater && _swingDir.Y > 0.55f)
            {
                Velocity = new Vector2(Velocity.X, -BaseJumpV * Tune.Hero.PogoBounceMult * MathF.Sqrt(Stats.JumpMult));
                _airJumps = Stats.DoubleJump ? 1 : 0; _airDashes = Stats.AirDash ? 1 : 0; _jumpCutDone = true;
                G.Fx.Ring(e.GlobalPosition, 14, new Color(1f, 1f, 0.7f, 0.9f));
            }
        }
    }

    /// <summary>The blade smear of the current swing (for the 3D stage): angles in 2D radians, sizes in pixels.</summary>
    public struct Smear
    {
        public float Head, Tail, Outer, Blade, Fade;
        public bool Finisher, Charged;
        public Color Tint;
        public Vector2 Origin;
    }

    public bool GetSmear(out Smear sm)
    {
        sm = default;
        if (Dead || _swingT < 0 || SweepT < 0 || SweepT > _active + 0.15f) return false;
        float prog = Math.Clamp(SweepT / _active, 0, 1);
        prog = 1 - (1 - prog) * (1 - prog) * (1 - prog);
        float fade = 1 - Math.Clamp((SweepT - _active) / 0.15f, 0, 1);
        // (a heave always comes over the top: from behind your head down to the floor in front)
        float dirSign = _heave ? Facing : _comboStep % 2 == 0 ? 1 : -1;
        float a0 = _swingDir.Angle() - dirSign * _swingArc * 0.5f;
        sm.Head = a0 + dirSign * _swingArc * prog;
        sm.Tail = a0 + dirSign * _swingArc * Math.Max(0, prog - 0.85f + (1 - fade) * 0.85f);
        if (Math.Abs(sm.Head - sm.Tail) < 0.02f) return false;
        sm.Finisher = _finisher || _heave;
        sm.Charged = _swingCharged;
        sm.Outer = _swingReach + 3;
        sm.Blade = _swingReach * (IsWarden ? 0.62f : IsRogue ? 0.5f : 0.8f) * (_finisher || _heave ? 1.1f : 1f);
        sm.Fade = fade;
        sm.Tint = _swingCharged ? new Color(1f, 0.5f, 0.28f) : _finisher ? new Color(1f, 0.82f, 0.35f) : new Color(0.8f, 0.95f, 1f);
        sm.Origin = GlobalPosition + new Vector2(0, -3);
        return true;
    }

    /// <summary>The 2D debug view's smear: a crescent brightest at its leading edge.</summary>
    private void DrawSmear()
    {
        if (!GetSmear(out var sm)) return;
        var o = new Vector2(0, -3);
        const int n = 18;
        var outerBand = new Vector2[n * 2];
        var innerBand = new Vector2[n * 2];
        var outerCols = new Color[n * 2];
        var innerCols = new Color[n * 2];
        var edge = new Vector2[n];
        var edgeCols = new Color[n];
        for (int k = 0; k < n; k++)
        {
            float t = k / (float)(n - 1);            // 0 = tail, 1 = head
            float ang = Mathf.Lerp(sm.Tail, sm.Head, t);
            float w = sm.Blade * (0.35f + 0.65f * MathF.Sin(t * MathF.PI * 0.5f)); // the tail thins out
            var d = Vector2.Right.Rotated(ang);
            float alpha = t * t * sm.Fade;
            var pMid = o + d * (sm.Outer - w * 0.4f);
            outerBand[k] = o + d * sm.Outer; outerBand[2 * n - 1 - k] = pMid;
            innerBand[k] = pMid; innerBand[2 * n - 1 - k] = o + d * (sm.Outer - w);
            outerCols[k] = new Color(sm.Tint, 0.8f * alpha);
            outerCols[2 * n - 1 - k] = innerCols[k] = new Color(sm.Tint, 0.32f * alpha);
            innerCols[2 * n - 1 - k] = new Color(sm.Tint, 0f);
            edge[k] = o + d * (sm.Outer + 0.5f);
            edgeCols[k] = new Color(1, 1, 1, alpha);
        }
        DrawPolygon(innerBand, innerCols);
        DrawPolygon(outerBand, outerCols);
        DrawPolylineColors(edge, edgeCols, sm.Finisher ? 2.5f : 1.8f);
    }
}
