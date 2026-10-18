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
    /// <summary>Opens the Ember Grid at a hero's branch / their loadout (the menus themselves live in Main).</summary>
    public Action<HeroKind> Grid, Loadout;
    /// <summary>True while one of those menus is up over this one (it has the keys then).</summary>
    public Func<bool> Covered;

    private VBoxContainer _choose, _lobby;
    private LineEdit _name, _code;
    private Label _title, _status, _code1, _code2, _code3, _router, _players, _wait, _mismatch;
    private HBoxContainer _codeRow, _lanRow, _vpnRow;
    private VBoxContainer _prep;
    private Label _prepTitle, _prepDesc, _prepBrought;
    private Button _prepGrid, _prepLoadout, _prepBack, _prepBtn;
    /// <summary>The lobby's second stage: after taking a hero, the Ember Grid and their loadout.</summary>
    private bool _inPrep;
    private HeroKind _prepHero;
    private Control _prepFocus;
    private Button _host, _start, _leave, _paste, _joinBtn, _backBtn, _copy1, _copy2, _copy3;
    private readonly Button[] _cards = new Button[Heroes.Length];
    private readonly Label[] _cardNote = new Label[Heroes.Length];
    private readonly HeroPortrait[] _portraits = new HeroPortrait[Heroes.Length];
    private string _note = "";
    private bool _wasOnline;
    private CheckBox _hard;
    private HSlider _diff, _perPlayer;
    private Label _diffLabel, _perLabel, _strengthLabel;

    private static readonly (HeroKind kind, string name, string design)[] Heroes =
    {
        (HeroKind.Swordsman, "Swordsman", "swordsman"),
        (HeroKind.Warden, "Warden", "warden"),
        (HeroKind.Vitalist, "Vitalist", "vitalist"),
        (HeroKind.Elementalist, "Elementalist", "elementalist"),
        (HeroKind.Rogue, "Rogue", "rogue"),
        (HeroKind.Aegis, "Aegis", "aegis"),
        (HeroKind.ShapeShifter, "Shifter", "shapeshifter"),
        (HeroKind.Automaton, "Automaton", "automaton_hero"),
    };

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        ProcessMode = ProcessModeEnum.Always;
        Theme = UiKit.Theme;
        Visible = false;
        AddChild(UiKit.Dimmer(0.8f));
        var (panel, col) = UiKit.Panel(this, new Vector2(1220, 0));
        _panel = panel;
        _title = UiKit.Label("PLAY ONLINE", 28, UiKit.Gold, HorizontalAlignment.Center);
        col.AddChild(_title);

        // ---- first page: host, or join
        _choose = new VBoxContainer();
        _choose.AddThemeConstantOverride("separation", 14);
        col.AddChild(_choose);
        _choose.AddChild(UiKit.Label("Up to six players, one of each hero. One of you hosts; the others join with the host's code.", 15, UiKit.Dim, HorizontalAlignment.Center));

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
        _paste = UiKit.Button("Paste", () => { _code.Text = DisplayServer.ClipboardGet().Trim(); }, 100);
        joinRow.AddChild(_paste);
        _joinBtn = UiKit.Button("Join", DoJoin, 120);
        joinRow.AddChild(_joinBtn);

        _status = UiKit.Label("", 15, UiKit.Gold, HorizontalAlignment.Center);
        _status.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _status.CustomMinimumSize = new Vector2(860, 44);
        _choose.AddChild(_status);
        var backRow = Row(_choose);
        backRow.Alignment = BoxContainer.AlignmentMode.Center;
        _backBtn = UiKit.Button("Back", () => Back?.Invoke(), 200);
        backRow.AddChild(_backBtn);

        // ---- the lobby
        // (the lobby's settings and hero cards on the left; on the right, the hero under the cursor: a portrait, how they play, their abilities)
        var lobbyRow = new HBoxContainer();
        lobbyRow.AddThemeConstantOverride("separation", 18);
        col.AddChild(lobbyRow);
        _lobby = new VBoxContainer { CustomMinimumSize = new Vector2(850, 0) };
        _lobby.AddThemeConstantOverride("separation", 5);
        lobbyRow.AddChild(_lobby);
        _side = BuildSide();
        lobbyRow.AddChild(_side);
        _codeRow = Row(_lobby);
        _codeRow.AddChild(Fixed(UiKit.Label("Send your friend this code:", 16), 290));
        _code1 = UiKit.Label("", 26, UiKit.Gold);
        _codeRow.AddChild(Fixed(_code1, 230));
        _copy1 = UiKit.Button("Copy", () => Copy(Net.JoinCode), 100);
        _codeRow.AddChild(_copy1);
        _lanRow = Row(_lobby);
        _lanRow.AddChild(Fixed(UiKit.Label("Same Wi-Fi or network:", 15, UiKit.Dim), 290));
        _code2 = UiKit.Label("", 20, UiKit.Text);
        _lanRow.AddChild(Fixed(_code2, 230));
        _copy2 = UiKit.Button("Copy", () => Copy(Net.LanCode), 100);
        _lanRow.AddChild(_copy2);
        _vpnRow = Row(_lobby);
        _vpnRow.AddChild(Fixed(UiKit.Label("Your Tailscale (virtual LAN) address:", 15, UiKit.Dim), 290));
        _code3 = UiKit.Label("", 20, UiKit.Text);
        _vpnRow.AddChild(Fixed(_code3, 230));
        _copy3 = UiKit.Button("Copy", () => Copy(Net.VpnIp), 100);
        _vpnRow.AddChild(_copy3);
        _router = UiKit.Label("", 13, UiKit.Dim);
        _router.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _router.CustomMinimumSize = new Vector2(840, 0);
        _lobby.AddChild(_router);
        _lobby.AddChild(new HSeparator());
        _players = UiKit.Label("", 14);
        _lobby.AddChild(_players);
        _mismatch = UiKit.Label("", 13, new Color(1f, 0.65f, 0.45f));
        _mismatch.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _mismatch.CustomMinimumSize = new Vector2(840, 0);
        _mismatch.Visible = false;
        _lobby.AddChild(_mismatch);

        // the run's difficulty (the host's to set; everyone sees it)
        var diffRow = Row(_lobby);
        diffRow.AddChild(Fixed(UiKit.Label("Difficulty", 16), 300));
        _diff = new HSlider { MinValue = RunSettings.DifficultyMin, MaxValue = RunSettings.DifficultyMax, Step = 0.05, CustomMinimumSize = new Vector2(260, 28), SizeFlagsVertical = SizeFlags.ShrinkCenter };
        _diff.ValueChanged += v => { RunSettings.Difficulty = (float)v; Net.SettingsChanged(); Refresh(); };
        diffRow.AddChild(_diff);
        _diffLabel = UiKit.Label("", 14, UiKit.Dim);
        diffRow.AddChild(_diffLabel);
        var perRow = Row(_lobby);
        perRow.AddChild(Fixed(UiKit.Label("Per-player difficulty", 16), 300));
        _perPlayer = new HSlider { MinValue = RunSettings.PerPlayerMin, MaxValue = RunSettings.PerPlayerMax, Step = 0.05, CustomMinimumSize = new Vector2(260, 28), SizeFlagsVertical = SizeFlags.ShrinkCenter };
        _perPlayer.ValueChanged += v => { RunSettings.PerPlayer = (float)v; Net.SettingsChanged(); Refresh(); };
        perRow.AddChild(_perPlayer);
        _perLabel = UiKit.Label("", 14, UiKit.Dim);
        perRow.AddChild(_perLabel);
        _strengthLabel = UiKit.Label("", 14, UiKit.Gold);
        _lobby.AddChild(_strengthLabel);
        var hardRow = Row(_lobby);
        _hard = new CheckBox { Text = "Hard Mode: enemies far tougher", TooltipText = "Doubles enemy health and raises their damage by half, on top of everything else." };
        _hard.Toggled += on => { RunSettings.Hard = on; Net.SettingsChanged(); Refresh(); };
        hardRow.AddChild(_hard);

        // (the heroes in two rows of four)
        HBoxContainer cards = null;
        for (int k = 0; k < Heroes.Length; k++)
        {
            if (k % 4 == 0)
            {
                cards = Row(_lobby);
                cards.Alignment = BoxContainer.AlignmentMode.Center;
                cards.AddThemeConstantOverride("separation", 10);
            }
            int at = k;
            var (kind, name, design) = Heroes[k];
            var portrait = new HeroPortrait { Design = design, Size = new Vector2I(84, 132) };
            AddChild(portrait);
            _portraits[k] = portrait;
            var card = new Button { CustomMinimumSize = new Vector2(140, 160), FocusMode = FocusModeEnum.All };
            card.Pressed += () => { G.Sfx?.Play("ui", null, -6); Net.PickHero(kind); EnterPrep(kind); };
            card.FocusEntered += () => ShowSide(at);
            card.MouseEntered += () => ShowSide(at);
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
        _prepBtn = UiKit.Button("Grid & loadout", () => EnterPrep(Net.Mine != null && Net.Mine.Picked ? Net.Mine.Hero : G.Hero), 220);
        buttons.AddChild(_prepBtn);
        _start = UiKit.Button("Start the descent", () => StartRun?.Invoke(), 280);
        buttons.AddChild(_start);
        _leave = UiKit.Button("Leave", DoLeave, 180);
        buttons.AddChild(_leave);

        // ---- the second stage: the hero taken, the Ember Grid and their loadout
        _prep = new VBoxContainer { Visible = false };
        _prep.AddThemeConstantOverride("separation", 12);
        col.AddChild(_prep);
        _prepTitle = UiKit.Label("", 26, UiKit.Gold, HorizontalAlignment.Center);
        _prep.AddChild(_prepTitle);
        _prepDesc = UiKit.Label("", 15, UiKit.Text, HorizontalAlignment.Center);
        _prepDesc.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _prepDesc.CustomMinimumSize = new Vector2(760, 0);
        _prep.AddChild(_prepDesc);
        _prepBrought = UiKit.Label("", 15, new Color(1f, 0.85f, 0.55f), HorizontalAlignment.Center);
        _prepBrought.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _prepBrought.CustomMinimumSize = new Vector2(760, 0);
        _prep.AddChild(_prepBrought);
        var prow = Row(_prep);
        prow.Alignment = BoxContainer.AlignmentMode.Center;
        _prepGrid = UiKit.Button("The Ember Grid", () => Grid?.Invoke(_prepHero), 220);
        prow.AddChild(_prepGrid);
        _prepLoadout = UiKit.Button("Loadout (side-grades)", () => Loadout?.Invoke(_prepHero), 220);
        prow.AddChild(_prepLoadout);
        var brow = Row(_prep);
        brow.Alignment = BoxContainer.AlignmentMode.Center;
        _prepBack = UiKit.Button("Done: back to the lobby", BackToLobby, 300);
        _prepBack.AddThemeColorOverride("font_color", UiKit.Gold);
        brow.AddChild(_prepBack);

        Net.Changed += OnNetChanged;
    }

    /// <summary>A hero taken (or "Grid & loadout"): on to the Ember Grid and their loadout.</summary>
    private void EnterPrep(HeroKind hero)
    {
        _prepHero = hero;
        G.Hero = hero;
        _inPrep = true;
        _prepFocus = null;
        Refresh();
        _prepGrid.CallDeferred(Control.MethodName.GrabFocus);
    }

    /// <summary>Back from the second stage to the lobby proper (the cursor on your hero's card).</summary>
    private void BackToLobby()
    {
        _inPrep = false;
        _prepFocus = null;
        Refresh();
        int at = Array.FindIndex(Heroes, h => h.kind == _prepHero);
        (at >= 0 && !_cards[at].Disabled ? _cards[at] : _start.Visible ? _start : _leave).CallDeferred(Control.MethodName.GrabFocus);
    }

    /// <summary>The Ember Grid or the loadout closed: the second stage again, with what changed.</summary>
    public void AfterSubmenu()
    {
        if (!Visible) return;
        Refresh();
        if (_inPrep)
        {
            var target = _prepFocus != null && IsInstanceValid(_prepFocus) && _prepFocus.IsVisibleInTree() ? _prepFocus : _prepGrid;
            target.CallDeferred(Control.MethodName.GrabFocus);
        }
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
        _inPrep = false;
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

    // ---------------------------------------------------------------- the hero under the cursor

    private VBoxContainer _side;
    private TextureRect _sidePic;
    private Label _sideName, _sidePlay, _sideAbilities;
    private readonly HeroPortrait[] _bigPortraits = new HeroPortrait[Heroes.Length];
    private int _sideAt = -1;
    /// <summary>For the tests: the hero the side panel shows.</summary>
    public HeroKind SideHero => _sideAt >= 0 ? Heroes[_sideAt].kind : G.Hero;

    private VBoxContainer BuildSide()
    {
        var side = new VBoxContainer { CustomMinimumSize = new Vector2(320, 0) };
        side.AddThemeConstantOverride("separation", 6);
        for (int k = 0; k < Heroes.Length; k++)
        {
            _bigPortraits[k] = new HeroPortrait { Design = Heroes[k].design, Size = new Vector2I(220, 300), Playing = true, RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled };
            AddChild(_bigPortraits[k]);
        }
        _sidePic = new TextureRect { CustomMinimumSize = new Vector2(320, 250), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered };
        side.AddChild(_sidePic);
        _sideName = UiKit.Label("", 24, UiKit.Gold, HorizontalAlignment.Center);
        side.AddChild(_sideName);
        _sidePlay = UiKit.Label("", 14, UiKit.Text);
        _sidePlay.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _sidePlay.CustomMinimumSize = new Vector2(320, 0);
        side.AddChild(_sidePlay);
        side.AddChild(new HSeparator());
        _sideAbilities = UiKit.Label("", 12, UiKit.Dim);
        _sideAbilities.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _sideAbilities.CustomMinimumSize = new Vector2(320, 0);
        side.AddChild(_sideAbilities);
        return side;
    }

    /// <summary>The side panel shows a hero: their portrait (moving), how they play, and what each of their abilities does.</summary>
    private void ShowSide(int k)
    {
        if (k < 0 || k >= Heroes.Length) return;
        _sideAt = k;
        var h = Heroes[k].kind;
        for (int j = 0; j < _bigPortraits.Length; j++) _bigPortraits[j].RenderTargetUpdateMode = j == k ? SubViewport.UpdateMode.Always : SubViewport.UpdateMode.Disabled;
        _sidePic.Texture = _bigPortraits[k].GetTexture();
        var (name, lines) = ScreenOverlay.HeroInfo(h);
        _sideName.Text = name;
        _sideName.AddThemeColorOverride("font_color", Hud.HeroColor(h).Lightened(0.3f));
        _sidePlay.Text = string.Join(" ", lines) + (Meta.IsUnlocked(h) ? "" : "  (Not found yet: freed from a cage in the caves.)");
        _sideAbilities.Text = string.Join("\n", Upgrades.Abilities(h).Select(a => $"{a.name}: {AbilityText(a.key, h)}").Append($"Support: {Hud.AbilityTip(SupportOf(h)).body}"));
    }

    private static string SupportOf(HeroKind h) => h switch
    {
        HeroKind.Swordsman => "SHOUT", HeroKind.Warden => "TAUNT", HeroKind.Vitalist => "TAP", HeroKind.Elementalist => "STALAG",
        HeroKind.Rogue => "EXPOSE", HeroKind.Aegis => "SHARE", HeroKind.Automaton => "PRESSURIZE", _ => "HOWL",
    };

    /// <summary>What an ability does, in a line (the HUD's own tooltips where they say it, a line of its own where they don't).</summary>
    private static string AbilityText(string key, HeroKind h) => key switch
    {
        "sword" => h == HeroKind.Warden ? "a quick shortsword combo behind the shield." : "a heavy blade swung in a combo; the last strike hits hardest.",
        "dodge" => Hud.AbilityTip("DASHSLASH").body.Split(". ")[0] + ".",
        "shield" => "raise it to stop blows from the front; it wears down and mends while lowered.",
        "drain" => "a beam that draws health from a creature and fills your vital force.",
        "bolt" => "fiery bolts from the staff that burst and set creatures alight.",
        "blizzard" => Hud.AbilityTip("STORM").body,
        "dagger" => "fast dagger slashes, deadlier from behind and from the shadows.",
        "throw" => "throw your daggers; they stick in walls and wait to be recalled.",
        "ward" => "a bolt of light that bursts on the creatures it strikes and weakens them.",
        "staff" => "a staff swung when you wear no form.",
        "form" => "each creature you become fights as it does, with your health and strength.",
        "special" => "every form has a trick of its own (a bear's roar, a spider's web...).",
        "cleaver" => "a slow, crushing cleaver; a full swing slams the ground.",
        _ => Hud.AbilityTip(key.ToUpperInvariant()).body,
    };

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
        if (!joined) _inPrep = false;
        _choose.Visible = !joined;
        _lobby.Visible = joined && !_inPrep;
        _side.Visible = _lobby.Visible;
        if (_side.Visible && _sideAt < 0) ShowSide(Math.Max(0, Array.FindIndex(Heroes, h => h.kind == (Net.Mine != null && Net.Mine.Picked ? Net.Mine.Hero : G.Hero))));
        _prep.Visible = joined && _inPrep;
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

        // ---- the second stage
        if (_inPrep)
        {
            var (hname, hlines) = ScreenOverlay.HeroInfo(_prepHero);
            _title.Text = "GET READY";
            _prepTitle.Text = hname;
            _prepTitle.AddThemeColorOverride("font_color", Hud.HeroColor(_prepHero).Lightened(0.3f));
            _prepDesc.Text = string.Join(" ", hlines);
            _prepBrought.Text = HexGrid.Summary(_prepHero) + $"\nLoadout: {Meta.LoadoutFor(_prepHero).Count}/{Tune.Loadout.Slots} side-grades";
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
            var owners = Net.Peers.Values.Where(pp => pp.Picked && pp.Hero == Heroes[k].kind).ToList();
            bool mine = owners.Any(pp => pp.Id == Net.Me);
            bool have = Meta.IsUnlocked(Heroes[k].kind);
            _cards[k].Disabled = !have && !mine;
            var others = owners.Where(pp => pp.Id != Net.Me).Select(pp => pp.Name).ToList();
            _cardNote[k].Text = mine ? (others.Count > 0 ? "YOU  +  " + string.Join(", ", others) : "YOU") : others.Count > 0 ? string.Join(", ", others) : !have ? "not found yet" : "click to take";
            _cardNote[k].AddThemeColorOverride("font_color", mine ? UiKit.Gold : UiKit.Dim);
            _portraits[k].Playing = mine;
        }
        _diff.SetValueNoSignal(RunSettings.Difficulty);
        _perPlayer.SetValueNoSignal(RunSettings.PerPlayer);
        _hard.SetPressedNoSignal(RunSettings.Hard);
        _diff.Editable = _perPlayer.Editable = Net.IsHost;
        _hard.Disabled = !Net.IsHost;
        _diffLabel.Text = $"x{RunSettings.Difficulty:0.00}";
        _perLabel.Text = $"x{RunSettings.PerPlayer:0.00}  each";
        _strengthLabel.Text = $"{RunSettings.Difficulty:0.##} x ({Math.Max(1, Net.Count)} players x {RunSettings.PerPlayer:0.##})  =  enemies x{RunSettings.HpMult:0.0#} health, x{RunSettings.DmgMult:0.0#} damage";
        _start.Visible = Net.IsHost;
        _start.Disabled = !Net.AllPicked;
        _start.Text = Net.Count > 1 ? "Start the descent" : "Start (on your own for now)";
        _wait.Text = Net.IsHost
            ? (!Net.AllPicked ? "Waiting for everyone to have a hero..." : Net.Count > 1 ? "Everyone has a hero. Start when you're ready." : "Waiting for friends to join (they'll show up here).")
            : "Waiting for the host to start...";
        // (the cursor starts on your own hero's card: left and right from there reach the others)
        if (pageChanged)
        {
            int mineAt = Array.FindIndex(Heroes, h => Net.Mine != null && Net.Mine.Picked && Net.Mine.Hero == h.kind);
            var first = mineAt >= 0 && !_cards[mineAt].Disabled ? _cards[mineAt] : _cards.FirstOrDefault(c => !c.Disabled);
            (first ?? (Net.IsHost ? _start : _leave)).CallDeferred(Control.MethodName.GrabFocus);
        }
    }

    private PanelContainer _panel;

    public override void _Process(double delta)
    {
        // (the lobby with six heroes is tall: shrink the whole panel to sit inside the screen)
        if (Visible && _panel != null)
        {
            float vh = GetViewportRect().Size.Y, h = Math.Max(1f, _panel.Size.Y);
            _panel.PivotOffset = _panel.Size * 0.5f;
            _panel.Scale = Vector2.One * Math.Min(1f, (vh - 24f) / h);
        }
        if (_inPrep && Visible && GetViewport()?.GuiGetFocusOwner() is Control fo && _prep.IsAncestorOf(fo)) _prepFocus = fo;
        bool show = Visible && _lobby.Visible;
        foreach (var p in _portraits)
            if (p != null) p.RenderTargetUpdateMode = show ? SubViewport.UpdateMode.Always : SubViewport.UpdateMode.Disabled;
        for (int k = 0; k < _bigPortraits.Length; k++)
            if (_bigPortraits[k] != null) _bigPortraits[k].RenderTargetUpdateMode = show && k == _sideAt ? SubViewport.UpdateMode.Always : SubViewport.UpdateMode.Disabled;
    }


    // ---------------------------------------------------------------- the cursor
    // Left and right walk along a row (so from the Warden, left is always the Swordsman), up and
    // down step between rows: the order the page is laid out in. (The engine's own guess goes by
    // where things sit on screen, and skips about.)

    private static bool CanFocus(Control c) =>
        c != null && c.IsVisibleInTree() && c.FocusMode != FocusModeEnum.None
        && !(c is BaseButton b && b.Disabled) && !(c is Slider sl && !sl.Editable) && !(c is LineEdit le && !le.Editable);

    /// <summary>The rows of controls the cursor can reach on the page shown, top to bottom.</summary>
    private System.Collections.Generic.List<System.Collections.Generic.List<Control>> NavRows()
    {
        var rows = new System.Collections.Generic.List<System.Collections.Generic.List<Control>>();
        void Row(params Control[] cs)
        {
            var r = cs.Where(CanFocus).ToList();
            if (r.Count > 0) rows.Add(r);
        }
        if (_prep.Visible)
        {
            Row(_prepGrid, _prepLoadout); Row(_prepBack);
        }
        else if (_lobby.Visible)
        {
            Row(_copy1); Row(_copy2); Row(_copy3);
            Row(_diff); Row(_perPlayer); Row(_hard);
            Row(_cards.Take(4).ToArray()); Row(_cards.Skip(4).ToArray());
            Row(_prepBtn, _start, _leave);
        }
        else
        {
            Row(_name); Row(_host); Row(_code, _paste, _joinBtn); Row(_backBtn);
        }
        return rows;
    }

    private void MoveCursor(int dx, int dy)
    {
        var rows = NavRows();
        if (rows.Count == 0) return;
        var f = GetViewport().GuiGetFocusOwner();
        int r = rows.FindIndex(row => row.Contains(f)), c = r >= 0 ? rows[r].IndexOf(f) : -1;
        if (r < 0)
        {
            // nothing here has the cursor: start on your hero's card (or the first thing)
            int mineAt = Array.FindIndex(Heroes, h => Net.Mine != null && Net.Mine.Picked && Net.Mine.Hero == h.kind);
            var start = _lobby.Visible && mineAt >= 0 && CanFocus(_cards[mineAt]) ? _cards[mineAt] : rows[0][0];
            start.GrabFocus();
            G.Sfx?.Play("ui", null, -8);
            return;
        }
        Control to = f;
        if (dx != 0) to = rows[r][Math.Clamp(c + dx, 0, rows[r].Count - 1)];
        else
        {
            int nr = Math.Clamp(r + dy, 0, rows.Count - 1);
            if (nr != r)
            {
                // the control in the next row nearest across
                float x = f.GetGlobalRect().GetCenter().X;
                to = rows[nr].OrderBy(o => Math.Abs(o.GetGlobalRect().GetCenter().X - x)).First();
            }
        }
        if (to != f) { to.GrabFocus(); G.Sfx?.Play("ui", null, -8); }
    }

    public override void _Input(InputEvent e)
    {
        if (!Visible || (Covered?.Invoke() ?? false)) return;
        int dx = e.IsActionPressed("ui_left", true) ? -1 : e.IsActionPressed("ui_right", true) ? 1 : 0;
        int dy = e.IsActionPressed("ui_up", true) ? -1 : e.IsActionPressed("ui_down", true) ? 1 : 0;
        if (dx == 0 && dy == 0) return;
        var f = GetViewport().GuiGetFocusOwner();
        // a slider or a text box keeps left and right for itself (a slider with no cursor yet doesn't)
        if (dx != 0 && f is HSlider or LineEdit) return;
        MoveCursor(dx, dy);
        GetViewport().SetInputAsHandled();
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (!Visible || !e.IsActionPressed("ui_cancel") || (Covered?.Invoke() ?? false)) return;
        GetViewport().SetInputAsHandled();
        if (_inPrep) BackToLobby();
        else if (_lobby.Visible) DoLeave();
        else Back?.Invoke();
    }
}
