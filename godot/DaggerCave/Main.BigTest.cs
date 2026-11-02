using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

public partial class Main
{
    private IEnumerator<object> _bgSteps;

    /// <summary>
    /// `--scenario=big --hero=swordsman|warden|elementalist|vitalist|aegis|rogue`, the heroes' great specials: the special button is the hero's great
    /// stroke (Player.Big.cs): it strikes a whole knot of creatures, rests a long while (a second press does nothing), and does its own thing
    /// besides (the Whirlwind strikes each many times, the Great Bash stuns, the Bloom mends, the Judgement wards, the Death Blossom can't be
    /// touched). Every hero jumps again in mid-air. A caster's bolt or drain splashes the creatures about the one it strikes.
    /// </summary>
    private void BigScenario()
    {
        if (_bgSteps == null) { if (_scT < 0.8f) return; _bgSteps = BigRun().GetEnumerator(); }
        if (!_bgSteps.MoveNext()) ScEnd();
    }

    private IEnumerable<object> BigRun()
    {
        var p = G.Player;
        var hero = p.Stats.Hero;
        foreach (var e in G.Enemies.ToArray()) { G.Enemies.Remove(e); e.QueueFree(); }
        _genSpots.Clear();
        p.Stats.MaxHp = p.Hp = 5000;
        p.InputOverride = () => { var i = _scInput; _scInput.Attack = _scInput.Ability = _scInput.Ability2 = _scInput.Dodge = _scInput.Jump = false; return i; };
        foreach (var _ in ElWait(0.5f)) yield return null;
        var home = p.GlobalPosition;
        var aim = new Vector2(p.Facing, 0);

        // ---- a jump in mid-air, for everyone
        ScCheck($"the {hero} has a jump in mid-air ({p.AirJumpsMax}) and kicks off walls ({p.CanWallJump})", p.AirJumpsMax >= 1 && p.CanWallJump);
        // (somewhere with room overhead for two jumps, and no ledge to land on in between)
        foreach (float dx in new[] { 0f, 40f, -40f, 80f, -80f, 120f, -120f, 160f, -160f, 200f, -200f, 260f, -260f, 320f, -320f })
        {
            if (!G.Cave.FindFloor(home + new Vector2(dx, -30f), 120f, out var fl)) continue;
            var stand = fl + new Vector2(0, -12f);
            bool open = true;
            for (float ox = -8f; ox <= 8f && open; ox += 8f)
            {
                G.Cave.Raycast(stand + new Vector2(ox, 0), Vector2.Up, 200f, out var roof, 4f);
                open = stand.Y - roof.Y >= 196f;
            }
            if (!open) continue;
            p.GlobalPosition = stand; p.Velocity = Vector2.Zero;
            break;
        }
        for (int f = 0; f < 120 && !p.IsOnFloor(); f++) yield return null;
        float y0 = p.GlobalPosition.Y;
        // (the button held through each jump, as a player would)
        _scInput = new PlayerInput { Jump = true, JumpHeld = true }; yield return null;
        for (int f = 0; f < 90 && p.Velocity.Y < 0f || f < 3; f++) { _scInput.JumpHeld = true; yield return null; }
        float top1 = p.GlobalPosition.Y;
        _scInput = new PlayerInput { Jump = true, JumpHeld = true }; yield return null;
        float top2 = p.GlobalPosition.Y;
        for (int f = 0; f < 90 && p.Velocity.Y < 0f || f < 3; f++) { _scInput.JumpHeld = true; top2 = Math.Min(top2, p.GlobalPosition.Y); yield return null; }
        _scInput = default;
        ScCheck($"the second jump carries them on up ({y0 - top1:0} px, then {top1 - top2:0} px more; {p.AirJumpsLeft} left)", top1 - top2 > 20f && p.AirJumpsLeft == p.AirJumpsMax - 1);
        for (int f = 0; f < 180 && !p.IsOnFloor(); f++) yield return null;
        p.GlobalPosition = home; p.Velocity = Vector2.Zero;
        foreach (var _ in ElWait(0.3f)) yield return null;

        // ---- a knot of creatures where the stroke goes
        float reach = hero switch
        {
            HeroKind.Elementalist or HeroKind.Aegis => Math.Min(Tune.ElementalStaff.FixedRange - 30f, 150f),
            HeroKind.Warden => 60f,
            HeroKind.Rogue => 50f,
            _ => 30f,
        };
        var knot = new List<Enemy>();
        float[] offs = hero is HeroKind.Elementalist or HeroKind.Aegis ? new[] { 0f, -24f, 24f, -44f, 44f } : new[] { 0f, 20f, 40f, -20f, -40f };
        foreach (float o in offs)
        {
            float dx = reach + o;
            // (the Warden's arc is ahead only: all of them before the shield)
            if (hero == HeroKind.Warden) dx = 30f + Math.Abs(o) * 1.6f;
            var d = ElDummy(p, dx);
            knot.Add(d);
        }
        foreach (var _ in ElWait(0.3f)) yield return null;
        float hp0 = knot.Sum(e => e.Hp);
        int uses = p.BigUses, struck = p.BigStruck;
        if (hero == HeroKind.Vitalist) p.Hp = p.Stats.MaxHp * 0.5f;
        float hpMine = p.Hp, barrier0 = p.BarrierHp;
        _scInput = new PlayerInput { Ability = true, Aim = aim, AimGiven = true }; yield return null; yield return null; _scInput = default;
        bool untouchable = false, airborne = false;
        int whirlHits = 0, stunned = 0;
        for (float t = 0; t < 1.8f; t += (float)GetProcessDeltaTime())
        {
            if (p.Blossoming) { untouchable |= p.Invulnerable; airborne |= !p.IsOnFloor(); }
            if (p.Whirling) whirlHits = p.BigStruck - struck;
            stunned = Math.Max(stunned, knot.Count(e => IsInstanceValid(e) && e.StunLeft > 0.2f));
            yield return null;
        }
        if (p.LastMeteor != null) ScCheck($"the meteor fell from on high and landed ({p.LastMeteor.Landed || !IsInstanceValid(p.LastMeteor)})", !IsInstanceValid(p.LastMeteor) || p.LastMeteor.Landed);
        ScShot($"big_{hero.ToString().ToLowerInvariant()}");
        string name = Elements.SimpleName(hero, KitRole.Special, p.Mode);
        int hit = knot.Count(e => e.Hp < e.MaxHp);
        ScCheck($"{name}: used ({p.BigUses - uses}), it strikes the whole knot ({hit} of {knot.Count}, {hp0 - knot.Sum(e => e.Hp):0} dealt)", p.BigUses == uses + 1 && hit >= 4);
        float rest = p.BigCooldown;
        ScCheck($"  and rests a long while ({rest:0.0} s left)", rest > 5f);
        int uses2 = p.BigUses;
        _scInput = new PlayerInput { Ability = true, Aim = aim, AimGiven = true }; yield return null; yield return null; _scInput = default;
        ScCheck($"  a press while it rests does nothing ({p.BigUses - uses2})", p.BigUses == uses2);
        switch (hero)
        {
            case HeroKind.Swordsman:
                ScCheck($"  the blade comes round again and again ({p.BigStruck - struck} blows on {knot.Count} creatures)", p.BigStruck - struck >= knot.Count * 4);
                break;
            case HeroKind.Warden:
                ScCheck($"  they're flung back and stunned (at most {stunned} of {knot.Count} reeling at once)", stunned >= 4);
                ScCheck($"  and the charge is on the other button now ({Elements.Noun(hero, KitRole.Other)})", Elements.Noun(hero, KitRole.Other) == "Charge");
                break;
            case HeroKind.Vitalist:
                ScCheck($"  it mends her ({hpMine:0} -> {p.Hp:0}) and seeps her element into them ({knot.Count(e => e.SeepLeft > 0)} seeping)", p.Hp > hpMine + 1f && knot.Count(e => e.SeepLeft > 0) >= 4);
                break;
            case HeroKind.Aegis:
                ScCheck($"  it wards her ({barrier0:0} -> {p.BarrierHp:0})", p.BarrierHp > barrier0 + 1f);
                break;
            case HeroKind.Rogue:
                ScCheck($"  up in the air ({airborne}) and untouchable while the knives fly ({untouchable})", airborne && untouchable);
                break;
        }

        // ---- a caster's splash
        if (hero is HeroKind.Elementalist or HeroKind.Vitalist)
        {
            foreach (var e in knot) if (IsInstanceValid(e)) e.QueueFree();
            p.GlobalPosition = home; p.Velocity = Vector2.Zero;
            foreach (var _ in ElWait(0.3f)) yield return null;
            var main = ElDummy(p, 90f);
            // (beside it, off the line the bolt flies along: above it, and above and beyond it)
            var side = new[] { ElDummy(p, 90f), ElDummy(p, 112f) };
            side[0].GlobalPosition += new Vector2(0, -28f);
            side[1].GlobalPosition += new Vector2(0, -24f);
            var far = ElDummy(p, 90f + 140f);
            foreach (var _ in ElWait(0.3f)) yield return null;
            int splashed = p.Splashed;
            float m0 = main.Hp, s0 = side.Sum(e => e.Hp), f0 = far.Hp;
            for (int k = 0; k < 6 && main.Hp >= m0; k++)
            {
                _scInput = new PlayerInput { Attack = true, Aim = aim, AimGiven = true }; yield return null; yield return null; _scInput = default;
                foreach (var _ in ElWait(0.5f)) yield return null;
            }
            foreach (var _ in ElWait(0.4f)) yield return null;
            float md = m0 - main.Hp, sd = (s0 - side.Sum(e => e.Hp)) / 2f;
            ScCheck($"  the {Elements.Noun(hero, KitRole.Primary).ToLowerInvariant()} splashes the creatures about the one it strikes ({p.Splashed - splashed} splashed; {md:0.0} on it, {sd:0.0} on each beside it: x{(md > 0 ? sd / md : 0):0.00}, want about {Tune.Splash.Share})",
                p.Splashed - splashed >= 2 && md > 0 && Math.Abs(sd / md - Tune.Splash.Share) < 0.12f);
            ScCheck($"  but not one far off ({f0 - far.Hp:0.0})", far.Hp >= f0 - 0.01f);
        }
    }
}
