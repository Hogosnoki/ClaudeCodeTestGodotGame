using System;
using System.Collections.Generic;
using Godot;

namespace DaggerCave;

/// <summary>
/// The wards that shut a trial room (the Gauntlet): a block of violet light over every open cell on the room's rim. No creature and no shot
/// passes them either way until the trial is won. A hero is let in, never out: one still outside passes through them (a friend joining
/// late), and once a step inside the room the wards hold them too. They rise when the trial begins and fade when it ends. Every game builds
/// them from the same list of cells, and lets its own heroes through.
/// </summary>
public partial class TrialSeal : StaticBody2D
{
    public static readonly List<TrialSeal> All = new();
    public int RoomIndex;
    public readonly List<Vector2I> Cells = new();
    private float _t, _fade = -1f;
    public bool Opening => _fade >= 0f;
    /// <summary>The open cell the room is grown from; the room's cells are flooded from it, up to the wards (<see cref="Inside"/>).</summary>
    public Vector2I? Seed;
    public readonly HashSet<int> Inside = new();
    private readonly HashSet<int> _rim = new();
    /// <summary>Heroes outside, who may pass in (a collision exception each), and how many have come in through the wards since.</summary>
    private readonly HashSet<Player> _let = new();
    public int Joined { get; private set; }
    /// <summary>How far up the wards stand: rising over a quarter second, fading over the last.</summary>
    public float Shown => Math.Min(1f, _t / 0.25f) * (Opening ? Math.Max(0f, 1f - _fade / 0.6f) : 1f);

    public override void _Ready()
    {
        CollisionLayer = G.LayerTerrain;
        CollisionMask = 0;
        ZIndex = 4;
        foreach (var c in Cells)
            AddChild(new CollisionShape2D { Position = new Vector2(c.X + 0.5f, c.Y + 0.5f) * CaveData.Cell, Shape = new RectangleShape2D { Size = new Vector2(CaveData.Cell, CaveData.Cell) } });
        FloodInside();
    }

    /// <summary>The room's own cells: every open cell reached from the seed without crossing a ward.</summary>
    private void FloodInside()
    {
        var cave = G.Cave;
        if (cave == null || Seed is not Vector2I s0) return;
        foreach (var c in Cells) _rim.Add(c.Y * cave.W + c.X);
        var q = new Queue<Vector2I>();
        if (_rim.Contains(s0.Y * cave.W + s0.X)) return;
        Inside.Add(s0.Y * cave.W + s0.X);
        q.Enqueue(s0);
        while (q.Count > 0 && Inside.Count < 40000)
        {
            var c = q.Dequeue();
            foreach (var o in new[] { new Vector2I(1, 0), new Vector2I(-1, 0), new Vector2I(0, 1), new Vector2I(0, -1) })
            {
                var n = c + o;
                if (n.X < 0 || n.Y < 0 || n.X >= cave.W || n.Y >= cave.H || !cave.CellOpen(n.X, n.Y)) continue;
                int k = n.Y * cave.W + n.X;
                if (_rim.Contains(k) || !Inside.Add(k)) continue;
                q.Enqueue(n);
            }
        }
    }

    /// <summary>
    /// Whether a sudden move (a blink, a shadowstep) from one point to another breaks a trial's rule: one way only, in and never out. From
    /// inside a trial under way, it may not end outside it; nor may it end in a ward.
    /// </summary>
    public static bool BlocksMove(Vector2 from, Vector2 to)
    {
        var cave = G.Cave;
        if (cave == null) return false;
        int k = (int)(to.Y / CaveData.Cell) * cave.W + (int)(to.X / CaveData.Cell);
        foreach (var s in All)
        {
            if (s.Opening || s.Inside.Count == 0) continue;
            if (s._rim.Contains(k)) return true;
            if (s.InRoom(from) && !s.Inside.Contains(k)) return true;
        }
        return false;
    }

    /// <summary>Whether a point is on one of the room's cells.</summary>
    public bool InRoom(Vector2 p)
    {
        var cave = G.Cave;
        return cave != null && Inside.Contains((int)(p.Y / CaveData.Cell) * cave.W + (int)(p.X / CaveData.Cell));
    }

    /// <summary>Whether a point is a step inside the room: on one of its cells, with no ward beside it (in the doorway itself a hero may still turn back).</summary>
    public bool WellInside(Vector2 p)
    {
        var cave = G.Cave;
        if (cave == null) return false;
        int i = (int)(p.X / CaveData.Cell), j = (int)(p.Y / CaveData.Cell);
        if (!Inside.Contains(j * cave.W + i)) return false;
        for (int dj = -1; dj <= 1; dj++)
            for (int di = -1; di <= 1; di++)
                if (_rim.Contains((j + dj) * cave.W + i + di)) return false;
        return true;
    }

    /// <summary>Which way is in, from a warded cell (toward its neighbour in the room; zero if none).</summary>
    public Vector2 InwardOf(Vector2I c)
    {
        var cave = G.Cave;
        if (cave == null) return Vector2.Zero;
        foreach (var o in new[] { new Vector2I(1, 0), new Vector2I(-1, 0), new Vector2I(0, 1), new Vector2I(0, -1) })
        {
            var n = c + o;
            if (Inside.Contains(n.Y * cave.W + n.X)) return new Vector2(o.X, o.Y);
        }
        return Vector2.Zero;
    }

    /// <summary>One way only, for heroes: those outside may pass through the wards; once a step inside, they're held with the rest.</summary>
    public override void _PhysicsProcess(double delta)
    {
        if (Opening || Inside.Count == 0) return;
        foreach (var h in G.Players)
        {
            if (h == null || !IsInstanceValid(h)) continue;
            // (a pass only for a hero outside the room: one inside when it shut, even by a doorway, is held from the start)
            if (!_let.Contains(h)) { if (!InRoom(h.GlobalPosition)) { h.AddCollisionExceptionWith(this); _let.Add(h); } }
            else if (WellInside(h.GlobalPosition) && _let.Remove(h))
            {
                h.RemoveCollisionExceptionWith(this);
                Joined++;
                G.Sfx.Play("dodge", h.GlobalPosition, -8, 0.1f, 0.7f);
                G.Fx.Ring(h.GlobalPosition, 18, new Color(0.7f, 0.45f, 1f, 0.8f), 0.25f, FxLayer.RingStyle.Burst);
            }
        }
    }

    private void ReleaseAll()
    {
        foreach (var h in _let) if (h != null && IsInstanceValid(h)) h.RemoveCollisionExceptionWith(this);
        _let.Clear();
    }

    public override void _EnterTree() { if (!All.Contains(this)) All.Add(this); }
    public override void _ExitTree() { All.Remove(this); ReleaseAll(); }

    /// <summary>The trial is won: the wards fade and let everyone through.</summary>
    public void Open()
    {
        if (Opening) return;
        _fade = 0f;
        ReleaseAll();
        foreach (var n in GetChildren()) if (n is CollisionShape2D s) s.SetDeferred(CollisionShape2D.PropertyName.Disabled, true);
        foreach (var c in Cells)
        {
            var at = new Vector2(c.X + 0.5f, c.Y + 0.5f) * CaveData.Cell;
            if (G.Chance(0.4f)) G.Fx.Burst(at, new Color(0.7f, 0.45f, 1f, 0.8f), 3, 60, 1.4f, 0.4f, -30f);
        }
        G.Sfx.Play("dodge", GlobalPosition, -4, 0.1f, 0.6f);
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _t += dt;
        if (Opening && (_fade += dt) > 0.6f) { QueueFree(); return; }
        QueueRedraw();
    }

    public override void _Draw()
    {
        // (rising over the first quarter second, fading over the last)
        float rise = Math.Min(1f, _t / 0.25f), a = rise * (Opening ? 1f - _fade / 0.6f : 1f);
        float s = CaveData.Cell;
        foreach (var c in Cells)
        {
            var at = new Vector2(c.X, c.Y) * s;
            float pulse = 0.5f + 0.5f * MathF.Sin(_t * 4f + (c.X + c.Y) * 0.7f);
            DrawRect(new Rect2(at, new Vector2(s, s)), new Color(0.35f, 0.15f, 0.6f, (0.35f + 0.15f * pulse) * a));
            DrawRect(new Rect2(at + new Vector2(2, 2), new Vector2(s - 4, s - 4)), new Color(0.8f, 0.55f, 1f, (0.35f + 0.35f * pulse) * a), false, 1.5f);
            // (a rune at its heart)
            var mid = at + new Vector2(s, s) * 0.5f;
            DrawLine(mid + new Vector2(-3, -3), mid + new Vector2(3, 3), new Color(0.95f, 0.85f, 1f, 0.6f * a * pulse), 1.2f);
            DrawLine(mid + new Vector2(3, -3), mid + new Vector2(-3, 3), new Color(0.95f, 0.85f, 1f, 0.6f * a * pulse), 1.2f);
        }
    }
}
