using System;
using System.Collections.Generic;
using Godot;

namespace DaggerCave;

/// <summary>
/// A rope a hero lets down for the others. Holding the rope button pays it out: the coil drops from the hero's hands and unrolls as it
/// falls, so the wound part is always at the bottom; letting go of the button (or running out of rope, about five times a hero's height)
/// stops it, the coil becomes the rope's end and it swings. Then anyone can climb it, and a second press reels it back in. The holder can't
/// move while it is out.
///
/// The rope is a chain of points (verlet), hung from the holder's hands and resting on the rock it falls against. A climber hangs their
/// weight on it: the point they hold is many times heavier than the rope, so a grab sets the rope swinging with the climber's own way of
/// going, and pushing left or right pumps the swing. Every game keeps a copy, and each copy carries only that game's own hero.
/// </summary>
public partial class Rope : Node2D
{
    public static readonly List<Rope> All = new();

    public enum Phase { Lowering, Hanging, Reeling, Dropped }

    /// <summary>Who holds it (null: a rope tied off by itself, e.g. a test's, which hangs at full length for <see cref="Life"/> seconds).</summary>
    public Player Holder;
    /// <summary>The rope it carries in all (px), and for a tied-off one how long it lasts.</summary>
    public float Length = Tune.Rope.Length, Life = Tune.Rope.Seconds;
    public Phase State { get; private set; } = Phase.Lowering;
    public float Age => _t;
    /// <summary>Paid out so far (px).</summary>
    public float Unrolled { get; private set; }
    /// <summary>Still wound in the coil at the end (px); the coil fades into the rope's end once it stops.</summary>
    public float CoilLeft => State == Phase.Lowering ? Math.Max(0f, Length - Unrolled) : 0f;
    /// <summary>0..1, how much of the coil is still drawn (it shrinks to the end's knot once the rope stops).</summary>
    public float CoilShow { get; private set; } = 1f;
    /// <summary>How much of it is there (a dropped or tied-off rope fades away at the end).</summary>
    public float Strength => Holder == null || State == Phase.Dropped ? Math.Clamp(Life / 0.8f, 0f, 1f) : 1f;
    /// <summary>Climbable: lowered and stopped (a hanging rope), or one tied off.</summary>
    public bool Climbable => State == Phase.Hanging || State == Phase.Reeling;

    /// <summary>The chain: points from the anchor (index 0, at the holder's hands) down to the end, and where each was a step ago.</summary>
    public readonly List<Vector2> P = new(), Prev = new();
    private readonly List<float> _rest = new();
    private float _t;

    // the one hero of this game who is on it (each game carries only its own): where along it (px from the anchor) and how hard they pump
    private Player _climber;
    private float _climbS, _pump;

    private const float Seg = Tune.Rope.Segment;

    public override void _EnterTree() { if (!All.Contains(this)) All.Add(this); }
    public override void _ExitTree() => All.Remove(this);

    public override void _Ready()
    {
        ZIndex = 3;
        P.Add(GlobalPosition); Prev.Add(GlobalPosition);
        if (Holder == null)
        {
            // tied off: it hangs at full length (down to the ground, if that is nearer), straight away
            float h = 0;
            while (h < Length && !G.Cave.IsSolid(GlobalPosition + new Vector2(0, h + 4f))) h += 4f;
            Length = Math.Max(16f, h);
            for (float y = Seg; ; y += Seg)
            {
                float yy = Math.Min(y, Length);
                var q = GlobalPosition + new Vector2(0, yy);
                P.Add(q); Prev.Add(q); _rest.Add(yy - (y - Seg));
                if (yy >= Length) break;
            }
            Unrolled = Length;
            State = Phase.Hanging;
            CoilShow = 0f;
        }
        else
        {
            // the coil, in the hands: one short segment to begin with
            var q = GlobalPosition + new Vector2(0, 2f);
            P.Add(q); Prev.Add(q); _rest.Add(2f);
            Unrolled = 2f;
        }
    }

    // ------------------------------------------------------------------ the holder's commands

    /// <summary>The button is let go (or the rope has run out): it pays out no more, the coil becomes the end, and it swings.</summary>
    public void Stop(float length = -1f)
    {
        if (State != Phase.Lowering) return;
        if (length > 0f) TrimTo(length);
        State = Phase.Hanging;
        // the coil drops open into the end: a little kick sideways sets it swinging
        int e = P.Count - 1;
        float kick = (GD.Randf() < 0.5f ? -1f : 1f) * 26f / 60f;
        Prev[e] = Prev[e] - new Vector2(kick, 0f);
        G.Sfx.Play("rope", P[e], -10, 0.1f, 1.1f);
        NetSync.RopeState(this);
    }

    /// <summary>The second press: it is reeled back in (anyone still on it rides up with it).</summary>
    public void Reel()
    {
        if (State != Phase.Hanging) return;
        State = Phase.Reeling;
        G.Sfx.Play("rope", GlobalPosition, -10, 0.1f, 0.8f);
        NetSync.RopeState(this);
    }

    /// <summary>The holder lost hold of it (hurt off their feet, dead, in the water): it falls and is gone.</summary>
    public void Drop()
    {
        if (State == Phase.Dropped) return;
        State = Phase.Dropped;
        Life = 1.2f;
        if (_climber != null && IsInstanceValid(_climber)) _climber.LetGoOfRope();
        _climber = null;
        NetSync.RopeState(this);
    }

    /// <summary>A friend's rope changed in their game (this copy follows).</summary>
    public void NetState(Phase phase, float length)
    {
        switch (phase)
        {
            case Phase.Hanging: Stop(length); break;
            case Phase.Reeling: if (State == Phase.Lowering) Stop(length); Reel(); break;
            case Phase.Dropped: Drop(); break;
        }
    }

    private void TrimTo(float length)
    {
        while (Unrolled > length + 0.01f && _rest.Count > 1)
        {
            float last = _rest[^1];
            if (Unrolled - last >= length) { Unrolled -= last; _rest.RemoveAt(_rest.Count - 1); P.RemoveAt(P.Count - 1); Prev.RemoveAt(Prev.Count - 1); }
            else { _rest[^1] = last - (Unrolled - length); Unrolled = length; }
        }
    }

    // ------------------------------------------------------------------ climbing

    /// <summary>Where along the rope (px from the anchor) a point lies, and how far from it: the nearest place on the chain.</summary>
    public float Nearest(Vector2 p, out float dist)
    {
        float best = float.MaxValue, bestS = 0f, s = 0f;
        for (int i = 0; i + 1 < P.Count; i++)
        {
            var a = P[i]; var b = P[i + 1]; var ab = b - a;
            float l2 = ab.LengthSquared();
            float t = l2 > 1e-4f ? Math.Clamp((p - a).Dot(ab) / l2, 0f, 1f) : 0f;
            float d = p.DistanceTo(a + ab * t);
            if (d < best) { best = d; bestS = s + _rest[i] * t; }
            s += _rest[i];
        }
        dist = best;
        return bestS;
    }

    /// <summary>The point on the rope <paramref name="s"/> px from the anchor.</summary>
    public Vector2 PointAt(float s)
    {
        float acc = 0f;
        for (int i = 0; i + 1 < P.Count; i++)
        {
            float r = _rest[i];
            if (s <= acc + r || i + 2 == P.Count) return P[i].Lerp(P[i + 1], r > 0.01f ? Math.Clamp((s - acc) / r, 0f, 1f) : 1f);
            acc += r;
        }
        return P[^1];
    }

    /// <summary>How fast the rope moves at <paramref name="s"/> (px/s).</summary>
    public Vector2 VelocityAt(float s, float dt)
    {
        float acc = 0f;
        for (int i = 0; i + 1 < P.Count; i++)
        {
            float r = _rest[i];
            if (s <= acc + r || i + 2 == P.Count)
            {
                float t = r > 0.01f ? Math.Clamp((s - acc) / r, 0f, 1f) : 1f;
                var v0 = P[i] - Prev[i]; var v1 = P[i + 1] - Prev[i + 1];
                return v0.Lerp(v1, t) / Math.Max(1e-3f, dt);
            }
            acc += r;
        }
        return Vector2.Zero;
    }

    /// <summary>Whether a hero whose middle is at <paramref name="p"/> could take hold of it.</summary>
    public bool Holds(Vector2 p)
    {
        if (!Climbable || Strength <= 0.2f || P.Count < 2) return false;
        float s = Nearest(p, out float d);
        return d < Tune.Rope.GrabWidth && s > 4f;
    }

    public static Rope At(Vector2 p)
    {
        Rope best = null; float bestD = float.MaxValue;
        foreach (var r in All)
        {
            if (!IsInstanceValid(r) || !r.Holds(p)) continue;
            r.Nearest(p, out float d);
            if (d < bestD) { bestD = d; best = r; }
        }
        return best;
    }

    /// <summary>A hero takes hold <paramref name="s"/> px down it, moving at <paramref name="vel"/>: the rope there is set going their way.</summary>
    public void Grab(Player p, float s, Vector2 vel, float dt)
    {
        _climber = p;
        _climbS = Math.Clamp(s, 4f, Unrolled);
        int k = PointIndexAt(_climbS);
        // (the hero's own momentum, given to the rope where they hold it: from here it swings the way they were going)
        var give = vel * dt;
        for (int i = Math.Max(1, k - 1); i <= Math.Min(P.Count - 1, k + 1); i++) Prev[i] = P[i] - give;
        G.Sfx.Play("rope", PointAt(_climbS), -12, 0.1f, 1.3f);
    }

    /// <summary>This game's hero, while on it: where along it they are, and which way they push.</summary>
    public void Hold(Player p, float s, float pump)
    {
        _climber = p;
        _climbS = Math.Clamp(s, 2f, Unrolled);
        _pump = Math.Clamp(pump, -1f, 1f);
    }

    public void LetGo(Player p) { if (_climber == p) { _climber = null; _pump = 0f; } }

    public float ClimbS => _climbS;

    private int PointIndexAt(float s)
    {
        float acc = 0f;
        for (int i = 0; i < _rest.Count; i++)
        {
            if (s <= acc + _rest[i] * 0.5f) return i;
            acc += _rest[i];
        }
        return P.Count - 1;
    }

    // ------------------------------------------------------------------ the physics

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        _t += dt;
        // the anchor rides in the holder's hands
        if (Holder != null && IsInstanceValid(Holder) && State != Phase.Dropped)
        {
            GlobalPosition = Holder.RopeHands;
            if (Holder.Dead || !Holder.HoldingRope(this)) Drop();
        }
        else if (Holder != null && State != Phase.Dropped) Drop();

        if (Holder == null || State == Phase.Dropped)
        {
            Life -= dt;
            if (Life <= 0) { if (_climber != null && IsInstanceValid(_climber)) _climber.LetGoOfRope(); QueueFree(); return; }
        }
        if (_climber != null && (!IsInstanceValid(_climber) || !_climber.OnRopeOf(this))) _climber = null;

        if (State == Phase.Lowering) PayOut(dt);
        else if (State == Phase.Reeling && ReelIn(dt)) return;
        CoilShow = State == Phase.Lowering ? 1f : Math.Max(0f, CoilShow - dt * 5f);

        const int sub = 2;
        float h = dt / sub;
        for (int k = 0; k < sub; k++) Step(h);
    }

    /// <summary>
    /// Lowering: the coil falls (a little slower than a stone: the rope paying out holds it back) and the rope comes off it as it goes, so
    /// what is paid out is what the coil's fall has pulled out. Out of rope, or the coil come to rest on the ground, it stops of itself.
    /// </summary>
    private void PayOut(float dt)
    {
        int e = P.Count - 1;
        var last = P[e - 1];
        float gap = P[e].DistanceTo(last);
        // the coil's fall pulls rope out: the last stretch takes up what it has fallen, a segment at a time
        float want = Math.Min(gap, _rest[^1] + Tune.Rope.PayOutSpeed * dt);
        float grow = Math.Max(0f, want - _rest[^1]);
        grow = Math.Min(grow, Length - Unrolled);
        _rest[^1] += grow;
        Unrolled += grow;
        while (_rest[^1] > Seg)
        {
            // a new point comes off the coil just above it
            float extra = _rest[^1] - Seg;
            var dir = (P[e] - P[e - 1]).Normalized();
            if (dir.LengthSquared() < 0.5f) dir = Vector2.Down;
            var at = P[e - 1] + dir * Seg;
            var coilStep = P[e] - Prev[e];
            P.Insert(e, at); Prev.Insert(e, at - coilStep);
            _rest[^1] = Seg;
            _rest.Add(extra);
            e = P.Count - 1;
        }
        // (the coil sits on the ground: nothing more comes off; or there is none left)
        bool resting = G.Cave.IsSolid(P[e] + new Vector2(0, 5f)) && (P[e] - Prev[e]).Length() < 0.4f && _t > 0.35f;
        if (Unrolled >= Length - 0.5f || resting)
        {
            if (Holder != null && !Holder.IsRemote) Stop();
            else if (Holder == null) Stop();
        }
    }

    /// <summary>Reeling in: rope comes off the top, at the hands; true once it is all in (and the holder is free).</summary>
    private bool ReelIn(float dt)
    {
        float take = Tune.Rope.ReelSpeed * dt;
        while (take > 0f && _rest.Count > 0)
        {
            float r = _rest[0];
            if (r > take) { _rest[0] = r - take; Unrolled -= take; if (_climber != null) _climbS = Math.Max(2f, _climbS - take); take = 0f; }
            else
            {
                take -= r; Unrolled -= r; if (_climber != null) _climbS = Math.Max(2f, _climbS - r);
                _rest.RemoveAt(0);
                P.RemoveAt(1); Prev.RemoveAt(1);
            }
        }
        if (_rest.Count == 0 || Unrolled <= 1f)
        {
            if (_climber != null && IsInstanceValid(_climber)) _climber.LetGoOfRope();
            Holder?.RopeReeledIn(this);
            QueueFree();
            return true;
        }
        return false;
    }

    private void Step(float h)
    {
        var cave = G.Cave;
        int n = P.Count;
        if (n < 2) return;
        var grav = new Vector2(0, Tune.Rope.Gravity);
        int ck = _climber != null ? PointIndexAt(_climbS) : -1;
        // the masses: the rope's points weigh 1; the coil the rope still wound in it; the point a climber holds, the climber
        Span<float> inv = n <= 64 ? stackalloc float[n] : new float[n];
        for (int i = 0; i < n; i++) inv[i] = 1f;
        inv[0] = 0f;
        if (State == Phase.Lowering) inv[n - 1] = 1f / (1f + CoilLeft / Seg * 0.6f);
        if (ck > 0) inv[ck] = 1f / Tune.Rope.ClimberMass;

        for (int i = 1; i < n; i++)
        {
            var p = P[i];
            var vel = (p - Prev[i]) * Tune.Rope.Damping;
            var acc = grav;
            if (i == ck) acc += new Vector2(_pump * Tune.Rope.PumpAccel, 0f);
            // (the coil falls a touch slower than a stone: the rope coming off it holds it back)
            if (State == Phase.Lowering && i == n - 1 && vel.Y > Tune.Rope.CoilFallMax * h) vel.Y = Tune.Rope.CoilFallMax * h;
            Prev[i] = p;
            P[i] = p + vel + acc * h * h;
        }
        P[0] = GlobalPosition; Prev[0] = GlobalPosition;

        for (int it = 0; it < 14; it++)
        {
            for (int i = 0; i + 1 < n; i++)
            {
                var a = P[i]; var b = P[i + 1];
                var d = b - a;
                float len = d.Length();
                if (len < 1e-4f) continue;
                float wa = inv[i], wb = inv[i + 1], w = wa + wb;
                if (w <= 0f) continue;
                float diff = (len - _rest[i]) / len;
                // (a rope pulls but doesn't push: slack is let be)
                if (diff < 0f) continue;
                var corr = d * diff;
                P[i] = a + corr * (wa / w);
                P[i + 1] = b - corr * (wb / w);
            }
            P[0] = GlobalPosition;
        }

        // it lies on the rock it falls against (pushed back out to the open side, and held by friction)
        for (int i = 1; i < n; i++)
        {
            if (!cave.IsSolid(P[i])) continue;
            var nrm = cave.OpenGradient(P[i]);
            if (nrm.LengthSquared() < 1e-4f) nrm = Vector2.Up;
            nrm = nrm.Normalized();
            var q = P[i];
            for (int k = 0; k < 10 && cave.IsSolid(q); k++) q += nrm * 1.5f;
            P[i] = q;
            Prev[i] = Prev[i].Lerp(q, 0.6f);
        }
    }
}
