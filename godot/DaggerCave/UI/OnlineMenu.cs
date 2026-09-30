using System;
using System.Linq;
using Godot;

namespace DaggerCave;

/// <summary>
/// Playing online. First page: your name, "Host a game", or a friend's join code and "Join".
/// Then the lobby: the host's join codes to send (with Copy buttons), who's in, and a card for
/// each hero; each hero can be taken once. The host starts the descent for everyone.
/// </summary>
public partial class OnlineMenu : Control
{
    public Action Back, StartRun;

    private VBoxContainer _choose, _lobby;
    private LineEdit _name, _code;
    private Label _title, _status, _code1, _code2, _code3, _router, _players, _wait, _mismatch;
    private HBoxContainer _codeRow, _lanRow, _vpnRow;
    private Button _host, _start, _leave;
    private readonly Button[] _cards = new Button[5];
    private readonly Label[] _cardNote = new Label[5];
    private readonly HeroPortrait[] _portraits = new HeroPortrait[5];
    private string _note = "";
    private bool _wasOnline;
    private CheckBox _scaleOn, _hard;
    private HSlider _scale;
    private Label _scaleLabel;

    private static readonly (HeroKind kind, string name, string design)[] Heroes =
    {
        (HeroKind.Swordsman, "Swordsman", "swordsman"),
        (HeroKind.Warden, "Warden", "warden"),
        (HeroKind.Vitalist, "Vitalist", "vitalist"),
        (HeroKind.Elementalist, "Elementalist", "elementalist"),
        (HeroKind.Rogue, "Rogue", "rogue"),
    };

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        ProcessMode = ProcessModeEnum.Always;
        Theme = UiKit.Theme;
        Visible = false;
        AddChild(UiKit.Dimmer(0.8f));
        var (_, col) = UiKit.Panel(this, new Vector2(920, 0));
        _title = UiKit.Label("PLAY ONLINE", 28, UiKit.Gold, HorizontalAlignment.Center);
        col.AddChild(_title);

        // ---- first page: host, or join
        _choose = new VBoxContainer();
        _choose.AddThemeConstantOverride("separation", 14);
        col.AddChild(_choose);
        _choose.AddChild(UiKit.Label("Up to five players, one of each hero. One of you hosts; the others join with the host's code.", 15, UiKit.Dim, HorizontalAlignment.Center));

        var nameRow = Row(_choose);
        nameRow.AddChild(Fixed(UiKit.Label("Your name", 16), 150));
        _name = new LineEdit { MaxLength = 16, CustomMinimumSize = new Vector2(280, 38) };
        _name.TextChanged += t => GameSettings.PlayerName = t.Trim();
        nameRow.AddChild(_name);
        nameRow.AddChild(UiKit.Label("(what the others see)", 13, UiKit.Dim));

        _choose.AddChild(new HSeparator());
        var hostRow = Row(_choose);
        _host = UiKit.Button("Host a game", DoHost, 240);
        hostRow.AddChild(_host);
        hostRow.AddChild(UiKit.Label("Opens your game to friends and gives you a code to send them.", 14, UiKit.Dim));

        _choose.AddChild(new HSeparator());
        var joinRow = Row(_choose);
        joinRow.AddChild(Fixed(UiKit.Label("Join a friend", 16), 150));
        _code = new LineEdit { PlaceholderText = "their code, like 7K3QD-M2XP9", CustomMinimumSize = new Vector2(330, 38) };
        _code.TextSubmitted += _ => DoJoin();
        joinRow.AddChild(_code);
        joinRow.AddChild(UiKit.Button("Paste", () => { _code.Text = DisplayServer.ClipboardGet().Trim(); }, 100));
        joinRow.AddChild(UiKit.Button("Join", DoJoin, 120));

        _status = UiKit.Label("", 15, UiKit.Gold, HorizontalAlignment.Center);
        _status.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _status.CustomMinimumSize = new Vector2(860, 44);
        _choose.AddChild(_status);
        var backRow = Row(_choose);
        backRow.Alignment = BoxContainer.AlignmentMode.Center;
        backRow.AddChild(UiKit.Button("Back", () => Back?.Invoke(), 200));

        // ---- the lobby
        _lobby = new VBoxContainer();
        _lobby.AddThemeConstantOverride("separation", 10);
        col.AddChild(_lobby);
        _codeRow = Row(_lobby);
        _codeRow.AddChild(Fixed(UiKit.Label("Send your friend this code:", 16), 290));
        _code1 = UiKit.Label("", 26, UiKit.Gold);
        _codeRow.AddChild(Fixed(_code1, 230));
        _codeRow.AddChild(UiKit.Button("Copy", () => Copy(Net.JoinCode), 100));
        _lanRow = Row(_lobby);
        _lanRow.AddChild(Fixed(UiKit.Label("Same Wi-Fi or network:", 15, UiKit.Dim), 290));
        _code2 = UiKit.Label("", 20, UiKit.Text);
        _lanRow.AddChild(Fixed(_code2, 230));
        _lanRow.AddChild(UiKit.Button("Copy", () => Copy(Net.LanCode), 100));
        _vpnRow = Row(_lobby);
        _vpnRow.AddChild(Fixed(UiKit.Label("Your Tailscale (virtual LAN) address:", 15, UiKit.Dim), 290));
        _code3 = UiKit.Label("", 20, UiKit.Text);
        _vpnRow.AddChild(Fixed(_code3, 230));
        _vpnRow.AddChild(UiKit.Button("Copy", () => Copy(Net.VpnIp), 100));
        _router = UiKit.Label("", 13, UiKit.Dim);
        _router.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _router.CustomMinimumSize = new Vector2(860, 0);
        _lobby.AddChild(_router);
        _lobby.AddChild(new HSeparator());
        _players = UiKit.Label("", 16);
        _lobby.AddChild(_players);
        _mismatch = UiKit.Label("", 13, new Color(1f, 0.65f, 0.45f));
        _mismatch.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _mismatch.CustomMinimumSize = new Vector2(860, 0);
        _mismatch.Visible = false;
        _lobby.AddChild(_mismatch);

        // the run's difficulty (the host's to set; everyone sees it)
        var scaleRow = Row(_lobby);
        _scaleOn = new CheckBox { Text = "Scale difficulty with players", TooltipText = "Enemies get tougher with each extra player: health and damage grow by the meter's amount." };
        _scaleOn.Toggled += on => { RunSettings.ScaleWithPlayers = on; Net.SettingsChanged(); Refresh(); };
        scaleRow.AddChild(Fixed(_scaleOn, 300));
        _scale = new HSlider { MinValue = 1, MaxValue = 3, Step = 0.05, CustomMinimumSize = new Vector2(260, 28), SizeFlagsVertical = SizeFlags.ShrinkCenter };
        _scale.ValueChanged += v => { RunSettings.Scale = (float)v; Net.SettingsChanged(); Refresh(); };
        scaleRow.AddChild(_scale);
        _scaleLabel = UiKit.Label("", 14, UiKit.Dim);
        scaleRow.AddChild(_scaleLabel);
        var hardRow = Row(_lobby);
        _hard = new CheckBox { Text = "Hard Mode: enemies far tougher", TooltipText = "Doubles enemy health and raises their damage by half, on top of everything else." };
        _hard.Toggled += on => { RunSettings.Hard = on; Net.SettingsChanged(); Refresh(); };
        hardRow.AddChild(_hard);

        var cards = Row(_lobby);
        cards.Alignment = BoxContainer.AlignmentMode.Center;
        cards.AddThemeConstantOverride("separation", 10);
        for (int k = 0; k < Heroes.Length; k++)
        {
            var (kind, name, design) = Heroes[k];
            var portrait = new HeroPortrait { Design = design, Size = new Vector2I(150, 200) };
            AddChild(portrait);
            _portraits[k] = portrait;
            var card = new Button { CustomMinimumSize = new Vector2(166, 214), FocusMode = FocusModeEnum.All };
            card.Pressed += () => { G.Sfx?.Play("ui", null, -6); Net.PickHero(kind); };
            var v = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
            v.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            v.OffsetTop = 6; v.OffsetBottom = -6;
            card.AddChild(v);
            var pic = new TextureRect
            {
                Texture = portrait.GetTexture(), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, SizeFlagsVertical = SizeFlags.ExpandFill,
                MouseFilter = MouseFilterEnum.Ignore,
            };
            v.AddChild(pic);
            var label = UiKit.Label(name.ToUpperInvariant(), 15, Hud.HeroColor(kind), HorizontalAlignment.Center);
            label.MouseFilter = MouseFilterEnum.Ignore;
            v.AddChild(label);
            _cardNote[k] = UiKit.Label("", 13, UiKit.Dim, HorizontalAlignment.Center);
            _cardNote[k].MouseFilter = MouseFilterEnum.Ignore;
            v.AddChild(_cardNote[k]);
            cards.AddChild(card);
            _cards[k] = card;
        }
        _wait = UiKit.Label("", 15, UiKit.Dim, HorizontalAlignment.Center);
        _lobby.AddChild(_wait);
        var buttons = Row(_lobby);
        buttons.Alignment = BoxContainer.AlignmentMode.Center;
        _start = UiKit.Button("Start the descent", () => StartRun?.Invoke(), 280);
        buttons.AddChild(_start);
        _leave = UiKit.Button("Leave", DoLeave, 180);
        buttons.AddChild(_leave);

        Net.Changed += OnNetChanged;
    }

    public override void _ExitTree() => Net.Changed -= OnNetChanged;

    private static HBoxContainer Row(Container parent)
    {
        var r = new HBoxContainer();
        r.AddThemeConstantOverride("separation", 12);
        parent.AddChild(r);
        return r;
    }

    private static Control Fixed(Control c, float w)
    {
        c.CustomMinimumSize = new Vector2(w, c.CustomMinimumSize.Y);
        return c;
    }

    /// <summary>Shows the menu (the lobby, if already in a game), with a note (why the last game ended).</summary>
    public void Open(string note = "")
    {
        _note = note ?? "";
        Visible = true;
        _name.Text = GameSettings.PlayerName;
        _name.PlaceholderText = Net.MyName;
        // a join code just copied from a chat fills itself in (then it's one click on Join)
        string clip = DisplayServer.GetName() == "headless" ? "" : DisplayServer.ClipboardGet().Trim();
        if (LooksLikeCode(clip)) _code.Text = clip;
        else if (_code.Text == "") _code.Text = GameSettings.LastJoin;
        _wasOnline = !(Net.Online && Net.Mine != null); // (so the focus is placed for the page shown)
        Refresh();
    }

    private void OnNetChanged()
    {
        if (!Visible) return;
        // the connection's own news replaces an old note
        if (Net.Online || Net.Status != "") _note = "";
        Refresh();
    }

    private void DoHost()
    {
        GameSettings.Save();
        _note = "";
        Net.Host();
        Refresh();
    }

    private void DoJoin()
    {
        string code = _code.Text.Trim();
        _note = "";
        if (code == "")
        {
            _note = "Type (or paste) the code your friend's game shows them, then Join.";
            Refresh();
            return;
        }
        GameSettings.LastJoin = code;
        GameSettings.Save();
        Net.Join(code);
        Refresh();
    }

    private void DoLeave()
    {
        Net.Leave();
        _note = "";
        Refresh();
    }

    private void Copy(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        DisplayServer.ClipboardSet(text);
        _note = $"Copied {text}";
        G.Sfx?.Play("ui", null, -4, 0, 1.3f);
        Refresh();
    }

    private static string HeroName(HeroKind h) => Heroes.First(x => x.kind == h).name;

    /// <summary>Ten letters and digits, maybe split by a dash: a join code (and not just any text on the clipboard).</summary>
    private static bool LooksLikeCode(string s)
    {
        s = s.Replace("-", "").Replace(" ", "");
        return s.Length == 10 && s.All(char.IsLetterOrDigit) && Net.ReadCode(s, out _, out _);
    }

    private void Refresh()
    {
        bool online = Net.Online;
        bool joined = online && Net.Mine != null;
        _choose.Visible = !joined;
        _lobby.Visible = joined;
        bool pageChanged = _wasOnline != joined;
        _wasOnline = joined;
        if (!joined)
        {
            _title.Text = "PLAY ONLINE";
            _status.Text = _note != "" ? _note : Net.Status;
            _host.Disabled = online;
            if (pageChanged) _host.CallDeferred(Control.MethodName.GrabFocus);
            return;
        }

        // ---- the lobby
        if (Net.IsHost)
        {
            _title.Text = "YOUR GAME";
            bool internet = Net.JoinCode != "";
            _codeRow.Visible = internet || Net.UpnpChecking;
            _code1.Text = internet ? Net.JoinCode : "...";
            _lanRow.Visible = Net.LanCode != "";
            _code2.Text = Net.LanCode;
            _vpnRow.Visible = Net.VpnIp != "";
            _code3.Text = Net.VpnIp;
            string lan = Net.LanCode != "" ? "Friends on your own network can use the second code. " : "";
            string router = Net.UpnpChecking ? "Asking your router to let friends in..."
                : Net.BehindCgnat ? "Your router let friends in, but your internet provider shares one internet address among many homes, "
                  + "so the code can't reach you from outside. " + lan + "For friends elsewhere, all of you install a free virtual LAN "
                  + "(Tailscale or ZeroTier) and they type your address from it instead of a code."
                : Net.UpnpOk ? "Your router opened the way by itself: friends can join from anywhere with the code."
                : "Your router didn't open the way by itself, so the code may not reach you from outside. " + lan
                  + $"For friends elsewhere: forward UDP port {Net.Port} to this computer in your router's settings, or all of you install "
                  + "a free virtual LAN (Tailscale, ZeroTier) and they type your address from it instead of a code.";
            _router.Text = _note != "" ? _note : router;
        }
        else
        {
            var host = Net.Peer(1);
            _title.Text = host != null ? $"{host.Name.ToUpperInvariant()}'S GAME" : "JOINING";
            _codeRow.Visible = _lanRow.Visible = _vpnRow.Visible = false;
            _router.Text = _note != "" ? _note : "You're in. Pick a hero; the host starts the descent for everyone.";
        }
        var lines = Net.Peers.Values.Select(p => $"{p.Name}{(p.Id == Net.Me ? " (you)" : "")}{(p.Id == 1 ? ", hosting" : "")}   ·   {(p.Picked ? HeroName(p.Hero) : "choosing a hero...")}");
        _players.Text = string.Join("\n", lines) + (Net.Count < Net.MaxPlayers && Net.IsHost ? "\n(room for " + (Net.MaxPlayers - Net.Count) + " more)" : "");
        // different copies of the game still connect, but may not build the same caves
        var odd = Net.Peers.Values.Where(p => p.Build != "" && p.Build != Net.Build).Select(p => p.Name).ToList();
        _mismatch.Visible = odd.Count > 0;
        if (odd.Count > 0)
            _mismatch.Text = $"Heads-up: {string.Join(" and ", odd)} {(odd.Count > 1 ? "are" : "is")} running a different copy of the game than you. "
                           + "If things go strange (creatures in the rock, chests that won't open), everyone should use the same download.";
        for (int k = 0; k < Heroes.Length; k++)
        {
            var owner = Net.TakenBy(Heroes[k].kind);
            bool mine = owner != null && owner.Id == Net.Me;
            _cards[k].Disabled = owner != null && !mine;
            _cardNote[k].Text = mine ? "YOU" : owner != null ? $"taken by {owner.Name}" : "free: click to take";
            _cardNote[k].AddThemeColorOverride("font_color", mine ? UiKit.Gold : UiKit.Dim);
            _portraits[k].Playing = mine;
        }
        _scaleOn.SetPressedNoSignal(RunSettings.ScaleWithPlayers);
        _scale.SetValueNoSignal(RunSettings.Scale);
        _hard.SetPressedNoSignal(RunSettings.Hard);
        _scaleOn.Disabled = !Net.IsHost;
        _scale.Editable = Net.IsHost && RunSettings.ScaleWithPlayers;
        _hard.Disabled = !Net.IsHost;
        int extra = Math.Max(0, Net.Count - 1);
        _scaleLabel.Text = !RunSettings.ScaleWithPlayers ? "no scaling"
            : $"x{RunSettings.Scale:0.00}   ·   +{(RunSettings.Scale - 1) * 100:0}% health, +{(RunSettings.Scale - 1) * 20:0}% damage per extra player" + (extra > 0 ? $"  (now x{RunSettings.HpMult / (RunSettings.Hard ? Tune.Difficulty.HardHp : 1f):0.0#} / x{RunSettings.DmgMult / (RunSettings.Hard ? Tune.Difficulty.HardDamage : 1f):0.0#})" : "");
        _start.Visible = Net.IsHost;
        _start.Disabled = !Net.AllPicked;
        _start.Text = Net.Count > 1 ? "Start the descent" : "Start (on your own for now)";
        _wait.Text = Net.IsHost
            ? (!Net.AllPicked ? "Waiting for everyone to have a hero..." : Net.Count > 1 ? "Everyone has a hero. Start when you're ready." : "Waiting for friends to join (they'll show up here).")
            : "Waiting for the host to start...";
        if (pageChanged) (Net.IsHost ? _start : _cards.FirstOrDefault(c => !c.Disabled) ?? _leave).CallDeferred(Control.MethodName.GrabFocus);
    }

    public override void _Process(double delta)
    {
        bool show = Visible && _lobby.Visible;
        foreach (var p in _portraits)
            if (p != null) p.RenderTargetUpdateMode = show ? SubViewport.UpdateMode.Always : SubViewport.UpdateMode.Disabled;
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (!Visible || !e.IsActionPressed("ui_cancel")) return;
        GetViewport().SetInputAsHandled();
        if (_lobby.Visible) DoLeave();
        else Back?.Invoke();
    }
}
