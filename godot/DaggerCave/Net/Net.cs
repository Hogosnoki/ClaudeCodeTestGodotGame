using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Godot;

namespace DaggerCave;

/// <summary>
/// Online co-op for up to five players, one of each hero.
///
/// One player hosts. The host's game owns the cave: every creature, pickup, chest and exit lives
/// there, and the other games show copies of them (puppets). Each player's own hero runs on their
/// own machine, so it handles exactly as it does alone; the others see it as a puppet. When your
/// hero strikes a creature, your game tells the host, which applies the blow; when a creature
/// strikes your hero, the host tells your game, which takes it (with your own dodge, shield and
/// invulnerability deciding what gets through). Effects and sounds travel between the games too.
///
/// Connecting: the host's game opens a UDP port (asking the router to forward it, by UPnP) and
/// shows a join code that packs its internet address; the friend types or pastes the code. A
/// plain address ("192.168.1.20" or "host:port") works as well.
///
/// This file is the session: hosting, joining, the lobby, and moving messages. What the messages
/// carry and do is in NetSync.cs.
/// </summary>
public static partial class Net
{
    public const int Port = 24890;
    /// <summary>Games only play together at the same version of the protocol.</summary>
    public const int Version = 6;
    public const int MaxPlayers = 5;

    /// <summary>
    /// Which copy of the game this is: the commit a downloaded build was made from (written by
    /// the build, `build_id.txt`), or "dev" when run from the editor. Copies that differ still
    /// play together, but the lobby warns: different code can build different caves from one seed.
    /// </summary>
    public static string Build => _build ??= Godot.FileAccess.FileExists("res://build_id.txt") ? Godot.FileAccess.GetFileAsString("res://build_id.txt").Trim() : "dev";
    private static string _build;

    public enum Mode { Off, Host, Client }
    public static Mode State { get; private set; } = Mode.Off;
    public static bool Online => State != Mode.Off;
    public static bool IsHost => State == Mode.Host;
    public static bool IsClient => State == Mode.Client;
    /// <summary>This game's peer id (the host is always 1).</summary>
    public static int Me { get; private set; } = 1;
    /// <summary>A run has started (the lobby is closed to newcomers).</summary>
    public static bool InRun { get; set; }

    /// <summary>A player's name (for notices), or "SOMEONE".</summary>
    public static string NameOf(int id) => Peers.TryGetValue(id, out var p) ? p.Name.ToUpperInvariant() : "SOMEONE";

    public sealed class PeerInfo
    {
        public int Id;
        public string Name = "Player";
        /// <summary>Which copy of the game they run (<see cref="Build"/>).</summary>
        public string Build = "";
        public HeroKind Hero;
        public bool Picked;
        /// <summary>Their hero in this game (a puppet, unless it's this machine's own).</summary>
        public Player Avatar;
        /// <summary>Waiting at an exit to go down.</summary>
        public bool AtExit;
    }

    /// <summary>Everyone in the game, this machine included, by peer id.</summary>
    public static readonly SortedDictionary<int, PeerInfo> Peers = new();
    public static PeerInfo Mine => Peers.TryGetValue(Me, out var p) ? p : null;
    public static PeerInfo Peer(int id) => Peers.TryGetValue(id, out var p) ? p : null;
    public static int Count => Peers.Count;

    /// <summary>What the online menu says about the connection ("Connecting…", "Couldn't reach…").</summary>
    public static string Status = "";
    /// <summary>The host's join codes: over the internet, and on the same network.</summary>
    public static string JoinCode = "", LanCode = "", PublicIp = "", LanIp = "";
    /// <summary>The host's address on a virtual LAN like Tailscale (100.64.0.0/10), if it's on one.</summary>
    public static string VpnIp = "";
    /// <summary>The router forwarded the port by itself (UPnP), or the check is still going.</summary>
    public static bool UpnpOk, UpnpChecking;
    /// <summary>The router's own internet address isn't the one the world sees: the internet
    /// provider shares one address among many homes (carrier-grade NAT), so the code can't work.</summary>
    public static bool BehindCgnat;
    private static string _routerIp = "";
    /// <summary>Anything shown in the online menu changed.</summary>
    public static event Action Changed;
    /// <summary>The connection closed (the host left, or it never connected): why.</summary>
    public static event Action<string> Closed;

    private static NetNode _node;
    private static ENetMultiplayerPeer _peer;
    private static Upnp _upnp;
    private static bool _mapped;

    /// <summary>The player's name, as the others see it (kept in the settings).</summary>
    public static string MyName
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(GameSettings.PlayerName)) return GameSettings.PlayerName;
            string n = System.Environment.UserName;
            return string.IsNullOrWhiteSpace(n) ? "Player" : n.Length > 16 ? n[..16] : n;
        }
    }

    public static void Attach(NetNode node) => _node = node;

    private static void Notify() => Changed?.Invoke();

    // ---------------------------------------------------------------- hosting and joining

    /// <summary>Opens a game for friends to join. False (with <see cref="Status"/> saying why) if the port can't be opened.</summary>
    public static bool Host()
    {
        Leave();
        _peer = new ENetMultiplayerPeer();
        var err = _peer.CreateServer(Port, MaxPlayers - 1);
        if (err != Error.Ok)
        {
            Status = $"Couldn't open port {Port} ({err}). Is another copy of the game hosting already?";
            _peer = null;
            Notify();
            return false;
        }
        Bind();
        State = Mode.Host;
        Me = 1;
        Peers.Clear();
        // (the host keeps the hero picked on the title)
        Peers[1] = new PeerInfo { Id = 1, Name = MyName, Hero = G.Hero, Picked = true, Build = Build };
        InRun = false;
        LanIp = FindLanIp();
        LanCode = LanIp != "" ? MakeCode(LanIp, Port) : "";
        VpnIp = FindVpnIp();
        JoinCode = "";
        PublicIp = "";
        _routerIp = "";
        BehindCgnat = false;
        Status = "Opening the door on your router…";
        StartUpnp();
        // (the address the world sees, from a what's-my-address service: the code carries it)
        _node?.LookUpPublicIp();
        Notify();
        return true;
    }

    /// <summary>Joins a friend's game by join code or address. The outcome arrives later (<see cref="Changed"/> / <see cref="Closed"/>).</summary>
    public static bool Join(string codeOrAddress)
    {
        Leave();
        if (!ReadCode(codeOrAddress, out string ip, out int port))
        {
            Status = "That doesn't look like a join code (they look like 7K3QD-M2XP9) or an address.";
            Notify();
            return false;
        }
        _peer = new ENetMultiplayerPeer();
        var err = _peer.CreateClient(ip, port);
        if (err != Error.Ok)
        {
            Status = $"Couldn't start connecting ({err}).";
            _peer = null;
            Notify();
            return false;
        }
        Bind();
        State = Mode.Client;
        Peers.Clear();
        InRun = false;
        _askedHero = false;
        Status = $"Connecting to {ip}:{port}…";
        Notify();
        return true;
    }

    /// <summary>Closes the connection (and the router's forwarded port) and forgets everyone.
    /// Quitting, it waits a moment for the router to close the port.</summary>
    public static void Leave(bool quitting = false)
    {
        if (_peer != null)
        {
            var mp = _node?.Multiplayer;
            if (mp != null)
            {
                mp.PeerConnected -= OnPeerConnected;
                mp.PeerDisconnected -= OnPeerDisconnected;
                mp.ConnectedToServer -= OnConnected;
                mp.ConnectionFailed -= OnConnectFailed;
                mp.ServerDisconnected -= OnServerGone;
                mp.MultiplayerPeer = null;
            }
            _peer.Close();
            _peer = null;
        }
        if (_mapped && _upnp != null)
        {
            var u = _upnp;
            var closing = Task.Run(() => { try { u.DeletePortMapping(Port, "UDP"); } catch (Exception) { } });
            if (quitting) closing.Wait(1500);
        }
        _mapped = false;
        State = Mode.Off;
        Me = 1;
        InRun = false;
        Peers.Clear();
        NetSync.Reset();
        Status = "";
        JoinCode = LanCode = PublicIp = "";
    }

    private static void Bind()
    {
        var mp = _node.Multiplayer;
        mp.MultiplayerPeer = _peer;
        mp.PeerConnected += OnPeerConnected;
        mp.PeerDisconnected += OnPeerDisconnected;
        mp.ConnectedToServer += OnConnected;
        mp.ConnectionFailed += OnConnectFailed;
        mp.ServerDisconnected += OnServerGone;
    }

    private static void OnPeerConnected(long id)
    {
        // (the host waits for their Hello; a client hears about the others through the lobby)
    }

    private static void OnPeerDisconnected(long id)
    {
        int pid = (int)id;
        if (!Peers.TryGetValue(pid, out var p)) return;
        Peers.Remove(pid);
        if (p.Avatar != null && GodotObject.IsInstanceValid(p.Avatar)) p.Avatar.QueueFree();
        if (IsHost)
        {
            SendLobby();
            if (InRun) NetSync.PeerLeft(p);
        }
        G.Main?.OnlineNotice($"{p.Name} left the game");
        Notify();
    }

    private static void OnConnected()
    {
        Me = _node.Multiplayer.GetUniqueId();
        Status = "Connected. Saying hello…";
        var w = new NetOut(Msg.Hello);
        w.Int(Version);
        w.Str(MyName);
        w.Str(Build);
        SendTo(1, w, true);
        Notify();
    }

    private static void OnConnectFailed()
        => Close("Couldn't reach the host. Check the code, and that their game is hosting (and, if it said so, that their router lets you in).");

    private static void OnServerGone() => Close(InRun ? "The host left the game." : "The host closed the game.");

    /// <summary>The host refused us (full, a different version, a run under way): leave with the reason.</summary>
    private static void Refused(string why) => Close(why);

    /// <summary>Hangs up, saying why (after the network has finished with this frame's messages).</summary>
    private static void Close(string why)
    {
        var peer = _peer;
        Callable.From(() =>
        {
            if (_peer != peer) return; // (already left, or a new game started meanwhile)
            Leave();
            Status = why;
            Closed?.Invoke(why);
            Notify();
        }).CallDeferred();
    }

    // ---------------------------------------------------------------- the lobby

    /// <summary>The hero this machine wants. The host decides (each hero once).</summary>
    public static void PickHero(HeroKind h)
    {
        if (!Online) return;
        if (IsHost) { TryPick(1, h); return; }
        var w = new NetOut(Msg.PickHero);
        w.Byte((byte)h);
        SendTo(1, w, true);
    }

    /// <summary>Who has a hero, by hero.</summary>
    public static PeerInfo TakenBy(HeroKind h) => Peers.Values.FirstOrDefault(p => p.Picked && p.Hero == h);

    private static void TryPick(int who, HeroKind h)
    {
        if (!Peers.TryGetValue(who, out var p) || InRun) return;
        var owner = TakenBy(h);
        if (owner != null && owner.Id != who) return; // someone else has that hero
        p.Hero = h;
        p.Picked = true;
        SendLobby();
        Notify();
    }

    /// <summary>Everyone has a hero (and there's someone to play with).</summary>
    public static bool AllPicked => Peers.Count >= 1 && Peers.Values.All(p => p.Picked);

    private static void SendLobby()
    {
        if (!IsHost) return;
        var w = new NetOut(Msg.Lobby);
        w.Byte((byte)Peers.Count);
        foreach (var p in Peers.Values)
        {
            w.Int(p.Id);
            w.Str(p.Name);
            w.Byte((byte)p.Hero);
            w.Bool(p.Picked);
            w.Bool(p.AtExit);
            w.Str(p.Build);
        }
        SendAll(w, true);
    }

    /// <summary>Tells everyone who is waiting at an exit (host).</summary>
    public static void SendExitState() => SendLobby();

    // ---------------------------------------------------------------- messages

    /// <summary>Kinds of message (the first byte of each packet).</summary>
    public enum Msg : byte
    {
        Hello = 1, Refuse, Lobby, PickHero, Start, Level,
        HeroState, HeroEvent, WorldSnap, EnemySpawn, EnemyDie, EnemyHit, EnemyEffect,
        HeroHurt, HeroHeal, PropSpawn, PropGone, Xp, Pickup, ChestOpen, ChestOpened,
        ExitReady, Fx, Sfx, Banner, GuardianDown, RunOver, Revive, KillCredit, Music, HeroGone, Dealt, LeftCave,
        /// <summary>Chests: who is looking in one, the cards it was dealt, and a look finished (taken or left).</summary>
        ChestLook, ChestCards, ChestDone,
        /// <summary>A barrier or a mending for another player's hero.</summary>
        HeroBoon,
        /// <summary>Vault gates: a hero with a key asks the host, and the host opens one (and says whose key).</summary>
        GateAsk, GateOpened,
        /// <summary>The two-game test harness (--nettest) talking to itself.</summary>
        Test,
    }

    /// <summary>Sends to one peer.</summary>
    public static void SendTo(int peer, NetOut w, bool reliable)
    {
        if (_peer == null || _node == null) return;
        var data = w.Bytes();
        if (peer == Me) return;
        if (reliable) _node.RpcId(peer, NetNode.MethodName.Rel, data);
        else _node.RpcId(peer, NetNode.MethodName.Unrel, data);
    }

    /// <summary>The host sends to every client; a client sends to the host (who passes on what's for everyone).</summary>
    public static void SendAll(NetOut w, bool reliable)
    {
        if (_peer == null || _node == null) return;
        var data = w.Bytes();
        if (IsHost)
        {
            foreach (var id in Peers.Keys) if (id != Me) Raw(id, data, reliable);
        }
        else Raw(1, data, reliable);
    }

    /// <summary>The host passes a client's message on to the other clients.</summary>
    private static void Relay(int from, byte[] data, bool reliable)
    {
        if (!IsHost) return;
        foreach (var id in Peers.Keys) if (id != Me && id != from) Raw(id, data, reliable);
    }

    private static void Raw(int peer, byte[] data, bool reliable)
    {
        if (reliable) _node.RpcId(peer, NetNode.MethodName.Rel, data);
        else _node.RpcId(peer, NetNode.MethodName.Unrel, data);
    }

    /// <summary>Every packet arrives here.</summary>
    public static void Receive(int from, byte[] data, bool reliable)
    {
        if (data == null || data.Length == 0 || !Online) return;
        var r = new NetIn(data);
        var type = (Msg)r.Byte();
        try
        {
            switch (type)
            {
                case Msg.Hello: OnHello(from, r); break;
                case Msg.Refuse: Refused(r.Str()); break;
                case Msg.Lobby: OnLobby(r); break;
                case Msg.PickHero: if (IsHost) TryPick(from, (HeroKind)r.Byte()); break;
                default:
                    // what's for everyone goes on to the others first
                    if (IsHost && from != Me && NetSync.IsBroadcast(type)) Relay(from, data, reliable);
                    NetSync.Handle(type, from, r);
                    break;
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[net] bad {type} from {from}: {ex.Message}");
        }
    }

    private static void OnHello(int from, NetIn r)
    {
        if (!IsHost) return;
        int version = r.Int();
        string name = Clean(r.Str());
        string build = r.More ? r.Str() : "";
        string refuse = version != Version ? "Your game is a different version from the host's. You both need the same copy."
            : InRun ? "That game is already under way. Ask the host to go back to the lobby."
            : Peers.Count >= MaxPlayers ? "That game is full (five heroes, five players)."
            : null;
        if (refuse != null)
        {
            var no = new NetOut(Msg.Refuse);
            no.Str(refuse);
            SendTo(from, no, true);
            // (hang up a moment later, once the refusal has gone)
            _node.GetTree().CreateTimer(1.0).Timeout += () => _peer?.DisconnectPeer(from);
            return;
        }
        // two players can't share a name on screen
        string unique = name;
        for (int k = 2; Peers.Values.Any(p => p.Name == unique); k++) unique = $"{name} {k}";
        var info = new PeerInfo { Id = from, Name = unique, Build = build };
        // hand them the first hero nobody has (they can switch to another free one)
        foreach (HeroKind h in Enum.GetValues(typeof(HeroKind)))
            if (TakenBy(h) == null) { info.Hero = h; info.Picked = true; break; }
        Peers[from] = info;
        SendLobby();
        G.Main?.OnlineNotice($"{unique} joined");
        Notify();
    }

    private static void OnLobby(NetIn r)
    {
        int n = r.Byte();
        var keep = new Dictionary<int, Player>();
        foreach (var p in Peers.Values) if (p.Avatar != null) keep[p.Id] = p.Avatar;
        Peers.Clear();
        for (int k = 0; k < n; k++)
        {
            var p = new PeerInfo { Id = r.Int(), Name = r.Str(), Hero = (HeroKind)r.Byte(), Picked = r.Bool(), AtExit = r.Bool(), Build = r.Str() };
            if (keep.TryGetValue(p.Id, out var av)) p.Avatar = av;
            Peers[p.Id] = p;
        }
        if (IsClient && Status.StartsWith("Connect")) Status = "In the lobby.";
        // in: ask for the hero picked on the title, if nobody has it
        if (IsClient && !_askedHero && Mine != null)
        {
            _askedHero = true;
            if (Mine.Hero != G.Hero && TakenBy(G.Hero) == null) PickHero(G.Hero);
        }
        Notify();
    }

    private static bool _askedHero;

    private static string Clean(string s)
    {
        s = (s ?? "").Trim();
        var sb = new StringBuilder();
        foreach (char c in s) if (!char.IsControl(c)) sb.Append(c);
        s = sb.ToString();
        if (s.Length > 16) s = s[..16];
        return s == "" ? "Player" : s;
    }

    // ---------------------------------------------------------------- the router, addresses, codes

    private static void StartUpnp()
    {
        UpnpChecking = true;
        UpnpOk = false;
        var upnp = new Upnp();
        _upnp = upnp;
        Task.Run(() =>
        {
            bool ok = false;
            string ext = "";
            try
            {
                int err = upnp.Discover(2000, 2, "InternetGatewayDevice");
                var gw = upnp.GetGateway();
                if (err == (int)Upnp.UpnpResult.Success && gw != null && gw.IsValidGateway())
                {
                    ok = upnp.AddPortMapping(Port, Port, "Dagger Deep", "UDP", 0) == (int)Upnp.UpnpResult.Success;
                    ext = upnp.QueryExternalAddress();
                }
            }
            catch (Exception) { ok = false; }
            Callable.From(() => OnUpnp(ok, ext)).CallDeferred();
        });
    }

    private static void OnUpnp(bool ok, string external)
    {
        if (!IsHost) return;
        UpnpChecking = false;
        UpnpOk = ok;
        _mapped = ok;
        _routerIp = !string.IsNullOrEmpty(external) && IsIpv4(external) ? external.Trim() : "";
        // (if the what's-my-address service can't be reached, the router's own address will do)
        if (PublicIp == "" && _routerIp != "" && !IsPrivate(_routerIp)) UsePublicIp(_routerIp);
        CheckCgnat();
        Notify();
    }

    /// <summary>The host's address as the internet sees it (from a what's-my-address service).</summary>
    public static void SetPublicIp(string ip)
    {
        if (!IsHost) return;
        UsePublicIp(ip);
        CheckCgnat();
        Notify();
    }

    private static void UsePublicIp(string ip)
    {
        ip = ip.Trim();
        if (!IsIpv4(ip)) return;
        PublicIp = ip;
        JoinCode = MakeCode(ip, Port);
    }

    /// <summary>The router opened the door, but to an address that isn't the one the world sees:
    /// the internet provider's own NAT sits in between, and no router setting gets past it.</summary>
    private static void CheckCgnat()
    {
        BehindCgnat = UpnpOk && _routerIp != "" && (IsPrivate(_routerIp) || (PublicIp != "" && PublicIp != _routerIp));
        Status = UpnpChecking ? "Opening the door on your router…"
            : BehindCgnat ? "Hosting, but your internet provider shares one address among many homes, so friends elsewhere can't reach you with the code."
            : UpnpOk ? "Hosting. Send your friend the join code."
            : "Hosting, but your router didn't open the door by itself.";
    }

    /// <summary>Addresses that only mean something on a private network (or behind a provider's NAT).</summary>
    public static bool IsPrivate(string ip)
    {
        var p = ip.Split('.');
        if (p.Length != 4 || !byte.TryParse(p[0], out byte a) || !byte.TryParse(p[1], out byte b)) return false;
        return a == 10 || a == 127 || (a == 172 && b >= 16 && b <= 31) || (a == 192 && b == 168) || (a == 100 && b >= 64 && b <= 127) || (a == 169 && b == 254);
    }

    private static bool IsIpv4(string s)
    {
        var parts = s.Split('.');
        return parts.Length == 4 && parts.All(p => byte.TryParse(p, out _));
    }

    /// <summary>This computer's address on the local network (192.168.x.x and the like), or "".</summary>
    public static string FindLanIp()
    {
        static bool Lan(string a) => IsIpv4(a) && IsPrivate(a) && !a.StartsWith("127.") && !a.StartsWith("169.254.") && !a.StartsWith("100.");
        // the address the way out to the internet leaves from (not a virtual machine's or a VPN's adapter)
        try
        {
            using var s = new System.Net.Sockets.Socket(System.Net.Sockets.AddressFamily.InterNetwork, System.Net.Sockets.SocketType.Dgram, System.Net.Sockets.ProtocolType.Udp);
            s.Connect(System.Net.IPAddress.Parse("8.8.8.8"), 53); // (nothing is sent: this only picks the route)
            string route = (s.LocalEndPoint as System.Net.IPEndPoint)?.Address.ToString() ?? "";
            if (Lan(route)) return route;
        }
        catch (Exception) { }
        foreach (var a in IP.GetLocalAddresses())
            if (Lan(a)) return a;
        return "";
    }

    /// <summary>This computer's address on a virtual LAN like Tailscale (100.64.0.0/10), or "".</summary>
    public static string FindVpnIp()
    {
        foreach (var a in IP.GetLocalAddresses())
        {
            if (!IsIpv4(a)) continue;
            var p = a.Split('.');
            if (p[0] == "100" && int.TryParse(p[1], out int b) && b >= 64 && b <= 127) return a;
        }
        return "";
    }

    // Crockford's base 32: no I, L, O or U, so a code read aloud or retyped doesn't go wrong
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    /// <summary>A join code: an IPv4 address and a port in ten letters and digits ("7K3QD-M2XP9").</summary>
    public static string MakeCode(string ip, int port)
    {
        var b = ip.Split('.').Select(byte.Parse).ToArray();
        ulong v = ((ulong)b[0] << 40) | ((ulong)b[1] << 32) | ((ulong)b[2] << 24) | ((ulong)b[3] << 16) | (ushort)port;
        var sb = new StringBuilder();
        for (int k = 9; k >= 0; k--) sb.Append(Alphabet[(int)((v >> (k * 5)) & 31)]);
        return sb.ToString(0, 5) + "-" + sb.ToString(5, 5);
    }

    /// <summary>Reads a join code, or an address ("1.2.3.4", "1.2.3.4:5678", "my.host.name").</summary>
    public static bool ReadCode(string text, out string ip, out int port)
    {
        ip = ""; port = Port;
        text = (text ?? "").Trim();
        if (text == "") return false;
        if (text.Contains('.') || text.Equals("localhost", StringComparison.OrdinalIgnoreCase))
        {
            // an address, maybe with a port
            int colon = text.LastIndexOf(':');
            if (colon > 0 && int.TryParse(text[(colon + 1)..], out int p)) { port = p; text = text[..colon]; }
            ip = text;
            if (!IsIpv4(ip))
            {
                // a host name: look it up
                ip = IP.ResolveHostname(text, IP.Type.Ipv4);
                if (string.IsNullOrEmpty(ip)) return false;
            }
            return true;
        }
        var clean = new StringBuilder();
        foreach (char c0 in text.ToUpperInvariant())
        {
            char c = c0 switch { 'O' => '0', 'I' => '1', 'L' => '1', _ => c0 };
            if (c == '-' || c == ' ') continue;
            if (Alphabet.IndexOf(c) < 0) return false;
            clean.Append(c);
        }
        if (clean.Length != 10) return false;
        ulong v = 0;
        foreach (char c in clean.ToString()) v = (v << 5) | (uint)Alphabet.IndexOf(c);
        ip = $"{(v >> 40) & 255}.{(v >> 32) & 255}.{(v >> 24) & 255}.{(v >> 16) & 255}";
        port = (int)(v & 0xFFFF);
        return port > 0;
    }
}

/// <summary>
/// The node the messages travel through (the same path in every game), and the per-frame tick.
/// </summary>
public partial class NetNode : Node
{
    public NetNode() => Name = "Net";

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        Net.Attach(this);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void Rel(byte[] data) => Net.Receive(Multiplayer.GetRemoteSenderId(), data, true);

    // (a channel of its own: a lost reliable message mustn't hold up the next state updates behind it)
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable, TransferChannel = 1)]
    public void Unrel(byte[] data) => Net.Receive(Multiplayer.GetRemoteSenderId(), data, false);

    public override void _Process(double delta)
    {
        // (in real time: a hit-stop slowing this game mustn't slow what it sends)
        if (Net.Online) NetSync.Tick((float)(delta / Math.Max(0.05, Engine.TimeScale)));
    }

    public override void _ExitTree() => Net.Leave(quitting: true);

    /// <summary>Asks a what's-my-address service for this computer's internet address (when the router wouldn't say).</summary>
    public void LookUpPublicIp()
    {
        var req = new HttpRequest { Timeout = 5 };
        AddChild(req);
        req.RequestCompleted += (result, code, headers, body) =>
        {
            if (result == (long)HttpRequest.Result.Success && code == 200) Net.SetPublicIp(Encoding.ASCII.GetString(body));
            req.QueueFree();
        };
        if (req.Request("https://api.ipify.org") != Error.Ok) req.QueueFree();
    }
}

/// <summary>Writes a message.</summary>
public sealed class NetOut
{
    private readonly MemoryStream _ms = new();
    private readonly BinaryWriter _w;
    public NetOut(Net.Msg type) { _w = new BinaryWriter(_ms); _w.Write((byte)type); }
    public byte[] Bytes() { _w.Flush(); return _ms.ToArray(); }
    public long Length => _ms.Length;
    public NetOut Byte(byte v) { _w.Write(v); return this; }
    public NetOut SByte(sbyte v) { _w.Write(v); return this; }
    public NetOut Bool(bool v) { _w.Write(v); return this; }
    public NetOut Short(short v) { _w.Write(v); return this; }
    public NetOut UShort(ushort v) { _w.Write(v); return this; }
    public NetOut Int(int v) { _w.Write(v); return this; }
    public NetOut UInt(uint v) { _w.Write(v); return this; }
    public NetOut Float(float v) { _w.Write(v); return this; }
    public NetOut Half(float v) { _w.Write((System.Half)v); return this; }
    public NetOut Vec(Vector2 v) { _w.Write(v.X); _w.Write(v.Y); return this; }
    /// <summary>A direction or small vector, at half precision.</summary>
    public NetOut HVec(Vector2 v) { _w.Write((System.Half)v.X); _w.Write((System.Half)v.Y); return this; }
    public NetOut Col(Color c) { _w.Write((byte)Math.Clamp(c.R * 255f, 0, 255)); _w.Write((byte)Math.Clamp(c.G * 255f, 0, 255)); _w.Write((byte)Math.Clamp(c.B * 255f, 0, 255)); _w.Write((byte)Math.Clamp(c.A * 255f, 0, 255)); return this; }
    public NetOut Str(string s) { _w.Write(s ?? ""); return this; }
    public NetOut Raw(byte[] b) { _w.Write(b); return this; }
}

/// <summary>Reads a message.</summary>
public sealed class NetIn
{
    private readonly BinaryReader _r;
    public NetIn(byte[] data) { _r = new BinaryReader(new MemoryStream(data)); }
    public bool More => _r.BaseStream.Position < _r.BaseStream.Length;
    public byte Byte() => _r.ReadByte();
    public sbyte SByte() => _r.ReadSByte();
    public bool Bool() => _r.ReadBoolean();
    public short Short() => _r.ReadInt16();
    public ushort UShort() => _r.ReadUInt16();
    public int Int() => _r.ReadInt32();
    public uint UInt() => _r.ReadUInt32();
    /// <summary>Reads past bytes this game has no use for.</summary>
    public void Skip(int n) => _r.BaseStream.Seek(n, SeekOrigin.Current);
    public float Float() => _r.ReadSingle();
    public float Half() => (float)_r.ReadHalf();
    public Vector2 Vec() => new(_r.ReadSingle(), _r.ReadSingle());
    public Vector2 HVec() => new((float)_r.ReadHalf(), (float)_r.ReadHalf());
    public Color Col() => new(_r.ReadByte() / 255f, _r.ReadByte() / 255f, _r.ReadByte() / 255f, _r.ReadByte() / 255f);
    public string Str() => _r.ReadString();
}
