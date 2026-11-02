using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

/// <summary>
/// Joining a run that's under way, and getting back into one.
///
/// A client whose connection drops in the middle of a run doesn't see a "disconnected" screen at first: it stays in the level and at
/// once tries the same code again, for a few seconds, over and over. A short lapse is ridden out like that: the host takes it back
/// as the hero it was, puts it in the level the party is in and brings it up to date. Only when that fails does it go to the title,
/// saying so, with a Reconnect button in the online menu.
///
/// The hero is also kept on disk while a run together goes on (every few seconds, and as it drops), so quitting the game, or a crash,
/// loses nothing: start the game again, join the same code (the Reconnect button does it), and the host, seeing the run's token,
/// lets it pick up as it was: the same hero, its level, cards and relics, purse, keys, potions and health.
/// Someone new joining a run under way starts at the party's level, with that many picks to take.
/// </summary>
public partial class Main
{
    /// <summary>The run under way (its first seed: every game in it knows it), and the biome the level was made from.</summary>
    private int _runSeed, _levelBiome;
    /// <summary>A token is this run's: the one who presents it dropped out of it.</summary>
    public bool IsThisRun(int token) => Net.InRun && token != 0 && token == _runSeed;

    /// <summary>This game hosted the run under way (the host never rejoins: the run was its). Kept as the run starts: once closed, the connection forgets.</summary>
    private bool _wasHost;

    /// <summary>What this game's hero was in a run together, kept to get back in with (in memory, and on disk: see the class summary).</summary>
    private sealed class Dropped
    {
        public string Code = "";
        public int RunSeed; public HeroKind Hero;
        /// <summary>The peer id it had in the run (the others still count its worldly relics under it).</summary>
        public int OldPeer;
        /// <summary>The very stats object (only while this same game is running; after a restart the <see cref="Log"/> is played again instead).</summary>
        public PlayerStats Stats;
        /// <summary>The cards and relics taken, and the levels gained, in order (<see cref="PlayerStats.Log"/>).</summary>
        public List<string> Log = new();
        public float Hp, VitalForce, Alimus, Fuel;
        public int Level, Xp, Kills, Potions, Keys, Gold, GoldFound, GoldSpent, Rerolls, Trades, Milestones, Picks;
        public List<string> Relics = new();
        /// <summary>The run's embers so far, and its finds (only kept in memory).</summary>
        public int Embers; public string Finds = "";
        public long SavedAt;
    }
    private Dropped _dropped;

    private static string RejoinFile => G.NoSave ? "user://rejoin_test.cfg" : "user://rejoin.cfg";
    /// <summary>A record older than this is a run long gone.</summary>
    private const long RejoinKeepSeconds = 12 * 3600;
    /// <summary>How long the quiet reconnect tries before giving up, how long it waits between tries, and how often the hero is written down.</summary>
    private const float ReconnectFor = 12f, ReconnectGap = 0.75f, RejoinSaveEvery = 4f;

    private bool _reconnecting;
    private float _reconnectLeft, _reconnectRetryT = -1f, _rejoinSaveT;
    private string _reconnectWhy = "";
    /// <summary>A reconnect being put back in while this game was still running (its run's embers etc. are carried over, not counted again).</summary>
    private bool _resumeInPlace;

    /// <summary>The worldly relics the party carries (the host's word, for a joiner's level to be built the same: see <see cref="BuildLevel"/>).</summary>
    private List<(int Peer, string Id)> _joinRelics;
    /// <summary>While a dropped hero is being put back (<see cref="JoinRunInProgress"/>): what BuildLevel carries over instead of a fresh hero.</summary>
    private Dropped _resumeFrom;

    /// <summary>A client in a run together: its hero as it is now (kept in memory, and written down).</summary>
    private void RememberHero()
    {
        var p = G.Player;
        if (!_onlineRun || _wasHost || _runSeed == 0 || p == null || !IsInstanceValid(p) || p.Stats == null || NetSync.Building) return;
        _dropped = new Dropped
        {
            Code = Net.ActiveCode ?? "", RunSeed = _runSeed, OldPeer = Net.Me, Hero = p.Stats.Hero, Stats = p.Stats, Log = p.Stats.Log.ToList(),
            Hp = Math.Max(1f, p.Hp), VitalForce = p.VitalForce, Alimus = p.Alimus, Fuel = p.Fuel,
            Level = p.Level, Xp = p.Xp, Kills = p.Kills, Potions = p.Potions, Keys = p.Keys, Gold = p.Gold, GoldFound = p.GoldFound, GoldSpent = p.GoldSpent,
            Rerolls = p.Rerolls, Trades = p.Trades, Milestones = p.PendingMilestones, Picks = p.PendingPicks, Relics = RunRelics.Mine.ToList(),
            Embers = _runEmbers, Finds = _runFinds, SavedAt = (long)Time.GetUnixTimeFromSystem(),
        };
        SaveDropped();
    }

    private void SaveDropped()
    {
        if (_dropped == null || _dropped.Code == "") return;
        var d = _dropped;
        var cfg = new ConfigFile();
        void Set(string k, Variant v) => cfg.SetValue("rejoin", k, v);
        Set("code", d.Code); Set("run", d.RunSeed); Set("hero", (int)d.Hero); Set("saved", d.SavedAt); Set("peer", d.OldPeer);
        Set("log", d.Log.ToArray()); Set("relics", d.Relics.ToArray());
        Set("hp", d.Hp); Set("vital", d.VitalForce); Set("alimus", d.Alimus); Set("fuel", d.Fuel);
        Set("level", d.Level); Set("xp", d.Xp); Set("kills", d.Kills); Set("potions", d.Potions); Set("keys", d.Keys);
        Set("gold", d.Gold); Set("gold_found", d.GoldFound); Set("gold_spent", d.GoldSpent); Set("rerolls", d.Rerolls); Set("trades", d.Trades);
        Set("milestones", d.Milestones); Set("picks", d.Picks);
        cfg.Save(RejoinFile);
    }

    /// <summary>The hero written down by a game that quit or crashed in the middle of a run (null for none, or one long gone).</summary>
    private static Dropped LoadDropped()
    {
        var cfg = new ConfigFile();
        if (!FileAccess.FileExists(RejoinFile) || cfg.Load(RejoinFile) != Error.Ok) return null;
        Variant Get(string k, Variant def) => cfg.GetValue("rejoin", k, def);
        long saved = Get("saved", 0L).AsInt64();
        if ((long)Time.GetUnixTimeFromSystem() - saved > RejoinKeepSeconds) return null;
        var d = new Dropped
        {
            Code = Get("code", "").AsString(), RunSeed = Get("run", 0).AsInt32(), Hero = (HeroKind)Get("hero", 0).AsInt32(), SavedAt = saved, OldPeer = Get("peer", 0).AsInt32(),
            Log = Get("log", Array.Empty<string>()).AsStringArray().ToList(), Relics = Get("relics", Array.Empty<string>()).AsStringArray().ToList(),
            Hp = Get("hp", 1f).AsSingle(), VitalForce = Get("vital", 0f).AsSingle(), Alimus = Get("alimus", 0f).AsSingle(), Fuel = Get("fuel", 0f).AsSingle(),
            Level = Get("level", 1).AsInt32(), Xp = Get("xp", 0).AsInt32(), Kills = Get("kills", 0).AsInt32(), Potions = Get("potions", 1).AsInt32(), Keys = Get("keys", 0).AsInt32(),
            Gold = Get("gold", 0).AsInt32(), GoldFound = Get("gold_found", 0).AsInt32(), GoldSpent = Get("gold_spent", 0).AsInt32(),
            Rerolls = Get("rerolls", 0).AsInt32(), Trades = Get("trades", 0).AsInt32(), Milestones = Get("milestones", 0).AsInt32(), Picks = Get("picks", 0).AsInt32(),
        };
        return d.Code == "" || d.RunSeed == 0 || !Enum.IsDefined(d.Hero) ? null : d;
    }

    /// <summary>The run is over (or a new one began): nothing to get back into.</summary>
    private void ForgetDropped()
    {
        _dropped = null;
        Net.RejoinToken = 0;
        if (FileAccess.FileExists(RejoinFile)) DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(RejoinFile));
    }

    /// <summary>A hero this game can take back into a run (the online menu's Reconnect button), and the code it was joined with.</summary>
    public string RejoinCode => (_dropped ??= LoadDropped())?.Code;
    public HeroKind? RejoinHero => _dropped?.Hero;

    /// <summary>Joining (Net.Join, by hand or the quiet reconnect): with a hero kept from a run joined by that same code, ask the host to take it back.</summary>
    public void PrepareJoin(string code)
    {
        _dropped ??= LoadDropped();
        if (_dropped != null && string.Equals(_dropped.Code.Trim(), code.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            Net.RejoinToken = _dropped.RunSeed;
            G.Hero = _dropped.Hero;
        }
        else Net.RejoinToken = 0;
    }

    /// <summary>Every frame: the hero written down now and then, and the quiet reconnect's clock.</summary>
    private void RejoinTick(float dt)
    {
        if (_onlineRun && !_wasHost && Net.InRun && _state == State.Playing && !_reconnecting)
        {
            _rejoinSaveT -= dt;
            if (_rejoinSaveT <= 0) { _rejoinSaveT = RejoinSaveEvery; RememberHero(); }
        }
        if (!_reconnecting) return;
        _reconnectLeft -= dt;
        if (_reconnectRetryT >= 0)
        {
            _reconnectRetryT -= dt;
            if (_reconnectRetryT < 0 && _reconnectLeft > 0) Net.Join(_dropped?.Code ?? Net.ActiveCode);
        }
        if (_reconnectLeft <= 0) ReconnectFailed();
    }

    /// <summary>
    /// The connection closed under a client in a run: before anything is shown, it tries to get straight back in with the same code
    /// (true: trying; the game goes on meanwhile). A try that fails tries again until the time is up.
    /// </summary>
    private bool TryReconnect(string why)
    {
        if (_reconnecting)
        {
            // (that try failed: another in a moment, while there's time)
            if (_reconnectLeft > 0) { _reconnectRetryT = ReconnectGap; return true; }
            ReconnectFailed();
            return true;
        }
        if (_wasHost || _runSeed == 0 || string.IsNullOrEmpty(Net.ActiveCode) || G.Player == null || !IsInstanceValid(G.Player)) return false;
        RememberHero();
        if (_dropped == null) return false;
        _reconnecting = true;
        _reconnectWhy = why;
        _reconnectLeft = ReconnectFor;
        _reconnectRetryT = -1f;
        _hud.ShowNotice("Connection lost: reconnecting…", ReconnectFor);
        GD.Print($"[DaggerDeep] connection lost ({why}): reconnecting to {_dropped.Code}");
        if (!Net.Join(_dropped.Code)) _reconnectRetryT = ReconnectGap;
        return true;
    }

    /// <summary>The quiet reconnect ran out of time: the title, saying so (the online menu offers to reconnect by hand).</summary>
    private void ReconnectFailed()
    {
        if (!_reconnecting) return;
        _reconnecting = false;
        _reconnectRetryT = -1f;
        if (Net.Online && Net.Peers.Count > 0)
        {
            // (connected again, but no run to go back into: the host's lobby)
            ForgetDropped();
            _onlineRun = false;
            ResetToTitle();
            _overlay.Visible = false;
            _onlineMenu.Open("Back online, but that run is over.");
            return;
        }
        Net.Leave();
        GD.Print("[DaggerDeep] reconnect failed");
        ReturnToTitle($"{_reconnectWhy} Couldn't reconnect. Your hero is kept: press Reconnect to try again.");
    }

    /// <summary>The online menu's Reconnect: the same code again, asking for the hero kept.</summary>
    public void Reconnect()
    {
        var code = RejoinCode;
        if (string.IsNullOrEmpty(code)) return;
        GameSettings.LastJoin = code;
        GameSettings.Save();
        Net.Join(code);
    }

    /// <summary>
    /// Host: someone joined while the run is under way (new, or back after a drop). They're told where the party is (the run, the level and
    /// its seed, the time, the worldly relics carried, and whether to pick up as they were); then brought up to date (every creature, the
    /// chests opened, the trials shut). A newcomer starts at the party's level (the host's), with that many picks to take.
    /// </summary>
    public void LateJoiner(int peer, int token)
    {
        if (!Net.IsHost || !Net.InRun) return;
        var w = new NetOut(Net.Msg.JoinRun);
        w.Int(_runSeed).Byte((byte)_levelBiome).Int(G.Depth).Int(_seed).Float(G.RunTime).Bool(G.Shop).Bool(IsThisRun(token)).Int(G.Player?.Level ?? 1);
        var worldly = RunRelics.WorldlyCarried().ToList();
        w.Int(worldly.Count);
        foreach (var (who, id) in worldly) w.Int(who).Str(id);
        Net.SendTo(peer, w, true);
        NetSync.CatchUp(peer);
        SendOpenTrials(peer);
        NetSync.SpawnAvatars();
    }

    /// <summary>
    /// This game joined a run under way (the host's <see cref="LateJoiner"/>): the level the party is in, built here from the same seed, and
    /// this game's hero in it, picking up as they were if they dropped out of this very run as this hero, or else a fresh one brought up to
    /// the party's level.
    /// </summary>
    public void JoinRunInProgress(int runSeed, BiomeId biome, int depth, int seed, float runTime, bool shop, bool resume, int partyLevel, List<(int, string)> relics)
    {
        if (!Net.Online || Net.InRun) return;
        _onlineMenu.Visible = false;
        _pauseMenu.Visible = false;
        _upgradeMenu.Visible = false;
        if (_settingsMenu.Visible) { _settingsMenu.Visible = false; GameSettings.Save(); }
        _grid.Visible = false;
        _dropped ??= LoadDropped();
        var back = resume && _dropped != null && _dropped.RunSeed == runSeed && _dropped.Hero == (Net.Mine?.Hero ?? G.Hero) ? _dropped : null;
        _resumeInPlace = back != null && _reconnecting && back.Stats != null;
        _reconnecting = false;
        _reconnectRetryT = -1f;
        Net.InRun = true;
        _onlineRun = true;
        _waitingAt = null;
        _allDownT = 0;
        _descending = false;
        NetSync.ClearExits();
        Engine.TimeScale = 1;
        _hitStopLeft = 0;
        G.Hero = Net.Mine?.Hero ?? G.Hero;
        _wasHost = false;
        _runSeed = runSeed;
        _levelBiome = (int)biome;
        G.Depth = depth;
        G.Shop = shop;
        G.Biome = shop ? Biomes.ShopOf(Biomes.Get(biome)) : Biomes.Get(biome);
        _seed = seed;
        _pendingTreasure.Clear();
        // (what was carried over from the run so far, when it's this same game picking up)
        int embers = _runEmbers; string finds = _runFinds; var metaAtStart = _metaAtStart; int unlocks = Meta.RunUnlocks;
        var gridFinds = new Dictionary<string, int>(GridFinds.Run);
        _resumeFrom = back;
        _joinRelics = relics;
        try { BuildLevel(seed, freshPlayer: true); }
        finally { _resumeFrom = null; _joinRelics = null; }
        StartPlaying();
        G.RunTime = _runTime = runTime;
        var p = G.Player;
        if (back != null)
        {
            if (_resumeInPlace)
            {
                Meta.Runs--; // (the same run, picked back up: not another)
                _runEmbers = embers; _runFinds = finds; _metaAtStart = metaAtStart; Meta.RunUnlocks = unlocks;
                foreach (var (k, n) in gridFinds) GridFinds.Run[k] = n;
            }
            Upgrades.Replaying = true;
            try
            {
                // (the very stats object: the relics carried are noted again)
                if (back.Stats != null) foreach (var id in back.Relics) RunRelics.Take(id);
                // (after a restart: everything taken, and every level gained, played again in order on a fresh hero)
                else foreach (var e in back.Log)
                {
                    if (e == PlayerStats.LevelMark) p.ReplayLevelUp();
                    else if (Upgrades.Find(e) is Upgrade u) Upgrades.Apply(u, p.Stats, p);
                }
            }
            finally { Upgrades.Replaying = false; }
            // (the others told what this hero has, under the peer it is now)
            foreach (var (id, n) in p.Stats.Stacks) for (int k = 0; k < n; k++) NetSync.SendCard(id);
            if (back.Stats == null)
            {
                p.Level = back.Level; p.Xp = back.Xp; p.Kills = back.Kills; p.Potions = back.Potions; p.Keys = back.Keys;
                p.Gold = back.Gold; p.GoldFound = back.GoldFound; p.GoldSpent = back.GoldSpent; p.Rerolls = back.Rerolls; p.Trades = back.Trades;
                p.PendingMilestones = back.Milestones; p.PendingPicks = back.Picks;
                p.SetVitalForce(back.VitalForce);
                p.SetAlimus(back.Alimus);
                p.Fuel = back.Fuel;
                p.SyncCharges();
            }
            p.Hp = Math.Clamp(back.Hp, 1f, p.Stats.MaxHp);
            _hud.ShowNotice(_resumeInPlace ? "Reconnected" : "Back in the run, as you were", 4f);
            GD.Print($"[DaggerDeep] rejoined the run as {back.Hero}, level {p.Level}, {p.Stats.Stacks.Values.Sum()} cards ({(back.Stats != null ? "kept" : "replayed")})");
        }
        else
        {
            // (a newcomer catches up: the party's level, with its picks to take)
            if (partyLevel > 1) p.GainLevels(partyLevel - 1);
            _hud.ShowNotice(depth > 0 ? $"Joined the run at depth {depth}" : "Joined the run", 5f);
        }
        _resumeInPlace = false;
        _rejoinSaveT = 0f;
        RememberHero();
        Net.RejoinToken = 0;
    }
}
