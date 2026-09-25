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

    private Node2D _world;
    private Camera2D _cam;
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
    private bool _nnTest;

    public override void _Ready()
    {
        G.Main = this;
        ProcessMode = ProcessModeEnum.Always;
        SetupInput();
        var win = GetWindow();
        win.ContentScaleSize = new Vector2I(1280, 720);
        win.ContentScaleMode = Window.ContentScaleModeEnum.CanvasItems;
        win.ContentScaleAspect = Window.ContentScaleAspectEnum.Expand;
        win.Title = "Dagger Deep";
        RenderingServer.SetDefaultClearColor(Colors.Black);

        _sfx = new SoundBank();
        AddChild(_sfx);
        G.Sfx = _sfx;

        _world = new Node2D { Name = "World", ProcessMode = ProcessModeEnum.Pausable };
        AddChild(_world);
        G.World = _world;

        _darkLayer = new CanvasLayer { Layer = 5 };
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

        ParseArgs(out bool gentest);
        try { Begin(gentest); }
        catch (Exception ex)
        {
            GD.PrintErr(ex.ToString());
            if (_autotest || gentest) GetTree().Quit(1);
            else throw;
        }
    }

    private void Begin(bool gentest)
    {
        if (gentest) { RunGenTest(); return; }
        if (_nnTest) { RunNnTest(); return; }

        _seed = _seed != 0 ? _seed : (int)(Time.GetUnixTimeFromSystem() * 1000 % 1000000);
        if (_autotest) G.Rng = new Random(_seed);
        G.Depth = 1;
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
            G.Player.InputOverride = () => _heroInput;
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
        else if (_autotest)
        {
            StartPlaying();
            _bot = new BotPilot();
            G.Player.InputOverride = _bot.Read;
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
            "A rogue-lite descent through flooded caverns.  Choose your hero:",
            "@",
            "KEYBOARD + MOUSE:  A / D move   SPACE jump   W / S swim   LEFT CLICK swing   RIGHT CLICK throw / barrier   SHIFT dodge / shield",
            "CONTROLLER:  stick move   A jump   X swing   RB / RT throw / barrier   B / LB dodge / shield   right stick aims",
            "!LEFT / RIGHT to choose  -  ENTER / A to begin");
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
            else if (a == "--padtest") _padTest = true;
            else if (a == "--showcase") { _showcase = true; _autotest = true; }
            else if (a == "--train") Brains.Training = true;
            else if (a == "--hero=warden") G.Hero = HeroKind.Warden;
            else if (a == "--hero=swordsman") G.Hero = HeroKind.Swordsman;
            else if (a.StartsWith("--braindir=")) Brains.DirOverride = a[11..];
            else if (a == "--nntest") _nnTest = true;
        }
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
        Act("throw", new InputEventMouseButton { ButtonIndex = MouseButton.Right });
        Act("throw_alt", K(Key.K), J(JoyButton.RightShoulder), Ax(JoyAxis.TriggerRight, 1));
        Act("dodge", K(Key.Shift), K(Key.L), J(JoyButton.B), J(JoyButton.LeftShoulder), Ax(JoyAxis.TriggerLeft, 1));
        Act("pause", K(Key.Escape), J(JoyButton.Start));
        Act("confirm", K(Key.Enter), K(Key.KpEnter), J(JoyButton.A));
        Act("restart", K(Key.R), J(JoyButton.Y));
        Act("pick_1", K(Key.Key1));
        Act("pick_2", K(Key.Key2));
        Act("pick_3", K(Key.Key3));
    }

    // ------------------------------------------------------------------ level

    private void BuildLevel(int seed, bool freshPlayer)
    {
        PlayerStats keepStats = null; float keepHp = 0; int keepLevel = 1, keepXp = 0, keepKills = 0;
        if (!freshPlayer && G.Player != null)
        {
            keepStats = G.Player.Stats; keepHp = G.Player.Hp; keepLevel = G.Player.Level; keepXp = G.Player.Xp; keepKills = G.Player.Kills;
        }
        foreach (var c in _world.GetChildren()) { _world.RemoveChild(c); c.QueueFree(); }
        G.Enemies.Clear();
        EnemyProjectiles.Clear();
        _roomElites.Clear();
        ActiveBoss = null;

        ulong t0 = Time.GetTicksMsec();
        var cave = CaveGenerator.Generate(seed);
        G.Cave = cave;
        GD.Print($"[DaggerDeep] depth {G.Depth} seed {seed}: generated in {Time.GetTicksMsec() - t0} ms, attempts {cave.Attempts}, trap cells {cave.TrapCells}, reachable {cave.ReachableCells}, rooms {cave.Rooms.Count}, spawns {cave.Spawns.Count}");

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
            player.Level = keepLevel; player.Xp = keepXp; player.Kills = keepKills;
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

        // Treasure chests are visible from the start.
        foreach (var room in cave.Rooms)
        {
            if (room.Kind != RoomKind.Treasure || !G.Chance(Tune.Drops.TreasureRoomChestChance)) continue;
            // Sit the chest on real ground (the room's floor line may have been cut by another tunnel).
            if (!cave.FindFloor(room.Center, 700, out var floor)) continue;
            _world.AddChild(new Chest { Position = floor });
        }

        SpawnCritters(cave);
        _hud.ResetMap(cave);
        _hud.ShowBanner($"DEPTH {G.Depth}", 3f);
        _spawnT = 0;
    }

    /// <summary>Ambient wildlife: glow moths in dry tunnels, crabs on floors (including the sea bed).</summary>
    private void SpawnCritters(CaveData cave)
    {
        var rng = new Random(cave.Seed ^ 0x5eed);
        int moths = 0, crabs = 0;
        for (int tries = 0; tries < 9000 && (moths < Tune.Cave.Moths || crabs < Tune.Cave.Crabs); tries++)
        {
            var pos = new Vector2(rng.Next(4, cave.W - 4) + 0.5f, rng.Next(4, cave.H - 4) + 0.5f) * CaveData.Cell;
            if (cave.IsSolid(pos) || pos.DistanceTo(cave.StartPos) < 120) continue;
            if (moths < Tune.Cave.Moths && !cave.IsWater(pos) && rng.NextDouble() < 0.5)
            {
                _world.AddChild(new GlowMoth { Position = pos });
                moths++;
            }
            else if (crabs < Tune.Cave.Crabs && cave.FindFloor(pos, 160, out var fl) && !cave.IsSolid(fl + new Vector2(0, -8)))
            {
                _world.AddChild(new CaveCrab { Position = fl + new Vector2(0, -5) });
                crabs++;
            }
        }
    }

    public void NextDepth()
    {
        if (Brains.Training) Brains.SaveAll();
        G.Depth++;
        _seed = _rng.Next(1, 999999);
        BuildLevel(_seed, freshPlayer: false);
        if (_bot != null) G.Player.InputOverride = _bot.Read;
        _sfx.SetMusic("ambient");
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
        _sfx.SetMusic("ambient");
    }

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
        G.Depth = 1;
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
        _sfx.SetMusic("");
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
        else if (p.PendingLevelUps > 0) { treasure = false; p.PendingLevelUps--; choices = Upgrades.RollLevelUp(p.Stats, _rng); }
        else return;
        if (choices.Count == 0) { p.Heal(30); return; }
        _state = State.Choosing;
        GetTree().Paused = true;
        _sfx.Play(treasure ? "chest" : "levelup");
        _upgradeMenu.Open(choices, treasure ? "TREASURE!" : $"LEVEL {p.Level - p.PendingLevelUps}!");
        _autoPickT = 0.5f;
    }

    private void OnUpgradePicked(Upgrade u)
    {
        Upgrades.Apply(u, G.Player.Stats, G.Player);
        _sfx.Play("ui");
        _hud.ShowBanner(u.Name, 1.6f);
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
            if (k.PhysicalKeycode == Key.F8 && Brains.Training)
            {
                Brains.ShowLabels = !Brains.ShowLabels;
                GetViewport().SetInputAsHandled();
                return;
            }
        }
        if ((_state == State.Title || (_state == State.Dead && _overlay.Visible)) && (e.IsActionPressed("move_left") || e.IsActionPressed("move_right")))
        {
            PickHero(G.Hero == HeroKind.Swordsman ? HeroKind.Warden : HeroKind.Swordsman);
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
                if (_titleShot != "" && _titleT > 1.5f)
                {
                    GetViewport().GetTexture().GetImage().SavePng(_titleShot);
                    GetTree().Quit();
                }
                return;
            case State.Paused:
                if (Input.IsActionJustPressed("pause")) { _overlay.Visible = false; GetTree().Paused = false; _state = State.Playing; }
                return;
            case State.Dead:
                _deadT += dt;
                if (_deadT > 1.2f && !_overlay.Visible)
                {
                    var p = G.Player;
                    int secs = (int)_runTime;
                    _overlay.HeroCards = true;
                    _overlay.Show("YOU DIED", 0.6f,
                        $"Depth {G.Depth}   ·   Level {p.Level}   ·   {p.Kills} kills   ·   {secs / 60}:{secs % 60:00}",
                        "@",
                        UsingPad ? "!LEFT / RIGHT to switch hero  -  Y or A to descend again" : "!LEFT / RIGHT to switch hero  -  R or ENTER to descend again");
                }
                if (_deadT > 1.5f && (Input.IsActionJustPressed("restart") || Input.IsActionJustPressed("confirm"))) Restart();
                if (_autotest && _deadT > 3f) Restart();
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
        if (_autotest) AutotestTick(dt);
        if (_bestiary) BestiaryTick(dt);
        if (_animTest) AnimTestTick(dt);
        if (_heroTest) HeroTestTick(dt);
    }

    private void UpdateCamera(float dt)
    {
        var p = G.Player;
        if (p == null || _cam == null) return;
        var target = p.GlobalPosition + new Vector2(p.Velocity.X * 0.15f, p.Velocity.Y * 0.08f - 10);
        if (ActiveBoss != null && !ActiveBoss.Dead) target = target.Lerp(ActiveBoss.GlobalPosition, 0.25f);
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
        int alive = G.Enemies.Count(e => !e.Dead && e.GlobalPosition.DistanceSquaredTo(p.GlobalPosition) < 900 * 900);
        int cap = Tune.Spawning.CapBase + (int)(G.Pace * Tune.Spawning.CapPerPace);
        // Residents: how many spawn points actually hold a group rises from ~40% to 100% over 8 minutes.
        float fill = Math.Min(1f, Tune.Spawning.ResidentFillStart + G.RunTime / (Tune.Spawning.ResidentFillMinutes * 60f) * (1f - Tune.Spawning.ResidentFillStart));
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
        if (_waveT <= 0 && alive < cap + 4)
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
            e.Engage();
            e.Position = cave.IsSolid(pos) ? at : pos;
            _world.AddChild(e);
            if (_autotest) _entrants.Add((e, e.Position.DistanceTo(p.GlobalPosition), _runTime));
        }
    }

    private static Enemy EntrantFor(Vector2 at, out Vector2 pos)
    {
        var cave = G.Cave;
        pos = at;
        if (cave.IsWater(at)) return new Fish();
        if (!cave.FindFloor(at, 140, out var floor) || G.Chance(Tune.Spawning.EntranceBatChance)) return new Bat();
        pos = floor + new Vector2(0, -12);
        float r = G.RandF();
        return r < 0.4f ? new Frog() : r < 0.75f ? new Goblin() : r < 0.9f ? new Goblin { Slinger = true }
            : G.Chance(0.5f) ? new Golem() : new LavaMonster();
    }

    private int SpawnGroup(SpawnPoint sp)
    {
        var cave = G.Cave;
        int extra = (int)(G.Pace * 1.4f);
        int n = 0;
        void Add(Enemy e, Vector2 pos)
        {
            if (cave.IsSolid(pos)) pos = sp.Pos;
            e.Position = pos;
            _world.AddChild(e);
            n++;
        }
        switch (sp.Kind)
        {
            case SpawnKind.Ground:
            {
                float lowFactor = Math.Clamp(sp.Pos.Y / cave.WaterY, 0, 1);
                float r = G.RandF();
                float pLava = 0.08f + 0.2f * lowFactor, pGolem = 0.08f + 0.03f * extra;
                if (r < pLava) Add(new LavaMonster(), sp.Pos + new Vector2(0, -4));
                else if (r < pLava + pGolem) Add(new Golem(), sp.Pos + new Vector2(0, -8));
                else if (r < 0.6f)
                {
                    int c = G.RangeI(1, 2 + Math.Min(extra, 2));
                    for (int k = 0; k < c; k++) Add(new Goblin { Slinger = G.Chance(0.35f) }, sp.Pos + new Vector2(G.Range(-30, 30), -4));
                }
                else
                {
                    int c = G.RangeI(1, 2 + Math.Min(extra, 1));
                    for (int k = 0; k < c; k++) Add(new Frog(), sp.Pos + new Vector2(G.Range(-30, 30), -2));
                }
                break;
            }
            case SpawnKind.Ceiling:
                if (G.Chance(0.6f))
                {
                    int c = G.RangeI(2, 3 + Math.Min(extra, 2));
                    for (int k = 0; k < c; k++) Add(new Bat(), sp.Pos + new Vector2(G.Range(-40, 40), 0));
                }
                else
                {
                    int c = G.RangeI(1, 2);
                    for (int k = 0; k < c; k++)
                    {
                        var pos = sp.Pos + new Vector2(k * 40 - 20, 0);
                        if (cave.FindCeiling(pos + new Vector2(0, 20), 60, out var ce)) pos = ce + new Vector2(0, 8);
                        Add(new Spider(), pos);
                    }
                }
                break;
            case SpawnKind.Water:
            {
                int c = G.RangeI(2, 4 + Math.Min(extra, 2));
                for (int k = 0; k < c; k++) Add(new Fish(), sp.Pos + G.RandDir() * G.Range(0, 30));
                break;
            }
            case SpawnKind.WaterWall:
                Add(new Eel { WallNormal = sp.Normal }, sp.Pos);
                if (G.Chance(0.4f)) Add(new Fish(), sp.Pos + sp.Normal * 40);
                break;
            case SpawnKind.WaterFloor:
                Add(new Urchin(), sp.Pos);
                if (G.Chance(0.5f)) for (int k = 0; k < 2; k++) Add(new Fish(), sp.Pos + new Vector2(G.Range(-40, 40), -40));
                break;
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
                    var boss = new CavernColossus { Position = room.Floor + new Vector2(room.RxPx * 0.25f * Math.Sign(room.Center.X - p.GlobalPosition.X), -room.RyPx * 0.7f) };
                    boss.Init(room);
                    boss.OnDeath = e => OnBossKilled(room, e);
                    _world.AddChild(boss);
                    ActiveBoss = boss;
                    _hud.ShowBanner("THE CAVERN COLOSSUS", 3f);
                    _sfx.SetMusic("boss");
                    break;
                }
                case RoomKind.MiniBoss:
                {
                    Enemy elite = room.Underwater
                        ? (G.Chance(0.5f) ? new Eel() : new Fish())
                        : G.Pick(new Func<Enemy>[] { () => new Golem(), () => new Goblin(), () => new Frog(), () => new LavaMonster(), () => new Goblin { Slinger = true } })();
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
                    int n = G.RangeI(1, 2);
                    for (int k = 0; k < n; k++)
                    {
                        Enemy e = room.Underwater ? new Fish() : (G.Chance(0.5f) ? new Goblin() : new Frog());
                        e.Position = room.Underwater ? room.Center + G.RandDir() * 30 : room.Floor + new Vector2(G.Range(-40, 40), -14);
                        _world.AddChild(e);
                    }
                    break;
                }
            }
        }
    }

    private void SpawnChest(Vector2 at) => _world.AddChild(new Chest { Position = at });

    private void OnBossKilled(Room room, Enemy boss)
    {
        _hud.ShowBanner("COLOSSUS SLAIN", 3f);
        _sfx.SetMusic("ambient");
        G.Fx.AddShake(14);
        CallDeferred(MethodName.SpawnPortal, room.Floor + new Vector2(0, -30));
        CallDeferred(MethodName.SpawnChest, room.Floor + new Vector2(60, 0));
    }

    private void SpawnPortal(Vector2 at) => _world.AddChild(new Portal { Position = at });

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
            GetTree().Quit();
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
        Enemy[] land = { new Bat(), new Frog(), new Goblin(), new Goblin { Slinger = true }, new Spider(), new LavaMonster(), new Golem() };
        for (int k = 0; k < land.Length; k++)
        {
            var at = p + new Vector2(-150 + k * 50, -30);
            if (land[k] is Bat or Spider && G.Cave.FindCeiling(at, 200, out var ce)) at = ce + new Vector2(0, 10);
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
        if (_bestiaryT > 4.2f)
        {
            GetViewport().GetTexture().GetImage().SavePng($"{_shotDir}/bestiary_boss.png");
            GetTree().Quit();
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
                foreach (var id in new[] { "combo", "combo3", "knock", "pogo", "throw2", "atkspd" }) Upgrades.Apply(Upgrades.Get(id), p.Stats, p);
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
            (2.3f, () => Axis(JoyAxis.TriggerRight, 1f), "RT throw"),
            (2.35f, () => { Axis(JoyAxis.TriggerRight, 0f); GD.Print($"[padtest] throw cooldown after RT: {G.Player.ThrowCooldowns[0]:0.00}"); }, ""),
            (2.6f, () => Btn(JoyButton.B, true), "B dodge"),
            (2.65f, () => { Btn(JoyButton.B, false); GD.Print($"[padtest] dodging after B: {G.Player.IsDodging}"); }, ""),
            (3.0f, () => { G.Player.PendingLevelUps = 1; }, "level up"),
            (3.6f, () => { GD.Print($"[padtest] state: {_state}"); Btn(JoyButton.DpadRight, true); }, "dpad right"),
            (3.65f, () => Btn(JoyButton.DpadRight, false), ""),
            (3.8f, () => Btn(JoyButton.A, true), "A pick"),
            (3.85f, () => { Btn(JoyButton.A, false); GD.Print($"[padtest] after pick: state {_state}, upgrades [{string.Join(",", G.Player.Stats.Stacks.Keys)}]"); }, ""),
            (4.2f, () => Btn(JoyButton.Start, true), "start pause"),
            (4.25f, () => { Btn(JoyButton.Start, false); GD.Print($"[padtest] after start: {_state}"); }, ""),
            (4.5f, () => Btn(JoyButton.Start, true), ""),
            (4.55f, () => { Btn(JoyButton.Start, false); GD.Print($"[padtest] after start again: {_state}"); GetTree().Quit(); }, ""),
        };
        while (_padStep < steps.Length && _padT >= steps[_padStep].at) steps[_padStep++].act();
    }

    /// <summary>Scripted inputs that walk the player through every movement/attack transition.</summary>
    // ------------------------------------------------------------------ --herotest
    // Scripted checks of both heroes' mechanics (run once with --hero=warden, once without).
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
        // steps are keyed by time (tenths of a second); each runs once
        int s = (int)(_heroT * 10);
        while (_lastHeroStep < s)
        {
            // run every step, even if a slow frame skipped past one
            int step = ++_lastHeroStep;
            if (warden) WardenStep(step, p); else SwordStep(step, p);
            if (_shotDir != "" && (step == 12 || step == 145 || step == 163 || step == 9 || step == 10 || step == 22 || step == 23)) GetViewport().GetTexture().GetImage().SavePng($"{_shotDir}/hero_{step:000}.png");
        }
    }
    private int _lastHeroStep = -1;

    private void WardenStep(int s, Player p)
    {
        switch (s)
        {
            case 5: // raise the shield to the right, fire from the right
                _heroInput = new PlayerInput { GuardHeld = true, GuardAim = Vector2.Right };
                _hpMark = p.Hp; _shieldMark = p.ShieldHp;
                break;
            case 10: _probe = Shoot(new Vector2(120, -4)); break;
            case 18:
                Check($"shield blocks a shot from the front (hp {p.Hp:0}/{_hpMark:0}, shield {p.ShieldHp:0.0} < {_shieldMark:0.0})", p.Hp == _hpMark && p.ShieldHp < _shieldMark);
                _probe = Shoot(new Vector2(-120, -4)); // from behind
                break;
            case 26:
                Check($"a shot from behind gets through (hp {p.Hp:0} < {_hpMark:0})", p.Hp < _hpMark);
                p.Heal(100);
                break;
            case 40: // wait out the post-hit invulnerability, then break it
                for (int k = 0; k < 12; k++) { var pr = Shoot(new Vector2(110 + k * 30, -4)); pr.Damage = 8; }
                break;
            case 70:
                Check($"shield breaks when drained (shield {p.ShieldHp:0.0}, broken {p.ShieldBroken})", p.ShieldBroken && p.ShieldHp == 0);
                Check("a broken shield can't be raised", !p.ShieldRaised);
                break;
            case 95:
                Check($"still broken a few seconds later, at zero ({p.ShieldHp:0.0}, {p.ShieldBrokenLeft:0.0}s left)", p.ShieldBroken && p.ShieldHp == 0);
                break;
            case 140:
                Check($"recovers after the break time and regenerates slowly ({p.ShieldHp:0.0})", !p.ShieldBroken && p.ShieldHp > 0 && p.ShieldHp < 14);
                // perfect block + reflect
                Upgrades.Apply(Upgrades.Get("perfect_reflect"), p.Stats, p);
                p.Heal(100);
                _heroInput = default;
                break;
            case 143: _probe = Shoot(new Vector2(70, -4)); break;
            case 144: _heroInput = new PlayerInput { GuardHeld = true, GuardAim = Vector2.Right }; break; // raised ~0.2 s before impact
            case 150:
                Check($"a perfect block reflects the shot (reflected {IsInstanceValid(_probe) && _probe.Reflected})", IsInstanceValid(_probe) && _probe.Reflected);
                _heroInput = default;
                break;
            case 160: // barrier
                _heroInput = new PlayerInput { Throw = true };
                break;
            case 161:
                _heroInput = default;
                Check($"barrier is up ({p.BarrierHp:0.0})", p.BarrierHp > 0);
                _hpMark = p.Hp;
                Shoot(new Vector2(-90, -4)).Damage = 4;
                break;
            case 165: _heroInput = new PlayerInput { GuardHeld = true, GuardAim = Vector2.Right, Attack = true, Aim = Vector2.Right }; break;
            case 166:
                Check($"can swing with the shield up (swinging {p.IsSwinging}, shield {p.ShieldRaised})", p.IsSwinging && p.ShieldRaised);
                _heroInput = default;
                break;
            case 170:
                Check($"barrier soaks a small hit (hp {p.Hp:0}/{_hpMark:0}, barrier {p.BarrierHp:0.0})", p.Hp == _hpMark);
                Finish();
                break;
        }
    }

    private void SwordStep(int s, Player p)
    {
        switch (s)
        {
            case 5:
            {
                // a dummy 50 px away: out of the old dagger's reach, inside the sword's
                var dummy = new Golem { Position = p.GlobalPosition + new Vector2(50, -6) };
                dummy.SetMeta("test", true);
                _world.AddChild(dummy);
                _probeEnemy = dummy;
                _posMark = p.GlobalPosition;
                break;
            }
            case 8:
                _hpMark = _probeEnemy.Hp;
                _posMark = p.GlobalPosition;
                _heroInput = new PlayerInput { Attack = true, Aim = Vector2.Right };
                break;
            case 9: _heroInput = default; break;
            case 14:
                Check($"sword reaches a golem 50 px away (golem hp {_probeEnemy.Hp:0} < {_hpMark:0})", _probeEnemy.Hp < _hpMark);
                Check($"the swing lunges forward ({p.GlobalPosition.X - _posMark.X:0.0} px)", p.GlobalPosition.X - _posMark.X > 8);
                // Crescent Wave: a swing from well out of reach still cuts the golem
                Upgrades.Apply(Upgrades.Get("wave"), p.Stats, p);
                _probeEnemy.GlobalPosition = p.GlobalPosition + new Vector2(120, -6);
                break;
            case 20:
                _hpMark = _probeEnemy.Hp;
                _heroInput = new PlayerInput { Attack = true, Aim = Vector2.Right };
                break;
            case 21: _heroInput = default; break;
            case 28:
                Check($"crescent wave hits at 120 px (golem hp {_probeEnemy.Hp:0} < {_hpMark:0})", _probeEnemy.Hp < _hpMark);
                Finish();
                break;
        }
    }
    private Enemy _probeEnemy;

    private void Finish()
    {
        GD.Print(_heroOk ? "[herotest] PASS" : "[herotest] FAIL");
        GetTree().Quit(_heroOk ? 0 : 1);
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
        if (Edge(4.2f)) { i.Throw = true; i.Aim = new Vector2(1, 0); }
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
        if (_animT > 5.4f) GetTree().Quit();
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
        GetTree().Quit(ok ? 0 : 1);
    }

    private void RunGenTest()
    {
        int n = 30, clean = 0;
        ulong total = 0;
        for (int s = 1; s <= n; s++)
        {
            ulong t0 = Time.GetTicksMsec();
            var c = CaveGenerator.Generate(s * 1013);
            ulong ms = Time.GetTicksMsec() - t0;
            total += ms;
            if (c.TrapCells <= 6) clean++;
            int dead = c.Rooms.Count(r => r.Kind != RoomKind.Start && r.Kind != RoomKind.Boss);
            float bossDist = c.Boss == null ? -1 : c.Boss.Center.DistanceTo(c.StartPos);
            GD.Print($"seed {s * 1013}: {ms} ms attempts {c.Attempts} traps {c.TrapCells} reachable {c.ReachableCells} deadEndRooms {dead} miniBosses {c.Rooms.Count(r => r.Kind == RoomKind.MiniBoss)} boss {(c.Boss != null)} bossDist {bossDist:0} spawns {c.Spawns.Count}");
        }
        foreach (int s in new[] { 7091, 1013, 20260 })
        {
            var c = CaveGenerator.GenerateOnce(s);
            SaveCaveImage(c, $"user://cave_{s}.png");
            GD.Print($"image seed {s}: traps {c.TrapCells} -> {ProjectSettings.GlobalizePath($"user://cave_{s}.png")}");

        }
        GD.Print($"[gentest] {clean}/{n} trap-free, avg {total / (ulong)n} ms");
        GetTree().Quit();
    }
}
