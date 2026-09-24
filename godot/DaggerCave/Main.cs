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
/// for testing).
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
    private ulong _hitStopUntil;
    private float _spawnT, _runTime, _deadT;
    private int _seed;
    private readonly Random _rng = new();
    private readonly Queue<bool> _pendingTreasure = new();
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
    private bool _bestiary;
    private float _bestiaryT = -1;
    private BotPilot _bot;

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

        _seed = _seed != 0 ? _seed : (int)(Time.GetUnixTimeFromSystem() * 1000 % 1000000);
        if (_autotest) G.Rng = new Random(_seed);
        G.Depth = 1;
        BuildLevel(_seed, freshPlayer: true);
        if (_bestiary)
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
            _overlay.Show("DAGGER DEEP", 0.55f,
                "A rogue-lite descent through flooded caverns.",
                "",
                "A / D  move      SPACE  jump      W / S  swim up / down",
                "LEFT CLICK  swing dagger toward the mouse      RIGHT CLICK  throw dagger",
                "SHIFT  dodge      ESC  pause      (J / K / L also swing / throw / dodge)",
                "",
                "!Press ENTER or click to begin");
            _sfx.SetMusic("ambient");
        }
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

        Act("move_left", K(Key.A), K(Key.Left), Ax(JoyAxis.LeftX, -1));
        Act("move_right", K(Key.D), K(Key.Right), Ax(JoyAxis.LeftX, 1));
        Act("move_up", K(Key.W), K(Key.Up), Ax(JoyAxis.LeftY, -1));
        Act("move_down", K(Key.S), K(Key.Down), Ax(JoyAxis.LeftY, 1));
        Act("jump", K(Key.Space), J(JoyButton.A));
        Act("attack", new InputEventMouseButton { ButtonIndex = MouseButton.Left });
        Act("attack_alt", K(Key.J), J(JoyButton.X));
        Act("throw", new InputEventMouseButton { ButtonIndex = MouseButton.Right });
        Act("throw_alt", K(Key.K), J(JoyButton.RightShoulder));
        Act("dodge", K(Key.Shift), K(Key.L), J(JoyButton.B));
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

        _cam = new Camera2D { Zoom = new Vector2(2.1f, 2.1f), ProcessCallback = Camera2D.Camera2DProcessCallback.Physics };
        _cam.LimitLeft = 0; _cam.LimitTop = 0;
        _cam.LimitRight = (int)cave.SizePx.X; _cam.LimitBottom = (int)cave.SizePx.Y;
        _world.AddChild(_cam);
        _cam.GlobalPosition = player.GlobalPosition;
        _cam.MakeCurrent();

        // Treasure chests are visible from the start.
        foreach (var room in cave.Rooms)
        {
            if (room.Kind != RoomKind.Treasure) continue;
            // Sit the chest on real ground (the room's floor line may have been cut by another tunnel).
            if (!cave.FindFloor(room.Center, 700, out var floor)) continue;
            _world.AddChild(new Chest { Position = floor });
        }

        _hud.ResetMap(cave);
        _hud.ShowBanner($"DEPTH {G.Depth}", 3f);
        _spawnT = 0;
    }

    public void NextDepth()
    {
        G.Depth++;
        _seed = _rng.Next(1, 999999);
        BuildLevel(_seed, freshPlayer: false);
        if (_bot != null) G.Player.InputOverride = _bot.Read;
        _sfx.SetMusic("ambient");
    }

    private void StartPlaying()
    {
        _overlay.Visible = false;
        _hud.Visible = true;
        GetTree().Paused = false;
        _state = State.Playing;
        _runTime = 0;
        _sfx.SetMusic("ambient");
    }

    private void Restart()
    {
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
        _state = State.Dead;
        _deadT = 0;
        _sfx.SetMusic("");
    }

    public void OfferUpgrades(bool treasure)
    {
        _pendingTreasure.Enqueue(treasure);
    }

    private void TryOpenUpgradeMenu()
    {
        var p = G.Player;
        if (p == null || p.Dead || _upgradeMenu.Visible) return;
        bool? treasure = null;
        if (_pendingTreasure.Count > 0) treasure = _pendingTreasure.Dequeue();
        else if (p.PendingLevelUps > 0) { p.PendingLevelUps--; treasure = false; }
        if (treasure == null) return;
        var choices = Upgrades.Roll(p.Stats, 3, treasure.Value, _rng);
        if (choices.Count == 0) { p.Heal(30); return; }
        _state = State.Choosing;
        GetTree().Paused = true;
        _sfx.Play(treasure.Value ? "chest" : "levelup");
        _upgradeMenu.Open(choices, treasure.Value ? "TREASURE!" : $"LEVEL {p.Level}!");
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

    public void HitStop(float seconds)
    {
        _hitStopUntil = Time.GetTicksMsec() + (ulong)(seconds * 1000);
        Engine.TimeScale = 0.08;
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (_state == State.Title && (e.IsActionPressed("confirm") || (e is InputEventMouseButton mb && mb.Pressed)))
        {
            StartPlaying();
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        if (_hitStopUntil != 0 && Time.GetTicksMsec() >= _hitStopUntil) { Engine.TimeScale = 1; _hitStopUntil = 0; }

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
                    _overlay.Show("YOU DIED", 0.6f,
                        $"Depth {G.Depth}   ·   Level {p.Level}   ·   {p.Kills} kills   ·   {secs / 60}:{secs % 60:00}",
                        "",
                        "!Press R to descend again");
                }
                if (_deadT > 1.2f && Input.IsActionJustPressed("restart")) Restart();
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
                    _overlay.Show("PAUSED", 0.5f, "", "!Press ESC to resume");
                    return;
                }
                _runTime += dt;
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
        if (_autotest) AutotestTick(dt);
        if (_bestiary) BestiaryTick(dt);
    }

    private void UpdateCamera(float dt)
    {
        var p = G.Player;
        if (p == null || _cam == null) return;
        var target = p.GlobalPosition + new Vector2(p.Velocity.X * 0.15f, p.Velocity.Y * 0.08f - 10);
        if (ActiveBoss != null && !ActiveBoss.Dead) target = target.Lerp(ActiveBoss.GlobalPosition, 0.25f);
        _cam.GlobalPosition = _cam.GlobalPosition.Lerp(target, 1 - MathF.Exp(-dt * 7));
        float s = _fx?.Shake ?? 0;
        _cam.Offset = s > 0 ? new Vector2(G.Range(-s, s), G.Range(-s, s)) * 0.5f : Vector2.Zero;
    }

    // ------------------------------------------------------------------ spawning

    private void RunSpawner(float dt)
    {
        var cave = G.Cave;
        var p = G.Player;
        if (p.Dead) return;
        int alive = G.Enemies.Count(e => !e.Dead);
        int cap = 22 + G.Depth * 4;
        foreach (var sp in cave.Spawns)
        {
            float d = sp.Pos.DistanceTo(p.GlobalPosition);
            if (sp.Used)
            {
                sp.Cooldown -= dt;
                if (sp.Cooldown <= 0 && d > 1000) sp.Used = false;
                continue;
            }
            if (d < 330 || d > 640 || alive >= cap) continue;
            sp.Used = true;
            sp.Cooldown = G.Range(70, 110);
            alive += SpawnGroup(sp);
        }
    }

    private int SpawnGroup(SpawnPoint sp)
    {
        var cave = G.Cave;
        int extra = G.Depth - 1;
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
            GD.Print($"seed {s * 1013}: {ms} ms attempts {c.Attempts} traps {c.TrapCells} reachable {c.ReachableCells} deadEndRooms {dead} boss {(c.Boss != null)} bossDist {bossDist:0} spawns {c.Spawns.Count}");
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
