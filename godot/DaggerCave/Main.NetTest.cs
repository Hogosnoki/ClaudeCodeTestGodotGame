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
    private bool _ntReady;
    private float _ntChooseT;
    private PlayerInput _ntInput;

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

    /// <summary>The level's own chests: which, and where (the same in every game, or the games have split).</summary>
    private string ChestSig()
    {
        var chests = _world.GetChildren().OfType<Chest>()
            .Select(c => (id: NetSync.IdOf(c), c.Position))
            .Where(x => x.id > 0 && x.id < 1_000_000)
            .OrderBy(x => x.id)
            .Select(x => $"{x.id}@{(int)x.Position.X},{(int)x.Position.Y}");
        return $"{G.Depth}/{G.Biome.Id}/{G.Cave.Seed}/{string.Join(";", chests)}";
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
            _ntInput.Attack = _ntInput.Ability = _ntInput.Ability2 = _ntInput.Dodge = _ntInput.Jump = _ntInput.Potion = _ntInput.Interact = false;
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
        _ntT += dt;
        _ntPhaseT += dt;
        if (_ntT > 240) { NtFail($"everything done within four minutes (stuck in phase {_ntPhase})"); return; }
        if (_ntPhase > 0 && !Net.Online) { NtFail($"still connected (phase {_ntPhase}: {Net.Status})"); return; }
        // the host keeps the cave clear of anything but the test's own creature
        if (Net.IsHost && Net.InRun)
            foreach (var e in G.Enemies.ToArray())
                if (!e.Puppet && e != _ntGolem && !e.IsQueuedForDeletion()) e.QueueFree();
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
                    if (_ntShots != "" && _ntShotsTaken.Count == 0) { _ntLobbyT += 1f / 60f; if (_ntLobbyT < 1.5f) break; NtShot("nt_lobby"); }
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
                if (NtGot("cave-ok", out var r)) NtCheck($"the friend's game built the same cave, chests and all ({r})", true);
                else if (NtGot("cave-bad", out r)) NtCheck($"the friend's game built the same cave ({r})", false);
                else { if (_ntPhaseT > 20) NtFail("the friend compares caves"); break; }
                // a golem beside the friend (held still), for them to strike
                _ntSide = G.Cave.IsSolid(friend.GlobalPosition + new Vector2(44, -6)) ? -1 : 1;
                _ntGolem = new Golem { Position = friend.GlobalPosition + new Vector2(_ntSide * 44, -6) };
                _ntGolem.SetMeta("test", true);
                _world.AddChild(_ntGolem);
                _ntGolem.Freeze(1000);
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
                if (NtGot("chest-ok", out var r)) NtCheck($"the chest opened for the friend who reached it, and gave them its pick ({r})", true);
                else if (NtGot("chest-bad", out r)) NtCheck($"the chest opened for the friend who reached it ({r})", false);
                else { if (_ntPhaseT > 20) NtFail("the friend opens the chest"); break; }
                var chest = _world.GetChildren().OfType<Chest>().OrderBy(c => c.GlobalPosition.DistanceTo(friend.GlobalPosition)).FirstOrDefault();
                NtCheck($"the host's copy of that chest is open too, and the pick wasn't the host's ({_state})", chest != null && chest.Open && _state == State.Playing);
                NtSay("fall");
                NtNext();
                break;
            }
            case 8:
                if (friend != null && friend.Dead && _ntPhaseT > 1f)
                {
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
                    SpawnPortal(G.Player.GlobalPosition + new Vector2(0, -17), (int)BiomeId.Slime, 1, "test");
                    _ntPortal = _world.GetChildren().OfType<Portal>().LastOrDefault();
                    NtSay($"portal {NetSync.IdOf(_ntPortal)}");
                    NtNext();
                }
                else if (_ntPhaseT > 20) NtFail("the friend is revived");
                break;
            case 10:
                if (_ntPhaseT > 1.2f && _waitingAt == null && G.Depth == 0) _ntPortal?.Enter();
                if (_waitingAt != null && G.Depth == 0) NtShot("nt_exit");
                if (G.Depth == 1)
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
                NtTough();
                NtSay("all-fall");
                G.Player.GiveUp();
                NtNext();
                break;
            }
            case 12:
                if (_state == State.Dead && NtGot("over", out _))
                {
                    NtCheck("with everyone down, the run ended in both games", true);
                    NtSay("bye");
                    NtNext();
                }
                else if (_ntPhaseT > 20) NtFail($"the run ends for everyone (host state {_state})");
                break;
            case 13:
                if (_ntShots != "" && _ntPhaseT < 2.5f) break;
                NtShot("nt_camp");
                NtEnd();
                break;
        }
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
                _ntMark = G.Player.Hp;
                NtSay("swung");
                NtNext();
                break;
            case 6:
                if (NtGot("hurt", out var amount))
                {
                    float want = float.Parse(amount, System.Globalization.CultureInfo.InvariantCulture) * (1f - G.Player.Stats.DamageReduction);
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
                // (the pick is offered a moment after the chest opens, and takes a press only after a beat)
                _ntChooseT = _state == State.Choosing ? _ntChooseT + 1f / 60f : 0;
                if (_ntChooseT > 0.6f)
                {
                    NtCheck($"the chest at my feet opened for me, with a pick (choosing {G.Player.Choosing}, safe {G.Player.Invulnerable})", G.Player.Choosing && G.Player.Invulnerable);
                    _upgradeMenu.Choose(0);
                    NtNext();
                }
                else if (_ntPhaseT > 15) { NtCheck($"the chest opens for me ({_state})", false); NtSay("chest-bad no pick"); _ntPhase = 11; NtNext(); }
                break;
            case 11:
                NtCheck($"the pick is taken and play goes on ({_state}, upgrades [{string.Join(",", G.Player.Stats.Stacks.Keys)}])", _state == State.Playing && G.Player.Stats.Stacks.Count > 0);
                NtSay($"chest-ok {string.Join(",", G.Player.Stats.Stacks.Keys)}");
                NtNext();
                break;
            case 12:
                if (NtGot("fall", out _)) { G.Player.GiveUp(); NtNext(); }
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
                if (G.Depth == 1) { NtCheck("went down the exit with the host", true); NtNext(); }
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
                if (NtGot("all-fall", out _)) { G.Player.GiveUp(); NtNext(); }
                else if (_ntPhaseT > 30) NtFail("the host asks everyone to fall");
                break;
            case 18:
                if (_state == State.Dead)
                {
                    NtCheck("with everyone down, the host ended the run here too", true);
                    NtSay("over");
                    NtNext();
                }
                else if (_ntPhaseT > 20) NtFail($"the run ends ({_state})");
                break;
            case 19:
                if (NtGot("bye", out _) || _ntPhaseT > 10) NtEnd();
                break;
        }
    }
}
