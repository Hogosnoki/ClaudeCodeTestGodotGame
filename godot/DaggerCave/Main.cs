using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

/// <summary>
/// Root of the Dagger Deep game: builds each cave level, owns the camera, spawns enemies as the
/// player explores, runs rooms (treasure, mini-bosses, ambushes, the boss), and drives the
/// title / level-up / pause / game-over flow.
///
/// Command-line user args (after `--`): `--seed=N`, `--autotest` (bot plays, screenshots are
/// written to `--shots=DIR` every few seconds for `--duration=S`), `--gentest` (prints
/// generator statistics for a batch of seeds and quits), `--start=boss|water|secret|drain` (spawn position
/// for testing), `--train` (enemy brain training on from the start), `--braindir=DIR` and
/// `--nntest` (checks the neural-net maths and quits).
/// </summary>
public partial class Main : Node
{
    public readonly List<EnemyProjectile> EnemyProjectiles = new();
    public Enemy ActiveBoss;
    /// <summary>True while a menu or screen has the controls (the hero ignores input).</summary>
    public bool MenuOpen => _state != State.Playing;
    /// <summary>An upgrade pick (a chest's cards, a milestone) is up on screen.</summary>
    public bool ChoosingNow => _state == State.Choosing;
    /// <summary>The run's dice (chest cards, exits).</summary>
    public Random Rng => _rng;
    private MetaMenu _metaMenu;
    private PerkMenu _perkMenu;
    private LoadoutMenu _loadoutMenu;
    private PauseMenu _pauseMenu;
    private SettingsMenu _settingsMenu;
    private BuildPanel _buildPanel;
    private bool _victory;
    private int _runEmbers;
    private string _runFinds = "";

    private Node2D _world;
    private Camera2D _cam;
    private Stage3D _stage;
    /// <summary>The 3D stage (its terrain and the material of its rock).</summary>
    public Stage3D Stage => _stage;
    /// <summary>The gameplay camera (2D): it decides what counts as on screen; the 3D camera follows it.</summary>
    public Camera2D Cam2D => _cam;
    private CanvasLayer _uiLayer, _darkLayer;
    private Hud _hud;
    public Hud Hud => _hud;
    private UpgradeMenu _upgradeMenu;
    private ScreenOverlay _overlay;
    private SoundBank _sfx;
    private FxLayer _fx;

    private enum State { Title, Playing, Paused, Choosing, Dead }
    private State _state = State.Title;
    private float _hitStopLeft;
    private float _frameScale = 1f;
    private float _spawnT, _runTime, _deadT;
    private int _seed;
    private readonly Random _rng = new();
    private readonly Queue<Chest> _pendingTreasure = new(); // chests looked into, awaiting a pick
    /// <summary>The chest whose cards are up on screen (null for a milestone).</summary>
    private Chest _lookingChest;
    private readonly Dictionary<Room, Enemy> _roomElites = new();

    // test harness
    private bool _autotest;
    private string _shotDir = "";
    private float _duration = 60, _shotT, _autoPickT;
    private int _shotN;
    private string _startAt = "";
    private string _titleShot = "";
    private float _titleT;
    private bool _menuShotDone;
    private bool _bestiary, _animTest, _padTest, _showcase, _heroTest;
    private float _showT;
    private int _showStage = -1;
    private float _padT;
    private int _padStep;
    private float _animT;
    private int _animFrame;
    private float _bestiaryT = -1;
    private BotPilot _bot;
    private bool _nnTest, _fullRun;
    private string _metaShot = "";
    private int _metaShotStep;

    /// <summary>--metashot=DIR: screenshots of the potion tree's introduction and the tree screen.</summary>
    private bool _perkShot;
    private void MetaShotTick()
    {
        var steps = _perkShot ? new (float at, Action act)[]
        {
            (0.5f, () => { Meta.Embers = 9; G.Hero = HeroKind.Warden; foreach (var id in new[] { "warden_dr", "warden_window" }) Meta.PerkBuy(ClassPerks.Find(id)); _perkMenu.Open(G.Hero); }),
            (1.2f, () => GetViewport().GetTexture().GetImage().SavePng($"{_metaShot}/perks_1.png")),
            (1.3f, () => Input.ParseInputEvent(new InputEventAction { Action = "move_down", Pressed = true })),
            (1.35f, () => Input.ParseInputEvent(new InputEventAction { Action = "move_down", Pressed = false })),
            (1.4f, () => Input.ParseInputEvent(new InputEventAction { Action = "confirm", Pressed = true })),
            (1.45f, () => Input.ParseInputEvent(new InputEventAction { Action = "confirm", Pressed = false })),
            (1.9f, () => GetViewport().GetTexture().GetImage().SavePng($"{_metaShot}/perks_2.png")),
            (2.1f, () => { GD.Print($"[perkshot] {ClassPerks.EquippedNames(G.Hero)} | embers {Meta.Embers}"); SafeQuit.Request(this); }),
        } : new (float at, Action act)[]
        {
            (0.5f, () => { Meta.Embers = 4; Meta.RollResource(new Random(1)); Meta.Found["potion"] = 1; Meta.Held["potion"] = 1; _metaMenu.Open(MetaMenu.Mode.PotionTutorial); }),
            (1.2f, () => GetViewport().GetTexture().GetImage().SavePng($"{_metaShot}/meta_1.png")),
            (1.3f, () => Input.ParseInputEvent(new InputEventAction { Action = "move_down", Pressed = true })),
            (1.35f, () => Input.ParseInputEvent(new InputEventAction { Action = "move_down", Pressed = false })),
            (1.4f, () => Input.ParseInputEvent(new InputEventAction { Action = "confirm", Pressed = true })),
            (1.45f, () => Input.ParseInputEvent(new InputEventAction { Action = "confirm", Pressed = false })),
            (1.9f, () => GetViewport().GetTexture().GetImage().SavePng($"{_metaShot}/meta_2.png")),
            (2.0f, () => Input.ParseInputEvent(new InputEventAction { Action = "confirm", Pressed = true })),
            (2.05f, () => Input.ParseInputEvent(new InputEventAction { Action = "confirm", Pressed = false })),
            (2.6f, () => { Meta.Found["pearl"] = 2; Meta.Held["pearl"] = 1; GetViewport().GetTexture().GetImage().SavePng($"{_metaShot}/meta_3.png"); }),
            (3.0f, () => GetViewport().GetTexture().GetImage().SavePng($"{_metaShot}/meta_4.png")),
            (3.2f, () => { GD.Print($"[metashot] hot rank active: {Meta.Active.Contains("hot1")}, embers {Meta.Embers}"); SafeQuit.Request(this); }),
        };
        while (_metaShotStep < steps.Length && _titleT >= steps[_metaShotStep].at) steps[_metaShotStep++].act();
    }
    private string _biomeArg, _loadShots = "";

    public override void _Ready()
    {
        G.Main = this;
        ProcessMode = ProcessModeEnum.Always;
        // closing the window waits out background shader compiles first (see SafeQuit)
        GetTree().AutoAcceptQuit = false;
        SetupInput();
        var win = GetWindow();
        win.ContentScaleSize = new Vector2I(1280, 720);
        win.ContentScaleMode = Window.ContentScaleModeEnum.CanvasItems;
        // resizing or maximizing scales the whole picture up, keeping 16:9 (letterboxed if needed)
        win.ContentScaleAspect = Window.ContentScaleAspectEnum.Keep;
        win.Title = "Dagger Deep";
        RenderingServer.SetDefaultClearColor(Colors.Black);

        _sfx = new SoundBank();
        AddChild(_sfx);
        G.Sfx = _sfx;

        // What you see is 3D (Stage3D); the 2D world below still runs the whole simulation but
        // draws nothing (F7 shows it on top, for checking collisions against the 3D art).
        _stage = new Stage3D();
        AddChild(_stage);
        GameSettings.Apply(this);

        _world = new Node2D { Name = "World", ProcessMode = ProcessModeEnum.Pausable, Visible = false };
        AddChild(_world);
        G.World = _world;

        _darkLayer = new CanvasLayer { Layer = 5, Visible = false };
        AddChild(_darkLayer);
        _darkLayer.AddChild(new DarknessOverlay());

        _uiLayer = new CanvasLayer { Layer = 10 };
        AddChild(_uiLayer);
        var loadLayer = new CanvasLayer { Layer = 20, ProcessMode = ProcessModeEnum.Always };
        AddChild(loadLayer);
        _loadScreen = new LoadingScreen();
        loadLayer.AddChild(_loadScreen);
        _hud = new Hud();
        _uiLayer.AddChild(_hud);
        _upgradeMenu = new UpgradeMenu();
        _upgradeMenu.Picked += OnUpgradePicked;
        _upgradeMenu.ShowBuild = () => _buildPanel.Open();
        _uiLayer.AddChild(_upgradeMenu);
        _overlay = new ScreenOverlay();
        _uiLayer.AddChild(_overlay);
        _metaMenu = new MetaMenu();
        _metaMenu.Closed += OnMetaClosed;
        _uiLayer.AddChild(_metaMenu);
        _perkMenu = new PerkMenu();
        _perkMenu.Closed += OnMetaClosed;
        _uiLayer.AddChild(_perkMenu);
        _loadoutMenu = new LoadoutMenu();
        _loadoutMenu.Closed += () =>
        {
            if (_onlineMenu != null && _onlineMenu.Visible) { _onlineMenu.AfterSubmenu(); return; }
            _heroChoice?.Refresh(); _heroChoice?.RestoreFocus();
        };
        _uiLayer.AddChild(_loadoutMenu);
        UiKit.EnsureMenuControls();
        _pauseMenu = new PauseMenu { Resume = Unpause, Settings = OpenSettings, Build = OpenBuild, Unstick = () => { G.Player?.ForceUnstick(); Unpause(); }, Quit = GiveUpRun, QuitGame = () => SafeQuit.Request(this) };
        _uiLayer.AddChild(_pauseMenu);
        _settingsMenu = new SettingsMenu { Closed = OnSettingsClosed };
        _uiLayer.AddChild(_settingsMenu);
        _buildPanel = new BuildPanel { Closed = OnBuildClosed };
        _uiLayer.AddChild(_buildPanel);
        SetupOnline();
        SetupFrontMenus();

        ParseArgs(out bool gentest);
        G.NoSave = _autotest || gentest || _nnTest || _heroTest || _hitStopTest || _bestiary || _animTest || _padTest || _titleShot != "" || OS.GetCmdlineUserArgs().Contains("--metatest") || OS.GetCmdlineUserArgs().Contains("--upgradetest") || _metaShot != "" || _lookShot != "" || _menuShot != "" || _netTest != "" || _onlineShot != "" || _scenario != "" || _campShot != "" || _frontTest != "";
        try { Begin(gentest); }
        catch (Exception ex)
        {
            GD.PrintErr(ex.ToString());
            if (_autotest || gentest) SafeQuit.Request(this, 1);
            else throw;
        }
    }

    private void Begin(bool gentest)
    {
        if (ModelSheet.Wanted) { _uiLayer.Visible = false; AddChild(new ModelSheet()); return; }
        if (gentest) { RunGenTest(); return; }
        if (_loadShots != "") { RunLoadShots(_loadShots); return; }
        if (OS.GetCmdlineUserArgs().Contains("--bosstest")) { RunBossTest(); return; }
        if (OS.GetCmdlineUserArgs().Contains("--upgradetest")) { RunUpgradeTest(); return; }
        if (OS.GetCmdlineUserArgs().Contains("--webaudit")) { int bad = CreatureLibrary.WebAudit(); GD.Print(bad == 0 ? "[webaudit] PASS" : $"[webaudit] {bad} designs still have welds"); SafeQuit.Request(this, bad == 0 ? 0 : 1); return; }
        foreach (var arg in OS.GetCmdlineUserArgs())
        {
            if (arg.StartsWith("--sfxdump=")) { _sfx.DumpSfx(arg[10..]); SafeQuit.Request(this); return; }
            if (arg.StartsWith("--musicdump=")) { _sfx.DumpMusic(arg[12..]); SafeQuit.Request(this); return; }
        }
        if (_campShot != "") { ShowCampScene(true); return; }
        if (OS.GetCmdlineUserArgs().Contains("--metatest")) { RunMetaTest(); return; }
        if (_nnTest) { RunNnTest(); return; }
        if (_netTest != "") { Tune.Share.On = false; BeginNetTest(); return; } // (the transport checks use whole blows)

        // (the magma scenario wants a cave with a real lava lake unless told otherwise, and the
        // water scenario one with wide open water: not every Slime Cavern has it where creatures swim)
        if (_seed == 0 && _scenario == "magma") _seed = 2;
        if (_seed == 0 && _scenario == "water") _seed = 1013;
        // (the hero checks stand in one known cave, so a spot's lie of the land can't tip them)
        if (_heroTest) { Player.KnockLock = false; Tune.Combat.HurtKnockbackMult = 0.8f; Tune.Feel.HitStopPlayerHurt = 0.18f; }
        if (_heroTest) Tune.Cave.HeightScale = 1f; // (the tests were laid out for the original cave height)
        if (_seed == 0 && _heroTest) _seed = 5065; // (a cave whose start chamber has room for every hero's test)
        _seed = _seed != 0 ? _seed : (int)(Time.GetUnixTimeFromSystem() * 1000 % 1000000);
        if (_autotest) G.Rng = new Random(_seed);
        G.Depth = 0;
        G.Biome = Biomes.Get(BiomeId.Entrance);
        if (_biomeArg == null && (_heroTest || _bestiary || _animTest || _showcase || _hitStopTest)) _biomeArg = "slime"; // these need water
        if (_biomeArg == null && _scenario != "") _biomeArg = ScenarioBiome;
        if (_biomeArg != null)
        {
            G.Biome = Biomes.All.First(b => b.Id.ToString().Equals(_biomeArg, StringComparison.OrdinalIgnoreCase));
            G.Depth = G.Biome.MinDepth;
        }
        BuildLevel(_seed, freshPlayer: true);
        if (_padTest)
        {
            // Starts on the main menu and drives everything with synthetic controller events.
            GetTree().Paused = true;
            _state = State.Title;
            ShowTitle();
        }
        else if (_heroTest)
        {
            StartPlaying();
            _hud.HintTime = 0;
            // a step's presses last one frame, like a real button (held flags stay as the step set them)
            G.Player.InputOverride = () =>
            {
                var i = _heroInput;
                _heroInput.Attack = _heroInput.Ability = _heroInput.Ability2 = _heroInput.Dodge = _heroInput.Jump = _heroInput.Potion = _heroInput.Interact = false;
                return i;
            };
        }
        else if (_hitStopTest)
        {
            StartPlaying();
            _hud.HintTime = 0;
            G.Player.InputOverride = HitStopTestInput;
        }
        else if (_animTest)
        {
            StartPlaying();
            _hud.HintTime = 0;
            G.Player.InputOverride = AnimTestInput;
        }
        else if (_bestiary)
        {
            StartPlaying();
            _hud.HintTime = 0;
            G.Player.InputOverride = () => default;
            SpawnBestiary();
        }
        else if (_menuShot != "")
        {
            StartPlaying();
            _hud.HintTime = 0;
            G.Player.InputOverride = () => default;
        }
        else if (_lookShot != "")
        {
            StartPlaying();
            _hud.HintTime = 0;
            G.Player.InputOverride = () => default;
        }
        else if (_scenario != "") BeginScenario();
        else if (_autotest)
        {
            StartPlaying();
            _bot = new BotPilot { Focused = _fullRun, SkipAhead = _fullRun };
            G.Player.InputOverride = _bot.Read;
            if (_fullRun) { G.Player.Stats.MaxHp = 5000; G.Player.Hp = 5000; G.Player.Stats.DamageMult = 3f; }
        }
        else
        {
            GetTree().Paused = true;
            _state = State.Title;
            _hud.Visible = false;
            ShowTitle();
        }
    }

    /// <summary>The main menu, over the camp outside the cave.</summary>
    private void ShowTitle() => ShowMainMenu();

    /// <summary>The controls in one line, as bound (keyboard and mouse, or the controller).</summary>
    private static string ControlsLine(bool pad)
    {
        string N(string a) => Controls.Name(a, pad);
        return pad
            ? $"CONTROLLER:  stick move   {N("jump")} jump   {N("attack")} attack   {N("ability")} ability   {N("ability2")} second ability   {N("dodge")} dodge / shield / hex / updraft / vanish   {N("interact")} open / descend / revive   {N("potion")} potion   right stick attacks (the Warden's raises the shield)"
            : $"KEYBOARD + MOUSE:  {N("move_left")} / {N("move_right")} move   {N("jump")} jump   {N("attack")} attack   {N("ability")} ability   {N("ability2")} second ability   {N("dodge")} dodge / shield / hex / updraft / vanish   {N("interact")} open / descend   {N("potion")} potion";
    }

    /// <summary>The line about embers and the upgrade trees on the title and camp screens.</summary>
    private string CampLine()
    {
        bool trees = Meta.Trees.Any(Meta.Visible);
        string pad = UsingPad ? "BACK" : "U";
        return trees ? $"Embers: {Meta.Embers}   ·   press {pad} for the upgrade trees" : Meta.Embers > 0 ? $"Embers: {Meta.Embers}" : "";
    }

    private void ParseArgs(out bool gentest)
    {
        gentest = false;
        foreach (var a in OS.GetCmdlineUserArgs())
        {
            if (a == "--autotest") _autotest = true;
            else if (a == "--gentest") gentest = true;
            else if (a.StartsWith("--seed=")) _seed = int.Parse(a[7..]);
            else if (a == "--ledgesasground") CaveGenerator.KeepLedges = true;
            else if (a == "--loadscreen") _forceLoadScreen = true;
            else if (a.StartsWith("--loadshots=")) _loadShots = a[12..];
            else if (a.StartsWith("--shots=")) _shotDir = a[8..];
            else if (a.StartsWith("--duration=")) _duration = float.Parse(a[11..], System.Globalization.CultureInfo.InvariantCulture);
            else if (a.StartsWith("--start=")) _startAt = a[8..];
            else if (a.StartsWith("--titleshot=")) _titleShot = a[12..];
            else if (a == "--bestiary") _bestiary = true;
            else if (a == "--animtest") _animTest = true;
            else if (a == "--herotest") _heroTest = true;
            else if (a == "--alttest") _heroTest = _altTest = true;
            else if (a == "--hitstoptest") _hitStopTest = true;
            else if (a == "--padtest") _padTest = true;
            else if (a == "--showcase") { _showcase = true; _autotest = true; }
            else if (a == "--train") Brains.Training = true;
            else if (a == "--hero=warden") G.Hero = HeroKind.Warden;
            else if (a == "--hero=swordsman") G.Hero = HeroKind.Swordsman;
            else if (a == "--hero=vitalist") G.Hero = HeroKind.Vitalist;
            else if (a == "--hero=elementalist") G.Hero = HeroKind.Elementalist;
            else if (a == "--hero=rogue") G.Hero = HeroKind.Rogue;
            else if (a == "--hero=aegis") G.Hero = HeroKind.Aegis;
            else if (a == "--hero=shifter") G.Hero = HeroKind.ShapeShifter;
            else if (a.StartsWith("--braindir=")) Brains.DirOverride = a[11..];
            else if (a == "--nntest") _nnTest = true;
            else if (a.StartsWith("--biome=")) _biomeArg = a[8..];
            else if (a == "--fullrun") _fullRun = true;
            else if (a == "--forcedrain") CaveGenerator.ForceDrain = true;
            else if (a == "--forcenooks") CaveGenerator.ForceNooks = true;
            else if (a.StartsWith("--metashot=")) _metaShot = a[11..];
            else if (a.StartsWith("--perkshot=")) { _metaShot = a[11..]; _perkShot = true; }
            else if (a.StartsWith("--lookshot=")) _lookShot = a[11..];
            else if (a.StartsWith("--frames=")) _lookFrames = int.Parse(a[9..]);
            else if (a.StartsWith("--fxtest=")) _fxTest = int.Parse(a[9..]);
            else if (a == "--proptest") _propTest = true;
            else if (a == "--chesttest") _chestTest = true;
            else if (a == "--elemrow") _elemRow = true;
            else if (a == "--webaudit") _webAudit = true;
            else if (a.StartsWith("--lookstatus=")) _lookStatus = a[13..];
            else if (a == "--elementtest") _elementTest = true;
            else if (a == "--roguetest") _rogueLook = true;
            else if (a == "--exittest") _exitTest = true;
            else if (a.StartsWith("--menushot=")) _menuShot = a[11..];
            else if (a.StartsWith("--nettest=")) _netTest = a[10..];
            else if (a.StartsWith("--netaddr=")) _netAddr = a[10..];
            else if (a.StartsWith("--ntshots=")) _ntShots = a[10..];
            else if (a.StartsWith("--onlineshot=")) _onlineShot = a[13..];
            else if (a.StartsWith("--campshot=")) _campShot = a[11..];
            else if (a.StartsWith("--fronttest=")) _frontTest = a[12..];
            else if (a.StartsWith("--scenario=")) _scenario = a[11..];
        }
        Affinity.Off = _heroTest || _hitStopTest || _altTest;
    }

    // --lookshot=PATH [--frames=N]: build the level, stand still for N frames, save a screenshot
    // and quit (look development for the 3D presentation)
    private string _lookShot = "";
    // --menushot=DIR: the pause menu and each settings tab, then a chest holding a friend's cards,
    // a milestone's cards and the build page, saved as screenshots
    private string _menuShot = "";
    private int _menuShotFrame;
    private Chest _msChest;

    private void MenuShotTick()
    {
        int f = ++_menuShotFrame;
        void Shot(string n) { GetViewport().GetTexture().GetImage().SavePng($"{_menuShot}/{n}.png"); GD.Print($"[menushot] {n}"); }
        switch (f)
        {
            case 20: PauseGame(); break;
            case 30: Shot("pause"); OpenSettings(); break;
            case 40: Shot("settings_graphics"); _settingsMenu.ShowTab(1); break;
            case 50: Shot("settings_sound"); _settingsMenu.ShowTab(2); break;
            case 60: Shot("settings_controls"); _settingsMenu.Visible = false; Unpause(); break;
            case 66:
            {
                // a chest dealt for a party: one card for this hero, two for the others
                var chest = new Chest { Position = G.Player.GlobalPosition + new Vector2(60, 13), Cards = new[] { "hp", "relic_leap", "stalwart" } };
                foreach (var rid in new[] { "relic_anvil", "relic_flask", "relic_fount", "relic_sniper", "relic_a_prism", "relic_r_hemo" }) RunRelics.Note(Net.Me, rid);
                _world.AddChild(chest);
                _msChest = chest;
                break;
            }
            case 72: Shot("shrine"); _msChest?.Look(); break;
            case 80: Shot("chest_cards"); _upgradeMenu.ChooseLeave(); break;
            case 84:
            {
                // a build under way, then a milestone's cards (an alteration among them) and the build page
                var p = G.Player;
                string[] cards = p.Stats.Hero switch
                {
                    HeroKind.Warden => new[] { "reach", "shield_wide", "aegis", "aegis", "shield_unyielding", "unyielding_more", "dash_cd", "hp", "speed", "rr_lungs" },
                    HeroKind.Vitalist => new[] { "mouths", "hex_long", "heal_slow", "heal_warding", "wellspring", "rupture_cheap", "hp", "armor", "rr_glass" },
                    HeroKind.Elementalist => new[] { "kindling", "reservoir", "attune", "attune", "whiteout", "gathering", "echo", "hp", "speed", "rr_reserve" },
                    HeroKind.Rogue => new[] { "keen", "backstab", "twin_throw", "throw_ricochet", "surprise", "hp", "speed", "jump", "rr_glass" },
                    _ => new[] { "combo", "reach", "charge_combo", "charge_combo_more", "windrunner", "windrunner", "iframes", "hp", "speed", "rr_heavy" },
                };
                foreach (var id in cards) Upgrades.Apply(Upgrades.Get(id), p.Stats, p);
                p.PendingMilestones = 1;
                TryOpenUpgradeMenu(true);
                break;
            }
            case 98: Shot("milestone_cards"); _buildPanel.Open(); break;
            case 104: Shot("build"); _buildPanel.Close(); break;
            case 106: SafeQuit.Request(this); break;
        }
    }
    private int _lookFrames = 24, _lookFrame;
    private bool _exitTest;

    private int _fxTest;
    private bool _propTest, _elementTest, _rogueLook, _chestTest, _elemRow;
    private string _lookStatus = "";
    private bool _webAudit;

    /// <summary>Test aid: one of every prop laid out around the player (for their 3D look).</summary>
    /// <summary>Test aid (--exittest): the two exits a guardian leaves, one right where the hero stands.</summary>
    private void SpawnExitTest()
    {
        var p = G.Player.GlobalPosition;
        Vector2 Floor(float dx) => G.Cave.FindFloor(p + new Vector2(dx, -40), 200, out var f) ? f : p + new Vector2(dx, 12);
        var exits = Biomes.ChooseExits(G.Depth, _rng);
        for (int k = 0; k < exits.Count && k < 2; k++)
        {
            var (bd, depth) = exits[k];
            string label = exits.Count == 1 ? $"depth {depth}" : depth - G.Depth == 1 ? $"depth {depth}  ·  the gentle way" : $"depth {depth}  ·  the steep way";
            _world.AddChild(new Portal { Position = Floor(k * 170) + new Vector2(0, -30), To = bd, Depth = depth, Label = label });
        }
    }

    /// <summary>Test aid (--chesttest, with --lookshot): every kind of chest in a row beside the hero: wood, silver (relic), gold (a guardian's), the vault's, and a silver one hung in a web.</summary>
    private void SpawnChestTest()
    {
        var p = G.Player.GlobalPosition;
        var cave = G.Cave;
        Vector2 Floor(float dx) => cave.FindFloor(p + new Vector2(dx, -40), 200, out var f) ? f : p + new Vector2(dx, 12);
        foreach (var e in G.Enemies.ToArray()) e.QueueFree();
        void Add(Node2D n, Vector2 at) { n.Position = at; _world.AddChild(n); }
        Add(new Chest(), Floor(-150));
        Add(new Chest { Tier = ChestTier.Relic }, Floor(-95));
        Add(new Chest { Tier = ChestTier.Boss }, Floor(-40));
        Add(new Chest { Vault = true }, Floor(25));
        var fl = Floor(90);
        Add(new Chest { Hung = true, LandY = fl.Y, Tier = ChestTier.Relic }, fl - new Vector2(0, Tune.Relics.WebHangHeight));
        var fl2 = Floor(150);
        Add(new Chest { Tier = ChestTier.Boss, Owner = Net.Me + 5 }, fl2);
    }

    private void SpawnPropTest()
    {
        var p = G.Player.GlobalPosition;
        var cave = G.Cave;
        Vector2 Floor(float dx) => cave.FindFloor(p + new Vector2(dx, -40), 200, out var f) ? f : p + new Vector2(dx, 12);
        void Add(Node2D n, Vector2 at) { n.Position = at; _world.AddChild(n); }
        Add(new Chest(), Floor(-150));
        Add(new Chest { Tier = ChestTier.Relic }, Floor(-190));
        Add(new Chest { Tier = ChestTier.Boss }, Floor(-235));
        {
            var fl = Floor(-280);
            Add(new Chest { Hung = true, LandY = fl.Y, Tier = ChestTier.Relic }, fl - new Vector2(0, Tune.Relics.WebHangHeight));
        }
        Add(new XpOrb { Value = 3 }, p + new Vector2(-110, -40));
        Add(new XpOrb { Value = 10 }, p + new Vector2(-95, -52));
        Add(new HeartPickup(), p + new Vector2(-70, -45));
        Add(new PotionPickup(), p + new Vector2(-45, -45));
        Add(new KeyPickup(), p + new Vector2(-20, -45));
        Add(new Chest { Vault = true }, Floor(-200));
        Add(new EnemyProjectile { Kind = "rock", Vel = Vector2.Zero, Grav = 0, Life = 99 }, p + new Vector2(40, -70));
        Add(new EnemyProjectile { Kind = "lava", Vel = Vector2.Zero, Grav = 0, Life = 99 }, p + new Vector2(60, -70));
        Add(new EnemyProjectile { Kind = "fire", Vel = Vector2.Zero, Grav = 0, Life = 0.6f }, p + new Vector2(80, -70));
        Add(new EnemyProjectile { Kind = "ice", Vel = new Vector2(1, 0), Grav = 0, Life = 99 }, p + new Vector2(100, -70));
        Add(new EnemyProjectile { Kind = "spit", Vel = Vector2.Zero, Grav = 0, Life = 99 }, p + new Vector2(120, -70));
        Add(new LavaPuddle(), Floor(60));
        Add(new CrystalSpikes(), Floor(110));
        Add(new FireVent(), Floor(160));
        Add(new SporePod(), Floor(-110));
        Add(new WebPatch { Radius = 26 }, p + new Vector2(170, -60));
        Add(new SporeCloud { Radius = 30, Life = 99 }, p + new Vector2(-160, -70));
        Add(new SwordWave { Dir = Vector2.Right, Damage = 0, Range = 9999, Speed = 1 }, p + new Vector2(0, -80));
        Add(new GraspingRoots { Radius = 26 }, Floor(-60) + new Vector2(0, 2));
        if (cave.FindCeiling(p + new Vector2(90, -20), 400, out var ce)) Add(new CaveIn { Drop = 200 }, ce + new Vector2(0, 3));
    }

    /// <summary>
    /// Test aid (--elementtest, with --lookshot): the Elementalist's spells laid out around the hero
    /// for their 3D look: a blizzard and a firestorm, an updraft, a firebolt and a frostbolt hanging
    /// in the air, and a goblin frozen solid beside one that burns.
    /// </summary>
    private void SpawnElementTest()
    {
        var p = G.Player.GlobalPosition;
        var cave = G.Cave;
        Vector2 Floor(float dx) => cave.FindFloor(p + new Vector2(dx, -40), 200, out var f) ? f : p + new Vector2(dx, 12);
        void Add(Node2D n, Vector2 at) { n.Position = at; _world.AddChild(n); }
        foreach (var e in G.Enemies.ToArray()) e.QueueFree();
        Add(new Blizzard { Seconds = 999, Ticks = 0, Harmless = true }, Floor(-150) - new Vector2(0, 18));
        Add(new Blizzard { Seconds = 999, Ticks = 0, Harmless = true, Fire = true }, Floor(150) - new Vector2(0, 18));
        Add(new Updraft { Life = 999 }, Floor(-70));
        Add(new ElementBolt { Dir = Vector2.Right, Speed = 0.01f, Range = 9999, Harmless = true }, p + new Vector2(40, -40));
        Add(new ElementBolt { Dir = Vector2.Right, Speed = 0.01f, Range = 9999, Harmless = true, Frost = true }, p + new Vector2(70, -40));
        var frozen = new Goblin();
        frozen.SetMeta("test", true);
        Add(frozen, Floor(55) - new Vector2(0, 8));
        frozen.FreezeSolid(999f);
        var burning = new Goblin();
        burning.SetMeta("test", true);
        Add(burning, Floor(95) - new Vector2(0, 8));
        burning.Freeze(999f, hold: true);
        burning.Ignite(0.001f, 999f);
    }

    /// <summary>Test aid: one of every effect in a grid around the player (for tuning their 3D look).</summary>
    private void SpawnFxTest()
    {
        var fx = G.Fx;
        var p = G.Player.GlobalPosition + new Vector2(0, -40);
        float dx = 70, x0 = -2 * dx;
        Vector2 At(int col, int row) => p + new Vector2(x0 + col * dx, -60 + row * 55);
        fx.Burst(At(0, 0), new Color(1f, 0.6f, 0.2f), 14, 180, 2.5f, 0.6f);
        fx.Spark(At(1, 0), Vector2.Right, true, new Color(1f, 0.9f, 0.5f));
        fx.Explosion(At(2, 0), new Color(1f, 0.5f, 0.2f), 0.8f);
        fx.Pop(At(3, 0), new Color(0.9f, 0.2f, 0.2f), 12);
        fx.Flash(At(4, 0), 20, new Color(0.6f, 0.9f, 1f));
        fx.Smoke(At(0, 1), 8, new Color(0.35f, 0.33f, 0.32f, 0.6f));
        fx.Dust(At(1, 1), 8);
        fx.Debris(At(2, 1), new Color(0.45f, 0.42f, 0.38f), 10);
        fx.Splash(At(3, 1), 1f, new Color(0.6f, 0.85f, 1f, 0.85f));
        for (int k = 0; k < 6; k++) fx.Ember(At(4, 1) + G.RandDir() * 8, new Color(1f, 0.6f, 0.2f));
        fx.Ring(At(0, 2), 20, new Color(1f, 0.85f, 0.4f));
        fx.Shockwave(At(1, 2), 50, new Color(1f, 0.9f, 0.7f, 0.9f));
        fx.Glint(At(2, 2), new Color(1f, 0.95f, 0.7f), 10);
        fx.Swoosh(At(3, 2), 1, 22, new Color(0.85f, 0.95f, 1f, 0.9f));
        fx.Text(At(4, 2), "123", new Color(1f, 0.9f, 0.3f), 12);
        fx.Bubbles(At(4, 2) + new Vector2(0, 30), 6);
    }

    private void LookShotTick()
    {
        ++_lookFrame;
        // --fxtest: lay out one of every effect around the player a moment before the shot
        if (_fxTest > 0 && _lookFrame == Math.Max(1, _lookFrames - _fxTest)) SpawnFxTest();
        if (_propTest && _lookFrame == 2) SpawnPropTest();
        if (_chestTest && _lookFrame == 2) SpawnChestTest();
        // Test aid (--lookexit, with --lookshot): an exit's doorway (and the stair you came by) beside the hero, to see how they are lit
        if (_lookFrame == 2 && OS.GetCmdlineUserArgs().Contains("--lookexit"))
        {
            var at = G.Player.GlobalPosition;
            if (G.Cave.FindFloor(at + new Vector2(150, -20), 120, out var f1)) SpawnPortal(f1 + new Vector2(0, -17), (int)BiomeId.Ruins, 2, "test");
        }
        if (_lookStatus != "" && _lookFrame == 2)
        {
            // Test aid (--lookstatus=poison|burn|frost, with --lookshot): the hero afflicted, a burning goblin and a frozen one beside
            var p = G.Player; var at = p.GlobalPosition;
            foreach (var e in G.Enemies.ToArray()) e.QueueFree();
            p.Stats.MaxHp = p.Hp = 9999;
            if (_lookStatus == "poison") p.GivePoison(1f, 999f); else if (_lookStatus == "burn") p.GiveBurn(1f, 999f); else p.GiveFrozen(999f);
            var burning = new Goblin(); var frozen = new Goblin();
            burning.Position = at + new Vector2(50, -6); frozen.Position = at + new Vector2(95, -6);
            foreach (var g in new[] { burning, frozen }) { g.SetMeta("test", true); _world.AddChild(g); g.Freeze(999f, hold: true); }
            burning.Ignite(0.001f, 999f); frozen.FreezeSolid(999f);
        }
        if (_elemRow && _lookFrame == 2)
        {
            // Test aid (--elemrow, with --lookshot): the five elementals side by side, held still, a goblin for scale
            var at = G.Player.GlobalPosition;
            foreach (var e in G.Enemies.ToArray()) e.QueueFree();
            var row = new Enemy[] { new Goblin(), new WaterElemental(), new EarthElemental(), new FrostElemental(), new NatureElemental(), new FireElemental() };
            for (int k = 0; k < row.Length; k++)
            {
                var fl = G.Cave.FindFloor(at + new Vector2(-130 + k * 42, -40), 200, out var f) ? f : at;
                row[k].Position = fl + new Vector2(0, -20);
                row[k].SetMeta("test", true);
                _world.AddChild(row[k]);
                row[k].Freeze(99f, hold: true);
                row[k].Hp = row[k].MaxHp * 0.6f; // (hurt, so its health bar shows)
            }
        }
        if (_elementTest && _lookFrame == 2) SpawnElementTest();
        if (_rogueLook && _lookFrame == 2) SpawnRogueLook();
        if (_exitTest && _lookFrame == 2) SpawnExitTest();
        if (_lookFrame < _lookFrames) return;
        GetViewport().GetTexture().GetImage().SavePng(_lookShot);
        GD.Print($"[lookshot] saved {_lookShot}");
        SafeQuit.Request(this);
    }

    /// <summary>Every action with its default bindings, then the player's saved settings (and bindings) on top.</summary>
    private static void SetupInput()
    {
        Controls.SetupDefaults();
        GameSettings.Load();
    }

    // ------------------------------------------------------------------ level

    // ---- the loading screen: the way down takes up the screen at once, and the cave is made behind it

    private LoadingScreen _loadScreen;
    private bool _loading, _forceLoadScreen, _loadWasPaused;
    private int _loadFrames, _loadSeed;
    private float _loadT;
    private bool _loadFresh;
    private System.Threading.Tasks.Task<CaveData> _loadTask;
    private Action _loadDone;
    /// <summary>The least time the screen stays up, so that its line can be read.</summary>
    private const float MinLoadTime = 0.9f;

    /// <summary>The loading screen is for the real game; the checks that drive the game from code keep their levels coming at once (--loadscreen puts it back).</summary>
    private bool LoadScreenWanted => !G.NoSave || _forceLoadScreen;

    /// <summary>
    /// Makes the level for the biome and depth now set, behind the loading screen: the screen is up before anything else happens, the cave is
    /// generated on other threads while it animates, and once it is ready the level is built and <paramref name="done"/> runs.
    /// </summary>
    private void BuildLevelAsync(int seed, bool freshPlayer, Action done)
    {
        if (!LoadScreenWanted || _loading) { BuildLevel(seed, freshPlayer); done?.Invoke(); return; }
        var biome = G.Biome ??= Biomes.Get(BiomeId.Entrance);
        _loading = true; _loadT = 0; _loadFrames = 0; _loadSeed = seed; _loadFresh = freshPlayer; _loadDone = done; _loadTask = null;
        _loadScreen.Open(biome, G.Depth);
        // (the level left behind holds still under the screen; what the others in a party send for the new one waits until it is built)
        _loadWasPaused = GetTree().Paused;
        GetTree().Paused = true;
        if (Net.Online) { GetTree().MultiplayerPoll = false; NetSync.BeginLevel(); }
    }

    private void TickLoading(float dt)
    {
        _loadT += dt; _loadFrames++;
        var biome = G.Biome;
        // (a few frames of the screen on its own first, so it is really there before the threads take the machine)
        if (_loadTask == null && _loadFrames >= 3)
        {
            int seed = _loadSeed;
            _loadTask = System.Threading.Tasks.Task.Run(() =>
            {
                var c = CaveGenerator.Generate(biome, seed);
                // (the rock's 3D meshes too: they take as long as the cave itself)
                c.TerrainPre = TerrainView.Precompute(c);
                // (and the ledges', cut out of the rock as it was with them in it)
                LedgeMesh.Precompute(c);
                return c;
            });
        }
        if (_loadTask == null || !_loadTask.IsCompleted || _loadT < MinLoadTime) return;
        CaveData cave = null;
        if (_loadTask.IsFaulted) GD.PrintErr(_loadTask.Exception?.ToString());
        else cave = _loadTask.Result;
        _loadTask = null;
        _loading = false;
        ulong buildFrom = Time.GetTicksMsec();
        if (Net.Online) GetTree().MultiplayerPoll = true;
        GetTree().Paused = _loadWasPaused;
        BuildLevel(_loadSeed, _loadFresh, cave);
        GD.Print($"[DaggerDeep] loading screen: the cave was ready after {(int)(_loadT * 1000)} ms, the level built in {Time.GetTicksMsec() - buildFrom} ms");
        var done = _loadDone; _loadDone = null;
        done?.Invoke();
        _loadScreen.Close();
    }

    private void BuildLevel(int seed, bool freshPlayer, CaveData pregenerated = null)
    {
        PlayerStats keepStats = null; float keepHp = 0, keepVitalForce = 0, keepAlimus = 0; int keepLevel = 1, keepXp = 0, keepKills = 0, keepPotions = 1, keepMilestones = 0, keepKeys = 0;
        if (!freshPlayer && G.Player != null)
        {
            keepStats = G.Player.Stats; keepHp = G.Player.Hp; keepLevel = G.Player.Level; keepXp = G.Player.Xp; keepKills = G.Player.Kills; keepPotions = G.Player.Potions;
            keepKeys = G.Player.Keys;
            keepMilestones = G.Player.PendingMilestones;
            keepVitalForce = G.Player.VitalForce;
            keepAlimus = G.Player.Alimus;
        }
        foreach (var c in _world.GetChildren()) { _world.RemoveChild(c); c.QueueFree(); }
        G.Enemies.Clear();
        NetSync.BeginLevel();
        EnemyProjectiles.Clear();
        Breakables.All.Clear();
        RockLedge.All.Clear();
        _roomElites.Clear();
        ActiveBoss = null;
        _roomCells.Clear();
        _bossStrandedT = 0;
        _guardianDown = false;
        _victoryT = -1;
        ExitSpots.Clear();

        ulong t0 = Time.GetTicksMsec();
        var biome = G.Biome ??= Biomes.Get(BiomeId.Entrance);
        // (a new run starts with no relics carried)
        if (keepStats == null) RunRelics.Reset();
        var cave = pregenerated ?? CaveGenerator.Generate(biome, seed);
        G.Cave = cave;
        GD.Print($"[DaggerDeep] depth {G.Depth} {biome.Name} seed {seed}: generated in {Time.GetTicksMsec() - t0} ms, attempts {cave.Attempts}, trap cells {cave.TrapCells}, reachable {cave.ReachableCells}, rooms {cave.Rooms.Count}, spawns {cave.Spawns.Count}");
        _stage.BuildLevel(cave);

        var back = new Backdrop();
        back.Setup(cave);
        _world.AddChild(back);
        var view = new CaveView();
        _world.AddChild(view);
        view.Build(cave);
        var water = new WaterView();
        water.Setup(cave);
        _world.AddChild(water);

        _fx = new FxLayer();
        _world.AddChild(_fx);
        G.Fx = _fx;

        var player = new Player();
        if (keepStats != null) { player.Stats = keepStats; }
        else ClassPerks.Apply(player.Stats); // (a new run: the perks this hero brings)
        _world.AddChild(player);
        if (keepStats == null) ApplyLoadout(player);
        if (keepStats != null)
        {
            // (Spring Water: going deeper heals you to full)
            player.Hp = keepStats.DepthHeal ? keepStats.MaxHp : Math.Min(keepStats.MaxHp, keepHp + keepStats.MaxHp * 0.3f);
            player.Level = keepLevel; player.Xp = keepXp; player.Kills = keepKills; player.Potions = keepPotions;
            player.Keys = keepKeys;
            player.PendingMilestones = keepMilestones;
            player.SetVitalForce(Math.Max(keepVitalForce, Tune.Vitalist.VitalForceStart * 0.5f));
            // (alimus comes back by itself anyway: at least half a reserve on a new level)
            player.SetAlimus(Math.Max(keepAlimus, keepStats.AlimusMax * 0.5f));
            player.SyncCharges();
        }
        player.GlobalPosition = cave.StartPos;
        if (_startAt == "boss" && cave.Boss != null)
        {
            var at = cave.Boss.Center + new Vector2(-cave.Boss.RxPx * 0.55f, 0);
            if (cave.IsSolid(at)) at = cave.Boss.Center;
            player.GlobalPosition = at;
        }
        if (_startAt == "secret" && cave.Rooms.FirstOrDefault(r => r.Kind == RoomKind.Secret) is Room hidden && cave.FindFloor(hidden.Center + new Vector2(-30, 0), 300, out var hf))
            player.GlobalPosition = hf + new Vector2(0, -14);
        if (_startAt == "drain" && cave.Drain is Vector2 dpos) player.GlobalPosition = dpos + new Vector2(0, -110);
        if (_startAt == "water")
        {
            var sp = cave.Spawns.FirstOrDefault(s => s.Kind == SpawnKind.Water);
            if (sp != null) player.GlobalPosition = sp.Pos;
        }
        G.Player = player;

        _cam = new Camera2D { Zoom = new Vector2(Tune.Feel.CameraZoom, Tune.Feel.CameraZoom), ProcessCallback = Camera2D.Camera2DProcessCallback.Physics };
        _cam.LimitLeft = 0; _cam.LimitTop = 0;
        _cam.LimitRight = (int)cave.SizePx.X; _cam.LimitBottom = (int)cave.SizePx.Y;
        _world.AddChild(_cam);
        _cam.GlobalPosition = player.GlobalPosition;
        _cam.MakeCurrent();

        // the way back out, at the cave mouth (depth 0)
        if (cave.Mouth is Vector2 mouth)
        {
            var way = new Portal { Position = mouth + new Vector2(0, -30), Outside = true, Label = "the way out" };
            NetSync.LevelId(way);
            _world.AddChild(way);
        }

        // the chests come from the seed alone (online, every game places the same ones, in the same order)
        G.Rng = new Random(seed * 31 + G.Depth * 7 + 1);
        // Treasure chests are visible from the start (a few of the treasure rooms hold one).
        int roomChests = 0;
        foreach (var room in cave.Rooms.Where(r => r.Kind == RoomKind.Treasure).OrderBy(_ => G.Rng.Next()))
        {
            if (roomChests >= biome.RoomChests || !G.Chance(Tune.Drops.TreasureRoomChestChance)) continue;
            roomChests++;
            // Sit the chest on real ground (the room's floor line may have been cut by another tunnel).
            if (!cave.FindFloor(room.Center, 700, out var floor)) continue;
            var chest = MakeLevelChest(floor);
            NetSync.LevelId(chest);
            _world.AddChild(chest);
        }

        // the hidden chambers (a chimney up out of a tunnel's roof, out of reach of anyone without a way up): a silver chest in each
        foreach (var room in cave.Rooms.Where(r => r.Kind == RoomKind.Secret))
        {
            if (!cave.FindFloor(room.Center, 300, out var sf)) continue;
            var secret = new Chest { Position = sf, Tier = ChestTier.Relic };
            NetSync.LevelId(secret);
            _world.AddChild(secret);
        }

        PlaceCaches(cave);
        PlaceBonusChests(cave);
        PlaceHeroCage(cave);
        PlaceRubble(cave);
        PlaceLedges(cave);
        PlaceVault(cave);
        SpawnCritters(cave);
        if (cave.Liquid == Liquid.Water) PlaceAirVents(cave);
        PlaceHazards(cave);
        PlaceSecretWays(cave, seed);
        PlaceLampsAndHints(cave, seed);
        _hud.ResetMap(cave);
        _hud.ShowBanner(G.Depth == 0 ? biome.Name.ToUpperInvariant() : $"DEPTH {G.Depth}  ·  {biome.Name.ToUpperInvariant()}", 3f);
        _spawnT = 0;
        _waitingAt = null;
        NetSync.LevelBuilt();
    }

    /// <summary>
    /// The drain at the bottom of a lake (to the secret depth), and, in the secret depth itself (which has no guardian), the way on:
    /// two exits at the far shelf, open from the start. Both from the seed alone, so every game makes the same.
    /// </summary>
    private void PlaceSecretWays(CaveData cave, int seed)
    {
        var biome = G.Biome;
        // the stair you came down by, in the rock behind where you arrive (the first level you walked in from outside)
        if (G.Depth > 0 && biome.Id != BiomeId.Lair)
            _world.AddChild(new Portal { Position = cave.StartPos + new Vector2(0, -11), Entry = true, Label = "", To = biome });
        if (cave.Drain is Vector2 dr && G.Depth > 0 && G.Depth + 1 < Biomes.FinalDepth && biome.Id != BiomeId.Abyss)
        {
            var drain = new Portal { Position = dr, To = Biomes.Get(BiomeId.Abyss), Depth = G.Depth + 1, Label = "the drain", Drain = true };
            NetSync.LevelId(drain);
            _world.AddChild(drain);
        }
        if (!biome.NoGuardian || cave.Boss == null) return;
        _guardianDown = true;
        // the lake above comes down to meet you: a waterfall from a crack in the roof to the beach, a little way along from where you arrive
        {
            var near = cave.StartPos + new Vector2(8 * CaveData.Cell, -40);
            if (cave.FindFloor(near, 300, out var foot) && cave.FindCeiling(foot + new Vector2(0, -24), 1200, out var roof) && foot.Y - roof.Y > 120)
                _world.AddChild(new Waterfall { Position = foot + new Vector2(0, 1), Height = foot.Y - roof.Y, Width = 40 });
        }
        var room = cave.Boss;
        var rng = new Random(seed * 31 + 7);
        var exits = Biomes.ChooseExits(G.Depth, rng);
        ExitSpots.Clear();
        for (int k = 0; k < exits.Count; k++)
        {
            float off = exits.Count == 1 ? 0 : (k == 0 ? -1 : 1) * room.RxPx * 0.4f;
            var probe = new Vector2(room.Floor.X + off, room.Floor.Y - 40);
            if (cave.IsSolid(probe)) probe = room.Center;
            var floor = cave.FindFloor(probe, 400, out var f) ? f : room.Floor;
            var (bd, depth) = exits[k];
            string label = exits.Count == 1 ? $"depth {depth}" : depth - G.Depth == 1 ? $"depth {depth}  ·  the gentle way" : $"depth {depth}  ·  the steep way";
            ExitSpots.Add(floor + new Vector2(0, -16));
            var portal = new Portal { Position = floor + new Vector2(0, -30), To = bd, Depth = depth, Label = label };
            NetSync.LevelId(portal);
            _world.AddChild(portal);
        }
    }

    /// <summary>
    /// The Guild's lamps and the hidden ways in: now and then a lamp still burning in a dead end where one of the lost expeditions
    /// camped (with a page of the journal by it), and a mark at the mouth of each hidden crack or slit. From the seed alone.
    /// </summary>
    private void PlaceLampsAndHints(CaveData cave, int seed)
    {
        foreach (var (pos, kind) in cave.Hints) _world.AddChild(new SecretMouth { Position = pos, Kind = kind });
        var biome = cave.Biome;
        if (biome == null || G.Depth <= 0 || biome.Id is BiomeId.Entrance or BiomeId.Lair || JournalPageOf(biome.Id) == false) return;
        var rng = new Random(seed * 17 + 3);
        if (rng.NextDouble() > 0.7) return;
        bool Reach(Vector2 p)
        {
            int i = (int)(p.X / CaveData.Cell), j = (int)(p.Y / CaveData.Cell) - 1;
            return cave.ReachMask != null && i >= 0 && j >= 0 && i < cave.W && j < cave.H && cave.ReachMask[j * cave.W + i];
        }
        // a dead-end room (one of the treasure rooms, out of the way), on the far side from its chest
        var rooms = cave.Rooms.Where(r => r.Kind == RoomKind.Treasure && !r.Underwater && r.Center.DistanceTo(cave.StartPos) > 40 * CaveData.Cell).OrderBy(_ => rng.Next()).ToList();
        foreach (var room in rooms)
        {
            float side = rng.Next(2) == 0 ? -1f : 1f;
            foreach (float s in new[] { side, -side })
            {
                var probe = room.Center + new Vector2(s * room.RxPx * 0.55f, -10);
                if (cave.IsSolid(probe) || !cave.FindFloor(probe, 300, out var floor) || cave.IsWater(floor + new Vector2(0, -8)) || !Reach(floor)) continue;
                _world.AddChild(new GuildLamp { Position = floor, Biome = biome.Id });
                return;
            }
        }
    }

    private static bool JournalPageOf(BiomeId id) => Lore.Journal.Any(p => p.biome == id);

    /// <summary>Ambient wildlife: glow moths in dry tunnels, crabs on floors (including the sea bed).</summary>
    private void SpawnCritters(CaveData cave)
    {
        var rng = new Random(cave.Seed ^ 0x5eed);
        int moths = 0, crabs = 0;
        float share = (cave.Biome?.Critters ?? 60) / 60f * cave.W * cave.H / (250f * 150f);
        int wantMoths = (int)(Tune.Cave.Moths * share), wantCrabs = (int)(Tune.Cave.Crabs * share);
        for (int tries = 0; tries < 9000 && (moths < wantMoths || crabs < wantCrabs); tries++)
        {
            var pos = new Vector2(rng.Next(4, cave.W - 4) + 0.5f, rng.Next(4, cave.H - 4) + 0.5f) * CaveData.Cell;
            if (cave.IsSolid(pos) || pos.DistanceTo(cave.StartPos) < 120 || cave.IsLava(pos)) continue;
            if (moths < wantMoths && !cave.IsWater(pos) && rng.NextDouble() < 0.5)
            {
                _world.AddChild(new GlowMoth { Position = pos });
                moths++;
            }
            else if (crabs < wantCrabs && cave.FindFloor(pos, 160, out var fl) && !cave.IsSolid(fl + new Vector2(0, -8)) && !cave.IsLava(fl + new Vector2(0, -8)))
            {
                _world.AddChild(new CaveCrab { Position = fl + new Vector2(0, -5) });
                crabs++;
            }
        }
    }

    /// <summary>
    /// A chest of the level's own: wood, or (now and then) silver with a relic in it, and on the odd
    /// occasion strung up in a web from a tall ceiling. All from the seed, so every game makes the same.
    /// </summary>
    private Chest MakeLevelChest(Vector2 floor)
    {
        var chest = new Chest { Position = floor, Tier = G.Chance(Tune.Relics.RelicChestChance) ? ChestTier.Relic : ChestTier.Wood };
        var cave = G.Cave;
        if (cave != null && G.Chance(Tune.Relics.WebChestChance) && !cave.IsWater(floor + new Vector2(0, -10)))
        {
            float hang = Tune.Relics.WebHangHeight;
            // (a tall ceiling: room for the thread above the chest)
            if (cave.FindCeiling(floor + new Vector2(0, -hang - 20), 700, out var ce) && ce.Y <= floor.Y - hang - Tune.Relics.WebMinThread)
            {
                chest.Hung = true;
                chest.LandY = floor.Y;
                chest.Position = floor - new Vector2(0, hang);
            }
        }
        return chest;
    }

    /// <summary>
    /// Now and then a hero not yet unlocked sits caged somewhere hard to reach: high up, far from the
    /// start, off the beaten path.
    /// </summary>
    /// <summary>A new run: the side-grades this hero brought, applied before the first step.</summary>
    private static void ApplyLoadout(Player player)
    {
        var s = player.Stats;
        foreach (var id in Meta.LoadoutFor(s.Hero).ToList())
        {
            var u = Upgrades.Get(id);
            if (u == null || !u.Alteration || !Meta.SideGradeUnlocked(id) || !Upgrades.Available(u, s)) continue;
            u.Apply?.Invoke(s, player);
            s.Stacks[id] = s.StackOf(id) + 1;
        }
        player.SyncCharges();
        player.Hp = s.MaxHp;
    }

    /// <summary>The generated ledges and stepping stones, as slabs that can be broken (in the order the generator laid them: the same in every game).</summary>
    private void PlaceLedges(CaveData cave)
    {
        if (CaveGenerator.KeepLedges) return;
        int k = 0;
        foreach (var l in cave.Ledges)
            _world.AddChild(new RockLedge { Position = new Vector2(l.Cx, l.Cy) * CaveData.Cell, Half = l.Half * CaveData.Cell, Index = k++, Rec = l });
    }

    private void PlaceRubble(CaveData cave)
    {
        int k = 0;
        foreach (var (pos, size) in cave.Rubble)
            _world.AddChild(new Rubble { Position = pos, Size = size, Index = k++ });
    }

    private void PlaceHeroCage(CaveData cave)
    {
        // (never at the cave's mouth: nobody is caged where you walked in from outside, and the lair has none)
        if (G.Depth <= 0 || G.Biome?.Id is BiomeId.Lair or BiomeId.Entrance || cave.ReachMask == null) return;
        // (every game rolls the same dice whichever heroes it has, so the levels stay alike)
        bool wanted = G.Chance(G.Biome?.Id == BiomeId.Abyss ? Tune.Heroes.AbyssCageChance : Tune.Heroes.CageChance);
        var caged = Meta.PickCageHero(G.Range(0f, 1f));
        float pick = G.Range(0f, 1f);
        if (!wanted || caged is not HeroKind heroInCage) return;
        Vector2? spot = null;
        // a hidden chamber (a chimney up from a tunnel's roof, a fish slit, a spider crack) when the level has one, most of the time
        var secrets = cave.Rooms.Where(r => r.Kind == RoomKind.Secret).ToList();
        if (secrets.Count > 0 && pick < 0.7f)
        {
            var room = secrets[Math.Min(secrets.Count - 1, (int)(pick / 0.7f * secrets.Count))];
            // (beside the chamber's own chest, which stands at its middle)
            foreach (float dx in new[] { -26f, 26f, -44f, 44f })
                if (spot == null && !cave.IsSolid(room.Center + new Vector2(dx, 0)) && cave.FindFloor(room.Center + new Vector2(dx, 0), 300, out var f) && Math.Abs(f.Y - room.Center.Y) < 80f
                    && !cave.IsSolid(f + new Vector2(0, -14)) && !cave.IsWater(f + new Vector2(0, -10)))
                    spot = f;
        }
        if (spot == null)
        {
            // otherwise somewhere out of the way: high up and a long way from the start, off the beaten path
            float bestScore = float.MinValue;
            for (int tries = 0; tries < 1800; tries++)
            {
                var at = new Vector2(G.Range(64, cave.SizePx.X - 64), G.Range(60, cave.SizePx.Y - 40));
                if (cave.IsSolid(at) || cave.IsLava(at) || !cave.FindFloor(at, 300, out var floor)) continue;
                int i = (int)(floor.X / CaveData.Cell), j = (int)(floor.Y / CaveData.Cell) - 1;
                if (i < 0 || j < 0 || i >= cave.W || j >= cave.H || !cave.ReachMask[j * cave.W + i]) continue;
                if (cave.IsWater(floor + new Vector2(0, -10)) || cave.IsLava(floor + new Vector2(0, -8)) || floor.DistanceTo(cave.StartPos) < cave.SizePx.X * 0.35f) continue;
                if (cave.Boss != null && floor.DistanceTo(cave.Boss.Center) < cave.Boss.RxPx + 80) continue;
                if (Chest.All.Any(c => IsInstanceValid(c) && c.GlobalPosition.DistanceTo(floor) < 160)) continue;
                float score = (cave.WaterY - floor.Y) * 0.6f + floor.DistanceTo(cave.StartPos) * 0.4f + G.Range(0, 120);
                if (score > bestScore) { bestScore = score; spot = floor; }
            }
        }
        if (spot is not Vector2 at2) return;
        var cage = new HeroCage { Position = at2, Hero = heroInCage };
        NetSync.LevelId(cage);
        _world.AddChild(cage);
        if (_autotest) GD.Print($"[autotest] a caged {cage.Hero} at {at2}");
    }

    /// <summary>A Hunter's Map: more chests, scattered like the caches (dry or flooded), on top of the level's own.</summary>
    private void PlaceBonusChests(CaveData cave)
    {
        float bonus = RunRelics.ChestBonus;
        if (bonus <= 0 || cave.ReachMask == null) return;
        float want = Chest.All.Count * bonus;
        int n = (int)want + (G.Chance(want - (int)want) ? 1 : 0);
        var placed = new List<Vector2>();
        foreach (var c in Chest.All) if (IsInstanceValid(c)) placed.Add(c.GlobalPosition);
        foreach (var r in cave.Rooms) placed.Add(r.Floor);
        int made = 0;
        for (int tries = 0; tries < 2500 && made < n; tries++)
        {
            var at = new Vector2(G.Range(64, cave.SizePx.X - 64), G.Range(60, cave.SizePx.Y - 40));
            if (cave.IsSolid(at) || cave.IsLava(at) || !cave.FindFloor(at, 300, out var floor)) continue;
            int i = (int)(floor.X / CaveData.Cell), j = (int)(floor.Y / CaveData.Cell) - 1;
            if (i < 0 || j < 0 || i >= cave.W || j >= cave.H || !cave.ReachMask[j * cave.W + i]) continue;
            if (cave.IsLava(floor + new Vector2(0, -8)) || floor.DistanceTo(cave.StartPos) < 160) continue;
            if (cave.Boss != null && floor.DistanceTo(cave.Boss.Center) < cave.Boss.RxPx + 60) continue;
            if (placed.Any(q => q.DistanceTo(floor) < 240)) continue;
            placed.Add(floor);
            var chest = MakeLevelChest(floor);
            NetSync.LevelId(chest);
            _world.AddChild(chest);
            made++;
        }
        if (_autotest) GD.Print($"[autotest] bonus chests placed: {made} of {n}");
    }

    /// <summary>
    /// Extra chests away from the dead ends: some on the flooded floor (where movement upgrades are
    /// likeliest), some high in the dry caves (survival upgrades), and in the Magma Caverns a few
    /// sunk in the lava (for Magma Skin to reach), spread apart and reachable.
    /// </summary>
    private void PlaceCaches(CaveData cave)
    {
        var placed = new List<Vector2>();
        foreach (var r in cave.Rooms) placed.Add(r.Floor);
        bool Reachable(Vector2 p)
        {
            int i = (int)(p.X / CaveData.Cell), j = (int)(p.Y / CaveData.Cell) - 1;
            return cave.ReachMask != null && i >= 0 && j >= 0 && i < cave.W && j < cave.H && cave.ReachMask[j * cave.W + i];
        }
        void Scatter(int count, float yMin, float yMax, bool underwater, bool lava = false)
        {
            bool Sunk(Vector2 p) => lava ? cave.IsLava(p) : cave.IsWater(p);
            int made = 0;
            for (int tries = 0; tries < 2000 && made < count; tries++)
            {
                var at = new Vector2(G.Range(64, cave.SizePx.X - 64), G.Range(yMin, yMax));
                if (cave.IsSolid(at) || Sunk(at) != underwater) continue;
                if (!cave.FindFloor(at, 300, out var floor) || Sunk(floor + new Vector2(0, -10)) != underwater) continue;
                if (!underwater && floor.Y > yMax) continue;
                // (the lava lies low and narrow between the rooms: its chests may sit closer to them)
                if (!Reachable(floor) || placed.Any(q => q.DistanceTo(floor) < (lava ? 96 : 350))) continue;
                placed.Add(floor);
                var chest = MakeLevelChest(floor);
                NetSync.LevelId(chest);
                _world.AddChild(chest);
                made++;
            }
        }
        var bd = cave.Biome;
        if (cave.Liquid == Liquid.Water) Scatter(bd?.WaterCaches ?? Tune.Drops.WaterCaches, cave.WaterY + 40, cave.SizePx.Y - 40, true);
        if (cave.Liquid == Liquid.Lava && bd?.LavaCaches > 0) Scatter(bd.LavaCaches, cave.WaterY + 8, cave.SizePx.Y - 20, true, lava: true);
        float dryBottom = Math.Min(cave.WaterY, cave.SizePx.Y);
        Scatter(bd?.HighCaches ?? Tune.Drops.HighCaches, 60, dryBottom * Tune.Drops.HighZoneFraction, false);
        if (_autotest) GD.Print($"[autotest] caches placed: {placed.Count - cave.Rooms.Count}");
    }

    // ------------------------------------------------------------------ keys and the vault

    /// <summary>The level's vault gate, if it has one.</summary>
    public VaultGate Gate { get; private set; }
    /// <summary>The second vault's gate (a Locksmith's Ring).</summary>
    public VaultGate Gate2 { get; private set; }
    /// <summary>A mini-boss here has dropped the level's key already (the host's to track).</summary>
    private bool _keyDropped;

    /// <summary>
    /// The vault's iron gate across its passage and its chest in the chamber, then the level's
    /// hidden keys: one, or both where no mini-boss lairs to drop the other. They come from the
    /// seed alone (online, every game places the same ones, with the same ids).
    /// </summary>
    private void PlaceVault(CaveData cave)
    {
        Gate = null;
        _keyDropped = false;
        var v = cave.Vault;
        if (v == null) return;
        Gate = new VaultGate { Position = v.Gate, Top = v.GateTop, Side = v.Side };
        NetSync.LevelId(Gate);
        _world.AddChild(Gate);
        var chest = new Chest { Position = v.Chest, Vault = true };
        NetSync.LevelId(chest);
        _world.AddChild(chest);
        // a Locksmith's Ring: a second vault, and a key more to open it
        Gate2 = null;
        if (cave.ExtraVault is VaultSpot v2)
        {
            Gate2 = new VaultGate { Position = v2.Gate, Top = v2.GateTop, Side = v2.Side };
            NetSync.LevelId(Gate2);
            _world.AddChild(Gate2);
            var chest2 = new Chest { Position = v2.Chest, Vault = true };
            NetSync.LevelId(chest2);
            _world.AddChild(chest2);
        }
        int hidden = Tune.Vault.KeysPerLevel + (Gate2 != null ? 1 : 0) - (cave.Rooms.Any(r => r.Kind == RoomKind.MiniBoss) ? 1 : 0);
        PlaceHiddenKeys(cave, hidden);
    }

    /// <summary>
    /// Keys tucked away where you'd have to go looking: a treasure dead end with no chest in it,
    /// the flooded floor, a ledge up high, anywhere reachable well away from the start, the vault,
    /// the guardian and the chests.
    /// </summary>
    private void PlaceHiddenKeys(CaveData cave, int count)
    {
        var taken = new List<Vector2> { cave.StartPos, cave.Vault.Chest, cave.Vault.Gate };
        if (cave.ExtraVault != null) { taken.Add(cave.ExtraVault.Chest); taken.Add(cave.ExtraVault.Gate); }
        foreach (var c in Chest.All) if (IsInstanceValid(c)) taken.Add(c.GlobalPosition);
        bool Reachable(Vector2 p)
        {
            int i = (int)(p.X / CaveData.Cell), j = (int)(p.Y / CaveData.Cell) - 1;
            return cave.ReachMask != null && i >= 0 && j >= 0 && i < cave.W && j < cave.H && cave.ReachMask[j * cave.W + i];
        }
        var nooks = new List<Vector2>();
        foreach (var r in cave.Rooms.Where(r => r.Kind == RoomKind.Treasure))
            if (cave.FindFloor(r.Center, 700, out var f)) nooks.Add(f);
        int made = 0;
        for (int tries = 0; tries < 4000 && made < count; tries++)
        {
            Vector2 floor;
            if (nooks.Count > 0 && G.Chance(0.35f)) floor = nooks[G.Rng.Next(nooks.Count)];
            else
            {
                var at = new Vector2(G.Range(64, cave.SizePx.X - 64), G.Range(48, cave.SizePx.Y - 40));
                if (cave.IsSolid(at) || cave.IsLava(at) || !cave.FindFloor(at, 400, out floor)) continue;
            }
            if (cave.IsLava(floor + new Vector2(0, -8)) || !Reachable(floor)) continue;
            if (floor.DistanceTo(cave.StartPos) < Tune.Vault.HiddenKeyFromStart) continue;
            if (taken.Any(q => q.DistanceTo(floor) < Tune.Vault.HiddenKeySpacing)) continue;
            if (cave.Boss != null && floor.DistanceTo(cave.Boss.Center) < cave.Boss.RxPx + 60) continue;
            taken.Add(floor);
            var key = new KeyPickup { Position = floor + new Vector2(0, -6), Stashed = true, Puppet = Net.Online && !Net.IsHost };
            NetSync.LevelId(key);
            _world.AddChild(key);
            made++;
        }
        if (_autotest) GD.Print($"[autotest] hidden keys placed: {made} of {count}");
    }

    /// <summary>A key dropped mid-level (a mini-boss's): online, the host's, sent to every game.</summary>
    private void SpawnKey(Vector2 at)
    {
        // (beside the chest it dropped, clear of the rock)
        var spot = at + new Vector2(0, -10);
        foreach (float dx in new[] { 0f, -44f, 22f })
            if (!G.Cave.IsSolid(at + new Vector2(dx, -10))) { spot = at + new Vector2(dx, -10); break; }
        NetSync.Scope++;
        try { G.Spawn(new KeyPickup { Position = spot, Vy = -180f }); }
        finally { NetSync.Scope--; }
    }

    /// <summary>A vault's gate opened (in every game): a word for it.</summary>
    public void OnVaultOpened(VaultGate g)
    {
        _hud.ShowBanner("THE VAULT IS OPEN", 2.2f);
        _sfx.Play("levelup", null, -8, 0, 0.8f);
    }

    /// <summary>A sparse scattering of air vents on the flooded cave floor.</summary>
    private void PlaceAirVents(CaveData cave)
    {
        var vents = new List<Vector2>();
        for (int tries = 0; tries < 600 && vents.Count < Tune.Hero.AirVents; tries++)
        {
            var at = new Vector2(G.Range(48, cave.SizePx.X - 48), G.Range(cave.WaterY + 40, cave.SizePx.Y - 40));
            if (!cave.IsWater(at) || !cave.FindFloor(at, 600, out var floor)) continue;
            if (!cave.IsWater(floor + new Vector2(0, -10))) continue;
            if (vents.Any(v => v.DistanceTo(floor) < 260)) continue;
            vents.Add(floor);
            _world.AddChild(new AirVent { Position = floor });
        }
    }

    /// <summary>Walks through an exit tunnel: on to that biome, that many levels deeper.</summary>
    public void EnterExit(BiomeDef to, int depth)
    {
        // (--onelevel, for recordings: the run ends a moment after the way down is taken)
        if (OS.GetCmdlineUserArgs().Contains("--onelevel")) GetTree().CreateTimer(2.5).Timeout += () => SafeQuit.Request(this);
        CallDeferred(MethodName.GoDeeper, (int)(to?.Id ?? BiomeId.Slime), depth, 0);
    }

    private void GoDeeper(int biome, int depth, int seed)
    {
        if (Brains.Training) Brains.SaveAll();
        G.Depth = depth;
        G.Biome = Biomes.Get((BiomeId)biome);
        Meta.BestDepth = Math.Max(Meta.BestDepth, G.Depth);
        _seed = seed != 0 ? seed : _rng.Next(1, 999999);
        BuildLevelAsync(_seed, false, () =>
        {
            if (_bot != null) { G.Player.InputOverride = _bot.Read; _bot.Reset(); }
            // (online, a pick still open when the others went down carries on in the new level)
            if (_state == State.Choosing) G.Player.Choosing = Net.InRun;
            _sfx.SetMusic("ambient");
        });
    }

    /// <summary>
    /// Biome hazards and features: spore pods, webs, crystal spikes, fire vents on the floors;
    /// the frozen water surface and breakable ice ledges in the frost caverns.
    /// </summary>
    private void PlaceHazards(CaveData cave)
    {
        var b = cave.Biome;
        if (b == null) return;
        var rng = new Random(cave.Seed ^ 0x4a2a);
        var placed = new List<Vector2>();
        bool Clear(Vector2 at)
        {
            if (at.DistanceTo(cave.StartPos) < 260) return false;
            foreach (var r in cave.Rooms) if (r.Kind == RoomKind.Boss && at.DistanceTo(r.Center) < r.RxPx + 80) return false;
            return !placed.Any(q => q.DistanceTo(at) < 180);
        }
        void Floors(int n, Func<Vector2, Node2D> make)
        {
            int made = 0;
            for (int tries = 0; tries < 3000 && made < n; tries++)
            {
                var at = new Vector2(rng.Next(4, cave.W - 4) + 0.5f, rng.Next(4, cave.H - 4) + 0.5f) * CaveData.Cell;
                if (cave.IsSolid(at) || cave.IsWater(at) || cave.IsLava(at)) continue;
                if (!cave.FindFloor(at, 400, out var fl) || cave.IsWater(fl + new Vector2(0, -6)) || cave.IsLava(fl + new Vector2(0, -6))) continue;
                // flat enough to sit on
                if (!cave.IsSolid(fl + new Vector2(-12, 6)) || !cave.IsSolid(fl + new Vector2(12, 6)) || cave.IsSolid(fl + new Vector2(-12, -6)) || cave.IsSolid(fl + new Vector2(12, -6))) continue;
                if (!Clear(fl)) continue;
                placed.Add(fl);
                _world.AddChild(make(fl));
                made++;
            }
        }
        int count = (int)(b.HazardCount * cave.W * cave.H / (230f * 120f));
        if (b.Spores) Floors(count, fl => new SporePod { Position = fl });
        if (b.CrystalSpikes) Floors(count, fl => new CrystalSpikes { Position = fl });
        if (b.FireVents) Floors(count, fl => new FireVent { Position = fl });
        if (b.Webs) Floors(count, fl => new WebPatch { Position = fl + new Vector2(0, -26), Radius = 28 + rng.Next(8) });
        if (b.RootSnares) Floors(count, fl => new GraspingRoots { Position = fl + new Vector2(0, 2), Radius = 22 + rng.Next(10) });
        if (b.CaveIns)
        {
            // unstable ceilings over open floor, high enough for their dust to warn you
            int made = 0;
            for (int tries = 0; tries < 3000 && made < count; tries++)
            {
                var at = new Vector2(rng.Next(4, cave.W - 4) + 0.5f, rng.Next(4, cave.H - 4) + 0.5f) * CaveData.Cell;
                if (cave.IsSolid(at) || !cave.FindCeiling(at, 400, out var ce) || !cave.FindFloor(at, 400, out var fl)) continue;
                float drop = fl.Y - ce.Y;
                if (drop < 90 || drop > 360 || cave.IsWater(fl + new Vector2(0, -6))) continue;
                if (!Clear(fl)) continue;
                placed.Add(fl);
                _world.AddChild(new CaveIn { Position = ce + new Vector2(0, 3), Drop = drop });
                made++;
            }
        }
        if (b.IceSheet && cave.Liquid == Liquid.Water)
        {
            // the frozen surface: tiles wherever the water meets open air
            float wy = cave.WaterY;
            for (float x = 24; x < cave.SizePx.X - 24; x += 48)
            {
                if (cave.IsSolid(new Vector2(x, wy - 4)) || !cave.IsWater(new Vector2(x, wy + 6))) continue;
                if (cave.IsSolid(new Vector2(x - 20, wy + 2)) || cave.IsSolid(new Vector2(x + 20, wy + 2))) continue;
                _world.AddChild(new IceSheet { Position = new Vector2(x, wy), HalfW = 24 });
            }
        }
        foreach (var l in cave.IceLedges)
            _world.AddChild(new IcePlatform { Position = new Vector2(l.X, l.Y) * CaveData.Cell, HalfW = l.Z * CaveData.Cell });
    }

    private void StartPlaying()
    {
        ShowCampScene(false);
        _mainMenu.Visible = false;
        _heroChoice.Visible = false;
        _overlay.Visible = false;
        _overlay.HeroCards = false;
        _hud.Visible = true;
        GetTree().Paused = false;
        _state = State.Playing;
        _runTime = 0;
        G.RunTime = 0;
        _waveT = Tune.Spawning.FirstWave;
        _victory = false;
        _runEmbers = 0;
        _runFinds = "";
        Meta.RunUnlocks = 0;
        // (walk back out of the cave mouth and none of this run is kept, not even that it happened)
        _metaAtStart = Meta.Snapshot();
        Meta.Runs++;
        _sfx.SetMusic("ambient");
    }

    private Godot.Collections.Dictionary _metaAtStart;

    /// <summary>The hero walked out of the cave mouth (depth 0): the run ends on the spot and nothing from it is kept.</summary>
    public void LeaveCave() => CallDeferred(MethodName.WalkOut);

    private void WalkOut()
    {
        if (Net.InRun || _state is State.Title or State.Dead) return;
        Meta.Restore(_metaAtStart);
        _metaAtStart = null;
        ResetToTitle();
        ShowHeroChoice();
        FadeFrom(new Color(1f, 0.98f, 0.92f), 1.3f);
    }

    private static readonly int HeroCount = Enum.GetValues<HeroKind>().Length;

    private void PickHero(HeroKind h)
    {
        if (G.Hero == h) return;
        G.Hero = h;
        _sfx.Play("ui", null, -6);
        _overlay.QueueRedraw();
    }

    private void Restart()
    {
        if (Net.Online) { BackToLobby(); return; }
        if (Brains.Training) Brains.SaveAll();
        G.Depth = 0;
        G.Biome = Biomes.Get(BiomeId.Entrance);
        _seed = _rng.Next(1, 999999);
        _pendingTreasure.Clear();
        BuildLevel(_seed, freshPlayer: true);
        _hud.HintTime = 8;
        StartPlaying();
    }

    // ------------------------------------------------------------------ flow

    public void OnPlayerDied()
    {
        if (Net.InRun) { OnlineHeroDown(); return; }
        if (Brains.Training) Brains.SaveAll();
        _state = State.Dead;
        _deadT = 0;
        _deaths++;
        _sfx.SetMusic("");
        Meta.Save();
    }

    /// <summary>The camp between runs: how the run went, what it earned, the heroes, and the trees.</summary>
    private void ShowCamp()
    {
        // (the creatures of the run wait until the death screen comes up: then they go, and their sounds with them)
        ClearRunActors();
        if (Net.Online) { ShowCampScene(true); ShowOnlineCamp(); return; }
        var p = G.Player;
        int secs = (int)_runTime;
        string earned = _runEmbers > 0 || _runFinds != "" ? $"Earned: {_runEmbers} ember{(_runEmbers == 1 ? "" : "s")}{_runFinds}" : "";
        ShowHeroChoice(_victory ? "VICTORY" : "YOU DIED",
            (_victory ? "The Elder Dragon is slain. The deep is quiet... for now." : $"Fell at depth {G.Depth} in the {G.Biome?.Name ?? "cave"}")
            + $"\nLevel {p.Level}   ·   {p.Kills} kills   ·   {secs / 60}:{secs % 60:00}" + (earned != "" ? "\n" + earned : ""));
    }

    /// <summary>The run is over: its creatures and their projectiles leave the world (and the air), so nothing of the cave carries on behind the camp.</summary>
    private void ClearRunActors()
    {
        foreach (var e in G.Enemies.ToArray()) if (IsInstanceValid(e)) { e.GetParent()?.RemoveChild(e); e.QueueFree(); }
        G.Enemies.Clear();
        NetSync.Enemies.Clear();
        foreach (var pr in EnemyProjectiles.ToArray()) if (IsInstanceValid(pr)) pr.QueueFree();
        EnemyProjectiles.Clear();
        ActiveBoss = null;
        _roomElites.Clear();
        _sfx.StopAll();
    }

    private void OnMetaClosed()
    {
        // (opened from the online lobby: back to it)
        if (_onlineMenu != null && _onlineMenu.Visible) { _onlineMenu.AfterSubmenu(); return; }
        if (_state == State.Dead) ShowCamp();
        else if (_state == State.Title && _front == Front.Heroes) { _heroChoice.Visible = true; _heroChoice.Refresh(); _heroChoice.RestoreFocus(); }
        else if (_state == State.Title) ShowTitle();
    }

    /// <summary>This game's hero looked into a chest: offer its cards next.</summary>
    public void OfferChest(Chest c) { if (!_pendingTreasure.Contains(c)) _pendingTreasure.Enqueue(c); }

    /// <summary>Chests open their cards at once; a milestone waits until the player asks for it (the milestone key), so it never interrupts a fight.</summary>
    private void TryOpenUpgradeMenu(bool milestoneAsked = false)
    {
        var p = G.Player;
        if (p == null || p.Dead || _upgradeMenu.Visible) return;
        // chests hand out the real upgrades (their cards stay the same until one is taken);
        // milestones a big boost
        List<Upgrade> choices;
        List<string> locks = null;
        Chest chest = null;
        while (_pendingTreasure.Count > 0 && chest == null)
        {
            var c = _pendingTreasure.Dequeue();
            if (IsInstanceValid(c) && !c.Open && c.Cards != null) chest = c;
        }
        if (chest != null)
        {
            choices = chest.Cards.Select(Upgrades.Find).Where(u => u != null).ToList();
            // (online, a card may be a friend's: shown, but theirs to take)
            locks = choices.Select(u => Upgrades.LockReason(u, p.Stats)).ToList();
            // a relic is its finder's alone: the others may see it, never take it
            if (chest.RelicFor != 0 && chest.RelicFor != Net.Me)
                for (int k = 0; k < choices.Count; k++)
                    if (choices[k].Relic && locks[k] == null) locks[k] = "MEANT FOR " + Net.NameOf(chest.RelicFor);
            choices.Add(Upgrades.LeaveChest);
            locks.Add(null);
        }
        else if (p.PendingMilestones > 0 && milestoneAsked)
        {
            p.PendingMilestones--;
            choices = Upgrades.RollMilestone(p.Stats, _rng);
            if (choices.Count == 0) { p.Heal(30); return; }
            choices.Add(Upgrades.Skip);
        }
        else return;
        _lookingChest = chest;
        _state = State.Choosing;
        if (Net.InRun) p.Choosing = true;
        else GetTree().Paused = true;
        _sfx.Play(chest != null ? "chest" : "levelup");
        _upgradeMenu.Open(choices, chest != null ? (chest.Vault ? "THE VAULT!" : chest.Tier == ChestTier.Boss ? "THE GUARDIAN'S HOARD!" : chest.Tier == ChestTier.Relic ? "A RELIC!" : "TREASURE!") : "MILESTONE!", locks);
        _autoPickT = 0.5f;
    }

    private void OnUpgradePicked(Upgrade u)
    {
        var chest = _lookingChest;
        _lookingChest = null;
        if (u.Icon == "skip")
        {
            _sfx.Play("ui", null, 0, 0, 0.7f);
            // the chest closes again with its cards (for later, or for a friend)
            if (chest != null)
            {
                _hud.ShowBanner("LEFT FOR LATER", 1.4f);
                if (IsInstanceValid(chest)) NetSync.ChestDone(chest, false);
            }
        }
        else
        {
            Upgrades.Apply(u, G.Player.Stats, G.Player);
            _sfx.Play("ui");
            _hud.ShowBanner(u.Name, 1.6f);
            // a card taken: the chest is spent
            if (chest != null && IsInstanceValid(chest))
            {
                if (Net.Online) NetSync.ChestDone(chest, true);
                else chest.OpenBy(Net.Me);
            }
        }
        if (G.Player != null) G.Player.Choosing = false;
        GetTree().Paused = false;
        _state = State.Playing;
    }

    // ------------------------------------------------------------------ pause and settings

    private void PauseGame()
    {
        _state = State.Paused;
        if (!Net.InRun) GetTree().Paused = true;
        _pauseMenu.Open(online: Net.InRun, host: Net.IsHost);
    }

    private void Unpause()
    {
        _pauseMenu.Visible = false;
        if (_state != State.Paused) return;
        GetTree().Paused = false;
        _state = State.Playing;
    }

    private void OpenSettings()
    {
        _pauseMenu.Visible = false;
        _settingsMenu.Open();
    }

    private void OpenBuild()
    {
        _pauseMenu.Visible = false;
        _buildPanel.Open();
    }

    private void OnBuildClosed()
    {
        if (_state == State.Paused) _pauseMenu.Open(online: Net.InRun, host: Net.IsHost);
    }

    private void OnSettingsClosed()
    {
        if (_state == State.Paused) _pauseMenu.Open(online: Net.InRun, host: Net.IsHost);
        else if (_state == State.Title) ShowTitle();
    }

    /// <summary>Gives up the run from the pause menu: the hero falls, and it's back to camp (online: leaves the game).</summary>
    private void GiveUpRun()
    {
        Unpause();
        if (Net.Online) { LeaveOnline(Net.IsHost ? "You ended the online game." : "You left the online game."); return; }
        G.Player?.GiveUp();
    }

    /// <summary>Freezes the action for a beat so hits land with weight.</summary>
    public void HitStop(float seconds, float timeScale = -1f)
    {
        // measured in unscaled frame time (not the wall clock) so it also works when rendering a movie
        if (timeScale < 0) timeScale = Tune.Feel.HitStopTimeScale;
        _hitStopLeft = Math.Max(_hitStopLeft, seconds);
        Engine.TimeScale = Math.Min(Engine.TimeScale, timeScale);
    }

    /// <summary>Kill-cam style slow motion (real-time duration).</summary>
    public void SlowMo(float seconds, float timeScale = 0.3f) => HitStop(seconds, timeScale);

    /// <summary>Nudges the camera (decays quickly) - used on hits for directional impact.</summary>
    public void Kick(Vector2 offset) => _kick += offset * GameSettings.Shake;

    /// <summary>Controller rumble, only while a controller is the active device.</summary>
    public void Rumble(float weak, float strong, float seconds)
    {
        if (!UsingPad || !GameSettings.Vibration) return;
        foreach (int id in Input.GetConnectedJoypads()) Input.StartJoyVibration(id, weak, strong, seconds);
    }

    /// <summary>True when the last input came from a controller (drives prompts and hides the mouse).</summary>
    public bool UsingPad;
    private Vector2 _kick;

    public override void _Input(InputEvent e)
    {
        GameSettings.NoteInput(e);
        bool pad = e is InputEventJoypadButton { Pressed: true } || (e is InputEventJoypadMotion jm && Math.Abs(jm.AxisValue) > 0.5f);
        bool kbm = e is InputEventKey { Pressed: true } || e is InputEventMouseButton { Pressed: true } || (e is InputEventMouseMotion mm && mm.Relative.Length() > 3);
        if (pad && !UsingPad) { UsingPad = true; Input.MouseMode = Input.MouseModeEnum.Hidden; }
        else if (kbm && UsingPad) { UsingPad = false; Input.MouseMode = Input.MouseModeEnum.Visible; }
    }

    public override void _Notification(int what)
    {
        // closing the window (or quitting from code) keeps what was learned
        if ((what == NotificationWMCloseRequest || what == NotificationExitTree) && Brains.Training) Brains.SaveAll();
        if (what == NotificationWMCloseRequest) SafeQuit.Request(this);
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is InputEventKey { Pressed: true, Echo: false } k)
        {
            if (k.PhysicalKeycode == Key.F9)
            {
                Brains.Training = !Brains.Training;
                if (!Brains.Training) Brains.SaveAll();
                _hud.ShowBanner(Brains.Training ? "ENEMY TRAINING ON" : "ENEMY TRAINING OFF (saved)", 1.6f);
                GetViewport().SetInputAsHandled();
                return;
            }
            if (k.PhysicalKeycode == Key.F10)
            {
                Brains.SaveAll(force: true);
                _hud.ShowBanner("BRAINS SAVED", 1.2f);
                GetViewport().SetInputAsHandled();
                return;
            }
            if (k.PhysicalKeycode == Key.F7)
            {
                // debug: the 2D simulation drawn over the 3D view
                _world.Visible = !_world.Visible;
                GetViewport().SetInputAsHandled();
                return;
            }
            if (k.PhysicalKeycode == Key.F8 && Brains.Training)
            {
                Brains.ShowLabels = !Brains.ShowLabels;
                GetViewport().SetInputAsHandled();
                return;
            }
        }
        if (_metaMenu.Visible || _perkMenu.Visible || _loadoutMenu.Visible || _onlineMenu.Visible) return;
        if (_state == State.Playing && e.IsActionPressed("pause") && !_settingsMenu.Visible && !_pauseMenu.Visible && !BuildPanel.Showing)
        {
            PauseGame();
            GetViewport().SetInputAsHandled();
            return;
        }
        if (_settingsMenu.Visible || _departT >= 0f) return;
        // at the camp: the main menu (its buttons take keys and the controller themselves), or
        // choosing a hero at the fire (after a run too, once its summary has had a moment)
        bool atMenu = _state == State.Title && _mainMenu.Visible;
        bool choosing = _heroChoice.Visible && !Net.Online && (_state == State.Title || (_state == State.Dead && _deadT > 1.5f));
        if ((atMenu || choosing) && e.IsActionPressed("online"))
        {
            OpenOnlineMenu();
            GetViewport().SetInputAsHandled();
            return;
        }
        if (atMenu && e.IsActionPressed("pause"))
        {
            OpenFrontSettings();
            GetViewport().SetInputAsHandled();
            return;
        }
        if (!choosing) return;
        if (_heroChoice.Stage == HeroChoice.StageKind.Hero && (e.IsActionPressed("move_left") || e.IsActionPressed("move_right") || e.IsActionPressed("ui_left") || e.IsActionPressed("ui_right")))
        {
            StepHero(e.IsActionPressed("move_left") || e.IsActionPressed("ui_left") ? -1 : 1);
            GetViewport().SetInputAsHandled();
        }
        else if (e.IsActionPressed("pause") || e.IsActionPressed("ui_cancel"))
        {
            // back a stage (from the first, to the main menu)
            _heroChoice.Retreat();
            GetViewport().SetInputAsHandled();
        }
        else if (e.IsActionPressed("confirm") || e.IsActionPressed("ui_accept") || e.IsActionPressed("restart"))
        {
            // (a button with the cursor on it takes its own accept: this is only for when nothing does)
            var focus = GetViewport().GuiGetFocusOwner();
            if (e.IsActionPressed("ui_accept") && focus is BaseButton && _heroChoice.IsAncestorOf(focus)) return;
            // on a stage (the last goes down into the cave)
            _heroChoice.Advance();
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _Process(double delta)
    {
        if (SafeQuit.Quitting) return;
        float dt = (float)delta;
        // This frame's delta was scaled by whatever time scale was in effect when the frame began,
        // so un-scale it with that value (not with a scale a hit may have set during this frame).
        float unscaled = dt / Math.Max(0.001f, _frameScale);
        if (_hitStopLeft > 0)
        {
            _hitStopLeft -= unscaled;
            if (_hitStopLeft <= 0) Engine.TimeScale = 1;
        }
        _frameScale = (float)Engine.TimeScale;

        if (_padTest) PadTestTick(dt);
        if (_loading) { TickLoading(dt); if (_scenario == "loading") LoadingScenario(); return; }
        if (_campShot != "") { CampShotTick(dt); return; }
        if (_frontTest != "") FrontTestTick(dt);
        if (_menuShot != "") MenuShotTick();
        if (_netTest != "") NetTestTick(dt);
        if (_onlineShot != "") OnlineShotTick();
        if (_scenario != "") ScenarioTick(dt);
        UpdateTitleButton();
        TickFront(dt);
        switch (_state)
        {
            case State.Title:
                _titleT += dt;
                if (_metaShot != "") MetaShotTick();
                if (!_metaMenu.Visible && !_perkMenu.Visible && !_onlineMenu.Visible && (_front != Front.Heroes || _heroChoice.InPrep) && Input.IsActionJustPressed("meta") && Meta.Trees.Any(Meta.Visible)) _metaMenu.Open(MetaMenu.Mode.Browse);
                if (_front == Front.Heroes && _heroChoice.InPrep && !_metaMenu.Visible && !_perkMenu.Visible && !_loadoutMenu.Visible && !_onlineMenu.Visible && _departT < 0f && Input.IsActionJustPressed("perks")) _perkMenu.Open(G.Hero);
                if (_front == Front.Heroes && _heroChoice.InPrep && !_metaMenu.Visible && !_perkMenu.Visible && !_loadoutMenu.Visible && !_onlineMenu.Visible && _departT < 0f && Input.IsActionJustPressed("loadout")) _loadoutMenu.Open(G.Hero);
                if (_titleShot != "" && _titleT > 1.5f)
                {
                    GetViewport().GetTexture().GetImage().SavePng(_titleShot);
                    SafeQuit.Request(this);
                }
                return;
            case State.Paused:
                // (online, the cave carries on while you're in the pause menu)
                if (!Net.InRun) return;
                break;
            case State.Dead:
                _deadT += dt;
                if (_metaMenu.Visible || _perkMenu.Visible) break;
                if (_deadT > 1.2f && !_overlay.Visible && !_heroChoice.Visible)
                {
                    ShowCamp();
                    // back at camp after the first reagent: the potion tree's introduction
                    if (!_autotest && Meta.Visible(Meta.PotionTree) && !Meta.PotionTutorialDone) _metaMenu.Open(MetaMenu.Mode.PotionTutorial);
                    else if (!_autotest && Meta.Visible(Meta.PearlTree) && !Meta.PearlTutorialDone) _metaMenu.Open(MetaMenu.Mode.PearlIntro);
                }
                bool summary = _overlay.Visible || _heroChoice.InPrep;
                if (_deadT > 1.5f && summary && Input.IsActionJustPressed("perks")) _perkMenu.Open(G.Hero);
                else if (_deadT > 1.5f && summary && Input.IsActionJustPressed("meta") && Meta.Trees.Any(Meta.Visible)) _metaMenu.Open(MetaMenu.Mode.Browse);
                // (online: on to the lobby; alone, the hero choice at the fire takes it from here)
                else if (Net.Online && _deadT > 1.5f && _overlay.Visible && (Input.IsActionJustPressed("restart") || Input.IsActionJustPressed("confirm"))) Restart();
                if (_autotest && _deadT > 3f) { if (_fullRun && _victory) { FinishFullRun(true); return; } Restart(); }
                break;
            case State.Choosing:
                if (_autotest)
                {
                    _autoPickT -= dt;
                    if (!_menuShotDone && _shotDir != "" && _autoPickT < 0.3f)
                    {
                        _menuShotDone = true;
                        GetViewport().GetTexture().GetImage().SavePng($"{_shotDir}/menu.png");
                    }
                    if (_autoPickT <= 0) _upgradeMenu.ChooseFirstOpen();
                }
                // (online, the cave carries on while you pick)
                if (!Net.InRun) return;
                break;
            case State.Playing:
                _runTime += dt;
                // (the host's clock is the difficulty's clock: a client takes it from the host)
                if (!Net.IsClient) G.RunTime = _runTime;
                Brains.Tick(unscaled);
                TryOpenUpgradeMenu(!MenuOpen && Input.IsActionJustPressed("milestone"));
                break;
        }

        UpdateCamera(dt);
        var player = G.Player;
        bool worldRuns = _state == State.Playing || (Net.InRun && _state is State.Paused or State.Choosing);
        if (player != null && worldRuns)
        {
            _sfx.SetUnderwater(player.HeadUnder);
            _spawnT -= dt;
            // the host's game (or a game alone) runs the cave: creatures, rooms, the guardian
            if (_spawnT <= 0 && !Net.IsClient)
            {
                _spawnT = 0.25f;
                NetSync.Scope++;
                try { RunSpawner(0.25f); RunRooms(); WatchGuardian(0.25f); }
                finally { NetSync.Scope--; }
            }
            if (ActiveBoss != null && (ActiveBoss.Dead || !IsInstanceValid(ActiveBoss))) ActiveBoss = null;
            if (_victoryT > 0)
            {
                _victoryT -= dt;
                if (_victoryT <= 0)
                {
                    if (Net.IsHost) { NetSync.SendRunOver(true); OnlineRunOver(true); }
                    else if (!Net.InRun) { _state = State.Dead; _deadT = 0; _sfx.SetMusic(""); Meta.Save(); }
                }
            }
            if (Net.InRun) OnlineTick(dt);
        }
        if (_showcase) ShowcaseTick(dt);
        if (_lookShot != "") LookShotTick();
        if (_autotest) AutotestTick(dt);
        if (_bestiary) BestiaryTick(dt);
        if (_animTest) AnimTestTick(dt);
        if (_heroTest) HeroTestTick(dt);
        if (_hitStopTest) HitStopTestTick();
    }

    private Player _spectate;

    private void UpdateCamera(float dt)
    {
        var p = G.Player;
        if (p == null || _cam == null) return;
        if (p.Dead && Net.InRun)
        {
            // watching a friend: attack or ability steps to the next, or back to the one before
            var alive = new List<Player>();
            foreach (var h in G.Players) if (IsInstanceValid(h) && !h.Dead) alive.Add(h);
            if (alive.Count > 0)
            {
                int step = Input.IsActionJustPressed("attack") ? 1 : Input.IsActionJustPressed("ability") ? -1 : 0;
                if (step != 0 && !MenuOpen)
                {
                    int cur = _spectate != null ? alive.IndexOf(_spectate) : -1;
                    _spectate = alive[((cur + step) % alive.Count + alive.Count) % alive.Count];
                    _hud?.ShowBanner($"WATCHING {(_spectate.Stats.Hero).ToString().ToUpperInvariant()}  ·  attack: next  ·  ability: back", 1.6f);
                }
                if (_spectate == null || !alive.Contains(_spectate)) _spectate = alive[0];
                p = _spectate;
            }
        }
        else _spectate = null;
        var target = p.GlobalPosition + new Vector2(p.Velocity.X * 0.15f, p.Velocity.Y * 0.08f - 10);
        // lean toward the guardian only when it's in the fight with you: one woken far away (by a
        // friend, online) never drags your view off your own hero
        var boss = ActiveBoss;
        if (boss != null && IsInstanceValid(boss) && !boss.Dead)
        {
            var half = ViewHalf(0);
            var off = boss.GlobalPosition - p.GlobalPosition;
            float reach = Math.Max(Math.Abs(off.X) / half.X, Math.Abs(off.Y) / half.Y); // 1 = at the view's edge
            float lean = 0.25f * Math.Clamp(1.6f - reach, 0f, 1f);
            if (lean > 0) target = target.Lerp(boss.GlobalPosition, lean);
        }
        _cam.GlobalPosition = _cam.GlobalPosition.Lerp(target, 1 - MathF.Exp(-dt * Tune.Feel.CameraFollowSharpness));
        float s = (_fx?.Shake ?? 0) * GameSettings.Shake;
        _kick = _kick.Lerp(Vector2.Zero, 1 - MathF.Exp(-dt * 14));
        // hold the frame perfectly still during a hit-stop; the shake plays out once time resumes
        if (_hitStopLeft <= 0)
            _cam.Offset = (s > 0 ? new Vector2(G.Range(-s, s), G.Range(-s, s)) * 0.5f : Vector2.Zero) + _kick;
    }

    // ------------------------------------------------------------------ spawning

    private float _waveT = Tune.Spawning.FirstWave;
    private readonly List<(Enemy e, float d0, float t0)> _entrants = new();

    /// <summary>Half the view's size in the world, plus a margin.</summary>
    private Vector2 ViewHalf(float margin) => GetViewport().GetVisibleRect().Size / _cam.Zoom * 0.5f + new Vector2(margin, margin);

    private static bool InView(Vector2 p, Vector2 center, Vector2 half) => Math.Abs(p.X - center.X) < half.X && Math.Abs(p.Y - center.Y) < half.Y;

    /// <summary>Where a hero's view is centred (this game's camera, or a friend's hero).</summary>
    private Vector2 ViewCenter(Player h) => h == null || h == G.Player ? _cam.GetScreenCenterPosition() : h.GlobalPosition + new Vector2(0, -10);

    /// <summary>True if a world point is inside the camera's view (plus a margin); online, anyone's view.</summary>
    private bool OnScreen(Vector2 p, float margin = 40f)
    {
        var half = ViewHalf(margin);
        if (InView(p, _cam.GetScreenCenterPosition(), half)) return true;
        if (Net.InRun) foreach (var h in G.Players) if (h.IsRemote && InView(p, ViewCenter(h), half)) return true;
        return false;
    }

    private int _anchorTurn;

    /// <summary>The hero the spawner works around this time (online, each standing hero in turn).</summary>
    private Player SpawnAnchor()
    {
        if (!Net.InRun) return G.Player;
        var alive = G.Players.Where(h => !h.Dead).ToList();
        if (alive.Count == 0) return G.Player;
        return alive[_anchorTurn++ % alive.Count];
    }

    /// <summary>
    /// Two sources of enemies. Residents sit at the cave's pre-computed spawn points and are only
    /// ever created out of view, so they are simply "there" when you arrive; early on many
    /// points stay empty. Entrances arrive in waves on a timer that shortens as the run goes on:
    /// they appear at a point along the tunnels just out of view and come to you by their own
    /// means (bats fly in, frogs hop, goblins run, fish swim).
    /// </summary>
    private void RunSpawner(float dt)
    {
        var cave = G.Cave;
        var p = SpawnAnchor();
        if (p == null || p.Dead) return;
        // only enemies in the neighbourhood count toward the cap (far-off residents are asleep)
        var biome = G.Biome;
        int alive = G.Enemies.Count(e => !e.Dead && e.GlobalPosition.DistanceSquaredTo(p.GlobalPosition) < 900 * 900);
        int cap = Math.Max(2, (int)((Tune.Spawning.CapBase + G.Pace * Tune.Spawning.CapPerPace) * Math.Max(0.5f, biome.Density) * NetSync.CapScale * Tune.Spawning.SpawnShare));
        // Residents: how many spawn points actually hold a group rises from ~40% to 100% over the run
        // (sooner the deeper you are); sparse biomes stay sparse.
        float fill = biome.ResidentFill >= 0 ? biome.ResidentFill
            : Math.Min(1f, Tune.Spawning.ResidentFillStart + G.Depth * Tune.Spawning.ResidentFillPerDepth + G.RunTime / (Tune.Spawning.ResidentFillMinutes * 60f) * (1f - Tune.Spawning.ResidentFillStart)) * Math.Min(1f, biome.Density);
        fill *= Tune.Spawning.SpawnShare;
        foreach (var sp in cave.Spawns)
        {
            float d = sp.Pos.DistanceTo(p.GlobalPosition);
            if (sp.Used)
            {
                sp.Cooldown -= dt;
                if (sp.Cooldown <= 0 && d > 1000) sp.Used = false;
                continue;
            }
            if (d > Tune.Spawning.ResidentMaxDistance || alive >= cap || d < Tune.Spawning.MinSpawnDistance || OnScreen(sp.Pos, Tune.Spawning.OffscreenMargin)) continue;
            sp.Used = true;
            if (!G.Chance(fill)) { sp.Cooldown = G.Range(40, 80); continue; }
            sp.Cooldown = G.Range(Tune.Spawning.ResidentRespawnMin, Tune.Spawning.ResidentRespawnMax) / (1f + G.Pace);
            alive += SpawnGroup(sp);
        }

        // Entrances: slow at first; the rate doubles every RateDoublingMinutes, like the difficulty.
        _waveT -= dt;
        if (biome.Waves && _waveT <= 0 && alive < cap + 4)
        {
            float rate = MathF.Pow(2f, G.RunTime / (Tune.Spawning.RateDoublingMinutes * 60f));
            _waveT = Math.Max(Tune.Spawning.IntervalMin, Tune.Spawning.IntervalStart / rate) * Tune.Spawning.WaveGapMult * G.Range(0.8f, 1.2f);
            SpawnEntrance(p);
        }
    }

    /// <summary>
    /// Brings in a wave from all around the view: each newcomer gets its own entry point in a band
    /// just outside the screen edges, spread across different directions, and reachable along the
    /// tunnels (so it has a route to you). What comes in suits the spot: fish in water, bats where
    /// there's no floor, walkers on the ground.
    /// </summary>
    private void SpawnEntrance(Player p)
    {
        var cave = G.Cave;
        int W = cave.W, H = cave.H;
        var start = new Vector2I((int)(p.GlobalPosition.X / CaveData.Cell), (int)(p.GlobalPosition.Y / CaveData.Cell));
        var dist = new Dictionary<int, int>();
        var q = new Queue<Vector2I>();
        q.Enqueue(start); dist[start.Y * W + start.X] = 0;
        var band = new List<Vector2>();   // just off-screen: the ideal entry points
        var farther = new List<Vector2>(); // fallback when the band is all rock
        float edge = Tune.Spawning.EntranceBandPx;
        // (a vault's shut gate is as good as rock: nothing comes in behind it)
        var gate = Gate != null && IsInstanceValid(Gate) && !Gate.Opened ? Gate : null;
        while (q.Count > 0)
        {
            var c = q.Dequeue();
            int d = dist[c.Y * W + c.X];
            var w = new Vector2(c.X + 0.5f, c.Y + 0.5f) * CaveData.Cell;
            if (!OnScreen(w, Tune.Spawning.OffscreenMargin) && w.DistanceTo(p.GlobalPosition) >= Tune.Spawning.MinSpawnDistance)
            {
                if (InView(w, ViewCenter(p), ViewHalf(Tune.Spawning.OffscreenMargin + edge))) band.Add(w);
                else farther.Add(w);
            }
            if (d >= Tune.Spawning.EntranceMaxCells) continue;
            foreach (var o in new[] { new Vector2I(1, 0), new Vector2I(-1, 0), new Vector2I(0, 1), new Vector2I(0, -1) })
            {
                var n = c + o;
                if (n.X < 0 || n.Y < 0 || n.X >= W || n.Y >= H || dist.ContainsKey(n.Y * W + n.X) || !cave.CellOpen(n.X, n.Y)) continue;
                if (gate != null && gate.BlocksCell(n)) continue;
                dist[n.Y * W + n.X] = d + 1;
                q.Enqueue(n);
            }
        }
        var pool = band.Count > 0 ? band : farther;
        if (pool.Count == 0) return;

        // sort the candidates into 8 directions around the view and take each newcomer from a
        // different direction (cycling if the wave is bigger than the directions available)
        var center = ViewCenter(p);
        var sectors = new List<Vector2>[8];
        for (int k = 0; k < 8; k++) sectors[k] = new List<Vector2>();
        foreach (var w in pool)
        {
            float ang = Mathf.PosMod((w - center).Angle(), Mathf.Tau);
            sectors[Math.Min(7, (int)(ang / Mathf.Tau * 8))].Add(w);
        }
        var open = sectors.Where(s => s.Count > 0).OrderBy(_ => G.Rng.Next()).ToList();
        int count = 1 + G.RangeI(0, (int)(G.Pace * Tune.Spawning.WaveGrowthPerPace));

        for (int k = 0; k < count; k++)
        {
            var list = open[k % open.Count];
            var at = list[G.Rng.Next(list.Count)];
            var e = EntrantFor(at, out var pos);
            if (e == null) continue;
            e.Engage();
            e.Position = cave.IsSolid(pos) ? at : pos;
            _world.AddChild(e);
            if (_autotest) _entrants.Add((e, e.Position.DistanceTo(p.GlobalPosition), _runTime));
        }
    }

    /// <summary>A newcomer suited to the spot and the biome: swimmers in water, fliers where there's no floor.</summary>
    private static Enemy EntrantFor(Vector2 at, out Vector2 pos)
    {
        var cave = G.Cave;
        var b = G.Biome;
        pos = at;
        if (cave.IsLava(at)) return null;
        if (cave.IsWater(at)) return Biomes.Make(b.WaterEntrants);
        if (!cave.FindFloor(at, 140, out var floor) || (b.AirEntrants.Count > 0 && G.Chance(Tune.Spawning.EntranceBatChance)) || b.GroundEntrants.Count == 0)
            return Biomes.Make(b.AirEntrants);
        if (cave.IsLava(floor + new Vector2(0, -12))) return Biomes.Make(b.AirEntrants);
        pos = floor + new Vector2(0, -12);
        return Biomes.Make(b.GroundEntrants);
    }

    /// <summary>Fills a resident spawn point with a group from the biome's table for that kind of spot.</summary>
    private int SpawnGroup(SpawnPoint sp)
    {
        var cave = G.Cave;
        var b = G.Biome;
        if (!b.Residents.TryGetValue(sp.Kind, out var table) || table.Count == 0) return 0;
        var entry = Biomes.Pick(table);
        int count = G.RangeI(entry.Min, entry.Max);
        if (count > 1) count += Math.Min((int)(G.Pace * 1.2f), 2);
        bool elite = G.Chance(b.EliteChance);
        if (elite) count = 1;
        int n = 0;
        for (int k = 0; k < count; k++)
        {
            var e = entry.Make();
            if (elite) e.MakeElite();
            Vector2 pos = sp.Kind switch
            {
                SpawnKind.Ground => sp.Pos + new Vector2(G.Range(-30, 30), -4 - e.BodyRadius * e.Size * 0.5f),
                SpawnKind.Shore => sp.Pos + new Vector2(G.Range(-6, 6), -2 - e.BodyRadius * e.Size * 0.5f),
                SpawnKind.Ceiling => sp.Pos + new Vector2(G.Range(-40, 40), 0),
                SpawnKind.Water => sp.Pos + G.RandDir() * G.Range(0, 30),
                _ => sp.Pos,
            };
            if (e is Spider { Grounded: false } && cave.FindCeiling(pos + new Vector2(0, 20), 60, out var ce)) pos = ce + new Vector2(0, 8);
            if (e is Eel eel) eel.WallNormal = sp.Normal;
            if (cave.IsSolid(pos)) pos = sp.Pos;
            e.Position = pos;
            _world.AddChild(e);
            n++;
        }
        return n;
    }

    private void RunRooms()
    {
        var cave = G.Cave;
        foreach (var room in cave.Rooms)
        {
            if (room.Triggered || room.Kind == RoomKind.Start) continue;
            // whoever walks in first (online, any of the heroes)
            Player p = null;
            foreach (var h in G.Players)
            {
                if (h.Dead) continue;
                // the guardian wakes once you're properly inside its chamber: on one of the chamber's
                // own open cells (flooded out from its floor, so never through a wall) and a few cells
                // in from the doorway. (A line of sight to the chamber's centre isn't needed: slabs
                // and ledges inside it used to block that, and the guardian never came.)
                bool inside = room.Kind == RoomKind.Boss ? InRoom(cave, room, h.GlobalPosition, 0.9f)
                    : room.Center.DistanceTo(h.GlobalPosition) <= Math.Max(room.RxPx, room.RyPx) + 60;
                if (inside) { p = h; break; }
            }
            if (p == null) continue;
            room.Triggered = true;
            switch (room.Kind)
            {
                case RoomKind.Boss:
                {
                    var b = G.Biome;
                    if (b.NoGuardian) break;
                    var boss = b.Guardian(room);
                    float side = Math.Sign(room.Center.X - p.GlobalPosition.X);
                    if (boss is Dragon) boss.Position = new Vector2(room.Center.X, room.Center.Y - room.RyPx * 0.5f);
                    else
                    {
                        // on floor you can reach (never a ledge or slab you can't get up to), a little
                        // past the middle of the chamber from where you came in, dropping in from above
                        var floor = GuardianFloor(cave, room, room.Floor.X + room.RxPx * 0.25f * side, boss);
                        float half = boss.BodyRadius * boss.Size;
                        float drop = cave.IsSolid(floor + new Vector2(0, -half * 2 - 48)) ? 0 : 40;
                        boss.Position = floor + new Vector2(0, -half - 4 - drop);
                    }
                    if (cave.IsSolid(boss.Position)) boss.Position = room.Center;
                    boss.OnDeath = e => OnGuardianKilled(room, e);
                    boss.Wake();
                    _world.AddChild(boss);
                    ActiveBoss = boss;
                    _hud.ShowBanner(boss.Title != "" ? boss.Title : boss.DisplayName.ToUpperInvariant(), 3f);
                    // (online, a friend may have woken it far from here)
                    if (p != G.Player) GuardianFarNotice(room.Center, p.NetName);
                    _sfx.SetMusic("boss");
                    if (boss is Dragon) { G.Fx.ScreenFlash(new Color(1f, 0.4f, 0.1f), 0.5f); G.Fx.AddShake(10); }
                    break;
                }
                case RoomKind.MiniBoss:
                {
                    var b = G.Biome;
                    var slime = Biomes.Get(BiomeId.Slime);
                    var pool = room.Underwater ? (b.WaterMiniBosses.Count > 0 ? b.WaterMiniBosses : slime.WaterMiniBosses) : (b.MiniBosses.Count > 0 ? b.MiniBosses : slime.MiniBosses);
                    Enemy elite = G.Pick(pool)();
                    elite.MakeElite();
                    var pos = room.Center;
                    if (elite is Eel eel)
                    {
                        // put the eel's burrow in the room wall
                        var dir = new Vector2(Math.Sign(room.Center.X - p.GlobalPosition.X), 0);
                        if (dir.X == 0) dir.X = 1;
                        if (cave.Raycast(room.Center, dir, room.RxPx * 2, out var wall)) { pos = wall - dir * 8; eel.WallNormal = -dir; }
                    }
                    else if (!room.Underwater) pos = room.Floor + new Vector2(0, -elite.BodyRadius * elite.Size - 4);
                    elite.Position = pos;
                    elite.OnDeath = e =>
                    {
                        var floor = e.GlobalPosition;
                        if (cave.FindFloor(e.GlobalPosition, 800, out var f)) floor = f;
                        CallDeferred(MethodName.SpawnChest, floor);
                        // the first mini-boss down on a level with a vault drops its key too
                        if (!_keyDropped && cave.Vault != null)
                        {
                            _keyDropped = true;
                            CallDeferred(MethodName.SpawnKey, floor + new Vector2(22, 0));
                        }
                    };
                    _world.AddChild(elite);
                    _hud.ShowBanner(elite.DisplayName.ToUpperInvariant(), 2f);
                    NetSync.SendBanner(elite.DisplayName.ToUpperInvariant(), 2f);
                    G.Sfx.Play("roar", elite.GlobalPosition, -6, 0, 1.6f);
                    break;
                }
                case RoomKind.Ambush:
                {
                    if (!Tune.Spawning.AmbushRooms) break;
                    int n = 3 + G.Depth;
                    for (int k = 0; k < n; k++)
                    {
                        var pos = room.Floor + new Vector2(G.Range(-room.RxPx * 0.7f, room.RxPx * 0.7f), -14);
                        Enemy e = G.Chance(0.5f) ? new Goblin { Slinger = G.Chance(0.3f) } : new Frog();
                        e.Position = pos;
                        _world.AddChild(e);
                    }
                    for (int k = 0; k < 2; k++) _world.AddChild(new Bat { Position = room.Center + new Vector2(G.Range(-40, 40), -room.RyPx * 0.4f) });
                    _hud.ShowBanner("AMBUSH!", 1.5f);
                    NetSync.SendBanner("AMBUSH!", 1.5f);
                    break;
                }
                case RoomKind.Treasure:
                {
                    // a couple of guards
                    var b = G.Biome;
                    var table = room.Underwater ? b.WaterEntrants : b.GroundEntrants.Count > 0 ? b.GroundEntrants : b.Residents.GetValueOrDefault(SpawnKind.Ground);
                    int n = G.Chance(b.Density) ? G.RangeI(1, 2) : 0;
                    for (int k = 0; k < n; k++)
                    {
                        var e = Biomes.Make(table);
                        if (e == null) break;
                        e.Position = room.Underwater ? room.Center + G.RandDir() * 30 : room.Floor + new Vector2(G.Range(-40, 40), -14);
                        _world.AddChild(e);
                    }
                    break;
                }
            }
        }
    }

    private void SpawnChest(Vector2 at)
    {
        // (online, a chest made mid-level goes to every game)
        NetSync.Scope++;
        try { G.Spawn(new Chest { Position = at, Tier = G.Chance(Tune.Relics.RelicChestChance) ? ChestTier.Relic : ChestTier.Wood }); }
        finally { NetSync.Scope--; }
    }

    /// <summary>
    /// A guardian fell: a gold chest for each player, side by side, each theirs to look in first
    /// (their class's upgrade and two more). A Hunter's Map or a Prodigy's Brand gives up its bearer's.
    /// </summary>
    private void SpawnBossChests(Vector2 at)
    {
        var owners = new List<int>();
        bool Wants(int id, Player p) => !RunRelics.Has(id, "relic_treasure") && !RunRelics.Has(id, "relic_prodigy") && p?.Stats.NoChests != true;
        if (Net.Online) { foreach (var id in Net.Peers.Keys) if (Wants(id, id == Net.Me ? G.Player : null)) owners.Add(id); }
        else if (Wants(Net.Me, G.Player)) owners.Add(0);
        NetSync.Scope++;
        try
        {
            for (int k = 0; k < owners.Count; k++)
            {
                float x = at.X + (k - (owners.Count - 1) * 0.5f) * 36f;
                var spot = G.Cave.FindFloor(new Vector2(x, at.Y - 30), 240, out var f) ? f : at;
                G.Spawn(new Chest { Position = spot, Tier = ChestTier.Boss, Owner = owners[k] });
            }
            // and one chest of relics for the whole party, to talk over or to scramble for
            var relicSpot = G.Cave.FindFloor(new Vector2(at.X + (owners.Count * 0.5f + 1.2f) * 36f, at.Y - 30), 240, out var rf) ? rf : at;
            G.Spawn(new Chest { Position = relicSpot, Tier = ChestTier.Relic, Owner = 0 });
        }
        finally { NetSync.Scope--; }
    }

    private bool _guardianDown;
    private float _victoryT = -1;
    /// <summary>Where the exits are (for the autopilot).</summary>
    public readonly List<Vector2> ExitSpots = new();

    // ---- guardians: where they stand, and never out of reach

    private readonly Dictionary<Room, HashSet<int>> _roomCells = new();
    private float _bossStrandedT;

    /// <summary>
    /// The open cells that belong to a room: flooded out from its floor through open space, kept
    /// within the room's own ellipse (a little enlarged), so a tunnel passing near it is never "in".
    /// </summary>
    private HashSet<int> RoomCells(CaveData cave, Room room)
    {
        if (_roomCells.TryGetValue(room, out var set)) return set;
        set = new HashSet<int>();
        float ci = room.Center.X / CaveData.Cell, cj = room.Center.Y / CaveData.Cell;
        float rx = room.RxPx / CaveData.Cell + 1.5f, ry = room.RyPx / CaveData.Cell + 2f;
        bool Inside(int i, int j) { float u = (i + 0.5f - ci) / rx, v = (j + 0.5f - cj) / ry; return u * u + v * v <= 1f; }
        int si = (int)(room.Floor.X / CaveData.Cell), sj = (int)(room.Floor.Y / CaveData.Cell) - 1;
        // start from an open cell at the floor (search up a little if the floor point sits in rock)
        for (int k = 0; k < 4 && !cave.CellOpen(si, sj); k++) sj--;
        if (!cave.CellOpen(si, sj)) { si = (int)ci; sj = (int)cj; }
        var q = new Queue<(int, int)>();
        if (cave.CellOpen(si, sj)) { set.Add(sj * cave.W + si); q.Enqueue((si, sj)); }
        while (q.Count > 0)
        {
            var (i, j) = q.Dequeue();
            foreach (var (di, dj) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                int a = i + di, c = j + dj;
                if (a < 0 || c < 0 || a >= cave.W || c >= cave.H || !cave.CellOpen(a, c) || !Inside(a, c)) continue;
                if (set.Add(c * cave.W + a)) q.Enqueue((a, c));
            }
        }
        _roomCells[room] = set;
        return set;
    }

    /// <summary>Is a point inside a room (one of its cells), at most <paramref name="depth"/> of the way out to its rim?</summary>
    private bool InRoom(CaveData cave, Room room, Vector2 at, float depth)
    {
        var cells = RoomCells(cave, room);
        int i = (int)(at.X / CaveData.Cell), j = (int)(at.Y / CaveData.Cell);
        if (cells.Count == 0)
            return room.Center.DistanceTo(at) < room.RxPx * 0.75f; // (no cells found: the old test)
        if (!cells.Contains(j * cave.W + i) && !cells.Contains((j - 1) * cave.W + i)) return false;
        float u = (at.X - room.Center.X) / (room.RxPx + 1.5f * CaveData.Cell), v = (at.Y - room.Center.Y) / (room.RyPx + 2f * CaveData.Cell);
        return u * u + v * v <= depth * depth;
    }

    private static bool Reach(CaveData cave, int i, int j)
        => cave.ReachMask != null && i >= 0 && j >= 0 && i < cave.W && j < cave.H && cave.ReachMask[j * cave.W + i];

    /// <summary>
    /// A floor spot in the room the hero can reach (the traversal check's map), with headroom for
    /// the guardian, as close as possible to <paramref name="wantX"/> on the room's lowest floor.
    /// </summary>
    private Vector2 GuardianFloor(CaveData cave, Room room, float wantX, Enemy boss)
    {
        int head = Math.Max(2, (int)MathF.Ceiling(boss.BodyRadius * boss.Size * 2f / CaveData.Cell));
        float floorRow = room.Floor.Y / CaveData.Cell;
        float best = float.MaxValue;
        Vector2 spot = room.Floor;
        foreach (int k in RoomCells(cave, room))
        {
            int i = k % cave.W, j = k / cave.W;
            if (cave.CellOpen(i, j + 1)) continue; // must stand on rock
            bool reachable = Reach(cave, i, j) || Reach(cave, i, j - 1);
            if (!reachable) continue;
            bool room2 = true;
            for (int h = 1; h <= head && room2; h++) room2 = cave.CellOpen(i, j - h);
            if (!room2) continue;
            float x = (i + 0.5f) * CaveData.Cell;
            float score = Math.Abs(x - wantX) + Math.Abs(j + 1 - floorRow) * CaveData.Cell * 3f;
            if (score < best) { best = score; spot = new Vector2(x, (j + 1) * CaveData.Cell); }
        }
        return spot;
    }

    /// <summary>
    /// A guardian that walks, stranded where the hero can't get at it (up on a ledge it was
    /// knocked or charged onto) for a few seconds, leaps back down to reachable floor near the hero.
    /// </summary>
    private void WatchGuardian(float dt)
    {
        var boss = ActiveBoss;
        var cave = G.Cave;
        if (boss == null || !IsInstanceValid(boss) || boss.Dead || boss is Dragon || !boss.Walks || cave?.ReachMask == null) { _bossStrandedT = 0; return; }
        if (!boss.IsOnFloor()) return;
        var feet = boss.GlobalPosition + new Vector2(0, boss.BodyRadius * boss.Size);
        int i = (int)(feet.X / CaveData.Cell), j = (int)(feet.Y / CaveData.Cell) - 1;
        bool ok = false;
        for (int dj = -2; dj <= 0 && !ok; dj++) for (int di = -1; di <= 1 && !ok; di++) ok = Reach(cave, i + di, j + dj);
        _bossStrandedT = ok ? 0 : _bossStrandedT + dt;
        if (_bossStrandedT < 3f) return;
        _bossStrandedT = 0;
        var room = cave.Rooms.FirstOrDefault(r => r.Kind == RoomKind.Boss) ?? cave.Boss;
        if (room == null) return;
        var near = boss.Target ?? G.Player;
        var floor = GuardianFloor(cave, room, near.GlobalPosition.X + Math.Sign(boss.GlobalPosition.X - near.GlobalPosition.X) * 80, boss);
        G.Fx.Dust(boss.GlobalPosition, 10, 2f);
        boss.GlobalPosition = floor + new Vector2(0, -boss.BodyRadius * boss.Size - 4);
        boss.Velocity = Vector2.Zero;
        G.Fx.Dust(boss.GlobalPosition + new Vector2(0, boss.BodyRadius * boss.Size), 14, 2.5f);
        G.Fx.AddShake(5);
        G.Sfx.Play("slam", boss.GlobalPosition, -2);
        GD.Print($"[guardian] stranded out of reach: brought back down to {floor}");
    }

    /// <summary>
    /// The level's guardian is dead: pay out its embers, roll a resource, drop a chest and open
    /// the exits. The dragon ends the run.
    /// </summary>
    private void OnGuardianKilled(Room room, Enemy boss)
    {
        bool dragon = boss is Dragon;
        int embers = dragon ? Tune.Drops.DragonEmbers : G.Depth >= 5 ? Tune.Drops.DeepGuardianEmbers : Tune.Drops.GuardianEmbers;
        string name = boss.Title != "" ? boss.Title : boss.DisplayName.ToUpperInvariant();
        if (_autotest) GD.Print($"[autotest] guardian {boss.DisplayName} killed at depth {G.Depth} ({G.Biome.Name}) after {_runTime:0}s, level {G.Player.Level}");
        // (online, each player's game pays its own player: the others hear of it by message)
        if (Net.IsHost) NetSync.SendGuardianDown(embers, dragon, name);
        NetSync.Local(() => GuardianRewards(embers, dragon, name, boss.GlobalPosition));
        if (dragon)
        {
            _victoryT = 5f;
            return;
        }
        CallDeferred(MethodName.SpawnBossChests, room.Floor);
        // the way on: one or two tunnels into what lies below
        var exits = Biomes.ChooseExits(G.Depth, _rng);
        ExitSpots.Clear();
        for (int k = 0; k < exits.Count; k++)
        {
            float off = exits.Count == 1 ? 0 : (k == 0 ? -1 : 1) * room.RxPx * 0.55f;
            var probe = new Vector2(room.Floor.X + off, room.Floor.Y - 40);
            if (G.Cave.IsSolid(probe)) probe = room.Center;
            var floor = G.Cave.FindFloor(probe, 400, out var f) ? f : room.Floor;
            var (bd, depth) = exits[k];
            string label = exits.Count == 1 ? $"depth {depth}" : depth - G.Depth == 1 ? $"depth {depth}  ·  the gentle way" : $"depth {depth}  ·  the steep way";
            ExitSpots.Add(floor + new Vector2(0, -16));
            CallDeferred(MethodName.SpawnPortal, floor + new Vector2(0, -30), (int)bd.Id, depth, label);
        }
    }

    /// <summary>
    /// A guardian fell: this player's embers and a resource roll, with the fanfare. The dragon
    /// wins the run.
    /// </summary>
    private void GuardianRewards(int embers, bool dragon, string name, Vector2 at)
    {
        _guardianDown = true;
        _sfx.SetMusic("ambient");
        G.Fx.AddShake(14);
        G.Fx.ScreenFlash(new Color(1f, 0.9f, 0.6f), 0.3f);
        Meta.AddEmbers(embers);
        _runEmbers += embers;
        var found = Meta.RollResource(_rng);
        _hud.ShowBanner($"{name} SLAIN  ·  +{embers} EMBER{(embers > 1 ? "S" : "")}", 3.5f);
        G.Fx.Text(at + new Vector2(0, -40), $"+{embers} ember{(embers > 1 ? "s" : "")}", new Color(1f, 0.7f, 0.35f), 13, 2.5f);
        if (found != null)
        {
            _runFinds += $", 1 {found.Resource}";
            G.Fx.Text(at + new Vector2(0, -58), $"FOUND: {found.Resource.ToUpperInvariant()}", found.Color, 15, 3f);
            for (int k = 0; k < 12; k++) G.Fx.Glint(at + G.RandDir() * G.Range(10, 40), found.Color, 9);
            _sfx.Play("levelup", at, 0, 0, 1.3f);
        }
        if (dragon)
        {
            _victory = true;
            Meta.Victories++;
            Meta.Save();
            _hud.ShowBanner("THE ELDER DRAGON IS SLAIN", 5f);
            // (a run that found no hero in the caves: the dragon's slayers bring one home, one the party played that you hadn't got)
            var party = Net.Online ? Net.Peers.Values.Select(pp => pp.Hero).ToList() : new List<HeroKind> { G.Hero };
            if (Meta.AwardAfterDragon(party, _rng) is HeroKind won)
            {
                _runFinds += $", the {won} joins your camp";
                _hud.ShowBanner($"THE ELDER DRAGON IS SLAIN  ·  THE {won.ToString().ToUpperInvariant()} JOINS YOUR CAMP", 6f);
            }
        }
    }

    private void SpawnPortal(Vector2 at, int biome, int depth, string label)
    {
        NetSync.Scope++;
        try { G.Spawn(new Portal { Position = at, To = Biomes.Get((BiomeId)biome), Depth = depth, Label = label }); }
        finally { NetSync.Scope--; }
    }

    private void FinishFullRun(bool ok)
    {
        GD.Print($"[fullrun] {(ok ? "VICTORY" : "FAILED")}: depth {G.Depth}, level {G.Player?.Level}, time {_runTime:0}s, deaths {_deaths}");
        SafeQuit.Request(this, ok ? 0 : 1);
    }
    private int _deaths;

    // ------------------------------------------------------------------ testing

    private void AutotestTick(float dt)
    {
        _bot?.Tick(dt);
        _shotT -= dt;
        if (_shotDir != "" && _shotT <= 0)
        {
            _shotT = 3f;
            var img = GetViewport().GetTexture().GetImage();
            img.SavePng($"{_shotDir}/shot_{_shotN++:000}.png");
        }
        _duration -= dt;
        if (_duration <= 0 && _fullRun) { FinishFullRun(false); return; }
        if (_duration <= 0)
        {
            var p = G.Player;
            foreach (var (e, d0, t0) in _entrants)
                GD.Print($"[autotest] entrance {e.GetType().Name,-12} at t={t0:0.0}s spawned {d0:0}px away (off-screen) -> " +
                         (IsInstanceValid(e) && !e.Dead ? $"now {e.GlobalPosition.DistanceTo(p.GlobalPosition):0}px" : "killed/gone"));
            GD.Print($"[autotest] threat x{G.Threat:0.00} tempo x{G.Tempo:0.00} pace {G.Pace:0.00}");
            foreach (var e in G.Enemies)
                if (e.GlobalPosition.DistanceTo(p.GlobalPosition) < 700)
                    GD.Print($"[autotest] near: {e.GetType().Name} at {e.GlobalPosition - p.GlobalPosition} water {G.Cave.IsWater(e.GlobalPosition)} solid {G.Cave.IsSolid(e.GlobalPosition)}");
            GD.Print($"[autotest] done: depth {G.Depth} level {p.Level} hp {p.Hp:0}/{p.Stats.MaxHp} kills {p.Kills} enemies {G.Enemies.Count} upgrades [{string.Join(",", p.Stats.Stacks.Keys)}] pos {p.GlobalPosition} runtime {_runTime:0.0}");
            SafeQuit.Request(this);
        }
    }

    private static bool DebugStampColors = false;

    private static void SaveCaveImage(CaveData c, string path)
    {
        const int sc = 3;
        var reachMask = c.ReachMask;
        var img = Image.CreateEmpty(c.W * sc, c.H * sc, false, Image.Format.Rgb8);
        for (int j = 0; j < c.H * sc; j++)
            for (int i = 0; i < c.W * sc; i++)
            {
                float x = i / (float)sc, y = j / (float)sc;
                bool open = c.SampleCells(x, y) >= 0.5f;
                int ci = i / sc, cj = j / sc;
                Color col = !open ? new Color(0.1f, 0.08f, 0.08f) : (y * CaveData.Cell > c.WaterY ? new Color(0.2f, 0.4f, 0.8f) : new Color(0.7f, 0.65f, 0.6f));
                if (open && c.TrapMask != null && c.TrapMask[cj * c.W + ci]) col = new Color(1f, 0.1f, 0.1f);
                else if (open && reachMask != null && !reachMask[cj * c.W + ci]) col = col.Darkened(0.5f);
                img.SetPixel(i, j, col);
            }
        void Dot(Vector2 p, Color col, int r)
        {
            int x0 = (int)(p.X / CaveData.Cell * sc), y0 = (int)(p.Y / CaveData.Cell * sc);
            for (int dy = -r; dy <= r; dy++) for (int dx = -r; dx <= r; dx++)
                if (x0 + dx >= 0 && y0 + dy >= 0 && x0 + dx < c.W * sc && y0 + dy < c.H * sc) img.SetPixel(x0 + dx, y0 + dy, col);
        }
        var kc = new[] { new Color(0.9f, 0.9f, 0.9f), new Color(0.2f, 0.9f, 0.2f), new Color(0.1f, 0.3f, 1f), new Color(1f, 0.3f, 0.3f), new Color(0f, 1f, 1f), new Color(1f, 0.5f, 0f) };
        if (DebugStampColors) foreach (var (pos, kind) in c.DebugStamps) Dot(pos * CaveData.Cell, kind < kc.Length ? kc[kind] : new Color(1f, 1f, 0f), 0);
        foreach (var sp in c.Spawns) Dot(sp.Pos, new Color(1f, 0.8f, 0.2f), 1);
        foreach (var r in c.Rooms) Dot(r.Center, r.Kind == RoomKind.Boss ? new Color(1f, 0f, 1f) : r.Kind == RoomKind.Treasure ? new Color(1f, 1f, 0f) : new Color(1f, 0.5f, 0f), 4);
        Dot(c.StartPos, new Color(0f, 1f, 0f), 5);
        img.SavePng(path);
    }

    /// <summary>Test aid: lines up one of every creature near the start and in the water, then screenshots them.</summary>
    private void SpawnBestiary()
    {
        var p = G.Player.GlobalPosition;
        _world.AddChild(new Chest { Position = p + new Vector2(-60, 14) });
        Enemy[] land = { new Bat(), new Frog(), new Goblin(), new Goblin { Slinger = true }, new Spider(), new LavaMonster(), new Golem(),
            new Rat(), new Bear(), new Scorpion(), new Crab(), new Hornet(), new Skeleton(), new Sporeling(), new FrostWraith(), new Shardling(),
            new EarthElemental(), new FrostElemental(), new NatureElemental(), new FireElemental() };
        for (int k = 0; k < land.Length; k++)
        {
            var at = p + new Vector2(-190 + (k % 8) * 54, k < 8 ? -30 : -80);
            if (land[k] is Bat or Spider && G.Cave.FindCeiling(at, 200, out var ce)) at = ce + new Vector2(0, 10);
            if (G.Cave.IsSolid(at)) at = p + new Vector2(0, -40);
            land[k].Position = at;
            _world.AddChild(land[k]);
        }
        _bestiaryT = 0;
    }

    private void BestiaryTick(float dt)
    {
        if (_bestiaryT < 0) return;
        _bestiaryT += dt;
        if (_bestiaryT > 1.2f && _bestiaryT - dt <= 1.2f)
        {
            GetViewport().GetTexture().GetImage().SavePng($"{_shotDir}/bestiary_land.png");
            // move to the water
            var sp = G.Cave.Spawns.FirstOrDefault(s => s.Kind == SpawnKind.Water);
            var w = sp?.Pos ?? G.Cave.StartPos;
            G.Player.GlobalPosition = w;
            _cam.GlobalPosition = w;
            Enemy[] water = { new Fish(), new Fish(), new Urchin(), new Eel() };
            for (int k = 0; k < water.Length; k++) { water[k].Position = w + new Vector2(-90 + k * 60, 40); _world.AddChild(water[k]); }
            var boss = new CavernColossus { Position = G.Cave.Boss.Floor + new Vector2(0, -60) };
            boss.Init(G.Cave.Boss);
            _world.AddChild(boss);
        }
        if (_bestiaryT > 2.6f && _bestiaryT - dt <= 2.6f)
        {
            GetViewport().GetTexture().GetImage().SavePng($"{_shotDir}/bestiary_water.png");
            G.Player.GlobalPosition = G.Cave.Boss.Floor + new Vector2(-120, -20);
            _cam.GlobalPosition = G.Player.GlobalPosition;
        }
        if (_bestiaryT > 4.2f && _bestiaryT - dt <= 4.2f)
        {
            GetViewport().GetTexture().GetImage().SavePng($"{_shotDir}/bestiary_boss.png");
            foreach (var e in G.Enemies.ToArray()) e.QueueFree();
            var d = new Dragon { Position = G.Cave.Boss.Floor + new Vector2(60, -80) };
            d.Init(G.Cave.Boss);
            _world.AddChild(d);
        }
        if (_bestiaryT > 6.4f)
        {
            GetViewport().GetTexture().GetImage().SavePng($"{_shotDir}/bestiary_dragon.png");
            SafeQuit.Request(this);
        }
    }

    /// <summary>
    /// A directed demo for recording: the autopilot fights a spread of land creatures, then
    /// dives into the water, then takes on the boss. Pair with Godot's --write-movie.
    /// </summary>
    private void ShowcaseTick(float dt)
    {
        if (_state != State.Playing) return;
        _showT += dt;
        var p = G.Player;
        var cave = G.Cave;
        void Teleport(Vector2 at) { p.GlobalPosition = at; p.Velocity = Vector2.Zero; _cam.GlobalPosition = at; }
        void Put(Enemy e, Vector2 at) { e.Position = at; _world.AddChild(e); }
        int stage = _showT < 11 ? 0 : _showT < 21 ? 1 : 2;
        if (stage == _showStage) return;
        _showStage = stage;
        foreach (var e in G.Enemies.ToArray()) if (!e.IsBoss) e.QueueFree();
        switch (stage)
        {
            case 0:
                // a seasoned rogue: combo + finisher + knockback + pogo, and enough health to survive the bot
                foreach (var id in new[] { "combo", "combo3", "knock", "pogo", "charge2", "atkspd" }) Upgrades.Apply(Upgrades.Get(id), p.Stats, p);
                p.Stats.MaxHp = 600; p.Hp = 600;
                var s0 = p.GlobalPosition;
                Put(new Goblin(), s0 + new Vector2(90, -10));
                Put(new Frog(), s0 + new Vector2(-80, -6));
                Put(new Goblin { Slinger = true }, s0 + new Vector2(150, -10));
                Put(new Bat(), s0 + new Vector2(40, -60));
                Put(new Bat(), s0 + new Vector2(-40, -70));
                Put(new Golem(), s0 + new Vector2(-150, -14));
                p.AddXp(p.XpToNext - 1);
                break;
            case 1:
                var sp = cave.Spawns.FirstOrDefault(x => x.Kind == SpawnKind.Water && x.Pos.Y > cave.WaterY + 60);
                var w = sp?.Pos ?? cave.StartPos;
                Teleport(w);
                for (int k = 0; k < 4; k++) Put(new Fish(), w + new Vector2(60 + k * 20, G.Range(-30, 30)));
                Put(new Urchin(), w + new Vector2(-70, 30));
                break;
            default:
                Teleport(cave.Boss.Center + new Vector2(-cave.Boss.RxPx * 0.5f, 0));
                break;
        }
    }

    private bool _padOk = true;
    private void PadCheck(string what, bool ok)
    {
        GD.Print($"[padtest] {(ok ? "ok  " : "FAIL")} {what}");
        _padOk &= ok;
    }

    private int _padMark;
    private Chest _padChest;
    private string _padCards = "";

    /// <summary>Feeds synthetic joypad events through Godot's input pipeline and checks the game reacts.</summary>
    private void PadTestTick(float dt)
    {
        _padT += dt;
        void Btn(JoyButton b, bool down) => Input.ParseInputEvent(new InputEventJoypadButton { ButtonIndex = b, Pressed = down, Device = 0 });
        void Axis(JoyAxis a, float v) => Input.ParseInputEvent(new InputEventJoypadMotion { Axis = a, AxisValue = v, Device = 0 });
        var steps = new (float at, Action act, string label)[]
        {
            (0.5f, () => { Btn(JoyButton.A, true); }, "A on the main menu"),
            (0.55f, () => { Btn(JoyButton.A, false); }, ""),
            (0.8f, () => PadCheck($"A on Single player goes to the hero choice at the fire (choice {_heroChoice.Visible}, using pad {UsingPad}, mouse {Input.MouseMode})", _heroChoice.Visible && UsingPad), ""),
            (1.0f, () => Btn(JoyButton.A, true), "A at the fire"),
            (1.05f, () => Btn(JoyButton.A, false), ""),
            (1.4f, () => { PadCheck($"A on the hero moves on to the loadout and perks (stage {_heroChoice.Stage})", _heroChoice.Stage == HeroChoice.StageKind.Prep); Btn(JoyButton.A, true); }, "A on the loadout stage"),
            (1.45f, () => Btn(JoyButton.A, false), ""),
            (1.8f, () => { PadCheck($"A on Next moves on to the difficulty (stage {_heroChoice.Stage})", _heroChoice.Stage == HeroChoice.StageKind.Difficulty); Btn(JoyButton.A, true); }, "A on the difficulty stage"),
            (1.85f, () => Btn(JoyButton.A, false), ""),
            (3.4f, () => PadCheck($"A on the last stage sets off into the cave (state {_state})", _state == State.Playing), ""),
            (3.0f, () => Axis(JoyAxis.LeftX, 1f), "stick right"),
            (3.6f, () => { PadCheck($"the stick runs (vx {G.Player.Velocity.X:0})", G.Player.Velocity.X > 100); Axis(JoyAxis.LeftX, 0f); }, ""),
            (3.8f, () => Btn(JoyButton.X, true), "X swing"),
            (3.85f, () => { Btn(JoyButton.X, false); PadCheck($"X swings (anim {G.Player.Anim.Current})", G.Player.Anim.Current.StartsWith("slash")); }, ""),
            (4.3f, () => Btn(JoyButton.RightShoulder, true), "RB ability"),
            (4.35f, () => { Btn(JoyButton.RightShoulder, false); PadCheck($"RB charges the blade ({G.Player.Charged}, cooldown {G.Player.ChargeCooldownFrac:0.00})", G.Player.Charged == 1); }, ""),
            (4.6f, () => Btn(JoyButton.B, true), "B dodge"),
            (4.65f, () => { Btn(JoyButton.B, false); PadCheck($"B dodges ({G.Player.IsDodging})", G.Player.IsDodging); }, ""),
            (5.0f, () => Axis(JoyAxis.TriggerRight, 1f), "RT second ability"),
            (5.05f, () => { Axis(JoyAxis.TriggerRight, 0f); PadCheck($"RT heaves (heaving {G.Player.Heaving}, anim {G.Player.Anim.Current})", G.Player.Heaving); }, ""),
            (5.4f, () => { G.Player.PendingMilestones = 1; }, "milestone"),
            (6.0f, () => { PadCheck($"a milestone offers a pick ({_state})", _state == State.Choosing); Btn(JoyButton.DpadRight, true); }, "dpad right"),
            (6.05f, () => Btn(JoyButton.DpadRight, false), ""),
            (6.2f, () => Btn(JoyButton.A, true), "A pick"),
            (6.25f, () => Btn(JoyButton.A, false), ""),
            (6.4f, () => PadCheck($"A takes it (state {_state}, upgrades [{string.Join(",", G.Player.Stats.Stacks.Keys)}])", _state == State.Playing && G.Player.Stats.Stacks.Count > 0), ""),
            (6.6f, () => Btn(JoyButton.Start, true), "start pause"),
            (6.65f, () => Btn(JoyButton.Start, false), ""),
            (6.8f, () => PadCheck($"START pauses with the menu up ({_state}, menu {_pauseMenu.Visible})", _state == State.Paused && _pauseMenu.Visible), ""),
            // down to Settings, A opens it, B backs out to the pause menu, START resumes
            (6.9f, () => Btn(JoyButton.DpadDown, true), "menu down"),
            (6.95f, () => Btn(JoyButton.DpadDown, false), ""),
            (7.05f, () => PadCheck($"the d-pad moves down the menu (focus: {(GetViewport().GuiGetFocusOwner() as Button)?.Text})", (GetViewport().GuiGetFocusOwner() as Button)?.Text == "Settings"), ""),
            (7.1f, () => Btn(JoyButton.A, true), "A settings"),
            (7.15f, () => Btn(JoyButton.A, false), ""),
            (7.3f, () => PadCheck($"A opens the settings ({_settingsMenu.Visible})", _settingsMenu.Visible), ""),
            (7.4f, () => Btn(JoyButton.RightShoulder, true), "RB tab"),
            (7.45f, () => Btn(JoyButton.RightShoulder, false), ""),
            (7.5f, () => Btn(JoyButton.B, true), "B back"),
            (7.55f, () => Btn(JoyButton.B, false), ""),
            (7.7f, () => PadCheck($"B backs out to the pause menu (settings {_settingsMenu.Visible}, pause menu {_pauseMenu.Visible})", !_settingsMenu.Visible && _pauseMenu.Visible), ""),
            (7.8f, () => Btn(JoyButton.Start, true), ""),
            (7.85f, () => Btn(JoyButton.Start, false), ""),
            (8.0f, () => PadCheck($"START resumes ({_state}, menu {_pauseMenu.Visible})", _state == State.Playing && !_pauseMenu.Visible), ""),
            // the right stick swings where it's pushed; LB is the dodge button too
            (8.1f, () => { _padMark = G.Player.AttacksStarted; Axis(JoyAxis.RightX, 1f); }, "right stick"),
            (8.35f, () => { PadCheck($"the right stick swings ({G.Player.AttacksStarted - _padMark} swings)", G.Player.AttacksStarted > _padMark); Axis(JoyAxis.RightX, 0f); }, ""),
            (8.9f, () => Btn(JoyButton.LeftShoulder, true), "LB dodge"),
            (8.95f, () => { Btn(JoyButton.LeftShoulder, false); PadCheck($"LB dodges too ({G.Player.IsDodging})", G.Player.IsDodging); }, ""),
            // LT opens a chest; leaving it keeps its cards for later
            (9.3f, () => { _padChest = new Chest { Position = G.Player.GlobalPosition + new Vector2(0, 13) }; _world.AddChild(_padChest); }, "chest"),
            (9.4f, () => Axis(JoyAxis.TriggerLeft, 1f), "LT interact"),
            (9.45f, () => Axis(JoyAxis.TriggerLeft, 0f), ""),
            (9.8f, () =>
            {
                PadCheck($"LT looks in the chest ({_state}, cards {string.Join(",", _padChest.Cards ?? Array.Empty<string>())})", _state == State.Choosing && _padChest.Cards?.Length > 0);
                _padCards = _padChest.Cards != null ? string.Join(",", _padChest.Cards) : "";
                Btn(JoyButton.DpadLeft, true);
            }, "to leave it"),
            (9.85f, () => Btn(JoyButton.DpadLeft, false), ""),
            (9.95f, () => Btn(JoyButton.A, true), "A leave"),
            (10.0f, () => Btn(JoyButton.A, false), ""),
            (10.15f, () => PadCheck($"leaving it closes it again, cards and all (state {_state}, spent {_padChest.Open})", _state == State.Playing && !_padChest.Open && _padChest.Cards != null && string.Join(",", _padChest.Cards) == _padCards), ""),
            (10.3f, () => Axis(JoyAxis.TriggerLeft, 1f), "LT again"),
            (10.35f, () => Axis(JoyAxis.TriggerLeft, 0f), ""),
            (10.7f, () => { PadCheck($"it offers the same cards again ({string.Join(",", _padChest.Cards ?? Array.Empty<string>())})", _state == State.Choosing && string.Join(",", _padChest.Cards ?? Array.Empty<string>()) == _padCards); Btn(JoyButton.A, true); }, "A take"),
            (10.75f, () => Btn(JoyButton.A, false), ""),
            (10.9f, () =>
            {
                PadCheck($"taking a card spends the chest (spent {_padChest.Open}, state {_state})", _padChest.Open && _state == State.Playing);
                GD.Print(_padOk ? "[padtest] PASS" : "[padtest] FAIL");
                SafeQuit.Request(this, _padOk ? 0 : 1);
            }, ""),
        };
        while (_padStep < steps.Length && _padT >= steps[_padStep].at) steps[_padStep++].act();
    }

    // ------------------------------------------------------------------ --hitstoptest
    // A golem dummy in front of the hero, swings on cue, and every frame around the impacts saved
    // (--shots=DIR/hs_NNN.png) with both freezes printed: for judging hit-stop and combos.
    private bool _hitStopTest;
    private int _hsFrame;
    private Enemy _hsDummy;
    private int[] _hsAttackFrames = { 40, 58 };

    private PlayerInput HitStopTestInput()
    {
        var i = new PlayerInput();
        foreach (int f in _hsAttackFrames) if (_hsFrame == f) { i.Attack = true; i.Aim = new Vector2(G.Player.Facing, 0); }
        return i;
    }

    private void HitStopTestTick()
    {
        _hsFrame++;
        var p = G.Player;
        if (_hsFrame == 20)
        {
            foreach (var e in G.Enemies.ToArray()) e.QueueFree();
            _hsDummy = new Golem { Position = p.GlobalPosition + new Vector2(p.Facing * 40, -6) };
            _hsDummy.SetMeta("test", true);
            _world.AddChild(_hsDummy);
            _hsDummy.Wake();
        }
        if (_hsFrame >= 38 && _hsFrame <= 96)
        {
            string e = IsInstanceValid(_hsDummy) ? $"golem freeze {_hsDummy.FreezeLeft:0.000} hp {_hsDummy.Hp:0}" : "golem gone";
            GD.Print($"[hitstop] frame {_hsFrame}: hero freeze {p.FreezeLeft:0.000} swinging {p.IsSwinging} anim {p.Anim.Sprite.Animation}:{p.Anim.Sprite.Frame} | {e}");
            if (_shotDir != "") GetViewport().GetTexture().GetImage().SavePng($"{_shotDir}/hs_{_hsFrame:000}.png");
        }
        if (_hsFrame > 96) SafeQuit.Request(this);
    }

    /// <summary>Scripted inputs that walk the player through every movement/attack transition.</summary>
    // ------------------------------------------------------------------ --herotest
    // Scripted checks of each hero's mechanics (run once per hero: --hero=warden, --hero=vitalist, or neither).
    private PlayerInput _heroInput;
    private float _heroT;
    private bool _heroOk = true;
    private float _hpMark, _shieldMark;
    private Vector2 _posMark;
    private EnemyProjectile _probe;

    private void Check(string what, bool ok)
    {
        GD.Print($"[herotest] {(ok ? "ok  " : "FAIL")} {what}");
        _heroOk &= ok;
    }

    private EnemyProjectile Shoot(Vector2 fromOffset)
    {
        var p = G.Player;
        var pr = new EnemyProjectile { Position = p.GlobalPosition + fromOffset, Vel = -fromOffset.Normalized() * 260, Grav = 0, Damage = 6, Kind = "rock", Radius = 4 };
        _world.AddChild(pr);
        return pr;
    }

    private void HeroTestTick(float dt)
    {
        var p = G.Player;
        _heroT += dt;
        if (Engine.TimeScale < 0.99) Check($"game clock untouched by hit-stops (time scale {Engine.TimeScale})", false);
        // keep enemies out of the way
        foreach (var e in G.Enemies.ToArray()) if (e.GlobalPosition.DistanceTo(p.GlobalPosition) < 600 && !e.IsBoss && e.GetMeta("test", false).AsBool() == false) e.QueueFree();
        bool warden = p.Stats.Hero == HeroKind.Warden;
        if (_dir == 0 && !p.IsOnFloor() && _heroT < 4f) { _heroT = 0; return; } // wait until landed
        if (_dir == 0)
        {
            // test toward whichever side has open floor (the start spot varies by cave)
            float Open(int side) { int n = 0; for (int k = 1; k <= 10; k++) if (!G.Cave.IsSolid(p.GlobalPosition + new Vector2(side * k * 18, -10)) && G.Cave.FindFloor(p.GlobalPosition + new Vector2(side * k * 18, -10), 40, out _)) n++; else break; return n; }
            _dir = Open(1) >= Open(-1) ? 1 : -1;
        }
        // steps are keyed by time (tenths of a second); each runs once
        int s = (int)(_heroT * 10);
        if (_lastHeroStep < s)
        {
            // one step per frame (so an input set by one step is seen before the next clears it);
            // a slow frame just delays the steps a little rather than skipping any
            int step = ++_lastHeroStep;
            if (OS.GetCmdlineUserArgs().Contains("--herodebug")) GD.Print($"[herodebug] step {step} t={_heroT:0.00} dir {_dir} swing {p.IsSwinging} shield {p.ShieldRaised} guardIn {_heroInput.GuardHeld} atk {_heroInput.Attack} abl {_heroInput.Ability} state {_state} paused {GetTree().Paused} pos {p.GlobalPosition} | {p.DebugState} | probe {(IsInstanceValid(_probe) ? $"{_probe.GlobalPosition} v {_probe.Vel}" : "none")}");
            if (_altTest) AltStep(step, p);
            else if (warden) WardenStep(step, p);
            else if (p.Stats.Hero == HeroKind.Vitalist) VitalistStep(step, p);
            else if (p.Stats.Hero == HeroKind.Elementalist) ElementalistStep(step, p);
            else if (p.Stats.Hero == HeroKind.Rogue) RogueStep(step, p);
            else SwordStep(step, p);
            if (_shotDir != "" && step % 20 == 0) GetViewport().GetTexture().GetImage().SavePng($"{_shotDir}/hero_{step:000}.png");
        }
    }
    private int _lastHeroStep = -1;

    private void WardenStep(int s, Player p)
    {
        switch (s)
        {
            case 5: // raise the shield to the right, fire from the right
                _heroInput = new PlayerInput { GuardHeld = true, GuardAim = new Vector2(_dir, 0) };
                _hpMark = p.Hp; _shieldMark = p.ShieldHp;
                break;
            case 10: _probe = Shoot(new Vector2(_dir * 120, -4)); break;
            case 18:
            {
                // all of it stopped (costing the shield half of that), nothing through
                float through = 6f * (1f - p.Stats.BlockShare) * (1f - p.Stats.DamageReduction) * p.Stats.DamageTakenMult;
                float cost = 6f * p.Stats.BlockShare * Tune.Warden.ShieldCost;
                Check($"the shield stops all of a shot from the front (hp {_hpMark:0.00} -> {p.Hp:0.00}, want -{through:0.00}; shield {_shieldMark:0.0} -> {p.ShieldHp:0.0}, want -{cost:0.0})",
                    Math.Abs(_hpMark - p.Hp - through) < 0.05f && Math.Abs(_shieldMark - p.ShieldHp - cost) < 0.3f);
                _hpMark = p.Hp;
                _probe = Shoot(new Vector2(_dir * -120, -4)); // from behind
                break;
            }
            case 26:
                Check($"a shot from behind gets through in full (hp {p.Hp:0.0} < {_hpMark:0.0} by {_hpMark - p.Hp:0.0})", _hpMark - p.Hp > 5f);
                p.Heal(100);
                break;
            case 40: // wait out the post-hit invulnerability, then break it (a sturdier Warden for this)
                p.Stats.MaxHp = 500; p.Hp = 500;
                for (int k = 0; k < 12; k++) { var pr = Shoot(new Vector2(_dir * (110 + k * 30), -4)); pr.Damage = 20; }
                break;
            case 70:
                Check($"shield breaks when drained (shield {p.ShieldHp:0.0}, broken {p.ShieldBroken})", p.ShieldBroken && p.ShieldHp == 0);
                Check("a broken shield can't be raised", !p.ShieldRaised);
                break;
            case 95:
                Check($"still broken a few seconds later, at zero ({p.ShieldHp:0.0}, {p.ShieldBrokenLeft:0.0}s left)", p.ShieldBroken && p.ShieldHp == 0);
                // healing mends a broken shield at once (half as much as it heals)
                _shieldMark = p.ShieldHp;
                p.Hp -= 30;
                p.Heal(10);
                break;
            case 96:
                Check($"healing mends a broken shield at once (broken {p.ShieldBroken}, shield {_shieldMark:0.0} -> {p.ShieldHp:0.0})", !p.ShieldBroken && Math.Abs(p.ShieldHp - _shieldMark - 10f * Tune.Warden.HealToShield) < 0.3f);
                _heroInput = default;
                p.RefillShield();
                break;
            case 97:
            {
                // a blow that breaks the shield leaves its striker stunned
                _heroInput = new PlayerInput { GuardHeld = true, GuardAim = new Vector2(_dir, 0) };
                var gob3 = new Goblin { Position = p.GlobalPosition + new Vector2(_dir * 20, -4) };
                gob3.SetMeta("test", true);
                _world.AddChild(gob3);
                _probeEnemy = gob3;
                break;
            }
            case 101:
            {
                // (held for a while now: an ordinary block, not a perfect one)
                var b = p.TryBlock(_probeEnemy.GlobalPosition, 400f, _probeEnemy, _probeEnemy);
                Check($"a blow that breaks the shield stuns whatever struck it (broken {p.ShieldBroken}, perfect {b.Perfect}, reeling {_probeEnemy.Reeling})", p.ShieldBroken && !b.Perfect && _probeEnemy.Reeling);
                break;
            }
            case 112:
                Check($"still stunned a second later (reeling {IsInstanceValid(_probeEnemy) && _probeEnemy.Reeling})", IsInstanceValid(_probeEnemy) && _probeEnemy.Reeling);
                if (IsInstanceValid(_probeEnemy)) _probeEnemy.QueueFree();
                _heroInput = default;
                p.RefillShield();
                p.Heal(1000);
                break;
            case 140:
                // perfect block + reflect
                Upgrades.Apply(Upgrades.Get("perfect_reflect"), p.Stats, p);
                p.Heal(1000);
                _heroInput = default;
                break;
            case 143: _probe = Shoot(new Vector2(_dir * 70, -4)); break;
            case 144: _heroInput = new PlayerInput { GuardHeld = true, GuardAim = new Vector2(_dir, 0) }; break; // raised ~0.2 s before impact
            case 150:
                Check($"a perfect block reflects the shot (reflected {IsInstanceValid(_probe) && _probe.Reflected})", IsInstanceValid(_probe) && _probe.Reflected);
                _heroInput = default;
                break;
            case 155:
            {
                // a perfect block breaks off a melee attack; an ordinary block doesn't
                var gob = new Goblin { Position = p.GlobalPosition + new Vector2(_dir * 20, -4) };
                gob.SetMeta("test", true);
                _world.AddChild(gob);
                _probeEnemy = gob;
                _heroInput = new PlayerInput { GuardHeld = true, GuardAim = new Vector2(_dir, 0) };
                break;
            }
            case 156:
            {
                _hpMark = p.Hp;
                var b = p.TryBlock(_probeEnemy.GlobalPosition, 10f, _probeEnemy);
                Check($"a perfect block stops all of a melee blow and breaks the attack off (through {b.Through:0.0}, reeling {_probeEnemy.Reeling})", b.Perfect && b.Through <= 0.01f && _probeEnemy.Reeling);
                break;
            }
            case 162:
            {
                // held for a while now: an ordinary block (no stagger)
                var gob2 = new Goblin { Position = p.GlobalPosition + new Vector2(_dir * 20, -4) };
                gob2.SetMeta("test", true);
                _world.AddChild(gob2);
                var b = p.TryBlock(gob2.GlobalPosition, 10f, gob2);
                Check($"an ordinary block stops all of it too, and leaves the attacker be (through {b.Through:0.0}, reeling {gob2.Reeling})", !b.Perfect && Math.Abs(b.Through - 10f * (1f - p.Stats.BlockShare)) < 0.01f && !gob2.Reeling);
                gob2.QueueFree();
                if (IsInstanceValid(_probeEnemy)) _probeEnemy.QueueFree();
                break;
            }
            case 165: _heroInput = new PlayerInput { GuardHeld = true, GuardAim = new Vector2(_dir, 0), Attack = true, Aim = new Vector2(_dir, 0) }; break;
            case 166:
                Check($"can swing with the shield up (swinging {p.IsSwinging}, shield {p.ShieldRaised})", p.IsSwinging && p.ShieldRaised);
                _heroInput = new PlayerInput { StickGuard = true, GuardAim = new Vector2(-_dir, 0), Move = new Vector2(_dir, 0) };
                break;
            case 168:
                Check($"the right stick raises the shield by itself, even behind you while you run (raised {p.ShieldRaised}, dir {p.ShieldDir.X:0}, facing {p.Facing})", p.ShieldRaised && Math.Sign(p.ShieldDir.X) == -_dir && (int)p.Facing == (int)_dir);
                _heroInput = default;
                p.RefillShield();
                break;

            // ---- the Guarded Charge
            case 180:
                _hpMark = p.Hp;
                _posMark = p.GlobalPosition;
                _probe = Shoot(new Vector2(_dir * 110, -4));
                _heroInput = new PlayerInput { Ability = true, Aim = new Vector2(_dir, 0) };
                break;
            case 181: _heroInput = default; break;
            case 186:
                // (with Riposte Guard, taken above, it goes back where it came from instead)
                Check($"the Guarded Charge swallows a projectile (gone {!IsInstanceValid(_probe)}, reflected {IsInstanceValid(_probe) && _probe.Reflected}, hp {_hpMark:0.0} -> {p.Hp:0.0})",
                    (!IsInstanceValid(_probe) || _probe.Reflected) && p.Hp >= _hpMark - 0.01f && !p.IsShieldDashing);
                Check($"and keeps going ({(p.GlobalPosition.X - _posMark.X) * _dir:0} px on)", (p.GlobalPosition.X - _posMark.X) * _dir > 100);
                var golem = new Golem { Position = p.GlobalPosition + new Vector2(_dir * 70, -6) };
                golem.SetMeta("test", true);
                _world.AddChild(golem);
                golem.Wake();
                _probeEnemy = golem;
                break;
            case 187: _posMark = p.GlobalPosition; p.ResetAbilityCooldowns(); break;
            case 290:
                Check($"the golem attacked (for the dash to meet){(_dashedAt > 0 || !IsInstanceValid(_probeEnemy) ? "" : $" (golem at {_probeEnemy.GlobalPosition - p.GlobalPosition:0})")}", _dashedAt > 0);
                break;
            case 292:
                if (IsInstanceValid(_probeEnemy)) _probeEnemy.QueueFree();
                break;
            case 328:
            {
                // a creature that has only just turned up (it can't have started a swing or a throw)
                var walker = new Goblin { Position = p.GlobalPosition + new Vector2(_dir * 45, -4) };
                walker.SetMeta("test", true);
                _world.AddChild(walker);
                _probeEnemy = walker;
                p.ResetAbilityCooldowns();
                break;
            }
            case 330:
                foreach (var pr in EnemyProjectiles.ToArray()) pr.QueueFree();
                _posMark = p.GlobalPosition;
                _heroInput = new PlayerInput { Ability = true, Aim = new Vector2(_dir, 0) };
                break;
            case 331: _heroInput = default; break;
            case 336:
                Check($"the dash passes by a creature that isn't attacking ({(p.GlobalPosition.X - _posMark.X) * _dir:0} px on, reeling {IsInstanceValid(_probeEnemy) && _probeEnemy.Reeling})",
                    (p.GlobalPosition.X - _posMark.X) * _dir > 60 && IsInstanceValid(_probeEnemy) && !_probeEnemy.Reeling);
                if (IsInstanceValid(_probeEnemy)) _probeEnemy.QueueFree();
                break;

            // ---- the shield bash: everything close in front
            case 345:
            {
                var gob = new Goblin { Position = p.GlobalPosition + new Vector2(_dir * 30, -4) };
                gob.SetMeta("test", true);
                _world.AddChild(gob);
                _probeEnemy = gob;
                var gob2 = new Goblin { Position = p.GlobalPosition + new Vector2(_dir * 44, -4) };
                gob2.SetMeta("test", true);
                _world.AddChild(gob2);
                _probe2 = gob2;
                p.RefillShield();
                p.ResetAbilityCooldowns();
                foreach (var pr in EnemyProjectiles.ToArray()) pr.QueueFree();
                break;
            }
            case 347:
                _hpMark = _probeEnemy.Hp;
                _hp2Mark = _probe2.Hp;
                _shieldMark = p.ShieldHp;
                _heroInput = new PlayerInput { Ability2 = true, Aim = new Vector2(_dir, 0) };
                break;
            case 348: _heroInput = default; break;
            case 352:
            {
                float Hp(Enemy e) => IsInstanceValid(e) ? e.Hp : 0;
                float dealt = _hpMark - Hp(_probeEnemy), dealt2 = _hp2Mark - Hp(_probe2);
                float want = Tune.Warden.BashDamage * p.Stats.DamageMult;
                Check($"the shield bash hits both goblins for {want:0} ({_hpMark:0} -> {Hp(_probeEnemy):0}, {_hp2Mark:0} -> {Hp(_probe2):0})", Math.Abs(dealt - want) < 0.5f && Math.Abs(dealt2 - want) < 0.5f);
                Check($"and the shield takes {p.Stats.ShieldMax * Tune.Warden.BashShieldShare:0}, once ({_shieldMark:0} -> {p.ShieldHp:0})", Math.Abs(_shieldMark - p.ShieldHp - p.Stats.ShieldMax * Tune.Warden.BashShieldShare) < 1f);
                Check($"both are stunned (reeling {IsInstanceValid(_probeEnemy) && _probeEnemy.Reeling}, {IsInstanceValid(_probe2) && _probe2.Reeling})", IsInstanceValid(_probeEnemy) && _probeEnemy.Reeling && IsInstanceValid(_probe2) && _probe2.Reeling);
                break;
            }
            case 362:
                Check($"still stunned a second later (reeling {IsInstanceValid(_probeEnemy) && _probeEnemy.Reeling})", IsInstanceValid(_probeEnemy) && _probeEnemy.Reeling);
                _hpMark = IsInstanceValid(_probeEnemy) ? _probeEnemy.Hp : 0;
                _heroInput = new PlayerInput { Ability2 = true, Aim = new Vector2(_dir, 0) };
                break;
            case 363: _heroInput = default; break;
            case 368:
                Check($"the bash waits out its cooldown (goblin hp {_hpMark:0} -> {(IsInstanceValid(_probeEnemy) ? _probeEnemy.Hp : 0):0}, cooldown {p.BashCooldownFrac:0.00})",
                    IsInstanceValid(_probeEnemy) && Math.Abs(_probeEnemy.Hp - _hpMark) < 0.01f && p.BashCooldownFrac > 0.8f);
                if (IsInstanceValid(_probeEnemy)) _probeEnemy.QueueFree();
                if (IsInstanceValid(_probe2)) _probe2.QueueFree();
                break;

            // ---- a creature struck while it winds up keeps its pose and its timing
            case 370:
            {
                var slammer = new Golem { Position = p.GlobalPosition + new Vector2(_dir * 36, -6) };
                slammer.SetMeta("test", true);
                _world.AddChild(slammer);
                slammer.Wake();
                _probeEnemy = slammer;
                _windupHit = -1;
                p.Heal(1000);
                break;
            }
            case 580:
                Check("the golem wound up a slam (for the swing to meet)", _windupHit > 0);
                if (IsInstanceValid(_probeEnemy)) _probeEnemy.QueueFree();
                // ---- holding the attack down swings again and again
                _swingsMark = p.AttacksStarted;
                _heroInput = new PlayerInput { AttackHeld = true, Aim = new Vector2(_dir, 0) };
                break;
            case 592:
                Check($"holding the attack keeps swinging ({p.AttacksStarted - _swingsMark} swings in 1.2 s)", p.AttacksStarted - _swingsMark >= 3);
                _heroInput = default;
                Finish();
                break;
        }
        if (s > 370 && s < 580 && _windupHit < 0 && IsInstanceValid(_probeEnemy) && _probeEnemy.Attacking && _probeEnemy is Golem)
        {
            // swing into it the moment its slam begins
            _windupHit = s;
            _hpMark = _probeEnemy.Hp;
            _clipMark = _probeEnemy.Animator?.Current ?? "";
            _heroInput = new PlayerInput { Attack = true, Aim = new Vector2(_dir, 0) };
        }
        else if (_windupHit > 0 && s == _windupHit + 1) _heroInput = default;
        else if (_windupHit > 0 && s == _windupHit + 3 && IsInstanceValid(_probeEnemy))
        {
            var e = _probeEnemy;
            Check($"a swing lands on the golem as it winds up (hp {_hpMark:0} -> {e.Hp:0})", e.Hp < _hpMark);
            Check($"and its slam holds for the hit-stop, then carries on as telegraphed (attacking {e.Attacking}, reeling {e.Reeling}, held {e.FreezeLeft:0.00} s, pose {_clipMark} -> {e.Animator?.Current})",
                e.Attacking && !e.Reeling && e.Animator?.Current != "hurt");
        }
        // between steps 188 and 289: dash into the golem the moment it starts its slam
        if (s > 187 && s < 290 && _dashedAt < 0 && IsInstanceValid(_probeEnemy) && _probeEnemy.Attacking && _probeEnemy is Golem)
        {
            _dashedAt = s;
            _hpMark = _probeEnemy.Hp;
            _heroInput = new PlayerInput { Ability = true, Aim = new Vector2(_dir, 0) };
        }
        else if (_dashedAt > 0 && s == _dashedAt + 1) _heroInput = default;
        else if (_dashedAt > 0 && s == _dashedAt + 5)
            Check($"the dash breaks off an attack it meets (golem reeling {IsInstanceValid(_probeEnemy) && _probeEnemy.Reeling}, hp {_hpMark:0} -> {(IsInstanceValid(_probeEnemy) ? _probeEnemy.Hp : 0):0}, still attacking {IsInstanceValid(_probeEnemy) && _probeEnemy.Attacking})",
                IsInstanceValid(_probeEnemy) && _probeEnemy.Reeling && !_probeEnemy.Attacking && _probeEnemy.Hp < _hpMark);
    }
    private int _dashedAt = -1, _windupHit = -1, _swingsMark;
    private float _hp2Mark;
    private string _clipMark = "";

    private void SwordStep(int s, Player p)
    {
        switch (s)
        {
            case 5:
            {
                // a dummy 50 px away: out of the old dagger's reach, inside the sword's
                var dummy = new Golem { Position = p.GlobalPosition + new Vector2(_dir * 50, -6) };
                dummy.SetMeta("test", true);
                _world.AddChild(dummy);
                _probeEnemy = dummy;
                _posMark = p.GlobalPosition;
                break;
            }
            case 8:
                _hpMark = _probeEnemy.Hp;
                _posMark = p.GlobalPosition;
                _heroInput = new PlayerInput { Attack = true, Aim = new Vector2(_dir, 0) };
                break;
            case 9: _heroInput = default; break;
            case 14:
                Check($"sword reaches a golem 50 px away (golem hp {_probeEnemy.Hp:0} < {_hpMark:0})", _probeEnemy.Hp < _hpMark);
                Check($"the swing lunges forward ({(p.GlobalPosition.X - _posMark.X) * _dir:0.0} px)", (p.GlobalPosition.X - _posMark.X) * _dir > 8);
                _normalHit = _hpMark - _probeEnemy.Hp;
                break;
            case 20:
                // a combo pressed during the hit-stop follows the moment it ends
                _probeEnemy.GlobalPosition = p.GlobalPosition + new Vector2(_dir * 36, -6);
                _heroInput = new PlayerInput { Attack = true, Aim = new Vector2(_dir, 0) };
                break;
            case 21: _heroInput = default; break;
        }
        if (s > 21 && s < 40 && _comboPressedAt < 0 && p.FreezeLeft > 0)
        {
            _comboPressedAt = s;
            _heroInput = new PlayerInput { Attack = true, Aim = new Vector2(_dir, 0) };
            GD.Print($"[herotest] pressed attack during the hit-stop (freeze {p.FreezeLeft:0.00} s)");
        }
        else if (_comboPressedAt > 0 && s == _comboPressedAt + 1)
        {
            _heroInput = default;
            Check($"a combo's second swing plays its own (b) stroke, the hit-stop holding the blade on the golem mid-swing (clip {p.Anim.Current}, swinging {p.IsSwinging})", p.Anim.Current.StartsWith("slash_b") && p.IsSwinging);
        }
        switch (s)
        {
            case 42:
                Check("the combo swing was pressed during a hit-stop", _comboPressedAt > 0);
                // Charged Strike: the next swing hits 50% harder, reaches further and weakens
                _probeEnemy.GlobalPosition = p.GlobalPosition + new Vector2(_dir * 50, -6);
                _heroInput = new PlayerInput { Ability = true, Aim = new Vector2(_dir, 0) };
                break;
            case 46: // (a blow landing on the hero just then would hold the press through its hit-stop)
                Check($"the ability charges the blade (charged swings {p.Charged}, cooldown {p.ChargeCooldownFrac:0.00})", p.Charged == 1 && p.ChargeCooldownFrac > 0.9f);
                _heroInput = default;
                _hpMark = _probeEnemy.Hp;
                break;
            case 50: _heroInput = new PlayerInput { Attack = true, Aim = new Vector2(_dir, 0) }; break;
            case 51: _heroInput = default; break;
            case 58:
            {
                float dealt = _hpMark - _probeEnemy.Hp;
                // (swings vary by 10% either way: beyond the strongest plain swing, by a margin)
                float most = Tune.Swordsman.Damage * p.Stats.DamageMult * 1.1f;
                Check($"a charged swing hits much harder ({dealt:0} vs {_normalHit:0}, a plain swing's best {most:0}) and is spent (charged {p.Charged})", dealt > most * 1.15f && p.Charged == 0);
                Check($"what it struck is weakened (weakened {_probeEnemy.Weakened})", _probeEnemy.Weakened);
                break;
            }
            case 60:
                // a swing out of a dodge: the roll turns into the strike
                _heroInput = new PlayerInput { Dodge = true, Move = new Vector2(-_dir, 0) };
                break;
            case 61: _heroInput = new PlayerInput { Attack = true, Aim = new Vector2(-_dir, 0) }; break;
            case 62:
                Check($"a swing can be started mid-dodge (swinging {p.IsSwinging}, dodging {p.IsDodging})", p.IsSwinging && !p.IsDodging);
                _heroInput = default;
                break;
            // ---- the heaving swing, carrying a Charged Strike
            case 64:
                p.ResetAbilityCooldowns();
                _heroInput = new PlayerInput { Ability = true, Aim = new Vector2(_dir, 0) };
                break;
            case 65:
                _probeEnemy.GlobalPosition = p.GlobalPosition + new Vector2(_dir * 55, -6);
                _hpMark = _probeEnemy.Hp;
                _posMark = p.GlobalPosition;
                _heroInput = new PlayerInput { Ability2 = true, Aim = new Vector2(_dir, 0) };
                break;
            case 66:
                Check($"the heaving swing plants you and takes the waiting charge (heaving {p.Heaving}, charged {p.Charged}, swing charged {p.SwingCharged})", p.Heaving && p.Charged == 0 && p.SwingCharged);
                Check($"nothing is struck during its long wind-up (golem {_hpMark:0} -> {_probeEnemy.Hp:0})", Math.Abs(_probeEnemy.Hp - _hpMark) < 0.01f);
                // try to walk (and jump) away: rooted
                _heroInput = new PlayerInput { Move = new Vector2(-_dir, 0), Jump = true, JumpHeld = true };
                break;
            case 67: _heroInput = new PlayerInput { Move = new Vector2(-_dir, 0) }; break;
            case 68: _posMark = p.GlobalPosition; break; // (planted by now; the golem's shove as it was set down doesn't count)
            case 73:
            {
                float dealt = _hpMark - _probeEnemy.Hp;
                Check($"it lands for about three normal swings with the charge ({dealt:0} vs {_normalHit:0} a swing)", dealt > _normalHit * 2.4f);
                Check($"and you stayed put through it ({(p.GlobalPosition.X - _posMark.X):0.0} px, {(p.GlobalPosition.Y - _posMark.Y):0.0} px)", Math.Abs(p.GlobalPosition.X - _posMark.X) < 3f && Math.Abs(p.GlobalPosition.Y - _posMark.Y) < 3f);
                _heroInput = default;
                break;
            }
            case 78:
                Check($"free to move once it's done (heaving {p.Heaving})", !p.Heaving);
                break;
            case 80:
                // Crescent Wave: a swing from well out of reach still cuts the golem
                Upgrades.Apply(Upgrades.Get("wave"), p.Stats, p);
                _probeEnemy.GlobalPosition = p.GlobalPosition + new Vector2(_dir * 120, -6);
                break;
            case 86:
                _hpMark = _probeEnemy.Hp;
                _heroInput = new PlayerInput { Attack = true, Aim = new Vector2(_dir, 0) };
                break;
            case 87: _heroInput = default; break;
            case 94:
                Check($"crescent wave hits at 120 px (golem hp {_probeEnemy.Hp:0} < {_hpMark:0})", _probeEnemy.Hp < _hpMark);
                // holding the attack down swings again and again
                _probeEnemy.GlobalPosition = p.GlobalPosition + new Vector2(_dir * 40, -6);
                _probeEnemy.Freeze(4f, hold: true);
                _swingsMark = p.AttacksStarted;
                _heroInput = new PlayerInput { AttackHeld = true, Aim = new Vector2(_dir, 0) };
                break;
            case 106:
                Check($"holding the attack keeps swinging ({p.AttacksStarted - _swingsMark} swings in 1.2 s)", p.AttacksStarted - _swingsMark >= 2);
                _heroInput = default;
                break;
            case 114:
                // a roll started with the attack still held isn't cut short by it
                _heroInput = new PlayerInput { AttackHeld = true, Dodge = true, Move = new Vector2(_dir, 0), Aim = new Vector2(_dir, 0) };
                break;
            case 115:
                Check($"holding the attack doesn't cut a roll short (dodging {p.IsDodging}, swinging {p.IsSwinging})", p.IsDodging && !p.IsSwinging);
                _heroInput = default;
                if (IsInstanceValid(_probeEnemy)) _probeEnemy.QueueFree();
                break;
            case 117:
            {
                // an air bubble from a vent gives back breath
                var vent = _world.GetChildren().OfType<AirVent>().FirstOrDefault();
                Check("the cave has air vents", vent != null);
                if (vent != null)
                {
                    p.GlobalPosition = vent.GlobalPosition + new Vector2(_dir * 0, -40);
                    p.Velocity = Vector2.Zero;
                    p.Breath = 1f;
                    _world.AddChild(new AirBubble { Position = p.GlobalPosition + new Vector2(_dir * 0, 6) });
                }
                break;
            }
            case 120:
                Check($"an air bubble refills breath ({p.Breath:0.0} s)", p.Breath > 2.2f);
                Finish();
                break;
        }
    }
    private float _normalHit;
    private int _comboPressedAt = -1;

    private void VitalistStep(int s, Player p)
    {
        Enemy Dummy(Enemy e, float dx)
        {
            e.Position = p.GlobalPosition + new Vector2(_dir * dx, -6);
            e.SetMeta("test", true);
            _world.AddChild(e);
            e.Wake();
            return e;
        }
        switch (s)
        {
            case 5:
                _probeEnemy = Dummy(new Golem(), 110);
                p.SetVitalForce(10);
                break;
            case 8:
                _hpMark = _probeEnemy.Hp;
                _shieldMark = p.VitalForce;
                _heroInput = new PlayerInput { Attack = true, Aim = new Vector2(_dir, 0.2f).Normalized() };
                break;
            case 9:
            {
                _heroInput = default;
                float dealt = _hpMark - _probeEnemy.Hp;
                Check($"the drain strikes a golem 110 px away as the staff comes forward (hp {_hpMark:0} -> {_probeEnemy.Hp:0})", dealt > 0);
                Check($"and its life flies back as a mote (motes {_world.GetChildren().OfType<LifeMote>().Count()})", _world.GetChildren().OfType<LifeMote>().Any());
                break;
            }
            case 16:
            {
                float dealt = _hpMark - _probeEnemy.Hp;
                Check($"the mote arrives: a tenth of the damage as vital force ({_shieldMark:0.0} -> {p.VitalForce:0.0}, want +{dealt * 0.1f:0.0})", Math.Abs(p.VitalForce - _shieldMark - dealt * 0.1f) < 0.05f && !_world.GetChildren().OfType<LifeMote>().Any());
                _normalHit = dealt;
                Check($"a drain hits for about {Tune.Vitalist.DrainDamage:0} ({dealt:0.0})", dealt > Tune.Vitalist.DrainDamage * 0.9f && dealt < Tune.Vitalist.DrainDamage * 1.1f);
                break;
            }
            case 18: _heroInput = new PlayerInput { Dodge = true }; break;
            case 19: _heroInput = default; break;
            case 20:
                Check($"the hex reaches the golem (hexed {_probeEnemy.Hexed})", _probeEnemy.Hexed);
                _hpMark = _probeEnemy.Hp;
                _heroInput = new PlayerInput { Attack = true, Aim = new Vector2(_dir, 0.2f).Normalized() };
                break;
            case 21: _heroInput = default; break;
            case 28:
            {
                float dealt = _hpMark - _probeEnemy.Hp;
                // (more than the strongest drain could do unhexed: its damage varies by 8% either way)
                float most = Tune.Vitalist.DrainDamage * p.Stats.DamageMult * 1.08f;
                Check($"a hexed creature takes more damage ({dealt:0.0}, more than an unhexed drain's best {most:0.0})", dealt > most);
                _probeEnemy.QueueFree();
                Check($"vital force holds {Tune.Vitalist.VitalForceMax:0} at most (max {p.Stats.VitalForceMax:0})", Math.Abs(p.Stats.VitalForceMax - Tune.Vitalist.VitalForceMax) < 0.01f);
                // the heal: everything to the one hurt player in range
                p.Hp = 20;
                p.SetVitalForce(30);
                p.ResetAbilityCooldowns();
                _hpMark = p.Hp;
                _heroInput = new PlayerInput { Ability = true };
                break;
            }
            case 29: _heroInput = default; break;
            case 31:
            {
                float want = Tune.Vitalist.HealAmount * p.Stats.HealMult;
                Check($"the heal restores {want:0} (hp {_hpMark:0} -> {p.Hp:0}) for {p.HealCost:0} vital force (30 -> {p.VitalForce:0})", Math.Abs(p.Hp - _hpMark - want) < 0.5f && Math.Abs(30 - p.VitalForce - p.HealCost) < 0.01f);
                break;
            }
            case 40:
                p.Hp = p.Stats.MaxHp;
                p.ResetAbilityCooldowns();
                _shieldMark = p.VitalForce;
                _heroInput = new PlayerInput { Ability = true };
                break;
            case 41: _heroInput = default; break;
            case 43:
                Check($"no heal (and no vital force spent) when no one is hurt ({_shieldMark:0} -> {p.VitalForce:0})", Math.Abs(p.VitalForce - _shieldMark) < 0.01f);
                break;

            // ---- the rupture
            case 45:
                _probeEnemy = Dummy(new Golem(), 110);
                _probe2 = Dummy(new Goblin(), 150);   // 40 px from the golem: in the burst
                _probe3 = Dummy(new Goblin(), 290);   // well outside it
                p.SetVitalForce(30);
                p.ResetAbilityCooldowns();
                break;
            case 47:
                // the goblins held where they are for the burst (one right beside the golem, one well away)
                _probe2.GlobalPosition = _probeEnemy.GlobalPosition + new Vector2(_dir * 26, 0);
                _probe3.GlobalPosition = _probeEnemy.GlobalPosition + new Vector2(_dir * 180, 0);
                if (G.Cave.IsSolid(_probe3.GlobalPosition)) _probe3.GlobalPosition = _probeEnemy.GlobalPosition + new Vector2(-_dir * 180, -20);
                _probe2.Freeze(1.2f, hold: true); _probe3.Freeze(1.2f, hold: true);
                _hpMark = _probeEnemy.Hp; _hp2 = _probe2.Hp; _hp3 = _probe3.Hp;
                _heroInput = new PlayerInput { Ability2 = true, Aim = new Vector2(_dir, 0.15f).Normalized() };
                break;
            case 48:
                _heroInput = default;
                Check($"the rupture spends {p.RuptureCost:0} vital force (left {p.VitalForce:0}) and seizes the golem first (frozen {_probeEnemy.FreezeLeft:0.00} s, hp {_hpMark:0} -> {_probeEnemy.Hp:0})",
                    p.VitalForce < 0.5f && _probeEnemy.FreezeLeft > 0 && Math.Abs(_probeEnemy.Hp - _hpMark) < 0.01f);
                break;
            case 54:
            {
                float main = _hpMark - _probeEnemy.Hp, near = _hp2 - (IsInstanceValid(_probe2) ? _probe2.Hp : 0), far = _hp3 - _probe3.Hp;
                Check($"then it bursts: {main:0} to the golem, {near:0} to the goblin beside it, {far:0} to the one far off",
                    main > Tune.Vitalist.RuptureDamage * 0.9f && Math.Abs(near - Tune.Vitalist.RuptureSplash) < 0.6f && far < 0.01f);
                // not enough vital force: refused, nothing spent
                p.ResetAbilityCooldowns();
                p.SetVitalForce(10);
                _heroInput = new PlayerInput { Ability2 = true, Aim = new Vector2(_dir, 0.15f).Normalized() };
                _hpMark = _probeEnemy.Hp;
                break;
            }
            case 55: _heroInput = default; break;
            case 60:
                Check($"no rupture without the vital force for it (vital force {p.VitalForce:0}, golem {_hpMark:0} -> {_probeEnemy.Hp:0})", Math.Abs(p.VitalForce - 10) < 0.5f && Math.Abs(_probeEnemy.Hp - _hpMark) < 0.01f);
                foreach (var e in new[] { _probe2, _probe3 }) if (IsInstanceValid(e)) e.QueueFree();
                break;

            // ---- Many Mouths: the drain takes a second creature near the target
            case 62:
                Upgrades.Apply(Upgrades.Get("mouths"), p.Stats, p);
                _probe2 = Dummy(new Golem(), 150);
                break;
            case 64:
                // (both held where they stand: the strike lands a beat after the press, as the staff comes forward)
                _probeEnemy.Freeze(0.5f, hold: true); _probe2.Freeze(0.5f, hold: true);
                _hpMark = _probeEnemy.Hp; _hp2 = _probe2.Hp;
                _heroInput = new PlayerInput { Attack = true, Aim = new Vector2(_dir, 0.15f).Normalized() };
                break;
            case 65:
            {
                _heroInput = default;
                float a = _hpMark - _probeEnemy.Hp, b = _hp2 - _probe2.Hp;
                Check($"Many Mouths drains the creature beside the target too ({a:0} and {b:0}, want {b:0} ~ 60% of {a:0})", a > 0 && b > 0 && Math.Abs(b / a - Tune.Vitalist.MultiShare) < 0.2f);
                _probeEnemy.QueueFree(); _probe2.QueueFree();
                break;
            }

            // ---- Twin Reserve: two heals back to back, then a long wait
            case 67:
                Upgrades.Apply(Upgrades.Get("rr_reserve"), p.Stats, p);
                p.ResetAbilityCooldowns();
                p.SetVitalForce(30);
                p.Hp = 10;
                _hpMark = p.Hp;
                _heroInput = new PlayerInput { Ability = true };
                break;
            case 68: _heroInput = default; break;
            case 73: _heroInput = new PlayerInput { Ability = true }; break;
            case 74: _heroInput = default; break;
            case 77:
            {
                float want = 2 * Tune.Vitalist.HealAmount * p.Stats.HealMult;
                Check($"Twin Reserve: two heals in a row (hp {_hpMark:0} -> {p.Hp:0}, want +{want:0}; uses left {p.AbilityUsesReady})", Math.Abs(p.Hp - _hpMark - want) < 0.5f && p.AbilityUsesReady == 0);
                Check($"each use comes back in twice the time ({p.AbilityRecharge:0.0} s)", Math.Abs(p.AbilityRecharge - 2 * Tune.Vitalist.HealCooldown) < 0.01f);
                // holding the attack down drains again and again
                _probeEnemy = Dummy(new Golem(), 100);
                _probeEnemy.Freeze(3f, hold: true);
                _swingsMark = p.AttacksStarted;
                _hpMark = _probeEnemy.Hp;
                _heroInput = new PlayerInput { AttackHeld = true, Aim = new Vector2(_dir, 0.15f).Normalized() };
                break;
            }
            case 90:
                Check($"holding the attack keeps draining ({p.AttacksStarted - _swingsMark} drains in 1.2 s, golem {_hpMark:0} -> {_probeEnemy.Hp:0})", p.AttacksStarted - _swingsMark >= 2 && _probeEnemy.Hp < _hpMark);
                _heroInput = default;
                _probeEnemy.QueueFree();
                Finish();
                break;
        }
    }

    private Enemy _probe2, _probe3;
    private float _hp2, _hp3;

    private Enemy _probeEnemy;
    private float _dir;

    private void Finish()
    {
        GD.Print(_heroOk ? "[herotest] PASS" : "[herotest] FAIL");
        SafeQuit.Request(this, _heroOk ? 0 : 1);
    }

    private PlayerInput AnimTestInput()
    {
        float t = _animT;
        var i = new PlayerInput();
        bool Edge(float at) => t >= at && t - (float)GetProcessDeltaTime() < at;
        if (t > 0.3f && t < 1.0f) i.Move.X = 1;
        else if (t > 1.0f && t < 1.45f) i.Move.X = -1;
        i.Jump = Edge(1.7f);
        i.JumpHeld = t > 1.7f && t < 2.0f;
        if (Edge(2.6f)) { i.Attack = true; i.Aim = new Vector2(-1, 0); }
        if (Edge(2.9f)) { i.Attack = true; i.Aim = new Vector2(-0.3f, -1).Normalized(); }
        if (Edge(3.2f)) { i.Attack = true; i.Aim = new Vector2(-1, 0.1f).Normalized(); }
        if (Edge(3.7f)) { i.Dodge = true; i.Move.X = 1; }
        if (Edge(4.2f)) { i.Ability = true; i.Aim = new Vector2(1, 0); }
        if (Edge(4.6f)) G.Player.Hurt(1, G.Player.GlobalPosition + new Vector2(20, 0));
        return i;
    }

    private void AnimTestTick(float dt)
    {
        _animT += dt;
        if (_animT > 0.2f && (int)(_animT * 20) > _animFrame)
        {
            _animFrame = (int)(_animT * 20);
            var img = GetViewport().GetTexture().GetImage();
            var sp = G.Player.GetGlobalTransformWithCanvas().Origin;
            var scale = img.GetSize() / GetViewport().GetVisibleRect().Size;
            var r = new Rect2I((int)(sp.X * scale.X) - 60, (int)(sp.Y * scale.Y) - 90, 300, 140);
            img.GetRegion(r).SavePng($"{_shotDir}/at_{_animFrame:000}.png");
        }
        if (_animT > 5.4f) SafeQuit.Request(this);
    }

    /// <summary>--nntest: checks the brain's maths (gradients, learning, save/load) without a window.</summary>
    private void RunNnTest()
    {
        bool ok = true;
        float worst = Brain.GradientCheck();
        GD.Print($"[nntest] gradient check: worst relative error {worst:0.00000}");
        ok &= worst < 0.02f;

        // A contextual bandit: 4 moves, the right one is whichever of the first 4 inputs is largest.
        // From scratch (no teacher) it must learn it from rewards alone.
        float teacher = Tune.Brains.TeacherStart;
        Tune.Brains.TeacherStart = 0;
        var rng = new Random(7);
        var b = new Brain("bandit", 8, 4, 11);
        var probs = new float[4];
        var mask = new[] { true, true, true, true };
        float Accuracy()
        {
            int hit = 0;
            for (int n = 0; n < 400; n++)
            {
                var x = new float[8];
                for (int i = 0; i < 8; i++) x[i] = (float)rng.NextDouble() * 2 - 1;
                b.Choose(x, mask, false, out _, probs);
                int best = 0, pick = 0;
                for (int k = 1; k < 4; k++) { if (x[k] > x[best]) best = k; if (probs[k] > probs[pick]) pick = k; }
                if (pick == best) hit++;
            }
            return hit / 400f;
        }
        float before = Accuracy();
        for (int n = 0; n < 12000; n++)
        {
            var x = new float[8];
            for (int i = 0; i < 8; i++) x[i] = (float)rng.NextDouble() * 2 - 1;
            int a = b.Choose(x, mask, true, out _, probs);
            int best = 0;
            for (int k = 1; k < 4; k++) if (x[k] > x[best]) best = k;
            b.Remember(new Brain.Transition { X = x, Mask = mask, Action = a, Teacher = -1, Target = a == best ? 1 : -0.2f });
        }
        float after = Accuracy();
        GD.Print($"[nntest] reward learning: accuracy {before:P0} -> {after:P0} ({b.Updates} updates, {b.ParamCount} parameters)");
        ok &= after > 0.8f;
        Tune.Brains.TeacherStart = teacher;

        // save / load round trip
        var copy = Brain.FromJson(b.ToJson(), "bandit", 8, 4);
        var probe = new float[] { 0.1f, -0.3f, 0.9f, 0.2f, 0, 0.5f, -0.5f, 0.3f };
        bool same = copy != null && Math.Abs(copy.Value(probe) - b.Value(probe)) < 1e-6f;
        bool rejects = Brain.FromJson(b.ToJson(), "bandit", 9, 4) == null;
        GD.Print($"[nntest] save/load round trip: {(same ? "identical" : "MISMATCH")}; wrong shape rejected: {rejects}");
        ok &= same && rejects;

        // decision cost
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var big = new Brain("speed", Enemy.BaseInputs + 5, 5, 3);
        var xs = new float[Enemy.BaseInputs + 5];
        var pr = new float[5];
        var m5 = new[] { true, true, true, true, true };
        for (int n = 0; n < 10000; n++) big.Choose(xs, m5, true, out _, pr);
        GD.Print($"[nntest] one decision takes {sw.Elapsed.TotalMilliseconds / 10000 * 1000:0.0} microseconds");

        GD.Print(ok ? "[nntest] PASS" : "[nntest] FAIL");
        SafeQuit.Request(this, ok ? 0 : 1);
    }

    private void RunGenTest()
    {
        int fineOk = 0; int n = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--gencount=")) is string gc ? int.Parse(gc[11..]) : 12, cleanAll = 0, totalAll = 0, vaultsAll = 0, vaultWant = 0;
        ulong total = 0;
        CaveGenerator.Verbose = OS.GetCmdlineUserArgs().Contains("--genverbose");
        if (CaveGenerator.Verbose)
            CaveGenerator.OnAttempt = (c, s) => { if (OS.GetCmdlineUserArgs().Contains($"--genimage={s}")) SaveCaveImage(c, $"user://attempt_{c.Biome?.Id}_{s}.png"); };
        foreach (var b in Biomes.All)
        {
            if (_biomeArg != null && !b.Id.ToString().Equals(_biomeArg, StringComparison.OrdinalIgnoreCase)) continue;
            G.Biome = b;
            int clean = 0, vaults = 0, fineB = 0, attemptsB = 0;
            bool wantVault = b.Style != GenStyle.Arena;
            for (int s = 1; s <= n; s++)
            {
                ulong t0 = Time.GetTicksMsec();
                var c = CaveGenerator.Generate(b, s * 1013);
                ulong ms = Time.GetTicksMsec() - t0;
                CaveGenerator.PutLedgesBack(c);
                attemptsB += c.Attempts;
                total += ms;
                bool fine = FineBossReachable(c, OS.GetCmdlineUserArgs().Contains("--finedump") ? $"/tmp/claude-0/fine/{b.Id}_{s * 1013}_{(OS.GetCmdlineUserArgs().Contains("--finedump") ? "x" : "")}.png" : null);
                if (fine) { fineOk++; fineB++; }
                bool ok = c.TrapCells <= 6 && c.Boss != null && BossReachable(c);
                if (ok) clean++;
                bool vault = VaultSound(c, out string why);
                if (vault) vaults++;
                if (!ok || s <= 3 || CaveGenerator.Verbose || (wantVault && !vault))
                    GD.Print($"  {b.Id,-9} seed {s * 1013}: {ms} ms attempts {c.Attempts} traps {c.TrapCells} reachable {c.ReachableCells} rooms {c.Rooms.Count} minis {c.Rooms.Count(r => r.Kind == RoomKind.MiniBoss)} boss {(c.Boss != null)} bossReach {BossReachable(c)} FINE {fine} reps {c.FineRepairs} spawns {c.Spawns.Count} shores {c.Spawns.Count(x => x.Kind == SpawnKind.Shore)} ice {c.IceLedges.Count} rubble {c.Rubble.Count} (dead ends {c.RubbleAtDeadEnds}) platforms {c.NaturalPlatforms}/{c.Ledges.Count} secrets {c.Rooms.Count(r => r.Kind == RoomKind.Secret)} drain {(c.Drain != null ? "yes" : "no")} start {c.StartPos.Y / CaveData.Cell / c.H:0.00} nooks {c.Hints.Count(h => h.Kind == 0)}f/{c.Hints.Count(h => h.Kind == 1)}s vault {(vault ? "ok" : why)}");
                if (s == 1 || OS.GetCmdlineUserArgs().Contains($"--genimage={s * 1013}")) SaveCaveImage(c, s == 1 ? $"user://cave_{b.Id}.png" : $"user://cave_{b.Id}_{s * 1013}.png");
            }
            GD.Print($"[gentest] {b.Id}: fine {fineB}/{n} ({attemptsB / (float)n:0.0} attempts each); {clean}/{n} trap-free with a reachable exit, {vaults}/{(wantVault ? n : 0)} with a sound vault  ->  {ProjectSettings.GlobalizePath($"user://cave_{b.Id}.png")}");
            cleanAll += clean; totalAll += n;
            if (wantVault) { vaultsAll += vaults; vaultWant += n; }
        }
        if (OS.GetCmdlineUserArgs().Contains("--genprof")) foreach (var kv in CaveGenerator.Prof.OrderByDescending(k => k.Value.ticks)) GD.Print($"[genprof] {kv.Key,-16} {kv.Value.ticks * 1000.0 / System.Diagnostics.Stopwatch.Frequency / Math.Max(1, totalAll),9:0.0} ms per cave  ({kv.Value.calls} calls)");
        GD.Print($"[gentest] {fineOk}/{totalAll} caves whose guardian a real hero can reach (pixel-true jumps, a body-sized fit)");
        GD.Print($"[gentest] {cleanAll}/{totalAll} trap-free, {vaultsAll}/{vaultWant} with a sound vault, avg {total / (ulong)Math.Max(1, totalAll)} ms");
        // (every level but the dragon's lair has its vault)
        SafeQuit.Request(this, vaultsAll == vaultWant ? 0 : 1);
    }

    /// <summary>--metatest: the resource draw, buying and activating ranks, and their effects.</summary>
    private void RunMetaTest()
    {
        bool ok = true;
        void Check(string what, bool cond) { GD.Print($"[metatest] {(cond ? "ok  " : "FAIL")} {what}"); ok &= cond; }
        Meta.Embers = 0; Meta.Bought.Clear(); Meta.Active.Clear(); Meta.Held.Clear(); Meta.Found.Clear();
        Check("trees hidden before any resource", !Meta.Trees.Any(Meta.Visible));
        var rng = new Random(3);
        int reagents = 0, pearls = 0;
        for (int k = 0; k < 22; k++) { var t = Meta.RollResource(rng); if (t == Meta.PotionTree) reagents++; else if (t == Meta.PearlTree) pearls++; }
        Check($"22 draws give every resource once (13 reagents: {reagents}, 9 pearls: {pearls})", reagents == 13 && pearls == 9);
        Check("nothing once all are found", Meta.RollResource(rng) == null);
        var heal1 = Meta.PotionTree.Branch("heal").First();
        Check("can't buy without embers", !Meta.Buy(heal1));
        Meta.Embers = 10;
        Check("buying rank 1 costs 1 ember", Meta.Buy(heal1) && Meta.Embers == 9);
        Check("a bought rank does nothing until activated", Math.Abs(Meta.PotionHealNow - 0.15f) < 1e-4f);
        Check("activating spends a reagent", Meta.Activate(heal1) && Meta.HeldOf(Meta.PotionTree) == 12);
        Check($"immediate heal now 20% ({Meta.PotionHealNow:P0})", Math.Abs(Meta.PotionHealNow - 0.20f) < 1e-4f);
        foreach (var id in new[] { "speed1", "speed2", "speed3", "max1", "mile1", "mile2", "mile3", "xp1" })
        {
            var tier = Meta.Trees.SelectMany(t => t.Tiers).First(x => x.Id == id);
            Meta.Embers += tier.Cost; Meta.Buy(tier); Meta.Activate(tier);
        }
        Check($"heal over time takes 10 s after three ranks ({Meta.PotionHotSeconds})", Math.Abs(Meta.PotionHotSeconds - 10f) < 1e-4f);
        Check($"two potions with one flask rank ({Meta.MaxPotions})", Meta.MaxPotions == 2);
        Check($"milestones every 2 levels after three ranks ({Meta.MilestoneEvery})", Meta.MilestoneEvery == 2);
        Check($"+5% experience ({Meta.XpMult})", Math.Abs(Meta.XpMult - 1.05f) < 1e-4f);
        GD.Print(ok ? "[metatest] PASS" : "[metatest] FAIL");
        SafeQuit.Request(this, ok ? 0 : 1);
    }

    /// <summary>
    /// A vault that works: it's there; shut its gate and the open cells round its chest never
    /// get out of the cut (it opens only through the gate); and the tunnel at its doorstep is
    /// somewhere you can walk to.
    /// </summary>
    private static bool VaultSound(CaveData c, out string why)
    {
        why = "";
        var v = c.Vault;
        if (v == null) { why = "none"; return false; }
        const float cell = CaveData.Cell;
        int gi = (int)(v.Gate.X / cell), fj = (int)(v.Gate.Y / cell) - 1;
        var box = v.Passage.Merge(v.Chamber);
        var from = new Vector2I((int)(v.Chest.X / cell), (int)(v.Chest.Y / cell) - 1);
        if (!c.CellOpen(from.X, from.Y)) { why = "its chest sits in rock"; return false; }
        var seen = new HashSet<Vector2I> { from };
        var q = new Queue<Vector2I>();
        q.Enqueue(from);
        while (q.Count > 0)
        {
            var u = q.Dequeue();
            if (!box.HasPoint(u)) { why = $"it leaks at {u}"; return false; }
            foreach (var d in new[] { Vector2I.Left, Vector2I.Right, Vector2I.Up, Vector2I.Down })
            {
                var w = u + d;
                if (w.X == gi || seen.Contains(w) || !c.CellOpen(w.X, w.Y)) continue;
                seen.Add(w);
                q.Enqueue(w);
            }
        }
        // the tunnel floor its doorstep starts from can be walked to, and from there to the gate
        // the way is open, a hero's height, over rock (a dip of a cell where the cut meets the
        // tunnel is nothing; a hole is)
        int ai = (int)(v.Approach.X / cell), aj = (int)(v.Approach.Y / cell) - 1;
        if (c.ReachMask == null || !c.ReachMask[aj * c.W + ai]) { why = "its doorstep can't be reached"; return false; }
        for (int i = ai; i != gi; i += v.Side)
            if (!c.CellOpen(i, fj) || !c.CellOpen(i, fj - 1) || (c.CellOpen(i, fj + 1) && c.CellOpen(i, fj + 2))) { why = $"the way to its gate is blocked at {i}"; return false; }
        return true;
    }

    /// <summary>
    /// --bosstest: for every biome and 24 seeds, the guardian's chamber must have a spot the hero
    /// can reach that wakes it, and the guardian must be placed on floor the hero can reach.
    /// </summary>
    private void RunBossTest()
    {
        int bad = 0, total = 0;
        foreach (var b in Biomes.All)
        {
            if (b.Id == BiomeId.Lair || (_biomeArg != null && !b.Id.ToString().Equals(_biomeArg, StringComparison.OrdinalIgnoreCase))) continue;
            G.Biome = b;
            int ok = 0;
            for (int s = 1; s <= 24; s++)
            {
                var c = CaveGenerator.Generate(b, s * 7919);
                G.Cave = c;
                _roomCells.Clear();
                total++;
                var room = c.Boss;
                bool trigger = false;
                foreach (int k in RoomCells(c, room))
                {
                    int i = k % c.W, j = k / c.W;
                    if (!c.CellOpen(i, j + 1) && Reach(c, i, j) && InRoom(c, room, new Vector2((i + 0.5f) * CaveData.Cell, (j + 0.9f) * CaveData.Cell), 0.9f)) { trigger = true; break; }
                }
                var guardian = b.Guardian(room);
                var floor = GuardianFloor(c, room, room.Floor.X + room.RxPx * 0.25f, guardian);
                guardian.Free();
                int fi = (int)(floor.X / CaveData.Cell), fj = (int)(floor.Y / CaveData.Cell) - 1;
                bool reach = Reach(c, fi, fj) || Reach(c, fi, fj - 1);
                // most of the chamber's own floor (where you walk in) must wake it, not a lucky corner
                int floorSpots = 0, floorWakes = 0;
                foreach (int k in RoomCells(c, room))
                {
                    int i = k % c.W, j = k / c.W;
                    var at = new Vector2((i + 0.5f) * CaveData.Cell, (j + 0.9f) * CaveData.Cell);
                    if (c.CellOpen(i, j + 1) || !Reach(c, i, j) || Math.Abs(at.Y - room.Floor.Y) > 2.5f * CaveData.Cell) continue;
                    floorSpots++;
                    if (InRoom(c, room, at, 0.9f)) floorWakes++;
                }
                bool floorOk = floorSpots == 0 || floorWakes * 2 >= floorSpots;
                if (trigger && reach && floorOk) ok++;
                else { bad++; GD.Print($"[bosstest] {b.Id} seed {s * 7919}: trigger spot {trigger}, floor that wakes it {floorWakes}/{floorSpots}, guardian floor reachable {reach} at {floor}"); }
            }
            GD.Print($"[bosstest] {b.Id}: {ok}/24");
        }
        GD.Print(bad == 0 ? $"[bosstest] PASS ({total} caves)" : $"[bosstest] FAIL: {bad} of {total}");
        SafeQuit.Request(this, bad == 0 ? 0 : 1);
    }

    /// <summary>Whether the guardian's chamber can really be reached by the slowest jumper (see FineReach).</summary>
    private static bool FineBossReachable(CaveData c, string dump = null)
    {
        if (c.Boss == null) return false;
        var f = new FineReach(c);
        bool ok = f.Run(c.StartPos, c.Boss.Floor);
        if (dump != null) f.SavePng(dump, c.StartPos, c.Boss.Floor);
        return ok;
    }

    private static bool BossReachable(CaveData c)
    {
        if (c.Boss == null || c.ReachMask == null) return false;
        int bi = (int)(c.Boss.Floor.X / CaveData.Cell), bj = (int)(c.Boss.Floor.Y / CaveData.Cell) - 2;
        for (int dj = -3; dj <= 3; dj++)
            for (int di = -4; di <= 4; di++)
            {
                int i = bi + di, j = bj + dj;
                if (i >= 0 && j >= 0 && i < c.W && j < c.H && c.ReachMask[j * c.W + i]) return true;
            }
        return false;
    }
}
