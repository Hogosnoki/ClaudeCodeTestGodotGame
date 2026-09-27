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
/// generator statistics for a batch of seeds and quits), `--start=boss|water` (spawn position
/// for testing), `--train` (enemy brain training on from the start), `--braindir=DIR` and
/// `--nntest` (checks the neural-net maths and quits).
/// </summary>
public partial class Main : Node
{
    public readonly List<EnemyProjectile> EnemyProjectiles = new();
    public Enemy ActiveBoss;
    /// <summary>Rewards skipped on this level: each pays an ember if its guardian falls.</summary>
    public int SkipBank;
    /// <summary>True while a menu or screen has the controls (the hero ignores input).</summary>
    public bool MenuOpen => _state != State.Playing;
    private MetaMenu _metaMenu;
    private bool _victory;
    private int _runEmbers;
    private string _runFinds = "";

    private Node2D _world;
    private Camera2D _cam;
    private Stage3D _stage;
    /// <summary>The gameplay camera (2D): it decides what counts as on screen; the 3D camera follows it.</summary>
    public Camera2D Cam2D => _cam;
    private CanvasLayer _uiLayer, _darkLayer;
    private Hud _hud;
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
    private readonly Queue<Vector2> _pendingTreasure = new(); // chests opened (where), awaiting a pick
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
    private void MetaShotTick()
    {
        var steps = new (float at, Action act)[]
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
    private string _biomeArg;

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

        _world = new Node2D { Name = "World", ProcessMode = ProcessModeEnum.Pausable, Visible = false };
        AddChild(_world);
        G.World = _world;

        _darkLayer = new CanvasLayer { Layer = 5, Visible = false };
        AddChild(_darkLayer);
        _darkLayer.AddChild(new DarknessOverlay());

        _uiLayer = new CanvasLayer { Layer = 10 };
        AddChild(_uiLayer);
        _hud = new Hud();
        _uiLayer.AddChild(_hud);
        _upgradeMenu = new UpgradeMenu();
        _upgradeMenu.Picked += OnUpgradePicked;
        _uiLayer.AddChild(_upgradeMenu);
        _overlay = new ScreenOverlay();
        _uiLayer.AddChild(_overlay);
        _metaMenu = new MetaMenu();
        _metaMenu.Closed += OnMetaClosed;
        _uiLayer.AddChild(_metaMenu);

        ParseArgs(out bool gentest);
        G.NoSave = _autotest || gentest || _nnTest || _heroTest || _hitStopTest || _bestiary || _animTest || _padTest || _titleShot != "" || OS.GetCmdlineUserArgs().Contains("--metatest") || _metaShot != "" || _lookShot != "";
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
        if (OS.GetCmdlineUserArgs().Contains("--metatest")) { RunMetaTest(); return; }
        if (_nnTest) { RunNnTest(); return; }

        _seed = _seed != 0 ? _seed : (int)(Time.GetUnixTimeFromSystem() * 1000 % 1000000);
        if (_autotest) G.Rng = new Random(_seed);
        G.Depth = 0;
        G.Biome = Biomes.Get(BiomeId.Entrance);
        if (_biomeArg == null && (_heroTest || _bestiary || _animTest || _showcase || _hitStopTest)) _biomeArg = "slime"; // these need water
        if (_biomeArg != null)
        {
            G.Biome = Biomes.All.First(b => b.Id.ToString().Equals(_biomeArg, StringComparison.OrdinalIgnoreCase));
            G.Depth = G.Biome.MinDepth;
        }
        BuildLevel(_seed, freshPlayer: true);
        if (_padTest)
        {
            // Starts on the title screen and drives everything with synthetic controller events.
            GetTree().Paused = true;
            _state = State.Title;
        }
        else if (_heroTest)
        {
            StartPlaying();
            _hud.HintTime = 0;
            // a step's presses last one frame, like a real button (held flags stay as the step set them)
            G.Player.InputOverride = () =>
            {
                var i = _heroInput;
                _heroInput.Attack = _heroInput.Ability = _heroInput.Dodge = _heroInput.Jump = _heroInput.Potion = _heroInput.Interact = false;
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
        else if (_lookShot != "")
        {
            StartPlaying();
            _hud.HintTime = 0;
            G.Player.InputOverride = () => default;
        }
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
            _sfx.SetMusic("ambient");
        }
    }

    private void ShowTitle()
    {
        _overlay.HeroCards = true;
        _overlay.Show("DAGGER DEEP", 0.55f,
            "A rogue-lite descent from the cave mouth to the dragon at the bottom of the world.  Choose your hero:",
            "@",
            "KEYBOARD + MOUSE:  A / D move   SPACE jump   W / S swim   LEFT CLICK attack   RIGHT CLICK ability   SHIFT dodge / shield / hex   E enter   Q potion",
            "CONTROLLER:  stick move   A jump   X attack   RB / RT ability   B / LB dodge / shield / hex   UP enter   Y potion   right stick aims (and raises the shield)",
            CampLine(),
            "!LEFT / RIGHT to choose  -  ENTER / A to begin");
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
            else if (a.StartsWith("--shots=")) _shotDir = a[8..];
            else if (a.StartsWith("--duration=")) _duration = float.Parse(a[11..], System.Globalization.CultureInfo.InvariantCulture);
            else if (a.StartsWith("--start=")) _startAt = a[8..];
            else if (a.StartsWith("--titleshot=")) _titleShot = a[12..];
            else if (a == "--bestiary") _bestiary = true;
            else if (a == "--animtest") _animTest = true;
            else if (a == "--herotest") _heroTest = true;
            else if (a == "--hitstoptest") _hitStopTest = true;
            else if (a == "--padtest") _padTest = true;
            else if (a == "--showcase") { _showcase = true; _autotest = true; }
            else if (a == "--train") Brains.Training = true;
            else if (a == "--hero=warden") G.Hero = HeroKind.Warden;
            else if (a == "--hero=swordsman") G.Hero = HeroKind.Swordsman;
            else if (a == "--hero=vitalist") G.Hero = HeroKind.Vitalist;
            else if (a.StartsWith("--braindir=")) Brains.DirOverride = a[11..];
            else if (a == "--nntest") _nnTest = true;
            else if (a.StartsWith("--biome=")) _biomeArg = a[8..];
            else if (a == "--fullrun") _fullRun = true;
            else if (a.StartsWith("--metashot=")) _metaShot = a[11..];
            else if (a.StartsWith("--lookshot=")) _lookShot = a[11..];
            else if (a.StartsWith("--frames=")) _lookFrames = int.Parse(a[9..]);
            else if (a.StartsWith("--fxtest=")) _fxTest = int.Parse(a[9..]);
            else if (a == "--proptest") _propTest = true;
            else if (a == "--exittest") _exitTest = true;
        }
    }

    // --lookshot=PATH [--frames=N]: build the level, stand still for N frames, save a screenshot
    // and quit (look development for the 3D presentation)
    private string _lookShot = "";
    private int _lookFrames = 24, _lookFrame;
    private bool _exitTest;

    private int _fxTest;
    private bool _propTest;

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

    private void SpawnPropTest()
    {
        var p = G.Player.GlobalPosition;
        var cave = G.Cave;
        Vector2 Floor(float dx) => cave.FindFloor(p + new Vector2(dx, -40), 200, out var f) ? f : p + new Vector2(dx, 12);
        void Add(Node2D n, Vector2 at) { n.Position = at; _world.AddChild(n); }
        Add(new Chest(), Floor(-150));
        Add(new XpOrb { Value = 3 }, p + new Vector2(-110, -40));
        Add(new XpOrb { Value = 10 }, p + new Vector2(-95, -52));
        Add(new HeartPickup(), p + new Vector2(-70, -45));
        Add(new PotionPickup(), p + new Vector2(-45, -45));
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
        if (_exitTest && _lookFrame == 2) SpawnExitTest();
        if (_lookFrame < _lookFrames) return;
        GetViewport().GetTexture().GetImage().SavePng(_lookShot);
        GD.Print($"[lookshot] saved {_lookShot}");
        SafeQuit.Request(this);
    }

    private static void SetupInput()
    {
        void Act(string name, params InputEvent[] evs)
        {
            if (!InputMap.HasAction(name)) InputMap.AddAction(name, 0.25f);
            foreach (var e in evs) InputMap.ActionAddEvent(name, e);
        }
        InputEventKey K(Key k) => new() { PhysicalKeycode = k };
        InputEventJoypadButton J(JoyButton b) => new() { ButtonIndex = b };
        InputEventJoypadMotion Ax(JoyAxis a, float v) => new() { Axis = a, AxisValue = v };

        Act("move_left", K(Key.A), K(Key.Left), Ax(JoyAxis.LeftX, -1), J(JoyButton.DpadLeft));
        Act("move_right", K(Key.D), K(Key.Right), Ax(JoyAxis.LeftX, 1), J(JoyButton.DpadRight));
        Act("move_up", K(Key.W), K(Key.Up), Ax(JoyAxis.LeftY, -1), J(JoyButton.DpadUp));
        Act("move_down", K(Key.S), K(Key.Down), Ax(JoyAxis.LeftY, 1), J(JoyButton.DpadDown));
        Act("jump", K(Key.Space), J(JoyButton.A));
        Act("attack", new InputEventMouseButton { ButtonIndex = MouseButton.Left });
        Act("attack_alt", K(Key.J), J(JoyButton.X));
        Act("ability", new InputEventMouseButton { ButtonIndex = MouseButton.Right });
        Act("ability_alt", K(Key.K), J(JoyButton.RightShoulder), Ax(JoyAxis.TriggerRight, 1));
        Act("interact", K(Key.E));
        Act("dodge", K(Key.Shift), K(Key.L), J(JoyButton.B), J(JoyButton.LeftShoulder), Ax(JoyAxis.TriggerLeft, 1));
        Act("pause", K(Key.Escape), J(JoyButton.Start));
        Act("confirm", K(Key.Enter), K(Key.KpEnter), J(JoyButton.A));
        Act("restart", K(Key.R), J(JoyButton.Y));
        Act("pick_1", K(Key.Key1));
        Act("pick_2", K(Key.Key2));
        Act("pick_3", K(Key.Key3));
        Act("pick_4", K(Key.Key4));
        Act("potion", K(Key.Q), J(JoyButton.Y));
        Act("meta", K(Key.U), J(JoyButton.Back));
        Act("skip", K(Key.X), J(JoyButton.X));
    }

    // ------------------------------------------------------------------ level

    private void BuildLevel(int seed, bool freshPlayer)
    {
        PlayerStats keepStats = null; float keepHp = 0, keepAlimus = 0; int keepLevel = 1, keepXp = 0, keepKills = 0, keepPotions = 1, keepMilestones = 0;
        if (!freshPlayer && G.Player != null)
        {
            keepStats = G.Player.Stats; keepHp = G.Player.Hp; keepLevel = G.Player.Level; keepXp = G.Player.Xp; keepKills = G.Player.Kills; keepPotions = G.Player.Potions;
            keepMilestones = G.Player.PendingMilestones;
            keepAlimus = G.Player.Alimus;
        }
        foreach (var c in _world.GetChildren()) { _world.RemoveChild(c); c.QueueFree(); }
        G.Enemies.Clear();
        EnemyProjectiles.Clear();
        Breakables.All.Clear();
        _roomElites.Clear();
        ActiveBoss = null;
        SkipBank = 0;
        _guardianDown = false;
        _victoryT = -1;
        ExitSpots.Clear();

        ulong t0 = Time.GetTicksMsec();
        var biome = G.Biome ??= Biomes.Get(BiomeId.Entrance);
        var cave = CaveGenerator.Generate(biome, seed);
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
        _world.AddChild(player);
        if (keepStats != null)
        {
            player.Hp = Math.Min(keepStats.MaxHp, keepHp + keepStats.MaxHp * 0.3f);
            player.Level = keepLevel; player.Xp = keepXp; player.Kills = keepKills; player.Potions = keepPotions;
            player.PendingMilestones = keepMilestones;
            player.SetAlimus(Math.Max(keepAlimus, Tune.Vitalist.AlimusStart * 0.5f));
            player.SyncCharges();
        }
        player.GlobalPosition = cave.StartPos;
        if (_startAt == "boss" && cave.Boss != null)
        {
            var at = cave.Boss.Center + new Vector2(-cave.Boss.RxPx * 0.55f, 0);
            if (cave.IsSolid(at)) at = cave.Boss.Center;
            player.GlobalPosition = at;
        }
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

        // Treasure chests are visible from the start (a few of the treasure rooms hold one).
        int roomChests = 0;
        foreach (var room in cave.Rooms.Where(r => r.Kind == RoomKind.Treasure).OrderBy(_ => G.Rng.Next()))
        {
            if (roomChests >= biome.RoomChests || !G.Chance(Tune.Drops.TreasureRoomChestChance)) continue;
            roomChests++;
            // Sit the chest on real ground (the room's floor line may have been cut by another tunnel).
            if (!cave.FindFloor(room.Center, 700, out var floor)) continue;
            _world.AddChild(new Chest { Position = floor });
        }

        PlaceCaches(cave);
        SpawnCritters(cave);
        if (cave.Liquid == Liquid.Water) PlaceAirVents(cave);
        PlaceHazards(cave);
        _hud.ResetMap(cave);
        _hud.ShowBanner(G.Depth == 0 ? biome.Name.ToUpperInvariant() : $"DEPTH {G.Depth}  ·  {biome.Name.ToUpperInvariant()}", 3f);
        _spawnT = 0;
    }

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
    /// Extra chests away from the dead ends: some on the flooded floor (where movement upgrades are
    /// likeliest) and some high in the dry caves (survival upgrades), spread apart and reachable.
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
        void Scatter(int count, float yMin, float yMax, bool underwater)
        {
            int made = 0;
            for (int tries = 0; tries < 2000 && made < count; tries++)
            {
                var at = new Vector2(G.Range(64, cave.SizePx.X - 64), G.Range(yMin, yMax));
                if (cave.IsSolid(at) || cave.IsWater(at) != underwater) continue;
                if (!cave.FindFloor(at, 300, out var floor) || cave.IsWater(floor + new Vector2(0, -10)) != underwater) continue;
                if (!underwater && floor.Y > yMax) continue;
                if (!Reachable(floor) || placed.Any(q => q.DistanceTo(floor) < 350)) continue;
                placed.Add(floor);
                _world.AddChild(new Chest { Position = floor });
                made++;
            }
        }
        var bd = cave.Biome;
        if (cave.Liquid == Liquid.Water) Scatter(bd?.WaterCaches ?? Tune.Drops.WaterCaches, cave.WaterY + 40, cave.SizePx.Y - 40, true);
        float dryBottom = Math.Min(cave.WaterY, cave.SizePx.Y);
        Scatter(bd?.HighCaches ?? Tune.Drops.HighCaches, 60, dryBottom * Tune.Drops.HighZoneFraction, false);
        if (_autotest) GD.Print($"[autotest] caches placed: {placed.Count - cave.Rooms.Count}");
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
    public void EnterExit(BiomeDef to, int depth) => CallDeferred(MethodName.GoDeeper, (int)(to?.Id ?? BiomeId.Slime), depth);

    private void GoDeeper(int biome, int depth)
    {
        if (Brains.Training) Brains.SaveAll();
        G.Depth = depth;
        G.Biome = Biomes.Get((BiomeId)biome);
        Meta.BestDepth = Math.Max(Meta.BestDepth, G.Depth);
        _seed = _rng.Next(1, 999999);
        BuildLevel(_seed, freshPlayer: false);
        if (_bot != null) { G.Player.InputOverride = _bot.Read; _bot.Reset(); }
        _sfx.SetMusic("ambient");
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
        Meta.Runs++;
        _sfx.SetMusic("ambient");
    }

    private const int HeroCount = 3;

    private void PickHero(HeroKind h)
    {
        if (G.Hero == h) return;
        G.Hero = h;
        _sfx.Play("ui", null, -6);
        _overlay.QueueRedraw();
    }

    private void Restart()
    {
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
        var p = G.Player;
        int secs = (int)_runTime;
        _overlay.HeroCards = true;
        string earned = _runEmbers > 0 || _runFinds != "" ? $"Earned: {_runEmbers} ember{(_runEmbers == 1 ? "" : "s")}{_runFinds}" : "";
        _overlay.Show(_victory ? "VICTORY" : "YOU DIED", 0.6f,
            _victory ? "The Elder Dragon is slain. The deep is quiet... for now." : $"Fell at depth {G.Depth} in the {G.Biome?.Name ?? "cave"}",
            $"Level {p.Level}   ·   {p.Kills} kills   ·   {secs / 60}:{secs % 60:00}",
            earned,
            "@",
            CampLine(),
            UsingPad ? "!LEFT / RIGHT to switch hero  -  Y or A to descend again" : "!LEFT / RIGHT to switch hero  -  R or ENTER to descend again");
    }

    private void OnMetaClosed()
    {
        if (_state == State.Dead) ShowCamp();
        else if (_state == State.Title) ShowTitle();
    }

    /// <summary>A chest was opened at <paramref name="at"/>: offer its upgrades next.</summary>
    public void OfferChest(Vector2 at) => _pendingTreasure.Enqueue(at);

    private void TryOpenUpgradeMenu()
    {
        var p = G.Player;
        if (p == null || p.Dead || _upgradeMenu.Visible) return;
        // chests hand out the real upgrades; level-ups a small stat of your choice
        List<Upgrade> choices;
        bool treasure;
        if (_pendingTreasure.Count > 0) { treasure = true; choices = Upgrades.RollChest(p.Stats, _rng, _pendingTreasure.Dequeue()); }
        else if (p.PendingMilestones > 0) { treasure = false; p.PendingMilestones--; choices = Upgrades.RollMilestone(p.Stats, _rng); }
        else return;
        if (choices.Count == 0) { p.Heal(30); return; }
        if (!_guardianDown) choices.Add(Upgrades.Skip); // (once the guardian is down, a skip would pay nothing)
        _state = State.Choosing;
        GetTree().Paused = true;
        _sfx.Play(treasure ? "chest" : "levelup");
        _upgradeMenu.Open(choices, treasure ? "TREASURE!" : "MILESTONE!");
        _autoPickT = 0.5f;
    }

    private void OnUpgradePicked(Upgrade u)
    {
        if (u == Upgrades.Skip) { SkipBank++; _sfx.Play("ui", null, 0, 0, 0.7f); _hud.ShowBanner("LEFT BEHIND  ·  +1 ember if the guardian falls", 1.8f); }
        else
        {
            Upgrades.Apply(u, G.Player.Stats, G.Player);
            _sfx.Play("ui");
            _hud.ShowBanner(u.Name, 1.6f);
        }
        GetTree().Paused = false;
        _state = State.Playing;
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
    public void Kick(Vector2 offset) => _kick += offset;

    /// <summary>Controller rumble, only while a controller is the active device.</summary>
    public void Rumble(float weak, float strong, float seconds)
    {
        if (!UsingPad) return;
        foreach (int id in Input.GetConnectedJoypads()) Input.StartJoyVibration(id, weak, strong, seconds);
    }

    /// <summary>True when the last input came from a controller (drives prompts and hides the mouse).</summary>
    public bool UsingPad;
    private Vector2 _kick;

    public override void _Input(InputEvent e)
    {
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
        if (_metaMenu.Visible) return;
        if ((_state == State.Title || (_state == State.Dead && _overlay.Visible)) && (e.IsActionPressed("move_left") || e.IsActionPressed("move_right")))
        {
            int step = e.IsActionPressed("move_left") ? -1 : 1;
            PickHero((HeroKind)(((int)G.Hero + step + HeroCount) % HeroCount));
            GetViewport().SetInputAsHandled();
            return;
        }
        if (_state == State.Title && (e.IsActionPressed("confirm") || (e is InputEventMouseButton mb && mb.Pressed)))
        {
            // a click on a hero card picks that hero before starting
            if (e is InputEventMouseButton click) { int card = _overlay.CardAt(click.Position); if (card >= 0) PickHero((HeroKind)card); }
            // the level was built for the hero shown when the game launched: rebuild if it changed
            if (G.Player != null && G.Player.Stats.Hero != G.Hero) BuildLevel(_seed, freshPlayer: true);
            StartPlaying();
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
        switch (_state)
        {
            case State.Title:
                _titleT += dt;
                if (_metaShot != "") MetaShotTick();
                if (!_metaMenu.Visible && Input.IsActionJustPressed("meta") && Meta.Trees.Any(Meta.Visible)) _metaMenu.Open(MetaMenu.Mode.Browse);
                if (_titleShot != "" && _titleT > 1.5f)
                {
                    GetViewport().GetTexture().GetImage().SavePng(_titleShot);
                    SafeQuit.Request(this);
                }
                return;
            case State.Paused:
                if (Input.IsActionJustPressed("pause")) { _overlay.Visible = false; GetTree().Paused = false; _state = State.Playing; }
                return;
            case State.Dead:
                _deadT += dt;
                if (_metaMenu.Visible) break;
                if (_deadT > 1.2f && !_overlay.Visible)
                {
                    ShowCamp();
                    // back at camp after the first reagent: the potion tree's introduction
                    if (!_autotest && Meta.Visible(Meta.PotionTree) && !Meta.PotionTutorialDone) _metaMenu.Open(MetaMenu.Mode.PotionTutorial);
                    else if (!_autotest && Meta.Visible(Meta.PearlTree) && !Meta.PearlTutorialDone) _metaMenu.Open(MetaMenu.Mode.PearlIntro);
                }
                if (_deadT > 1.5f && _overlay.Visible && Input.IsActionJustPressed("meta") && Meta.Trees.Any(Meta.Visible)) _metaMenu.Open(MetaMenu.Mode.Browse);
                else if (_deadT > 1.5f && _overlay.Visible && (Input.IsActionJustPressed("restart") || Input.IsActionJustPressed("confirm"))) Restart();
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
                    if (_autoPickT <= 0) _upgradeMenu.Choose(0);
                }
                return;
            case State.Playing:
                if (Input.IsActionJustPressed("pause"))
                {
                    _state = State.Paused;
                    GetTree().Paused = true;
                    _overlay.HeroCards = false;
                    _overlay.Show("PAUSED", 0.5f, "", UsingPad ? "!Press START to resume" : "!Press ESC to resume");
                    return;
                }
                _runTime += dt;
                G.RunTime = _runTime;
                if (_victoryT > 0)
                {
                    _victoryT -= dt;
                    if (_victoryT <= 0) { _state = State.Dead; _deadT = 0; _sfx.SetMusic(""); Meta.Save(); }
                }
                Brains.Tick(unscaled);
                TryOpenUpgradeMenu();
                break;
        }

        UpdateCamera(dt);
        var player = G.Player;
        if (player != null && _state == State.Playing)
        {
            _sfx.SetUnderwater(player.HeadUnder);
            _spawnT -= dt;
            if (_spawnT <= 0) { _spawnT = 0.25f; RunSpawner(0.25f); RunRooms(); }
            if (ActiveBoss != null && (ActiveBoss.Dead || !IsInstanceValid(ActiveBoss))) ActiveBoss = null;
        }
        if (_showcase) ShowcaseTick(dt);
        if (_lookShot != "") LookShotTick();
        if (_autotest) AutotestTick(dt);
        if (_bestiary) BestiaryTick(dt);
        if (_animTest) AnimTestTick(dt);
        if (_heroTest) HeroTestTick(dt);
        if (_hitStopTest) HitStopTestTick();
    }

    private void UpdateCamera(float dt)
    {
        var p = G.Player;
        if (p == null || _cam == null) return;
        var target = p.GlobalPosition + new Vector2(p.Velocity.X * 0.15f, p.Velocity.Y * 0.08f - 10);
        if (ActiveBoss != null && IsInstanceValid(ActiveBoss) && !ActiveBoss.Dead) target = target.Lerp(ActiveBoss.GlobalPosition, 0.25f);
        _cam.GlobalPosition = _cam.GlobalPosition.Lerp(target, 1 - MathF.Exp(-dt * Tune.Feel.CameraFollowSharpness));
        float s = _fx?.Shake ?? 0;
        _kick = _kick.Lerp(Vector2.Zero, 1 - MathF.Exp(-dt * 14));
        // hold the frame perfectly still during a hit-stop; the shake plays out once time resumes
        if (_hitStopLeft <= 0)
            _cam.Offset = (s > 0 ? new Vector2(G.Range(-s, s), G.Range(-s, s)) * 0.5f : Vector2.Zero) + _kick;
    }

    // ------------------------------------------------------------------ spawning

    private float _waveT = Tune.Spawning.FirstWave;
    private readonly List<(Enemy e, float d0, float t0)> _entrants = new();

    /// <summary>True if a world point is inside the camera's view (plus a margin).</summary>
    private bool OnScreen(Vector2 p, float margin = 40f)
    {
        var half = GetViewport().GetVisibleRect().Size / _cam.Zoom * 0.5f + new Vector2(margin, margin);
        var c = _cam.GetScreenCenterPosition();
        return Math.Abs(p.X - c.X) < half.X && Math.Abs(p.Y - c.Y) < half.Y;
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
        var p = G.Player;
        if (p.Dead) return;
        // only enemies in the neighbourhood count toward the cap (far-off residents are asleep)
        var biome = G.Biome;
        int alive = G.Enemies.Count(e => !e.Dead && e.GlobalPosition.DistanceSquaredTo(p.GlobalPosition) < 900 * 900);
        int cap = (int)((Tune.Spawning.CapBase + G.Pace * Tune.Spawning.CapPerPace) * Math.Max(0.5f, biome.Density));
        // Residents: how many spawn points actually hold a group rises from ~40% to 100% over the run
        // (sooner the deeper you are); sparse biomes stay sparse.
        float fill = biome.ResidentFill >= 0 ? biome.ResidentFill
            : Math.Min(1f, Tune.Spawning.ResidentFillStart + G.Depth * 0.08f + G.RunTime / (Tune.Spawning.ResidentFillMinutes * 60f) * (1f - Tune.Spawning.ResidentFillStart)) * Math.Min(1f, biome.Density);
        foreach (var sp in cave.Spawns)
        {
            float d = sp.Pos.DistanceTo(p.GlobalPosition);
            if (sp.Used)
            {
                sp.Cooldown -= dt;
                if (sp.Cooldown <= 0 && d > 1000) sp.Used = false;
                continue;
            }
            if (d > Tune.Spawning.ResidentMaxDistance || alive >= cap || OnScreen(sp.Pos)) continue;
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
            _waveT = Math.Max(Tune.Spawning.IntervalMin, Tune.Spawning.IntervalStart / rate) * G.Range(0.8f, 1.2f);
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
        while (q.Count > 0)
        {
            var c = q.Dequeue();
            int d = dist[c.Y * W + c.X];
            var w = new Vector2(c.X + 0.5f, c.Y + 0.5f) * CaveData.Cell;
            if (!OnScreen(w, 24))
            {
                if (OnScreen(w, 24 + edge)) band.Add(w);
                else farther.Add(w);
            }
            if (d >= Tune.Spawning.EntranceMaxCells) continue;
            foreach (var o in new[] { new Vector2I(1, 0), new Vector2I(-1, 0), new Vector2I(0, 1), new Vector2I(0, -1) })
            {
                var n = c + o;
                if (n.X < 0 || n.Y < 0 || n.X >= W || n.Y >= H || dist.ContainsKey(n.Y * W + n.X) || !cave.CellOpen(n.X, n.Y)) continue;
                dist[n.Y * W + n.X] = d + 1;
                q.Enqueue(n);
            }
        }
        var pool = band.Count > 0 ? band : farther;
        if (pool.Count == 0) return;

        // sort the candidates into 8 directions around the view and take each newcomer from a
        // different direction (cycling if the wave is bigger than the directions available)
        var center = _cam.GetScreenCenterPosition();
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
        var p = G.Player;
        foreach (var room in cave.Rooms)
        {
            if (room.Triggered || room.Kind == RoomKind.Start) continue;
            float d = room.Center.DistanceTo(p.GlobalPosition);
            float trigger = room.Kind == RoomKind.Boss ? room.RxPx * 0.75f : Math.Max(room.RxPx, room.RyPx) + 60;
            if (d > trigger) continue;
            if (room.Kind == RoomKind.Boss && !cave.LineClear(room.Center, p.GlobalPosition)) continue;
            room.Triggered = true;
            switch (room.Kind)
            {
                case RoomKind.Boss:
                {
                    var b = G.Biome;
                    var boss = b.Guardian(room);
                    float side = Math.Sign(room.Center.X - p.GlobalPosition.X);
                    boss.Position = boss is Dragon
                        ? new Vector2(room.Center.X, room.Center.Y - room.RyPx * 0.5f)
                        : room.Floor + new Vector2(room.RxPx * 0.25f * side, -Math.Min(room.RyPx * 0.7f, 90));
                    if (cave.IsSolid(boss.Position)) boss.Position = room.Center;
                    boss.OnDeath = e => OnGuardianKilled(room, e);
                    boss.Wake();
                    _world.AddChild(boss);
                    ActiveBoss = boss;
                    _hud.ShowBanner(boss.Title != "" ? boss.Title : boss.DisplayName.ToUpperInvariant(), 3f);
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
                    };
                    _world.AddChild(elite);
                    _hud.ShowBanner(elite.DisplayName.ToUpperInvariant(), 2f);
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

    private void SpawnChest(Vector2 at) => _world.AddChild(new Chest { Position = at });

    private bool _guardianDown;
    private float _victoryT = -1;
    /// <summary>Where the exits are (for the autopilot).</summary>
    public readonly List<Vector2> ExitSpots = new();

    /// <summary>
    /// The level's guardian is dead: pay out the embers (its own, plus one per reward skipped on
    /// this level), roll a resource, drop a chest and open the exits. The dragon ends the run.
    /// </summary>
    private void OnGuardianKilled(Room room, Enemy boss)
    {
        _guardianDown = true;
        _sfx.SetMusic("ambient");
        G.Fx.AddShake(14);
        G.Fx.ScreenFlash(new Color(1f, 0.9f, 0.6f), 0.3f);
        bool dragon = boss is Dragon;
        int embers = dragon ? 5 : G.Depth >= 5 ? 2 : 1;
        int skipped = SkipBank;
        embers += skipped;
        SkipBank = 0;
        Meta.AddEmbers(embers);
        _runEmbers += embers;
        var found = Meta.RollResource(_rng);
        if (_autotest) GD.Print($"[autotest] guardian {boss.DisplayName} killed at depth {G.Depth} ({G.Biome.Name}) after {_runTime:0}s, level {G.Player.Level}");
        string name = boss.Title != "" ? boss.Title : boss.DisplayName.ToUpperInvariant();
        _hud.ShowBanner($"{name} SLAIN  ·  +{embers} EMBER{(embers > 1 ? "S" : "")}", 3.5f);
        var at = boss.GlobalPosition;
        G.Fx.Text(at + new Vector2(0, -40), $"+{embers} ember{(embers > 1 ? "s" : "")}" + (skipped > 0 ? $" ({skipped} for rewards left behind)" : ""), new Color(1f, 0.7f, 0.35f), 13, 2.5f);
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
            _victoryT = 5f;
            _hud.ShowBanner("THE ELDER DRAGON IS SLAIN", 5f);
            return;
        }
        CallDeferred(MethodName.SpawnChest, room.Floor + new Vector2(0, 0));
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

    private void SpawnPortal(Vector2 at, int biome, int depth, string label)
        => _world.AddChild(new Portal { Position = at, To = Biomes.Get((BiomeId)biome), Depth = depth, Label = label });

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
            new Rat(), new Bear(), new Scorpion(), new Hornet(), new Skeleton(), new Sporeling(), new FrostWraith(), new Shardling() };
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

    /// <summary>Feeds synthetic joypad events through Godot's input pipeline and checks the game reacts.</summary>
    private void PadTestTick(float dt)
    {
        _padT += dt;
        void Btn(JoyButton b, bool down) => Input.ParseInputEvent(new InputEventJoypadButton { ButtonIndex = b, Pressed = down, Device = 0 });
        void Axis(JoyAxis a, float v) => Input.ParseInputEvent(new InputEventJoypadMotion { Axis = a, AxisValue = v, Device = 0 });
        var steps = new (float at, Action act, string label)[]
        {
            (0.5f, () => { Btn(JoyButton.A, true); }, "A on title"),
            (0.6f, () => { Btn(JoyButton.A, false); GD.Print($"[padtest] state after A: {_state}, usingPad {UsingPad}, mouse {Input.MouseMode}"); }, ""),
            (1.0f, () => Axis(JoyAxis.LeftX, 1f), "stick right"),
            (1.6f, () => { GD.Print($"[padtest] player vx after stick: {G.Player.Velocity.X:0}"); Axis(JoyAxis.LeftX, 0f); }, ""),
            (1.8f, () => Btn(JoyButton.X, true), "X swing"),
            (1.85f, () => { Btn(JoyButton.X, false); GD.Print($"[padtest] anim after X: {G.Player.Anim.Current}"); }, ""),
            (2.3f, () => Axis(JoyAxis.TriggerRight, 1f), "RT ability"),
            (2.35f, () => { Axis(JoyAxis.TriggerRight, 0f); GD.Print($"[padtest] charged strike after RT: {G.Player.Charged} (cooldown {G.Player.ChargeCooldownFrac:0.00})"); }, ""),
            (2.6f, () => Btn(JoyButton.B, true), "B dodge"),
            (2.65f, () => { Btn(JoyButton.B, false); GD.Print($"[padtest] dodging after B: {G.Player.IsDodging}"); }, ""),
            (3.0f, () => { G.Player.PendingMilestones = 1; }, "milestone"),
            (3.6f, () => { GD.Print($"[padtest] state: {_state}"); Btn(JoyButton.DpadRight, true); }, "dpad right"),
            (3.65f, () => Btn(JoyButton.DpadRight, false), ""),
            (3.8f, () => Btn(JoyButton.A, true), "A pick"),
            (3.85f, () => { Btn(JoyButton.A, false); GD.Print($"[padtest] after pick: state {_state}, upgrades [{string.Join(",", G.Player.Stats.Stacks.Keys)}]"); }, ""),
            (4.2f, () => Btn(JoyButton.Start, true), "start pause"),
            (4.25f, () => { Btn(JoyButton.Start, false); GD.Print($"[padtest] after start: {_state}"); }, ""),
            (4.5f, () => Btn(JoyButton.Start, true), ""),
            (4.55f, () => { Btn(JoyButton.Start, false); GD.Print($"[padtest] after start again: {_state}"); SafeQuit.Request(this); }, ""),
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
            if (warden) WardenStep(step, p); else if (p.Stats.Hero == HeroKind.Vitalist) VitalistStep(step, p); else SwordStep(step, p);
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
                // 70% stopped (costing the shield half of that), 30% through, less armour
                float through = 6f * (1f - p.Stats.BlockShare) * (1f - p.Stats.DamageReduction);
                float cost = 6f * p.Stats.BlockShare * Tune.Warden.ShieldCost;
                Check($"the shield stops most of a shot from the front (hp {_hpMark:0.00} -> {p.Hp:0.00}, want -{through:0.00}; shield {_shieldMark:0.0} -> {p.ShieldHp:0.0}, want -{cost:0.0})",
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
                Check($"healing mends a broken shield at once (broken {p.ShieldBroken}, shield {_shieldMark:0.0} -> {p.ShieldHp:0.0})", !p.ShieldBroken && Math.Abs(p.ShieldHp - _shieldMark - 5f) < 0.3f);
                _heroInput = default;
                p.RefillShield();
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
                Check($"an ordinary block lets 30% through and leaves the attacker be (through {b.Through:0.0}, reeling {gob2.Reeling})", !b.Perfect && Math.Abs(b.Through - 10f * (1f - p.Stats.BlockShare)) < 0.01f && !gob2.Reeling);
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

            // ---- the shield dash
            case 180:
                _hpMark = p.Hp;
                _probe = Shoot(new Vector2(_dir * 110, -4));
                _heroInput = new PlayerInput { Ability = true, Aim = new Vector2(_dir, 0) };
                break;
            case 181: _heroInput = default; break;
            case 186:
                // (with Riposte Guard, taken above, it goes back where it came from instead)
                Check($"the shield dash swallows a projectile (gone {!IsInstanceValid(_probe)}, reflected {IsInstanceValid(_probe) && _probe.Reflected}, hp {_hpMark:0.0} -> {p.Hp:0.0}, still dashing {p.IsShieldDashing})",
                    (!IsInstanceValid(_probe) || _probe.Reflected) && p.Hp >= _hpMark - 0.01f && !p.IsShieldDashing);
                var golem = new Golem { Position = p.GlobalPosition + new Vector2(_dir * 70, -6) };
                golem.SetMeta("test", true);
                _world.AddChild(golem);
                golem.Wake();
                _probeEnemy = golem;
                break;
            case 187: _posMark = p.GlobalPosition; p.ResetAbilityCooldowns(); break;
            case 290:
                Check("the golem attacked (for the dash to meet)", _dashedAt > 0);
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
                Finish();
                break;
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
    private int _dashedAt = -1;

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
        else if (_comboPressedAt > 0 && s == _comboPressedAt + 1) _heroInput = default;
        else if (_comboPressedAt > 0 && s == _comboPressedAt + 4)
            Check($"a swing pressed during the hit-stop starts as it ends (clip {p.Anim.Current})", p.Anim.Current.StartsWith("slash_b"));
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
                Check($"a charged swing hits much harder ({dealt:0} vs {_normalHit:0}) and is spent (charged {p.Charged})", dealt > _normalHit * 1.3f && p.Charged == 0);
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
            case 70:
                // Crescent Wave: a swing from well out of reach still cuts the golem
                Upgrades.Apply(Upgrades.Get("wave"), p.Stats, p);
                _probeEnemy.GlobalPosition = p.GlobalPosition + new Vector2(_dir * 120, -6);
                break;
            case 76:
                _hpMark = _probeEnemy.Hp;
                _heroInput = new PlayerInput { Attack = true, Aim = new Vector2(_dir, 0) };
                break;
            case 77: _heroInput = default; break;
            case 84:
                Check($"crescent wave hits at 120 px (golem hp {_probeEnemy.Hp:0} < {_hpMark:0})", _probeEnemy.Hp < _hpMark);
                _probeEnemy.QueueFree();
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
            case 87:
                Check($"an air bubble refills breath ({p.Breath:0.0} s)", p.Breath > 2.2f);
                Finish();
                break;
        }
    }
    private float _normalHit;
    private int _comboPressedAt = -1;

    private void VitalistStep(int s, Player p)
    {
        switch (s)
        {
            case 5:
            {
                var dummy = new Golem { Position = p.GlobalPosition + new Vector2(_dir * 110, -6) };
                dummy.SetMeta("test", true);
                _world.AddChild(dummy);
                _probeEnemy = dummy;
                p.SetAlimus(20);
                break;
            }
            case 8:
                _hpMark = _probeEnemy.Hp;
                _shieldMark = p.Alimus;
                _heroInput = new PlayerInput { Attack = true, Aim = new Vector2(_dir, 0.2f).Normalized() };
                break;
            case 9: _heroInput = default; break;
            case 16:
            {
                float dealt = _hpMark - _probeEnemy.Hp;
                Check($"a drain bolt strikes a golem 110 px away (hp {_hpMark:0} -> {_probeEnemy.Hp:0})", dealt > 0);
                Check($"and a tenth of the damage comes back as alimus ({_shieldMark:0.0} -> {p.Alimus:0.0}, want +{dealt * 0.1f:0.0})", Math.Abs(p.Alimus - _shieldMark - dealt * 0.1f) < 0.05f);
                _normalHit = dealt;
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
                Check($"a hexed creature takes more damage ({dealt:0.0} vs {_normalHit:0.0})", dealt > _normalHit * 1.08f);
                _probeEnemy.QueueFree();
                // the heal: everything to the one hurt player in range
                p.Hp = 20;
                p.SetAlimus(50);
                _hpMark = p.Hp;
                _heroInput = new PlayerInput { Ability = true };
                break;
            }
            case 29: _heroInput = default; break;
            case 31:
            {
                float want = Tune.Vitalist.HealAmount * p.Stats.HealMult;
                Check($"the heal restores {want:0} (hp {_hpMark:0} -> {p.Hp:0}) for {p.HealCost:0} alimus (50 -> {p.Alimus:0})", Math.Abs(p.Hp - _hpMark - want) < 0.5f && Math.Abs(50 - p.Alimus - p.HealCost) < 0.01f);
                break;
            }
            case 40:
                p.Hp = p.Stats.MaxHp;
                _shieldMark = p.Alimus;
                _heroInput = new PlayerInput { Ability = true };
                break;
            case 41: _heroInput = default; break;
            case 43:
                Check($"no heal (and no alimus spent) when no one is hurt ({_shieldMark:0} -> {p.Alimus:0})", Math.Abs(p.Alimus - _shieldMark) < 0.01f);
                Finish();
                break;
        }
    }

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
        int n = 12, cleanAll = 0, totalAll = 0;
        ulong total = 0;
        foreach (var b in Biomes.All)
        {
            if (_biomeArg != null && !b.Id.ToString().Equals(_biomeArg, StringComparison.OrdinalIgnoreCase)) continue;
            G.Biome = b;
            int clean = 0;
            for (int s = 1; s <= n; s++)
            {
                ulong t0 = Time.GetTicksMsec();
                var c = CaveGenerator.Generate(b, s * 1013);
                ulong ms = Time.GetTicksMsec() - t0;
                total += ms;
                bool ok = c.TrapCells <= 6 && c.Boss != null && BossReachable(c);
                if (ok) clean++;
                if (!ok || s == 1)
                    GD.Print($"  {b.Id,-9} seed {s * 1013}: {ms} ms attempts {c.Attempts} traps {c.TrapCells} reachable {c.ReachableCells} rooms {c.Rooms.Count} minis {c.Rooms.Count(r => r.Kind == RoomKind.MiniBoss)} boss {(c.Boss != null)} bossReach {BossReachable(c)} spawns {c.Spawns.Count} ice {c.IceLedges.Count}");
                if (s == 1) SaveCaveImage(c, $"user://cave_{b.Id}.png");
            }
            GD.Print($"[gentest] {b.Id}: {clean}/{n} trap-free with a reachable exit  ->  {ProjectSettings.GlobalizePath($"user://cave_{b.Id}.png")}");
            cleanAll += clean; totalAll += n;
        }
        GD.Print($"[gentest] {cleanAll}/{totalAll} trap-free, avg {total / (ulong)Math.Max(1, totalAll)} ms");
        SafeQuit.Request(this);
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
        Check($"milestones every 5 levels ({Meta.MilestoneEvery})", Meta.MilestoneEvery == 5);
        Check($"+5% experience ({Meta.XpMult})", Math.Abs(Meta.XpMult - 1.05f) < 1e-4f);
        GD.Print(ok ? "[metatest] PASS" : "[metatest] FAIL");
        SafeQuit.Request(this, ok ? 0 : 1);
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
