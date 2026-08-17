#nullable enable
using Godot;

namespace ProcGenGame
{
    /// <summary>
    /// The "Generation" sub-tab under Maps: region (designated area), Transformation, and the
    /// selected layer's seed/noise -- everything that shapes terrain except its tile palette
    /// (see MapEditorToolScene.Tiles.cs). Painting/panning on the map stays active while this
    /// sub-tab is showing (see <see cref="MapEditorToolScene.CanPaintOrDraw"/>).
    /// </summary>
    public partial class MapEditorToolScene
    {
        private void BuildGenerationSubTab(TabContainer parent)
        {
            var root = new VBoxContainer { Name = "Generation" };
            root.AddThemeConstantOverride("separation", 6);
            parent.AddChild(root);

            root.AddChild(Header("Region (designated map area)"));
            _originXBox = AddIntField(root, "Origin X", _originX, -100000, 100000, v => { _originX = v; Regenerate(); });
            _originYBox = AddIntField(root, "Origin Y", _originY, -100000, 100000, v => { _originY = v; Regenerate(); });
            _widthBox = AddIntField(root, "Width", _regionWidth, 1, 200, v => { _regionWidth = v; Regenerate(); });
            _heightBox = AddIntField(root, "Height", _regionHeight, 1, 200, v => { _regionHeight = v; Regenerate(); });
            root.AddChild(new Label
            {
                Text = "Content outside this area renders dimmed. Editing these fields doesn't move the camera -- use the coordinate readout to find where to place it, then type it in here.",
                Modulate = new Color(1, 1, 1, 0.6f),
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
            });
            root.AddChild(new HSeparator());

            root.AddChild(Header("Transformation"));
            var tRow = new HBoxContainer();
            var minusT = new Button { Text = "-0.1" };
            minusT.Pressed += () => StepTransformation(-0.1);
            tRow.AddChild(minusT);
            // Fixed (not expand-fill) width: the ScrollContainer this sits in doesn't reliably
            // clip an expand-fill child to available width, so a flexible width here can push
            // the trailing "+0.1" button out past the visible panel. A fixed width keeps the
            // row's total size small and predictable regardless.
            _transformationBox = new SpinBox { Step = 0.01, MinValue = -100000, MaxValue = 100000, CustomMinimumSize = new Vector2(110, 0) };
            _transformationBox.Value = _transformation;
            _transformationBox.ValueChanged += OnTransformationBoxChanged;
            tRow.AddChild(_transformationBox);
            var plusT = new Button { Text = "+0.1" };
            plusT.Pressed += () => StepTransformation(0.1);
            tRow.AddChild(plusT);
            root.AddChild(tRow);
            root.AddChild(new Label
            {
                Text = "Larger steps are clamped to 0.1, to keep shapes continuous.",
                Modulate = new Color(1, 1, 1, 0.6f),
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
            });
            root.AddChild(new HSeparator());

            root.AddChild(Header("Seed (selected layer)"));
            _seedXBox = AddDoubleField(root, "X", -1000000, 1000000, OnSeedChanged);
            _seedYBox = AddDoubleField(root, "Y", -1000000, 1000000, OnSeedChanged);
            _seedTBox = AddDoubleField(root, "T", -1000000, 1000000, OnSeedChanged);
            root.AddChild(new HSeparator());

            root.AddChild(Header("Noise (selected layer)"));
            _octavesBox = AddIntField(root, "Octaves", 1, 1, 8, v => { CurrentLayer().Noise.Octaves = v; Regenerate(); });
            _frequencyBox = AddDoubleField(root, "Frequency", 0.0001, 10, OnNoiseChanged, step: 0.0001);
            _persistenceBox = AddDoubleField(root, "Persistence", 0, 1, OnNoiseChanged);
            _lacunarityBox = AddDoubleField(root, "Lacunarity", 0.1, 10, OnNoiseChanged, step: 0.001);
            root.AddChild(new Label
            {
                Text = "Fewer octaves / lower frequency = smoother, more gradual terrain transitions (e.g. a wide sandy beach ring); more octaves = rougher, more detailed but steeper edges.",
                Modulate = new Color(1, 1, 1, 0.6f),
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
            });
        }
    }
}
