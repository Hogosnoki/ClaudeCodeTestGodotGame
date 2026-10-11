using System.Collections.Generic;
using Godot;

namespace DaggerCave;

/// <summary>
/// The thin ink line round the outline of every solid thing in a 3D view (edge_depth.gdshader): a full-screen pass that finds where the
/// scene steps away behind something. Its width is the ground borders setting (<see cref="GameSettings.TerrainInkPx"/>); at 0 the pass is
/// hidden. (The characters' bolder line is <see cref="InkOutline"/>, drawn over this one.)
/// </summary>
public partial class EdgeInk : MeshInstance3D
{
    private static readonly List<EdgeInk> All = new();
    private static Shader _shader;

    public override void _EnterTree() { if (!All.Contains(this)) All.Add(this); }
    public override void _ExitTree() => All.Remove(this);

    public override void _Ready()
    {
        Name = "EdgeInk";
        _shader ??= GD.Load<Shader>("res://DaggerCave/Render3D/Shaders/edge_depth.gdshader");
        // (under the characters' line: that one draws at 100)
        MaterialOverride = new ShaderMaterial { Shader = _shader, RenderPriority = 99 };
        Mesh = new QuadMesh { Size = new Vector2(2, 2) };
        ExtraCullMargin = 16384f;
        CastShadow = ShadowCastingSetting.Off;
        Layers = 1;
        Visible = GameSettings.TerrainInkPx > 0.01f;
    }

    /// <summary>The setting changed: shown only with a width.</summary>
    public static void Refresh()
    {
        foreach (var e in All) if (IsInstanceValid(e)) e.Visible = GameSettings.TerrainInkPx > 0.01f;
    }
}
