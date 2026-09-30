using System;
using Godot;

namespace DaggerCave;

/// <summary>
/// Screen-space layer over the 3D view: floating damage numbers and callouts, elite health
/// bars, and the whole-screen wash (level-ups, potions, enrages). Positions come from the
/// gameplay (2D pixels) projected through the 3D camera, so text stays crisp at any depth.
/// </summary>
public partial class FxOverlay : Control
{
    public override void _Ready()
    {
        Name = "FxOverlay";
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsPreset(LayoutPreset.FullRect);
    }

    public override void _Process(double delta) => QueueRedraw();

    /// <summary>Where a gameplay point (2D pixels) shows on this layer.</summary>
    private Vector2? Screen(Camera3D cam, Vector2 world, float z = 0.4f)
    {
        var p3 = W3.P(world, z);
        if (cam.IsPositionBehind(p3)) return null;
        var sp = cam.UnprojectPosition(p3);
        // viewport pixels -> this canvas (the window may be stretched from the 1280x720 base)
        return GetViewport().GetFinalTransform().AffineInverse() * sp;
    }

    public override void _Draw()
    {
        var fx = G.Fx;
        var cam = Stage3D.I?.Cam;
        if (fx == null || !IsInstanceValid(fx) || cam == null) return;
        // pixels on this layer per gameplay pixel, for sizing text and bars like the 2D game did
        float zoom = G.Main?.Cam2D?.Zoom.X ?? Tune.Feel.CameraZoom;

        var wash = fx.ScreenWash;
        if (wash.A > 0.001f) DrawRect(new Rect2(Vector2.Zero, Size), wash);

        foreach (var e in G.Enemies)
        {
            if (!IsInstanceValid(e) || e.Dead || !e.Elite || e.IsBoss || e.IsGuardian || e.Hp >= e.MaxHp) continue;
            var at = Screen(cam, e.HeadPoint(10f), 0.05f);
            if (at == null) continue;
            float w = 30 * e.Size * 0.6f * zoom;
            var pos = at.Value - new Vector2(w / 2, 0);
            DrawRect(new Rect2(pos - new Vector2(1, 1), new Vector2(w + 2, 4 * zoom * 0.5f + 2)), new Color(0, 0, 0, 0.75f));
            DrawRect(new Rect2(pos, new Vector2(w * Math.Max(0, e.Hp / e.MaxHp), 4 * zoom * 0.5f)), new Color(0.95f, 0.22f, 0.25f));
        }

        var font = ThemeDB.FallbackFont;
        foreach (var t in fx.Texts)
        {
            var at = Screen(cam, t.Pos, 0.1f);
            if (at == null) continue;
            float a = Math.Clamp(t.Life / t.Max * 2f, 0, 1);
            float age = t.Max - t.Life;
            float pop = age < 0.1f ? 1.7f - age * 7f : 1f;
            int size = Math.Max(8, (int)(t.Size * pop * zoom * 0.62f));
            var pos = at.Value - new Vector2(t.Text.Length * size * 0.28f, 0);
            // a dark outline, then the colour, then a hot core for big numbers
            for (int k = 0; k < 4; k++)
            {
                var o = new Vector2(k < 2 ? -1.5f : 1.5f, k % 2 == 0 ? -1.5f : 1.5f);
                DrawString(font, pos + o, t.Text, HorizontalAlignment.Left, -1, size, new Color(0, 0, 0, a * 0.75f));
            }
            DrawString(font, pos, t.Text, HorizontalAlignment.Left, -1, size, new Color(t.Col, a));
        }
    }
}
