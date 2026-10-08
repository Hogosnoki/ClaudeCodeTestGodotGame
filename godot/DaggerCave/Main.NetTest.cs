using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

/// <summary>
/// --nettest=host / --nettest=join [--netaddr=IP]: two copies of the game on one machine (see
/// tools/nettest.sh), one hosting and one joining, checking what travels between them: the lobby,
/// the same cave in both, a creature of the host's struck from the other game, a creature's blow
/// taken by the other game's hero, shared experience, a chest opened by whoever reaches it, a
/// fallen friend brought back, going down an exit together, and the run ending for everyone.
/// </summary>
public partial class Main
{
    private string _netTest = "", _netAddr = "127.0.0.1";
    /// <summary>--ntshots=DIR: screenshots of the test's moments (the lobby, the fight, a revive, the exit, the camp).</summary>
    private string _ntShots = "";
    private readonly HashSet<string> _ntShotsTaken = new();
    private float _ntT, _ntPhaseT;
    private int _ntPhase;
    private bool _ntOk = true, _ntDone;
    private readonly List<string> _ntInbox = new();
    private Enemy _ntGolem;
    private Portal _ntPortal;
    private float _ntMark;
    private long _ntXpMark;
    private int _ntSide = 1;
    private float _ntLobbyT;
    /// <summary>This frame's real time (a window-less copy runs as fast as it can, far above 60 frames a second).</summary>
    private float _ntDt;
    private bool _ntReady;
    private float _ntChooseT;
    private PlayerInput _ntInput;
    private int _ntSub;
    private Chest _ntChest;
    private string _ntCards = "";

    private void NtCheck(string what, bool ok)
    {
        GD.Print($"[nettest] {(ok ? "ok  " : "FAIL")} {what}");
        _ntOk &= ok;
    }

    /// <summary>A message to the other copy's script.</summary>
    private static void NtSay(string text)
    {
        var w = new NetOut(Net.Msg.Test);
        w.Str(text);
        Net.SendAll(w, true);
    }

    public void OnNetTest(int from, string text) => _ntInbox.Add(text);

    /// <summary>Takes a message from the other copy that starts with <paramref name="head"/>.</summary>
    private bool NtGot(string head, out string rest)
    {
        rest = "";
        for (int k = 0; k < _ntInbox.Count; k++)
        {
            var m = _ntInbox[k];
            if (m != head && !m.StartsWith(head + " ")) continue;
            _ntInbox.RemoveAt(k);
            rest = m.Length > head.Length ? m[(head.Length + 1)..] : "";
            return true;
        }
        return false;
    }

    private void NtNext() { _ntPhase++; _ntPhaseT = 0; }
    private bool _ntSawBarrier;
    private int _ntRogueStep, _ntVault;
    private float _ntVaultT;

    /// <summary>Where the test's exit leads (a Fossil Graveyard: big chambers, a camera to watch).</summary>
    private const int NtDepth = 5;
    private float _ntArriveT, _ntCamWorst, _ntCamLogT;

    /// <summary>After going down: how far the view's centre strays from this game's own hero.</summary>
    private void NtWatchCamera(float dt)
    {
        if (G.Depth != NtDepth || G.Player == null || _cam == null) return;
        _ntArriveT += dt;
        var half = ViewHalf(0);
        var off = _cam.GetScreenCenterPosition() - G.Player.GlobalPosition;
        float frac = Math.Max(Math.Abs(off.X) / half.X, Math.Abs(off.Y) / half.Y);
        if (_ntArriveT > 1.2f) _ntCamWorst = Math.Max(_ntCamWorst, frac);
        _ntCamLogT -= dt;
        if (_ntCamLogT <= 0 && _ntArriveT < 4f)
        {
            _ntCamLogT = 0.5f;
            GD.Print($"[nettest] camera t={_ntArriveT:0.0}: hero {G.Player.GlobalPosition.Round()} dead {G.Player.Dead} centre {_cam.GetScreenCenterPosition().Round()} ({frac:0.00} of the half view) current {_cam.IsCurrent()} boss {(ActiveBoss != null ? ActiveBoss.GlobalPosition.Round().ToString() : "-")}");
        }
    }

    /// <summary>A screenshot of this moment (once), with --ntshots.</summary>
    private void NtShot(string name)
    {
        if (_ntShots == "" || !_ntShotsTaken.Add(name)) return;
        GetViewport().GetTexture().GetImage().SavePng($"{_ntShots}/{name}.png");
        GD.Print($"[nettest] screenshot {name}");
    }

    // --onlineshot=DIR: the title (with its online button), the online menu, and a lobby as host
    private string _onlineShot = "";
    private int _onlineShotFrame;

    private void OnlineShotTick()
    {
        int f = ++_onlineShotFrame;
        void Shot(string n) { GetViewport().GetTexture().GetImage().SavePng($"{_onlineShot}/{n}.png"); GD.Print($"[onlineshot] {n}"); }
        switch (f)
        {
            case 40: Shot("title"); OpenOnlineMenu(); break;
            case 55: Shot("online_menu"); Net.Host(); break;
            case 110: Shot("online_lobby"); break;
            case 112: Net.Leave(); SafeQuit.Request(this); break;
        }
    }

    private void NtFail(string what)
    {
        NtCheck(what, false);
        NtEnd();
    }

    private void NtEnd()
    {
        if (_ntDone) return;
        _ntDone = true;
        GD.Print(_ntOk ? $"[nettest] {_netTest} PASS" : $"[nettest] {_netTest} FAIL");
        SafeQuit.Request(this, _ntOk ? 0 : 1);
    }

    private static long XpTotal(Player p) => p.Level * 100000L + p.Xp;

    private static Player OtherHero() => G.Players.FirstOrDefault(h => h != G.Player && GodotObject.IsInstanceValid(h) && h.IsRemote);

    /// <summary>The level's own chests, its vault's gate and its hidden keys: which, and where (the
    /// same in every game, or the games have split).</summary>
    private string ChestSig()
    {
        string Sig(IEnumerable<Node2D> nodes) => string.Join(";", nodes
            .Select(n => (id: NetSync.IdOf(n), n.Position))
            .Where(x => x.id > 0 && x.id < 1_000_000)
            .OrderBy(x => x.id)
            .Select(x => $"{x.id}@{(int)x.Position.X},{(int)x.Position.Y}"));
        var kids = _world.GetChildren();
        return $"{G.Depth}/{G.Biome.Id}/{G.Cave.Seed}/{Sig(kids.OfType<Chest>())}"
             + $"/gate {Sig(kids.OfType<VaultGate>())}/keys {Sig(kids.OfType<KeyPickup>().Where(k => k.Stashed))}";
    }

    /// <summary>A hero that shrugs off stray creatures for the length of the test.</summary>
    private void NtTough()
    {
        var p = G.Player;
        p.Stats.MaxHp = 5000;
        p.Hp = 5000;
        // one frame's press at a time (held flags stay as a step set them)
        p.InputOverride = () =>
        {
            var i = _ntInput;
            _ntInput.Attack = _ntInput.Ability = _ntInput.Ability2 = _ntInput.Dodge = _ntInput.Jump = _ntInput.Potion = _ntInput.Interact = _ntInput.Rope = false;
            return i;
        };
    }

    private void BeginNetTest()
    {
        GetTree().Paused = true;
        _state = State.Title;
        _hud.Visible = false;
        GameSettings.PlayerName = _netTest == "host" ? "Hosty" : "Joiner";
        GD.Print($"[nettest] {_netTest}: {(_netTest == "host" ? "hosting" : $"joining {_netAddr}")} as the {G.Hero} (build {Net.Build})");
        if (_ntShots != "") _onlineMenu.Open();
        if (_netTest == "host") { if (!Net.Host()) NtFail($"hosting opens port {Net.Port} ({Net.Status})"); }
        else Net.Join(_netAddr);
    }

    private void NetTestTick(float dt)
    {
        if (_ntDone) return;
        // (real time: the checks wait on the other copy, which runs at its own pace)
        dt = (float)(dt / Math.Max(0.05, Engine.TimeScale));
        _ntDt = dt;
        _ntT += dt;
        _ntPhaseT += dt;
        if (_ntT > 240) { NtFail($"everything done within four minutes (stuck in phase {_ntPhase})"); return; }
        // (once this game's last check is in, the other closing the connection is just the end)
        if (_ntPhase > 0 && !Net.Online)
        {
            if (_ntFinished) NtEnd();
            else NtFail($"still connected (phase {_ntPhase}: {Net.Status})");
            return;
        }
        // the host keeps the cave clear of anything but the test's own creature
        if (Net.IsHost && Net.InRun)
            foreach (var e in G.Enemies.ToArray())
                if (!e.Puppet && e != _ntGolem && !e.IsQueuedForDeletion()) e.QueueFree();
        NtWatchCamera(dt);
        if (_netTest == "host") HostTestStep();
        else JoinTestStep();
    }

    private void HostTestStep()
    {
        var friend = OtherHero();
        switch (_ntPhase)
        {
            case 0:
                if (NtGot("ready", out _)) _ntReady = true;
                if (_ntReady && Net.Count == 2 && Net.AllPicked && Net.Peers.Values.Select(p => p.Hero).Distinct().Count() == 2)
                {
                    // (a moment for the lobby to show who's in, for its picture)
                    if (_ntShots != "" && _ntShotsTaken.Count == 0) { _ntLobbyT += _ntDt; if (_ntLobbyT < 1.5f) break; NtShot("nt_lobby"); }
                    NtCheck($"a friend joined the lobby, each with their own hero ({string.Join(", ", Net.Peers.Values.Select(p => $"{p.Name}: {p.Hero}"))})", true);
                    HostStartRun();
                    NtNext();
                }
                else if (_ntPhaseT > 60) NtFail("a friend joins the lobby");
                break;
            case 1:
                if (friend != null && _ntPhaseT > 2f)
                {
                    NtCheck($"the friend's hero is in the cave here, shown from their game ({friend.NetName}, the {friend.Hero}, {G.Players.Count} heroes)", friend.IsRemote && G.Players.Count == 2 && friend.Hero != G.Player.Hero);
                    NtTough();
                    NtSay("cave " + ChestSig());
                    NtNext();
                }
                else if (_ntPhaseT > 30) NtFail("the friend's hero appears in the host's cave");
                break;
            case 2:
            {
                if (NtGot("cave-ok", out var r)) NtCheck($"the friend's game built the same cave, with the same chests, vault and hidden keys ({r})", true);
                else if (NtGot("cave-bad", out r)) NtCheck($"the friend's game built the same cave ({r})", false);
                else { if (_ntPhaseT > 20) NtFail("the friend compares caves"); break; }
                // a golem beside the friend (held still), for them to strike
                _ntSide = G.Cave.IsSolid(friend.GlobalPosition + new Vector2(44, -6)) ? -1 : 1;
                _ntGolem = new Golem { Position = friend.GlobalPosition + new Vector2(_ntSide * 44, -6) };
                _ntGolem.SetMeta("test", true);
                _world.AddChild(_ntGolem);
                _ntGolem.Freeze(1000, hold: true);
                NtSay($"golem {_ntGolem.NetId} {_ntSide}");
                NtNext();
                break;
            }
            case 3:
                if (NtGot("swung", out _)) { _ntMark = _ntGolem.MaxHp; NtNext(); }
                else if (_ntPhaseT > 20) NtFail("the friend swings at the golem");
                break;
            case 4:
                NtShot("nt_golem");
                if (_ntPhaseT < 0.6f) break;
                NtCheck($"the friend's swing lands on the host's golem (hp {_ntMark:0} -> {_ntGolem.Hp:0}), credited to them (last struck by {_ntGolem.LastAttacker})",
                    _ntGolem.Hp < _ntMark - 1 && _ntGolem.LastAttacker == friend?.NetOwner);
                if (friend != null && friend.Hero == HeroKind.Rogue)
                {
                    // a Rogue's thrown dagger flies here too (a harmless copy: their game dealt its blow)
                    var dagger = _world.GetChildren().OfType<ThrownDagger>().FirstOrDefault(d => d.Harmless && d.Thrower == friend);
                    NtCheck($"the friend's thrown dagger shows here, stuck in the golem ({dagger?.State})", dagger != null && dagger.State == ThrownDagger.Phase.Stuck);
                    NtCheck($"the throw's blow (dealt in their game) reached the golem here ({_ntMark - _ntGolem.Hp:0} in all)", _ntMark - _ntGolem.Hp >= Tune.Rogue.ThrowDamage - 0.5f);
                    NtCheck($"and their vanishing shows here too (hidden {friend.Hidden})", friend.Hidden);
                }
                // the golem's blow lands on the friend's hero: their game takes it
                friend?.Hurt(12f, _ntGolem.GlobalPosition, 100f, _ntGolem);
                NtSay("hurt 12");
                NtNext();
                break;
            case 5:
            {
                if (NtGot("hurt-ok", out var r)) NtCheck($"a creature's blow on the friend's hero is taken in their game ({r})", true);
                else if (NtGot("hurt-bad", out r)) NtCheck($"a creature's blow on the friend's hero is taken in their game ({r})", false);
                else { if (_ntPhaseT > 20) NtFail("the friend reports the blow"); break; }
                _ntXpMark = XpTotal(G.Player);
                NtSay("killing");
                _ntGolem.Hurt(1e6f, new Vector2(_ntSide * 50, 0), _ntGolem.GlobalPosition);
                NtNext();
                break;
            }
            case 6:
            {
                if (_ntPhaseT < 4f) break;
                if (!NtGot("xp", out var r)) { if (_ntPhaseT > 20) NtFail("the friend reports their experience"); break; }
                NtCheck($"the golem's experience is shared: the host's {_ntXpMark} -> {XpTotal(G.Player)}, the friend's {r}", XpTotal(G.Player) > _ntXpMark && r.EndsWith("up"));
                // a chest right at the friend's feet (the host's hero well away from it)
                var away = G.Cave.Spawns.Where(s => s.Kind == SpawnKind.Ground && s.Pos.DistanceTo(friend.GlobalPosition) > 250).Select(s => s.Pos).FirstOrDefault();
                if (away != default) { G.Player.GlobalPosition = away + new Vector2(0, -16); G.Player.Velocity = Vector2.Zero; }
                SpawnChest(friend.GlobalPosition + new Vector2(0, 12));
                NtSay("chest");
                NtNext();
                break;
            }
            case 7:
            {
                // the friend looks in the chest and leaves it: here it's closed again, with the
                // same cards; the host looks in, is offered the same cards, and takes one
                _ntChest ??= _world.GetChildren().OfType<Chest>().OrderBy(c => c.GlobalPosition.DistanceTo(friend.GlobalPosition)).FirstOrDefault();
                var chest = _ntChest;
                string Cards() => chest?.Cards != null ? string.Join(",", chest.Cards) : "";
                if (_ntSub == 0)
                {
                    if (NtGot("chest-bad", out var bad)) { NtCheck($"the friend looks in the chest ({bad})", false); NtSay("fall"); NtNext(); break; }
                    if (!NtGot("chest-left", out var r)) { if (_ntPhaseT > 20) NtFail("the friend looks in the chest"); break; }
                    NtCheck($"the friend left the chest: here it's closed again with the same cards (theirs {r}, mine {Cards()}, spent {chest?.Open}, looked at by {chest?.LookingBy})",
                        chest != null && !chest.Open && r != "" && Cards() == r && chest.LookingBy == 0);
                    _ntCards = r;
                    if (chest != null) { G.Player.GlobalPosition = chest.GlobalPosition + new Vector2(0, -13); G.Player.Velocity = Vector2.Zero; }
                    _ntSub = 1; _ntChooseT = 0;
                    break;
                }
                if (_ntSub == 1)
                {
                    if (_state != State.Choosing && chest != null && chest.Reaches(G.Player.GlobalPosition)) _ntInput.Interact = true;
                    _ntChooseT = _state == State.Choosing ? _ntChooseT + _ntDt : 0;
                    if (_ntChooseT > 0.6f)
                    {
                        NtCheck($"the host looks in and is offered the very same cards ({Cards()})", Cards() == _ntCards);
                        // take one if one is the host's to take; otherwise leave it for the friend
                        _ntSub = _upgradeMenu.HasOpenCard ? 2 : 3;
                        if (_ntSub == 2) _upgradeMenu.ChooseFirstOpen();
                        else { _upgradeMenu.ChooseLeave(); NtSay("chest-yours"); }
                    }
                    else if (_ntPhaseT > 30) NtFail("the host looks in the chest");
                    break;
                }
                if (_ntSub == 2)
                {
                    // (a press the menu wasn't ready for is simply made again)
                    if (_state == State.Choosing) { if (_ntChooseT < 3f) { _ntChooseT += _ntDt; _upgradeMenu.ChooseFirstOpen(); } break; }
                    NtCheck($"the host took a card: the chest is spent (spent {chest?.Open}, upgrades [{string.Join(",", G.Player.Stats.Stacks.Keys)}])", chest != null && chest.Open && G.Player.Stats.Stacks.Count > 0);
                    NtSay("chest-taken");
                    _ntSub = 3;
                    break;
                }
                // the friend's copy is spent as well
                if (NtGot("chest-ok", out var ok))
                {
                    NtCheck($"the friend's copy of the chest is spent too, and so is the host's ({ok}; spent here {chest?.Open}, state {_state})", chest != null && chest.Open && _state == State.Playing);
                    _ntSub = 0; _ntChest = null;
                    // a barrier (Guardian's Charge) and a warding mending (Slow Mending) for the friend
                    if (friend != null) { friend.GiveBarrier(20, 4); friend.GiveMending(12, 3, Tune.Vitalist.WardingShare); }
                    _ntSawBarrier = false;
                    NtSay("boons");
                    NtSay("fall");
                    NtNext();
                }
                else if (NtGot("chest-bad", out var bad2)) { NtCheck($"the friend's copy of the chest is spent too ({bad2})", false); _ntSub = 0; NtSay("fall"); NtNext(); }
                else if (_ntPhaseT > 60) NtFail("the friend confirms the chest is spent");
                break;
            }
            case 8:
                // the friend's game holds the boons; this one sees the barrier by their hero's flags
                if (friend != null && friend.Barriered) _ntSawBarrier = true;
                if (NtGot("boon-ok", out var boon)) NtCheck($"boons given here reach the friend's hero in their game ({boon})", true);
                else if (NtGot("boon-bad", out boon)) NtCheck($"boons given here reach the friend's hero in their game ({boon})", false);
                if (friend != null && friend.Dead && _ntPhaseT > 1f)
                {
                    NtCheck($"and their barrier shows here too (seen {_ntSawBarrier})", _ntSawBarrier);
                    NtCheck("the friend's hero fell, and shows here as down", true);
                    G.Player.GlobalPosition = friend.GlobalPosition + new Vector2(0, -4);
                    G.Player.Velocity = Vector2.Zero;
                    _ntInput = new PlayerInput { InteractHeld = true };
                    NtNext();
                }
                else if (_ntPhaseT > 20) NtFail("the friend's hero falls");
                break;
            case 9:
                if (_ntPhaseT > 1.2f) NtShot("nt_revive");
                if (NtGot("revived", out var rv))
                {
                    NtCheck($"holding interact beside the fallen friend brought them back ({rv})", true);
                    _ntInput = default;
                    // an exit, at the host's feet
                    SpawnPortal(G.Player.GlobalPosition + new Vector2(0, -17), (int)BiomeId.Fossils, NtDepth, "test");
                    _ntPortal = _world.GetChildren().OfType<Portal>().LastOrDefault();
                    NtSay($"portal {NetSync.IdOf(_ntPortal)}");
                    NtNext();
                }
                else if (_ntPhaseT > 20) NtFail("the friend is revived");
                break;
            case 10:
                if (_ntPhaseT > 1.2f && _waitingAt == null && G.Depth == 0) _ntPortal?.Enter();
                if (_waitingAt != null && G.Depth == 0) NtShot("nt_exit");
                if (G.Depth == NtDepth)
                {
                    NtCheck("with both heroes in the exit, everyone went down together", true);
                    NtSay("cave " + ChestSig());
                    NtNext();
                }
                else if (_ntPhaseT > 30) NtFail("both go down the exit together");
                break;
            case 11:
            {
                if (NtGot("cave-ok", out var r)) NtCheck($"the next level is the same in both games ({r})", true);
                else if (NtGot("cave-bad", out r)) NtCheck($"the next level is the same in both games ({r})", false);
                else { if (_ntPhaseT > 30) NtFail("the friend compares the next level"); break; }
                NtNext();
                break;
            }
            case 12:
            {
                if (_ntArriveT < 3.5f) break;
                if (_ntVault == 0)
                {
                    if (!NtGot("cam", out var fc)) { if (_ntPhaseT > 20) NtFail("the friend checks their view"); break; }
                    NtCheck($"down there, this game's view stays on its own hero (at worst {_ntCamWorst:0.00} of the way to the edge)", _ntCamWorst < 0.6f);
                    NtCheck($"and the friend's view stays on theirs ({fc})", fc.StartsWith("ok"));
                    NtTough();
                    _ntVault = 3;
                    // keys and the vault: a key at the friend's feet (the host's hero well away
                    // from it; the host says who holds it)
                    if (Gate != null && friend != null)
                    {
                        var away = G.Cave.Spawns.Where(s => s.Kind == SpawnKind.Ground && s.Pos.DistanceTo(friend.GlobalPosition) > 250).Select(s => s.Pos).FirstOrDefault();
                        if (away != default) { G.Player.GlobalPosition = away + new Vector2(0, -16); G.Player.Velocity = Vector2.Zero; }
                        NetSync.Scope++;
                        try { G.Spawn(new KeyPickup { Position = friend.GlobalPosition + new Vector2(0, -6) }); }
                        finally { NetSync.Scope--; }
                        NtSay("vault-key");
                        _ntVault = 1;
                        _ntPhaseT = 0;
                    }
                    else NtCheck($"(this level has a vault to open: {Gate != null})", false);
                }
                if (_ntVault == 1)
                {
                    if (NtGot("vault-open", out var vr)) NtCheck($"the friend picked up the key and opened the vault's gate with it: open here too ({Gate?.Opened}; {vr})", Gate != null && Gate.Opened && vr.StartsWith("ok"));
                    else if (NtGot("vault-bad", out vr)) NtCheck($"the friend opens the vault with the key ({vr})", false);
                    else { if (_ntPhaseT > 30) NtFail("the friend opens the vault"); break; }
                    _ntVault = 3;
                }
                // the friend's tools (a rope let down, a dagger in a wall, an updraft aimed, a heaving swing) as they arrive here
                if (_ntCoop < 100) { HostCoopStep(friend); break; }
                NtSay("all-fall");
                G.Player.GiveUp();
                NtNext();
                break;
            }
            case 13:
                if (_state == State.Dead && NtGot("over", out _))
                {
                    NtCheck("with everyone down, the run ended in both games", true);
                    if (_ntShots != "") NtShot("nt_camp");
                    // back to the lobby together, for one more (short) run
                    NtSay("lobby");
                    BackToLobby();
                    NtNext();
                }
                else if (_ntPhaseT > 20) NtFail($"the run ends for everyone (host state {_state})");
                break;
            case 14:
                if (!NtGot("lobby-ok", out _)) { if (_ntPhaseT > 20) NtFail("the friend comes back to the lobby"); break; }
                HostStartRun();
                NtNext();
                break;
            case 15:
            {
                // a fresh run at the cave mouth: step out into the daylight (and wait there for the friend)
                if (!Net.InRun || G.Cave?.Mouth is not Vector2 mouth || _ntPhaseT < 1f) { if (_ntPhaseT > 20) NtFail("a second run starts"); break; }
                NtWalkOut(mouth);
                NtSay("mouth");
                NtNext();
                break;
            }
            case 16:
                if (!Net.InRun && _state == State.Title && _onlineMenu.Visible)
                {
                    NtCheck("walking out of the cave mouth together ends the run: everyone is back in the lobby", true);
                    _ntFinished = true;
                    NtSay("bye");
                    NtNext();
                }
                else if (_ntPhaseT > 20) NtFail($"everyone walks out together (in run {Net.InRun}, state {_state}, lobby {_onlineMenu.Visible}, waiting {_waitingAt != null})");
                break;
            case 17:
                NtEnd();
                break;
        }
    }

    private bool _ntFinished;

    /// <summary>This game's hero steps up to the cave mouth and takes the way out.</summary>
    private void NtWalkOut(Vector2 mouth)
    {
        var p = G.Player;
        p.GlobalPosition = mouth + new Vector2(0, -19);
        p.Velocity = Vector2.Zero;
        foreach (var n in _world.GetChildren())
            if (n is Portal { Outside: true } way) { way.Enter(); break; }
    }

    private void JoinTestStep()
    {
        switch (_ntPhase)
        {
            case 0:
                // in, with the hero picked on the title (the host hands out a free one first, then takes the request)
                if (Net.Mine is { Picked: true } me && (me.Hero == G.Hero || Net.TakenBy(G.Hero)?.Id == 1))
                {
                    NtCheck($"joined the host's lobby ({Net.Peer(1)?.Name}), with the hero I picked (the {me.Hero})", true);
                    NtSay("ready");
                    NtNext();
                }
                else if (_ntPhaseT > 60) NtFail($"join the host's lobby with my hero ({Net.Status}, {Net.Mine?.Hero})");
                break;
            case 1:
                if (Net.InRun && _state == State.Playing && G.Players.Count == 2 && _ntPhaseT > 1f)
                {
                    NtCheck("the host started the run: both heroes in the cave here", OtherHero() != null);
                    NtTough();
                    NtNext();
                }
                else if (_ntPhaseT > 60) NtFail("the host starts the run");
                break;
            case 2:
                if (NtGot("cave", out var theirs))
                {
                    string mine = ChestSig();
                    bool same = mine == theirs;
                    NtCheck($"the same cave as the host's ({mine})", same);
                    NtSay(same ? $"cave-ok {mine}" : $"cave-bad mine {mine} vs {theirs}");
                    NtNext();
                }
                else if (_ntPhaseT > 30) NtFail("the host sends its cave");
                break;
            case 3:
            {
                if (!NtGot("golem", out var r)) { if (_ntPhaseT > 30) NtFail("the host makes a golem"); break; }
                var parts = r.Split(' ');
                _ntMark = int.Parse(parts[0]);
                _ntSide = int.Parse(parts[1]);
                NtNext();
                break;
            }
            case 4:
            {
                var golem = G.Enemies.FirstOrDefault(e => e.Puppet && e.NetId == (int)_ntMark);
                if (golem == null) { if (_ntPhaseT > 10) NtFail("the host's golem appears here"); break; }
                if (_ntPhaseT < 0.8f) break; // (a moment for its position to arrive)
                NtCheck($"the host's golem appears here, a copy ({golem.GetType().Name} #{golem.NetId}, {golem.GlobalPosition.DistanceTo(G.Player.GlobalPosition):0} px away)", true);
                _ntInput = new PlayerInput { Attack = true, Aim = new Vector2(_ntSide, 0) };
                NtNext();
                break;
            }
            case 5:
                if (_ntPhaseT < 0.5f) break;
                // (a Rogue throws a dagger into the golem too, then vanishes)
                if (G.Player.Stats.Hero == HeroKind.Rogue)
                {
                    if (_ntRogueStep == 0) { _ntInput = new PlayerInput { Ability = true, Aim = new Vector2(_ntSide, 0) }; _ntRogueStep = 1; }
                    else if (_ntRogueStep == 1 && _ntPhaseT > 0.9f) { _ntInput = new PlayerInput { Dodge = true }; _ntRogueStep = 2; }
                    if (_ntPhaseT < 1.2f) break;
                }
                _ntMark = G.Player.Hp;
                NtSay("swung");
                NtNext();
                break;
            case 6:
                if (NtGot("hurt", out var amount))
                {
                    float want = float.Parse(amount, System.Globalization.CultureInfo.InvariantCulture) * (1f - G.Player.Stats.DamageReduction) * G.Player.Stats.DamageTakenMult;
                    float took = _ntMark - G.Player.Hp;
                    bool ok = Math.Abs(took - want) < 0.6f;
                    NtCheck($"the host's golem struck my hero, and my game took it ({took:0.0} of {want:0.0})", ok);
                    NtSay(ok ? $"hurt-ok took {took:0.0}" : $"hurt-bad took {took:0.0} of {want:0.0}");
                    _ntXpMark = XpTotal(G.Player);
                    NtNext();
                }
                else if (_ntPhaseT > 20) NtFail("the host's golem strikes");
                break;
            case 7:
                if (NtGot("killing", out _)) NtNext();
                else if (_ntPhaseT > 20) NtFail("the host kills the golem");
                break;
            case 8:
                if (_ntPhaseT < 3.5f) break;
                NtCheck($"the golem's experience reached me too ({_ntXpMark} -> {XpTotal(G.Player)})", XpTotal(G.Player) > _ntXpMark);
                NtSay($"xp {_ntXpMark} -> {XpTotal(G.Player)} {(XpTotal(G.Player) > _ntXpMark ? "up" : "same")}");
                NtNext();
                break;
            case 9:
                if (NtGot("chest", out _)) NtNext();
                else if (_ntPhaseT > 30) NtFail("the host makes a chest");
                break;
            case 10:
                // chests open with the interact button: press it standing at the one at my feet
                if (_state != State.Choosing && Chest.At(G.Player.GlobalPosition) != null) _ntInput.Interact = true;
                // (the cards are offered a moment after, and take a press only after a beat)
                _ntChooseT = _state == State.Choosing ? _ntChooseT + _ntDt : 0;
                if (_ntChooseT > 0.6f)
                {
                    NtCheck($"the chest at my feet shows me its cards (choosing {G.Player.Choosing}, safe {G.Player.Invulnerable})", G.Player.Choosing && G.Player.Invulnerable);
                    _ntChest = Chest.At(G.Player.GlobalPosition) ?? Chest.All.OrderBy(c => c.GlobalPosition.DistanceTo(G.Player.GlobalPosition)).FirstOrDefault();
                    _upgradeMenu.ChooseLeave();
                    _ntSub = 0;
                    NtNext();
                }
                else if (_ntPhaseT > 15) { NtCheck($"the chest opens for me ({_state})", false); NtSay("chest-bad no pick"); _ntPhase = 11; NtNext(); }
                break;
            case 11:
            {
                var chest = _ntChest;
                string Cards() => chest?.Cards != null ? string.Join(",", chest.Cards) : "";
                if (_ntSub == 0)
                {
                    // left: it closes again, keeping its cards (for the host to look at)
                    if (_state == State.Choosing && _ntPhaseT < 3f) { _upgradeMenu.ChooseLeave(); break; }
                    NtCheck($"leaving it closes it again (state {_state}, spent {chest?.Open}, cards {Cards()})", _state == State.Playing && chest != null && !chest.Open && Cards() != "");
                    NtSay($"chest-left {Cards()}");
                    _ntSub = 1;
                    break;
                }
                if (_ntSub == 1)
                {
                    if (NtGot("chest-taken", out _))
                    {
                        NtCheck($"the host took a card: my copy of the chest is spent too (spent {chest?.Open})", chest != null && chest.Open);
                        NtSay(chest != null && chest.Open ? "chest-ok spent here" : "chest-bad still closed here");
                        NtNext();
                    }
                    else if (NtGot("chest-yours", out _)) { _ntSub = 2; _ntChooseT = 0; }
                    else if (_ntPhaseT > 60) NtFail("the host takes from the chest");
                    break;
                }
                if (_ntSub == 2)
                {
                    // none of the cards was the host's: take one myself
                    if (_state != State.Choosing && chest != null && chest.Reaches(G.Player.GlobalPosition)) _ntInput.Interact = true;
                    _ntChooseT = _state == State.Choosing ? _ntChooseT + _ntDt : 0;
                    if (_ntChooseT > 0.6f) { _upgradeMenu.ChooseFirstOpen(); _ntSub = 3; }
                    else if (_ntPhaseT > 60) NtFail("I take from the chest");
                    break;
                }
                if (_state == State.Choosing) { _upgradeMenu.ChooseFirstOpen(); break; }
                NtCheck($"I took a card and the chest is spent (spent {chest?.Open}, upgrades [{string.Join(",", G.Player.Stats.Stacks.Keys)}])", chest != null && chest.Open);
                NtSay(chest != null && chest.Open ? "chest-ok taken here" : "chest-bad still closed here");
                NtNext();
                break;
            }
            case 12:
                if (NtGot("boons", out _))
                {
                    var hero = G.Player;
                    bool ok = hero.BarrierHp > 19.9f && hero.Mended;
                    NtCheck($"the host's boons reached my hero (barrier {hero.BarrierHp:0}, mending {hero.Mended})", ok);
                    NtSay($"{(ok ? "boon-ok" : "boon-bad")} barrier {hero.BarrierHp:0}, mending {hero.Mended}");
                }
                // (a beat for the barrier to show in the host's game before falling)
                if (_ntPhaseT > 0.6f && NtGot("fall", out _)) { G.Player.GiveUp(); NtNext(); }
                else if (_ntPhaseT > 30) NtFail("the host asks me to fall");
                break;
            case 13:
                if (!G.Player.Dead && _ntPhaseT > 0.2f)
                {
                    NtCheck($"a friend brought me back (hp {G.Player.Hp:0} of {G.Player.Stats.MaxHp:0})", G.Player.Hp > 0);
                    NtSay($"revived hp {G.Player.Hp:0}");
                    NtNext();
                }
                else if (_ntPhaseT > 20) NtFail("I'm brought back");
                break;
            case 14:
            {
                if (!NtGot("portal", out var r)) { if (_ntPhaseT > 30) NtFail("the host opens an exit"); break; }
                int id = int.Parse(r);
                _ntPortal = NetSync.Props.TryGetValue(id, out var n) ? n as Portal : null;
                if (_ntPortal == null) { NtFail($"the host's exit #{id} is here"); break; }
                NtCheck($"the host's exit is here too (#{id}, to depth {_ntPortal.Depth})", true);
                G.Player.GlobalPosition = _ntPortal.GlobalPosition + new Vector2(0, 17);
                G.Player.Velocity = Vector2.Zero;
                NtNext();
                break;
            }
            case 15:
                if (_ntPhaseT > 3f && _waitingAt == null && G.Depth == 0 && IsInstanceValid(_ntPortal)) _ntPortal.Enter();
                if (G.Depth == NtDepth) { NtCheck("went down the exit with the host", true); NtNext(); }
                else if (_ntPhaseT > 30) NtFail("go down the exit together");
                break;
            case 16:
                if (NtGot("cave", out var theirs2))
                {
                    string mine = ChestSig();
                    bool same = mine == theirs2;
                    NtCheck($"the next level is the same as the host's ({mine})", same);
                    NtSay(same ? $"cave-ok {mine}" : $"cave-bad mine {mine} vs {theirs2}");
                    NtTough();
                    NtNext();
                }
                else if (_ntPhaseT > 30) NtFail("the host sends the next level");
                break;
            case 17:
            {
                if (_ntArriveT < 3.5f) break;
                bool ok = _ntCamWorst < 0.6f;
                NtCheck($"down there, my view stays on my own hero (at worst {_ntCamWorst:0.00} of the way to the edge)", ok);
                NtSay($"cam {(ok ? "ok" : "bad")} {_ntCamWorst:0.00}");
                NtNext();
                break;
            }
            case 18:
            {
                // (first, the host drops a key at my feet for me to open the vault with)
                if (_ntVault == 0 && NtGot("vault-key", out _)) { _ntVault = 1; _ntVaultT = 0; }
                if (_ntVault == 1)
                {
                    _ntVaultT += _ntDt;
                    if (G.Player.Keys >= 1 && Gate != null)
                    {
                        NtCheck($"the key the host dropped at my feet is mine (keys {G.Player.Keys})", true);
                        // (on the gate's doorstep)
                        G.Player.GlobalPosition = Gate.GlobalPosition + new Vector2(-Gate.Side * 14, -14);
                        G.Player.Velocity = Vector2.Zero;
                        _ntVault = 2; _ntVaultT = 0;
                    }
                    else if (_ntVaultT > 10) { NtCheck($"the key comes to me (keys {G.Player.Keys}, vault {Gate != null})", false); NtSay("vault-bad no key"); _ntVault = 3; }
                    break;
                }
                if (_ntVault == 2)
                {
                    _ntVaultT += _ntDt;
                    if (Gate != null && !Gate.Opened && _ntVaultT > 0.3f) _ntInput.Interact = true;
                    if (Gate != null && Gate.Opened)
                    {
                        bool ok = G.Player.Keys == 0;
                        NtCheck($"my key opened the vault's gate (open {Gate.Opened}), and it's spent (keys {G.Player.Keys})", ok);
                        NtSay(ok ? "vault-open ok, keys 0" : $"vault-bad keys {G.Player.Keys}");
                        _ntVault = 3;
                    }
                    else if (_ntVaultT > 10) { NtCheck("my key opens the vault's gate", false); NtSay("vault-bad still shut"); _ntVault = 3; }
                    break;
                }
                // (then the host's turn to see this hero's tools used)
                if (_ntVault == 3 && _ntCoop < 100) { JoinCoopStep(); break; }
                if (NtGot("all-fall", out _)) { G.Player.GiveUp(); NtNext(); }
                else if (_ntPhaseT > 60) NtFail("the host asks everyone to fall");
                break;
            }
            case 19:
                if (_state == State.Dead)
                {
                    NtCheck("with everyone down, the host ended the run here too", true);
                    NtSay("over");
                    NtNext();
                }
                else if (_ntPhaseT > 20) NtFail($"the run ends ({_state})");
                break;
            case 20:
                if (!NtGot("lobby", out _)) { if (_ntPhaseT > 20) NtFail("the host goes back to the lobby"); break; }
                BackToLobby();
                NtSay("lobby-ok");
                NtNext();
                break;
            case 21:
            {
                if (!Net.InRun || G.Cave?.Mouth is not Vector2 mouth || !NtGot("mouth", out _)) { if (_ntPhaseT > 25) NtFail("the host starts a second run and heads for the way out"); break; }
                NtWalkOut(mouth);
                NtNext();
                break;
            }
            case 22:
                if (!Net.InRun && _state == State.Title && _onlineMenu.Visible)
                {
                    NtCheck("we walked out of the cave mouth together: back in the lobby, the run over", true);
                    _ntFinished = true;
                    NtNext();
                }
                else if (_ntPhaseT > 20) NtFail($"we walk out together (in run {Net.InRun}, state {_state}, lobby {_onlineMenu.Visible})");
                break;
            case 23:
                if (NtGot("bye", out _) || _ntPhaseT > 10) NtEnd();
                break;
        }
    }
}
