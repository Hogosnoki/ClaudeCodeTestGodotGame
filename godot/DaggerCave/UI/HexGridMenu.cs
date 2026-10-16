using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

/// <summary>
/// The Ember Grid's screen: a field of hexes round the ember fire, a branch to each hero and feature. Drag (or the right stick) to
/// look about, the wheel (or the shoulders) to zoom, click a hex (or step to it with the arrows or the stick) to read it, and buy it
/// from the panel at the right (or with accept). Owned hexes burn in their branch's colour; the ones you can buy now pulse;
/// a branch whose hero or feature isn't freed yet is grey, with what opens it.
/// </summary>
public partial class HexGridMenu : Control
{
    public Action Closed;
    private const float R = 30f, PanelW = 340f;
    private Vector2 _pan;
    private float _zoom = 1f, _t;
    private HexNode _sel;
    private bool _dragging;
    private Vector2 _dragFrom, _panFrom;
    private Label _title, _branch, _desc, _cost, _status, _mats, _hint;
    private Button _buy;

    /// <summary>For the tests: the hex selected.</summary>
    public HexNode Selected => _sel;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        ProcessMode = ProcessModeEnum.Always;
        Theme = UiKit.Theme;
        Visible = false;
        MouseFilter = MouseFilterEnum.Stop;
        // the panel at the right
        var panel = new PanelContainer { Position = new Vector2(0, 0), CustomMinimumSize = new Vector2(PanelW, 0) };
        panel.SetAnchorsAndOffsetsPreset(LayoutPreset.RightWide);
        panel.OffsetLeft = -PanelW - 12; panel.OffsetRight = -12; panel.OffsetTop = 12; panel.OffsetBottom = -12;
        AddChild(panel);
        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 8);
        panel.AddChild(col);
        col.AddChild(UiKit.Label("THE EMBER GRID", 24, UiKit.Gold));
        _mats = UiKit.Label("", 13, UiKit.Text);
        _mats.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _mats.CustomMinimumSize = new Vector2(PanelW - 40, 0);
        col.AddChild(_mats);
        col.AddChild(new HSeparator());
        _branch = UiKit.Label("", 13, UiKit.Dim);
        col.AddChild(_branch);
        _title = UiKit.Label("", 21, Colors.White);
        _title.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _title.CustomMinimumSize = new Vector2(PanelW - 40, 0);
        col.AddChild(_title);
        _desc = UiKit.Label("", 15, UiKit.Text);
        _desc.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _desc.CustomMinimumSize = new Vector2(PanelW - 40, 0);
        col.AddChild(_desc);
        _cost = UiKit.Label("", 15, UiKit.Gold);
        col.AddChild(_cost);
        _status = UiKit.Label("", 13, new Color(1f, 0.6f, 0.5f));
        _status.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _status.CustomMinimumSize = new Vector2(PanelW - 40, 0);
        col.AddChild(_status);
        _buy = UiKit.Button("Buy", BuySelected, 200);
        col.AddChild(_buy);
        col.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill });
        _hint = UiKit.Label("", 12, UiKit.Dim);
        _hint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _hint.CustomMinimumSize = new Vector2(PanelW - 40, 0);
        col.AddChild(_hint);
        col.AddChild(UiKit.Button("Back", Close, 160));
    }

    /// <summary>Opens on a hero's branch (or the fire).</summary>
    public void Open(HeroKind? hero = null)
    {
        Visible = true;
        var b = hero is HeroKind h ? HexGrid.Branches.FirstOrDefault(x => x.Hero == h) : null;
        _sel = b?.Root ?? HexGrid.Branches[0].Root;
        _zoom = 1f;
        CenterOn(_sel);
        _hint.Text = Meta.GridTutorialDone ? "Drag or right stick: look about  ·  wheel or shoulders: zoom  ·  arrows or stick: step  ·  accept: buy"
            : "Every lasting advancement is bought here, outward from the fire: a hex once the one before it is yours, for embers and its branch's material. "
              + "Materials turn up in runs in place of something you couldn't use: a potion on a full belt is a Reagent half the time, a heart at full health a hero's Essence, a key with full slots a Tumbler, "
              + "gold on a full purse a Guild Seal; a relic can be salvaged for Dust; guardians sometimes leave a Pearl. A hero's branch opens once they are freed.";
        if (!Meta.GridTutorialDone) { Meta.GridTutorialDone = true; Meta.Save(); }
        Refresh();
    }

    private void CenterOn(HexNode n) => _pan = n == null ? Vector2.Zero : -HexGrid.ToPixel(n.Cell, R);

    private Vector2 Origin => new((Size.X - PanelW - 24) / 2f, Size.Y / 2f);
    private Vector2 ToScreen(Vector2I cell) => Origin + (HexGrid.ToPixel(cell, R) + _pan) * _zoom;

    private bool Reachable(HexNode n) => n.Branch.Open() && (n.Parent == HexGrid.Center || HexGrid.Owns(n.Parent));

    private void Refresh()
    {
        _mats.Text = $"Embers {Meta.Embers}    " + string.Join("    ", new[] { "reagent", "pearl", "tumbler", "seal", "dust" }.Select(k => $"{HexGrid.MatName(k)} {HexGrid.Mat(k)}"))
            + "\n" + string.Join("    ", Enum.GetValues<HeroKind>().Where(Meta.IsUnlocked).Select(h => $"{HexGrid.HeroLabel(h)} {HexGrid.Mat(HexGrid.EssenceKey(h))}"));
        if (_sel == null) { _title.Text = "The ember fire"; _desc.Text = "Where every branch begins."; _cost.Text = ""; _status.Text = ""; _buy.Visible = false; return; }
        var n = _sel;
        _branch.Text = $"{n.Branch.Name}  ·  {(n.Kind switch { HexKind.Keystone => "keystone", HexKind.Notable => "notable", HexKind.SideGrade => "side-grade", HexKind.Root => "root", _ => "minor" })}";
        _title.Text = n.Name;
        _desc.Text = n.Desc;
        bool owned = HexGrid.Owns(n.Id);
        _cost.Text = owned ? "Owned" : $"{n.Embers} embers  +  {n.Mats} {n.Branch.MatName}";
        bool can = HexGrid.CanBuy(n, out var why);
        _status.Text = owned ? "" : can ? "" : why;
        _buy.Visible = !owned;
        _buy.Disabled = !can;
    }

    private void BuySelected()
    {
        if (_sel == null || !HexGrid.Buy(_sel)) { G.Sfx?.Play("ui", null, -8, 0, 0.7f); return; }
        G.Sfx?.Play("levelup", null, -6, 0, 1.2f);
        Bought++;
        Refresh();
        QueueRedraw();
    }

    /// <summary>For the tests: hexes bought on this screen.</summary>
    public int Bought { get; private set; }

    private void Close()
    {
        Visible = false;
        Closed?.Invoke();
    }

    public override void _Process(double delta)
    {
        if (!Visible) return;
        _t += (float)delta;
        // the right stick looks about
        var look = new Vector2(Input.GetJoyAxis(0, JoyAxis.RightX), Input.GetJoyAxis(0, JoyAxis.RightY));
        if (look.Length() > 0.2f) _pan -= look * 600f * (float)delta / _zoom;
        if (Input.IsJoyButtonPressed(0, JoyButton.RightShoulder)) Zoom(1f + 1.5f * (float)delta, Origin);
        if (Input.IsJoyButtonPressed(0, JoyButton.LeftShoulder)) Zoom(1f / (1f + 1.5f * (float)delta), Origin);
        QueueRedraw();
    }

    private void Zoom(float k, Vector2 at)
    {
        float z = Math.Clamp(_zoom * k, 0.35f, 2.2f);
        // (about the point under the cursor)
        var world = (at - Origin) / _zoom - _pan;
        _zoom = z;
        _pan = (at - Origin) / _zoom - world;
    }

    public override void _GuiInput(InputEvent e)
    {
        if (e is InputEventMouseButton mb)
        {
            if (mb.ButtonIndex == MouseButton.WheelUp && mb.Pressed) Zoom(1.12f, mb.Position);
            else if (mb.ButtonIndex == MouseButton.WheelDown && mb.Pressed) Zoom(1f / 1.12f, mb.Position);
            else if (mb.ButtonIndex is MouseButton.Left or MouseButton.Right)
            {
                if (mb.Pressed) { _dragging = true; _dragFrom = mb.Position; _panFrom = _pan; }
                else
                {
                    _dragging = false;
                    // a click that didn't drag picks the hex under it (a double click buys it)
                    if (mb.ButtonIndex == MouseButton.Left && mb.Position.DistanceTo(_dragFrom) < 6f)
                    {
                        var cell = HexGrid.FromPixel((mb.Position - Origin) / _zoom - _pan, R);
                        if (HexGrid.AtCell.TryGetValue(cell, out var n)) { if (n == _sel && mb.DoubleClick) BuySelected(); _sel = n; Refresh(); }
                        else if (cell == Vector2I.Zero) { _sel = null; Refresh(); }
                    }
                }
            }
            AcceptEvent();
        }
        else if (e is InputEventMouseMotion mm && _dragging)
        {
            _pan = _panFrom + (mm.Position - _dragFrom) / _zoom;
            AcceptEvent();
        }
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (!Visible) return;
        if (e.IsActionPressed("pause") || e.IsActionPressed("ui_cancel")) { GetViewport().SetInputAsHandled(); Close(); return; }
        Vector2 dir = Vector2.Zero;
        if (e.IsActionPressed("ui_left")) dir = Vector2.Left;
        else if (e.IsActionPressed("ui_right")) dir = Vector2.Right;
        else if (e.IsActionPressed("ui_up")) dir = Vector2.Up;
        else if (e.IsActionPressed("ui_down")) dir = Vector2.Down;
        if (dir != Vector2.Zero) { Step(dir); GetViewport().SetInputAsHandled(); return; }
        if (e.IsActionPressed("ui_accept") && !_buy.HasFocus()) { BuySelected(); GetViewport().SetInputAsHandled(); }
    }

    /// <summary>To the hex in a direction (the nearest within a cone), keeping it in view.</summary>
    private void Step(Vector2 dir)
    {
        var from = _sel == null ? Vector2.Zero : HexGrid.ToPixel(_sel.Cell, R);
        HexNode best = null; float bs = float.MaxValue;
        foreach (var n in HexGrid.Nodes)
        {
            if (n == _sel) continue;
            var d = HexGrid.ToPixel(n.Cell, R) - from;
            float len = d.Length();
            if (len < 1f) continue;
            float cos = d.Normalized().Dot(dir);
            if (cos < 0.5f) continue;
            float score = len * (2f - cos);
            if (score < bs) { bs = score; best = n; }
        }
        if (best == null) return;
        _sel = best;
        G.Sfx?.Play("ui", null, -12, 0, 1.2f);
        var s = ToScreen(best.Cell);
        if (s.X < 80 || s.Y < 80 || s.X > Size.X - PanelW - 100 || s.Y > Size.Y - 80) CenterOn(best);
        Refresh();
    }

    private static Vector2[] HexPts(Vector2 c, float r)
    {
        var p = new Vector2[6];
        for (int k = 0; k < 6; k++) { float a = Mathf.Pi / 180f * (60 * k - 30); p[k] = c + new Vector2(MathF.Cos(a), MathF.Sin(a)) * r; }
        return p;
    }

    public override void _Draw()
    {
        var vs = Size;
        DrawRect(new Rect2(Vector2.Zero, vs), new Color(0.05f, 0.04f, 0.06f, 1f));
        float r = R * _zoom;
        // a faint field of empty hexes, for the grid's feel
        var o = Origin;
        var centerCell = HexGrid.FromPixel(-_pan, R);
        int span = (int)(Math.Max(vs.X, vs.Y) / (r * 1.5f)) + 2;
        for (int dq = -span; dq <= span; dq++)
            for (int dr = -span; dr <= span; dr++)
            {
                var c = new Vector2I(centerCell.X + dq, centerCell.Y + dr);
                var sp = ToScreen(c);
                if (sp.X < -r || sp.Y < -r || sp.X > vs.X - PanelW + r || sp.Y > vs.Y + r) continue;
                if (HexGrid.AtCell.ContainsKey(c)) continue;
                var pts = HexPts(sp, r * 0.96f);
                DrawPolyline(new[] { pts[0], pts[1], pts[2], pts[3], pts[4], pts[5], pts[0] }, new Color(0.3f, 0.26f, 0.22f, 0.12f), 1f);
            }
        // the links
        foreach (var n in HexGrid.Nodes)
        {
            var a = n.Parent == HexGrid.Center ? ToScreen(Vector2I.Zero) : ToScreen(HexGrid.Find(n.Parent).Cell);
            var b = ToScreen(n.Cell);
            bool lit = HexGrid.Owns(n.Id);
            DrawLine(a, b, lit ? new Color(n.Branch.Color, 0.85f) : new Color(n.Branch.Color, n.Branch.Open() ? 0.22f : 0.08f), lit ? 3f * _zoom : 1.5f * _zoom, true);
        }
        // the fire
        var fc = ToScreen(Vector2I.Zero);
        DrawColoredPolygon(HexPts(fc, r * 0.95f), new Color(0.55f, 0.22f, 0.06f));
        DrawPolyline(Ring(HexPts(fc, r * 0.95f)), new Color(1f, 0.7f, 0.3f), 2f, true);
        float flick = 1f + 0.08f * MathF.Sin(_t * 9f);
        DrawColoredPolygon(new[] { fc + new Vector2(0, -r * 0.55f * flick), fc + new Vector2(r * 0.3f, r * 0.25f), fc + new Vector2(-r * 0.3f, r * 0.25f) }, new Color(1f, 0.75f, 0.3f));
        // the hexes
        var font = ThemeDB.FallbackFont;
        foreach (var n in HexGrid.Nodes)
        {
            var c = ToScreen(n.Cell);
            if (c.X < -r * 2 || c.Y < -r * 2 || c.X > vs.X - PanelW || c.Y > vs.Y + r * 2) continue;
            bool owned = HexGrid.Owns(n.Id), open = n.Branch.Open(), reach = Reachable(n), can = !owned && HexGrid.CanBuy(n, out _);
            var col = n.Branch.Color;
            float size = n.Kind switch { HexKind.Keystone => 1.08f, HexKind.Notable => 1f, HexKind.Root => 1f, _ => 0.9f } * r * 0.92f;
            Color fill = !open ? new Color(0.16f, 0.16f, 0.17f) : owned ? col.Darkened(0.15f) : reach ? col.Darkened(0.78f) : new Color(0.09f, 0.08f, 0.1f);
            Color edge = !open ? new Color(0.35f, 0.35f, 0.36f) : owned ? col.Lightened(0.35f) : reach ? col : new Color(col, 0.3f);
            if (can) edge = edge.Lerp(Colors.White, 0.35f + 0.35f * MathF.Sin(_t * 4f));
            var pts = HexPts(c, size);
            DrawColoredPolygon(pts, fill);
            DrawPolyline(Ring(pts), edge, n.Kind == HexKind.Keystone ? 3f : 2f, true);
            var g = owned ? new Color(0.06f, 0.05f, 0.05f) : !open ? new Color(0.45f, 0.45f, 0.46f) : edge;
            float gs = size * 0.32f;
            switch (n.Kind)
            {
                case HexKind.Keystone:
                {
                    var st = new Vector2[10];
                    for (int k = 0; k < 10; k++) { float ang = -Mathf.Pi / 2 + k * Mathf.Pi / 5; st[k] = c + new Vector2(MathF.Cos(ang), MathF.Sin(ang)) * (k % 2 == 0 ? gs * 1.4f : gs * 0.6f); }
                    DrawColoredPolygon(st, g);
                    break;
                }
                case HexKind.Notable: DrawColoredPolygon(new[] { c + new Vector2(0, -gs), c + new Vector2(gs, 0), c + new Vector2(0, gs), c + new Vector2(-gs, 0) }, g); break;
                case HexKind.SideGrade:
                    DrawColoredPolygon(new[] { c + new Vector2(-gs, -gs * 0.5f), c + new Vector2(gs * 0.2f, -gs * 1.1f), c + new Vector2(gs * 0.2f, gs * 0.1f) }, g);
                    DrawColoredPolygon(new[] { c + new Vector2(gs, gs * 0.5f), c + new Vector2(-gs * 0.2f, gs * 1.1f), c + new Vector2(-gs * 0.2f, -gs * 0.1f) }, g);
                    break;
                case HexKind.Root: DrawArc(c, gs, 0, Mathf.Tau, 20, g, 2.5f, true); break;
                default: DrawCircle(c, gs * 0.45f, g); break;
            }
            if (n == _sel)
            {
                float pulse = 1.12f + 0.05f * MathF.Sin(_t * 6f);
                DrawPolyline(Ring(HexPts(c, size * pulse)), Colors.White, 2.5f, true);
            }
        }
        // the branches' names, by their roots
        foreach (var b in HexGrid.Branches)
        {
            var c = ToScreen(b.Root.Cell);
            var dir = new Vector2(MathF.Cos(b.Angle), MathF.Sin(b.Angle));
            var at = c - dir * r * 1.6f;
            int fs = (int)Math.Clamp(11 * _zoom, 8, 16);
            var size = font.GetStringSize(b.Name, HorizontalAlignment.Left, -1, fs);
            DrawString(font, at - new Vector2(size.X / 2, -fs / 3f), b.Name, HorizontalAlignment.Left, -1, fs, b.Open() ? new Color(b.Color, 0.9f) : new Color(0.5f, 0.5f, 0.5f, 0.8f));
            if (!b.Open()) DrawString(font, at - new Vector2(size.X / 2, -fs * 1.3f), "LOCKED", HorizontalAlignment.Left, -1, Math.Max(7, fs - 3), new Color(0.6f, 0.6f, 0.6f, 0.8f));
        }
    }

    private static Vector2[] Ring(Vector2[] p) => p.Append(p[0]).ToArray();
}
