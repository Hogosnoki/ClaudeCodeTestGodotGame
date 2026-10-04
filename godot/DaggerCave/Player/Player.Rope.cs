using System;
using Godot;

namespace DaggerCave;

/// <summary>Ropes: any hero in a party can lower one (a plain press of the rope button), and any hero can climb one.</summary>
public partial class Player
{
    /// <summary>For tests: allow ropes without a party.</summary>
    public static bool RopesAlone;
    private float _ropeCd;
    private Rope _rope;

    public static bool RopesAllowed => RopesAlone || (Net.Online && Net.Count > 1);
    public bool OnRope => _rope != null;
    public float RopeCooldownFrac => Math.Clamp(_ropeCd / Tune.Rope.Cooldown, 0f, 1f);

    private void TickRope(PlayerInput inp, float dt)
    {
        if (_ropeCd > 0) _ropeCd -= dt;
        if (inp.Rope && !InWater) TryRope();
        if (_rope != null && (!IsInstanceValid(_rope) || _rope.Life <= 0 || InWater || _dodgeT > 0 || _frozenT > 0)) _rope = null;
    }

    /// <summary>Lowers a rope from where you stand (over the nearest edge if you stand on the ground).</summary>
    public bool TryRope()
    {
        if (!RopesAllowed || _ropeCd > 0 || Dead) return false;
        // the column (this one, or a little to either side, the way you face first) that falls farthest
        float bestLen = 0, bestX = 0;
        foreach (float dx in new[] { 0f, 14f * Facing, -14f * Facing, 28f * Facing, -28f * Facing, 42f * Facing, -42f * Facing })
        {
            var at = GlobalPosition + new Vector2(dx, -8f);
            if (G.Cave.IsSolid(at)) continue;
            float h = 0;
            while (h < Tune.Rope.Length && !G.Cave.IsSolid(at + new Vector2(0, h + 4f))) h += 4f;
            if (h > bestLen + 6f) { bestLen = h; bestX = dx; }
        }
        if (bestLen < Tune.Rope.MinLength) return false;
        _ropeCd = Tune.Rope.Cooldown;
        var rope = new Rope { Position = GlobalPosition + new Vector2(bestX, -8f) };
        G.Spawn(rope);
        NetSync.HeroVisual(rope);
        G.Sfx.Play("jump", GlobalPosition, -8, 0.05f, 0.7f);
        return true;
    }

    /// <summary>The rope that would take you, if you pushed up now.</summary>
    private bool WantsRope(PlayerInput inp) => Rope.All.Count > 0 && inp.Move.Y < -0.5f && Math.Abs(inp.Move.X) < 0.6f && !InWater && Rope.At(GlobalPosition) != null;

    private bool GrabRope() { _rope = Rope.At(GlobalPosition); return _rope != null; }

    private Vector2 RopeMotion(PlayerInput inp, Vector2 v, float dt)
    {
        var r = _rope;
        // hold to the rope: slide onto it, climb and descend at a steady pace
        float x = Mathf.MoveToward(GlobalPosition.X, r.GlobalPosition.X, 220f * dt);
        float climb = -inp.Move.Y * Tune.Rope.ClimbSpeed;
        float topY = r.GlobalPosition.Y + 2f, botY = r.GlobalPosition.Y + r.Unrolled;
        float nextY = GlobalPosition.Y + climb * dt;
        if (nextY < topY - 6f)
        {
            // over the top: a hop onto whatever the rope hangs from
            _rope = null;
            return new Vector2(Facing * 90f, -BaseJumpV * 0.75f);
        }
        if (nextY > botY) climb = Math.Max(climb, 0f);
        if (_jumpBuffer > 0)
        {
            _jumpBuffer = 0; _rope = null; _jumpCutDone = false;
            float side = Math.Abs(inp.Move.X) > 0.3f ? Math.Sign(inp.Move.X) : -Facing;
            if (Math.Abs(inp.Move.X) > 0.3f) Facing = side;
            G.Sfx.Play("jump", GlobalPosition, -8);
            return new Vector2(side * Tune.Hero.WallJumpPush * 0.8f, -BaseJumpV * 0.9f);
        }
        if (IsOnFloor() && climb < 0) { _rope = null; return v; }
        _coyote = Tune.Hero.CoyoteTime;
        _airJumps = Stats.DoubleJump ? 1 : 0;
        return new Vector2((x - GlobalPosition.X) / dt, -climb);
    }
}
