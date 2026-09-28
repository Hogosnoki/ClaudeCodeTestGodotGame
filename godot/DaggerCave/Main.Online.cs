using System;
using System.Linq;
using Godot;

namespace DaggerCave;

/// <summary>
/// Online play, the game's side: the online menu, starting a run together, going down together
/// (everyone still standing waits at the same exit), falling and being brought back, the
/// guardian, and how the run ends. What travels between the games, and how, is in Net/.
/// </summary>
public partial class Main
{
    private OnlineMenu _onlineMenu;
    private Button _titleOnline;
    /// <summary>This game is in a run with others (or at the camp after one).</summary>
    private bool _onlineRun;
    private Portal _waitingAt;
    private float _allDownT;
    private bool _descending;

    /// <summary>The exit this game's hero waits at to go down with the others (online), or null.</summary>
    public Portal WaitingAt => _waitingAt;

    private void SetupOnline()
    {
        AddChild(new NetNode());
        _onlineMenu = new OnlineMenu { Back = CloseOnlineMenu, StartRun = HostStartRun };
        _uiLayer.AddChild(_onlineMenu);
        // a button on the title (and camp) screens for the mouse; keys and the controller use O / Y
        _titleOnline = UiKit.Button("Play online with friends", OpenOnlineMenu, 300);
        _titleOnline.Theme = UiKit.Theme;
        _titleOnline.FocusMode = Control.FocusModeEnum.None;
        _titleOnline.AnchorLeft = _titleOnline.AnchorRight = 0.5f;
        _titleOnline.AnchorTop = _titleOnline.AnchorBottom = 1f;
        _titleOnline.OffsetLeft = -150; _titleOnline.OffsetRight = 150;
        _titleOnline.OffsetTop = -66; _titleOnline.OffsetBottom = -24;
        _titleOnline.Visible = false;
        _uiLayer.AddChild(_titleOnline);
        Net.Closed += OnNetClosed;
    }

    private void UpdateTitleButton()
    {
        // (the main menu has its own Multiplayer button now)
        const bool show = false;
        if (_titleOnline.Visible != show) _titleOnline.Visible = show;
    }

    // ---------------------------------------------------------------- the menu

    private void OpenOnlineMenu()
    {
        if (_onlineMenu.Visible) return;
        // from the camp: a fresh cave behind the menu (and the title after it)
        if (_state == State.Dead) ResetToTitle();
        _overlay.Visible = false;
        _mainMenu.Visible = false;
        _heroChoice.Visible = false;
        ShowCampScene(true);
        _camp.Close = false;
        _onlineMenu.Open();
    }

    private void CloseOnlineMenu()
    {
        _onlineMenu.Visible = false;
        if (Net.Online && !Net.InRun) Net.Leave();
        if (_state == State.Title) ShowTitle();
    }

    /// <summary>A player joined or left (shown in the corner during a run; the lobby lists everyone).</summary>
    public void OnlineNotice(string text)
    {
        if (_onlineMenu.Visible) return;
        _hud.ShowNotice(text);
    }

    public void OnlineBanner(string text, float seconds) => _hud.ShowBanner(text, seconds);

    /// <summary>The connection closed (the host left, or never answered): back to the title, saying why.</summary>
    private void OnNetClosed(string why)
    {
        // (in the lobby, the online menu shows why by itself)
        if (_onlineRun) ReturnToTitle(why);
    }

    /// <summary>Leaves the online game (the pause menu), back to the title.</summary>
    private void LeaveOnline(string why)
    {
        Net.Leave();
        ReturnToTitle(why);
    }

    private void ReturnToTitle(string why)
    {
        _onlineRun = false;
        ResetToTitle();
        _overlay.Visible = false;
        _onlineMenu.Open(why);
    }

    /// <summary>From the camp after a run together: back to the lobby (still connected) for the next one.</summary>
    private void BackToLobby()
    {
        // (in the lobby, the online menu itself says if the connection closes)
        _onlineRun = false;
        ResetToTitle();
        _overlay.Visible = false;
        _onlineMenu.Open();
    }

    /// <summary>The title's state, over a fresh cave (never the level a run ended in).</summary>
    private void ResetToTitle()
    {
        _waitingAt = null;
        _descending = false;
        _pauseMenu.Visible = false;
        _upgradeMenu.Visible = false;
        if (_settingsMenu.Visible) { _settingsMenu.Visible = false; GameSettings.Save(); }
        _metaMenu.Visible = false;
        Engine.TimeScale = 1;
        _hitStopLeft = 0;
        _state = State.Title;
        GetTree().Paused = true;
        _hud.Visible = false;
        G.Depth = 0;
        G.Biome = Biomes.Get(BiomeId.Entrance);
        _seed = _rng.Next(1, 999999);
        _pendingTreasure.Clear();
        BuildLevel(_seed, freshPlayer: true);
        // (back at the camp outside)
        ShowCampScene(true);
        _camp.Close = false;
        _sfx.SetMusic("camp");
    }

    // ---------------------------------------------------------------- starting and going down

    /// <summary>The host's Start: everyone begins the same cave at once.</summary>
    private void HostStartRun()
    {
        if (!Net.IsHost || Net.InRun || !Net.AllPicked) return;
        int seed = _rng.Next(1, 999999);
        NetSync.SendStart(seed);
        StartOnlineRun(seed);
    }

    /// <summary>A run together begins (the host started it): the cave mouth, from the host's seed.</summary>
    public void StartOnlineRun(int seed)
    {
        if (!Net.Online) return;
        _onlineMenu.Visible = false;
        _pauseMenu.Visible = false;
        _upgradeMenu.Visible = false;
        if (_settingsMenu.Visible) { _settingsMenu.Visible = false; GameSettings.Save(); }
        _metaMenu.Visible = false;
        Net.InRun = true;
        _onlineRun = true;
        _waitingAt = null;
        _allDownT = 0;
        _descending = false;
        NetSync.ClearExits();
        Engine.TimeScale = 1;
        _hitStopLeft = 0;
        G.Hero = Net.Mine?.Hero ?? G.Hero;
        G.Depth = 0;
        G.Biome = Biomes.Get(BiomeId.Entrance);
        _seed = seed;
        _pendingTreasure.Clear();
        BuildLevel(seed, freshPlayer: true);
        _hud.HintTime = 10;
        StartPlaying();
        var others = Net.Peers.Values.Where(p => p.Id != Net.Me).Select(p => p.Name).ToList();
        if (others.Count > 0) _hud.ShowNotice("Descending with " + string.Join(" and ", others), 5f);
    }

    /// <summary>Everyone went down an exit (the host says where): the same level in every game.</summary>
    public void OnlineLevel(BiomeId biome, int depth, int seed, float runTime)
    {
        if (!Net.InRun) return;
        G.RunTime = runTime;
        _waitingAt = null;
        GoDeeper((int)biome, depth, seed);
    }

    /// <summary>This game's hero stepped into an exit: it waits there until everyone still standing has.</summary>
    public void WaitAtExit(Portal portal)
    {
        var me = G.Player;
        if (!Net.InRun || me == null || me.Dead || _waitingAt == portal) return;
        _waitingAt = portal;
        NetSync.AtExit(portal, true);
        G.Sfx.Play("portal", portal.GlobalPosition, -10, 0.05f, 1.3f);
        var (here, of) = ExitCount();
        if (here < of) _hud.ShowBanner("WAITING FOR THE OTHERS", 1.8f);
    }

    /// <summary>Heroes standing, and how many of them wait at an exit.</summary>
    public (int here, int of) ExitCount()
    {
        int here = 0, of = 0;
        foreach (var p in Net.Peers.Values)
        {
            var av = p.Avatar;
            if (av == null || !IsInstanceValid(av) || av.Dead) continue;
            of++;
            if (p.Id == Net.Me ? _waitingAt != null : p.AtExit) here++;
        }
        return (here, of);
    }

    /// <summary>Host: is everyone standing at the same exit? Then down they all go.</summary>
    public void CheckExits()
    {
        if (!Net.IsHost || !Net.InRun || _descending) return;
        var portal = NetSync.ExitEveryoneIsAt();
        if (portal == null) return;
        _descending = true;
        if (portal.Outside) CallDeferred(MethodName.HostLeaveCave);
        else CallDeferred(MethodName.HostDescend, (int)(portal.To?.Id ?? BiomeId.Slime), portal.Depth);
    }

    private void HostLeaveCave()
    {
        _descending = false;
        if (!Net.InRun) return;
        NetSync.SendLeftCave();
        OnlineLeftCave();
    }

    /// <summary>Everyone still standing walked out of the cave mouth together: back to the lobby, keeping nothing from the run.</summary>
    public void OnlineLeftCave()
    {
        if (!_onlineRun || !Net.InRun) return;
        Net.InRun = false;
        Meta.Restore(_metaAtStart);
        _metaAtStart = null;
        BackToLobby();
        _onlineMenu.Open("You all walked back out into the daylight. Nothing from that run was kept.");
    }

    private void HostDescend(int biome, int depth)
    {
        _descending = false;
        if (!Net.InRun) return;
        int seed = _rng.Next(1, 999999);
        // (the message first: every creature and thing made from here on belongs to the new level)
        NetSync.SendLevel((BiomeId)biome, depth, seed);
        GoDeeper(biome, depth, seed);
    }

    private void OnlineTick(float dt)
    {
        var me = G.Player;
        // walk away from the exit (or fall) and you're no longer waiting there
        if (_waitingAt != null && (!IsInstanceValid(_waitingAt) || me == null || me.Dead || me.GlobalPosition.DistanceTo(_waitingAt.GlobalPosition) > 60f))
        {
            NetSync.AtExit(IsInstanceValid(_waitingAt) ? _waitingAt : null, false);
            _waitingAt = null;
        }
        // host: when everyone is down (for a moment: a revive may be coming), the run is over
        if (Net.IsHost && _state != State.Dead)
        {
            bool anyone = false;
            foreach (var h in G.Players) if (IsInstanceValid(h) && !h.Dead) { anyone = true; break; }
            _allDownT = anyone ? 0 : _allDownT + dt;
            if (_allDownT > 3f)
            {
                _allDownT = 0;
                NetSync.SendRunOver(false);
                OnlineRunOver(false);
            }
        }
    }

    // ---------------------------------------------------------------- falling, the guardian, the end

    private void OnlineHeroDown()
    {
        if (_waitingAt != null) { NetSync.AtExit(_waitingAt, false); _waitingAt = null; }
        bool others = G.Players.Any(h => h != G.Player && IsInstanceValid(h) && !h.Dead);
        _hud.ShowBanner(others ? "YOU'RE DOWN" : "EVERYONE IS DOWN", 3f);
        if (others) _hud.ShowNotice("A friend can bring you back: they stand beside you and hold interact", 7f);
    }

    public void OnPlayerRevived() => _hud.ShowBanner("BACK ON YOUR FEET", 2f);

    /// <summary>The host's guardian woke (a client's copy of it arrived).</summary>
    public void OnlineGuardianAppeared(Enemy e)
    {
        ActiveBoss = e;
        _hud.ShowBanner(e.Title != "" ? e.Title : e.DisplayName.ToUpperInvariant(), 3f);
        GuardianFarNotice(e.GlobalPosition, "");
        _sfx.SetMusic("boss");
    }

    /// <summary>The guardian woke somewhere else (a friend found it): where to go, without moving your view.</summary>
    private void GuardianFarNotice(Vector2 at, string who)
    {
        var me = G.Player;
        if (me == null || me.GlobalPosition.DistanceTo(at) < 700f) return;
        string by = string.IsNullOrEmpty(who) ? "A friend" : who;
        _hud.ShowNotice($"{by} woke the guardian: its chamber is marked EXIT on your map", 6f);
    }

    /// <summary>An exit opened in the host's game (for the autopilot's map).</summary>
    public void NoteExit(Vector2 at)
    {
        _guardianDown = true;
        ExitSpots.Add(at + new Vector2(0, 14));
    }

    /// <summary>The guardian fell in the host's game: this player's own rewards.</summary>
    public void OnlineGuardianDown(int embers, bool dragon, string name)
    {
        if (!Net.InRun) return;
        var at = G.Player?.GlobalPosition ?? Vector2.Zero;
        if (ActiveBoss != null && IsInstanceValid(ActiveBoss)) at = ActiveBoss.GlobalPosition;
        NetSync.Local(() => GuardianRewards(embers, dragon, name, at));
    }

    /// <summary>The run together is over (everyone fell, or the dragon did): to the camp.</summary>
    public void OnlineRunOver(bool victory)
    {
        if (!_onlineRun || _state == State.Dead) return;
        Net.InRun = false;
        if (victory) _victory = true;
        _waitingAt = null;
        _victoryT = -1;
        _upgradeMenu.Visible = false;
        _pauseMenu.Visible = false;
        if (_settingsMenu.Visible) { _settingsMenu.Visible = false; GameSettings.Save(); }
        if (G.Player != null) G.Player.Choosing = false;
        Engine.TimeScale = 1;
        _hitStopLeft = 0;
        _state = State.Dead;
        _deadT = 0;
        _sfx.SetMusic("");
        Meta.Save();
    }

    private static string HeroName(HeroKind h) => h switch { HeroKind.Warden => "Warden", HeroKind.Vitalist => "Vitalist", _ => "Swordsman" };

    /// <summary>The camp after a run together: how it went, and back to the lobby for the next.</summary>
    private void ShowOnlineCamp()
    {
        var p = G.Player;
        int secs = (int)_runTime;
        _overlay.HeroCards = false;
        string earned = _runEmbers > 0 || _runFinds != "" ? $"Earned: {_runEmbers} ember{(_runEmbers == 1 ? "" : "s")}{_runFinds}" : "";
        string party = string.Join("   ·   ", Net.Peers.Values.Select(x => $"{x.Name} ({HeroName(x.Hero)})"));
        string next = Net.IsHost ? "go back to the lobby and set off again" : "go back to the lobby (the host starts the next descent)";
        _overlay.Show(_victory ? "VICTORY" : "THE PARTY HAS FALLEN", 0.45f,
            _victory ? "The Elder Dragon is slain. The deep is quiet... for now." : $"Fell at depth {G.Depth} in the {G.Biome?.Name ?? "cave"}",
            party,
            $"You: level {p.Level}   ·   {p.Kills} kills   ·   {secs / 60}:{secs % 60:00}",
            earned,
            CampLine(),
            UsingPad ? $"!A to {next}" : $"!ENTER to {next}");
    }
}
