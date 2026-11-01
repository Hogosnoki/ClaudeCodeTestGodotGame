using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

/// <summary>
/// A generator (the Gauntlet, <see cref="Tune.Gauntlet"/>): a cairn of dark stones heaped over a rift, pouring out the cave's own creatures
/// (the biome's walkers, fliers or swimmers, whatever suits the spot) as minions, until it is broken. It has three tiers by its health: whole,
/// it sends them quickest and keeps the most alive; struck down a tier, slower and fewer; it shrinks as it goes. Broken, it bursts into a
/// bonus: gold, often food, and now and then a power for every hero near (Fury or Haste).
/// </summary>
public partial class Generator : Enemy
{
    public override string HitSound => "hit_stone";
    private readonly List<Enemy> _brood = new();
    private float _spawnT = 0.6f;

    /// <summary>The trial room it was raised in (null: one of the level's own).</summary>
    public Room Trial;
    /// <summary>For the tests: minions sent out so far, and those alive now.</summary>
    public int Released { get; private set; }
    public int BroodAlive { get { Prune(); return _brood.Count; } }
    /// <summary>3 whole, 2 struck, 1 nearly broken.</summary>
    public int Tier => Hp > MaxHp * 2f / 3f ? 3 : Hp > MaxHp / 3f ? 2 : 1;

    protected override bool UsesGravity => false;

    public Generator()
    {
        MaxHp = Tune.Gauntlet.GeneratorHp;
        BodyRadius = 18;
        ContactDamage = 0;
        XpValue = Tune.Gauntlet.GeneratorXp;
        KnockResist = 1f;
    }

    protected override void Setup()
    {
        DisplayName = "Generator";
        UseSprite("generator");
        Anim.AllowTurns = false;
        MotionMode = MotionModeEnum.Floating;
        ManualMove = true;
    }

    private void Prune() => _brood.RemoveAll(e => e == null || !IsInstanceValid(e) || e.Dead);

    protected override void Think(float dt)
    {
        Velocity = Vector2.Zero;
        // (it shrinks a little with each tier lost)
        Size = 0.8f + 0.12f * Tier;
        if (!Awake)
        {
            foreach (var h in G.Players)
                if (h != null && IsInstanceValid(h) && !h.Dead && h.GlobalPosition.DistanceTo(GlobalPosition) < Tune.Gauntlet.WakeRange) { Awake = true; break; }
            if (!Awake) return;
        }
        Prune();
        int tier = Tier;
        int cap = (int)Tune.Gauntlet.Cap[tier] + G.Depth / Math.Max(1, Tune.Gauntlet.CapPerDepth);
        if ((_spawnT -= dt) > 0f || _brood.Count >= cap) return;
        _spawnT = Tune.Gauntlet.SpawnEvery[tier] * G.Range(0.8f, 1.2f);
        Release();
    }

    /// <summary>A creature crawls out of the rift: what suits the spot, made a minion.</summary>
    private void Release()
    {
        var mouth = GlobalPosition + new Vector2(G.Range(-8f, 8f), -4f);
        var e = Main.EntrantFor(mouth, out var pos);
        // (a biome with nothing that comes in from outside: its own residents crawl out instead)
        if (e == null)
        {
            bool wet = G.Cave.IsWater(mouth);
            if (!G.Biome.Residents.TryGetValue(wet ? SpawnKind.Water : SpawnKind.Ground, out var table) || table.Count == 0) return;
            e = Biomes.Pick(table).Make();
            pos = !wet && G.Cave.FindFloor(mouth, 140, out var floor) ? floor + new Vector2(0, -12) : mouth;
        }
        if (e == null) return;
        e.Minion = true;
        e.Position = G.Cave.IsSolid(pos) ? mouth : pos;
        e.Engage();
        G.World.AddChild(e);
        // (a minion: half the health, a little less to learn from)
        e.MaxHp *= Tune.Gauntlet.MinionHp;
        e.Hp = e.MaxHp;
        e.XpValue = Math.Max(1, (int)MathF.Round(e.XpValue * Tune.Gauntlet.MinionXp));
        _brood.Add(e);
        Released++;
        var col = new Color(0.65f, 0.4f, 1f);
        G.Fx.Burst(mouth, new Color(col, 0.85f), 8, 90, 1.6f, 0.35f, -40f);
        G.Fx.Ring(mouth, 12, new Color(col, 0.7f), 0.2f, FxLayer.RingStyle.Burst);
        G.Sfx.Play("dodge", mouth, -10, 0.15f, 0.55f);
    }

    protected override void Die()
    {
        if (Dead) return;
        // (made as a creature's own drops are: in every game, the gold a copy of the host's; the power the host's alone to give)
        NetSync.Scope++;
        try { Bonus(); }
        finally { NetSync.Scope--; }
        base.Die();
    }

    /// <summary>Broken: gold, often food, and now and then a power for every hero near.</summary>
    private void Bonus()
    {
        var at = GlobalPosition;
        G.Spawn(new CoinPickup { Position = at, Count = Tune.Gauntlet.BonusGold + G.Depth, Dropped = true, Vel = new Vector2(G.Range(-40, 40), -160), Puppet = Net.Online && !Net.IsHost });
        if (G.Chance(Tune.Gauntlet.FoodChance)) G.Spawn(new HeartPickup { Position = at + new Vector2(G.Range(-10, 10), -6) });
        Broken++;
        if ((!Net.Online || Net.IsHost) && G.Chance(Tune.Gauntlet.PowerChance))
        {
            bool fury = G.Chance(0.5f);
            int kind = fury ? Player.BuffFury : Player.BuffHaste;
            foreach (var h in G.Players.ToArray())
                if (h != null && IsInstanceValid(h) && !h.Dead && h.GlobalPosition.DistanceTo(at) <= Tune.Gauntlet.PowerRange) h.GiveBuff(kind, Tune.Gauntlet.PowerSeconds);
            Powers++;
            G.Fx.Text(at + new Vector2(0, -30), fury ? "FURY" : "HASTE", fury ? new Color(1f, 0.45f, 0.25f) : new Color(0.45f, 1f, 0.6f), 14, 1f);
        }
        G.Fx.Explosion(at, new Color(0.6f, 0.38f, 1f), 1.1f);
        G.Fx.AddShake(3f);
        G.Sfx.Play("rock", at, -2, 0.1f, 0.8f);
    }

    /// <summary>For the tests: generators broken this run, and the powers they gave.</summary>
    public static int Broken, Powers;

    protected override void Animate() => Anim.Loop("idle", 1f);
    protected override Color BloodColor => new(0.45f, 0.4f, 0.5f);

    public override void _Draw() => DrawHealthBar();
}
