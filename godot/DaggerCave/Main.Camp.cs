using System;
using System.Linq;
using Godot;

namespace DaggerCave;

/// <summary>
/// The camp outside the cave (the 3D scene behind the main menu and the hero choice): shown on a
/// layer of its own under the menus, sized to the window, and switched off while you're below.
/// --campshot=DIR renders it from each view for a look.
/// </summary>
public partial class Main
{
    private CampScene _camp;
    private CanvasLayer _campLayer;
    private TextureRect _campView;
    private string _campShot = "";
    private float _campShotT;
    private int _campShotStep;

    /// <summary>At the camp (State.Title): the main menu, or choosing a hero at the fire.</summary>
    private enum Front { Menu, Heroes }
    private Front _front = Front.Menu;
    private MainMenu _mainMenu;
    private HeroChoice _heroChoice;
    private ColorRect _fade;
    /// <summary>Seconds into setting off for the cave (the camera drifts to its mouth as the screen darkens), or -1.</summary>
    private float _departT = -1f;
    private float _fadeInT, _fadeInLen = 1f;

    private void SetupCamp()
    {
        _campLayer = new CanvasLayer { Layer = 1 };
        AddChild(_campLayer);
        _camp = new CampScene();
        _campLayer.AddChild(_camp);
        _campView = new TextureRect
        {
            Texture = _camp.GetTexture(), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _campView.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _campLayer.AddChild(_campView);
        FitCamp();
        GetWindow().SizeChanged += FitCamp;
    }

    /// <summary>The menus over the camp (drawn under every other menu), and the fade used going down.</summary>
    private void SetupFrontMenus()
    {
        _mainMenu = new MainMenu { SinglePlayer = () => ShowHeroChoice(), MultiPlayer = OpenOnlineMenu, Settings = OpenFrontSettings, Quit = () => SafeQuit.Request(this) };
        _uiLayer.AddChild(_mainMenu);
        _uiLayer.MoveChild(_mainMenu, 1);
        _heroChoice = new HeroChoice
        {
            Prev = () => StepHero(-1), Next = () => StepHero(1), Descend = BeginDescent, Back = BackToMenu,
            Perks = () => _perkMenu.Open(G.Hero),
            Loadout = () => _loadoutMenu.Open(G.Hero),
            Trees = () => { if (Meta.Trees.Any(Meta.Visible)) _metaMenu.Open(MetaMenu.Mode.Browse); },
        };
        _uiLayer.AddChild(_heroChoice);
        _uiLayer.MoveChild(_heroChoice, 2);
        _fade = new ColorRect { Color = new Color(0, 0, 0, 0), MouseFilter = Control.MouseFilterEnum.Ignore };
        _fade.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _uiLayer.AddChild(_fade);
    }

    /// <summary>The main menu over the wide view of the camp.</summary>
    private void ShowMainMenu()
    {
        _front = Front.Menu;
        ShowCampScene(true);
        _camp.Close = false;
        _camp.Depart = 0f;
        _overlay.Visible = false;
        _heroChoice.Visible = false;
        _hud.Visible = false;
        _mainMenu.Open(Meta.Embers > 0 ? $"Embers: {Meta.Embers}" : "");
        _sfx.SetMusic("camp");
    }

    /// <summary>Choosing a hero at the fire (after a run, with how it went on top).</summary>
    private void ShowHeroChoice(string summaryTitle = "", string summary = "")
    {
        _front = Front.Heroes;
        ShowCampScene(true);
        _camp.Close = true;
        _camp.Depart = 0f;
        _camp.Selected = G.Hero;
        _overlay.Visible = false;
        _mainMenu.Visible = false;
        _hud.Visible = false;
        _heroChoice.Open(summaryTitle, summary);
        _sfx.SetMusic("camp");
    }

    private void BackToMenu()
    {
        if (_departT >= 0f) return;
        // (after a run: back at the title's state, a fresh cave waiting)
        if (_state == State.Dead) ResetToTitle();
        ShowMainMenu();
    }

    private void StepHero(int step)
    {
        if (_departT >= 0f) return;
        var next = G.Hero;
        for (int k = 0; k < HeroCount; k++)
        {
            next = (HeroKind)(((int)next + step + HeroCount) % HeroCount);
            if (Meta.IsUnlocked(next)) break;
        }
        PickHero(next);
        _camp.Selected = G.Hero;
        _heroChoice.Refresh();
    }

    private void OpenFrontSettings()
    {
        _mainMenu.Visible = false;
        _settingsMenu.Open();
    }

    /// <summary>Off to the cave: the camera drifts to its mouth as the screen darkens, then the run begins at depth 0.</summary>
    private void BeginDescent()
    {
        if (_departT >= 0f || Net.Online) return;
        _departT = 0f;
        _heroChoice.Visible = false;
        _sfx.Play("portal", null, -8, 0.05f, 0.7f);
    }

    private void TickFront(float dt)
    {
        if (_departT >= 0f)
        {
            _departT += dt;
            _camp.Depart = Math.Min(1f, _departT / 1.1f);
            _fade.Color = new Color(0, 0, 0, W3.SmoothStep(0.35f, 1.05f, _departT));
            if (_departT >= 1.15f)
            {
                _departT = -1f;
                EnterCave();
                FadeFrom(Colors.Black, 0.7f);
            }
            return;
        }
        if (_fadeInT > 0f)
        {
            _fadeInT = Math.Max(0f, _fadeInT - dt);
            _fade.Color = _fade.Color with { A = W3.Smooth01(_fadeInT / _fadeInLen) };
        }
        // the hints follow the device in use
        if (_heroChoice.Visible && _heroChoice.IsVisibleInTree() && Engine.GetProcessFrames() % 20 == 0) _heroChoice.Refresh();
    }

    /// <summary>The screen starts as <paramref name="c"/> and clears over <paramref name="seconds"/>.</summary>
    private void FadeFrom(Color c, float seconds)
    {
        _fade.Color = c with { A = 1f };
        _fadeInT = _fadeInLen = seconds;
    }

    /// <summary>Down into the cave at depth 0 with the chosen hero.</summary>
    private void EnterCave()
    {
        if (Brains.Training) Brains.SaveAll();
        G.Depth = 0;
        G.Biome = Biomes.Get(BiomeId.Entrance);
        // (the cave waiting behind the camp was made for the hero shown then: remake it if that changed)
        if (G.Player == null || G.Player.Dead || G.Player.Stats.Hero != G.Hero || _state == State.Dead)
        {
            _seed = _rng.Next(1, 999999);
            _pendingTreasure.Clear();
            BuildLevel(_seed, freshPlayer: true);
        }
        _hud.HintTime = 8;
        StartPlaying();
    }

    /// <summary>The camp renders at the window's real size (the menus above are scaled from 1280x720).</summary>
    private void FitCamp()
    {
        if (_camp == null) return;
        var size = GetWindow().Size;
        _camp.Size = new Vector2I(Math.Max(64, size.X), Math.Max(64, size.Y));
    }

    /// <summary>Shows the camp (and stops drawing the cave), or hides it.</summary>
    private void ShowCampScene(bool on)
    {
        // (made the first time it's needed: the tests that never show it never pay for it)
        if (_camp == null) { if (!on) return; SetupCamp(); }
        if (_camp.Active != on) _camp.Active = on;
        _campLayer.Visible = on;
        _stage.Visible = !on;
    }

    // ---------------------------------------------------------------- --fronttest=DIR
    // The way in, driven by actions like the keyboard and controller send them: the main menu,
    // the hero choice at the fire, down into the cave, a death back to the camp, and back to the
    // menu, with a picture at each stage.

    private string _frontTest = "";
    private float _ftT;
    private int _ftStep;
    private bool _ftOk = true;

    private void FtCheck(string what, bool ok)
    {
        GD.Print($"[fronttest] {(ok ? "ok  " : "FAIL")} {what}");
        _ftOk &= ok;
    }

    private void FrontTestTick(float dt)
    {
        _ftT += dt;
        void Act(string action, bool down = true) => Input.ParseInputEvent(new InputEventAction { Action = action, Pressed = down });
        void Shot(string name) => GetViewport().GetTexture().GetImage().SavePng($"{_frontTest}/{name}.png");
        var steps = new (float at, Action act)[]
        {
            (1.5f, () => { Shot("front_menu"); FtCheck($"the game opens on the main menu over the camp ({_state}, menu {_mainMenu.Visible}, camp {_campLayer?.Visible})", _state == State.Title && _mainMenu.Visible && _campLayer.Visible); Act("ui_accept"); }),
            (1.55f, () => Act("ui_accept", false)),
            (1.8f, () => FtCheck($"Single player: choosing a hero at the fire (choice {_heroChoice.Visible}, menu {_mainMenu.Visible})", _heroChoice.Visible && !_mainMenu.Visible && _front == Front.Heroes)),
            (3.6f, () => { Shot("front_choice"); Act("move_right"); }),
            (3.65f, () => Act("move_right", false)),
            (5f, () => { Shot("front_choice_next"); FtCheck($"right chooses the next hero ({G.Hero}, standing at the fire: {_camp.Selected})", G.Hero == HeroKind.Warden && _camp.Selected == HeroKind.Warden); Act("confirm"); }),
            (5.05f, () => Act("confirm", false)),
            (5.6f, () => Shot("front_depart")),
            (7.2f, () => { Shot("front_cave"); FtCheck($"it sets off down into the cave at depth 0 with that hero ({_state}, depth {G.Depth}, {G.Player?.Stats.Hero}, camp shown {_campLayer.Visible})", _state == State.Playing && G.Depth == 0 && G.Player?.Stats.Hero == HeroKind.Warden && !_campLayer.Visible); G.Player?.GiveUp(); }),
            (10f, () => { Shot("front_dead"); FtCheck($"a death comes back to the camp: the fire, how it went, and the choice again ({_state}, choice {_heroChoice.Visible}, camp {_campLayer.Visible})", _state == State.Dead && _heroChoice.Visible && _campLayer.Visible); Act("pause"); }),
            (10.05f, () => Act("pause", false)),
            (10.6f, () => { Shot("front_back"); FtCheck($"back goes to the main menu ({_state}, menu {_mainMenu.Visible})", _state == State.Title && _mainMenu.Visible && !_heroChoice.Visible); }),
            (10.8f, () => { GD.Print(_ftOk ? "[fronttest] PASS" : "[fronttest] FAIL"); SafeQuit.Request(this, _ftOk ? 0 : 1); }),
        };
        while (_ftStep < steps.Length && _ftT >= steps[_ftStep].at) steps[_ftStep++].act();
    }

    private void CampShotTick(float dt)
    {
        _campShotT += dt;
        void Shot(string name) => _camp.GetTexture().GetImage().SavePng($"{_campShot}/{name}.png");
        switch (_campShotStep)
        {
            case 0 when _campShotT > 3f: Shot("camp_wide"); _camp.Close = true; _campShotStep++; _campShotT = 0; break;
            case 1 when _campShotT > 3f: Shot("camp_close_swordsman"); _camp.Selected = HeroKind.Warden; _campShotStep++; _campShotT = 0; break;
            case 2 when _campShotT > 1.5f: Shot("camp_close_warden"); _camp.Selected = HeroKind.Vitalist; _campShotStep++; _campShotT = 0; break;
            case 3 when _campShotT > 1.5f: Shot("camp_close_vitalist"); _camp.Selected = HeroKind.Elementalist; _campShotStep++; _campShotT = 0; break;
            case 4 when _campShotT > 1.5f: Shot("camp_close_elementalist"); _camp.Selected = HeroKind.Rogue; _campShotStep++; _campShotT = 0; break;
            case 5 when _campShotT > 1.5f: Shot("camp_close_rogue"); _camp.Depart = 0.6f; _campShotStep++; _campShotT = 0; break;
            case 6 when _campShotT > 0.5f: Shot("camp_depart"); _campShotStep++; SafeQuit.Request(this); break;
        }
    }
}
