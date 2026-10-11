using System;
using Godot;

namespace DaggerCave;

/// <summary>
/// The Automaton's kit. A copper-and-brass engine of a man: slow on its feet, short in the jump (a puff of steam from its
/// joints is its second jump), heavy and hard to shove, with a lot of health under some armour. It doesn't breathe, so
/// the water, the poisoned air and the gas are nothing to it, and water, steam and acid do it no harm; in water it sinks
/// like a dropped anvil, and swims only by kicking up with each press of jump.
///
/// It can't truly be healed. Healing it receives becomes fuel instead (30% of it), and Self-Repair (the ability
/// button) spends that fuel to mend it, one for one, while it slows to tinker. The attack button swings a great cleaver
/// in a three-stroke combo whose last stroke is a slam: a shock through the floor that strikes everything near and whose
/// sparks set gas alight. The second ability lets out its steam in a cone (scalding and shoving what's in it, blowing
/// gas away, putting out its own burning and kicking it the other way: aimed down, it's a lift). The dodge button is
/// Brace: planted, blows from in front lose most of their force and don't move it, and the first melee blow is answered
/// with a counter-chop. Its support is Pressurize: a long build-up, glowing and shaking harder and harder, and then (still
/// shaking and glowing) it strikes faster and harder, moves faster and jumps higher, burning fuel until the fuel is gone or it is
/// pressed again.
/// </summary>
public partial class Player
{
    private bool IsAutomaton => Stats.Hero == HeroKind.Automaton;

    /// <summary>Fuel for Self-Repair (the Automaton's), in health it can mend.</summary>
    public float Fuel = Tune.AutomatonHero.FuelStart;
    private float _fuelShown, _repairShown, _repairT, _repairFx;
    private float _braceT, _braceCd, _steamCd, _strokeCd, _puffT;
    private bool _braceAnswered;

    /// <summary>Mending itself (Self-Repair under way): it moves slowly while it does.</summary>
    public bool Repairing => _repairT > 0;
    /// <summary>Braced: planted, blows from in front softened and not moving it.</summary>
    public bool Bracing => _braceT > 0;
    public float FuelFrac => Math.Clamp(Fuel / Math.Max(1f, Stats.FuelMax), 0f, 1f);
    public float BraceCooldownFrac => Math.Clamp(_braceCd / Math.Max(0.01f, Tune.AutomatonHero.BraceCooldown + Tune.AutomatonHero.BraceTime + Stats.BraceBonus), 0f, 1f);
    public float SteamCooldownFrac => Math.Clamp(_steamCd / Math.Max(0.01f, SteamRecharge), 0f, 1f);
    private float SteamRecharge => Tune.AutomatonHero.SteamCooldown * Stats.SteamCdMult * Stats.AbilityCdMult;

    /// <summary>Test aid: the fuel set outright, and every Automaton cooldown ready.</summary>
    public void TestSetFuel(float e) { Fuel = Math.Clamp(e, 0f, Stats.FuelMax); _braceCd = _steamCd = 0; }

    private static readonly Color FuelColor = new(1f, 0.74f, 0.3f);
    private static readonly Color Copper = new(0.85f, 0.5f, 0.3f);

    /// <summary>Healing it receives becomes fuel: a share of it (none at all with Scrap Reclaimer).</summary>
    private void HealAsFuel(float amount)
    {
        if (Stats.ScrapReclaimer) return;
        GainFuel(amount * Stats.FuelFromHeal);
    }

    /// <summary>Fuel gained (floating the change shown, as healing does).</summary>
    public void GainFuel(float amount)
    {
        if (Dead || amount <= 0) return;
        float before = Fuel;
        Fuel = Math.Min(Stats.FuelMax, Fuel + amount);
        _fuelShown += Fuel - before;
        if (_fuelShown >= 1f)
        {
            int n = (int)_fuelShown;
            _fuelShown -= n;
            G.Fx?.Text(GlobalPosition + new Vector2(8, -26), "+" + n + " fuel", FuelColor, 11, 0.8f);
        }
    }

    private void TickAutomaton(float dt)
    {
        _braceT -= dt; _braceCd -= dt; _steamCd -= dt; _strokeCd -= dt;
        TickOverPressure(dt);
        if (_repairT > 0)
        {
            _repairT -= dt;
            float mend = Math.Min(Tune.AutomatonHero.RepairRate * Stats.RepairRateMult * dt, Math.Min(Fuel, Stats.MaxHp - Hp));
            if (mend > 0)
            {
                Fuel -= mend;
                Hp += mend;
                _repairShown += mend;
                if (_repairShown >= 1f)
                {
                    int n = (int)_repairShown;
                    _repairShown -= n;
                    G.Fx.Text(GlobalPosition + new Vector2(0, -22), "+" + n, HealColorLight, 12, 0.6f);
                }
            }
            // a welder's sparks at the joints, and the clank of a spanner
            if ((_repairFx -= dt) <= 0)
            {
                _repairFx = 0.09f;
                var at = GlobalPosition + new Vector2(G.Range(-7, 7), G.Range(-14, 6));
                G.Fx.Spark(at, G.RandDir(), false, new Color(0.75f, 0.9f, 1f));
                if (G.Chance(0.25f)) G.Sfx.Play("clink", at, -14, 0.1f, 1.6f);
            }
            if (Fuel <= 0.01f || Hp >= Stats.MaxHp - 0.01f || _repairT <= 0) StopRepair();
        }
        // steam wisps from the stack at its back, now and then (more when it's working hard)
        if ((_puffT -= dt) <= 0)
        {
            _puffT = Repairing || Bracing ? 0.12f : G.Range(0.6f, 1.4f);
            if (!HeadUnder) G.Fx.Smoke(GlobalPosition + new Vector2(-Facing * 5, -16), 1, new Color(0.92f, 0.92f, 0.94f, 0.35f), 6);
        }
        if (Bracing && G.Chance(dt * 8f))
            G.Fx.Directional(GlobalPosition + new Vector2(Facing * 10, -4), new Vector2(Facing, 0), 0.6f, new Color(Copper, 0.5f), 1, 30, 1.6f, 0.2f, 0);
    }

    // ---------------------------------------------------------------- self-repair

    /// <summary>Self-Repair: spends fuel to mend, one for one, for a few seconds (a press stops it).</summary>
    private bool TryRepair()
    {
        if (!IsAutomaton || Pressurising) return false;
        // (a second press stops it: not the same press held, or bounced, in its first moments)
        if (Repairing) { if (_repairT < Tune.AutomatonHero.RepairSeconds - 0.35f) StopRepair(); return true; }
        if (!AbilityChargeReady) return false;
        if (Fuel < 1f) { SayNo("NO ENERGY"); return true; }
        if (Hp >= Stats.MaxHp - 0.5f) { SayNo("NOTHING TO MEND"); return true; }
        SpendAbilityCharge();
        _repairT = Tune.AutomatonHero.RepairSeconds;
        _repairShown = 0;
        G.Fx.Text(GlobalPosition + new Vector2(0, -30), "SELF-REPAIR", FuelColor, 10, 0.8f);
        G.Fx.Ring(GlobalPosition + new Vector2(0, -4), 16, new Color(FuelColor, 0.8f), 0.3f);
        G.Sfx.Play("clink", GlobalPosition, -6, 0.05f, 0.7f);
        return true;
    }

    private void StopRepair()
    {
        if (_repairT <= 0) return;
        _repairT = 0;
        G.Sfx.Play("clink", GlobalPosition, -10, 0.05f, 1.2f);
    }

    // ---------------------------------------------------------------- steam release

    /// <summary>Steam Release: a cone of steam that scalds and shoves, blows gas away, puts out its burning, and kicks it back.</summary>
    private bool TrySteam(Vector2 aim)
    {
        if (!IsAutomaton || _steamCd > 0 || Pressurising) return false;
        _steamCd = SteamRecharge;
        var dir = aim.LengthSquared() > 0.01f ? aim.Normalized() : new Vector2(Facing, 0);
        if (Math.Abs(dir.X) > 0.2f) Facing = Math.Sign(dir.X);
        var origin = GlobalPosition + new Vector2(0, -6);
        float range = Tune.AutomatonHero.SteamRange * (0.85f + 0.15f * Stats.SteamMult);
        float half = Mathf.DegToRad(Tune.AutomatonHero.SteamCone) * 0.5f;
        float dmg = Tune.AutomatonHero.SteamDamage * Stats.DamageMult * Stats.SteamMult;
        float push = Tune.AutomatonHero.SteamPush * Stats.SteamMult;
        float total = 0;
        foreach (var e in G.Enemies.ToArray())
        {
            if (e.Dead || !e.CanBeHit) continue;
            var to = e.GlobalPosition - origin;
            float d = to.Length();
            if (d - e.HitRadius > range) continue;
            if (d > 12 && Math.Abs(dir.AngleTo(to)) > half + MathF.Atan2(e.HitRadius, Math.Max(d, 1f))) continue;
            if (!G.Cave.LineClear(origin, e.GlobalPosition)) continue;
            var away = d > 1 ? to / d : dir;
            float dealt = e.Hurt(dmg, Stats.ScaldingSteam ? Vector2.Zero : (away + dir).Normalized() * push, e.GlobalPosition - away * e.HitRadius);
            if (Stats.ScaldingSteam && !e.Dead) e.Ignite(Tune.AutomatonHero.ScaldDps * Stats.DamageMult * Stats.SteamMult, Tune.AutomatonHero.ScaldSeconds);
            total += Math.Max(0f, dealt);
        }
        if (total > 0) OnDealtDamage(total);
        // the gas is blown off along the steam (lit clouds too: they go up where they land)
        foreach (var c in GasCloud.All.ToArray())
        {
            if (!IsInstanceValid(c)) continue;
            var to = c.GlobalPosition - origin;
            if (to.Length() - c.Size > range || (to.Length() > 16 && Math.Abs(dir.AngleTo(to)) > half + 0.3f)) continue;
            c.GlobalPosition += dir * range * 0.9f;
        }
        // the hiss puts out its own fire
        if (Burning) { _burnLeft = 0; G.Fx.Text(GlobalPosition + new Vector2(0, -30), "QUENCHED", new Color(0.85f, 0.92f, 1f), 9, 0.7f); }
        // and the release kicks it the other way (aimed at the floor, a lift; in the water, a thrust)
        float kick = Tune.AutomatonHero.SteamRecoil * (0.7f + 0.3f * Stats.SteamMult);
        var v = Velocity - dir * kick;
        if (dir.Y > 0.5f) { v.Y = Math.Min(v.Y, -kick); _coyote = 0; _jumpCutDone = true; }
        Velocity = v;
        // the cloud of it
        for (int k = 0; k < 14; k++)
        {
            var along = dir.Rotated(G.Range(-half, half)) * G.Range(10f, range);
            G.Fx.Smoke(origin + along, 1, new Color(0.94f, 0.95f, 0.97f, 0.55f), 9);
        }
        G.Fx.Directional(origin, dir, half, new Color(0.95f, 0.97f, 1f, 0.7f), 18, 320, 2.4f, 0.35f, 0);
        G.Sfx.Play("dodge", origin, -2, 0.05f, 0.45f);
        G.Sfx.Play("splash_out", origin, -8, 0.1f, 1.6f);
        Anim.Punch(new Vector2(0.9f, 1.1f));
        G.Main.Rumble(0.3f, 0.4f, 0.14f);
        return true;
    }

    // ---------------------------------------------------------------- brace

    /// <summary>Brace: planted, the blows in front softened and unable to move it; the first melee blow answered.</summary>
    private bool TryBrace(in PlayerInput inp)
    {
        if (!IsAutomaton || _braceCd > 0 || Bracing || Possessed) return false;
        if (Math.Abs(inp.Move.X) > 0.2f) Facing = Math.Sign(inp.Move.X);
        _braceT = Tune.AutomatonHero.BraceTime + Stats.BraceBonus;
        _braceCd = _braceT + Tune.AutomatonHero.BraceCooldown;
        _braceAnswered = false;
        StopRepair();
        Anim.Face((int)Facing, instant: true);
        Anim.Punch(new Vector2(1.1f, 0.88f));
        G.Fx.Shockwave(GlobalPosition + new Vector2(Facing * 8, -2), 18, new Color(Copper, 0.6f), 0.2f);
        G.Sfx.Play("clink", GlobalPosition, -4, 0.05f, 0.55f);
        if (IsOnFloor()) G.Fx.Dust(GlobalPosition + new Vector2(0, 12), 4, 1.2f);
        return true;
    }

    /// <summary>A blow meets the brace: how much of it is left, and whether its shove is stopped. A melee blow is answered once.</summary>
    private float BraceBlow(float dmg, Vector2 from, Enemy source, bool melee, ref float knock)
    {
        if (!Bracing) return dmg;
        float dx = from.X - GlobalPosition.X;
        bool front = Stats.FullPlate || Math.Abs(dx) <= 2f || Math.Sign(dx) == Math.Sign(Facing);
        if (front)
        {
            dmg *= 1f - Tune.AutomatonHero.BraceSoak;
            knock = 0f;
            G.Fx.Spark(GlobalPosition + new Vector2(Math.Sign(dx == 0 ? Facing : dx) * 9, -4), new Vector2(Math.Sign(dx == 0 ? Facing : dx), 0), true, Copper);
            G.Sfx.Play("clink", GlobalPosition, -2, 0.05f, 0.8f);
        }
        if (melee && !_braceAnswered && source != null && IsInstanceValid(source) && !source.Dead)
        {
            _braceAnswered = true;
            var to = source.GlobalPosition - (GlobalPosition + new Vector2(0, -3));
            StartSwing(to.LengthSquared() > 1 ? to.Normalized() : new Vector2(Facing, 0), counter: true);
            _swingDmg = Tune.AutomatonHero.PunchDamage * Stats.DamageMult * Stats.PunchMult;
            Counters++;
            G.Fx.Text(GlobalPosition + new Vector2(0, -28), "COUNTER", Copper.Lightened(0.3f), 11, 0.7f);
        }
        return dmg;
    }

    // ---------------------------------------------------------------- the slam

    /// <summary>The combo's last stroke lands: a shock through the floor round where it strikes, and sparks that light gas.</summary>
    private void Slam()
    {
        var at = GlobalPosition + new Vector2(0, -3) + _swingDir * (_swingReach * 0.7f);
        // (it lands on the floor under the blow, if there is one near)
        for (int k = 0; k < 6; k++) { if (G.Cave.IsSolid(at + new Vector2(0, 6))) break; at.Y += 6; }
        float r = Tune.AutomatonHero.SlamRadius * (0.75f + 0.25f * Stats.SlamMult);
        float dmg = _swingDmg / Tune.Hero.FinisherDamageMult * Tune.AutomatonHero.SlamShare * Stats.SlamMult;
        float total = 0;
        foreach (var e in G.Enemies.ToArray())
        {
            if (e.Dead || !e.CanBeHit || _swingHits.Contains(e)) continue;
            var to = e.GlobalPosition - at;
            if (to.Length() > r + e.HitRadius || !G.Cave.LineClear(at + new Vector2(0, -6), e.GlobalPosition)) continue;
            _swingHits.Add(e);
            float dealt = e.Hurt(dmg, new Vector2(Math.Sign(to.X == 0 ? Facing : to.X) * 260f, -120f), e.GlobalPosition);
            total += Math.Max(0f, dealt);
        }
        if (total > 0) OnDealtDamage(total);
        GasCloud.IgniteAt(at, r * 0.6f);
        G.Fx.Shockwave(at, r, new Color(1f, 0.75f, 0.45f, 0.7f), 0.3f);
        G.Fx.Dust(at, 10, 2f);
        G.Fx.Debris(at, new Color(0.45f, 0.4f, 0.36f), 6, 200);
        for (int k = 0; k < 6; k++) G.Fx.Ember(at + new Vector2(G.Range(-10, 10), G.Range(-4, 2)), new Color(1f, 0.7f, 0.3f));
        G.Fx.AddShake(4f);
        G.Sfx.Play("slam", at, -4, 0.05f, 0.9f);
        G.Main.Rumble(0.5f, 0.7f, 0.18f);
    }

    // ---------------------------------------------------------------- the water

    /// <summary>
    /// The Automaton in water: it sinks fast (faster with down held), walks the bottom, and each press of jump kicks it up;
    /// at the surface a press leaps out as anyone does (with its own short jump).
    /// </summary>
    private Vector2 SinkingSwim(PlayerInput inp, Vector2 v, float dt, CaveData cave)
    {
        float spd = SwimSpeedBase * Stats.SwimSpeed * Tune.AutomatonHero.WadeMult;
        var flow = Vector2.Zero;
        if (cave.Flow != 0f) { flow = cave.FlowAt(GlobalPosition) * 0.5f; v -= flow; } // (heavy: the current carries it half as much)
        v.X = Mathf.MoveToward(v.X, inp.Move.X * spd * (Repairing ? Tune.AutomatonHero.RepairMove : 1f), Tune.Hero.SwimAccel * dt);
        float sink = inp.Move.Y > 0.5f ? Tune.AutomatonHero.SinkDown : Tune.AutomatonHero.SinkSpeed;
        v.Y = Mathf.MoveToward(v.Y, sink, Tune.AutomatonHero.SinkAccel * dt);
        v.X *= 1f / (1f + Tune.Hero.WaterDrag * 0.5f * dt);
        bool nearSurface = GlobalPosition.Y < cave.WaterY + 20;
        if (_jumpBuffer > 0)
        {
            _jumpBuffer = 0;
            if (nearSurface && _leapReady)
            {
                v.Y = -BaseJumpV * MathF.Sqrt(Stats.JumpMult) * Tune.Hero.SurfaceLeapMult;
                _jumpCutDone = true;
                _leapReady = false;
                G.Sfx.Play("jump", GlobalPosition, -6, 0.05f, 0.7f);
                flow = Vector2.Zero;
            }
            else if (!nearSurface && _strokeCd <= 0)
            {
                // a kick of the legs and a gout of bubbles from the joints
                _strokeCd = Tune.AutomatonHero.StrokeEvery;
                v.Y = Math.Min(v.Y, 0f) - Tune.AutomatonHero.StrokeSpeed;
                v.Y = Math.Max(v.Y, -Tune.AutomatonHero.StrokeSpeed * 1.3f);
                G.Fx.Bubbles(GlobalPosition + new Vector2(0, 8), 4);
                G.Sfx.Play("bubble", GlobalPosition, -10, 0.1f, 0.7f);
                Anim.Punch(new Vector2(0.92f, 1.08f));
            }
        }
        return v + flow;
    }

    // ---------------------------------------------------------------- pressurize (its support)

    private float _pressT = -1f, _pressSfx, _pressedFor;
    private bool _pressOn;
    /// <summary>Building up pressure: glowing and shaking harder and harder, slowed, unable to strike.</summary>
    public bool Pressurising => _pressT >= 0f;
    /// <summary>Pressurized: striking faster and harder, moving faster, jumping higher, burning fuel as it goes.</summary>
    public bool Overpressured => _pressOn;
    /// <summary>For the HUD: how long the fuel it holds would keep it pressurized.</summary>
    public float PressureLeft => _pressOn ? Fuel / Math.Max(0.01f, Tune.AutomatonHero.PressureFuel) : 0f;
    /// <summary>0..1 through the build-up (0 when not building).</summary>
    public float PressureBuild => _pressT < 0f ? 0f : Math.Clamp(_pressT / Tune.AutomatonHero.PressureSeconds, 0f, 1f);
    private static readonly Color PressureGlow = new(1f, 0.5f, 0.15f);

    /// <summary>Pressurize: the build-up begins (a blow doesn't break it); pressed again, building or pressurized, it lets the pressure go.</summary>
    private bool TryOverPressure()
    {
        if (!IsAutomaton || Possessed) return false;
        if (Pressurising || Overpressured) { EndOverPressure(); return true; }
        if (Fuel < Tune.AutomatonHero.PressureMinFuel) { SayNo("NO FUEL"); _supportCd = 0.5f; return false; }
        _pressT = 0f;
        _pressSfx = 0f;
        StopRepair();
        Anim.FlashColor = PressureGlow;
        G.Fx.Text(GlobalPosition + new Vector2(0, -30), "PRESSURIZING", PressureGlow.Lightened(0.3f), 10, 0.9f);
        G.Sfx.Play("clink", GlobalPosition, -6, 0.05f, 0.5f);
        return true;
    }

    private void TickOverPressure(float dt)
    {
        if (_pressT >= 0f)
        {
            _pressT += dt;
            float k = PressureBuild;
            // it shakes harder and harder, and glows hotter
            Shake(0.3f + 3.2f * k * k, 0.12f + 0.6f * k, 4f + 26f * k, 18f * k, dt);
            // the rattle quickens and rises
            if ((_pressSfx -= dt) <= 0f)
            {
                _pressSfx = 0.32f - 0.24f * k;
                G.Sfx.Play("clink", GlobalPosition, -14 + 8 * k, 0.05f, 0.6f + 0.9f * k);
            }
            G.Main.Rumble(0.1f + 0.4f * k, 0.05f + 0.35f * k, 0.05f);
            if (k >= 1f) { _pressT = -1f; BeginOverPressure(); }
        }
        else if (_pressOn)
        {
            _pressedFor += dt;
            // every moment of it burns fuel; when the fuel is gone, so is the pressure
            Fuel = Math.Max(0f, Fuel - Tune.AutomatonHero.PressureFuel * dt);
            // still shaking and glowing, the whole while: a hard thrum, steam streaming off it
            Shake(1.4f, 0.32f + 0.08f * MathF.Sin(_animT * 11f), 14f, 6f, dt);
            if ((_pressSfx -= dt) <= 0f) { _pressSfx = 0.16f; G.Sfx.Play("clink", GlobalPosition, -16, 0.1f, 1.4f); }
            if (Fuel <= 0f) { G.Fx.Text(GlobalPosition + new Vector2(0, -30), "OUT OF FUEL", PressureGlow, 10, 0.9f); EndOverPressure(); }
        }
    }

    /// <summary>The shaking and glowing of a body under pressure: a jitter of <paramref name="shake"/> px, a glow, steam and sparks.</summary>
    private void Shake(float shake, float glow, float steamRate, float emberRate, float dt)
    {
        Anim.Position = new Vector2(G.Range(-1f, 1f), G.Range(-0.5f, 0.5f)) * shake;
        Anim.FlashColor = PressureGlow;
        Anim.Flash(glow);
        if (G.Chance(dt * steamRate)) G.Fx.Smoke(GlobalPosition + new Vector2(G.Range(-8, 8), G.Range(-16, 4)), 1, new Color(0.94f, 0.95f, 0.97f, 0.5f), 7);
        if (G.Chance(dt * emberRate)) G.Fx.Ember(GlobalPosition + new Vector2(G.Range(-8, 8), G.Range(-14, 6)), PressureGlow);
    }

    private void BeginOverPressure()
    {
        _pressedFor = 0f;
        ApplyOverPressure(+1);
        G.Fx.Shockwave(GlobalPosition + new Vector2(0, -4), 34, new Color(PressureGlow, 0.8f), 0.3f);
        G.Fx.Burst(GlobalPosition + new Vector2(0, -6), new Color(0.95f, 0.96f, 1f, 0.7f), 16, 220, 2.6f, 0.5f, -40);
        G.Fx.Text(GlobalPosition + new Vector2(0, -32), "PRESSURIZED", PressureGlow.Lightened(0.35f), 12, 1f);
        G.Sfx.Play("dodge", GlobalPosition, 0, 0.05f, 0.4f);
        G.Sfx.Play("slam", GlobalPosition, -6, 0.05f, 1.3f);
        G.Fx.AddShake(3f);
        G.Main.Rumble(0.6f, 0.6f, 0.2f);
    }

    /// <summary>The pressure goes (a second press, the fuel run out, or the hero leaving the level): the stats go back as they were,
    /// and the support starts to come back.</summary>
    private void EndOverPressure()
    {
        bool was = _pressT >= 0f || _pressOn;
        _pressT = -1f;
        if (_pressOn) ApplyOverPressure(-1);
        if (!was) return;
        _supportCd = Tune.AutomatonHero.PressureCooldown * Stats.AbilityCdMult;
        if (Anim != null) { Anim.FlashColor = Colors.White; Anim.Position = Vector2.Zero; }
        if (IsInsideTree() && G.Fx != null)
        {
            G.Fx.Smoke(GlobalPosition + new Vector2(0, -8), 4, new Color(0.9f, 0.9f, 0.92f, 0.5f), 8);
            G.Sfx.Play("dodge", GlobalPosition, -8, 0.05f, 0.7f);
        }
    }

    private void ApplyOverPressure(int dir)
    {
        if (dir > 0 == _pressOn) return;
        _pressOn = dir > 0;
        float m(float x) => dir > 0 ? x : 1f / x;
        Stats.AttackSpeed *= m(Tune.AutomatonHero.PressureAttack);
        Stats.DamageMult *= m(Tune.AutomatonHero.PressureDamage);
        Stats.MoveSpeed *= m(Tune.AutomatonHero.PressureSpeed);
        Stats.JumpMult *= m(Tune.AutomatonHero.PressureJump);
    }
}
