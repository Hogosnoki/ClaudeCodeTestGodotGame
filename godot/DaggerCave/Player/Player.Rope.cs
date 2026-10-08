using System;
using Godot;

namespace DaggerCave;

/// <summary>
/// Ropes: every hero carries one. Hold the rope button to let it down (over the nearest edge; you can't move while you hold it), let go of
/// the button to stop it there, and press again to reel it back in. Anyone can climb a rope that hangs: push up beside it to take hold,
/// up and down to climb, left and right to swing, jump to let go.
/// </summary>
public partial class Player
{
    /// <summary>Tests: the old switch (ropes need no party now).</summary>
    public static bool RopesAlone;
    private float _ropeCd;
    /// <summary>The rope this hero is on (climbing), and the one it holds (lowered for the others).</summary>
    private Rope _rope, _heldRope;
    private float _ropeS;

    public static bool RopesAllowed => true;
    public bool OnRope => _rope != null;
    public bool HoldsRope => _heldRope != null;
    public float RopeCooldownFrac => Math.Clamp(_ropeCd / Tune.Rope.Cooldown, 0f, 1f);

    /// <summary>Where the rope comes from: the hands, a little out over the side the hero faces.</summary>
    public Vector2 RopeHands => GlobalPosition + new Vector2(Facing * 7f, -3f);
    public bool HoldingRope(Rope r) => _heldRope == r;
    public bool OnRopeOf(Rope r) => _rope == r;
    public void LetGoOfRope() { _rope?.LetGo(this); _rope = null; }
    public void RopeReeledIn(Rope r) { if (_heldRope == r) _heldRope = null; _ropeCd = Tune.Rope.Cooldown; }

    private void TickRope(PlayerInput inp, float dt)
    {
        if (_ropeCd > 0) _ropeCd -= dt;
        if (_heldRope != null && !IsInstanceValid(_heldRope)) _heldRope = null;
        if (_heldRope != null)
        {
            // knocked off your feet, in the water, frozen or dead: the rope goes
            if (Dead || InWater || _frozenT > 0 || (!IsOnFloor() && Velocity.Y > 60f)) { _heldRope.Drop(); _heldRope = null; _ropeCd = Tune.Rope.Cooldown; }
            else if (_heldRope.State == Rope.Phase.Lowering && !inp.RopeHeld) _heldRope.Stop();
            else if (_heldRope.State == Rope.Phase.Hanging && inp.Rope) _heldRope.Reel();
        }
        else if (inp.Rope && !InWater && _rope == null) TryRope();
        if (_rope != null && (!IsInstanceValid(_rope) || !_rope.Climbable || _rope.Strength <= 0.05f || InWater || _dodgeT > 0 || _frozenT > 0)) LetGoOfRope();
    }

    /// <summary>Starts letting a rope down, facing the drop nearest you (true once it is going).</summary>
    public bool TryRope()
    {
        if (System.Environment.GetEnvironmentVariable("ROPE_DEBUG") != null) GD.Print($"[rope] try: cd {_ropeCd:0.00} dead {Dead} held {_heldRope != null} floor {IsOnFloor()} at {GlobalPosition.Round()} facing {Facing}");
        if (!RopesAllowed || _ropeCd > 0 || Dead || _heldRope != null || !IsOnFloor()) return false;
        // the side with a drop just by you (the way you face first): the rope goes over it
        // (the column within reach, the way you face first, that drops farthest: the coil is let go of over it)
        int side = 0; Vector2 over = default; float best = 0f;
        foreach (int s in new[] { (int)Facing, -(int)Facing })
        {
            for (float dx = 8f; dx <= 34f; dx += 3f)
            {
                var at = GlobalPosition + new Vector2(s * dx, 4f);
                // (a wall in the way: no further that side; rising ground: look past it)
                if (G.Cave.IsSolid(at + new Vector2(0, -12f))) break;
                if (G.Cave.IsSolid(at)) continue;
                float h = 0;
                // (down to the rock, or to a breakable slab: the coil would land on either)
                while (h < Tune.Rope.Length && !G.Cave.IsSolid(at + new Vector2(0, h + 4f)) && !Rope.OnSlab(at + new Vector2(0, h + 4f))) h += 4f;
                if (h >= Tune.Rope.MinLength && h > best + 8f) { best = h; side = s; over = at + new Vector2(s * 2f, 0f); }
            }
            if (side != 0) break;
        }
        if (side == 0) { if (System.Environment.GetEnvironmentVariable("ROPE_DEBUG") != null) GD.Print("[rope] no edge"); SayNo("NO EDGE TO LOWER IT OVER"); return false; }
        Facing = side;
        Anim.Face(side, instant: true);
        var rope = new Rope { Position = RopeHands, Holder = this, DropAt = over };
        _heldRope = rope;
        G.Spawn(rope);
        NetSync.HeroVisual(rope);
        G.Sfx.Play("rope", GlobalPosition, -8, 0.05f, 0.9f);
        return true;
    }

    /// <summary>A friend's game let a rope down: this copy is held by their hero here.</summary>
    public void NetHoldRope(Rope r) => _heldRope = r;

    /// <summary>The rope that would take you, if you pushed up now.</summary>
    private bool WantsRope(PlayerInput inp) => Rope.All.Count > 0 && inp.Move.Y < -0.5f && Math.Abs(inp.Move.X) < 0.6f && !InWater && _heldRope == null && Rope.At(GlobalPosition) != null;

    private bool GrabRope()
    {
        var r = Rope.At(GlobalPosition);
        if (r == null || r.Holder == this) return false;
        _rope = r;
        _ropeS = r.Nearest(GlobalPosition + new Vector2(0, -8f), out _);
        r.Grab(this, _ropeS, Velocity, (float)GetPhysicsProcessDeltaTime());
        return true;
    }

    /// <summary>
    /// On a rope: the hands hold a point of it, the body hangs below them, and the hero goes wherever that point goes; the rope carries
    /// the hero's weight (see <see cref="Rope"/>), so a swing is the hero's own swing. Up and down climb, left and right pump it, jump lets go
    /// with the swing's speed, and at the top a hop takes you over onto whatever the rope hangs from.
    /// </summary>
    private Vector2 RopeMotion(PlayerInput inp, Vector2 v, float dt)
    {
        var r = _rope;
        float climb = -inp.Move.Y * Tune.Rope.ClimbSpeed;
        _ropeS = Math.Clamp(_ropeS - climb * dt, 0f, r.Unrolled);
        if (_ropeS < 3f && climb > 0f)
        {
            // over the top: a hop onto the ledge the rope comes over (toward whoever holds it)
            float side = r.Holder != null && IsInstanceValid(r.Holder) ? Math.Sign(r.Holder.GlobalPosition.X - GlobalPosition.X) : Facing;
            if (side == 0) side = Facing;
            Facing = side;
            LetGoOfRope();
            return new Vector2(side * 110f, -BaseJumpV * 0.8f);
        }
        if (_jumpBuffer > 0)
        {
            // let go, with the swing's own speed and a kick off the rope
            _jumpBuffer = 0; _jumpCutDone = false;
            var swing = r.VelocityAt(_ropeS, dt);
            float side = Math.Abs(inp.Move.X) > 0.3f ? Math.Sign(inp.Move.X) : Math.Abs(swing.X) > 20f ? Math.Sign(swing.X) : -Facing;
            if (Math.Abs(inp.Move.X) > 0.3f) Facing = side;
            LetGoOfRope();
            G.Sfx.Play("jump", GlobalPosition, -8);
            return new Vector2(swing.X + side * Tune.Hero.WallJumpPush * 0.5f, Math.Min(swing.Y, 0f) - BaseJumpV * 0.75f);
        }
        if (IsOnFloor() && climb < 0 && _ropeS >= r.Unrolled - 1f) { LetGoOfRope(); return v; }
        r.Hold(this, _ropeS, inp.Move.X);
        _coyote = Tune.Hero.CoyoteTime;
        _airJumps = Stats.DoubleJump ? 1 : 0;
        if (Math.Abs(inp.Move.X) > 0.3f) Facing = Math.Sign(inp.Move.X);
        // the hands on the rope, the body hanging below them
        var want = r.PointAt(_ropeS) + new Vector2(0, 8f);
        return (want - GlobalPosition) / Math.Max(dt, 1e-3f);
    }
}
