using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

/// <summary>
/// What travels between the games in an online run, and what each message does there.
///
///  * Heroes: each game sends its own hero's state twenty times a second (where it is, what it's
///    doing, its health); the others show it as a puppet. Swings, deaths and revivals go as events.
///  * Creatures: the host's game runs them all and sends them twenty times a second to the
///    others, whose copies (puppets) only show them. A blow that lands on a puppet is sent to the
///    host, which applies it; a blow a creature lands on someone's hero is sent to that hero's
///    game, which takes it (its own dodge, shield and invulnerability deciding what gets through).
///  * Things the host's creatures make (thrown rocks, shockwaves, dropped pickups) are sent as they
///    appear, and every game runs its own copy; each copy only ever hurts that game's own hero.
///  * Chests, exits and experience are the host's to decide: opening a chest, stepping into an
///    exit, picking up a heart or an orb all go through it. Experience is shared.
///  * Effects and sounds made by the host's creatures and by each hero are sent to the others,
///    so everyone sees and hears the same fight.
/// </summary>
public static class NetSync
{
    /// <summary>Seconds between state updates (twenty a second).</summary>
    public const float SendInterval = 0.05f;
    /// <summary>How far behind the newest update puppets are shown (smooths out uneven arrival).</summary>
    public const double InterpDelay = 0.1;

    private static float _sendT;
    private static int _nextId = 1, _levelId;
    private static uint _snapSeq;
    private static uint _lastSnapSeen;
    public static readonly Dictionary<int, Enemy> Enemies = new();
    public static readonly Dictionary<int, Node2D> Props = new();
    private static readonly Dictionary<Node2D, int> PropIds = new(ReferenceEqualityComparer.Instance);

    /// <summary>Code whose effects the other games should see is running (the host's creatures,
    /// this game's hero, the host's world). Effects, sounds and spawns made inside are sent.</summary>
    public static int Scope;
    /// <summary>A message from another game is being carried out (nothing it does is sent back).</summary>
    public static bool Applying;
    public static bool Recording => Net.Online && Scope > 0 && !Applying;

    private static NetOut _fx;
    private static int _fxCount;

    /// <summary>Enemy health for this many players (the host scales what it spawns).</summary>
    public static float HpScale => Net.Online ? 1f + 0.5f * Math.Max(0, Net.Count - 1) : 1f;
    /// <summary>How many more creatures the cave holds with company.</summary>
    public static float CapScale => Net.Online ? 1f + 0.35f * Math.Max(0, Net.Count - 1) : 1f;

    public static double Now => Time.GetTicksMsec() / 1000.0;

    public static void Reset()
    {
        Enemies.Clear();
        Props.Clear();
        PropIds.Clear();
        _exitOf.Clear();
        _dealt.Clear();
        // (the host's ids start well above the level's own, which count up from 1)
        _nextId = 1_000_000;
        _levelId = 0;
        _fx = null;
        _fxCount = 0;
        _lastSnapSeen = 0;
        _striker = 0;
        Scope = 0;
        Applying = false;
        Building = false;
    }

    /// <summary>A level is being built here (the old one's creatures and things go quietly: every game is building too).</summary>
    public static bool Building;

    /// <summary>A new level is about to be built: forget the old level's ids (the level's own chests count up from 1 again).</summary>
    public static void BeginLevel()
    {
        Building = true;
        Enemies.Clear();
        Props.Clear();
        PropIds.Clear();
        _exitOf.Clear();
        _levelId = 0;
        foreach (var p in Net.Peers.Values) p.AtExit = false;
    }

    /// <summary>The new level is built: put everyone's heroes in it.</summary>
    public static void LevelBuilt()
    {
        Building = false;
        if (Net.InRun) SpawnAvatars();
    }

    /// <summary>Puppets for the other players' heroes, where this game's hero starts.</summary>
    public static void SpawnAvatars()
    {
        foreach (var p in Net.Peers.Values)
        {
            if (p.Id == Net.Me) { p.Avatar = G.Player; continue; }
            if (p.Avatar != null && GodotObject.IsInstanceValid(p.Avatar) && p.Avatar.IsInsideTree()) continue;
            var puppet = new Player { IsRemote = true, NetOwner = p.Id, NetName = p.Name };
            puppet.Stats = new PlayerStats(p.Hero);
            puppet.Position = (G.Player?.GlobalPosition ?? G.Cave.StartPos) + new Vector2(20 * p.Id % 3 - 20, 0);
            G.World.AddChild(puppet);
            p.Avatar = puppet;
        }
    }

    /// <summary>Takes a player's hero away (they left).</summary>
    public static void PeerLeft(Net.PeerInfo p)
    {
        if (p.Avatar != null && GodotObject.IsInstanceValid(p.Avatar)) p.Avatar.QueueFree();
        p.Avatar = null;
        _exitOf.Remove(p.Id);
        FreeChestsOf(p.Id);
        G.Main?.CheckExits();
    }

    /// <summary>A level-built thing (a chest from the level's own layout): the same id in every game, by order of creation.</summary>
    public static int LevelId(Node2D n)
    {
        int id = ++_levelId;
        Props[id] = n;
        PropIds[n] = id;
        return id;
    }

    public static int IdOf(Node2D n) => n != null && PropIds.TryGetValue(n, out int id) ? id : 0;

    private static int NewId() => _nextId++;

    private static void Track(Node2D n, int id)
    {
        Props[id] = n;
        PropIds[n] = id;
        n.TreeExited += () => { if (Props.TryGetValue(id, out var x) && x == n) Props.Remove(id); PropIds.Remove(n); };
    }

    // ================================================================== ticking

    public static void Tick(float dt)
    {
        if (!Net.InRun || G.Player == null || G.Cave == null || Building) { FlushFx(); return; }
        _sendT -= dt;
        if (_sendT <= 0)
        {
            _sendT += SendInterval;
            if (_sendT < 0) _sendT = SendInterval;
            SendHeroState();
            if (Net.IsHost) SendWorld();
        }
        _dealtT -= dt;
        if (_dealtT <= 0) { _dealtT = 0.5f; SendDealt(); }
        FlushFx();
    }

    /// <summary>Runs code whose effects are this game's alone (nothing it makes or shows is sent).</summary>
    public static void Local(Action a)
    {
        bool was = Applying;
        Applying = true;
        try { a(); }
        finally { Applying = was; }
    }

    // ---- who struck (the host applies other games' blows)

    private static int _striker;
    /// <summary>Whose blow is landing right now: another game's hero (the host is applying it), or this game's.</summary>
    public static int Striker => _striker != 0 ? _striker : Net.Me;

    private static readonly Dictionary<int, float> _dealt = new();
    private static float _dealtT;

    /// <summary>Damage dealt over time (a withering hex) for a player's hero: life steal and alimus, in their game.</summary>
    public static void CreditDealt(int who, float amount)
    {
        if (amount <= 0) return;
        if (!Net.Online || who == 0 || who == Net.Me) { G.Player?.OnDealtDamage(amount); return; }
        _dealt[who] = _dealt.GetValueOrDefault(who) + amount;
    }

    private static void SendDealt()
    {
        if (_dealt.Count == 0) return;
        foreach (var (who, amount) in _dealt)
        {
            var w = new NetOut(Net.Msg.Dealt);
            w.Float(amount);
            Net.SendTo(who, w, true);
        }
        _dealt.Clear();
    }

    /// <summary>Is this message, from a client, one the host should pass on to the other clients?</summary>
    public static bool IsBroadcast(Net.Msg t) => t is Net.Msg.HeroState or Net.Msg.HeroEvent or Net.Msg.Fx or Net.Msg.PropGone or Net.Msg.Revive or Net.Msg.HeroHeal or Net.Msg.HeroBoon or Net.Msg.ChestCards;

    // ================================================================== heroes

    private static void SendHeroState()
    {
        var p = G.Player;
        if (p == null || !GodotObject.IsInstanceValid(p)) return;
        var w = new NetOut(Net.Msg.HeroState);
        w.Int(Net.Me);
        p.WriteNet(w);
        Net.SendAll(w, false);
    }

    /// <summary>This game's hero began a swing (the others show the blade's sweep).</summary>
    public static void HeroSwing(Player p, Vector2 dir, float arc, float reach, float windup, float active, int combo, bool finisher, bool charged, bool heave)
    {
        if (!Net.Online || p.IsRemote) return;
        var w = new NetOut(Net.Msg.HeroEvent);
        w.Int(Net.Me).Byte(1).HVec(dir).Half(arc).Half(reach).Half(windup).Half(active).Byte((byte)combo);
        w.Byte((byte)((finisher ? 1 : 0) | (charged ? 2 : 0) | (heave ? 4 : 0)));
        Net.SendAll(w, true);
    }

    /// <summary>A crescent wave, or a mote of stolen life, from this game's hero (the others see it, harmless).</summary>
    public static void HeroVisual(Node2D n)
    {
        if (!Net.Online || Applying) return;
        var w = new NetOut(Net.Msg.HeroEvent);
        w.Int(Net.Me);
        switch (n)
        {
            case SwordWave sw: w.Byte(2).Vec(sw.GlobalPosition).HVec(sw.Dir).Half(sw.Range).Half(sw.Speed); break;
            case LifeMote m: w.Byte(3).Vec(m.GlobalPosition).Half(m.Size); break;
            case HealingPool hp: w.Byte(6).Vec(hp.GlobalPosition).Half(hp.Radius).Half(hp.Rate).Half(hp.Life); break;
            default: return;
        }
        Net.SendAll(w, true);
    }

    /// <summary>This game's hero fell, or got back up.</summary>
    public static void HeroDown(bool down)
    {
        if (!Net.Online) return;
        var w = new NetOut(Net.Msg.HeroEvent);
        w.Int(Net.Me).Byte((byte)(down ? 4 : 5));
        Net.SendAll(w, true);
    }

    private static void OnHeroEvent(NetIn r)
    {
        int owner = r.Int();
        byte kind = r.Byte();
        var peer = Net.Peer(owner);
        var av = peer?.Avatar;
        if (av == null || !GodotObject.IsInstanceValid(av) || !av.IsRemote) return;
        switch (kind)
        {
            case 1:
            {
                var dir = r.HVec(); float arc = r.Half(), reach = r.Half(), windup = r.Half(), active = r.Half();
                int combo = r.Byte(); byte f = r.Byte();
                av.NetSwing(dir, arc, reach, windup, active, combo, (f & 1) != 0, (f & 2) != 0, (f & 4) != 0);
                break;
            }
            case 2:
            {
                var at = r.Vec(); var dir = r.HVec(); float range = r.Half(), speed = r.Half();
                Applying = true;
                G.Spawn(new SwordWave { Position = at, Dir = dir, Range = range, Speed = speed, Damage = 0, Harmless = true });
                Applying = false;
                break;
            }
            case 3:
            {
                var at = r.Vec(); float size = r.Half();
                Applying = true;
                G.Spawn(new LifeMote { Position = at, Caster = av, Alimus = 0, Size = size });
                Applying = false;
                break;
            }
            case 4: av.NetDown(true); break;
            case 5: av.NetDown(false); break;
            case 6:
            {
                // a friend's healing pool: this game's copy heals this game's hero
                var at = r.Vec(); float radius = r.Half(), rate = r.Half(), life = r.Half();
                Applying = true;
                G.Spawn(new HealingPool { Position = at, Radius = radius, Rate = rate, Life = life });
                Applying = false;
                break;
            }
        }
    }

    /// <summary>A creature (the host's) struck someone else's hero: that game takes it.</summary>
    public static float HurtRemote(Player target, float dmg, Vector2 from, float knock, Enemy source)
    {
        var w = new NetOut(Net.Msg.HeroHurt);
        w.Float(dmg).Vec(from).Half(knock).Int(source?.NetId ?? 0);
        Net.SendTo(target.NetOwner, w, true);
        return dmg;
    }

    /// <summary>Healing for someone else's hero (the Vitalist's heal): their game heals it.</summary>
    public static void HealRemote(Player target, float amount)
    {
        var w = new NetOut(Net.Msg.HeroHeal);
        w.Int(target.NetOwner).Float(amount);
        Net.SendAll(w, true);
    }

    /// <summary>A gift from one hero to another: the Warden's barrier, the Vitalist's mending.</summary>
    public enum Boon : byte { Barrier = 1, Mending }

    /// <summary>A boon for someone else's hero: their game gives it (a, b, c: its amount, seconds, a flag).</summary>
    public static void BoonRemote(Player target, Boon kind, float a, float b, float c = 0)
    {
        var w = new NetOut(Net.Msg.HeroBoon);
        w.Int(target.NetOwner).Byte((byte)kind).Float(a).Float(b).Float(c);
        Net.SendAll(w, true);
    }

    /// <summary>A fallen friend brought back (their game gets them up).</summary>
    public static void Revive(Player target)
    {
        var w = new NetOut(Net.Msg.Revive);
        w.Int(target.NetOwner);
        Net.SendAll(w, true);
    }

    // ================================================================== creatures (the host's)

    /// <summary>The host made a creature: every game gets a copy.</summary>
    public static void EnemySpawned(Enemy e)
    {
        e.NetId = NewId();
        Enemies[e.NetId] = e;
        var w = new NetOut(Net.Msg.EnemySpawn);
        w.Int(e.NetId);
        w.Str(e.GetType().Name);
        w.Vec(e.GlobalPosition);
        w.Float(e.MaxHp);
        w.Half(e.Size);
        w.Half(e.BodyRadius);
        w.Half(e.KnockResist);
        w.Byte((byte)((e.Elite ? 1 : 0) | (e.IsGuardian ? 2 : 0) | (e.IsBoss ? 4 : 0) | (e.Tint != null ? 8 : 0)));
        w.Str(e.NamePrefix);
        w.Str(e.Title);
        w.Str(e.DisplayName);
        if (e.Tint is Color t) w.Col(t);
        WriteState(w, e);
        Net.SendAll(w, true);
    }

    private static void WriteState(NetOut w, Enemy e)
    {
        var sub = new NetOut(Net.Msg.Fx); // (a scratch buffer: its type byte is dropped)
        e.NetState(new NetIO(sub));
        var b = sub.Bytes();
        w.Byte((byte)(b.Length - 1));
        for (int k = 1; k < b.Length; k++) w.Byte(b[k]);
    }

    private static void ReadState(NetIn r, Enemy e)
    {
        int len = r.Byte();
        if (e == null) { r.Skip(len); return; }
        var bytes = new byte[len + 1];
        bytes[0] = 0;
        for (int k = 0; k < len; k++) bytes[k + 1] = r.Byte();
        var sub = new NetIn(bytes);
        sub.Byte();
        e.NetState(new NetIO(sub));
    }

    private static void OnEnemySpawn(NetIn r)
    {
        int id = r.Int();
        string type = r.Str();
        var pos = r.Vec();
        float maxHp = r.Float(), size = r.Half(), body = r.Half(), knock = r.Half();
        byte f = r.Byte();
        string prefix = r.Str(), title = r.Str(), display = r.Str();
        Color? tint = (f & 8) != 0 ? r.Col() : null;
        var t = Type.GetType("DaggerCave." + type);
        if (t == null || G.World == null) return;
        if (Activator.CreateInstance(t) is not Enemy e) return;
        e.Puppet = true;
        e.NetId = id;
        e.Position = pos;
        e.MaxHp = maxHp;
        e.Hp = maxHp;
        e.Size = size;
        e.BodyRadius = body;
        e.KnockResist = knock;
        e.Elite = (f & 1) != 0;
        e.IsGuardian = (f & 2) != 0;
        e.IsBoss = (f & 4) != 0;
        e.NamePrefix = prefix;
        e.Title = title;
        e.Tint = tint;
        ReadState(r, e);
        Enemies[id] = e;
        e.NetDisplayName = display;
        G.World.AddChild(e);
        if (e.IsGuardian || e.IsBoss) G.Main?.OnlineGuardianAppeared(e);
    }

    private static void SendWorld()
    {
        var w = new NetOut(Net.Msg.WorldSnap);
        w.UInt(++_snapSeq);
        w.Float(G.RunTime);
        var list = new List<Enemy>();
        foreach (var e in G.Enemies)
        {
            if (e.Puppet || e.NetId == 0 || e.Dead) continue;
            bool near = false;
            foreach (var p in G.Players) if (p.GlobalPosition.DistanceSquaredTo(e.GlobalPosition) < 1500f * 1500f) { near = true; break; }
            if (near || e.IsBoss || e.IsGuardian) list.Add(e);
        }
        w.UShort((ushort)list.Count);
        foreach (var e in list)
        {
            w.Int(e.NetId);
            e.WriteNet(w);
            WriteState(w, e);
        }
        Net.SendAll(w, false);
    }

    private static void OnWorld(NetIn r)
    {
        uint seq = r.UInt();
        if (seq <= _lastSnapSeen && _lastSnapSeen - seq < 1_000_000) return; // an old one, arriving late
        _lastSnapSeen = seq;
        G.RunTime = r.Float();
        int n = r.UShort();
        double now = Now;
        for (int k = 0; k < n; k++)
        {
            int id = r.Int();
            Enemies.TryGetValue(id, out var e);
            if (e != null && !GodotObject.IsInstanceValid(e)) e = null;
            Enemy.ReadNet(r, e, now);
            ReadState(r, e);
        }
    }

    /// <summary>The host's creature died (or was put away): every copy goes too.</summary>
    public static void EnemyGone(Enemy e, bool died)
    {
        if (!Net.IsHost || e.NetId == 0 || e.Puppet || Building) return;
        var w = new NetOut(Net.Msg.EnemyDie);
        w.Int(e.NetId).Bool(died);
        Net.SendAll(w, true);
        Enemies.Remove(e.NetId);
        if (died && e.LastAttacker != 0 && e.LastAttacker != Net.Me)
        {
            var k = new NetOut(Net.Msg.KillCredit);
            Net.SendTo(e.LastAttacker, k, true);
        }
    }

    private static void OnEnemyDie(NetIn r)
    {
        int id = r.Int();
        bool died = r.Bool();
        if (!Enemies.TryGetValue(id, out var e)) return;
        Enemies.Remove(id);
        if (!GodotObject.IsInstanceValid(e)) return;
        e.PuppetGone(died);
    }

    /// <summary>This game's hero struck a creature that the host runs: the host applies the blow.</summary>
    public static float HitPuppet(Enemy e, float dmg, Vector2 knock, Vector2 hitPos)
    {
        var w = new NetOut(Net.Msg.EnemyHit);
        w.Int(e.NetId).Float(dmg).HVec(knock).Vec(hitPos);
        Net.SendTo(1, w, true);
        return dmg * e.PuppetVulnerability;
    }

    /// <summary>Kinds of lasting effect a hero's blow can put on a creature.</summary>
    public enum Effect : byte { Freeze = 1, Interrupt, Weaken, Hex, Bleed }

    public static void EffectPuppet(Enemy e, Effect kind, float a, float b = 0, float c = 0, float d = 0, Vector2 v = default, string label = "")
    {
        var w = new NetOut(Net.Msg.EnemyEffect);
        w.Int(e.NetId).Byte((byte)kind).Float(a).Float(b).Float(c).Float(d).HVec(v).Str(label);
        Net.SendTo(1, w, true);
    }

    private static void OnEnemyHit(int from, NetIn r)
    {
        if (!Net.IsHost) return;
        int id = r.Int();
        float dmg = r.Float();
        var knock = r.HVec();
        var hitPos = r.Vec();
        if (!Enemies.TryGetValue(id, out var e) || !GodotObject.IsInstanceValid(e) || e.Dead) return;
        _striker = from;
        Scope++;
        try { e.Hurt(dmg, knock, hitPos); }
        finally { Scope--; _striker = 0; }
    }

    private static void OnEnemyEffect(int from, NetIn r)
    {
        if (!Net.IsHost) return;
        int id = r.Int();
        var kind = (Effect)r.Byte();
        float a = r.Float(), b = r.Float(), c = r.Float(), d = r.Float();
        var v = r.HVec();
        string label = r.Str();
        if (!Enemies.TryGetValue(id, out var e) || !GodotObject.IsInstanceValid(e) || e.Dead) return;
        _striker = from;
        Scope++;
        try
        {
            switch (kind)
            {
                case Effect.Freeze: e.Freeze(a, b > 0.5f); break;
                case Effect.Interrupt: e.Interrupt(v, a, label == "" ? "BROKEN" : label); break;
                case Effect.Weaken: e.Weaken(a, b); break;
                case Effect.Hex: e.Hex(a, b, c, d); break;
                case Effect.Bleed: e.Bleed(a, b); break;
            }
        }
        finally { Scope--; _striker = 0; }
    }

    // ================================================================== things the host's world makes

    /// <summary>Something was just added to the world here: send it on if the others need it.</summary>
    public static void Spawned(Node n)
    {
        if (!Net.Online || Applying) return;
        if (Net.IsHost && Scope > 0) SendProp(n);
    }

    private static void SendProp(Node n)
    {
        var w = new NetOut(Net.Msg.PropSpawn);
        int id = NewId();
        switch (n)
        {
            case EnemyProjectile pr:
                w.Byte(1).Int(id).Vec(pr.GlobalPosition).Vec(pr.Vel).Half(pr.Grav).Half(pr.Radius).Float(pr.Damage).Str(pr.Kind).Half(pr.Life).Int(pr.Source?.NetId ?? 0);
                break;
            case Shockwave sw:
                w.Byte(2).Int(id).Vec(sw.GlobalPosition).SByte((sbyte)Math.Sign(sw.Dir)).Half(sw.Speed).Float(sw.Damage).Half(sw.Life).Half(sw.Size).Int(sw.Source?.NetId ?? 0);
                break;
            case FallingRock fr:
                w.Byte(3).Int(id).Vec(fr.GlobalPosition).Float(fr.Damage).Int(fr.Source?.NetId ?? 0);
                break;
            case LavaPuddle lp:
                w.Byte(4).Int(id).Vec(lp.GlobalPosition).Int(lp.Source?.NetId ?? 0);
                break;
            case SporeCloud sc:
                w.Byte(5).Int(id).Vec(sc.GlobalPosition).Half(sc.Radius).Half(sc.Life).Int(sc.Source?.NetId ?? 0);
                break;
            case XpOrb xp:
                w.Byte(6).Int(id).Vec(xp.GlobalPosition).HVec(xp.Vel).Short((short)xp.Value);
                break;
            case HeartPickup h:
                w.Byte(7).Int(id).Vec(h.GlobalPosition);
                break;
            case PotionPickup pp:
                w.Byte(8).Int(id).Vec(pp.GlobalPosition);
                break;
            case Chest ch:
                w.Byte(9).Int(id).Vec(ch.GlobalPosition);
                break;
            case Portal po:
                w.Byte(10).Int(id).Vec(po.GlobalPosition).Byte((byte)(po.To?.Id ?? BiomeId.Slime)).Int(po.Depth).Str(po.Label);
                break;
            default:
                return;
        }
        Track((Node2D)n, id);
        Net.SendAll(w, true);
    }

    private static void OnProp(NetIn r)
    {
        byte kind = r.Byte();
        int id = r.Int();
        Node2D n;
        Enemy Src(int sid) => sid != 0 && Enemies.TryGetValue(sid, out var e) && GodotObject.IsInstanceValid(e) ? e : null;
        switch (kind)
        {
            case 1:
            {
                var pos = r.Vec(); var vel = r.Vec(); float grav = r.Half(), radius = r.Half(), dmg = r.Float(); string k = r.Str(); float life = r.Half();
                n = new EnemyProjectile { Position = pos, Vel = vel, Grav = grav, Radius = radius, Damage = dmg, Kind = k, Life = life, Source = Src(r.Int()) };
                break;
            }
            case 2:
            {
                var pos = r.Vec(); float dir = r.SByte(), speed = r.Half(), dmg = r.Float(), life = r.Half(), size = r.Half();
                n = new Shockwave { Position = pos, Dir = dir, Speed = speed, Damage = dmg, Life = life, Size = size, Source = Src(r.Int()) };
                break;
            }
            case 3: { var pos = r.Vec(); float dmg = r.Float(); n = new FallingRock { Position = pos, Damage = dmg, Source = Src(r.Int()) }; break; }
            case 4: { var pos = r.Vec(); n = new LavaPuddle { Position = pos, Source = Src(r.Int()) }; break; }
            case 5: { var pos = r.Vec(); float rad = r.Half(), life = r.Half(); n = new SporeCloud { Position = pos, Radius = rad, Life = life, Source = Src(r.Int()) }; break; }
            case 6: { var pos = r.Vec(); var vel = r.HVec(); int value = r.Short(); n = new XpOrb { Position = pos, Vel = vel, Value = value, Puppet = true }; break; }
            case 7: n = new HeartPickup { Position = r.Vec(), Puppet = true }; break;
            case 8: n = new PotionPickup { Position = r.Vec(), Puppet = true }; break;
            case 9: n = new Chest { Position = r.Vec() }; break;
            case 10:
            {
                var pos = r.Vec(); var biome = Biomes.Get((BiomeId)r.Byte()); int depth = r.Int(); string label = r.Str();
                n = new Portal { Position = pos, To = biome, Depth = depth, Label = label };
                G.Main?.NoteExit(pos);
                break;
            }
            default: return;
        }
        Track(n, id);
        Applying = true;
        try { G.Spawn(n); }
        finally { Applying = false; }
    }

    /// <summary>Something that exists in every game went away here (taken, blocked, spent): the others let theirs go.</summary>
    public static void PropGone(Node2D n, bool quiet = false)
    {
        if (!Net.Online || Applying || Building) return;
        int id = IdOf(n);
        if (id == 0) return;
        var w = new NetOut(Net.Msg.PropGone);
        w.Int(id).Bool(quiet);
        Net.SendAll(w, true);
    }

    private static void OnPropGone(NetIn r)
    {
        int id = r.Int();
        bool quiet = r.Bool();
        if (!Props.TryGetValue(id, out var n) || !GodotObject.IsInstanceValid(n)) return;
        Applying = true;
        try
        {
            switch (n)
            {
                case EnemyProjectile pr when !quiet: pr.Deflect(); break;
                case XpOrb or HeartPickup or PotionPickup when !quiet: G.Fx.Glint(n.GlobalPosition, new Color(1f, 1f, 1f), 5); n.QueueFree(); break;
                default: n.QueueFree(); break;
            }
        }
        finally { Applying = false; }
    }

    // ================================================================== pickups, chests, exits (the host decides)

    /// <summary>Host: an orb was taken by someone: everyone gains the experience.</summary>
    public static void XpTaken(XpOrb orb, int value)
    {
        PropGone(orb);
        var w = new NetOut(Net.Msg.Xp);
        w.Int(value);
        Net.SendAll(w, true);
    }

    /// <summary>Host: someone else's hero touched a heart or a potion.</summary>
    public static void GivePickup(Player to, byte kind, float amount)
    {
        var w = new NetOut(Net.Msg.Pickup);
        w.Byte(kind).Float(amount);
        Net.SendTo(to.NetOwner, w, true);
    }

    // Chests: one player looks in at a time (the host says who). The first to look deals the
    // cards (for the whole party) and everyone learns them; taking one spends the chest for all,
    // leaving it closes it again with the same cards for anyone to look at.

    /// <summary>The heroes in this run (a chest's cards are dealt for all of them).</summary>
    public static IReadOnlyCollection<HeroKind> PartyHeroes() => Net.Peers.Values.Select(p => p.Hero).Distinct().ToList();

    private static Chest ChestById(int id) => Props.TryGetValue(id, out var n) && n is Chest c && GodotObject.IsInstanceValid(c) ? c : null;

    /// <summary>This game's hero is at a chest: ask the host whether it may look in.</summary>
    public static void AskChest(Chest c)
    {
        int id = IdOf(c);
        if (id == 0) return;
        if (Net.IsHost) { LookInChest(id, Net.Me); return; }
        var w = new NetOut(Net.Msg.ChestOpen);
        w.Int(id);
        Net.SendTo(1, w, true);
    }

    /// <summary>Host: <paramref name="viewer"/> wants to look in a chest: theirs if nobody else is looking.</summary>
    private static void LookInChest(int id, int viewer)
    {
        var c = ChestById(id);
        if (c == null || c.Open) return;
        if (c.LookingBy != 0 && c.LookingBy != viewer)
        {
            // someone else has it open: tell the one who asked
            var busy = new NetOut(Net.Msg.ChestLook);
            busy.Int(id).Int(c.LookingBy).Str(c.Cards != null ? string.Join(",", c.Cards) : "");
            if (viewer == Net.Me) OnChestLook(new NetIn(busy.Bytes()), skipType: true);
            else Net.SendTo(viewer, busy, true);
            return;
        }
        SendChestLook(c, id, viewer);
    }

    /// <summary>Host: who is looking in a chest now (0 = nobody), to everyone.</summary>
    private static void SendChestLook(Chest c, int id, int viewer)
    {
        var w = new NetOut(Net.Msg.ChestLook);
        w.Int(id).Int(viewer).Str(c.Cards != null ? string.Join(",", c.Cards) : "");
        Net.SendAll(w, true);
        OnChestLook(new NetIn(w.Bytes()), skipType: true);
    }

    private static void OnChestLook(NetIn r, bool skipType = false)
    {
        if (skipType) r.Byte();
        int id = r.Int(), viewer = r.Int();
        string cards = r.Str();
        var c = ChestById(id);
        if (c == null || c.Open) return;
        bool asked = c.Asked;
        c.LookingBy = viewer;
        if (cards != "") c.Cards = cards.Split(',');
        if (viewer == Net.Me) c.Look();
        else if (asked && viewer != 0) G.Main?.OnlineBanner($"{Net.NameOf(viewer)} IS LOOKING IN IT", 1.4f);
    }

    /// <summary>The cards were just dealt here: everyone keeps the same ones.</summary>
    public static void ChestCards(Chest c)
    {
        if (!Net.Online || c.Cards == null) return;
        int id = IdOf(c);
        if (id == 0) return;
        var w = new NetOut(Net.Msg.ChestCards);
        w.Int(id).Str(string.Join(",", c.Cards));
        Net.SendAll(w, true);
    }

    private static void OnChestCards(NetIn r)
    {
        int id = r.Int();
        string cards = r.Str();
        var c = ChestById(id);
        if (c != null && !c.Open && cards != "") c.Cards = cards.Split(',');
    }

    /// <summary>This game's hero took a card (the chest is spent) or left it (it closes again).</summary>
    public static void ChestDone(Chest c, bool taken)
    {
        if (!Net.Online) return;
        int id = IdOf(c);
        if (id == 0) return;
        if (Net.IsHost) { OnChestDone(Net.Me, id, taken); return; }
        var w = new NetOut(Net.Msg.ChestDone);
        w.Int(id).Bool(taken);
        Net.SendTo(1, w, true);
    }

    /// <summary>Host: a player finished with a chest.</summary>
    private static void OnChestDone(int from, int id, bool taken)
    {
        var c = ChestById(id);
        if (c == null || c.Open || c.LookingBy != from) return;
        if (!taken) { SendChestLook(c, id, 0); return; }
        var w = new NetOut(Net.Msg.ChestOpened);
        w.Int(id).Int(from);
        Net.SendAll(w, true);
        c.OpenBy(from);
    }

    /// <summary>Host: a player left: any chest they were looking in is free again.</summary>
    private static void FreeChestsOf(int peer)
    {
        if (!Net.IsHost) return;
        foreach (var (id, n) in Props.ToList())
            if (n is Chest c && GodotObject.IsInstanceValid(c) && !c.Open && c.LookingBy == peer) SendChestLook(c, id, 0);
    }

    private static void OnChestOpened(NetIn r)
    {
        int id = r.Int(), opener = r.Int();
        var c = ChestById(id);
        if (c != null && !c.Open) c.OpenBy(opener);
    }

    /// <summary>This game's hero stepped into an exit (or out of it again).</summary>
    public static void AtExit(Portal p, bool ready)
    {
        int id = IdOf(p);
        if (Net.IsHost) { SetAtExit(Net.Me, ready ? id : 0); return; }
        var w = new NetOut(Net.Msg.ExitReady);
        w.Int(ready ? id : 0);
        Net.SendTo(1, w, true);
    }

    private static readonly Dictionary<int, int> _exitOf = new();
    public static int ExitOf(int peer) => _exitOf.TryGetValue(peer, out int id) ? id : 0;

    private static void SetAtExit(int peer, int portalId)
    {
        if (!Net.IsHost) return;
        _exitOf[peer] = portalId;
        if (Net.Peers.TryGetValue(peer, out var p)) p.AtExit = portalId != 0;
        Net.SendExitState();
        G.Main?.CheckExits();
    }

    /// <summary>Host: the exit everyone still standing is waiting at, or null.</summary>
    public static Portal ExitEveryoneIsAt()
    {
        int common = 0;
        foreach (var p in Net.Peers.Values)
        {
            var av = p.Avatar;
            if (av == null || !GodotObject.IsInstanceValid(av) || av.Dead) continue;
            int at = ExitOf(p.Id);
            if (at == 0) return null;
            if (common == 0) common = at;
            else if (common != at) return null;
        }
        return common != 0 && Props.TryGetValue(common, out var n) && n is Portal portal ? portal : null;
    }

    public static void ClearExits() => _exitOf.Clear();

    // ================================================================== run flow (the host decides)

    public static void SendStart(int seed)
    {
        var w = new NetOut(Net.Msg.Start);
        w.Int(seed);
        Net.SendAll(w, true);
    }

    public static void SendLevel(BiomeId biome, int depth, int seed)
    {
        var w = new NetOut(Net.Msg.Level);
        w.Byte((byte)biome).Int(depth).Int(seed).Float(G.RunTime);
        Net.SendAll(w, true);
    }

    public static void SendBanner(string text, float seconds)
    {
        if (!Net.IsHost) return;
        var w = new NetOut(Net.Msg.Banner);
        w.Str(text).Half(seconds);
        Net.SendAll(w, true);
    }

    public static void SendMusic(string which)
    {
        if (!Net.IsHost) return;
        var w = new NetOut(Net.Msg.Music);
        w.Str(which);
        Net.SendAll(w, true);
    }

    public static void SendGuardianDown(int embers, bool dragon, string name)
    {
        var w = new NetOut(Net.Msg.GuardianDown);
        w.Int(embers).Bool(dragon).Str(name);
        Net.SendAll(w, true);
    }

    public static void SendRunOver(bool victory)
    {
        var w = new NetOut(Net.Msg.RunOver);
        w.Bool(victory);
        Net.SendAll(w, true);
    }

    /// <summary>Everyone walked out of the cave mouth: the run is over, and nothing from it is kept.</summary>
    public static void SendLeftCave() => Net.SendAll(new NetOut(Net.Msg.LeftCave), true);

    // ================================================================== effects and sounds

    /// <summary>Starts recording one effect (the caller writes its arguments into the returned buffer).</summary>
    public static NetOut FxBegin(byte op)
    {
        _fx ??= new NetOut(Net.Msg.Fx);
        _fx.Byte(op);
        _fxCount++;
        return _fx;
    }

    private static void FlushFx()
    {
        if (_fx == null || _fxCount == 0) return;
        if (Net.Online && Net.InRun) Net.SendAll(_fx, true);
        _fx = null;
        _fxCount = 0;
    }

    private static void OnFx(NetIn r)
    {
        if (G.Fx == null) return;
        Applying = true;
        try
        {
            while (r.More)
            {
                byte op = r.Byte();
                if (op == FxLayer.SoundOp) G.Sfx?.PlayNet(r);
                else G.Fx.ApplyNet(op, r);
            }
        }
        finally { Applying = false; }
    }

    // ================================================================== dispatch

    public static void Handle(Net.Msg type, int from, NetIn r)
    {
        switch (type)
        {
            case Net.Msg.Start: G.Main?.StartOnlineRun(r.Int()); break;
            case Net.Msg.Level: { var b = (BiomeId)r.Byte(); int depth = r.Int(), seed = r.Int(); float rt = r.Float(); G.Main?.OnlineLevel(b, depth, seed, rt); break; }
            case Net.Msg.HeroState:
            {
                int owner = r.Int();
                var av = Net.Peer(owner)?.Avatar;
                if (av != null && GodotObject.IsInstanceValid(av) && av.IsRemote) av.ReadNet(r, Now);
                break;
            }
            case Net.Msg.HeroEvent: OnHeroEvent(r); break;
            case Net.Msg.WorldSnap: if (!Net.IsHost) OnWorld(r); break;
            case Net.Msg.EnemySpawn: if (!Net.IsHost) OnEnemySpawn(r); break;
            case Net.Msg.EnemyDie: if (!Net.IsHost) OnEnemyDie(r); break;
            case Net.Msg.EnemyHit: OnEnemyHit(from, r); break;
            case Net.Msg.EnemyEffect: OnEnemyEffect(from, r); break;
            case Net.Msg.HeroHurt:
            {
                float dmg = r.Float(); var at = r.Vec(); float knock = r.Half(); int src = r.Int();
                Enemies.TryGetValue(src, out var e);
                if (e != null && !GodotObject.IsInstanceValid(e)) e = null;
                var p = G.Player;
                if (p != null && !p.Dead) { Scope++; try { p.Hurt(dmg, at, knock, e); } finally { Scope--; } }
                break;
            }
            case Net.Msg.HeroHeal:
            {
                int owner = r.Int(); float amount = r.Float();
                if (owner == Net.Me && G.Player != null) { Scope++; try { G.Player.Heal(amount); } finally { Scope--; } }
                break;
            }
            case Net.Msg.HeroBoon:
            {
                int owner = r.Int(); var kind = (Boon)r.Byte(); float a = r.Float(), b = r.Float(), c = r.Float();
                var p = G.Player;
                if (owner != Net.Me || p == null || p.Dead) break;
                Scope++;
                try
                {
                    if (kind == Boon.Barrier) p.GiveBarrier(a, b);
                    else if (kind == Boon.Mending) p.GiveMending(a, b, c > 0.5f);
                }
                finally { Scope--; }
                break;
            }
            case Net.Msg.Revive:
            {
                int owner = r.Int();
                if (owner == Net.Me) G.Player?.Revive();
                break;
            }
            case Net.Msg.PropSpawn: if (!Net.IsHost) OnProp(r); break;
            case Net.Msg.PropGone: OnPropGone(r); break;
            case Net.Msg.Xp: G.Player?.AddXp(r.Int()); break;
            case Net.Msg.Pickup:
            {
                byte kind = r.Byte(); float amount = r.Float();
                var p = G.Player;
                if (p == null || p.Dead) break;
                Scope++;
                try
                {
                    if (kind == 1) p.Heal(amount);
                    else if (kind == 2) p.GainPotion();
                }
                finally { Scope--; }
                break;
            }
            case Net.Msg.ChestOpen: if (Net.IsHost) LookInChest(r.Int(), from); break;
            case Net.Msg.ChestLook: OnChestLook(r); break;
            case Net.Msg.ChestCards: OnChestCards(r); break;
            case Net.Msg.ChestDone: if (Net.IsHost) { int id = r.Int(); OnChestDone(from, id, r.Bool()); } break;
            case Net.Msg.ChestOpened: OnChestOpened(r); break;
            case Net.Msg.ExitReady: SetAtExit(from, r.Int()); break;
            case Net.Msg.Fx: OnFx(r); break;
            case Net.Msg.Banner: G.Main?.OnlineBanner(r.Str(), r.Half()); break;
            case Net.Msg.Music: G.Sfx?.SetMusic(r.Str()); break;
            case Net.Msg.GuardianDown: { int embers = r.Int(); bool dragon = r.Bool(); string name = r.Str(); G.Main?.OnlineGuardianDown(embers, dragon, name); break; }
            case Net.Msg.RunOver: G.Main?.OnlineRunOver(r.Bool()); break;
            case Net.Msg.LeftCave: G.Main?.OnlineLeftCave(); break;
            case Net.Msg.KillCredit: G.Player?.OnKill(); break;
            case Net.Msg.Dealt: G.Player?.OnDealtDamage(r.Float()); break;
            case Net.Msg.Test: G.Main?.OnNetTest(from, r.Str()); break;
        }
    }
}

/// <summary>
/// Reads or writes the same fields in the same order (a creature's own extra state for its copies:
/// a frog's tongue, a spider's thread), so one method does both.
/// </summary>
public sealed class NetIO
{
    public readonly bool Writing;
    private readonly NetOut _w;
    private readonly NetIn _r;
    public NetIO(NetOut w) { Writing = true; _w = w; }
    public NetIO(NetIn r) { Writing = false; _r = r; }
    public void Sync(ref float v) { if (Writing) _w.Float(v); else v = _r.Float(); }
    public void Sync(ref int v) { if (Writing) _w.Int(v); else v = _r.Int(); }
    public void Sync(ref bool v) { if (Writing) _w.Bool(v); else v = _r.Bool(); }
    public void Sync(ref Vector2 v) { if (Writing) _w.Vec(v); else v = _r.Vec(); }
    /// <summary>An enum or small state number (0..255).</summary>
    public void SyncByte<T>(ref T v) where T : struct, Enum
    {
        if (Writing) _w.Byte(Convert.ToByte(v));
        else v = (T)Enum.ToObject(typeof(T), _r.Byte());
    }
}

/// <summary>Smooths a puppet's movement: positions arrive twenty times a second and it's shown a moment behind, between two of them.</summary>
public sealed class NetInterp
{
    private readonly (double t, Vector2 p, Vector2 v)[] _buf = new (double, Vector2, Vector2)[10];
    private int _n;
    public bool Has => _n > 0;

    public void Push(double t, Vector2 p, Vector2 v)
    {
        // a jump across the map (a teleport) starts over
        if (_n > 0 && _buf[_n - 1].p.DistanceTo(p) > 300f) _n = 0;
        if (_n == _buf.Length) { Array.Copy(_buf, 1, _buf, 0, _n - 1); _n--; }
        _buf[_n++] = (t, p, v);
    }

    public void Clear() => _n = 0;

    public bool Sample(double t, out Vector2 p, out Vector2 v)
    {
        p = default; v = default;
        if (_n == 0) return false;
        if (t <= _buf[0].t) { p = _buf[0].p; v = _buf[0].v; return true; }
        for (int k = 1; k < _n; k++)
        {
            if (t > _buf[k].t) continue;
            var a = _buf[k - 1]; var b = _buf[k];
            float f = (float)((t - a.t) / Math.Max(1e-4, b.t - a.t));
            p = a.p.Lerp(b.p, f);
            v = a.v.Lerp(b.v, f);
            return true;
        }
        // past the newest: carry on a little along its velocity (at most a tenth of a second)
        var last = _buf[_n - 1];
        float ahead = (float)Math.Min(0.1, t - last.t);
        p = last.p + last.v * ahead;
        v = last.v;
        return true;
    }
}
