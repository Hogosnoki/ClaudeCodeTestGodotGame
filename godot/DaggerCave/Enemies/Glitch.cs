using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

/// <summary>
/// The Glitch: a fault in the world itself, met (rarely) only by a party in which everyone has slain the Elder Dragon. It is a
/// heap of broken rectangles bigger than a hero, never quite where it seems (each game draws it somewhere within
/// <see cref="Tune.Glitch.ShowRadius"/> of where it really is, the place changing many times a second), and it does not move as
/// things move: it stutters toward a hero in jumps, through rock as through air, and every few seconds it is simply somewhere
/// else, beside another hero. The music stops while it is here.
/// A blow doesn't hurt it: its health is rolled anew, anywhere from nothing to all of it, and it can't come to nothing until it
/// has been struck <see cref="Tune.Glitch.HitsToMortal"/> times. Its own blow does the same to a hero (not to nothing before the
/// eighth), and now and then leaves something wrong with them too. Brought down, the music comes back and the wall at the end of
/// the guardian's chamber goes wrong as well (see <see cref="Main.OpenGlitchWall"/>).
/// </summary>
public partial class Glitch : Enemy
{
    private float _blinkT, _stepT, _windT, _cd, _chirpT;
    private bool _winding;
    private Player _tgt;
    /// <summary>How many times it has been struck (it can't fall before <see cref="Tune.Glitch.HitsToMortal"/>).</summary>
    public int Struck { get; private set; }
    /// <summary>For the tests and the view: blinks made, and whether its blow is coming.</summary>
    public int Blinks { get; private set; }
    public bool Winding => _winding;
    public float HurtFlashT => HurtFlash;
    public override void NetState(NetIO io) { io.Sync(ref _winding); }

    public Glitch()
    {
        MaxHp = Tune.Glitch.Hp; BodyRadius = Tune.Glitch.Radius; ContactDamage = 0; XpValue = Tune.Glitch.Xp;
        KnockResist = 1f;
    }

    protected override bool UsesGravity => false;
    public override bool Interruptible => false;
    public override string HitSound => "glitch_chirp";
    protected override Color BloodColor => GlitchColor();

    protected override void Setup()
    {
        DisplayName = "THE GLITCH";
        NamePrefix = "";
        Awake = true;
        ManualMove = true;
        MotionMode = MotionModeEnum.Floating;
        // (it goes through rock as through air)
        CollisionMask = 0;
        // (its health is no creature's: the depth doesn't grow it)
        if (!Puppet) { MaxHp = Tune.Glitch.Hp; Hp = MaxHp; }
        // (the animator only keeps the clock: the body you see is the broken one GlitchView draws)
        UseSprite("wraith");
        Anim.Visible = false;
        _blinkT = G.Range(Tune.Glitch.BlinkMin, Tune.Glitch.BlinkMax);
        _chirpT = 0.5f;
        G.Main?.GlitchArrived(this);
    }

    public override void _ExitTree()
    {
        base._ExitTree();
        G.Main?.GlitchLeft(this);
    }

    /// <summary>A colour no creature bleeds: the primaries of a broken screen.</summary>
    public static Color GlitchColor() => G.Pick(new[]
    {
        new Color(1f, 0f, 1f), new Color(0f, 1f, 1f), new Color(0.2f, 1f, 0.2f), new Color(1f, 1f, 0f), new Color(1f, 1f, 1f), new Color(0.1f, 0.1f, 0.1f),
    });

    /// <summary>What it shows for a number: never a number.</summary>
    public static string GlitchText() => G.Pick(new[] { "ERR", "NaN", "???", "0x" + G.Rng.Next(0, 65536).ToString("X4"), "-" + G.Rng.Next(0, 99999), "null", "##", "▓▒░", "OVERFLOW", "-0" });

    public override void _Process(double delta)
    {
        // (every game hears its own: a chirp now and then, a hum, a screech)
        if ((_chirpT -= (float)delta) <= 0 && !Dead)
        {
            _chirpT = G.Range(0.5f, 1.8f);
            G.Sfx.Play(G.Chance(0.25f) ? "glitch_hum" : "glitch_chirp", GlobalPosition, -6, 0.4f, G.Range(0.6f, 1.6f));
        }
    }

    private Player Target()
    {
        if (_tgt != null && GodotObject.IsInstanceValid(_tgt) && !_tgt.Dead) return _tgt;
        _tgt = G.Players.Where(h => GodotObject.IsInstanceValid(h) && !h.Dead).OrderBy(h => h.GlobalPosition.DistanceSquaredTo(GlobalPosition)).FirstOrDefault();
        return _tgt;
    }

    protected override void Think(float dt)
    {
        Velocity = Vector2.Zero;
        var tgt = Target();
        if (tgt == null) return;
        if (_winding)
        {
            if ((_windT -= dt) <= 0) Lash();
            return;
        }
        _cd -= dt; _stepT -= dt;
        if ((_blinkT -= dt) <= 0) { Blink(); return; }
        var to = tgt.GlobalPosition + new Vector2(0, -8) - GlobalPosition;
        if (Math.Abs(to.X) > 2f) Face = Math.Sign(to.X);
        if (to.Length() < Tune.Glitch.Reach && _cd <= 0)
        {
            _winding = true; _windT = Tune.Glitch.Windup;
            G.Sfx.Play("glitch", GlobalPosition, -2, 0.2f, 1.4f);
            return;
        }
        // no gliding, no gait: it is here, and then it is a jump nearer, a little askew
        if (_stepT <= 0 && to.Length() > Tune.Glitch.Reach * 0.6f)
        {
            _stepT = Tune.Glitch.StepEvery * G.Range(0.5f, 1.7f);
            var dir = to.Normalized();
            var side = new Vector2(-dir.Y, dir.X) * G.Range(-8f, 8f);
            GlobalPosition += dir * Math.Min(Tune.Glitch.StepPx * G.Range(0.6f, 1.4f), to.Length() - Tune.Glitch.Reach * 0.5f) + side;
        }
    }

    /// <summary>Gone from here and beside a hero (another one than it was after, when there is one).</summary>
    private void Blink()
    {
        _blinkT = G.Range(Tune.Glitch.BlinkMin, Tune.Glitch.BlinkMax);
        var heroes = G.Players.Where(h => GodotObject.IsInstanceValid(h) && !h.Dead).ToList();
        if (heroes.Count == 0) return;
        var others = heroes.Where(h => h != _tgt).ToList();
        _tgt = others.Count > 0 ? G.Pick(others.ToArray()) : heroes[0];
        var cave = G.Cave;
        var at = _tgt.GlobalPosition;
        // (it may land in the rock: it doesn't mind. Open air is tried first, so it can be seen)
        for (int k = 0; k < 10; k++)
        {
            at = _tgt.GlobalPosition + new Vector2(0, -10) + G.RandDir() * G.Range(Tune.Glitch.BlinkNear, Tune.Glitch.BlinkFar);
            if (cave == null || !cave.IsSolid(at)) break;
        }
        Smear(GlobalPosition);
        GlobalPosition = at;
        Smear(at);
        Blinks++;
        G.Sfx.Play("glitch", at, -4, 0.3f, G.Range(0.7f, 1.3f));
    }

    /// <summary>Broken pixels where it was, or arrives.</summary>
    private void Smear(Vector2 at)
    {
        for (int k = 0; k < 3; k++) G.Fx.Debris(at + G.RandDir() * 10f, GlitchColor(), 4, 140);
        G.Fx.Flash(at, 26, GlitchColor(), 0.08f);
    }

    /// <summary>Its blow: every hero within reach has their health rolled anew (and now and then something worse).</summary>
    private void Lash()
    {
        _winding = false;
        _cd = Tune.Glitch.Cooldown;
        G.Sfx.Play("glitch", GlobalPosition, 2, 0.2f, 0.8f);
        G.Fx.Shockwave(GlobalPosition, Tune.Glitch.Reach + 10f, GlitchColor(), 0.2f);
        foreach (var h in G.Players.ToArray())
        {
            if (!GodotObject.IsInstanceValid(h) || h.Dead) continue;
            if (h.GlobalPosition.DistanceTo(GlobalPosition) > Tune.Glitch.Reach + 12f) continue;
            h.Afflict(NetSync.Boon.Glitch, 0f, 0f);
        }
    }

    /// <summary>A blow rolls its health anew: anything from nothing (only after the hundredth) to all of it.</summary>
    protected override float TakeHit(float dmg, Vector2 knock, Vector2 hitPos)
    {
        if (Dead || dmg <= 0) return 0;
        Awake = true;
        Struck++;
        bool mortal = Struck >= Tune.Glitch.HitsToMortal;
        Hp = mortal && G.Chance(Tune.Glitch.ZeroChance) ? 0f : MathF.Round(G.Range(1f, MaxHp));
        HurtFlash = 0.12f;
        G.Fx.Text(HeadPoint(2f), GlitchText(), GlitchColor(), G.Rng.Next(10, 20));
        G.Fx.Debris(hitPos, GlitchColor(), 4, 160);
        G.Sfx.Play("glitch_chirp", GlobalPosition, -2, 0.5f, G.Range(0.5f, 2f));
        if (Hp <= 0) Die();
        return 1f;
    }

    /// <summary>Nothing but the hundredth blow and after brings it down (not poison, nor fire, nor bleeding).</summary>
    protected override void Die()
    {
        if (Dead) return;
        if (Struck < Tune.Glitch.HitsToMortal) { Hp = Math.Max(Hp, 1f); return; }
        // (it leaves no body behind to play a death on)
        var a = Anim; Anim = null; a?.QueueFree();
        base.Die();
        Defeated();
    }

    protected override void OnPuppetGone(bool died) { if (died) Defeated(); }

    private bool _defeated;
    private void Defeated()
    {
        if (_defeated) return;
        _defeated = true;
        for (int k = 0; k < 6; k++) Smear(GlobalPosition + G.RandDir() * G.Range(0, 30));
        G.Fx.ScreenFlash(new Color(1f, 0f, 1f), 0.3f);
        G.Main?.GlitchDefeated(this);
    }
}
