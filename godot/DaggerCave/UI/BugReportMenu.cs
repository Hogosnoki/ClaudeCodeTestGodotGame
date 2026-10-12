using System;
using Godot;

namespace DaggerCave;

/// <summary>
/// "Report a problem": a box for what went wrong, a category, and the facts the game adds by itself (the seed, the cave, the hero, where
/// they stand, the map, the settings: see <see cref="BugReports"/>). Sending saves the report, copies it, and opens the new GitHub issue,
/// ready to post, in the browser.
/// </summary>
public partial class BugReportMenu : Control
{
    public Action Closed;
    private Label _facts, _result;
    private Cycler _category;
    private TextEdit _text;
    private HBoxContainer _buttons, _done;
    private Label _prompt;
    private Button _send;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        ProcessMode = ProcessModeEnum.Always;
        Theme = UiKit.Theme;
        Visible = false;
        AddChild(UiKit.Dimmer(0.65f));
        var (_, col) = UiKit.Panel(this, new Vector2(700, 0));
        col.AddChild(UiKit.Label("REPORT A PROBLEM", 26, UiKit.Gold, HorizontalAlignment.Center));
        _facts = UiKit.Label("", 14, UiKit.Dim);
        _facts.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _facts.CustomMinimumSize = new Vector2(660, 0);
        col.AddChild(_facts);
        _category = new Cycler { Options = BugReports.Categories, Index = 0, CustomMinimumSize = new Vector2(660, 36) };
        col.AddChild(_category);
        _prompt = UiKit.Label("What happened? (Where you were, what you did, what you expected.)", 15);
        col.AddChild(_prompt);
        _text = new TextEdit { CustomMinimumSize = new Vector2(660, 170), PlaceholderText = "Type here…", WrapMode = TextEdit.LineWrappingMode.Boundary };
        col.AddChild(_text);
        _result = UiKit.Label("", 14, new Color(0.7f, 0.95f, 0.7f));
        _result.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _result.CustomMinimumSize = new Vector2(660, 0);
        _result.Visible = false;
        col.AddChild(_result);
        _buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        _buttons.AddThemeConstantOverride("separation", 16);
        col.AddChild(_buttons);
        _send = UiKit.Button("Send report", Send, 220);
        _buttons.AddChild(_send);
        _buttons.AddChild(UiKit.Button("Cancel", Close, 160));
        _done = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, Visible = false };
        col.AddChild(_done);
        _done.AddChild(UiKit.Button("Open the page again", () => { if (_last != null && BugReports.OpenBrowser) OS.ShellOpen(_last.Url); }, 240));
        _done.AddChild(UiKit.Button("Close", Close, 160));
    }

    private BugReports.Report _last;

    public void Open()
    {
        _facts.Text = $"Included automatically: {BugReports.Summary()}, where you stand, the map round you and the whole of it, whether you are shut in, your settings and the end of the game's log.";
        _text.Text = "";
        _result.Visible = false; _done.Visible = false; _buttons.Visible = true; _text.Visible = true; _category.Visible = true; _prompt.Visible = true;
        _last = null;
        Visible = true;
        _text.CallDeferred(Control.MethodName.GrabFocus);
    }

    private void Send()
    {
        _last = BugReports.Make(BugReports.Categories[Math.Clamp(_category.Index, 0, BugReports.Categories.Length - 1)], _text.Text);
        _result.Text = "Thank you! Your browser opened with the report ready on GitHub: press \"Submit new issue\" there (you need a GitHub account). "
            + "The whole report is also on your clipboard (paste it into the issue, or into a message) and saved at:\n" + _last.Path;
        _result.Visible = true;
        _buttons.Visible = false; _text.Visible = false; _category.Visible = false; _prompt.Visible = false;
        _done.Visible = true;
        (_done.GetChild(1) as Control)?.CallDeferred(Control.MethodName.GrabFocus);
    }

    private void Close()
    {
        Visible = false;
        Closed?.Invoke();
    }

    public override void _Input(InputEvent e)
    {
        if (!Visible) return;
        if (e is InputEventKey { Pressed: true, Echo: false, PhysicalKeycode: Key.Escape }) { GetViewport().SetInputAsHandled(); Close(); }
    }
}
