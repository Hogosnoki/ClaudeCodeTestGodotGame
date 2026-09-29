using Godot;

namespace DaggerCave;

/// <summary>
/// The block of ice a creature frozen solid stands in (for its 3D look, <see cref="IceBlockView"/>).
/// Every game makes its own from the creature's state, and it goes the moment the creature thaws,
/// is shattered or dies.
/// </summary>
public partial class IceBlock : Node2D
{
    public Enemy Holder;
    /// <summary>How big a block (the creature's hit radius, px).</summary>
    public float Size = 12f;
    private float _t;
    public float Age => _t;

    public override void _Ready()
    {
        ZIndex = 2;
        if (Holder != null && IsInstanceValid(Holder)) GlobalPosition = Holder.GlobalPosition;
    }

    public override void _Process(double delta)
    {
        _t += (float)delta;
        if (Holder == null || !IsInstanceValid(Holder) || Holder.Dead || !Holder.FrozenSolid) { QueueFree(); return; }
        GlobalPosition = Holder.GlobalPosition;
    }
}
