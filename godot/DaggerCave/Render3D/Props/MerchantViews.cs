using System;
using Godot;

namespace DaggerCave;

/// <summary>The merchant, at a stall: his own model, a plank table with a cloth and a few wares set out on it, a crate or two, and his lantern's light over it all.</summary>
public partial class MerchantView : PropView
{
    private CreatureModel _model;
    private OmniLight3D _light;
    private float _footY;
    private Node3D _stall;

    /// <summary>The whole stall (him, his table, his lantern) is drawn this much bigger than its models are made: a hero's height, the table at his waist.</summary>
    private const float Grow = 1.6f;

    protected override void Build()
    {
        // (the stall: behind him, a little to the side)
        _stall = new Node3D();
        AddChild(_stall);
        var wood = new Color(0.36f, 0.24f, 0.14f);
        var cloth = new Color(0.62f, 0.2f, 0.16f);
        var mb = new MeshBuilder();
        PropMeshes.Box(mb, new Vector3(0, 0.34f, 0), new Vector3(0.55f, 0.025f, 0.22f), wood);
        foreach (float x in new[] { -0.5f, 0.5f })
            foreach (float z in new[] { -0.18f, 0.18f })
                PropMeshes.Box(mb, new Vector3(x, 0.17f, z), new Vector3(0.025f, 0.17f, 0.025f), wood.Darkened(0.2f));
        PropMeshes.Box(mb, new Vector3(0, 0.365f, 0.2f), new Vector3(0.57f, 0.12f, 0.012f), cloth);
        PropMeshes.Box(mb, new Vector3(0, 0.37f, 0), new Vector3(0.5f, 0.006f, 0.2f), cloth.Lightened(0.1f));
        // wares: a few bottles, a coil of rope, a stack of books, a little chest
        var glass = new[] { new Color(0.3f, 0.7f, 0.4f), new Color(0.75f, 0.3f, 0.3f), new Color(0.4f, 0.5f, 0.85f) };
        for (int k = 0; k < 3; k++)
        {
            float x = -0.38f + k * 0.1f;
            mb.Tube(new[] { new Vector3(x, 0.38f, 0.0f), new Vector3(x, 0.47f, 0.0f) }, new[] { 0.032f, 0.028f }, 8, glass[k]);
            mb.Tube(new[] { new Vector3(x, 0.47f, 0.0f), new Vector3(x, 0.52f, 0.0f) }, new[] { 0.012f, 0.011f }, 6, new Color(0.55f, 0.4f, 0.25f));
        }
        PropMeshes.Box(mb, new Vector3(0.1f, 0.4f, 0.02f), new Vector3(0.07f, 0.02f, 0.05f), new Color(0.5f, 0.2f, 0.2f));
        PropMeshes.Box(mb, new Vector3(0.1f, 0.44f, 0.02f), new Vector3(0.065f, 0.02f, 0.05f), new Color(0.2f, 0.35f, 0.5f));
        PropMeshes.Box(mb, new Vector3(0.1f, 0.48f, 0.02f), new Vector3(0.06f, 0.02f, 0.045f), new Color(0.55f, 0.45f, 0.2f));
        PropMeshes.Box(mb, new Vector3(0.36f, 0.43f, 0f), new Vector3(0.09f, 0.055f, 0.06f), new Color(0.3f, 0.2f, 0.12f));
        PropMeshes.Box(mb, new Vector3(0.36f, 0.4f, 0.062f), new Vector3(0.09f, 0.01f, 0.004f), new Color(0.85f, 0.65f, 0.2f));
        // crates on the floor beside it
        PropMeshes.Box(mb, new Vector3(0.78f, 0.13f, -0.05f), new Vector3(0.13f, 0.13f, 0.13f), wood.Lightened(0.05f));
        PropMeshes.Box(mb, new Vector3(0.86f, 0.33f, -0.06f), new Vector3(0.09f, 0.07f, 0.09f), wood);
        _stall.AddChild(PropViews.Mesh(mb, PropViews.VertexColored));

        _model = CreatureModel.Create("merchant");
        if (_model != null)
        {
            AddChild(_model);
            _footY = float.IsNaN(_model.Design.FloorY) ? _model.Kit.Sculpt.Bounds.Position.Y : _model.Design.FloorY;
        }
        _light = PropViews.Light(Lore.MerchantColor, 1.1f, 5f);
        _light.Position = new Vector3(0.1f, 1.0f, 0.5f);
        AddChild(_light);
        AddChild(new Label3D
        {
            Text = "MERCHANT", Modulate = Lore.MerchantColor.Lightened(0.35f), OutlineModulate = new Color(0.05f, 0.04f, 0.03f, 0.9f), FontSize = 40, PixelSize = 0.008f / Grow,
            OutlineSize = 12, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, NoDepthTest = true, RenderPriority = 2, OutlineRenderPriority = 1, Position = new Vector3(0, 1.45f, 0),
        });
    }

    protected override void Sync(float dt)
    {
        var m = (Merchant)Owner2D;
        Follow(default, 0f);
        Scale = Vector3.One * Grow;
        // the stall stands on the side he faces away from (behind him)
        _stall.Position = new Vector3(0.55f * -m.Facing, 0, -0.35f);
        _stall.Scale = new Vector3(1f, 1f, 1f);
        if (_model != null)
        {
            var a = new AnimInput { Clip = "idle", T = 0f, Frame = 0, Frames = 1, Loop = true, Time = Time, Dt = dt, Facing = (int)m.Facing, OnFloor = true, ClipTime = Time };
            _model.Face((int)m.Facing, "idle", 0f, dt);
            _model.Animate(a);
            _model.UpdatePivot(new Vector2(m.Facing, 1f), 0f, -_footY, false);
            _model.Position = new Vector3(0, -_footY, 0);
        }
        _light.LightEnergy = 1.1f + 0.15f * MathF.Sin(Time * 6.1f) * MathF.Sin(Time * 2.3f) + 0.6f * Math.Max(0f, 1f - m.SoldT * 1.5f);
    }
}
