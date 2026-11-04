using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

/// <summary>
/// On-screen controls for a touch screen (a phone or tablet; <c>--touch</c> shows them anywhere, for testing). Shown only while
/// playing (menus are tapped as with a mouse), and hidden as soon as a controller is used (a touch brings them back).
/// <list type="bullet">
/// <item>Left half: a floating stick. Touch anywhere there and drag: it moves the hero (up and down swim, climb and aim, as the
/// left stick does). It follows the thumb if it strays past its rim.</item>
/// <item>Right side: the big attack button. Tapped, it attacks where you move or face; dragged off it, it aims and attacks that
/// way again and again, as the right stick does (the Warden's raises the shield that way).</item>
/// <item>Round it: jump, the dodge-button ability, the great special and the second ability; smaller ones for support, rope,
/// interact (chests, exits, reviving) and a potion; the hero's three elements down the left edge; pause (and a milestone, when
/// one is waiting) in the top right corner.</item>
/// </list>
/// Each button is sent as the same action a key or controller button is (<see cref="InputEventAction"/>), so everything that
/// reads the controls reads these too. The sticks are read by <see cref="Player.ReadLocalInput"/> from <see cref="Move"/> and
/// <see cref="AimStick"/>. While they are shown, a touch is not also turned into a mouse click (which would attack, and aim at
/// the thumb); in the menus it is again.
/// </summary>
public partial class TouchControls : Control
{
    /// <summary>The controls exist on this machine (a touch screen, or <c>--touch</c>).</summary>
    public static bool Enabled;
    /// <summary>The controls are on screen now.</summary>
    public static bool Shown;
    /// <summary>The move stick, shaped as a controller's left stick is (round dead zone, -1..1).</summary>
    public static Vector2 Move;
    /// <summary>The attack button dragged off its middle: the aim, as a controller's right stick (zero when not).</summary>
    public static Vector2 AimStick;
    /// <summary>For the tests and the screenshots: the last frame's shown buttons (action, centre, radius).</summary>
    public static readonly List<(string Action, Vector2 At, float R)> Layout = new();

    private const float StickR = 90f, AttackDrag = 22f;
    private static readonly Rect2 StickZone = new(110, 170, 520, 550);

    private sealed class Btn
    {
        public string Action, Text;
        public Vector2 At;
        public float R;
        public Color Tint = new(1, 1, 1);
        public Func<bool> Show = () => true;
        public int Finger = -1;
    }

    private readonly List<Btn> _btns = new();
    private Btn _attack;
    private int _moveFinger = -1;
    private Vector2 _moveOrigin, _moveAt;
    private int _attackFinger = -1;
    private Vector2 _attackAt;
    private bool _dragging;

    public static void Setup()
    {
        Enabled = DisplayServer.IsTouchscreenAvailable() || OS.GetCmdlineUserArgs().Contains("--touch") || OS.HasFeature("mobile");
    }

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsPreset(LayoutPreset.FullRect);
        Size = new Vector2(1280, 720);
        Visible = false;
        _attack = Add("attack", "ATTACK", 1160, 610, 64);
        Add("jump", "JUMP", 1030, 652, 50);
        Add("dodge", "DODGE", 1046, 530, 44);
        Add("ability", "SPECIAL", 1140, 478, 44);
        Add("ability2", "SKILL", 1236, 500, 40);
        Add("support", "SUP", 1242, 398, 30);
        Add("rope", "ROPE", 1176, 384, 28);
        Add("interact", "USE", 912, 652, 34);
        Add("potion", "POT", 1242, 314, 28).Show = () => G.Player != null && G.Player.Potions > 0;
        for (int k = 0; k < 3; k++)
        {
            int kk = k;
            var b = Add($"infuse_{k + 1}", $"{k + 1}", 48, 300 + k * 74, 30);
            b.Show = () => ElementOf(kk) != HeroElement.None;
        }
        // (left of the minimap)
        Add("pause", "II", 960, 100, 26);
        Add("milestone", "★", 1030, 100, 26).Show = () => G.Player != null && G.Player.PendingMilestones > 0;
    }

    private Btn Add(string action, string text, float x, float y, float r)
    {
        var b = new Btn { Action = action, Text = text, At = new Vector2(x, y), R = r };
        _btns.Add(b);
        return b;
    }

    private static HeroElement ElementOf(int k)
    {
        var p = G.Player;
        if (p == null || !p.Elemental) return HeroElement.None;
        var els = Elements.Of(p.Hero);
        return k < els.Length ? els[k] : HeroElement.None;
    }

    public override void _Process(double delta)
    {
        Flush();
        bool show = Enabled && G.Main != null && !G.Main.UsingPad && !G.Main.MenuOpen && G.Player != null && !GetTree().Paused;
        if (show != Visible)
        {
            Visible = show;
            if (!show) { ReleaseAll(); Flush(); }
        }
        Shown = show;
        // (a touch in a menu is a click; in play it is only these controls)
        if (Enabled) Input.EmulateMouseFromTouch = !show;
        if (!show) return;
        Relabel();
        Layout.Clear();
        foreach (var b in _btns) if (b.Show()) Layout.Add((b.Action, b.At, b.R));
        QueueRedraw();
    }

    /// <summary>The buttons take the hero's own names for its abilities, and its elements' colours.</summary>
    private void Relabel()
    {
        var p = G.Player;
        foreach (var b in _btns)
        {
            if (b.Action.StartsWith("infuse_"))
            {
                var el = ElementOf(b.Action[^1] - '1');
                b.Tint = el == HeroElement.None ? Colors.White : Elements.ColorOf(el);
                b.Text = el == HeroElement.None ? "" : Elements.Name(el).ToUpperInvariant();
                continue;
            }
            if (!p.SimpleKit) continue;
            b.Text = b.Action switch
            {
                "attack" => p.KitLabel(KitRole.Primary).ToUpperInvariant(),
                "dodge" => p.KitLabel(KitRole.Defensive).ToUpperInvariant(),
                "ability" => p.KitLabel(KitRole.Special).ToUpperInvariant(),
                "ability2" => p.KitLabel(KitRole.Other).ToUpperInvariant(),
                _ => b.Text,
            };
        }
    }

    /// <summary>The presses and releases to send (sent from <see cref="_Process"/>, not from inside the touch's own handling, where a
    /// new event would be lost).</summary>
    private static readonly List<(string Action, bool Down)> Pending = new();

    private static void Send(string action, bool down) { if (InputMap.HasAction(action)) Pending.Add((action, down)); }

    private static void Flush()
    {
        if (Pending.Count == 0) return;
        var now = Pending.ToArray();
        Pending.Clear();
        foreach (var (a, d) in now) Input.ParseInputEvent(new InputEventAction { Action = a, Pressed = d, Strength = d ? 1f : 0f });
    }

    private void ReleaseAll()
    {
        foreach (var b in _btns)
            if (b.Finger >= 0) { b.Finger = -1; Send(b.Action, false); }
        _moveFinger = _attackFinger = -1;
        _dragging = false;
        Move = AimStick = Vector2.Zero;
    }

    public override void _Input(InputEvent e)
    {
        if (!Enabled) return;
        if (e is InputEventScreenTouch t)
        {
            // (a touch brings the controls back after a controller was in use)
            if (t.Pressed && G.Main != null && G.Main.UsingPad) G.Main.UsingPad = false;
            if (!Visible) return;
            if (t.Pressed) Press(t.Index, t.Position);
            else Release(t.Index);
            GetViewport().SetInputAsHandled();
        }
        else if (e is InputEventScreenDrag d && Visible)
        {
            Drag(d.Index, d.Position);
            GetViewport().SetInputAsHandled();
        }
    }

    private void Press(int finger, Vector2 at)
    {
        Btn hit = null;
        float best = float.MaxValue;
        foreach (var b in _btns)
        {
            if (!b.Show()) continue;
            float d = at.DistanceTo(b.At);
            if (d <= b.R * 1.2f && d < best) { best = d; hit = b; }
        }
        if (hit != null)
        {
            if (hit.Finger >= 0) return;
            hit.Finger = finger;
            Send(hit.Action, true);
            if (hit == _attack) { _attackFinger = finger; _attackAt = at; _dragging = false; }
            return;
        }
        if (_moveFinger < 0 && StickZone.HasPoint(at))
        {
            _moveFinger = finger;
            _moveOrigin = _moveAt = at;
            Move = Vector2.Zero;
        }
    }

    private void Drag(int finger, Vector2 at)
    {
        if (finger == _moveFinger)
        {
            var off = at - _moveOrigin;
            // (a thumb that strays past the rim drags the stick along with it)
            if (off.Length() > StickR) { _moveOrigin = at - off.Normalized() * StickR; off = at - _moveOrigin; }
            _moveAt = at;
            Move = Player.ShapeStick(off / StickR);
            return;
        }
        if (finger == _attackFinger)
        {
            _attackAt = at;
            var off = at - _attack.At;
            if (!_dragging && off.Length() > AttackDrag)
            {
                // dragged off the button: it becomes the right stick (the held attack lets go, the stick takes over)
                _dragging = true;
                Send("attack", false);
            }
            if (_dragging) AimStick = (off / (StickR * 0.8f)).LimitLength(1f);
        }
    }

    private void Release(int finger)
    {
        if (finger == _moveFinger) { _moveFinger = -1; Move = Vector2.Zero; }
        if (finger == _attackFinger) { _attackFinger = -1; AimStick = Vector2.Zero; }
        foreach (var b in _btns)
        {
            if (b.Finger != finger) continue;
            b.Finger = -1;
            if (!(b == _attack && _dragging)) Send(b.Action, false);
            if (b == _attack) _dragging = false;
        }
    }

    public override void _Draw()
    {
        var font = ThemeDB.FallbackFont;
        // the move stick: a faint hint where it can be touched, the ring and the knob while held
        if (_moveFinger >= 0)
        {
            DrawCircle(_moveOrigin, StickR, new Color(0, 0, 0, 0.22f));
            DrawArc(_moveOrigin, StickR, 0, Mathf.Tau, 48, new Color(1, 1, 1, 0.45f), 3f);
            DrawCircle(_moveOrigin + Move * StickR, 34, new Color(1, 1, 1, 0.45f));
        }
        else
        {
            // (a faint hint where it rests, clear of the HUD's ability squares in the corner)
            var c = new Vector2(430, 560);
            DrawArc(c, StickR, 0, Mathf.Tau, 48, new Color(1, 1, 1, 0.16f), 2f);
            DrawCircle(c, 30, new Color(1, 1, 1, 0.1f));
        }
        foreach (var b in _btns)
        {
            if (!b.Show()) continue;
            bool down = b.Finger >= 0;
            var fill = new Color(b.Tint.R * 0.25f, b.Tint.G * 0.25f, b.Tint.B * 0.25f, down ? 0.55f : 0.32f);
            DrawCircle(b.At, b.R, fill);
            DrawArc(b.At, b.R, 0, Mathf.Tau, 40, new Color(b.Tint, down ? 0.95f : 0.55f), down ? 4f : 2.5f);
            if (b.Text != "")
            {
                int size = (int)Math.Clamp(b.R * 0.42f * Math.Min(1f, 6f / Math.Max(1, b.Text.Length)), 9f, 22f);
                DrawString(font, b.At + new Vector2(-b.R, size * 0.35f), b.Text, HorizontalAlignment.Center, b.R * 2f, size, new Color(1, 1, 1, down ? 1f : 0.8f));
            }
        }
        // the attack dragged into aim: a line out from it, and the knob at the thumb
        if (_dragging && _attackFinger >= 0)
        {
            DrawLine(_attack.At, _attack.At + AimStick * StickR * 0.8f, new Color(1, 0.9f, 0.6f, 0.7f), 4f);
            DrawCircle(_attack.At + AimStick * StickR * 0.8f, 18, new Color(1, 0.9f, 0.6f, 0.6f));
        }
    }
}
