using Godot;

namespace DaggerCave;

/// <summary>A gear on a wall of the works, turning for ever at <see cref="Speed"/> radians a second about its axle (its own Z).</summary>
public partial class GearSpinner : MeshInstance3D
{
    public float Speed = 0.3f;
    public override void _Process(double delta) => RotateObjectLocal(Vector3.Back, Speed * (float)delta);
}
