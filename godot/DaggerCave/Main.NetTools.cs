using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

/// <summary>
/// The online test's co-op tools (a stretch of --nettest on the level below, after the vault): what one game's hero does for the
/// others, as the other game sees it. The joining player lets a rope down over an edge and the host's hero, in the host's game, climbs it up
/// to them; then, by the joiner's hero, a Rogue's dagger goes into a wall (the host's hero stands on its hilt there), an Elementalist's
/// updraft rises at the angle aimed (the same angle there), a Swordsman's heaving swing brings a rubble pile down (down there too).
/// </summary>
public partial class Main
{
    /// <summary>0 not begun (the joiner waits for the host's word), 1..99 running, 100 done (on both sides).</summary>
    private int _ntCoop;
    private float _ntCoopT;
    private Vector2 _ntCoopAt;
    private int _ntCoopSide;
    private float _ntCoopNum;
    private string _ntCoopNote = "";
    private bool _ntCoopFlag;
    private Rope _ntCoopRope;
    private ThrownDagger _ntCoopDagger;
    private Rubble _ntCoopRubble;
    private List<(Vector2 stand, int side, float depth)> _ntEdges;
    private int _ntEdgeAt, _ntCoopTries;

    private void CoopTo(int step) { _ntCoop = step; _ntCoopT = 0; }

    private static string Num(float v) => v.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
    private static float Parse(string s) => float.Parse(s, System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>A floor with open air before it (and a wall at the end of it, if asked): facing right if there is one, else left.</summary>
    private bool NtRun(float clear, bool wall, out Vector2 stand, out int side, float fromX = 60f)
    {
        side = 1;
        if (FindRun(1, clear, wall, out stand, fromX)) return true;
        side = -1;
        return FindRun(-1, clear, wall, out stand, fromX);
    }

    /// <summary>Where a hero can be set down to come onto a dagger's hilt in the wall at <paramref name="hilt"/> (the wall to the <paramref name="side"/>):
    /// a few px above it and out from the rock, with room round the whole body. False if the rock leaves none.</summary>
    private bool NtOverHilt(Vector2 hilt, int side, out Vector2 at)
    {
        var cave = G.Cave;
        foreach (float dx in new[] { 8f, 7f, 9f, 6f })
            foreach (float dy in new[] { -22f, -26f })
            {
                var p = hilt + new Vector2(-side * dx, dy);
                bool clear = true;
                foreach (var o in new[] { new Vector2(0, 0), new Vector2(-7, 0), new Vector2(7, 0), new Vector2(0, -14), new Vector2(-6, -9), new Vector2(6, -9), new Vector2(0, 12), new Vector2(-6, 8), new Vector2(6, 8) })
                    if (cave.IsSolid(p + o)) { clear = false; break; }
                // (and open air all the way down to the hilt)
                if (clear && cave.LineClear(p, p + new Vector2(0, -dy - 4f))) { at = p; return true; }
            }
        at = default;
        return false;
    }

    /// <summary>Whether a hero's body (the capsule) fits at <paramref name="p"/>: no rock or ledge slab in it.</summary>
    private bool NtBodyFits(Vector2 p)
    {
        foreach (var o in new[] { new Vector2(0, -12), new Vector2(0, 0), new Vector2(0, 12), new Vector2(-6, -8), new Vector2(6, -8), new Vector2(-6, 8), new Vector2(6, 8) })
            if (G.Cave.IsSolid(p + o)) return false;
        foreach (var l in RockLedge.All)
            if (GodotObject.IsInstanceValid(l) && !l.Broken && Math.Abs(p.X - l.GlobalPosition.X) < l.Half + 8f && Math.Abs(p.Y - l.GlobalPosition.Y) < l.ThickPx * 0.5f + 15f) return false;
        return true;
    }

    // ------------------------------------------------------------------ the host's game: the friend's tools, as they arrive here

    private void HostCoopStep(Player friend)
    {
        _ntCoopT += _ntDt;
        if (friend == null || !GodotObject.IsInstanceValid(friend)) { NtFail("the friend is still in the cave for the co-op checks"); return; }
        var me = G.Player;
        switch (_ntCoop)
        {
            case 0:
                NtSay("coop");
                CoopTo(1);
                break;

            // ---- the rope: let down in their game, climbed in this one
            case 1:
            {
                if (!NtGot("rope-down", out var r)) { if (_ntCoopT > 40) NtFail("the friend lets a rope down"); break; }
                _ntCoopNum = Parse(r);
                CoopTo(2);
                break;
            }
            case 2:
            {
                _ntCoopRope = Rope.All.FirstOrDefault(x => GodotObject.IsInstanceValid(x) && x.Holder == friend);
                bool hangs = _ntCoopRope != null && _ntCoopRope.State == Rope.Phase.Hanging;
                if (!hangs && _ntCoopT < 5f) break;
                NtCheck($"the friend's rope, let down in their game, hangs here too ({_ntCoopRope?.State}, {_ntCoopRope?.Unrolled:0} px of their {_ntCoopNum:0})", hangs && Math.Abs(_ntCoopRope.Unrolled - _ntCoopNum) < 6f);
                if (!hangs) { NtSay("rope-climbed bad no rope"); CoopTo(4); break; }
                // this game's hero takes hold of its lower end and pushes up
                // (where a hero's whole body fits, not wedged in a gap the rope alone threads)
                var grip = _ntCoopRope.PointAt(Math.Max(10f, _ntCoopRope.Unrolled - 30f));
                foreach (float back in new[] { 30f, 45f, 60f, 20f, 75f })
                {
                    var q = _ntCoopRope.PointAt(Math.Max(10f, _ntCoopRope.Unrolled - back));
                    if (NtBodyFits(q)) { grip = q; break; }
                }
                me.GlobalPosition = grip; me.Velocity = Vector2.Zero;
                _ntInput = new PlayerInput { Move = new Vector2(0, -1) };
                _ntCoopFlag = false;
                CoopTo(3);
                break;
            }
            case 3:
            {
                _ntCoopFlag |= me.OnRope;
                float dist = me.GlobalPosition.DistanceTo(friend.GlobalPosition);
                // (at the top: up on the ledge the rope hangs from, beside the friend)
                bool up = _ntCoopFlag && !me.OnRope && Math.Abs(me.GlobalPosition.Y - friend.GlobalPosition.Y) < 36f && Math.Abs(me.GlobalPosition.X - friend.GlobalPosition.X) < 150f;
                if (!up && _ntCoopT < 12f) break;
                _ntInput = default;
                NtCheck($"this game's hero climbs it up to the friend (took hold {_ntCoopFlag}; {dist:0} px from them at the top, on the rope {me.OnRope})", up);
                NtSay(up ? "rope-climbed ok" : $"rope-climbed bad {dist:0}");
                CoopTo(4);
                break;
            }
            case 4:
                // (they reel it in)
                if (!NtGot("rope-gone", out _ntCoopNote)) { if (_ntCoopT > 25) NtFail("the friend reels the rope in"); break; }
                CoopTo(5);
                break;
            case 5:
            {
                bool gone = !Rope.All.Any(x => GodotObject.IsInstanceValid(x) && x.Holder == friend);
                if (!gone && _ntCoopT < 4f) break;
                NtCheck($"reeled in over there, the rope is gone here too ({gone}; theirs: {_ntCoopNote})", gone && _ntCoopNote == "yes");
                // (what else this hero can do)
                switch (friend.Hero)
                {
                    case HeroKind.Rogue: CoopTo(10); break;
                    case HeroKind.Elementalist: CoopTo(20); break;
                    case HeroKind.Swordsman: CoopTo(30); break;
                    default: CoopTo(100); NtSay("tool-done"); break;
                }
                break;
            }

            // ---- the Rogue's dagger in a wall: a foothold here too
            case 10:
            {
                if (!NtGot("wall", out var r)) { if (_ntCoopT > 30) NtFail("the friend throws a dagger at a wall"); break; }
                var parts = r.Split(' ');
                _ntCoopAt = new Vector2(Parse(parts[0]), Parse(parts[1])); _ntCoopSide = (int)Parse(parts[2]);
                CoopTo(11);
                break;
            }
            case 11:
            {
                _ntCoopDagger = _world.GetChildren().OfType<ThrownDagger>().FirstOrDefault(d => d.Harmless && d.Thrower == friend && d.State == ThrownDagger.Phase.Stuck && d.InWall);
                if (_ntCoopDagger == null && _ntCoopT < 5f) break;
                float off = _ntCoopDagger?.GlobalPosition.DistanceTo(_ntCoopAt) ?? -1f;
                NtCheck($"the friend's dagger is in the wall here too, where it is there ({off:0.0} px apart)", _ntCoopDagger != null && off < 3f);
                if (_ntCoopDagger == null) { NtSay("wall-stood bad"); CoopTo(13); break; }
                // this game's hero is put over the hilt and comes down on it
                if (!NtOverHilt(_ntCoopDagger.GlobalPosition, _ntCoopSide, out var over)) { NtCheck("(the rock round that hilt leaves room to set a hero down over it)", false); NtSay("wall-stood bad"); CoopTo(13); break; }
                me.GlobalPosition = over; me.Velocity = Vector2.Zero;
                CoopTo(12);
                break;
            }
            case 12:
            {
                if (_ntCoopT < 1.0f) break;
                float rest = me.GlobalPosition.Y - (_ntCoopDagger.GlobalPosition.Y - 14f);
                NtCheck($"this game's hero stands on that hilt ({rest:0.0} px off, on floor {me.IsOnFloor()})", me.IsOnFloor() && Math.Abs(rest) < 9f);
                NtSay("wall-stood ok");
                CoopTo(13);
                break;
            }
            case 13:
                if (!NtGot("wall-recalled", out _)) { if (_ntCoopT > 20) NtFail("the friend recalls the dagger"); break; }
                CoopTo(14);
                break;
            case 14:
            {
                bool freed = _ntCoopDagger == null || !GodotObject.IsInstanceValid(_ntCoopDagger) || !_ntCoopDagger.InWall;
                if (!freed && _ntCoopT < 4f) break;
                NtCheck($"recalled over there, the dagger leaves the wall here too, the foothold gone ({freed})", freed);
                CoopTo(100);
                NtSay("tool-done");
                break;
            }

            // ---- the Elementalist's aimed updraft: the same angle here
            case 20:
            {
                if (!NtGot("draft", out var r)) { if (_ntCoopT > 30) NtFail("the friend raises an updraft"); break; }
                var parts = r.Split(' ');
                float angle = Parse(parts[0]), width = Parse(parts[1]), height = Parse(parts[2]);
                var draft = _world.GetChildren().OfType<Updraft>().Where(u => GodotObject.IsInstanceValid(u)).OrderBy(u => Math.Abs(u.Angle - angle)).FirstOrDefault();
                NtCheck($"the friend's updraft rises here at the angle they aimed ({Mathf.RadToDeg(draft?.Angle ?? 0f):0} degrees of their {Mathf.RadToDeg(angle):0}), {draft?.Width:0} by {draft?.Height:0} px",
                    draft != null && Math.Abs(draft.Angle - angle) < 0.02f && Math.Abs(draft.Width - width) < 0.6f && Math.Abs(draft.Height - height) < 1.5f);
                CoopTo(100);
                NtSay("tool-done");
                break;
            }

            // ---- the Swordsman's heaving swing on a pile of rubble: it comes down here too
            case 30:
            {
                if (!NtGot("rubble", out var r)) { if (_ntCoopT > 30) NtFail("the friend sets a rubble pile"); break; }
                var parts = r.Split(' ');
                _ntCoopRubble = new Rubble { Position = new Vector2(Parse(parts[0]), Parse(parts[1])), Size = new Vector2(24, 48), Index = 900 };
                _world.AddChild(_ntCoopRubble);
                NtSay("rubble-ready");
                CoopTo(31);
                break;
            }
            case 31:
            {
                if (!NtGot("rubble-swung", out var r)) { if (_ntCoopT > 30) NtFail("the friend heaves at the pile"); break; }
                _ntCoopNum = Parse(r);
                CoopTo(32);
                break;
            }
            case 32:
            {
                if (!_ntCoopRubble.Cleared && _ntCoopT < 4f) break;
                NtCheck($"the friend's heaving swing, in one blow, brought the pile down here too (down here: {_ntCoopRubble.Cleared}, {_ntCoopRubble.Left} blows left; theirs: {(_ntCoopNum > 0 ? "down" : "standing")})", _ntCoopRubble.Cleared && _ntCoopNum > 0);
                CoopTo(100);
                NtSay("tool-done");
                break;
            }
        }
    }

    // ------------------------------------------------------------------ the joining game: this hero's tools, used

    private void JoinCoopStep()
    {
        _ntCoopT += _ntDt;
        var me = G.Player;
        switch (_ntCoop)
        {
            case 0:
            {
                if (!NtGot("coop", out _)) { if (_ntCoopT > 40) NtFail("the host begins the co-op checks"); break; }
                // an edge with a drop of a hero's height or two beside it, where the hero really stands (the rope reaching the floor, if there is one)
                _ntEdges = FindEdges(Tune.Rope.Length + 20, 70f)
                    .OrderBy(e => e.depth > Tune.Rope.Length - 5f ? 1 : 0).ThenBy(e => Math.Abs(e.depth - 100f)).Take(12).ToList();
                _ntEdgeAt = 0;
                GD.Print($"[nettest] edges: {_ntEdges.Count} with a drop of 70 px or more (best: {string.Join(", ", _ntEdges.Take(4).Select(e => $"{e.stand.Round()} {e.depth:0}"))}; cave {G.Cave.SizePx}, depth {G.Depth})");
                if (_ntEdges.Count == 0) { NtFail("this cave has an edge to let a rope down over"); break; }
                CoopTo(1);
                break;
            }
            case 1:
            {
                if (_ntEdgeAt >= _ntEdges.Count) { NtFail("a spot at an edge where this hero stands"); break; }
                var e = _ntEdges[_ntEdgeAt];
                me.GlobalPosition = e.stand; me.Velocity = Vector2.Zero;
                _ntCoopSide = e.side;
                CoopTo(2);
                break;
            }
            case 2:
            {
                if (_ntCoopT < 0.8f) break;
                var e = _ntEdges[_ntEdgeAt];
                if (!(me.IsOnFloor() && me.GlobalPosition.DistanceTo(e.stand) < 8f)) { _ntEdgeAt++; CoopTo(1); break; }
                // the button held: the rope pays out (the hero can't move while it does)
                _ntInput = new PlayerInput { Rope = true, RopeHeld = true, Move = new Vector2(_ntCoopSide, 0) };
                CoopTo(3);
                break;
            }
            case 3:
            {
                _ntCoopRope = Rope.All.FirstOrDefault(x => GodotObject.IsInstanceValid(x) && x.Holder == me);
                if (_ntCoopRope == null) { if (_ntCoopT > 2f) { _ntEdgeAt++; _ntInput = default; CoopTo(1); } break; }
                _ntInput = new PlayerInput { RopeHeld = true, Move = new Vector2(_ntCoopSide, 0) };
                // (it stops of itself on the ground, or after a while, here)
                if (_ntCoopRope.State == Rope.Phase.Hanging || _ntCoopT > 3.5f)
                {
                    _ntInput = default;
                    CoopTo(4);
                }
                break;
            }
            case 4:
            {
                if (_ntCoopT < 0.3f) break;
                NtCheck($"a press lets a rope down and releasing stops it ({_ntCoopRope?.State}, {_ntCoopRope?.Unrolled:0} px)", _ntCoopRope != null && _ntCoopRope.State == Rope.Phase.Hanging);
                if (_ntCoopRope == null || _ntCoopRope.State != Rope.Phase.Hanging) { NtFail("the rope hangs for the others"); break; }
                NtSay($"rope-down {Num(_ntCoopRope.Unrolled)}");
                CoopTo(5);
                break;
            }
            case 5:
                // (the host's hero climbs it, in the host's game)
                if (!NtGot("rope-climbed", out _)) { if (_ntCoopT > 30) NtFail("the host climbs the rope"); break; }
                _ntInput = new PlayerInput { Rope = true };
                CoopTo(6);
                break;
            case 6:
            {
                // (the second press reels it in)
                bool gone = _ntCoopRope == null || !GodotObject.IsInstanceValid(_ntCoopRope);
                if (!gone && _ntCoopT < 6f) break;
                NtCheck($"a second press reels it in, and it is gone ({gone}, holding {me.HoldsRope})", gone && !me.HoldsRope);
                NtSay(gone ? "rope-gone yes" : "rope-gone no");
                _ntCoopNum = 60f;
                switch (me.Stats.Hero)
                {
                    case HeroKind.Rogue: CoopTo(10); break;
                    case HeroKind.Elementalist: CoopTo(20); break;
                    case HeroKind.Swordsman: CoopTo(30); break;
                    default: CoopTo(90); break;
                }
                break;
            }

            // ---- a dagger into a wall
            case 10:
            {
                if (!NtRun(70, true, out var stand, out _ntCoopSide, _ntCoopNum)) { NtFail("a floor with a wall to throw at (with room by its hilt)"); break; }
                _ntCoopNum = stand.X + 200f; // (the next try starts beyond this one)
                me.GlobalPosition = stand; me.Velocity = Vector2.Zero; me.Facing = _ntCoopSide;
                CoopTo(11);
                break;
            }
            case 11:
                if (_ntCoopT < 0.8f) break;
                _ntInput = new PlayerInput { Ability = true, Aim = new Vector2(_ntCoopSide, 0), AimGiven = true };
                CoopTo(12);
                break;
            case 12:
            {
                if (_ntCoopT < 0.9f) break;
                _ntCoopDagger = me.ThrownDaggerAt(1) ?? me.ThrownDaggerAt(0);
                bool inWall = _ntCoopDagger != null && _ntCoopDagger.State == ThrownDagger.Phase.Stuck && _ntCoopDagger.InWall;
                NtCheck($"a dagger thrown at a wall goes into it and stays ({_ntCoopDagger?.State}, in the wall: {_ntCoopDagger?.InWall})", inWall);
                // (a wall the dagger glanced off, or none went in: recall, and try the next wall along)
                if (!inWall) { if (++_ntCoopTries > 6) { NtFail("the dagger sticks in a wall"); break; } _ntInput = new PlayerInput { Ability2 = true }; CoopTo(15); break; }
                var at = _ntCoopDagger.GlobalPosition;
                // (a wall whose rock leaves no room to set a hero down over the hilt is not one to test with: recall, and try the next)
                if (!NtOverHilt(at, _ntCoopSide, out _)) { _ntInput = new PlayerInput { Ability2 = true }; CoopTo(15); break; }
                NtSay($"wall {Num(at.X)} {Num(at.Y)} {_ntCoopSide}");
                CoopTo(13);
                break;
            }
            case 15:
                if (_ntCoopT < 1.5f) break;
                CoopTo(10);
                break;
            case 13:
                if (!NtGot("wall-stood", out _)) { if (_ntCoopT > 20) NtFail("the host stands on the hilt"); break; }
                _ntInput = new PlayerInput { Ability2 = true };
                CoopTo(14);
                break;
            case 14:
            {
                if (_ntCoopT < 1.2f) break;
                NtCheck($"recalled, the dagger is back in hand ({me.DaggersInHand} of 2)", me.DaggersInHand == 2);
                NtSay("wall-recalled");
                CoopTo(90);
                break;
            }

            // ---- an aimed updraft
            case 20:
            {
                me.SetAlimus(me.Stats.AlimusMax);
                // (the button held aims; the aim is up and to the right, 45 degrees)
                _ntInput = new PlayerInput { Dodge = true, GuardHeld = true, Aim = new Vector2(1, -1).Normalized(), AimGiven = true };
                CoopTo(21);
                break;
            }
            case 21:
                if (_ntCoopT < 0.8f) break;
                _ntInput = default; // (let go: it rises at the angle it was left at)
                CoopTo(22);
                break;
            case 22:
            {
                if (_ntCoopT < 0.6f) break;
                var u = me.LastUpdraft;
                bool ok = u != null && GodotObject.IsInstanceValid(u) && Math.Abs(u.Angle - Mathf.Pi / 4f) < 0.05f;
                NtCheck($"an updraft let go at 45 degrees rises at {Mathf.RadToDeg(u?.Angle ?? 0f):0} degrees", ok);
                if (!ok) { NtFail("the updraft rises at the aimed angle"); break; }
                NtSay($"draft {Num(u.Angle)} {Num(u.Width)} {Num(u.Height)}");
                CoopTo(90);
                break;
            }

            // ---- a heaving swing on a pile of rubble
            case 30:
            {
                if (!NtRun(90, false, out var stand, out _ntCoopSide)) { NtFail("a floor to heave on"); break; }
                me.GlobalPosition = stand; me.Velocity = Vector2.Zero; me.Facing = _ntCoopSide;
                _ntCoopAt = stand + new Vector2(_ntCoopSide * 34, -18);
                _ntCoopRubble = new Rubble { Position = _ntCoopAt, Size = new Vector2(24, 48), Index = 900 };
                _world.AddChild(_ntCoopRubble);
                NtSay($"rubble {Num(_ntCoopAt.X)} {Num(_ntCoopAt.Y)}");
                CoopTo(31);
                break;
            }
            case 31:
                if (!NtGot("rubble-ready", out _)) { if (_ntCoopT > 20) NtFail("the host sets the pile"); break; }
                CoopTo(32);
                break;
            case 32:
                if (_ntCoopT < 0.8f) break;
                _ntInput = new PlayerInput { Ability2 = true, Aim = new Vector2(_ntCoopSide, 0), AimGiven = true };
                CoopTo(33);
                break;
            case 33:
            {
                if (_ntCoopT < 1.4f) break;
                NtCheck($"one heaving swing brings the pile down ({_ntCoopRubble.Cleared})", _ntCoopRubble.Cleared);
                NtSay($"rubble-swung {(_ntCoopRubble.Cleared ? 1 : 0)}");
                CoopTo(90);
                break;
            }

            // ---- done: the host says so once it has seen it
            case 90:
                if (!NtGot("tool-done", out _)) { if (_ntCoopT > 30) NtFail("the host finishes the co-op checks"); break; }
                CoopTo(100);
                break;
        }
    }
}
