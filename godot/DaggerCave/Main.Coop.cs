using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

public partial class Main
{
    private IEnumerator<object> _coopSteps;

    /// <summary>
    /// `--scenario=coop --hero=NAME`: what a hero does for the others.
    /// rogue: a dagger thrown at a wall stays in it, its hilt a foothold to stand on, and goes home when recalled.
    /// swordsman: the heaving swing breaks a rubble pile in one blow and shatters a frozen creature.
    /// warden: the shield bash shatters a frozen creature.
    /// </summary>
    private void CoopScenario()
    {
        if (_coopSteps == null) { if (_scT < 0.6f) return; _coopSteps = CoopRun().GetEnumerator(); }
        if (!_coopSteps.MoveNext()) ScEnd();
    }

    /// <summary>A stretch of level floor with <paramref name="clear"/> px of open air before it to the <paramref name="side"/>, and (if <paramref name="wall"/>) a steep wall at the end of it.</summary>
    private bool FindRun(int side, float clear, bool wall, out Vector2 stand)
    {
        var cave = G.Cave; stand = default;
        for (float x = 60; x < cave.SizePx.X - 60; x += 8)
            for (float y = 60; y < cave.SizePx.Y - 60; y += 16)
            {
                var p = new Vector2(x, y);
                if (cave.IsSolid(p) || !cave.FindFloor(p, 24, out var f)) continue;
                if (cave.IsWater(f + new Vector2(0, -8)) || !cave.IsSolid(f + new Vector2(0, 8))) continue;
                var eye = f + new Vector2(0, -12);
                var far = eye + new Vector2(side * clear, 0);
                if (!cave.LineClear(eye, far) || cave.IsSolid(far) || !cave.IsSolid(f + new Vector2(side * (clear * 0.5f), 6))) continue;
                if (cave.IsSolid(eye + new Vector2(0, -26))) continue;
                if (wall)
                {
                    var end = eye + new Vector2(side * (clear + 14), 0);
                    if (!cave.IsSolid(end) || cave.IsSolid(end + new Vector2(-8, 0))) { }
                    if (!cave.Raycast(eye, new Vector2(side, 0), clear + 60, out var hit, 2f)) continue;
                    var n = cave.OpenGradient(hit + new Vector2(side * 2, 0));
                    if (Math.Abs(n.Y) > 0.4f || hit.DistanceTo(eye) < 40f || hit.DistanceTo(eye) > clear + 40f) continue;
                    // (room above the hilt to jump up to it, and room beside it to stand)
                    if (cave.IsSolid(hit + new Vector2(-side * 10, -24))) continue;
                }
                stand = f + new Vector2(0, -14);
                return true;
            }
        return false;
    }

    private IEnumerable<object> CoopRun()
    {
        var p = G.Player; var cave = G.Cave;
        p.Stats.MaxHp = 5000; p.Hp = 5000;
        foreach (var e in G.Enemies.ToArray()) e.QueueFree();
        int side = 1;
        switch (p.Stats.Hero)
        {
            case HeroKind.Rogue:
            {
                bool found = FindRun(1, 70, true, out var stand) || (side = -1) == -1 && FindRun(-1, 70, true, out stand);
                ScCheck($"a floor with a wall to throw at ({stand.Round()}, facing {side})", found);
                if (!found) yield break;
                // (a press lasts one physics tick here, as a real one does, however slow the frames: a held-down edge would throw both daggers and wall-jump)
                p.InputOverride = () => { var i = _scInput; _scInput.Jump = false; _scInput.Ability = false; _scInput.Ability2 = false; return i; };
                p.GlobalPosition = stand; p.Velocity = Vector2.Zero;
                foreach (var _ in SbSleep(0.8f)) yield return null;
                _scInput = new PlayerInput { Ability = true, Aim = new Vector2(side, 0), AimGiven = true }; yield return null;
                _scInput = default;
                foreach (var _ in SbSleep(0.9f)) yield return null;
                var d = p.ThrownDaggerAt(1) ?? p.ThrownDaggerAt(0);
                ScCheck($"a dagger thrown at a wall goes into it and stays ({d?.State}, in the wall: {d?.InWall})", d != null && d.State == ThrownDagger.Phase.Stuck && d.InWall);
                if (d == null || !d.InWall) yield break;
                ScShot("coop_dagger_in_wall");
                // another hero's weight on the hilt: a hero put over it comes down on it
                var hilt = d.GlobalPosition;
                p.GlobalPosition = hilt + new Vector2(-side * 8, -22); p.Velocity = Vector2.Zero;
                foreach (var _ in SbSleep(1.0f)) yield return null;
                float restY = p.GlobalPosition.Y;
                ScCheck($"a hero lowered onto it stands on it ({restY - hilt.Y:0} px from the dagger, on floor {p.IsOnFloor()})", p.IsOnFloor() && Math.Abs(restY - (hilt.Y - 14f)) < 9f);
                ScShot("coop_dagger_foothold");
                // recalled, it comes home and the foothold goes
                _scInput = new PlayerInput { Ability2 = true }; yield return null; _scInput = default;
                foreach (var _ in SbSleep(1.2f)) yield return null;
                ScCheck($"recalled, the dagger leaves the wall ({!GodotObject.IsInstanceValid(d) || !d.InWall}) and is back in hand ({p.DaggersInHand} of 2)", (!GodotObject.IsInstanceValid(d) || !d.InWall) && p.DaggersInHand == 2);
                // a second one, slanted up, so its hilt is well above the ground: a ledge a hero has to jump up through
                p.GlobalPosition = stand; p.Velocity = Vector2.Zero; p.Facing = side;
                foreach (var _ in SbSleep(0.8f)) yield return null;
                float run = Math.Abs(hilt.X - stand.X);
                _scInput = new PlayerInput { Ability = true, Aim = new Vector2(side * run, -30f).Normalized(), AimGiven = true }; yield return null;
                _scInput = default;
                foreach (var _ in SbSleep(0.9f)) yield return null;
                var d2 = p.ThrownDaggerAt(0) ?? p.ThrownDaggerAt(1);
                bool in2 = d2 != null && d2.State == ThrownDagger.Phase.Stuck && d2.InWall;
                ScCheck($"a dagger thrown up at the wall goes in it too ({d2?.State}, in the wall: {d2?.InWall})", in2);
                if (!in2) yield break;
                var hilt2 = d2.GlobalPosition;
                ScShot("coop_dagger_high");
                // below it, beside the wall, on the ground: a jump goes up through the hilt and comes down on it
                p.GlobalPosition = hilt2 + new Vector2(-side * 8, 70); p.Velocity = Vector2.Zero;
                foreach (var _ in SbSleep(1.0f)) yield return null;
                float groundY = p.GlobalPosition.Y;
                float height = groundY - 14f - hilt2.Y;
                ScCheck($"the hilt hangs {groundY - hilt2.Y:0} px above where a hero stands, within a jump's reach", height > 14f && height < 50f && p.IsOnFloor());
                bool left = false, landed = false; float minY = 1e9f;
                _scInput = new PlayerInput { Jump = true, JumpHeld = true }; yield return null;
                for (float t = 0; t < 2.4f; t += (float)GetProcessDeltaTime())
                {
                    _scInput = new PlayerInput { JumpHeld = t < 0.6f };
                    if (!p.IsOnFloor()) left = true;
                    minY = Math.Min(minY, p.GlobalPosition.Y);
                    if (left && p.IsOnFloor()) { landed = true; break; }
                    yield return null;
                }
                _scInput = default;
                float stoodAbove = groundY - p.GlobalPosition.Y;
                ScCheck($"a hero jumping up from below goes through it and lands on top ({landed}; {stoodAbove:0} px above where it jumped from, hilt {groundY - hilt2.Y:0}; the jump rose {groundY - minY:0} px)", landed && Math.Abs(p.GlobalPosition.Y - (hilt2.Y - 14f)) < 9f);
                ScShot("coop_dagger_climbed");
                _scInput = new PlayerInput { Ability2 = true }; yield return null; _scInput = default;
                foreach (var _ in SbSleep(1.2f)) yield return null;
                ScCheck($"recalled again, it is back in hand ({p.DaggersInHand} of 2)", p.DaggersInHand == 2);
                break;
            }
            case HeroKind.Swordsman:
            {
                bool found = FindRun(1, 90, false, out var stand) || (side = -1) == -1 && FindRun(-1, 90, false, out stand);
                ScCheck($"a floor to heave on ({stand.Round()})", found);
                if (!found) yield break;
                p.GlobalPosition = stand; p.Velocity = Vector2.Zero; p.Facing = side;
                foreach (var _ in SbSleep(0.6f)) yield return null;
                // a rubble pile, and a frozen goblin, both within the swing's reach
                var rubble = new Rubble { Position = stand + new Vector2(side * 34, -18), Size = new Vector2(24, 48), Index = 900 };
                _world.AddChild(rubble);
                var goblin = new Goblin { Position = stand + new Vector2(side * 28, 0) };
                goblin.SetMeta("test", true);
                _world.AddChild(goblin);
                goblin.MaxHp = goblin.Hp = 4000;
                goblin.FreezeSolid(60f);
                foreach (var _ in SbSleep(0.4f)) yield return null;
                ScCheck($"the pile stands ({!rubble.Cleared}, {rubble.Left} blows to go) and the goblin is frozen ({goblin.FrozenSolid})", !rubble.Cleared && goblin.FrozenSolid);
                float hp0 = goblin.Hp; int sh0 = p.ShatteredByBlows;
                _scInput = new PlayerInput { Ability2 = true, Aim = new Vector2(side, 0), AimGiven = true }; yield return null; _scInput = default;
                foreach (var _ in SbSleep(1.4f)) yield return null;
                ScCheck($"one heaving swing brings the whole pile down ({rubble.Cleared})", rubble.Cleared);
                ScCheck($"and shatters the frozen goblin (shattered {p.ShatteredByBlows - sh0}, frozen now {goblin.FrozenSolid}, hp {hp0:0} -> {goblin.Hp:0})", p.ShatteredByBlows > sh0 && !goblin.FrozenSolid && goblin.Hp < hp0);
                break;
            }
            case HeroKind.Warden:
            {
                bool found = FindRun(1, 70, false, out var stand) || (side = -1) == -1 && FindRun(-1, 70, false, out stand);
                ScCheck($"a floor to bash on ({stand.Round()})", found);
                if (!found) yield break;
                p.GlobalPosition = stand; p.Velocity = Vector2.Zero; p.Facing = side;
                foreach (var _ in SbSleep(0.6f)) yield return null;
                var goblin = new Goblin { Position = stand + new Vector2(side * 22, 0) };
                goblin.SetMeta("test", true);
                _world.AddChild(goblin);
                goblin.MaxHp = goblin.Hp = 4000;
                goblin.FreezeSolid(60f);
                foreach (var _ in SbSleep(0.4f)) yield return null;
                float hp0 = goblin.Hp; int sh0 = p.ShatteredByBlows;
                ScCheck($"the goblin is frozen solid ({goblin.FrozenSolid})", goblin.FrozenSolid);
                _scInput = new PlayerInput { Ability2 = true, Aim = new Vector2(side, 0), AimGiven = true }; yield return null; _scInput = default;
                foreach (var _ in SbSleep(1.0f)) yield return null;
                ScCheck($"the shield bash shatters it (shattered {p.ShatteredByBlows - sh0}, frozen now {goblin.FrozenSolid}, hp {hp0:0} -> {goblin.Hp:0})", p.ShatteredByBlows > sh0 && !goblin.FrozenSolid && goblin.Hp < hp0);
                break;
            }
            default:
                ScCheck($"a scenario for {p.Stats.Hero}", false);
                break;
        }
    }
}
